using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using API.Common.TenantResolution;
using API.Features.Portal.DTOs;
using Domain.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Security.Infrastructure.Persistence;
using Security.Interfaces;
using Security.Services;

namespace API.Features.Portal.Services;

public class CustomerAuthService : ICustomerAuthService
{
    private readonly SecurityDbContext _securityContext;
    private readonly DomainDbContext _domainContext;
    private readonly ITokenService _tokenService;
    private readonly PasswordService _passwordService;
    private readonly ITenantContext _tenantContext;

    public CustomerAuthService(
        SecurityDbContext securityContext,
        DomainDbContext domainContext,
        ITokenService tokenService,
        PasswordService passwordService,
        ITenantContext tenantContext)
    {
        _securityContext = securityContext ?? throw new ArgumentNullException(nameof(securityContext));
        _domainContext = domainContext ?? throw new ArgumentNullException(nameof(domainContext));
        _tokenService = tokenService ?? throw new ArgumentNullException(nameof(tokenService));
        _passwordService = passwordService ?? throw new ArgumentNullException(nameof(passwordService));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
    }

    public async Task<CustomerLoginResponse> LoginAsync(CustomerLoginRequest request, CancellationToken ct = default)
    {
        if (request == null)
            throw new ArgumentException("Login credentials are required.", nameof(request));

        if (string.IsNullOrWhiteSpace(request.Email))
            throw new ArgumentException("Email is required.", nameof(request.Email));

        if (string.IsNullOrWhiteSpace(request.Password))
            throw new ArgumentException("Password is required.", nameof(request.Password));

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        // 1. Authenticate user in SecurityDbContext
        var user = await _securityContext.Users
            .FirstOrDefaultAsync(u => u.Username.ToLower() == normalizedEmail || (u.Email != null && u.Email.ToLower() == normalizedEmail), ct);

        if (user == null || !user.IsActive)
            throw new UnauthorizedAccessException("Invalid email or password.");

        bool isPasswordValid = _passwordService.VerifyPassword(request.Password, user.PasswordHash);
        if (!isPasswordValid)
            throw new UnauthorizedAccessException("Invalid email or password.");

        if (!user.TenantId.HasValue)
            throw new UnauthorizedAccessException("User is not associated with an organization tenant.");

        var tenantId = user.TenantId.Value;

        // 2. Validate tenant domain consistency if host-specific
        if (_tenantContext.IsTenantDomainRequest && _tenantContext.TenantId.HasValue && _tenantContext.TenantId.Value != tenantId)
        {
            throw new UnauthorizedAccessException("Invalid credentials for this organization.");
        }

        // 3. Resolve active Contact in DomainDbContext
        var contact = await _domainContext.Contacts
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && (c.ContactId == user.Id || (c.Email != null && c.Email.ToLower() == normalizedEmail)) && c.IsActive, ct);

        if (contact == null)
            throw new UnauthorizedAccessException("No active contact record found for user.");

        // 4. Resolve active CustomerContact link
        var customerContact = await _domainContext.CustomerContacts
            .FirstOrDefaultAsync(cc => cc.TenantId == tenantId && cc.ContactId == contact.ContactId && cc.IsActive, ct);

        if (customerContact == null)
            throw new UnauthorizedAccessException("User is not linked to an active customer company.");

