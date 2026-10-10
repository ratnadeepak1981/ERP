using System;
using ERP.Domain.Common;
using ERP.Domain.Common.Scope;

namespace ERP.Domain.Features.MasterData.Warehouse;

public class WarehouseAddress : Auditable, ITenantScopedEntity
{
    public Guid WarehouseAddressId { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid WarehouseId { get; private set; }

    public Guid AddressId { get; private set; }

    public Guid AddressTypeId { get; private set; }

    public bool IsDefault { get; private set; }

    public bool IsActive { get; private set; }

    public static WarehouseAddress Create(
        Guid tenantId,
        Guid warehouseId,
        Guid addressId,
        Guid addressTypeId,
        bool isDefault = false,
        Guid? warehouseAddressId = null)
    {
        return new WarehouseAddress
        {
            WarehouseAddressId = warehouseAddressId ?? Guid.NewGuid(),
            TenantId = tenantId,
            WarehouseId = warehouseId,
            AddressId = addressId,
            AddressTypeId = addressTypeId,
            IsDefault = isDefault,
            IsActive = true
        };
    }

    public void SetAsDefault(bool isDefault)
    {
        IsDefault = isDefault;
    }

    public void SetAddressType(Guid addressTypeId)
    {
        AddressTypeId = addressTypeId;
    }

    public void Deactivate()
    {
        IsActive = false;
        IsDefault = false;
    }

    public void Activate()
    {
        IsActive = true;
    }

    private WarehouseAddress()
    {
    }
}
