using System;
using ERP.Domain.Common;

namespace ERP.Domain.Features.MasterData.Warehouse;

public class LocationType : Auditable
{
    public Guid LocationTypeId { get; private set; }

    public Guid? TenantId { get; private set; }

    public string Code { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public bool CanStoreInventory { get; private set; } = true;

    public bool CanPick { get; private set; } = true;

    public bool CanPutAway { get; private set; } = true;

    public bool CanContainChildren { get; private set; } = true;

    public bool IsActive { get; private set; }

    public static LocationType Create(
        Guid? tenantId,
        string code,
        string name,
        string? description = null,
        bool canStoreInventory = true,
        bool canPick = true,
        bool canPutAway = true,
        bool canContainChildren = true,
        Guid? locationTypeId = null)
    {
        return new LocationType
        {
            LocationTypeId = locationTypeId ?? Guid.NewGuid(),
            TenantId = tenantId,
            Code = code.Trim().ToUpperInvariant(),
            Name = name.Trim(),
            Description = description?.Trim(),
            CanStoreInventory = canStoreInventory,
            CanPick = canPick,
            CanPutAway = canPutAway,
            CanContainChildren = canContainChildren,
            IsActive = true
        };
    }

    public void Update(
        string name,
        string? description,
        bool canStoreInventory,
        bool canPick,
        bool canPutAway,
        bool canContainChildren)
    {
        Name = name.Trim();
        Description = description?.Trim();
        CanStoreInventory = canStoreInventory;
        CanPick = canPick;
        CanPutAway = canPutAway;
        CanContainChildren = canContainChildren;
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    public void Activate()
    {
        IsActive = true;
    }

    private LocationType()
    {
    }
}
