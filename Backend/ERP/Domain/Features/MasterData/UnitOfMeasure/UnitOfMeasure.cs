using System;
using ERP.Domain.Common;
using ERP.Domain.Common.Scope;

namespace Domain.Features.MasterData.UnitOfMeasure;

public class UnitOfMeasure : Auditable, ITenantScopedEntity
{
    public Guid UnitOfMeasureId { get; private set; }

    public Guid TenantId { get; private set; }

    public string Code { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public bool IsActive { get; private set; }

    public static UnitOfMeasure Create(
        Guid tenantId,
        string code,
        string name,
        string? description = null,
        Guid? unitOfMeasureId = null)
    {
        return new UnitOfMeasure
        {
            UnitOfMeasureId = unitOfMeasureId ?? Guid.NewGuid(),
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

    private UnitOfMeasure()
    {
    }
}
