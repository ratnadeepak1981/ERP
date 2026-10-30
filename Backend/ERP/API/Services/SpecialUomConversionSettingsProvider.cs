using System;
using System.Threading.Tasks;
using Domain.Features.MasterData.Company;
using Domain.Features.MasterData.UnitOfMeasure;
using SaaS.Application.Interfaces.Repositories;

namespace API.Services;

public class SpecialUomConversionSettingsProvider : ISpecialUomConversionSettingsProvider
{
    private readonly ITenantConfigurationRepository _tenantConfigurationRepository;
    private readonly ICompanyRepository _companyRepository;

    public SpecialUomConversionSettingsProvider(
        ITenantConfigurationRepository tenantConfigurationRepository,
        ICompanyRepository companyRepository)
    {
        _tenantConfigurationRepository = tenantConfigurationRepository;
        _companyRepository = companyRepository;
    }

    public async Task<bool> IsSpecialUomConversionEnabledAsync(Guid tenantId, Guid? companyId = null)
    {
        // 1. If caller supplied a company context, validate that company belongs to this tenant
        if (companyId.HasValue)
        {
            var company = await _companyRepository.GetByIdAsync(tenantId, companyId.Value);
            if (company == null || company.TenantId != tenantId)
            {
                throw new InvalidOperationException($"Company '{companyId.Value}' is invalid or does not belong to the current tenant.");
            }

            // SaaS Master Gate: If SaaS tenant setting is disabled, no company can bypass it
            var config = await _tenantConfigurationRepository.GetByTenantIdAsync(tenantId);
            bool tenantEnabled = config?.SpecialUomConversionEnabled ?? false;
            if (!tenantEnabled)
            {
                return false;
            }

            return company.IsActive && company.SpecialUomConversionEnabled;
        }

        // 2. Caller intentionally has no company context: Fallback to SaaS tenant-level setting
        var tenantConfig = await _tenantConfigurationRepository.GetByTenantIdAsync(tenantId);
        return tenantConfig?.SpecialUomConversionEnabled ?? false;
    }
}
