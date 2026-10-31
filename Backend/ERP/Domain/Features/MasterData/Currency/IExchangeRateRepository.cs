namespace Domain.Features.MasterData.Currency;

public interface IExchangeRateRepository
{
    Task<List<ExchangeRate>> GetRatesAsync(Guid tenantId);
    Task<ExchangeRate?> GetByIdAsync(Guid tenantId, Guid exchangeRateId);
    Task<bool> ExistsAsync(
        Guid tenantId,
        Guid fromCurrencyId,
        Guid toCurrencyId,
        ExchangeRateType rateType,
        DateTime effectiveDate,
        Guid? excludeId = null);
    Task<ExchangeRate?> GetLatestDailyRateAsync(
        Guid tenantId,
        Guid fromCurrencyId,
        Guid toCurrencyId,
        DateTime asOfDate);
    Task<ExchangeRate?> GetMonthlyRateAsync(
        Guid tenantId,
        Guid fromCurrencyId,
        Guid toCurrencyId,
        DateTime effectiveMonth);
    Task<ExchangeRate?> GetLatestMonthlyRateBeforeAsync(
        Guid tenantId,
        Guid fromCurrencyId,
        Guid toCurrencyId,
        DateTime effectiveMonth);
    Task AddAsync(ExchangeRate rate);
    Task SaveChangesAsync();
}
