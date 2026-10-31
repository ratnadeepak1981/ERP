namespace Domain.Features.MasterData.Currency;

public class ExchangeRateDto
{
    public Guid ExchangeRateId { get; set; }
    public Guid TenantId { get; set; }
    public Guid FromCurrencyId { get; set; }
    public string FromCurrencyCode { get; set; } = string.Empty;
    public Guid ToCurrencyId { get; set; }
    public string ToCurrencyCode { get; set; } = string.Empty;
    public ExchangeRateType RateType { get; set; }
    public DateTime EffectiveDate { get; set; }
    public decimal Rate { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ModifiedAt { get; set; }
}

public class CreateExchangeRateRequest
{
    public Guid FromCurrencyId { get; set; }
    public Guid ToCurrencyId { get; set; }
    public ExchangeRateType RateType { get; set; }
    public DateTime EffectiveDate { get; set; }
    public decimal Rate { get; set; }
}

public class UpdateExchangeRateRequest : CreateExchangeRateRequest
{
}

public sealed record ResolvedExchangeRate(
    decimal Rate,
    ExchangeRateSource Source,
    Guid? ExchangeRateId,
    DateTime? EffectiveDate);
