using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using API.Common;
using API.Features.Portal.Context;
using API.Features.Portal.DTOs;
using Domain.Features.MasterData.Address;
using Domain.Features.MasterData.AddressType;
using Domain.Infrastructure.Persistence;
using ERP.Domain.Features.MasterData.Customer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API.Features.Portal;

/// <summary>
/// Authenticated Customer Portal controller for Address Master self-service.
/// Reuses existing Address, CustomerAddress, and AddressType domain architecture.
/// Strictly enforces customer ownership, tenant isolation, and prevents cross-customer access.
/// </summary>
[ApiController]
[Route("api/portal/customer/addresses")]
[Authorize]
public class CustomerAddressController : BaseApiController
{
    private readonly DomainDbContext _domainContext;
    private readonly ICurrentCustomerContext _customerContext;

    public CustomerAddressController(
        DomainDbContext domainContext,
        ICurrentCustomerContext customerContext)
    {
        _domainContext = domainContext ?? throw new ArgumentNullException(nameof(domainContext));
        _customerContext = customerContext ?? throw new ArgumentNullException(nameof(customerContext));
    }

    /// <summary>
    /// Retrieves all active addresses linked to the authenticated customer account.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAddresses(CancellationToken ct = default)
    {
        if (!_customerContext.IsCustomer)
            return Forbid();

        var tenantId = _customerContext.TenantId;
        var customerId = _customerContext.CustomerId;

        var query = from ca in _domainContext.CustomerAddresses
                    join a in _domainContext.Addresses on new { ca.TenantId, ca.AddressId } equals new { a.TenantId, a.AddressId }
                    join at in _domainContext.AddressTypes on ca.AddressTypeId equals at.AddressTypeId into atGroup
                    from at in atGroup.DefaultIfEmpty()
                    where ca.TenantId == tenantId && ca.CustomerId == customerId && ca.IsActive && a.IsActive
                    orderby ca.IsDefault descending, a.AddressName
                    select new CustomerAddressPortalDto
                    {
                        CustomerAddressId = ca.CustomerAddressId,
                        AddressId = a.AddressId,
                        AddressName = a.AddressName,
                        AddressLine1 = a.AddressLine1,
                        AddressLine2 = a.AddressLine2,
                        City = a.City,
                        StateProvince = a.StateProvince,
                        PostalCode = a.PostalCode,
                        CountryId = a.CountryId,
                        Country = a.UnmatchedCountryText,
                        AddressTypeId = ca.AddressTypeId,
                        AddressTypeCode = at != null ? at.Code : "PRIMARY",
                        AddressTypeName = at != null ? at.Name : "Primary Address",
                        IsDefault = ca.IsDefault,
                        IsActive = ca.IsActive
                    };

        var addresses = await query.ToListAsync(ct);

        // If customer has no linked CustomerAddress records yet, fall back to presenting the flat registered address
        if (addresses.Count == 0)
        {
            var customer = await _domainContext.Customers
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.CustomerId == customerId && c.IsActive, ct);

            if (customer != null && !string.IsNullOrWhiteSpace(customer.AddressLine1))
            {
                addresses.Add(new CustomerAddressPortalDto
                {
                    CustomerAddressId = Guid.Empty,
                    AddressId = Guid.Empty,
                    AddressName = "Registered Address",
                    AddressLine1 = customer.AddressLine1,
                    AddressLine2 = customer.AddressLine2,
                    City = customer.City ?? string.Empty,
                    PostalCode = customer.PostalCode,
                    Country = customer.Country,
                    AddressTypeCode = "DELIVERY",
                    AddressTypeName = "Default Delivery Address",
                    IsDefault = true,
                    IsActive = true
                });
            }
        }

