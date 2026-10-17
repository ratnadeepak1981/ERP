using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using API.Common;
using API.Common.Sorting;
using API.Security.Authorization;
using Domain.Features.Sales.Shipment;
using Microsoft.AspNetCore.Mvc;
using Security.Interfaces;

namespace API.Features.Domain;

[ApiController]
[Route("api/companies/{companyId:guid}/branches/{branchId:guid}/sales-shipments")]
[RequirePermission("SALES_SHIPMENT.VIEW")]
public class SalesShipmentController : BaseApiController
{
    private readonly ISalesShipmentService _shipmentService;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IUserAccessService _userAccessService;

    private static readonly SortMapping<SalesShipmentDto> _sortMapping = new SortMapping<SalesShipmentDto>()
        .Add("shipmentNumber", s => s.ShipmentNumber, isDefault: true)
        .Add("shipmentDate", s => s.ShipmentDate)
        .Add("status", s => s.Status)
        .SetSecondary(s => s.Id);

    public SalesShipmentController(
        ISalesShipmentService shipmentService,
        ICurrentUserContext currentUserContext,
        IUserAccessService userAccessService)
    {
        _shipmentService = shipmentService;
        _currentUserContext = currentUserContext;
        _userAccessService = userAccessService;
    }

    [HttpGet]
    public async Task<IActionResult> GetShipments(
        Guid companyId,
        Guid branchId,
        [FromQuery] SalesShipmentStatus? status = null,
        [FromQuery] int? page = null,
        [FromQuery] int? pageSize = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortDirection = null)
    {
        var authResult = await ValidateScopeAccess(companyId, branchId);
        if (authResult.Action != null) return authResult.Action;

        var list = await _shipmentService.GetShipmentsAsync(
            authResult.TenantId,
            companyId,
            branchId,
            status);

        return PaginateAndSort(list, page, pageSize, sortBy, sortDirection, _sortMapping);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetShipment(Guid companyId, Guid branchId, Guid id)
    {
        var authResult = await ValidateScopeAccess(companyId, branchId);
        if (authResult.Action != null) return authResult.Action;

        var shipment = await _shipmentService.GetShipmentByIdAsync(
            authResult.TenantId,
            companyId,
            branchId,
            id);

        if (shipment == null)
        {
            return NotFoundResponse("Sales shipment not found in the authorized scope.");
        }

        return SuccessResponse(shipment);
    }

    [HttpPost("from-order/{salesOrderId:guid}")]
    [RequirePermission("SALES_SHIPMENT.CREATE")]
    public async Task<IActionResult> CreateFromOrder(
        Guid companyId,
        Guid branchId,
        Guid salesOrderId,
        [FromBody] CreateShipmentFromOrderRequest request)
    {
        var authResult = await ValidateScopeAccess(companyId, branchId);
        if (authResult.Action != null) return authResult.Action;

        try
        {
            var shipment = await _shipmentService.CreateFromOrderAsync(
                authResult.TenantId,
                companyId,
                branchId,
                salesOrderId,
                request);

            return CreatedResponse(
                nameof(GetShipment),
                new { companyId, branchId, id = shipment.Id },
                shipment);
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpPost]
    [RequirePermission("SALES_SHIPMENT.CREATE")]
    public async Task<IActionResult> CreateManualDraft(
        Guid companyId,
        Guid branchId,
        [FromBody] CreateSalesShipmentRequest request)
    {
        var authResult = await ValidateScopeAccess(companyId, branchId);
        if (authResult.Action != null) return authResult.Action;

        try
        {
            var shipment = await _shipmentService.CreateManualDraftAsync(
                authResult.TenantId,
                companyId,
                branchId,
                request);

            return CreatedResponse(
                nameof(GetShipment),
                new { companyId, branchId, id = shipment.Id },
                shipment);
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpPut("{id:guid}")]
    [RequirePermission("SALES_SHIPMENT.EDIT")]
    public async Task<IActionResult> UpdateDraft(
        Guid companyId,
        Guid branchId,
        Guid id,
        [FromBody] UpdateSalesShipmentRequest request)
    {
        var authResult = await ValidateScopeAccess(companyId, branchId);
        if (authResult.Action != null) return authResult.Action;

        try
        {
            var shipment = await _shipmentService.UpdateDraftAsync(
                authResult.TenantId,
                companyId,
                branchId,
                id,
                request);

            return SuccessResponse(shipment, "Sales shipment draft updated successfully.");
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpPost("{id:guid}/dispatch")]
    [RequirePermission("SALES_SHIPMENT.DISPATCH")]
    public async Task<IActionResult> DispatchShipment(Guid companyId, Guid branchId, Guid id)
    {
        var authResult = await ValidateScopeAccess(companyId, branchId);
        if (authResult.Action != null) return authResult.Action;

        try
        {
            var shipment = await _shipmentService.DispatchShipmentAsync(
                authResult.TenantId,
                companyId,
                branchId,
                id);

            return SuccessResponse(shipment, "Sales shipment dispatched successfully.");
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpPost("{id:guid}/deliver")]
    [RequirePermission("SALES_SHIPMENT.DELIVER")]
    public async Task<IActionResult> DeliverShipment(
        Guid companyId,
        Guid branchId,
        Guid id,
        [FromBody] DeliverShipmentRequest? request = null)
    {
        var authResult = await ValidateScopeAccess(companyId, branchId);
        if (authResult.Action != null) return authResult.Action;

        try
        {
            var shipment = await _shipmentService.DeliverShipmentAsync(
                authResult.TenantId,
                companyId,
                branchId,
                id,
                request);

            return SuccessResponse(shipment, "Sales shipment delivered successfully.");
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpPost("{id:guid}/cancel")]
    [RequirePermission("SALES_SHIPMENT.CANCEL")]
    public async Task<IActionResult> CancelDraft(Guid companyId, Guid branchId, Guid id)
    {
        var authResult = await ValidateScopeAccess(companyId, branchId);
        if (authResult.Action != null) return authResult.Action;

        try
        {
            var shipment = await _shipmentService.CancelDraftAsync(
                authResult.TenantId,
                companyId,
                branchId,
                id);

            return SuccessResponse(shipment, "Sales shipment cancelled successfully.");
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
