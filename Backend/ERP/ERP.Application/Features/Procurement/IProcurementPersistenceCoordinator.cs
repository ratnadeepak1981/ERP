using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Domain.Features.MasterData.Company;
using Domain.Features.MasterData.Product;
using Domain.Features.Procurement.GoodsReceiptNote;
using Domain.Features.Procurement.PurchaseOrder;
using ERP.Domain.Features.MasterData.Warehouse;

namespace ERP.Application.Features.Procurement;

public interface IProcurementPersistenceCoordinator
{
    Task EnsureInventoryBalanceAsync(Guid tenantId, Guid warehouseId, Guid locationId, Guid productId);

    Task ApplyOnOrderDeltaAsync(
        Guid tenantId,
        Guid warehouseId,
        Guid locationId,
        Guid productId,
        decimal baseDelta,
        bool isApproval);

    Task DecrementOnOrderBalanceAsync(Guid tenantId, Guid warehouseId, Guid productId, decimal baseQuantity);

    Task RecordAuditAsync(ERP.Infrastructure.Persistence.Auditing.DomainAuditRecord record);

    Task<Company?> GetActiveCompanyWithBaseCurrencyAsync(Guid tenantId, Guid companyId);

    Task<List<Warehouse>> GetActiveBranchWarehousesAsync(Guid tenantId, Guid branchId);

    Task<Warehouse?> GetActiveCompanyWarehouseAsync(Guid tenantId, Guid companyId);

    Task<List<WarehouseLocation>> GetActiveLocationsAsync(Guid tenantId, Guid warehouseId);

    Task<Dictionary<Guid, Product>> GetProductsByIdsAsync(Guid tenantId, IEnumerable<Guid> productIds);

    Task<List<GoodsReceiptNoteLine>> GetPreviouslyConfirmedPoLinesAsync(
        Guid tenantId,
        Guid companyId,
        Guid purchaseOrderId,
        Guid currentGrnId,
        IEnumerable<Guid> poItemIds);

    Task<decimal> GetPreviouslyAllocatedAmountAsync(
        Guid tenantId,
        Guid companyId,
        Guid purchaseOrderId,
        Guid poItemId);

    Task ReplaceGrnLinesAsync(GoodsReceiptNote grn, IEnumerable<GoodsReceiptNoteLine> newLines);

    Task<IAsyncDisposable> BeginTransactionAsync();

    Task CommitTransactionAsync();

    Task RollbackTransactionAsync();

    Task SaveChangesAsync();
}
