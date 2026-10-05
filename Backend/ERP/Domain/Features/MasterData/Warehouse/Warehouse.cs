using ERP.Domain.Common;

namespace ERP.Domain.Features.MasterData.Warehouse;

public class Warehouse : Auditable
{
    public Guid WarehouseId { get; private set; }

    public Guid TenantId { get; private set; }

    public string WarehouseCode { get; private set; } = string.Empty;

    public string WarehouseName { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public string? AddressLine1 { get; private set; }

    public string? AddressLine2 { get; private set; }

    public string? City { get; private set; }

    public string? PostalCode { get; private set; }

    public string? Country { get; private set; }

    public bool IsActive { get; private set; }

    private Warehouse()
    {
    }
}