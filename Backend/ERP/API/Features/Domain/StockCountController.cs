using System;
using System.Threading.Tasks;
using API.Common;
using API.Security.Authorization;
using ERP.Domain.Features.Inventory.StockCount;
using Microsoft.AspNetCore.Mvc;
using Security.Interfaces;

namespace API.Features.Domain;

[ApiController]
[Route("api/stock-counts")]
public class StockCountController : BaseApiController
{
    private readonly IStockCountService _stockCountService;
    private readonly ICurrentUserContext _currentUserContext;

    public StockCountController(
        IStockCountService stockCountService,
        ICurrentUserContext currentUserContext)
    {
        _stockCountService = stockCountService;
        _currentUserContext = currentUserContext;
    }

    [HttpGet]
    [RequirePermission("STOCK_COUNT.VIEW")]
    public async Task<IActionResult> GetStockCounts(
        [FromQuery] Guid? warehouseId = null,
        [FromQuery] Guid? warehouseLocationId = null,
        [FromQuery] StockCountStatus? status = null)
    {
        var tenantId = ResolveTenantId();
        if (tenantId == null)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        var counts = await _stockCountService.GetCountsAsync(tenantId.Value, warehouseId, warehouseLocationId, status);
        return SuccessResponse(counts);
    }

    [HttpGet("{id:guid}")]
    [RequirePermission("STOCK_COUNT.VIEW")]
    public async Task<IActionResult> GetStockCount(Guid id)
    {
        var tenantId = ResolveTenantId();
        if (tenantId == null)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        var count = await _stockCountService.GetByIdAsync(tenantId.Value, id);
        if (count == null)
            return NotFoundResponse("Stock count not found.");

        return SuccessResponse(count);
    }

    [HttpPost]
    [RequirePermission("STOCK_COUNT.CREATE")]
    public async Task<IActionResult> CreateDraft([FromBody] CreateStockCountDraftRequest request)
    {
        var tenantId = ResolveTenantId();
        if (tenantId == null)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        try
        {
            var result = await _stockCountService.CreateDraftAsync(tenantId.Value, request);
            return CreatedResponse(nameof(GetStockCount), new { id = result.StockCountId }, result);
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpPut("{id:guid}")]
    [RequirePermission("STOCK_COUNT.EDIT")]
    public async Task<IActionResult> UpdateDraft(Guid id, [FromBody] UpdateStockCountDraftRequest request)
    {
        var tenantId = ResolveTenantId();
        if (tenantId == null)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        try
        {
            var result = await _stockCountService.UpdateDraftAsync(tenantId.Value, id, request);
            return SuccessResponse(result, "Stock count draft updated successfully.");
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpPost("{id:guid}/submit")]
    [RequirePermission("STOCK_COUNT.SUBMIT")]
    public async Task<IActionResult> Submit(Guid id, [FromBody] SubmitStockCountRequest request)
    {
        var tenantId = ResolveTenantId();
        if (tenantId == null)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        try
        {
            request.SubmittedByUserId = _currentUserContext.UserId;
            var result = await _stockCountService.SubmitAsync(tenantId.Value, id, request);
            return SuccessResponse(result, "Stock count submitted successfully.");
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpPost("{id:guid}/approve")]
    [RequirePermission("STOCK_COUNT.APPROVE")]
    public async Task<IActionResult> Approve(Guid id, [FromBody] ApproveStockCountRequest request)
    {
        var tenantId = ResolveTenantId();
        if (tenantId == null)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        try
        {
            request.ApprovedByUserId = _currentUserContext.UserId;
            var result = await _stockCountService.ApproveAsync(tenantId.Value, id, request);
            return SuccessResponse(result, "Stock count approved successfully.");
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpPost("{id:guid}/post")]
    [RequirePermission("STOCK_COUNT.POST")]
    public async Task<IActionResult> Post(Guid id, [FromBody] PostStockCountRequest request)
    {
        var tenantId = ResolveTenantId();
        if (tenantId == null)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        try
        {
            request.PostedByUserId = _currentUserContext.UserId;
            var result = await _stockCountService.PostAsync(tenantId.Value, id, request);
            return SuccessResponse(result, "Stock count posted successfully.");
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpPost("{id:guid}/cancel")]
    [RequirePermission("STOCK_COUNT.CANCEL")]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] CancelStockCountRequest request)
    {
        var tenantId = ResolveTenantId();
        if (tenantId == null)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        try
        {
            request.CancelledByUserId = _currentUserContext.UserId;
            var result = await _stockCountService.CancelAsync(tenantId.Value, id, request);
            return SuccessResponse(result, "Stock count cancelled successfully.");
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    private Guid? ResolveTenantId()
    {
        if (_currentUserContext.IsPlatformUser)
        {
            return null;
        }

        return _currentUserContext.TenantId;
    }
}
