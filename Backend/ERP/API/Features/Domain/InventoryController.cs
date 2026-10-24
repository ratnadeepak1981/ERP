using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using API.Common;
using API.Common.Sorting;
using API.Security.Authorization;
using ERP.Domain.Features.Inventory;
using Microsoft.AspNetCore.Mvc;
using Security.Interfaces;

namespace API.Features.Domain;

[ApiController]
[Route("api/inventory")]
[RequirePermission("INVENTORY.VIEW")]
public class InventoryController : BaseApiController
{
    private readonly IInventoryService _inventoryService;
    private readonly ICurrentUserContext _currentUserContext;

    private static readonly SortMapping<StockIssueDto> _stockIssueSortMapping = new SortMapping<StockIssueDto>()
        .Add("issueNumber", s => s.IssueNumber, isDefault: true)
        .Add("issueDate", s => s.IssueDate)
        .Add("productName", s => s.ProductName)
        .Add("quantity", s => s.Quantity)
        .Add("totalCost", s => s.TotalCost)
        .Add("purposeName", s => s.PurposeName)
        .SetSecondary(s => s.StockIssueId);

    private static readonly SortMapping<StockReceiptDto> _stockReceiptSortMapping = new SortMapping<StockReceiptDto>()
        .Add("receiptNumber", s => s.ReceiptNumber, isDefault: true)
        .Add("receiptDate", s => s.ReceiptDate)
        .Add("productName", s => s.ProductName)
        .Add("quantity", s => s.Quantity)
        .Add("totalCost", s => s.TotalCost)
        .Add("reasonName", s => s.ReasonName)
        .SetSecondary(s => s.StockReceiptId);

    public InventoryController(
        IInventoryService inventoryService,
        ICurrentUserContext currentUserContext)
    {
        _inventoryService = inventoryService;
        _currentUserContext = currentUserContext;
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary()
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

        var summary = await _inventoryService.GetSummaryAsync(tenantId.Value);
        return SuccessResponse(summary);
    }

    [HttpGet("recent-transactions")]
    public async Task<IActionResult> GetRecentTransactions([FromQuery] int limit = 10)
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

        var transactions = await _inventoryService.GetRecentTransactionsAsync(tenantId.Value, limit);
        return SuccessResponse(transactions);
    }

    [HttpGet("stock-issues")]
    public async Task<IActionResult> GetStockIssues(
        [FromQuery] Guid? warehouseId = null,
        [FromQuery] StockIssuePurpose? purpose = null,
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

        var issues = await _inventoryService.GetStockIssuesAsync(tenantId.Value, warehouseId, purpose);
        return PaginateAndSort(issues, page, pageSize, sortBy, sortDirection, _stockIssueSortMapping);
    }

    [HttpGet("stock-issues/{id:guid}")]
    public async Task<IActionResult> GetStockIssue(Guid id)
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

        var issue = await _inventoryService.GetStockIssueByIdAsync(tenantId.Value, id);
        if (issue == null)
        {
            return NotFoundResponse("Stock issue record not found.");
        }

        return SuccessResponse(issue);
    }

    [HttpGet("available-stock")]
    public async Task<IActionResult> GetAvailableStock(
        [FromQuery] Guid productId,
        [FromQuery] Guid warehouseLocationId,
        [FromQuery] string? batchNumber = null,
        [FromQuery] string? serialNumber = null)
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

        var stock = await _inventoryService.GetAvailableStockAsync(tenantId.Value, productId, warehouseLocationId, batchNumber, serialNumber);
        return SuccessResponse(stock);
    }

    [HttpPost("stock-issues")]
    [RequirePermission("STOCK_ISSUE.POST")]
    public async Task<IActionResult> CreateStockIssue([FromBody] CreateStockIssueRequest request)
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

        try
        {
            var results = await _inventoryService.CreateStockIssueAsync(tenantId.Value, request);
            return SuccessResponse(results, "Stock issued successfully.");
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpGet("stock-receipts")]
    public async Task<IActionResult> GetStockReceipts(
        [FromQuery] Guid? warehouseId = null,
        [FromQuery] StockReceiptReason? reason = null,
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

        var receipts = await _inventoryService.GetStockReceiptsAsync(tenantId.Value, warehouseId, reason);
        return PaginateAndSort(receipts, page, pageSize, sortBy, sortDirection, _stockReceiptSortMapping);
    }

    [HttpGet("stock-receipts/{id:guid}")]
    public async Task<IActionResult> GetStockReceipt(Guid id)
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

        var receipt = await _inventoryService.GetStockReceiptByIdAsync(tenantId.Value, id);
        if (receipt == null)
        {
            return NotFoundResponse("Stock receipt not found.");
        }

        return SuccessResponse(receipt);
    }

    [HttpPost("stock-receipts")]
    [RequirePermission("STOCK_RECEIPT.POST")]
    public async Task<IActionResult> CreateStockReceipt([FromBody] CreateStockReceiptRequest request)
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

        try
        {
            var results = await _inventoryService.CreateStockReceiptAsync(tenantId.Value, request);
            return SuccessResponse(results, "Stock received successfully.");
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }
}
