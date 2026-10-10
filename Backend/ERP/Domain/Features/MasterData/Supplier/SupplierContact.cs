using System;
using ERP.Domain.Common;
using ERP.Domain.Common.Scope;

namespace ERP.Domain.Features.MasterData.Supplier;

public class SupplierContact : Auditable, ITenantScopedEntity
{
    public Guid SupplierContactId { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid SupplierId { get; private set; }

    public Guid ContactId { get; private set; }

    public Guid ContactTypeId { get; private set; }

    public bool IsPrimary { get; private set; }

    public bool IsActive { get; private set; }

    public static SupplierContact Create(
        Guid tenantId,
        Guid supplierId,
        Guid contactId,
        Guid contactTypeId,
        bool isPrimary = false,
        Guid? supplierContactId = null)
    {
        return new SupplierContact
        {
            SupplierContactId = supplierContactId ?? Guid.NewGuid(),
            TenantId = tenantId,
            SupplierId = supplierId,
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

    private SupplierContact()
    {
    }
}
