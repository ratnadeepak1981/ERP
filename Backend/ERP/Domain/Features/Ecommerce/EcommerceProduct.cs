using System;
using Domain.Features.MasterData.Company;
using Domain.Features.MasterData.Product;
using ERP.Domain.Common;
using ERP.Domain.Common.Scope;

namespace Domain.Features.Ecommerce;

/// <summary>
/// Controls which existing ERP Product Master records are displayed in a Selling Company's
/// public e-commerce catalogue.
/// Scoped independently per Tenant and Selling Company.
/// </summary>
public class EcommerceProduct : Auditable, ITenantScopedEntity, ICompanyScopedEntity
{
    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid CompanyId { get; private set; }

    public Guid ProductId { get; private set; }

    public bool IsPublished { get; private set; }

    public string? OnlineDescription { get; private set; }

    public int DisplayOrder { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? UpdatedAtUtc { get; private set; }

    // Navigation properties
    public Company? Company { get; private set; }

    public Product? Product { get; private set; }

    public static EcommerceProduct Create(
        Guid tenantId,
        Guid companyId,
        Guid productId,
        string? onlineDescription = null,
        int displayOrder = 0,
        bool isPublished = false,
        Guid? id = null)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId is required.", nameof(tenantId));

        if (companyId == Guid.Empty)
            throw new ArgumentException("CompanyId is required.", nameof(companyId));

        if (productId == Guid.Empty)
            throw new ArgumentException("ProductId is required.", nameof(productId));

        if (displayOrder < 0)
            throw new ArgumentOutOfRangeException(nameof(displayOrder), "Display order cannot be negative.");

        var now = DateTime.UtcNow;

        return new EcommerceProduct
        {
            Id = id ?? Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            ProductId = productId,
            OnlineDescription = onlineDescription?.Trim(),
            DisplayOrder = displayOrder,
            IsPublished = isPublished,
            CreatedAtUtc = now,
            UpdatedAtUtc = null
        };
    }

    public void UpdateDetails(string? onlineDescription, int displayOrder)
    {
        if (displayOrder < 0)
            throw new ArgumentOutOfRangeException(nameof(displayOrder), "Display order cannot be negative.");

        OnlineDescription = onlineDescription?.Trim();
        DisplayOrder = displayOrder;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Publish()
    {
        IsPublished = true;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Unpublish()
    {
        IsPublished = false;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    private EcommerceProduct() { }
}
