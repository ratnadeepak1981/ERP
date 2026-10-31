using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using API.Common;
using API.Common.Sorting;
using API.Security.Authorization;
using Domain.Features.MasterData.Branch;
using Domain.Features.Procurement.Approval;
using Domain.Features.Procurement.PurchaseOrder;
using Microsoft.AspNetCore.Mvc;
using Security.Interfaces;

namespace API.Features.Domain;

[ApiController]
[Route("api/companies/{companyId:guid}/branches/{branchId:guid}/purchase-orders")]
[RequirePermission("PURCHASE_ORDER.VIEW")]
public class PurchaseOrderController : BaseApiController
{
    private readonly IPurchaseOrderService _orderService;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IUserAccessService _userAccessService;
    private readonly IBranchService _branchService;

    private static readonly SortMapping<PurchaseOrder> _sortMapping = new SortMapping<PurchaseOrder>()
        .Add("orderNumber", o => o.OrderNumber, isDefault: true)
        .Add("orderDate", o => o.OrderDate)
        .Add("totalAmount", o => o.TotalAmount)
        .Add("status", o => o.Status)
        .SetSecondary(o => o.Id);

    public PurchaseOrderController(
        IPurchaseOrderService orderService,
        ICurrentUserContext currentUserContext,
        IUserAccessService userAccessService,
        IBranchService branchService)
    {
        _orderService = orderService;
        _currentUserContext = currentUserContext;
        _userAccessService = userAccessService;
        _branchService = branchService;
    }

    [HttpGet("~/api/companies/{companyId:guid}/purchase-orders")]
    public async Task<IActionResult> GetCompanyOrders(
        Guid companyId,
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

        List<PurchaseOrder> orders;
        var isTenantAdmin = User.IsInRole("Tenant Admin");
        if (isTenantAdmin)
        {
            orders = await _orderService.GetOrdersByCompanyAsync(tenantId.Value, companyId);
        }
        else
        {
            var branches = await _branchService.GetBranchesAsync(tenantId.Value, companyId);
            var accessibleBranchIds = new List<Guid>();
            foreach (var b in branches)
            {
                if (await _userAccessService.CanAccessBranchAsync(_currentUserContext.UserId, tenantId.Value, companyId, b.Id))
                {
                    accessibleBranchIds.Add(b.Id);
                }
            }
            orders = await _orderService.GetOrdersByCompanyAsync(tenantId.Value, companyId, accessibleBranchIds);
        }

        return PaginateAndSort(orders, page, pageSize, sortBy, sortDirection, _sortMapping);
    }

    [HttpGet("~/api/companies/{companyId:guid}/purchase-orders/{id:guid}")]
    public async Task<IActionResult> GetCompanyOrder(
        Guid companyId,
        Guid id)
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

        var order = await _orderService.GetOrderByIdAsync(
            tenantId.Value,
            companyId,
            id);

        if (order == null)
        {
            return NotFoundResponse("Purchase order not found in the authorized scope.");
        }

        var canAccessBranch = await _userAccessService.CanAccessBranchAsync(
            _currentUserContext.UserId,
            tenantId.Value,
            companyId,
            order.BranchId);

        if (!canAccessBranch)
        {
            return Forbid();
        }

