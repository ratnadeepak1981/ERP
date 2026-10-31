namespace Domain.Features.MasterData.Currency;

public class ExchangeRateService : IExchangeRateService
{
    private readonly IExchangeRateRepository _repository;
    private readonly ICurrencyRepository _currencyRepository;

    public ExchangeRateService(
        IExchangeRateRepository repository,
        ICurrencyRepository currencyRepository)
    {
        _repository = repository;
        _currencyRepository = currencyRepository;
    }

    public async Task<List<ExchangeRateDto>> GetRatesAsync(Guid tenantId)
    {
        var rates = await _repository.GetRatesAsync(tenantId);
        return rates.Select(MapToDto).ToList();
    }

    public async Task<ExchangeRateDto?> GetRateAsync(Guid tenantId, Guid exchangeRateId)
    {
        var rate = await _repository.GetByIdAsync(tenantId, exchangeRateId);
        return rate == null ? null : MapToDto(rate);
    }

    public async Task<ExchangeRateDto> CreateRateAsync(Guid tenantId, CreateExchangeRateRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        await ValidateCurrenciesAsync(tenantId, request.FromCurrencyId, request.ToCurrencyId);

        var rate = ExchangeRate.Create(
            tenantId,
            request.FromCurrencyId,
            request.ToCurrencyId,
            request.RateType,
            request.EffectiveDate,
            request.Rate);

        if (await _repository.ExistsAsync(
                tenantId,
                rate.FromCurrencyId,
                rate.ToCurrencyId,
                rate.RateType,
                rate.EffectiveDate))
            throw new InvalidOperationException("An exchange rate already exists for this currency pair, type, and effective date.");

        await _repository.AddAsync(rate);
        await _repository.SaveChangesAsync();
        return await GetRateAsync(tenantId, rate.ExchangeRateId)
            ?? throw new InvalidOperationException("The saved exchange rate could not be loaded.");
    }

    public async Task<ExchangeRateDto> UpdateRateAsync(
        Guid tenantId,
        Guid exchangeRateId,
        UpdateExchangeRateRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var rate = await _repository.GetByIdAsync(tenantId, exchangeRateId)
            ?? throw new KeyNotFoundException("Exchange rate not found.");

        await ValidateCurrenciesAsync(tenantId, request.FromCurrencyId, request.ToCurrencyId);
        rate.Update(
            request.FromCurrencyId,
            request.ToCurrencyId,
            request.RateType,
            request.EffectiveDate,
            request.Rate);

        if (await _repository.ExistsAsync(
                tenantId,
                rate.FromCurrencyId,
                rate.ToCurrencyId,
                rate.RateType,
                rate.EffectiveDate,
                rate.ExchangeRateId))
            throw new InvalidOperationException("An exchange rate already exists for this currency pair, type, and effective date.");

        await _repository.SaveChangesAsync();
        return await GetRateAsync(tenantId, exchangeRateId)
            ?? throw new InvalidOperationException("The saved exchange rate could not be loaded.");
    }

    public async Task DeactivateRateAsync(Guid tenantId, Guid exchangeRateId)
    {
        var rate = await _repository.GetByIdAsync(tenantId, exchangeRateId)
            ?? throw new KeyNotFoundException("Exchange rate not found.");
        rate.Deactivate();
        await _repository.SaveChangesAsync();
    }

    public async Task ActivateRateAsync(Guid tenantId, Guid exchangeRateId)
    {
        var rate = await _repository.GetByIdAsync(tenantId, exchangeRateId)
            ?? throw new KeyNotFoundException("Exchange rate not found.");
        await ValidateCurrenciesAsync(tenantId, rate.FromCurrencyId, rate.ToCurrencyId);
        rate.Activate();
        await _repository.SaveChangesAsync();
    }

    public async Task<ResolvedExchangeRate> ResolveAsync(
        Guid tenantId,
        Guid fromCurrencyId,
        Guid toCurrencyId,
        DateTime transactionDate)
    {
        var from = await GetActiveCurrencyAsync(tenantId, fromCurrencyId);
        var to = await GetActiveCurrencyAsync(tenantId, toCurrencyId);
        var date = transactionDate.Date;

        if (fromCurrencyId == toCurrencyId)
            return new ResolvedExchangeRate(1m, ExchangeRateSource.SameCurrency, null, date);

        var daily = await _repository.GetLatestDailyRateAsync(
            tenantId, fromCurrencyId, toCurrencyId, date);
        if (daily != null)
            return MapResolution(daily);

        var monthStart = new DateTime(date.Year, date.Month, 1);
        var applicableMonth = await _repository.GetMonthlyRateAsync(
            tenantId, fromCurrencyId, toCurrencyId, monthStart);
        if (applicableMonth != null)
            return MapResolution(applicableMonth);

        var previousMonth = await _repository.GetLatestMonthlyRateBeforeAsync(
            tenantId, fromCurrencyId, toCurrencyId, monthStart);
        if (previousMonth != null)
            return MapResolution(previousMonth);

        throw new InvalidOperationException(
            $"No valid exchange rate from {from.CurrencyCode} to {to.CurrencyCode} is available on {date:yyyy-MM-dd}.");
    }

    private async Task ValidateCurrenciesAsync(Guid tenantId, Guid fromCurrencyId, Guid toCurrencyId)
    {
        if (fromCurrencyId == toCurrencyId)
            throw new ArgumentException("Exchange rate currencies must be different.");
        await GetActiveCurrencyAsync(tenantId, fromCurrencyId);
        await GetActiveCurrencyAsync(tenantId, toCurrencyId);
    }

    private async Task<Currency> GetActiveCurrencyAsync(Guid tenantId, Guid currencyId)
    {
        var currency = await _currencyRepository.GetCurrencyByIdAsync(tenantId, currencyId);
        if (currency == null || currency.TenantId != tenantId)
            throw new InvalidOperationException("Currency does not exist or does not belong to this tenant.");
        if (!currency.IsActive)
            throw new InvalidOperationException($"Currency '{currency.CurrencyCode}' is inactive.");
        return currency;
    }

    private static ResolvedExchangeRate MapResolution(ExchangeRate rate) =>
        new(rate.Rate, (ExchangeRateSource)rate.RateType, rate.ExchangeRateId, rate.EffectiveDate);

    private static ExchangeRateDto MapToDto(ExchangeRate rate) => new()
    {
        ExchangeRateId = rate.ExchangeRateId,
        TenantId = rate.TenantId,
        FromCurrencyId = rate.FromCurrencyId,
        FromCurrencyCode = rate.FromCurrency.CurrencyCode,
        ToCurrencyId = rate.ToCurrencyId,
        ToCurrencyCode = rate.ToCurrency.CurrencyCode,
        RateType = rate.RateType,
        EffectiveDate = rate.EffectiveDate,
        Rate = rate.Rate,
        IsActive = rate.IsActive,
        CreatedAt = rate.CreatedAt,
        ModifiedAt = rate.ModifiedAt
    };
}
