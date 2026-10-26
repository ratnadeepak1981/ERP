using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Domain.Features.MasterData.Currency;

public interface ICurrencyService
{
    Task<List<CurrencyDto>> GetCurrenciesAsync(Guid tenantId);
    Task<CurrencyDto?> GetCurrencyByIdAsync(Guid tenantId, Guid currencyId);
    Task<CurrencyDto?> GetCurrencyByCodeAsync(Guid tenantId, string currencyCode);
    Task<CurrencyDto> CreateCurrencyAsync(Guid tenantId, CreateCurrencyRequest request);
    Task<CurrencyDto> UpdateCurrencyAsync(Guid tenantId, Guid currencyId, UpdateCurrencyRequest request);
    Task DeactivateCurrencyAsync(Guid tenantId, Guid currencyId);
    Task ActivateCurrencyAsync(Guid tenantId, Guid currencyId);
}
