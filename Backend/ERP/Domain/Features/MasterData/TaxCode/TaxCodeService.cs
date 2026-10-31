using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Domain.Features.MasterData.TaxCode;

public class TaxCodeService : ITaxCodeService
{
    private readonly ITaxCodeRepository _repository;

    public TaxCodeService(ITaxCodeRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<TaxCodeDto>> GetTaxCodesAsync(
        Guid tenantId,
        bool includeInactive = false,
        TaxApplicability? applicability = null)
    {
        var list = await _repository.GetTaxCodesAsync(tenantId, includeInactive, applicability);
        return list.Select(MapToDto).ToList();
    }

    public async Task<TaxCodeDto?> GetTaxCodeAsync(Guid tenantId, Guid taxCodeId)
    {
        var taxCode = await _repository.GetTaxCodeByIdAsync(tenantId, taxCodeId);
        return taxCode == null ? null : MapToDto(taxCode);
    }

    public async Task<TaxCodeDto> CreateTaxCodeAsync(Guid tenantId, CreateTaxCodeRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        if (string.IsNullOrWhiteSpace(request.Code))
            throw new ArgumentException("Tax code is required.", nameof(request.Code));

        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("Tax name is required.", nameof(request.Name));

        if (request.Rate < 0)
            throw new ArgumentException("Tax rate cannot be negative.", nameof(request.Rate));

        if (await _repository.ExistsCodeAsync(tenantId, request.Code))
            throw new InvalidOperationException($"Tax code '{request.Code.Trim().ToUpperInvariant()}' already exists.");

        var taxCode = TaxCode.Create(
            tenantId,
            request.Code,
            request.Name,
            request.Rate,
            request.Applicability,
            request.EffectiveFrom,
            request.EffectiveTo,
            request.Description);

        await _repository.AddAsync(taxCode);
        await _repository.SaveChangesAsync();

        return MapToDto(taxCode);
    }

    public async Task<TaxCodeDto> UpdateTaxCodeAsync(Guid tenantId, Guid taxCodeId, UpdateTaxCodeRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        var taxCode = await _repository.GetTaxCodeByIdAsync(tenantId, taxCodeId);
        if (taxCode == null)
            throw new KeyNotFoundException($"Tax code with ID '{taxCodeId}' was not found.");

        taxCode.Update(
            request.Name,
            request.Rate,
            request.Applicability,
            request.EffectiveFrom,
            request.EffectiveTo,
            request.Description);

        await _repository.SaveChangesAsync();

        return MapToDto(taxCode);
    }

    public async Task DeactivateTaxCodeAsync(Guid tenantId, Guid taxCodeId)
    {
        var taxCode = await _repository.GetTaxCodeByIdAsync(tenantId, taxCodeId);
        if (taxCode == null)
            throw new KeyNotFoundException($"Tax code with ID '{taxCodeId}' was not found.");

        taxCode.Deactivate();
        await _repository.SaveChangesAsync();
    }

    public async Task<TaxCodeDto> ReactivateTaxCodeAsync(Guid tenantId, Guid taxCodeId)
    {
        var taxCode = await _repository.GetTaxCodeByIdAsync(tenantId, taxCodeId);
        if (taxCode == null)
            throw new KeyNotFoundException($"Tax code with ID '{taxCodeId}' was not found.");

        taxCode.Reactivate();
        await _repository.SaveChangesAsync();

        return MapToDto(taxCode);
    }

    private static TaxCodeDto MapToDto(TaxCode taxCode)
    {
        return new TaxCodeDto
        {
            TaxCodeId = taxCode.TaxCodeId,
            TenantId = taxCode.TenantId,
            Code = taxCode.Code,
            Name = taxCode.Name,
            Rate = taxCode.Rate,
            Applicability = taxCode.Applicability,
            EffectiveFrom = taxCode.EffectiveFrom,
            EffectiveTo = taxCode.EffectiveTo,
            Description = taxCode.Description,
            IsActive = taxCode.IsActive
        };
    }
}
