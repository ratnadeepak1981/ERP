using ERP.Domain.Common;
using ERP.Domain.Common.Scope;

namespace Domain.Features.MasterData.Currency;

public enum ExchangeRateType
{
    Daily = 1,
    Monthly = 2
}

public enum ExchangeRateSource
{
    SameCurrency = 0,
    Daily = 1,
    Monthly = 2
}

public class ExchangeRate : Auditable, ITenantScopedEntity
{
    public Guid ExchangeRateId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid FromCurrencyId { get; private set; }
    public Guid ToCurrencyId { get; private set; }
    public ExchangeRateType RateType { get; private set; }
    public DateTime EffectiveDate { get; private set; }
    public decimal Rate { get; private set; }
    public bool IsActive { get; private set; }
    public Currency FromCurrency { get; private set; } = null!;
    public Currency ToCurrency { get; private set; } = null!;

    public static ExchangeRate Create(
        Guid tenantId,
        Guid fromCurrencyId,
        Guid toCurrencyId,
        ExchangeRateType rateType,
        DateTime effectiveDate,
        decimal rate,
        Guid? exchangeRateId = null)
    {
        Validate(fromCurrencyId, toCurrencyId, rateType, rate);

        return new ExchangeRate
        {
            ExchangeRateId = exchangeRateId ?? Guid.NewGuid(),
            TenantId = tenantId,
            FromCurrencyId = fromCurrencyId,
            ToCurrencyId = toCurrencyId,
            RateType = rateType,
            EffectiveDate = NormalizeEffectiveDate(rateType, effectiveDate),
            Rate = rate,
            IsActive = true
        };
    }

    public void Update(
        Guid fromCurrencyId,
        Guid toCurrencyId,
        ExchangeRateType rateType,
        DateTime effectiveDate,
        decimal rate)
    {
        Validate(fromCurrencyId, toCurrencyId, rateType, rate);
        FromCurrencyId = fromCurrencyId;
        ToCurrencyId = toCurrencyId;
        RateType = rateType;
        EffectiveDate = NormalizeEffectiveDate(rateType, effectiveDate);
        Rate = rate;
    }

    public void Deactivate() => IsActive = false;
    public void Activate() => IsActive = true;

    private static void Validate(
        Guid fromCurrencyId,
        Guid toCurrencyId,
        ExchangeRateType rateType,
        decimal rate)
    {
        if (fromCurrencyId == Guid.Empty || toCurrencyId == Guid.Empty)
            throw new ArgumentException("Both currencies are required.");
        if (fromCurrencyId == toCurrencyId)
            throw new ArgumentException("Exchange rate currencies must be different.");
        if (!Enum.IsDefined(rateType))
            throw new ArgumentException("Exchange rate type must be Daily or Monthly.", nameof(rateType));
        if (rate <= 0)
            throw new ArgumentOutOfRangeException(nameof(rate), "Exchange rate must be greater than zero.");
    }

    private static DateTime NormalizeEffectiveDate(ExchangeRateType rateType, DateTime effectiveDate)
    {
        var date = effectiveDate.Date;
        return rateType == ExchangeRateType.Monthly
            ? new DateTime(date.Year, date.Month, 1)
            : date;
    }

    private ExchangeRate() { }
}
