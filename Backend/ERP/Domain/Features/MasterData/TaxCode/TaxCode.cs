using System;
using ERP.Domain.Common;
using ERP.Domain.Common.Scope;

namespace Domain.Features.MasterData.TaxCode;

public enum TaxApplicability
{
    Both = 1,
    Purchase = 2,
    Sales = 3
}

public class TaxCode : Auditable, ITenantScopedEntity
{
    public Guid TaxCodeId { get; private set; }

    public Guid TenantId { get; private set; }

    public string Code { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public decimal Rate { get; private set; }

    public TaxApplicability Applicability { get; private set; } = TaxApplicability.Both;

    public DateTime? EffectiveFrom { get; private set; }

    public DateTime? EffectiveTo { get; private set; }

    public string? Description { get; private set; }

    public bool IsActive { get; private set; }

    public static TaxCode Create(
        Guid tenantId,
        string code,
        string name,
        decimal rate,
        TaxApplicability applicability = TaxApplicability.Both,
        DateTime? effectiveFrom = null,
        DateTime? effectiveTo = null,
        string? description = null,
        Guid? taxCodeId = null)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Tax code cannot be empty.", nameof(code));

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Tax name cannot be empty.", nameof(name));

        if (rate < 0)
            throw new ArgumentException("Tax rate cannot be negative.", nameof(rate));

        if (effectiveFrom.HasValue && effectiveTo.HasValue && effectiveFrom.Value > effectiveTo.Value)
            throw new ArgumentException("EffectiveFrom cannot be later than EffectiveTo.", nameof(effectiveFrom));

        return new TaxCode
        {
            TaxCodeId = taxCodeId ?? Guid.NewGuid(),
            TenantId = tenantId,
            Code = code.Trim().ToUpperInvariant(),
            Name = name.Trim(),
            Rate = Math.Round(rate, 4, MidpointRounding.AwayFromZero),
            Applicability = applicability,
            EffectiveFrom = effectiveFrom,
            EffectiveTo = effectiveTo,
            Description = description?.Trim(),
            IsActive = true
        };
    }

    public void Update(
        string name,
        decimal rate,
        TaxApplicability applicability,
        DateTime? effectiveFrom,
        DateTime? effectiveTo,
        string? description)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Tax name cannot be empty.", nameof(name));

        if (rate < 0)
            throw new ArgumentException("Tax rate cannot be negative.", nameof(rate));

        if (effectiveFrom.HasValue && effectiveTo.HasValue && effectiveFrom.Value > effectiveTo.Value)
            throw new ArgumentException("EffectiveFrom cannot be later than EffectiveTo.", nameof(effectiveFrom));

        Name = name.Trim();
        Rate = Math.Round(rate, 4, MidpointRounding.AwayFromZero);
        Applicability = applicability;
        EffectiveFrom = effectiveFrom;
        EffectiveTo = effectiveTo;
        Description = description?.Trim();
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    public void Reactivate()
    {
        IsActive = true;
    }

    public bool IsEffectiveAt(DateTime date)
    {
        if (!IsActive) return false;
        if (EffectiveFrom.HasValue && date < EffectiveFrom.Value) return false;
        if (EffectiveTo.HasValue && date > EffectiveTo.Value) return false;
        return true;
    }

    public bool AppliesTo(TaxApplicability target)
    {
        if (Applicability == TaxApplicability.Both) return true;
        return Applicability == target;
    }
}
