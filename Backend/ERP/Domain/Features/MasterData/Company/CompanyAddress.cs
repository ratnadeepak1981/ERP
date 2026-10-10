using System;
using ERP.Domain.Common;
using ERP.Domain.Common.Scope;

namespace Domain.Features.MasterData.Company;

public class CompanyAddress : Auditable, ITenantScopedEntity
{
    public Guid CompanyAddressId { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid CompanyId { get; private set; }

    public Guid AddressId { get; private set; }

    public Guid AddressTypeId { get; private set; }

    public bool IsDefault { get; private set; }

    public bool IsActive { get; private set; }

    public static CompanyAddress Create(
        Guid tenantId,
        Guid companyId,
        Guid addressId,
        Guid addressTypeId,
        bool isDefault = false,
        Guid? companyAddressId = null)
    {
        return new CompanyAddress
        {
            CompanyAddressId = companyAddressId ?? Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
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

    private CompanyAddress()
    {
    }
}
