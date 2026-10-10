using System;
using System.Threading.Tasks;
using Domain.Features.Procurement.Approval;
using SaaS.Application.Interfaces.Repositories;

namespace API.Services;

public class ProcurementApprovalSettingsProvider : IProcurementApprovalSettingsProvider
{
    private readonly ITenantConfigurationRepository _tenantConfigurationRepository;

    public ProcurementApprovalSettingsProvider(ITenantConfigurationRepository tenantConfigurationRepository)
    {
        _tenantConfigurationRepository = tenantConfigurationRepository;
    }

    public async Task<ProcurementApprovalSettings> GetSettingsAsync(Guid tenantId)
    {
        var config = await _tenantConfigurationRepository.GetByTenantIdAsync(tenantId);
        if (config == null)
        {
            // Default safe setting: Approval is required in Simple mode
            return new ProcurementApprovalSettings
            {
                ApprovalRequired = true,
                ApprovalMode = 1
            };
        }

        return new ProcurementApprovalSettings
        {
            ApprovalRequired = config.ProcurementApprovalRequired,
            ApprovalMode = config.ProcurementApprovalMode
        };
    }
}
