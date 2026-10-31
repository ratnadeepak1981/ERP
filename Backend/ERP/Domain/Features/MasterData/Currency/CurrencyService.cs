using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Domain.Features.MasterData.Currency;

public class CurrencyService : ICurrencyService
{
    private readonly ICurrencyRepository _repository;

    public CurrencyService(ICurrencyRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<CurrencyDto>> GetCurrenciesAsync(Guid tenantId)
    {
        var currencies = await _repository.GetCurrenciesAsync(tenantId);
        return currencies.Select(MapToDto).ToList();
    }

    public async Task<CurrencyDto?> GetCurrencyByIdAsync(Guid tenantId, Guid currencyId)
    {
        var currency = await _repository.GetCurrencyByIdAsync(tenantId, currencyId);
        return currency == null ? null : MapToDto(currency);
    }

    public async Task<CurrencyDto?> GetCurrencyByCodeAsync(Guid tenantId, string currencyCode)
    {
        var currency = await _repository.GetCurrencyByCodeAsync(tenantId, currencyCode);
        return currency == null ? null : MapToDto(currency);
    }

    public async Task<CurrencyDto> CreateCurrencyAsync(Guid tenantId, CreateCurrencyRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        if (string.IsNullOrWhiteSpace(request.CurrencyCode))
            throw new ArgumentException("Currency code is required.", nameof(request.CurrencyCode));

        var normalizedCode = request.CurrencyCode.Trim().ToUpperInvariant();
        if (await _repository.ExistsByCodeAsync(tenantId, normalizedCode))
            throw new InvalidOperationException($"Currency code '{normalizedCode}' already exists for this tenant.");

        var currency = Currency.Create(
            tenantId,
            normalizedCode,
            request.CurrencyName,
            request.Symbol,
            request.DecimalPlaces);

        await _repository.AddCurrencyAsync(currency);
        await _repository.SaveChangesAsync();

        return MapToDto(currency);
    }

    public async Task<CurrencyDto> UpdateCurrencyAsync(Guid tenantId, Guid currencyId, UpdateCurrencyRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        var currency = await _repository.GetCurrencyByIdAsync(tenantId, currencyId)
            ?? throw new KeyNotFoundException($"Currency with ID '{currencyId}' was not found.");

        currency.Update(request.CurrencyName, request.Symbol, request.DecimalPlaces);
        await _repository.SaveChangesAsync();

        return MapToDto(currency);
    }

    public async Task DeactivateCurrencyAsync(Guid tenantId, Guid currencyId)
    {
        var currency = await _repository.GetCurrencyByIdAsync(tenantId, currencyId)
            ?? throw new KeyNotFoundException($"Currency with ID '{currencyId}' was not found.");

        currency.Deactivate();
        await _repository.SaveChangesAsync();
    }

    public async Task ActivateCurrencyAsync(Guid tenantId, Guid currencyId)
    {
        var currency = await _repository.GetCurrencyByIdAsync(tenantId, currencyId)
            ?? throw new KeyNotFoundException($"Currency with ID '{currencyId}' was not found.");

        currency.Activate();
        await _repository.SaveChangesAsync();
    }

    private static CurrencyDto MapToDto(Currency currency) => new()
    {
        CurrencyId = currency.CurrencyId,
        TenantId = currency.TenantId,
        CurrencyCode = currency.CurrencyCode,
        CurrencyName = currency.CurrencyName,
        Symbol = currency.Symbol,
        DecimalPlaces = currency.DecimalPlaces,
        IsActive = currency.IsActive,
        CreatedAt = currency.CreatedAt,
        ModifiedAt = currency.ModifiedAt
    };
}
