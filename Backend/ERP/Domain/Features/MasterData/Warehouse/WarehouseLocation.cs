using ERP.Domain.Common;
using ERP.Domain.Common.Scope;

namespace ERP.Domain.Features.MasterData.Warehouse;

public class WarehouseLocation : Auditable, ITenantScopedEntity
{
    public Guid WarehouseLocationId { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid WarehouseId { get; private set; }

    public Guid? WarehouseZoneId { get; private set; }

    public Guid? LocationTypeId { get; private set; }

    public Guid? ParentLocationId { get; private set; }

    public string LocationCode { get; private set; } = string.Empty;

    public string LocationName { get; private set; } = string.Empty;

    public bool IsActive { get; private set; }

    public static WarehouseLocation Create(
        Guid tenantId,
        Guid warehouseId,
        Guid? warehouseZoneId,
        string locationCode,
        string locationName,
        Guid? locationTypeId = null,
        Guid? parentLocationId = null,
        Guid? warehouseLocationId = null)
    {
        return new WarehouseLocation
        {
            WarehouseLocationId = warehouseLocationId ?? Guid.NewGuid(),
            TenantId = tenantId,
            WarehouseId = warehouseId,
            WarehouseZoneId = warehouseZoneId,
            LocationTypeId = locationTypeId,
            ParentLocationId = parentLocationId,
            LocationCode = locationCode,
            LocationName = locationName,
            IsActive = true
        };
    }

    public void Update(
        string locationName,
        Guid? locationTypeId,
        Guid? parentLocationId)
    {
        LocationName = locationName;
        LocationTypeId = locationTypeId;
        ParentLocationId = parentLocationId;
    }

    public void SetParentLocation(Guid? parentLocationId)
    {
        ParentLocationId = parentLocationId;
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    public void Activate()
    {
        IsActive = true;
    }

    private WarehouseLocation()
    {
    }
}