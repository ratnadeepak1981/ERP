using ERP.Domain.Common;

namespace ERP.Domain.Features.MasterData.Warehouse;

public class WarehouseLocation : Auditable
{
    public Guid WarehouseLocationId { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid WarehouseId { get; private set; }

    public Guid WarehouseZoneId { get; private set; }

    public string LocationCode { get; private set; } = string.Empty;

    public string LocationName { get; private set; } = string.Empty;

    public bool IsActive { get; private set; }

    private WarehouseLocation()
    {
    }
}