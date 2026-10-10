using System;
using ERP.Domain.Common;
using ERP.Domain.Common.Scope;

namespace ERP.Domain.Features.MasterData.Customer;

public class CustomerAddress : Auditable, ITenantScopedEntity
{
    public Guid CustomerAddressId { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid CustomerId { get; private set; }

    public Guid AddressId { get; private set; }

    public Guid AddressTypeId { get; private set; }

    public bool IsDefault { get; private set; }

    public bool IsActive { get; private set; }

    public static CustomerAddress Create(
        Guid tenantId,
        Guid customerId,
        Guid addressId,
        Guid addressTypeId,
        bool isDefault = false,
        Guid? customerAddressId = null)
    {
        return new CustomerAddress
        {
            CustomerAddressId = customerAddressId ?? Guid.NewGuid(),
            TenantId = tenantId,
            CustomerId = customerId,
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

    private CustomerAddress()
    {
    }
}
