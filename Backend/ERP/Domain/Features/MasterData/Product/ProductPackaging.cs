using System;
using ERP.Domain.Common;
using ERP.Domain.Common.Scope;

namespace Domain.Features.MasterData.Product;

public class ProductPackaging : Auditable, ITenantScopedEntity
{
    public Guid ProductPackagingId { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid? CompanyId { get; private set; }

    public Guid ProductId { get; private set; }

    public string PackagingCode { get; private set; } = string.Empty;

    public string PackagingName { get; private set; } = string.Empty;

    public Guid PackagingUomId { get; private set; }

    public decimal BaseQuantity { get; private set; }

    public bool PurchaseAllowed { get; private set; } = true;

    public bool SalesAllowed { get; private set; } = true;

    public bool IsActive { get; private set; } = true;

    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    public static ProductPackaging Create(
        Guid tenantId,
        Guid productId,
        string packagingCode,
        string packagingName,
        Guid packagingUomId,
        decimal baseQuantity,
        bool purchaseAllowed = true,
        bool salesAllowed = true,
        Guid? companyId = null,
        Guid? productPackagingId = null)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId is required.", nameof(tenantId));

        if (productId == Guid.Empty)
            throw new ArgumentException("ProductId is required.", nameof(productId));

        if (packagingUomId == Guid.Empty)
            throw new ArgumentException("PackagingUomId is required.", nameof(packagingUomId));

        if (string.IsNullOrWhiteSpace(packagingCode))
            throw new ArgumentException("Packaging code is required.", nameof(packagingCode));

        if (string.IsNullOrWhiteSpace(packagingName))
            throw new ArgumentException("Packaging name is required.", nameof(packagingName));

        if (baseQuantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(baseQuantity), "Base quantity must be greater than zero.");

        if (!purchaseAllowed && !salesAllowed)
            throw new InvalidOperationException("Packaging definition must be allowed for at least Purchase or Sales.");

        return new ProductPackaging
        {
            ProductPackagingId = productPackagingId ?? Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            ProductId = productId,
            PackagingCode = packagingCode.Trim().ToUpperInvariant(),
            PackagingName = packagingName.Trim(),
            PackagingUomId = packagingUomId,
            BaseQuantity = baseQuantity,
            PurchaseAllowed = purchaseAllowed,
            SalesAllowed = salesAllowed,
            IsActive = true
        };
    }

    public void Update(
        string packagingName,
        decimal baseQuantity,
        bool purchaseAllowed,
        bool salesAllowed,
        Guid? companyId = null)
    {
        if (string.IsNullOrWhiteSpace(packagingName))
            throw new ArgumentException("Packaging name is required.", nameof(packagingName));

        if (baseQuantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(baseQuantity), "Base quantity must be greater than zero.");

        if (!purchaseAllowed && !salesAllowed)
            throw new InvalidOperationException("Packaging definition must be allowed for at least Purchase or Sales.");

        PackagingName = packagingName.Trim();
        BaseQuantity = baseQuantity;
        PurchaseAllowed = purchaseAllowed;
        SalesAllowed = salesAllowed;
        CompanyId = companyId;
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    public void Activate()
    {
        IsActive = true;
    }

    private ProductPackaging()
    {
    }
}