        // 5. Resolve active Customer master record
        var customer = await _domainContext.Customers
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.CustomerId == customerContact.CustomerId && c.IsActive, ct);

        if (customer == null)
            throw new UnauthorizedAccessException("Customer company is inactive or not found.");

        // 6. Resolve customer roles & permissions
        var roles = await _securityContext.UserRoles
            .Where(ur => ur.UserId == user.Id)
            .Select(ur => ur.Role.Name)
            .ToListAsync(ct);

        if (roles.Count == 0)
        {
            roles = ["CUSTOMER_COMPANY_ADMIN"];
        }

        var permissions = new List<string>
        {
            "PORTAL_PROFILE.VIEW",
            "PORTAL_SALES.VIEW",
            "PORTAL_ORDER.VIEW",
            "PORTAL_DELIVERY.VIEW",
            "PORTAL_INVOICE.VIEW"
        };

        // Automatic purchasing/acceptance authority is granted if primary contact (or auto-promoted if no primary exists)
        if (!customerContact.IsPrimary)
        {
            var hasOtherPrimary = await _domainContext.CustomerContacts.AnyAsync(cc =>
                cc.TenantId == tenantId &&
                cc.CustomerId == customer.CustomerId &&
                cc.ContactId != contact.ContactId &&
                cc.IsPrimary &&
                cc.IsActive, ct);

            if (!hasOtherPrimary)
            {
                customerContact.SetAsPrimary(true);
                await _domainContext.SaveChangesAsync(ct);
            }
        }

        if (customerContact.IsPrimary)
        {
            if (!permissions.Contains("PORTAL_QUOTATION.REQUEST"))
                permissions.Add("PORTAL_QUOTATION.REQUEST");
            if (!permissions.Contains("PORTAL.QUOTATION.REQUEST"))
                permissions.Add("PORTAL.QUOTATION.REQUEST");
            if (!permissions.Contains("PORTAL_QUOTATION.ACCEPT"))
                permissions.Add("PORTAL_QUOTATION.ACCEPT");
            if (!permissions.Contains("PORTAL.QUOTATION.ACCEPT"))
                permissions.Add("PORTAL.QUOTATION.ACCEPT");
            if (!permissions.Contains("PORTAL_ORDER.SUBMIT"))
                permissions.Add("PORTAL_ORDER.SUBMIT");
            if (!permissions.Contains("PORTAL.ORDER.SUBMIT"))
                permissions.Add("PORTAL.ORDER.SUBMIT");
        }

        // 7. Issue Customer JWT
        var token = _tokenService.GenerateCustomerAccessToken(
            user.Id,
            user.Username,
            tenantId,
            customer.CustomerId,
            contact.ContactId,
            roles,
            permissions);

        int lifetimeMinutes = _tokenService.GetAccessTokenLifetimeMinutes();

        return new CustomerLoginResponse
        {
            AccessToken = token,
            TokenType = "Bearer",
            ExpiresIn = lifetimeMinutes * 60,
            UserId = user.Id,
            TenantId = tenantId,
            CustomerId = customer.CustomerId,
            ContactId = contact.ContactId,
            CustomerName = customer.CustomerName,
            ContactName = contact.ContactName,
            Email = user.Email ?? user.Username,
            PortalType = "CUSTOMER",
            Roles = roles,
            Permissions = permissions
        };
    }

    public async Task<CustomerProfileDto> GetProfileAsync(Guid userId, Guid tenantId, CancellationToken ct = default)
    {
        var user = await _securityContext.Users
            .FirstOrDefaultAsync(u => u.Id == userId && u.TenantId == tenantId && u.IsActive, ct);

        if (user == null)
            throw new UnauthorizedAccessException("User account not found or inactive.");

        var normalizedEmail = (user.Email ?? user.Username).Trim().ToLowerInvariant();

        var contact = await _domainContext.Contacts
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && (c.ContactId == userId || (c.Email != null && c.Email.ToLower() == normalizedEmail)) && c.IsActive, ct);

        if (contact == null)
            throw new UnauthorizedAccessException("Contact record not found or inactive.");

        var customerContact = await _domainContext.CustomerContacts
            .FirstOrDefaultAsync(cc => cc.TenantId == tenantId && cc.ContactId == contact.ContactId && cc.IsActive, ct);

        if (customerContact == null)
            throw new UnauthorizedAccessException("Customer association not found or inactive.");

        var customer = await _domainContext.Customers
            .Include(c => c.Currency)
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.CustomerId == customerContact.CustomerId && c.IsActive, ct);

        if (customer == null)
            throw new UnauthorizedAccessException("Customer company not found or inactive.");

        var roles = await _securityContext.UserRoles
            .Where(ur => ur.UserId == userId)
            .Select(ur => ur.Role.Name)
            .ToListAsync(ct);

        var permissions = new List<string>
        {
            "PORTAL_PROFILE.VIEW",
            "PORTAL_SALES.VIEW",
            "PORTAL_ORDER.VIEW",
            "PORTAL_DELIVERY.VIEW",
            "PORTAL_INVOICE.VIEW"
        };

        if (!customerContact.IsPrimary)
        {
            var hasOtherPrimary = await _domainContext.CustomerContacts.AnyAsync(cc =>
                cc.TenantId == tenantId &&
                cc.CustomerId == customer.CustomerId &&
                cc.ContactId != contact.ContactId &&
                cc.IsPrimary &&
                cc.IsActive, ct);

            if (!hasOtherPrimary)
            {
                customerContact.SetAsPrimary(true);
                await _domainContext.SaveChangesAsync(ct);
            }
        }

        if (customerContact.IsPrimary)
        {
            if (!permissions.Contains("PORTAL_QUOTATION.REQUEST"))
                permissions.Add("PORTAL_QUOTATION.REQUEST");
            if (!permissions.Contains("PORTAL.QUOTATION.REQUEST"))
                permissions.Add("PORTAL.QUOTATION.REQUEST");
            if (!permissions.Contains("PORTAL_QUOTATION.ACCEPT"))
                permissions.Add("PORTAL_QUOTATION.ACCEPT");
            if (!permissions.Contains("PORTAL.QUOTATION.ACCEPT"))
                permissions.Add("PORTAL.QUOTATION.ACCEPT");
            if (!permissions.Contains("PORTAL_ORDER.SUBMIT"))
                permissions.Add("PORTAL_ORDER.SUBMIT");
            if (!permissions.Contains("PORTAL.ORDER.SUBMIT"))
                permissions.Add("PORTAL.ORDER.SUBMIT");
        }

        var availableSellingCompanies = await GetAvailableSellingCompaniesAsync(tenantId, ct);

        return new CustomerProfileDto
        {
            Customer = new CustomerCompanyDetailsDto
            {
                CustomerId = customer.CustomerId,
                CustomerCode = customer.CustomerCode,
                CustomerName = customer.CustomerName,
                TaxNumber = customer.TaxNumber,
                CurrencyCode = customer.CurrencyCode,
                Email = customer.Email,
                Phone = customer.Phone,
                AddressLine1 = customer.AddressLine1,
                AddressLine2 = customer.AddressLine2,
                City = customer.City,
                PostalCode = customer.PostalCode,
                Country = customer.Country
            },
            Contact = new CustomerContactDetailsDto
            {
                ContactId = contact.ContactId,
                ContactName = contact.ContactName,
                JobTitle = contact.JobTitle,
                Email = contact.Email,
                Phone = contact.Phone,
                MobileNumber = contact.MobileNumber
            },
            Membership = new CustomerMembershipDetailsDto
            {
                TenantId = tenantId,
                IsPrimary = customerContact.IsPrimary,
                IsActive = customerContact.IsActive,
                RequiresAuthorization = !customerContact.IsPrimary,
                Roles = roles,
                Permissions = permissions
            },
            AvailableSellingCompanies = availableSellingCompanies
        };
    }

    public async Task<IReadOnlyList<SellingCompanyDto>> GetAvailableSellingCompaniesAsync(Guid tenantId, CancellationToken ct = default)
    {
        return await _domainContext.Companies
            .Include(c => c.BaseCurrency)
            .Where(c => c.TenantId == tenantId && c.IsActive)
            .Select(c => new SellingCompanyDto
            {
                CompanyId = c.Id,
                CompanyName = c.Name,
                CompanyCode = c.Code,
                IsActive = c.IsActive,
                BaseCurrencyCode = c.BaseCurrency != null ? c.BaseCurrency.CurrencyCode : null
            })
            .ToListAsync(ct);
    }

    public async Task<SellingCompanyDto> ValidateSellingCompanyContextAsync(Guid customerId, Guid tenantId, Guid sellingCompanyId, CancellationToken ct = default)
    {
        // 1. Verify customer master exists and is active within tenant
        var customer = await _domainContext.Customers
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.CustomerId == customerId && c.IsActive, ct);

        if (customer == null)
            throw new ArgumentException("Customer account not found or inactive within authorized tenant scope.");

        // 2. Verify selling company exists and is active within tenant
        var company = await _domainContext.Companies
            .Include(c => c.BaseCurrency)
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == sellingCompanyId && c.IsActive, ct);

        if (company == null)
            throw new ArgumentException("Selling company not found or inactive within authorized tenant scope.");

        return new SellingCompanyDto
        {
            CompanyId = company.Id,
            CompanyName = company.Name,
            CompanyCode = company.Code,
            IsActive = company.IsActive,
            BaseCurrencyCode = company.BaseCurrency != null ? company.BaseCurrency.CurrencyCode : null
        };
    }
}
