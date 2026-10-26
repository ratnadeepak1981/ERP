using System;
using ERP.Domain.Common;
using ERP.Domain.Common.Scope;

namespace Domain.Features.MasterData.Brand;

public class Brand : Auditable, ITenantScopedEntity
{
    public Guid BrandId { get; private set; }

    public Guid TenantId { get; private set; }

    public string BrandCode { get; private set; } = string.Empty;

    public string BrandName { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public bool IsActive { get; private set; }

    public static Brand Create(
        Guid tenantId,
        string brandCode,
        string brandName,
        string? description = null,
        Guid? brandId = null)
    {
        return new Brand
        {
            BrandId = brandId ?? Guid.NewGuid(),
            TenantId = tenantId,
            BrandCode = brandCode.Trim().ToUpperInvariant(),
            BrandName = brandName.Trim(),
            Description = description?.Trim(),
            IsActive = true
        };
    }

    public void Update(string brandName, string? description)
    {
        BrandName = brandName.Trim();
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

    private Brand()
    {
    }
}
