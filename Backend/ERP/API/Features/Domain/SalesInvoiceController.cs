using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using API.Common;
using API.Common.Sorting;
using API.Security.Authorization;
using Domain.Features.Sales.Invoice;
using Microsoft.AspNetCore.Mvc;
using Security.Interfaces;

namespace API.Features.Domain;

[ApiController]
[Route("api/companies/{companyId:guid}/branches/{branchId:guid}/sales-invoices")]
[RequirePermission("SALES_INVOICE.VIEW")]
public class SalesInvoiceController : BaseApiController
{
    private readonly ISalesInvoiceService _invoiceService;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IUserAccessService _userAccessService;

    private static readonly SortMapping<SalesInvoiceDto> _sortMapping = new SortMapping<SalesInvoiceDto>()
        .Add("invoiceNumber", i => i.InvoiceNumber, isDefault: true)
        .Add("invoiceDate", i => i.InvoiceDate)
        .Add("totalAmount", i => i.TotalAmount)
        .Add("status", i => i.Status)
        .SetSecondary(i => i.Id);

    public SalesInvoiceController(
        ISalesInvoiceService invoiceService,
        ICurrentUserContext currentUserContext,
        IUserAccessService userAccessService)
    {
        _invoiceService = invoiceService;
        _currentUserContext = currentUserContext;
        _userAccessService = userAccessService;
    }

    [HttpGet]
    public async Task<IActionResult> GetInvoices(
        Guid companyId,
        Guid branchId,
        [FromQuery] SalesInvoiceStatus? status = null,
        [FromQuery] int? page = null,
        [FromQuery] int? pageSize = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortDirection = null)
    {
        var authResult = await ValidateScopeAccess(companyId, branchId);
        if (authResult.Action != null) return authResult.Action;

        var list = await _invoiceService.GetInvoicesAsync(
            authResult.TenantId,
            companyId,
            branchId,
            status);

        return PaginateAndSort(list, page, pageSize, sortBy, sortDirection, _sortMapping);
    }

