namespace Domain.Features.MasterData.Currency;

public interface IExchangeRateService
{
    Task<List<ExchangeRateDto>> GetRatesAsync(Guid tenantId);
    Task<ExchangeRateDto?> GetRateAsync(Guid tenantId, Guid exchangeRateId);
    Task<ExchangeRateDto> CreateRateAsync(Guid tenantId, CreateExchangeRateRequest request);
    Task<ExchangeRateDto> UpdateRateAsync(Guid tenantId, Guid exchangeRateId, UpdateExchangeRateRequest request);
    Task DeactivateRateAsync(Guid tenantId, Guid exchangeRateId);
    Task ActivateRateAsync(Guid tenantId, Guid exchangeRateId);
    Task<ResolvedExchangeRate> ResolveAsync(
        Guid tenantId,
        Guid fromCurrencyId,
        Guid toCurrencyId,
        DateTime transactionDate);
}
