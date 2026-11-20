using System;
using System.Threading;
using System.Threading.Tasks;
using API.Features.Portal.DTOs;
using Domain.Features.MasterData.Contact;
using Domain.Infrastructure.Persistence;
using ERP.Domain.Features.CRM.Deduplication;
using ERP.Domain.Features.MasterData.Customer;
using Microsoft.EntityFrameworkCore;
using Security.Core.Models;
using Security.Infrastructure.Persistence;
using Security.Services;

namespace API.Features.Portal.Services;

/// <summary>
/// Implements Company Customer registration and identity foundation in accordance with Phase 1.
/// Reuses existing Customer Master, Contact, Deduplication, Security User, and PasswordService.
/// </summary>
public class CustomerRegistrationService : ICustomerRegistrationService
{
    private readonly DomainDbContext _domainContext;
    private readonly SecurityDbContext _securityContext;
    private readonly ICustomerDeduplicationService _deduplicationService;
    private readonly PasswordService _passwordService;

    public CustomerRegistrationService(
        DomainDbContext domainContext,
        SecurityDbContext securityContext,
        ICustomerDeduplicationService deduplicationService,
        PasswordService passwordService)
    {
        _domainContext = domainContext ?? throw new ArgumentNullException(nameof(domainContext));
        _securityContext = securityContext ?? throw new ArgumentNullException(nameof(securityContext));
        _deduplicationService = deduplicationService ?? throw new ArgumentNullException(nameof(deduplicationService));
        _passwordService = passwordService ?? throw new ArgumentNullException(nameof(passwordService));
    }

    public async Task<CustomerRegistrationResult> RegisterCompanyCustomerAsync(
        RegisterCompanyCustomerRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        // 1. Mandatory Input Validation
        if (request.TenantId == Guid.Empty)
            throw new ArgumentException("Tenant ID is required.", nameof(request.TenantId));

        if (string.IsNullOrWhiteSpace(request.CompanyName))
            throw new ArgumentException("Company name is required.", nameof(request.CompanyName));

        if (string.IsNullOrWhiteSpace(request.ContactPersonName))
            throw new ArgumentException("Contact person full name is required.", nameof(request.ContactPersonName));

        if (string.IsNullOrWhiteSpace(request.Email) || !request.Email.Contains('@'))
            throw new ArgumentException("A valid personal business email address is required.", nameof(request.Email));

        if (string.IsNullOrWhiteSpace(request.Password))
            throw new ArgumentException("Password is required.", nameof(request.Password));

        if (request.Password != request.ConfirmPassword)
            throw new ArgumentException("Password and confirmation password do not match.", nameof(request.ConfirmPassword));

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        // 2. Security User Uniqueness Validation
        var existingUser = await _securityContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.TenantId == request.TenantId &&
                                      (u.Email == normalizedEmail || u.Username == normalizedEmail),
                                 cancellationToken);

        if (existingUser != null)
        {
            throw new InvalidOperationException("A user with this email address is already registered in this tenant.");
        }

        // 3. Customer Company Deduplication Check
        var matchCriteria = new CustomerMatchCriteria
        {
            CustomerName = request.CompanyName.Trim(),
            TaxNumber = request.TaxNumber?.Trim(),
            Email = request.CompanyEmail?.Trim(),
            Phone = request.CompanyPhone?.Trim()
        };

        var duplicateResult = await _deduplicationService.FindDuplicateAsync(request.TenantId, matchCriteria, cancellationToken);
        bool isExistingMatched = false;
        Customer customer;

        if (duplicateResult.MatchedCustomerId.HasValue)
        {
            var matched = await _domainContext.Customers
                .FirstOrDefaultAsync(c => c.TenantId == request.TenantId && c.CustomerId == duplicateResult.MatchedCustomerId.Value, cancellationToken);

            if (matched != null)
            {
                isExistingMatched = true;
                customer = matched;
            }
            else
            {
                customer = CreateNewCustomer(request, normalizedEmail);
                _domainContext.Customers.Add(customer);
            }
        }
        else
        {
            customer = CreateNewCustomer(request, normalizedEmail);
            _domainContext.Customers.Add(customer);
        }