    [HttpGet("~/api/companies/{companyId:guid}/sales-invoices")]
    public async Task<IActionResult> GetCompanyInvoices(
        Guid companyId,
        [FromQuery] SalesInvoiceStatus? status = null,
        [FromQuery] int? page = null,
        [FromQuery] int? pageSize = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortDirection = null)
    {
        if (_currentUserContext.IsPlatformUser)
        {
            return Forbid();
        }

        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
        {
            return UnauthorizedResponse("Tenant identity is missing or invalid.");
        }

        var canAccessCompany = await _userAccessService.CanAccessCompanyAsync(
            _currentUserContext.UserId,
            tenantId.Value,
            companyId);

        if (!canAccessCompany)
        {
            return Forbid();
        }

        var list = await _invoiceService.GetInvoicesAsync(
            tenantId.Value,
            companyId,
            branchId: null,
            status: status);

        return PaginateAndSort(list, page, pageSize, sortBy, sortDirection, _sortMapping);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetInvoice(Guid companyId, Guid branchId, Guid id)
    {
        var authResult = await ValidateScopeAccess(companyId, branchId);
        if (authResult.Action != null) return authResult.Action;

        var invoice = await _invoiceService.GetInvoiceByIdAsync(
            authResult.TenantId,
            companyId,
            branchId,
            id);

        if (invoice == null)
        {
            return NotFoundResponse("Sales invoice not found in the authorized scope.");
        }

        return SuccessResponse(invoice);
    }

    [HttpGet("~/api/companies/{companyId:guid}/sales-invoices/{id:guid}")]
    public async Task<IActionResult> GetCompanyInvoice(Guid companyId, Guid id)
    {
        if (_currentUserContext.IsPlatformUser)
        {
            return Forbid();
        }

        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
        {
            return UnauthorizedResponse("Tenant identity is missing or invalid.");
        }

        var canAccessCompany = await _userAccessService.CanAccessCompanyAsync(
            _currentUserContext.UserId,
            tenantId.Value,
            companyId);

        if (!canAccessCompany)
        {
            return Forbid();
        }

        var invoice = await _invoiceService.GetInvoiceByIdAsync(
            tenantId.Value,
            companyId,
            branchId: null,
            invoiceId: id);

        if (invoice == null)
        {
            return NotFoundResponse("Sales invoice not found in the authorized scope.");
        }

        return SuccessResponse(invoice);
    }

    [HttpGet("by-shipment/{shipmentId:guid}")]
    public async Task<IActionResult> GetInvoiceByShipment(Guid companyId, Guid branchId, Guid shipmentId)
    {
        var authResult = await ValidateScopeAccess(companyId, branchId);
        if (authResult.Action != null) return authResult.Action;

        var invoice = await _invoiceService.GetInvoiceByShipmentIdAsync(authResult.TenantId, shipmentId);
        if (invoice == null)
        {
            return NotFoundResponse("No sales invoice found for the specified shipment.");
        }

        return SuccessResponse(invoice);
    }

    [HttpPost("from-shipment/{shipmentId:guid}")]
    [RequirePermission("SALES_INVOICE.CREATE")]
    public async Task<IActionResult> CreateFromShipment(
        Guid companyId,
        Guid branchId,
        Guid shipmentId,
        [FromQuery] string? invoiceNumber = null)
    {
        var authResult = await ValidateScopeAccess(companyId, branchId);
        if (authResult.Action != null) return authResult.Action;

        try
        {
            var invoice = await _invoiceService.CreateDraftFromShipmentAsync(
                authResult.TenantId,
                companyId,
                branchId,
                shipmentId,
                invoiceNumber);

            return CreatedResponse(
                nameof(GetInvoice),
                new { companyId, branchId, id = invoice.Id },
                invoice);
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpPut("{id:guid}")]
    [RequirePermission("SALES_INVOICE.EDIT")]
    public async Task<IActionResult> UpdateDraft(
        Guid companyId,
        Guid branchId,
        Guid id,
        [FromBody] UpdateSalesInvoiceRequest request)
    {
        var authResult = await ValidateScopeAccess(companyId, branchId);
        if (authResult.Action != null) return authResult.Action;

        try
        {
            var invoice = await _invoiceService.UpdateDraftAsync(
                authResult.TenantId,
                companyId,
                branchId,
                id,
                request);

            return SuccessResponse(invoice, "Sales invoice draft updated successfully.");
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpPost("{id:guid}/confirm")]
    [RequirePermission("SALES_INVOICE.CONFIRM")]
    public async Task<IActionResult> ConfirmInvoice(
        Guid companyId,
        Guid branchId,
        Guid id,
        [FromBody] ConfirmSalesInvoiceRequest? request = null)
    {
        var authResult = await ValidateScopeAccess(companyId, branchId);
        if (authResult.Action != null) return authResult.Action;

        try
        {
            var invoice = await _invoiceService.ConfirmInvoiceAsync(
                authResult.TenantId,
                companyId,
                branchId,
                id,
                _currentUserContext.UserId,
                request?.Notes);

            return SuccessResponse(invoice, "Sales invoice confirmed successfully.");
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpPost("{id:guid}/cancel")]
    [RequirePermission("SALES_INVOICE.CANCEL")]
    public async Task<IActionResult> CancelInvoice(
        Guid companyId,
        Guid branchId,
        Guid id)
    {
        var authResult = await ValidateScopeAccess(companyId, branchId);
        if (authResult.Action != null) return authResult.Action;

        try
        {
            var invoice = await _invoiceService.CancelInvoiceAsync(
                authResult.TenantId,
                companyId,
                branchId,
                id);

            return SuccessResponse(invoice, "Sales invoice cancelled successfully.");
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    private async Task<(IActionResult? Action, Guid TenantId)> ValidateScopeAccess(Guid companyId, Guid branchId)
    {
        if (_currentUserContext.IsPlatformUser)
        {
            return (Forbid(), Guid.Empty);
        }

        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
        {
            return (UnauthorizedResponse("Tenant identity is missing or invalid."), Guid.Empty);
        }

        var canAccessCompany = await _userAccessService.CanAccessCompanyAsync(
            _currentUserContext.UserId,
            tenantId.Value,
            companyId);

        if (!canAccessCompany)
        {
            return (Forbid(), Guid.Empty);
        }

        var canAccessBranch = await _userAccessService.CanAccessBranchAsync(
            _currentUserContext.UserId,
            tenantId.Value,
            companyId,
            branchId);

        if (!canAccessBranch)
        {
            return (Forbid(), Guid.Empty);
        }

        return (null, tenantId.Value);
    }
}
