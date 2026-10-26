using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Domain.Features.MasterData.Currency;

public interface ICurrencyRepository
{
    Task<List<Currency>> GetCurrenciesAsync(Guid tenantId);
    Task<Currency?> GetCurrencyByIdAsync(Guid tenantId, Guid currencyId);
    Task<Currency?> GetCurrencyByCodeAsync(Guid tenantId, string currencyCode);
    Task<bool> ExistsByCodeAsync(Guid tenantId, string currencyCode, Guid? excludeId = null);
    Task AddCurrencyAsync(Currency currency);
    Task SaveChangesAsync();
}
