using System;
using ERP.Domain.Common;
using ERP.Domain.Common.Scope;

namespace ERP.Domain.Features.MasterData.Customer;

public class CustomerContact : Auditable, ITenantScopedEntity
{
    public Guid CustomerContactId { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid CustomerId { get; private set; }

    public Guid ContactId { get; private set; }

    public Guid ContactTypeId { get; private set; }

    public bool IsPrimary { get; private set; }

    public bool IsActive { get; private set; }

    public static CustomerContact Create(
        Guid tenantId,
        Guid customerId,
        Guid contactId,
        Guid contactTypeId,
        bool isPrimary = false,
        Guid? customerContactId = null)
    {
        return new CustomerContact
        {
            CustomerContactId = customerContactId ?? Guid.NewGuid(),
            TenantId = tenantId,
            CustomerId = customerId,
            ContactId = contactId,
            ContactTypeId = contactTypeId,
            IsPrimary = isPrimary,
            IsActive = true
        };
    }

    public void SetAsPrimary(bool isPrimary)
    {
        IsPrimary = isPrimary;
    }

    public void SetContactType(Guid contactTypeId)
    {
        ContactTypeId = contactTypeId;
    }

    public void Deactivate()
    {
        IsActive = false;
        IsPrimary = false;
    }

    public void Activate()
    {
        IsActive = true;
    }

    private CustomerContact()
    {
    }
}
