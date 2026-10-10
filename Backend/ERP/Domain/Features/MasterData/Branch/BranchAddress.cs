using System;
using ERP.Domain.Common;
using ERP.Domain.Common.Scope;

namespace Domain.Features.MasterData.Branch;

public class BranchAddress : Auditable, ITenantScopedEntity
{
    public Guid BranchAddressId { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid BranchId { get; private set; }

    public Guid AddressId { get; private set; }

    public Guid AddressTypeId { get; private set; }

    public bool IsDefault { get; private set; }

    public bool IsActive { get; private set; }

    public static BranchAddress Create(
        Guid tenantId,
        Guid branchId,
        Guid addressId,
        Guid addressTypeId,
        bool isDefault = false,
        Guid? branchAddressId = null)
    {
        return new BranchAddress
        {
            BranchAddressId = branchAddressId ?? Guid.NewGuid(),
            TenantId = tenantId,
            BranchId = branchId,
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

    private BranchAddress()
    {
    }
}
