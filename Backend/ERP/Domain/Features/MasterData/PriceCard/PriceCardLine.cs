using System;
using ERP.Domain.Common;
using ERP.Domain.Common.Scope;

namespace Domain.Features.MasterData.PriceCard;

public class PriceCardLine : Auditable, ITenantScopedEntity
{
    public Guid PriceCardLineId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid PriceCardId { get; private set; }
    public Guid ProductId { get; private set; }
    public Guid? UnitOfMeasureId { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal MinimumQuantity { get; private set; } = 1m;
    public DateTime? EffectiveFrom { get; private set; }
    public DateTime? EffectiveTo { get; private set; }
    public bool IsActive { get; private set; } = true;

    // Navigation properties
    public PriceCard? PriceCard { get; private set; }
    public global::Domain.Features.MasterData.Product.Product? Product { get; private set; }
    public global::Domain.Features.MasterData.UnitOfMeasure.UnitOfMeasure? UnitOfMeasure { get; private set; }

    public static PriceCardLine Create(
        Guid tenantId,
        Guid priceCardId,
        Guid productId,
        decimal unitPrice,
        Guid? unitOfMeasureId = null,
        decimal minimumQuantity = 1m,
        DateTime? effectiveFrom = null,
        DateTime? effectiveTo = null,
        Guid? priceCardLineId = null)
    {
        if (unitPrice < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(unitPrice), "Unit price cannot be negative.");
        }

        if (minimumQuantity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumQuantity), "Minimum quantity cannot be negative.");
        }

        if (effectiveFrom.HasValue && effectiveTo.HasValue && effectiveTo.Value < effectiveFrom.Value)
        {
            throw new ArgumentException("EffectiveTo date cannot be earlier than EffectiveFrom date.");
        }

        return new PriceCardLine
        {
            PriceCardLineId = priceCardLineId ?? Guid.NewGuid(),
            TenantId = tenantId,
            PriceCardId = priceCardId,
            ProductId = productId,
            UnitOfMeasureId = unitOfMeasureId,
            UnitPrice = unitPrice,
            MinimumQuantity = minimumQuantity,
            EffectiveFrom = effectiveFrom,
            EffectiveTo = effectiveTo,
            IsActive = true
        };
    }

    public void Update(
        decimal unitPrice,
        Guid? unitOfMeasureId,
        decimal minimumQuantity,
        DateTime? effectiveFrom,
        DateTime? effectiveTo)
    {
        if (unitPrice < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(unitPrice), "Unit price cannot be negative.");
        }

        if (minimumQuantity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumQuantity), "Minimum quantity cannot be negative.");
        }

        if (effectiveFrom.HasValue && effectiveTo.HasValue && effectiveTo.Value < effectiveFrom.Value)
        {
            throw new ArgumentException("EffectiveTo date cannot be earlier than EffectiveFrom date.");
        }

        UnitPrice = unitPrice;
        UnitOfMeasureId = unitOfMeasureId;
        MinimumQuantity = minimumQuantity;
        EffectiveFrom = effectiveFrom;
        EffectiveTo = effectiveTo;
    }

    public void Deactivate() => IsActive = false;
    public void Activate() => IsActive = true;

    private PriceCardLine() { }
}