        // 4. Resolve or Initialize ContactType
        var contactType = await _domainContext.ContactTypes
            .FirstOrDefaultAsync(ct => (ct.TenantId == request.TenantId || ct.TenantId == null) && ct.Code == "PRIMARY", cancellationToken)
            ?? await _domainContext.ContactTypes.FirstOrDefaultAsync(ct => ct.TenantId == request.TenantId || ct.TenantId == null, cancellationToken);

        if (contactType == null)
        {
            contactType = ContactType.Create(request.TenantId, "PRIMARY", "Primary Contact");
            _domainContext.ContactTypes.Add(contactType);
        }

        // 5. Create Human Contact & CustomerContact Association
        var userId = Guid.NewGuid();
        var contact = Contact.Create(
            request.TenantId,
            request.ContactPersonName.Trim(),
            request.JobTitle?.Trim(),
            normalizedEmail,
            request.MobileNumber?.Trim(),
            request.MobileNumber?.Trim(),
            contactId: userId);

        _domainContext.Contacts.Add(contact);

        var customerContact = CustomerContact.Create(
            request.TenantId,
            customer.CustomerId,
            contact.ContactId,
            contactType.ContactTypeId,
            isPrimary: !isExistingMatched);

        _domainContext.CustomerContacts.Add(customerContact);

        await _domainContext.SaveChangesAsync(cancellationToken);

        // 6. Create Security User & Assign Customer Role (Distinct from Employee)
        var user = new User
        {
            Id = userId,
            TenantId = request.TenantId,
            Username = normalizedEmail,
            Email = normalizedEmail,
            PasswordHash = _passwordService.HashPassword(request.Password),
            IsActive = true // Immediate login allowed in Phase 1 (Email verification deferred)
        };

        _securityContext.Users.Add(user);

        var customerRole = await _securityContext.Roles
            .FirstOrDefaultAsync(r => r.TenantId == request.TenantId && r.Name == "CUSTOMER_COMPANY_ADMIN", cancellationToken);

        if (customerRole == null)
        {
            customerRole = new Role
            {
                Id = Guid.NewGuid(),
                TenantId = request.TenantId,
                Name = "CUSTOMER_COMPANY_ADMIN",
                Description = "Customer Company Administrator (Portal)",
                IsSystemRole = false,
                IsActive = true,
                TemplateKey = "CUSTOMER_COMPANY_ADMIN",
                TemplateVersion = 1
            };
            _securityContext.Roles.Add(customerRole);
        }

        var userRole = new UserRole
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            RoleId = customerRole.Id
        };
        _securityContext.UserRoles.Add(userRole);

        await _securityContext.SaveChangesAsync(cancellationToken);

        // 7. Formulate Registration Response
        return new CustomerRegistrationResult
        {
            CustomerId = customer.CustomerId,
            CustomerCode = customer.CustomerCode,
            CompanyName = customer.CustomerName,
            ContactId = contact.ContactId,
            UserId = user.Id,
            Email = user.Email,
            IsExistingCompanyMatched = isExistingMatched,
            RequiresAuthorization = isExistingMatched,
            CanLoginImmediately = true,
            Message = isExistingMatched
                ? "Account registered and linked to existing company profile. Company representation authority requires separate validation."
                : "Company customer account registered successfully. You may log in immediately."
        };
    }

    private static Customer CreateNewCustomer(RegisterCompanyCustomerRequest request, string normalizedEmail)
    {
        var customerCode = $"CUST-{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        return Customer.Create(
            request.TenantId,
            customerCode,
            request.CompanyName.Trim(),
            contactPerson: request.ContactPersonName.Trim(),
            email: string.IsNullOrWhiteSpace(request.CompanyEmail) ? normalizedEmail : request.CompanyEmail.Trim(),
            phone: string.IsNullOrWhiteSpace(request.CompanyPhone) ? request.MobileNumber?.Trim() : request.CompanyPhone.Trim(),
            addressLine1: request.AddressLine1?.Trim(),
            addressLine2: request.AddressLine2?.Trim(),
            city: string.IsNullOrWhiteSpace(request.City) ? string.Empty : request.City.Trim(),
            postalCode: request.PostalCode?.Trim(),
            country: request.Country?.Trim(),
            taxNumber: request.TaxNumber?.Trim(),
            creditLimit: 0m,
            paymentTerms: null);
    }
}
