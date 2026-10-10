using System;
using ERP.Domain.Common;

namespace Domain.Features.MasterData.Contact;

public class ContactType : Auditable
{
    public Guid ContactTypeId { get; private set; }

    public Guid? TenantId { get; private set; }

    public string Code { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public bool IsActive { get; private set; }

    public static ContactType Create(
        Guid? tenantId,
        string code,
        string name,
        string? description = null,
        Guid? contactTypeId = null)
    {
        return new ContactType
        {
            ContactTypeId = contactTypeId ?? Guid.NewGuid(),
            TenantId = tenantId,
            Code = code.Trim().ToUpperInvariant(),
            Name = name.Trim(),
            Description = description?.Trim(),
            IsActive = true
        };
    }

    public void Update(string name, string? description)
    {
        Name = name.Trim();
        Description = description?.Trim();
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    public void Activate()
    {
        IsActive = true;
    }

    private ContactType()
    {
    }
}
