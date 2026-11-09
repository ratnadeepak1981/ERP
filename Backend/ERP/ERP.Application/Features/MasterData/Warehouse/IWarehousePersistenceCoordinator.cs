using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ERP.Domain.Features.MasterData.Warehouse;
using WarehouseEntity = ERP.Domain.Features.MasterData.Warehouse.Warehouse;

namespace ERP.Application.Features.MasterData.Warehouse;

public interface IWarehousePersistenceCoordinator
{
    Task<IAsyncDisposable?> BeginTransactionIfNeededAsync();
    Task CommitTransactionIfNeededAsync(IAsyncDisposable? tx);
    Task RollbackTransactionIfNeededAsync(IAsyncDisposable? tx);
    Task SaveChangesAsync();

    Task<WarehouseEntity?> GetWarehouseRawByIdAsync(Guid tenantId, Guid warehouseId);
    Task<WarehouseZone?> GetZoneRawByIdAsync(Guid tenantId, Guid zoneId);
    Task<WarehouseLocation?> GetLocationRawByIdAsync(Guid tenantId, Guid locationId);

    Task ValidateWarehouseDeactivationSafetyAsync(Guid tenantId, WarehouseEntity warehouse);
    Task ValidateZoneDeactivationSafetyAsync(Guid tenantId, WarehouseZone zone);
    Task ValidateLocationDeactivationSafetyAsync(Guid tenantId, WarehouseLocation location, List<Guid> subtreeLocationIds);
}
