using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using API.Common;
using API.Common.Sorting;
using API.Security.Authorization;
using ERP.Domain.Features.MasterData.Customer;
using Microsoft.AspNetCore.Mvc;
using Security.Interfaces;

namespace API.Features.MasterData.Customer;

[ApiController]
[Route("api/customers")]
[RequirePermission("CUSTOMER.VIEW")]
public class CustomerController : BaseApiController
{
    private readonly ICustomerService _customerService;
    private readonly ICurrentUserContext _currentUserContext;

    private static readonly SortMapping<global::ERP.Domain.Features.MasterData.Customer.Customer> _sortMapping =
        new SortMapping<global::ERP.Domain.Features.MasterData.Customer.Customer>()
            .Add("customerCode", c => c.CustomerCode, isDefault: true)
            .Add("customerName", c => c.CustomerName)
            .Add("city", c => c.City ?? "")
            .SetSecondary(c => c.CustomerId);

    public CustomerController(
        ICustomerService customerService,
        ICurrentUserContext currentUserContext)
    {
        _customerService = customerService;
        _currentUserContext = currentUserContext;
    }

    [HttpGet]
    public async Task<IActionResult> GetCustomers(
        [FromQuery] int? page = null,
        [FromQuery] int? pageSize = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortDirection = null)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        var customers = await _customerService.GetCustomersAsync(tenantId.Value);
        return PaginateAndSort(customers, page, pageSize, sortBy, sortDirection, _sortMapping);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetCustomer(Guid id)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        var customer = await _customerService.GetCustomerAsync(tenantId.Value, id);
        if (customer == null)
            return NotFoundResponse("Customer not found.");

        return SuccessResponse(customer);
    }

    [HttpPost]
    [RequirePermission("CUSTOMER.CREATE")]
    public async Task<IActionResult> CreateCustomer([FromBody] CreateCustomerRequest request)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        try
        {
            var customer = await _customerService.CreateCustomerAsync(tenantId.Value, request);
            return CreatedResponse(nameof(GetCustomer), new { id = customer.CustomerId }, customer);
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpPut("{id:guid}")]
    [RequirePermission("CUSTOMER.EDIT")]
    public async Task<IActionResult> UpdateCustomer(Guid id, [FromBody] UpdateCustomerRequest request)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        try
        {
            var customer = await _customerService.UpdateCustomerAsync(tenantId.Value, id, request);
            return SuccessResponse(customer);
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission("CUSTOMER.DELETE")]
    public async Task<IActionResult> DeactivateCustomer(Guid id)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        try
        {
            await _customerService.DeactivateCustomerAsync(tenantId.Value, id);
            return NoContent();
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpPost("{id:guid}/reactivate")]
    [RequirePermission("CUSTOMER.EDIT")]
    public async Task<IActionResult> ReactivateCustomer(Guid id)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        try
        {
            var customer = await _customerService.ReactivateCustomerAsync(tenantId.Value, id);
            return SuccessResponse(customer, "Customer reactivated successfully.");
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpGet("{id:guid}/addresses")]
    public async Task<IActionResult> GetAddresses(Guid id)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        var addresses = await _customerService.GetAddressesAsync(tenantId.Value, id);
        return SuccessResponse(addresses);
    }

    [HttpPost("{id:guid}/addresses")]
    [RequirePermission("ADDRESS.MANAGE")]
    public async Task<IActionResult> LinkAddress(Guid id, [FromBody] CustomerAddressLinkRequest request)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        try
        {
            var assoc = await _customerService.LinkAddressAsync(tenantId.Value, id, request);
            return SuccessResponse(assoc, "Customer address linked successfully.");
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpPut("{id:guid}/addresses/{addressAssocId:guid}/default")]
    [RequirePermission("ADDRESS.MANAGE")]
    public async Task<IActionResult> SetDefaultAddress(Guid id, Guid addressAssocId)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        try
        {
            await _customerService.SetDefaultAddressAsync(tenantId.Value, id, addressAssocId);
            return NoContent();
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpDelete("{id:guid}/addresses/{addressAssocId:guid}")]
    [RequirePermission("ADDRESS.MANAGE")]
    public async Task<IActionResult> RemoveAddress(Guid id, Guid addressAssocId)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        try
        {
            await _customerService.RemoveAddressLinkAsync(tenantId.Value, id, addressAssocId);
            return NoContent();
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpGet("{id:guid}/contacts")]
    public async Task<IActionResult> GetContacts(Guid id)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        var contacts = await _customerService.GetContactsAsync(tenantId.Value, id);
        return SuccessResponse(contacts);
    }

    [HttpPost("{id:guid}/contacts")]
    [RequirePermission("CONTACT.MANAGE")]
    public async Task<IActionResult> LinkContact(Guid id, [FromBody] CustomerContactLinkRequest request)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        try
        {
            var assoc = await _customerService.LinkContactAsync(tenantId.Value, id, request);
            return SuccessResponse(assoc, "Customer contact linked successfully.");
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpPut("{id:guid}/contacts/{contactAssocId:guid}/primary")]
    [RequirePermission("CONTACT.MANAGE")]
    public async Task<IActionResult> SetPrimaryContact(Guid id, Guid contactAssocId)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        try
        {
            await _customerService.SetPrimaryContactAsync(tenantId.Value, id, contactAssocId);
            return NoContent();
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpDelete("{id:guid}/contacts/{contactAssocId:guid}")]
    [RequirePermission("CONTACT.MANAGE")]
    public async Task<IActionResult> RemoveContact(Guid id, Guid contactAssocId)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        try
        {
            await _customerService.RemoveContactLinkAsync(tenantId.Value, id, contactAssocId);
            return NoContent();
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }
}
