using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Domain.Features.MasterData.TaxCode;

public interface ITaxCodeRepository
{
    Task<List<TaxCode>> GetTaxCodesAsync(Guid tenantId, bool includeInactive = false, TaxApplicability? applicability = null);
    Task<TaxCode?> GetTaxCodeByIdAsync(Guid tenantId, Guid taxCodeId);
    Task<TaxCode?> GetTaxCodeByCodeAsync(Guid tenantId, string code);
    Task<bool> ExistsCodeAsync(Guid tenantId, string code, Guid? excludeId = null);
    Task AddAsync(TaxCode taxCode);
    Task SaveChangesAsync();
}
