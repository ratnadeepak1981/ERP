using ERP.Domain.Common;

namespace ERP.Domain.Features.MasterData.Warehouse;

public class WarehouseZone : Auditable
{
    public Guid WarehouseZoneId { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid WarehouseId { get; private set; }

    public string ZoneCode { get; private set; } = string.Empty;

    public string ZoneName { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public bool IsActive { get; private set; }

    private WarehouseZone()
    {
    }
}