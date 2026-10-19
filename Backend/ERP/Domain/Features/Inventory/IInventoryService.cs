using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ERP.Domain.Features.Inventory;

public interface IInventoryService
{
    Task<InventorySummaryDto> GetSummaryAsync(Guid tenantId);
    Task<List<InventoryRecentTransactionDto>> GetRecentTransactionsAsync(Guid tenantId, int limit = 10);
    Task<List<StockIssueDto>> GetStockIssuesAsync(Guid tenantId, Guid? warehouseId = null, StockIssuePurpose? purpose = null);
    Task<StockIssueDto?> GetStockIssueByIdAsync(Guid tenantId, Guid issueId);
    Task<AvailableStockDto> GetAvailableStockAsync(Guid tenantId, Guid productId, Guid locationId, string? batchNumber = null, string? serialNumber = null);
    Task<List<StockIssueDto>> CreateStockIssueAsync(Guid tenantId, CreateStockIssueRequest request);
    Task<List<StockReceiptDto>> GetStockReceiptsAsync(Guid tenantId, Guid? warehouseId = null, StockReceiptReason? reason = null);
    Task<StockReceiptDto?> GetStockReceiptByIdAsync(Guid tenantId, Guid receiptId);
    Task<List<StockReceiptDto>> CreateStockReceiptAsync(Guid tenantId, CreateStockReceiptRequest request);
}