        return SuccessResponse(addresses);
    }

    /// <summary>
    /// Creates and links a new verified Address Master record for the authenticated customer.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateAddress(
        [FromBody] CreateCustomerAddressPortalRequest request,
        CancellationToken ct = default)
    {
        if (!_customerContext.IsCustomer)
            return Forbid();

        if (request == null)
            return BadRequestResponse("Address payload is required.");

        if (string.IsNullOrWhiteSpace(request.AddressLine1))
            return BadRequestResponse("Address line 1 is required.");

        if (string.IsNullOrWhiteSpace(request.City))
            return BadRequestResponse("City is required.");

        var tenantId = _customerContext.TenantId;
        var customerId = _customerContext.CustomerId;

        var customer = await _domainContext.Customers
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.CustomerId == customerId && c.IsActive, ct);

        if (customer == null)
            return NotFoundResponse("Customer account not found or inactive.");

        // Resolve or create AddressType
        var addressType = await ResolveAddressTypeAsync(tenantId, request.AddressTypeId, request.AddressTypeCode, ct);

        var addressName = string.IsNullOrWhiteSpace(request.AddressName)
            ? $"{addressType.Name} ({request.City.Trim()})"
            : request.AddressName.Trim();

        using var tx = await _domainContext.Database.BeginTransactionAsync(ct);
        try
        {
            var address = Address.Create(
                tenantId,
                addressName,
                request.AddressLine1.Trim(),
                request.City.Trim(),
                request.AddressLine2?.Trim(),
                request.StateProvince?.Trim(),
                request.PostalCode?.Trim(),
                request.CountryId,
                request.Country?.Trim());

            _domainContext.Addresses.Add(address);
            await _domainContext.SaveChangesAsync(ct);

            if (request.IsDefault)
            {
                var existingDefaults = await _domainContext.CustomerAddresses
                    .Where(ca => ca.TenantId == tenantId && ca.CustomerId == customerId && ca.AddressTypeId == addressType.AddressTypeId && ca.IsDefault && ca.IsActive)
                    .ToListAsync(ct);

                foreach (var def in existingDefaults)
                {
                    def.SetAsDefault(false);
                }
                await _domainContext.SaveChangesAsync(ct);
            }

            var customerAddress = CustomerAddress.Create(
                tenantId,
                customerId,
                address.AddressId,
                addressType.AddressTypeId,
                request.IsDefault);

            _domainContext.CustomerAddresses.Add(customerAddress);
            await _domainContext.SaveChangesAsync(ct);

            if (request.IsDefault)
            {
                customer.SyncDefaultAddress(
                    address.AddressLine1,
                    address.AddressLine2,
                    address.City,
                    address.PostalCode,
                    request.Country?.Trim() ?? address.UnmatchedCountryText);

                await _domainContext.SaveChangesAsync(ct);
            }

            await tx.CommitAsync(ct);

            var dto = new CustomerAddressPortalDto
            {
                CustomerAddressId = customerAddress.CustomerAddressId,
                AddressId = address.AddressId,
                AddressName = address.AddressName,
                AddressLine1 = address.AddressLine1,
                AddressLine2 = address.AddressLine2,
                City = address.City,
                StateProvince = address.StateProvince,
                PostalCode = address.PostalCode,
                CountryId = address.CountryId,
                Country = address.UnmatchedCountryText,
                AddressTypeId = addressType.AddressTypeId,
                AddressTypeCode = addressType.Code,
                AddressTypeName = addressType.Name,
                IsDefault = customerAddress.IsDefault,
                IsActive = customerAddress.IsActive
            };

            return CreatedResponse(dto, "Address added and linked successfully.");
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);
            return HandleDomainException(ex);
        }
    }

    /// <summary>
    /// Updates an existing authorized address linked to the customer.
    /// </summary>
    [HttpPut("{customerAddressId:guid}")]
    public async Task<IActionResult> UpdateAddress(
        Guid customerAddressId,
        [FromBody] UpdateCustomerAddressPortalRequest request,
        CancellationToken ct = default)
    {
        if (!_customerContext.IsCustomer)
            return Forbid();

        if (request == null)
            return BadRequestResponse("Address update payload is required.");

        var tenantId = _customerContext.TenantId;
        var customerId = _customerContext.CustomerId;

        var customerAddress = await _domainContext.CustomerAddresses
            .FirstOrDefaultAsync(ca => ca.TenantId == tenantId && ca.CustomerId == customerId && ca.CustomerAddressId == customerAddressId && ca.IsActive, ct);

        if (customerAddress == null)
            return NotFoundResponse("Customer address association not found in authorized account.");

        var address = await _domainContext.Addresses
            .FirstOrDefaultAsync(a => a.TenantId == tenantId && a.AddressId == customerAddress.AddressId && a.IsActive, ct);

        if (address == null)
            return NotFoundResponse("Address master record not found or inactive.");

        var customer = await _domainContext.Customers
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.CustomerId == customerId && c.IsActive, ct);

        using var tx = await _domainContext.Database.BeginTransactionAsync(ct);
        try
        {
            var addressName = string.IsNullOrWhiteSpace(request.AddressName) ? address.AddressName : request.AddressName.Trim();
            var line1 = string.IsNullOrWhiteSpace(request.AddressLine1) ? address.AddressLine1 : request.AddressLine1.Trim();
            var city = string.IsNullOrWhiteSpace(request.City) ? address.City : request.City.Trim();

            address.Update(
                addressName,
                line1,
                city,
                request.AddressLine2?.Trim() ?? address.AddressLine2,
                request.StateProvince?.Trim() ?? address.StateProvince,
                request.PostalCode?.Trim() ?? address.PostalCode,
                request.CountryId ?? address.CountryId);

            if (request.AddressTypeId.HasValue && request.AddressTypeId.Value != customerAddress.AddressTypeId)
            {
                customerAddress.SetAddressType(request.AddressTypeId.Value);
            }

            if (request.IsDefault && !customerAddress.IsDefault)
            {
                var existingDefaults = await _domainContext.CustomerAddresses
                    .Where(ca => ca.TenantId == tenantId && ca.CustomerId == customerId && ca.AddressTypeId == customerAddress.AddressTypeId && ca.IsDefault && ca.IsActive)
                    .ToListAsync(ct);

                foreach (var def in existingDefaults)
                {
                    def.SetAsDefault(false);
                }

                customerAddress.SetAsDefault(true);

                if (customer != null)
                {
                    customer.SyncDefaultAddress(
                        address.AddressLine1,
                        address.AddressLine2,
                        address.City,
                        address.PostalCode,
                        request.Country?.Trim() ?? address.UnmatchedCountryText);
                }
            }

            await _domainContext.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            var addressType = await _domainContext.AddressTypes
                .AsNoTracking()
                .FirstOrDefaultAsync(at => at.AddressTypeId == customerAddress.AddressTypeId, ct);

            return SuccessResponse(new CustomerAddressPortalDto
            {
                CustomerAddressId = customerAddress.CustomerAddressId,
                AddressId = address.AddressId,
                AddressName = address.AddressName,
                AddressLine1 = address.AddressLine1,
                AddressLine2 = address.AddressLine2,
                City = address.City,
                StateProvince = address.StateProvince,
                PostalCode = address.PostalCode,
                CountryId = address.CountryId,
                Country = address.UnmatchedCountryText,
                AddressTypeId = customerAddress.AddressTypeId,
                AddressTypeCode = addressType?.Code ?? "PRIMARY",
                AddressTypeName = addressType?.Name ?? "Primary Address",
                IsDefault = customerAddress.IsDefault,
                IsActive = customerAddress.IsActive
            }, "Address updated successfully.");
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);
            return HandleDomainException(ex);
        }
    }

    /// <summary>
    /// Sets an authorized customer address as the default for its address type.
    /// </summary>
    [HttpPut("{customerAddressId:guid}/default")]
    public async Task<IActionResult> SetDefaultAddress(
        Guid customerAddressId,
        CancellationToken ct = default)
    {
        if (!_customerContext.IsCustomer)
            return Forbid();

        var tenantId = _customerContext.TenantId;
        var customerId = _customerContext.CustomerId;

        var customerAddress = await _domainContext.CustomerAddresses
            .FirstOrDefaultAsync(ca => ca.TenantId == tenantId && ca.CustomerId == customerId && ca.CustomerAddressId == customerAddressId && ca.IsActive, ct);

        if (customerAddress == null)
            return NotFoundResponse("Customer address not found in authorized account.");

        var customer = await _domainContext.Customers
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.CustomerId == customerId && c.IsActive, ct);

        var address = await _domainContext.Addresses
            .FirstOrDefaultAsync(a => a.TenantId == tenantId && a.AddressId == customerAddress.AddressId && a.IsActive, ct);

        using var tx = await _domainContext.Database.BeginTransactionAsync(ct);
        try
        {
            var existingDefaults = await _domainContext.CustomerAddresses
                .Where(ca => ca.TenantId == tenantId && ca.CustomerId == customerId && ca.AddressTypeId == customerAddress.AddressTypeId && ca.IsDefault && ca.IsActive)
                .ToListAsync(ct);

            foreach (var def in existingDefaults)
            {
                def.SetAsDefault(false);
            }

            customerAddress.SetAsDefault(true);

            if (customer != null && address != null)
            {
                customer.SyncDefaultAddress(
                    address.AddressLine1,
                    address.AddressLine2,
                    address.City,
                    address.PostalCode,
                    address.UnmatchedCountryText);
            }

            await _domainContext.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            return NoContent();
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);
            return HandleDomainException(ex);
        }
    }

    /// <summary>
    /// Deactivates / unlinks an address association from the customer account.
    /// Preserves the physical Address record and historical order snapshot intact.
    /// </summary>
    [HttpDelete("{customerAddressId:guid}")]
    public async Task<IActionResult> DeactivateAddress(
        Guid customerAddressId,
        CancellationToken ct = default)
    {
        if (!_customerContext.IsCustomer)
            return Forbid();

        var tenantId = _customerContext.TenantId;
        var customerId = _customerContext.CustomerId;

        var customerAddress = await _domainContext.CustomerAddresses
            .FirstOrDefaultAsync(ca => ca.TenantId == tenantId && ca.CustomerId == customerId && ca.CustomerAddressId == customerAddressId && ca.IsActive, ct);

        if (customerAddress == null)
            return NotFoundResponse("Customer address not found in authorized account.");

        customerAddress.Deactivate();
        await _domainContext.SaveChangesAsync(ct);

        return NoContent();
    }

    /// <summary>
    /// Lists all active Address Types configured in the tenant.
    /// </summary>
    [HttpGet("types")]
    public async Task<IActionResult> GetAddressTypes(CancellationToken ct = default)
    {
        if (!_customerContext.IsCustomer)
            return Forbid();

        var tenantId = _customerContext.TenantId;

        var types = await _domainContext.AddressTypes
            .AsNoTracking()
            .Where(at => (at.TenantId == tenantId || at.TenantId == null) && at.IsActive)
            .OrderBy(at => at.Name)
            .Select(at => new PortalAddressTypeDto
            {
                AddressTypeId = at.AddressTypeId,
                Code = at.Code,
                Name = at.Name,
                Description = at.Description
            })
            .ToListAsync(ct);

        return SuccessResponse(types);
    }

    private async Task<AddressType> ResolveAddressTypeAsync(
        Guid tenantId,
        Guid? addressTypeId,
        string? addressTypeCode,
        CancellationToken ct)
    {
        if (addressTypeId.HasValue && addressTypeId.Value != Guid.Empty)
        {
            var found = await _domainContext.AddressTypes
                .FirstOrDefaultAsync(at => (at.TenantId == tenantId || at.TenantId == null) && at.AddressTypeId == addressTypeId.Value && at.IsActive, ct);
            if (found != null) return found;
        }

        string code = string.IsNullOrWhiteSpace(addressTypeCode) ? "DELIVERY" : addressTypeCode.Trim().ToUpperInvariant();

        var byCode = await _domainContext.AddressTypes
            .FirstOrDefaultAsync(at => (at.TenantId == tenantId || at.TenantId == null) && at.Code == code && at.IsActive, ct);

        if (byCode != null) return byCode;

        // Fallback to PRIMARY or create standard DELIVERY
        var fallback = await _domainContext.AddressTypes
            .FirstOrDefaultAsync(at => (at.TenantId == tenantId || at.TenantId == null) && at.Code == "PRIMARY" && at.IsActive, ct);

        if (fallback != null) return fallback;

        var created = AddressType.Create(tenantId, code, FormatTypeName(code));
        _domainContext.AddressTypes.Add(created);
        await _domainContext.SaveChangesAsync(ct);
        return created;
    }

    private static string FormatTypeName(string code) => code switch
    {
        "DELIVERY" => "Delivery Address",
        "BILLING" => "Billing Address",
        "HOME" => "Home Address",
        "HEAD_OFFICE" => "Head Office",
        "BRANCH_OFFICE" => "Branch Office",
        "WAREHOUSE" => "Warehouse / Dock",
        "REGISTERED_OFFICE" => "Registered Office",
        _ => "Other Address"
    };
}
