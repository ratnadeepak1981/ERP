using System;
using ERP.Domain.Common;
using ERP.Domain.Common.Scope;

namespace Domain.Features.MasterData.Contact;

public class Contact : Auditable, ITenantScopedEntity
{
    public Guid ContactId { get; private set; }

    public Guid TenantId { get; private set; }

    public string ContactName { get; private set; } = string.Empty;

    public string? JobTitle { get; private set; }

    public string? Email { get; private set; }

    public string? Phone { get; private set; }

    public string? MobileNumber { get; private set; }

    public string? PreferredContactMethod { get; private set; }

    public bool IsActive { get; private set; }

    public static Contact Create(
        Guid tenantId,
        string contactName,
        string? jobTitle = null,
        string? email = null,
        string? phone = null,
        string? mobileNumber = null,
        string? preferredContactMethod = null,
        Guid? contactId = null)
    {
        return new Contact
        {
            ContactId = contactId ?? Guid.NewGuid(),
            TenantId = tenantId,
            ContactName = contactName.Trim(),
            JobTitle = jobTitle?.Trim(),
            Email = email?.Trim(),
            Phone = phone?.Trim(),
            MobileNumber = mobileNumber?.Trim(),
            PreferredContactMethod = preferredContactMethod?.Trim(),
            IsActive = true
        };
    }

    public void Update(
        string contactName,
        string? jobTitle,
        string? email,
        string? phone,
        string? mobileNumber,
        string? preferredContactMethod)
    {
        ContactName = contactName.Trim();
        JobTitle = jobTitle?.Trim();
        Email = email?.Trim();
        Phone = phone?.Trim();
        MobileNumber = mobileNumber?.Trim();
        PreferredContactMethod = preferredContactMethod?.Trim();
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    public void Activate()
    {
        IsActive = true;
    }

    private Contact()
    {
    }
}
