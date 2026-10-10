using System;
using ERP.Domain.Common;

namespace Domain.Features.MasterData.AddressType;

public class AddressType : Auditable
{
    public Guid AddressTypeId { get; private set; }

    public Guid? TenantId { get; private set; }

    public string Code { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public bool IsActive { get; private set; }

    public static AddressType Create(
        Guid? tenantId,
        string code,
        string name,
        string? description = null,
        Guid? addressTypeId = null)
    {
        return new AddressType
        {
            AddressTypeId = addressTypeId ?? Guid.NewGuid(),
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

    private AddressType()
    {
    }
}
