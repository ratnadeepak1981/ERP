using System;
using System.Collections.Generic;
using System.Linq;
using ERP.Domain.Common;
using ERP.Domain.Common.Scope;

namespace Domain.Features.MasterData.PriceCard;

public class PriceCard : Auditable, ITenantScopedEntity
{
    public Guid PriceCardId { get; private set; }
    public Guid TenantId { get; private set; }
    public string PriceCardCode { get; private set; } = string.Empty;
    public string PriceCardName { get; private set; } = string.Empty;
    public PriceCardType PriceCardType { get; private set; }
    public string Currency { get; private set; } = "USD";
    public DateTime EffectiveFrom { get; private set; }
    public DateTime? EffectiveTo { get; private set; }
    public string? Description { get; private set; }
    public bool IsActive { get; private set; } = true;

    private readonly List<PriceCardLine> _lines = new();
    public IReadOnlyCollection<PriceCardLine> Lines => _lines.AsReadOnly();

    public static PriceCard Create(
        Guid tenantId,
        string priceCardCode,
        string priceCardName,
        PriceCardType priceCardType,
        string currency,
        DateTime effectiveFrom,
        DateTime? effectiveTo = null,
        string? description = null,
        Guid? priceCardId = null)
    {
        if (string.IsNullOrWhiteSpace(priceCardCode))
            throw new ArgumentException("Price card code is required.", nameof(priceCardCode));

        if (string.IsNullOrWhiteSpace(priceCardName))
            throw new ArgumentException("Price card name is required.", nameof(priceCardName));

        if (effectiveTo.HasValue && effectiveTo.Value < effectiveFrom)
            throw new ArgumentException("EffectiveTo date cannot be earlier than EffectiveFrom date.");

        return new PriceCard
        {
            PriceCardId = priceCardId ?? Guid.NewGuid(),
            TenantId = tenantId,
            PriceCardCode = priceCardCode.Trim().ToUpperInvariant(),
            PriceCardName = priceCardName.Trim(),
            PriceCardType = priceCardType,
            Currency = string.IsNullOrWhiteSpace(currency) ? "USD" : currency.Trim().ToUpperInvariant(),
            EffectiveFrom = effectiveFrom,
            EffectiveTo = effectiveTo,
            Description = description?.Trim(),
            IsActive = true
        };
    }

    public void Update(
        string priceCardName,
        string currency,
        DateTime effectiveFrom,
        DateTime? effectiveTo,
        string? description)
    {
        if (string.IsNullOrWhiteSpace(priceCardName))
            throw new ArgumentException("Price card name is required.", nameof(priceCardName));

        if (effectiveTo.HasValue && effectiveTo.Value < effectiveFrom)
            throw new ArgumentException("EffectiveTo date cannot be earlier than EffectiveFrom date.");

        PriceCardName = priceCardName.Trim();
        Currency = string.IsNullOrWhiteSpace(currency) ? "USD" : currency.Trim().ToUpperInvariant();
        EffectiveFrom = effectiveFrom;
        EffectiveTo = effectiveTo;
        Description = description?.Trim();
    }

    public void Deactivate() => IsActive = false;
    public void Activate() => IsActive = true;

    public PriceCardLine AddLine(
        Guid productId,
        decimal unitPrice,
        Guid? unitOfMeasureId = null,
        decimal minimumQuantity = 1m,
        DateTime? effectiveFrom = null,
        DateTime? effectiveTo = null,
        Guid? lineId = null)
    {
        var line = PriceCardLine.Create(
            TenantId,
            PriceCardId,
            productId,
            unitPrice,
            unitOfMeasureId,
            minimumQuantity,
            effectiveFrom ?? EffectiveFrom,
            effectiveTo ?? EffectiveTo,
            lineId);

        _lines.Add(line);
        return line;
    }

    public void RemoveLine(Guid lineId)
    {
        var line = _lines.FirstOrDefault(l => l.PriceCardLineId == lineId);
        if (line != null)
        {
            _lines.Remove(line);
        }
    }

    private PriceCard() { }
}