        return SuccessResponse(order);
    }

    [HttpGet]
    public async Task<IActionResult> GetOrders(
        Guid companyId,
        Guid branchId,
        [FromQuery] int? page = null,
        [FromQuery] int? pageSize = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortDirection = null)
    {
        var authResult = await ValidateScopeAccess(companyId, branchId);
        if (authResult.Action != null) return authResult.Action;

        var orders = await _orderService.GetOrdersAsync(
            authResult.TenantId,
            companyId,
            branchId);

        return PaginateAndSort(orders, page, pageSize, sortBy, sortDirection, _sortMapping);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetOrder(
        Guid companyId,
        Guid branchId,
        Guid id)
    {
        var authResult = await ValidateScopeAccess(companyId, branchId);
        if (authResult.Action != null) return authResult.Action;

        var order = await _orderService.GetOrderAsync(
            authResult.TenantId,
            companyId,
            branchId,
            id);

        if (order == null)
        {
            return NotFoundResponse("Purchase order not found in the authorized scope.");
        }

        return SuccessResponse(order);
    }

    [HttpPost]
    [RequirePermission("PURCHASE_ORDER.CREATE")]
    public async Task<IActionResult> CreateOrder(
        Guid companyId,
        Guid branchId,
        [FromBody] CreatePurchaseOrderRequest request)
    {
        var authResult = await ValidateScopeAccess(companyId, branchId);
        if (authResult.Action != null) return authResult.Action;

        try
        {
            var order = await _orderService.CreateOrderAsync(
                authResult.TenantId,
                companyId,
                branchId,
                request,
                _currentUserContext.UserId,
                User.Identity?.Name ?? _currentUserContext.UserId.ToString());

            return CreatedResponse(
                nameof(GetOrder),
                new { companyId, branchId, id = order.Id },
                order);
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpPost("{orderId:guid}/items")]
    [RequirePermission("PURCHASE_ORDER.EDIT")]
    public async Task<IActionResult> AddItem(
        Guid companyId,
        Guid branchId,
        Guid orderId,
        [FromBody] CreatePurchaseOrderItemRequest request)
    {
        var authResult = await ValidateScopeAccess(companyId, branchId);
        if (authResult.Action != null) return authResult.Action;

        try
        {
            var item = await _orderService.AddItemAsync(
                authResult.TenantId,
                companyId,
                branchId,
                orderId,
                request);

            return SuccessResponse(item, "Item added to purchase order successfully.");
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpPost("{orderId:guid}/refresh-exchange-rate")]
    [RequirePermission("PURCHASE_ORDER.EDIT")]
    public async Task<IActionResult> RefreshExchangeRate(
        Guid companyId,
        Guid branchId,
        Guid orderId,
        [FromBody] RefreshPurchaseOrderCurrencyRequest request)
    {
        var authResult = await ValidateScopeAccess(companyId, branchId);
        if (authResult.Action != null) return authResult.Action;

        try
        {
            var order = await _orderService.RefreshCurrencyAsync(
                authResult.TenantId,
                companyId,
                branchId,
                orderId,
                request);
            return SuccessResponse(order, "Purchase order exchange-rate snapshot refreshed.");
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpPost("{orderId:guid}/submit")]
    [RequirePermission("PURCHASE_ORDER.SUBMIT")]
    public async Task<IActionResult> SubmitOrder(
        Guid companyId,
        Guid branchId,
        Guid orderId)
    {
        var authResult = await ValidateScopeAccess(companyId, branchId);
        if (authResult.Action != null) return authResult.Action;

        try
        {
            var order = await _orderService.SubmitOrderAsync(
                authResult.TenantId,
                companyId,
                branchId,
                orderId,
                _currentUserContext.UserId,
                User.Identity?.Name ?? _currentUserContext.UserId.ToString());

            return SuccessResponse(order, "Purchase order submitted successfully.");
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpPost("{orderId:guid}/approve")]
    [RequirePermission("PURCHASE_ORDER.APPROVE")]
    public async Task<IActionResult> ApproveOrder(
        Guid companyId,
        Guid branchId,
        Guid orderId,
        [FromBody] ApprovalDecisionRequest? request)
    {
        var authResult = await ValidateScopeAccess(companyId, branchId);
        if (authResult.Action != null) return authResult.Action;

        try
        {
            var order = await _orderService.ApproveOrderAsync(
                authResult.TenantId,
                companyId,
                branchId,
                orderId,
                _currentUserContext.UserId,
                User.Identity?.Name ?? _currentUserContext.UserId.ToString(),
                request?.Remarks);

            return SuccessResponse(order, "Purchase order approved successfully.");
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpPost("{orderId:guid}/reject")]
    [RequirePermission("PURCHASE_ORDER.APPROVE")]
    public async Task<IActionResult> RejectOrder(
        Guid companyId,
        Guid branchId,
        Guid orderId,
        [FromBody] ApprovalDecisionRequest request)
    {
        var authResult = await ValidateScopeAccess(companyId, branchId);
        if (authResult.Action != null) return authResult.Action;

        try
        {
            var order = await _orderService.RejectOrderAsync(
                authResult.TenantId,
                companyId,
                branchId,
                orderId,
                _currentUserContext.UserId,
                User.Identity?.Name ?? _currentUserContext.UserId.ToString(),
                request?.Remarks ?? string.Empty);

            return SuccessResponse(order, "Purchase order rejected successfully.");
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpPost("{orderId:guid}/cancel")]
    [RequirePermission("PURCHASE_ORDER.CANCEL")]
    public async Task<IActionResult> CancelOrder(
        Guid companyId,
        Guid branchId,
        Guid orderId,
        [FromBody] ApprovalDecisionRequest? request)
    {
        var authResult = await ValidateScopeAccess(companyId, branchId);
        if (authResult.Action != null) return authResult.Action;

        try
        {
            var order = await _orderService.CancelOrderAsync(
                authResult.TenantId,
                companyId,
                branchId,
                orderId,
                _currentUserContext.UserId,
                User.Identity?.Name ?? _currentUserContext.UserId.ToString(),
                request?.Remarks);

            return SuccessResponse(order, "Purchase order cancelled successfully.");
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpGet("{orderId:guid}/approval-history")]
    [RequirePermission("PURCHASE_ORDER.VIEW")]
    public async Task<IActionResult> GetApprovalHistory(
        Guid companyId,
        Guid branchId,
        Guid orderId)
    {
        var authResult = await ValidateScopeAccess(companyId, branchId);
        if (authResult.Action != null) return authResult.Action;

        try
        {
            var history = await _orderService.GetApprovalHistoryAsync(
                authResult.TenantId,
                companyId,
                branchId,
                orderId);

            return SuccessResponse(history);
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    private async Task<(IActionResult? Action, Guid TenantId)> ValidateScopeAccess(
        Guid companyId,
        Guid branchId)
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
