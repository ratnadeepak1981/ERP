using ERP.Domain.Common;
using ERP.Domain.Common.Scope;

namespace ERP.Domain.Features.MasterData.Warehouse;

public class WarehouseZone : Auditable, ITenantScopedEntity
{
    public Guid WarehouseZoneId { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid WarehouseId { get; private set; }

    public string ZoneCode { get; private set; } = string.Empty;

    public string ZoneName { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public bool IsActive { get; private set; }

    public static WarehouseZone Create(
        Guid tenantId,
        Guid warehouseId,
        string zoneCode,
        string zoneName,
        string? description = null,
        Guid? warehouseZoneId = null)
    {
        return new WarehouseZone
        {
            WarehouseZoneId = warehouseZoneId ?? Guid.NewGuid(),
            TenantId = tenantId,
            WarehouseId = warehouseId,
            ZoneCode = zoneCode,
            ZoneName = zoneName,
            Description = description,
            IsActive = true
        };
    }

    public void Update(string zoneName, string? description)
    {
        ZoneName = zoneName;
        Description = description;
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    public void Activate()
    {
        IsActive = true;
    }

    private WarehouseZone()
    {
    }
}