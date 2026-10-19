using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using API.Common;
using API.Common.Sorting;
using API.Security.Authorization;
using Domain.Features.Sales.SalesOrder;
using Microsoft.AspNetCore.Mvc;
using Security.Interfaces;

namespace API.Features.Domain;

[ApiController]
[Route("api/companies/{companyId:guid}/branches/{branchId:guid}/sales-orders")]
[RequirePermission("SALES_ORDER.VIEW")]
public class SalesOrderController : BaseApiController
{
    private readonly ISalesOrderService _orderService;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IUserAccessService _userAccessService;

    private static readonly SortMapping<SalesOrderDto> _sortMapping = new SortMapping<SalesOrderDto>()
        .Add("orderNumber", o => o.OrderNumber, isDefault: true)
        .Add("orderDate", o => o.OrderDate)
        .Add("totalAmount", o => o.TotalAmount)
        .Add("status", o => o.Status)
        .SetSecondary(o => o.Id);

    public SalesOrderController(
        ISalesOrderService orderService,
        ICurrentUserContext currentUserContext,
        IUserAccessService userAccessService)
    {
        _orderService = orderService;
        _currentUserContext = currentUserContext;
        _userAccessService = userAccessService;
    }

    [HttpGet]
    public async Task<IActionResult> GetOrders(
        Guid companyId,
        Guid branchId,
        [FromQuery] SalesOrderStatus? status = null,
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
            branchId,
            status);

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
            return NotFoundResponse("Sales order not found in the authorized scope.");
        }

        return SuccessResponse(order);
    }

    [HttpPost]
    [RequirePermission("SALES_ORDER.CREATE")]
    public async Task<IActionResult> CreateOrder(
        Guid companyId,
        Guid branchId,
        [FromBody] CreateSalesOrderRequest request)
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
    [RequirePermission("SALES_ORDER.EDIT")]
    public async Task<IActionResult> AddItem(
        Guid companyId,
        Guid branchId,
        Guid orderId,
        [FromBody] CreateSalesOrderItemRequest request)
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

            return SuccessResponse(item, "Item added to sales order successfully.");
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpPost("{orderId:guid}/submit")]
    [RequirePermission("SALES_ORDER.SUBMIT")]
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

            return SuccessResponse(order, "Sales order submitted successfully.");
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpPost("{orderId:guid}/confirm")]
    [RequirePermission("SALES_ORDER.CONFIRM")]
    public async Task<IActionResult> ConfirmOrder(
        Guid companyId,
        Guid branchId,
        Guid orderId,
        [FromBody] SalesOrderDecisionRequest? request)
    {
        var authResult = await ValidateScopeAccess(companyId, branchId);
        if (authResult.Action != null) return authResult.Action;

        try
        {
            var order = await _orderService.ConfirmOrderAsync(
                authResult.TenantId,
                companyId,
                branchId,
                orderId,
                _currentUserContext.UserId,
                User.Identity?.Name ?? _currentUserContext.UserId.ToString(),
                request?.Remarks);

            return SuccessResponse(order, "Sales order confirmed successfully.");
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpPost("{orderId:guid}/reject")]
    [RequirePermission("SALES_ORDER.CONFIRM")]
    public async Task<IActionResult> RejectOrder(
        Guid companyId,
        Guid branchId,
        Guid orderId,
        [FromBody] SalesOrderDecisionRequest request)
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

            return SuccessResponse(order, "Sales order rejected successfully.");
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpPost("{orderId:guid}/cancel")]
    [RequirePermission("SALES_ORDER.CANCEL")]
    public async Task<IActionResult> CancelOrder(
        Guid companyId,
        Guid branchId,
        Guid orderId,
        [FromBody] SalesOrderDecisionRequest? request)
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

            return SuccessResponse(order, "Sales order cancelled successfully.");
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
