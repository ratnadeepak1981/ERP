using System;
using ERP.Domain.Common;
using ERP.Domain.Common.Scope;

namespace Domain.Features.MasterData.Supplier;

public class SupplierProductPrice : Auditable, ITenantScopedEntity
{
    public Guid SupplierProductPriceId { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid SupplierId { get; private set; }

    public Guid ProductId { get; private set; }

    public string? SupplierItemCode { get; private set; }

    public decimal UnitPrice { get; private set; }

    public string Currency { get; private set; } = "USD";

    public Guid? UnitOfMeasureId { get; private set; }

    public decimal MinimumOrderQuantity { get; private set; }

    public DateTime EffectiveFrom { get; private set; }

    public DateTime? EffectiveTo { get; private set; }

    public int? LeadTimeDays { get; private set; }

    public bool IsPreferred { get; private set; }

    public bool IsActive { get; private set; }

    public static SupplierProductPrice Create(
        Guid tenantId,
        Guid supplierId,
        Guid productId,
        decimal unitPrice,
        DateTime effectiveFrom,
        string currency = "USD",
        string? supplierItemCode = null,
        Guid? unitOfMeasureId = null,
        decimal minimumOrderQuantity = 1,
        DateTime? effectiveTo = null,
        int? leadTimeDays = null,
        bool isPreferred = false,
        Guid? supplierProductPriceId = null)
    {
        if (unitPrice < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(unitPrice), "Unit price cannot be negative.");
        }

        if (minimumOrderQuantity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumOrderQuantity), "Minimum order quantity cannot be negative.");
        }

        if (effectiveTo.HasValue && effectiveTo.Value < effectiveFrom)
        {
            throw new ArgumentException("Effective to date cannot be earlier than effective from date.");
        }

        return new SupplierProductPrice
        {
            SupplierProductPriceId = supplierProductPriceId ?? Guid.NewGuid(),
            TenantId = tenantId,
            SupplierId = supplierId,
            ProductId = productId,
            UnitPrice = unitPrice,
            Currency = string.IsNullOrWhiteSpace(currency) ? "USD" : currency.Trim().ToUpperInvariant(),
            SupplierItemCode = supplierItemCode?.Trim(),
            UnitOfMeasureId = unitOfMeasureId,
            MinimumOrderQuantity = minimumOrderQuantity,
            EffectiveFrom = effectiveFrom,
            EffectiveTo = effectiveTo,
            LeadTimeDays = leadTimeDays,
            IsPreferred = isPreferred,
            IsActive = true
        };
    }

    public void Update(
        decimal unitPrice,
        DateTime effectiveFrom,
        string currency,
        string? supplierItemCode,
        Guid? unitOfMeasureId,
        decimal minimumOrderQuantity,
        DateTime? effectiveTo,
        int? leadTimeDays,
        bool isPreferred)
    {
        if (unitPrice < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(unitPrice), "Unit price cannot be negative.");
        }

        if (minimumOrderQuantity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumOrderQuantity), "Minimum order quantity cannot be negative.");
        }

        if (effectiveTo.HasValue && effectiveTo.Value < effectiveFrom)
        {
            throw new ArgumentException("Effective to date cannot be earlier than effective from date.");
        }

        UnitPrice = unitPrice;
        EffectiveFrom = effectiveFrom;
        Currency = string.IsNullOrWhiteSpace(currency) ? "USD" : currency.Trim().ToUpperInvariant();
        SupplierItemCode = supplierItemCode?.Trim();
        UnitOfMeasureId = unitOfMeasureId;
        MinimumOrderQuantity = minimumOrderQuantity;
        EffectiveTo = effectiveTo;
        LeadTimeDays = leadTimeDays;
        IsPreferred = isPreferred;
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    public void Activate()
    {
        IsActive = true;
    }

    private SupplierProductPrice()
    {
    }
}
