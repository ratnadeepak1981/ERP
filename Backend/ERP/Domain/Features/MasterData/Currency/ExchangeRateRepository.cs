using Domain.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Domain.Features.MasterData.Currency;

public class ExchangeRateRepository : IExchangeRateRepository
{
    private readonly DomainDbContext _context;

    public ExchangeRateRepository(DomainDbContext context)
    {
        _context = context;
    }

    public Task<List<ExchangeRate>> GetRatesAsync(Guid tenantId) =>
        _context.ExchangeRates
            .Include(rate => rate.FromCurrency)
            .Include(rate => rate.ToCurrency)
            .Where(rate => rate.TenantId == tenantId)
            .OrderBy(rate => rate.FromCurrency.CurrencyCode)
            .ThenBy(rate => rate.ToCurrency.CurrencyCode)
            .ThenBy(rate => rate.RateType)
            .ThenByDescending(rate => rate.EffectiveDate)
            .ToListAsync();

    public Task<ExchangeRate?> GetByIdAsync(Guid tenantId, Guid exchangeRateId) =>
        _context.ExchangeRates
            .Include(rate => rate.FromCurrency)
            .Include(rate => rate.ToCurrency)
            .FirstOrDefaultAsync(rate =>
                rate.TenantId == tenantId &&
                rate.ExchangeRateId == exchangeRateId);

    public Task<bool> ExistsAsync(
        Guid tenantId,
        Guid fromCurrencyId,
        Guid toCurrencyId,
        ExchangeRateType rateType,
        DateTime effectiveDate,
        Guid? excludeId = null) =>
        _context.ExchangeRates.AnyAsync(rate =>
            rate.TenantId == tenantId &&
            rate.FromCurrencyId == fromCurrencyId &&
            rate.ToCurrencyId == toCurrencyId &&
            rate.RateType == rateType &&
            rate.EffectiveDate == effectiveDate &&
            (!excludeId.HasValue || rate.ExchangeRateId != excludeId.Value));

    public Task<ExchangeRate?> GetLatestDailyRateAsync(
        Guid tenantId,
        Guid fromCurrencyId,
        Guid toCurrencyId,
        DateTime asOfDate) =>
        GetValidPairQuery(tenantId, fromCurrencyId, toCurrencyId)
            .Where(rate => rate.RateType == ExchangeRateType.Daily &&
                           rate.EffectiveDate <= asOfDate.Date)
            .OrderByDescending(rate => rate.EffectiveDate)
            .FirstOrDefaultAsync();

    public Task<ExchangeRate?> GetMonthlyRateAsync(
        Guid tenantId,
        Guid fromCurrencyId,
        Guid toCurrencyId,
        DateTime effectiveMonth) =>
        GetValidPairQuery(tenantId, fromCurrencyId, toCurrencyId)
            .Where(rate => rate.RateType == ExchangeRateType.Monthly &&
                           rate.EffectiveDate == new DateTime(effectiveMonth.Year, effectiveMonth.Month, 1))
            .FirstOrDefaultAsync();

    public Task<ExchangeRate?> GetLatestMonthlyRateBeforeAsync(
        Guid tenantId,
        Guid fromCurrencyId,
        Guid toCurrencyId,
        DateTime effectiveMonth) =>
        GetValidPairQuery(tenantId, fromCurrencyId, toCurrencyId)
            .Where(rate => rate.RateType == ExchangeRateType.Monthly &&
                           rate.EffectiveDate < new DateTime(effectiveMonth.Year, effectiveMonth.Month, 1))
            .OrderByDescending(rate => rate.EffectiveDate)
            .FirstOrDefaultAsync();

    public Task AddAsync(ExchangeRate rate) => _context.ExchangeRates.AddAsync(rate).AsTask();

    public Task SaveChangesAsync() => _context.SaveChangesAsync();

    private IQueryable<ExchangeRate> GetValidPairQuery(
        Guid tenantId,
        Guid fromCurrencyId,
        Guid toCurrencyId) =>
        _context.ExchangeRates
            .Include(rate => rate.FromCurrency)
            .Include(rate => rate.ToCurrency)
            .Where(rate =>
                rate.TenantId == tenantId &&
                rate.FromCurrencyId == fromCurrencyId &&
                rate.ToCurrencyId == toCurrencyId &&
                rate.IsActive &&
                rate.FromCurrency.IsActive &&
                rate.ToCurrency.IsActive);
}
