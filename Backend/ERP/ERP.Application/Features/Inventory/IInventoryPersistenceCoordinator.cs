using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Domain.Features.MasterData.Product;
using Domain.Features.MasterData.UnitOfMeasure;
using Domain.Features.Procurement.GoodsReceiptNote;
using Domain.Features.Inventory.Putaway;
using ERP.Domain.Features.Inventory;
using ERP.Domain.Features.Inventory.Batch;
using ERP.Domain.Features.MasterData.Warehouse;

namespace ERP.Application.Features.Inventory;

public class InventorySummaryData
{
    public int TotalStockItems { get; set; }
    public int TotalWarehouses { get; set; }
    public int TotalLocations { get; set; }
    public int LowStockItems { get; set; }
    public int TotalTransactions { get; set; }
    public decimal TotalOnHandQuantity { get; set; }
    public decimal TotalInventoryValue { get; set; }
}

public class PutawayReferenceLookups
{
    public Dictionary<Guid, Warehouse> Warehouses { get; set; } = new();
    public Dictionary<Guid, Product> Products { get; set; } = new();
    public Dictionary<Guid, WarehouseLocation> Locations { get; set; } = new();
    public Dictionary<Guid, UnitOfMeasure> UnitsOfMeasure { get; set; } = new();
}

public class ApprovedPoLineItem
{
    public Guid BranchId { get; set; }
    public Guid ProductId { get; set; }
    public decimal OutstandingQty { get; set; }
}

public class InventoryPlanningData
{
    public List<Warehouse> TargetWarehouses { get; set; } = new();
    public List<Product> Products { get; set; } = new();
    public Dictionary<Guid, string> Categories { get; set; } = new();
    public List<InventoryBalance> Balances { get; set; } = new();
    public List<ProductWarehouseSetting> WarehouseSettings { get; set; } = new();
    public Dictionary<(Guid ProductId, string BatchNumber), DateTime?> BatchExpiryDict { get; set; } = new();
    public List<ApprovedPoLineItem> ApprovedPoLines { get; set; } = new();
}

public interface IInventoryPersistenceCoordinator
{
    bool HasActiveTransaction { get; }
    Task<IAsyncDisposable?> BeginTransactionIfNeededAsync();
    Task CommitTransactionIfNeededAsync(IAsyncDisposable? tx);
    Task RollbackTransactionIfNeededAsync(IAsyncDisposable? tx);
    Task SaveChangesAsync();
    Task ExecuteStrategyAsync(Func<Task> operation);
    Task<T> ExecuteStrategyAsync<T>(Func<Task<T>> operation);

    // Queries for InventoryService
    Task<InventorySummaryData> GetSummaryDataAsync(Guid tenantId);
    Task<List<InventoryRecentTransactionDto>> GetRecentTransactionsAsync(Guid tenantId, int limit);
    Task<List<StockIssueDto>> GetStockIssuesAsync(Guid tenantId, Guid? warehouseId, StockIssuePurpose? purpose);
    Task<StockIssueDto?> GetStockIssueByIdAsync(Guid tenantId, Guid issueId);
    Task<List<StockReceiptDto>> GetStockReceiptsAsync(Guid tenantId, Guid? warehouseId, StockReceiptReason? reason);
    Task<StockReceiptDto?> GetStockReceiptByIdAsync(Guid tenantId, Guid receiptId);
    Task<StockByLocationReportDto> GetStockByLocationReportAsync(Guid tenantId, StockByLocationReportQuery query);
    Task<StockLedgerReportDto> GetStockLedgerReportAsync(Guid tenantId, StockLedgerReportQuery query);

    // Lookups & operations for InventoryService
    Task<Dictionary<Guid, Product>> GetActiveProductsByIdsAsync(Guid tenantId, IEnumerable<Guid> productIds);
    Task<Dictionary<Guid, WarehouseLocation>> GetActiveLocationsByIdsAsync(Guid tenantId, IEnumerable<Guid> locationIds);
    Task<bool> HasTransactionsBySourceAsync(Guid tenantId, SourceDocumentType sourceDocType, Guid sourceDocId);

    // Queries for BatchService
    Task<List<ProductBatchDto>> GetBatchesAsync(Guid tenantId, Guid? productId);
    Task<ProductBatchDto?> GetBatchDetailsAsync(Guid tenantId, Guid batchId);
    Task<ProductBatch?> GetBatchByIdAsync(Guid tenantId, Guid productBatchId);
    Task<List<ProductBatch>> GetBatchesListAsync(Guid tenantId, Guid? productId);
    Task AddProductBatchAsync(ProductBatch batch);

    // Queries for PlanningService
    Task<InventoryPlanningData> GetPlanningDataAsync(Guid tenantId, Guid? warehouseId, Guid? categoryId);
    Task<List<Warehouse>> GetTargetWarehousesAsync(Guid tenantId, Guid? warehouseId);
    Task<List<Product>> GetTargetProductsAsync(Guid tenantId, Guid? categoryId);
    Task<Dictionary<Guid, decimal>> GetStockOnHandByWarehouseAndProductAsync(Guid tenantId, IEnumerable<Guid> warehouseIds, IEnumerable<Guid> productIds);
    Task<Dictionary<Guid, decimal>> GetApprovedOnOrderQuantitiesAsync(Guid tenantId, IEnumerable<Guid> productIds);

    // Lookups & operations for PutawayService
    Task<List<GoodsReceiptNote>> GetConfirmedGrnsAsync(Guid tenantId, Guid companyId, Guid branchId, Guid? warehouseId);
    Task<HashSet<Guid>> GetStockTrackedProductIdsAsync(Guid tenantId, IEnumerable<Guid> productIds);
    Task<List<InventoryTransaction>> GetGrnReceiptTransactionsAsync(Guid tenantId, Guid grnId);
    Task<List<InventoryTransaction>> GetStockAdjustmentInTransactionsAsync(Guid tenantId, Guid companyId, Guid branchId, Guid? warehouseId);
    Task<WarehouseLocation?> GetLocationDetailsAsync(Guid tenantId, Guid locationId);
    Task<LocationType?> GetLocationTypeAsync(Guid tenantId, Guid locationTypeId);
    Task<GoodsReceiptNote?> GetGrnWithLinesAsync(Guid tenantId, Guid grnId);
    Task<List<InventoryTransaction>> GetGrnTransactionsAsync(Guid tenantId, Guid grnId);
    Task<List<InventoryTransaction>> GetStockReceiptTransactionsAsync(Guid tenantId, Guid receiptDocId);
    Task<Product?> GetProductAsync(Guid tenantId, Guid productId);
    Task<WarehouseLocation?> GetLocationAsync(Guid tenantId, Guid locationId);
    Task<Warehouse?> GetWarehouseAsync(Guid tenantId, Guid warehouseId);
    Task<UnitOfMeasure?> GetUnitOfMeasureAsync(Guid tenantId, Guid uomId);
    Task RemovePutawayLinesAndAllocationsAsync(global::Domain.Features.Inventory.Putaway.Putaway putaway);
    Task<PutawayReferenceLookups> GetPutawayReferenceLookupsAsync(
        Guid tenantId,
        IEnumerable<Guid> warehouseIds,
        IEnumerable<Guid> productIds,
        IEnumerable<Guid> locationIds,
        IEnumerable<Guid> uomIds);
}
