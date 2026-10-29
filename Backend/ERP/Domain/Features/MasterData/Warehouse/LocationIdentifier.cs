using System;
using Domain.Features.MasterData.Common;
using ERP.Domain.Common;
using ERP.Domain.Common.Scope;

namespace ERP.Domain.Features.MasterData.Warehouse;

public class LocationIdentifier : Auditable, ITenantScopedEntity
{
    public Guid LocationIdentifierId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid WarehouseId { get; private set; }
    public Guid WarehouseLocationId { get; private set; }
    public string IdentifierValue { get; private set; } = string.Empty;
    public IdentifierType IdentifierType { get; private set; }
    public IdentifierSymbology Symbology { get; private set; }
    public IdentifierSource Source { get; private set; }
    public bool IsPrimary { get; private set; }
    public bool IsActive { get; private set; } = true;
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    public static LocationIdentifier Create(
        Guid tenantId,
        Guid warehouseId,
        Guid warehouseLocationId,
        string identifierValue,
        IdentifierType identifierType,
        IdentifierSymbology symbology,
        IdentifierSource source = IdentifierSource.Internal,
        bool isPrimary = true,
        Guid? locationIdentifierId = null)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (warehouseId == Guid.Empty)
            throw new ArgumentException("WarehouseId is required.", nameof(warehouseId));
        if (warehouseLocationId == Guid.Empty)
            throw new ArgumentException("WarehouseLocationId is required.", nameof(warehouseLocationId));
        if (string.IsNullOrWhiteSpace(identifierValue))
            throw new ArgumentException("Identifier value cannot be empty.", nameof(identifierValue));

        string normalized = identifierValue.Trim().ToUpperInvariant();

        return new LocationIdentifier
        {
            LocationIdentifierId = locationIdentifierId ?? Guid.NewGuid(),
            TenantId = tenantId,
            WarehouseId = warehouseId,
            WarehouseLocationId = warehouseLocationId,
            IdentifierValue = normalized,
            IdentifierType = identifierType,
            Symbology = symbology,
            Source = source,
            IsPrimary = isPrimary,
            IsActive = true
        };
    }

    public void PromoteToPrimary()
    {
        if (!IsActive)
            throw new InvalidOperationException("Cannot set an inactive identifier as primary.");
        IsPrimary = true;
    }

    public void DemoteFromPrimary()
    {
        IsPrimary = false;
    }

    public void Deactivate()
    {
        IsActive = false;
        IsPrimary = false;
    }

    public void Reactivate(bool isPrimary = false)
    {
        IsActive = true;
        IsPrimary = isPrimary;
    }

    private LocationIdentifier()
    {
    }
}
