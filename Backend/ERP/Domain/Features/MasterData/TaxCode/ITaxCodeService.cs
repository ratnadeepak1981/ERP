using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Domain.Features.MasterData.TaxCode;

public interface ITaxCodeService
{
    Task<List<TaxCodeDto>> GetTaxCodesAsync(Guid tenantId, bool includeInactive = false, TaxApplicability? applicability = null);
    Task<TaxCodeDto?> GetTaxCodeAsync(Guid tenantId, Guid taxCodeId);
    Task<TaxCodeDto> CreateTaxCodeAsync(Guid tenantId, CreateTaxCodeRequest request);
    Task<TaxCodeDto> UpdateTaxCodeAsync(Guid tenantId, Guid taxCodeId, UpdateTaxCodeRequest request);
    Task DeactivateTaxCodeAsync(Guid tenantId, Guid taxCodeId);
    Task<TaxCodeDto> ReactivateTaxCodeAsync(Guid tenantId, Guid taxCodeId);
}
