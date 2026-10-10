using System;
using ERP.Domain.Common;
using ERP.Domain.Common.Scope;

namespace ERP.Domain.Features.MasterData.Supplier;

public class SupplierAddress : Auditable, ITenantScopedEntity
{
    public Guid SupplierAddressId { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid SupplierId { get; private set; }

    public Guid AddressId { get; private set; }

    public Guid AddressTypeId { get; private set; }

    public bool IsDefault { get; private set; }

    public bool IsActive { get; private set; }

    public static SupplierAddress Create(
        Guid tenantId,
        Guid supplierId,
        Guid addressId,
        Guid addressTypeId,
        bool isDefault = false,
        Guid? supplierAddressId = null)
    {
        return new SupplierAddress
        {
            SupplierAddressId = supplierAddressId ?? Guid.NewGuid(),
            TenantId = tenantId,
            SupplierId = supplierId,
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

    private SupplierAddress()
    {
    }
}
