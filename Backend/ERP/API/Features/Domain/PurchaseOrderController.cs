using System;
using System.Threading.Tasks;
using API.Security.Authorization;
using Domain.Features.Procurement.PurchaseOrder;
using Microsoft.AspNetCore.Mvc;
using Security.Interfaces;

namespace API.Features.Domain;

[ApiController]
[Route("api/companies/{companyId:guid}/branches/{branchId:guid}/purchase-orders")]
[RequirePermission("PURCHASE_ORDER.VIEW")]
public class PurchaseOrderController : ControllerBase
{
    private readonly IPurchaseOrderService _orderService;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IUserAccessService _userAccessService;

    public PurchaseOrderController(
        IPurchaseOrderService orderService,
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
        Guid branchId)
    {
        var authResult = await ValidateScopeAccess(companyId, branchId);
        if (authResult.Action != null) return authResult.Action;

        var orders = await _orderService.GetOrdersAsync(
            authResult.TenantId,
            companyId,
            branchId);

        return Ok(orders);
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
            return NotFound(new
            {
                status = "FAIL",
                message = "Purchase order not found in the authorized scope."
            });
        }

        return Ok(order);
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
                request);

            return CreatedAtAction(
                nameof(GetOrder),
                new { companyId, branchId, id = order.Id },
                order);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new
            {
                status = "FAIL",
                message = ex.Message
            });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new
            {
                status = "FAIL",
                message = ex.Message
            });
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

            return Ok(item);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new
            {
                status = "FAIL",
                message = ex.Message
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new
            {
                status = "FAIL",
                message = ex.Message
            });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new
            {
                status = "FAIL",
                message = ex.Message
            });
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
            return (Unauthorized(new
            {
                status = "FAIL",
                message = "Tenant identity is missing or invalid."
            }), Guid.Empty);
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
