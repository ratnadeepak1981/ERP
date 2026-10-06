using SaaS.Application.DTOs;
using SaaS.Application.Interfaces;
using SaaS.Application.Interfaces.Repositories;
using SaaS.Core.Models;

namespace SaaS.Application.Services;

public class TenantProvisioningService
    : ITenantProvisioningService
{
    private readonly ITenantRepository _tenantRepository;
    private readonly ISubscriptionRepository _subscriptionRepository;
    private readonly ITenantDatabaseRepository _tenantDatabaseRepository;
    private readonly ITenantConfigurationRepository _tenantConfigurationRepository;

    public TenantProvisioningService(
        ITenantRepository tenantRepository,
        ISubscriptionRepository subscriptionRepository,
        ITenantDatabaseRepository tenantDatabaseRepository,
        ITenantConfigurationRepository tenantConfigurationRepository)
    {
        _tenantRepository = tenantRepository;
        _subscriptionRepository = subscriptionRepository;
        _tenantDatabaseRepository = tenantDatabaseRepository;
        _tenantConfigurationRepository = tenantConfigurationRepository;
    }

    public async Task<TenantProvisioningResult> ProvisionTenant(
        TenantProvisioningRequest request)
    {
        Guid tenantId = request.TenantId;

        Tenant? tenant =
            await _tenantRepository.GetByIdAsync(tenantId);

        if (tenant == null)
        {
            throw new InvalidOperationException(
                "Tenant does not exist.");
        }

        Subscription? subscription =
            await _subscriptionRepository
                .GetActiveByTenantIdAsync(tenantId);

        if (subscription == null)
        {
            throw new InvalidOperationException(
                "Active subscription does not exist for the tenant.");
        }

        if (await _tenantDatabaseRepository
            .ExistsByTenantIdAsync(tenantId))
        {
            throw new InvalidOperationException(
                "Tenant database configuration already exists.");
        }

        if (await _tenantConfigurationRepository
            .ExistsByTenantIdAsync(tenantId))
        {
            throw new InvalidOperationException(
                "Tenant configuration already exists.");
        }

        if (subscription.StorageMode != TenantStorageMode.Shared)
        {
            throw new InvalidOperationException(
                "Only shared storage provisioning is currently supported.");
        }

        TenantDatabase tenantDatabase = new TenantDatabase
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            DatabaseName = "ERP_DB",
            DatabaseServer = "SQLSERVER",
            IsActive = true
        };

        TenantConfiguration tenantConfiguration =
            TenantConfiguration.CreateDefault(tenantId);

        await _tenantDatabaseRepository.AddAsync(
            tenantDatabase);

        await _tenantConfigurationRepository.AddAsync(
            tenantConfiguration);

        await _tenantDatabaseRepository.SaveChangesAsync();

        return new TenantProvisioningResult
        {
            TenantId = tenantId,
            DatabaseProvisioned = true,
            ConfigurationProvisioned = true,
            IsProvisioned = true,
            StorageMode = subscription.StorageMode.ToString()
        };
    }
}