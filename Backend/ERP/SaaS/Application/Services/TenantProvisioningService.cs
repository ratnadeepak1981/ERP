using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using SaaS.Application.DTOs;
using SaaS.Application.Interfaces;
using SaaS.Application.Interfaces.Repositories;
using SaaS.Core.Models;
using SaaS.Core.Rules;

namespace SaaS.Application.Services;

public class TenantProvisioningService
    : ITenantProvisioningService
{
    private readonly ITenantRepository _tenantRepository;
    private readonly ISubscriptionRepository _subscriptionRepository;
    private readonly ITenantDatabaseRepository _tenantDatabaseRepository;
    private readonly ITenantConfigurationRepository _tenantConfigurationRepository;
    private readonly ITenantAdminProvisioningService _tenantAdminProvisioningService;
    private readonly ITenantDomainRepository _tenantDomainRepository;

    public TenantProvisioningService(
        ITenantRepository tenantRepository,
        ISubscriptionRepository subscriptionRepository,
        ITenantDatabaseRepository tenantDatabaseRepository,
        ITenantConfigurationRepository tenantConfigurationRepository,
        ITenantAdminProvisioningService tenantAdminProvisioningService,
        ITenantDomainRepository tenantDomainRepository)
    {
        _tenantRepository = tenantRepository;
        _subscriptionRepository = subscriptionRepository;
        _tenantDatabaseRepository = tenantDatabaseRepository;
        _tenantConfigurationRepository = tenantConfigurationRepository;
        _tenantAdminProvisioningService = tenantAdminProvisioningService;
        _tenantDomainRepository = tenantDomainRepository;
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

        // Verify or provision primary domain
        var existingDomains = await _tenantDomainRepository.GetByTenantIdAsync(tenantId);
        TenantDomain? primaryDomain = existingDomains.FirstOrDefault(x => x.IsPrimary && x.IsActive)
            ?? existingDomains.FirstOrDefault(x => x.IsActive);
        TenantDomain? newlyProvisionedDomain = null;

        if (primaryDomain == null)
        {
            string candidateSubdomain = Regex.Replace(tenant.Code.ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');
            if (candidateSubdomain.Length < 3 || TenantDomainRules.IsReservedSubdomain(candidateSubdomain))
            {
                candidateSubdomain = $"tenant-{tenantId.ToString("N")[..6]}";
            }

            string chosenSubdomain = candidateSubdomain;
            int counter = 1;
            while (await _tenantDomainRepository.ExistsSubdomainAsync(chosenSubdomain) ||
                   TenantDomainRules.IsReservedSubdomain(chosenSubdomain))
            {
                chosenSubdomain = $"{candidateSubdomain}-{counter++}";
            }

            newlyProvisionedDomain = new TenantDomain
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Hostname = TenantDomainRules.BuildLocalhostHostname(chosenSubdomain),
                Subdomain = chosenSubdomain,
                DomainType = TenantDomainType.Subdomain,
                IsPrimary = true,
                IsActive = true,
                IsVerified = true
            };

            await _tenantDomainRepository.AddAsync(newlyProvisionedDomain);
            primaryDomain = newlyProvisionedDomain;
        }

        TenantDatabase tenantDatabase = new TenantDatabase
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            DatabaseName = "DomainDB",
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

        try
        {
            // Ensure the Tenant Admin system role exists.
            await _tenantAdminProvisioningService
                .EnsureTenantAdminRoleAsync();

            // Create the Tenant Admin user for this tenant.
            await _tenantAdminProvisioningService
                .EnsureTenantAdminAsync(
                    tenantId,
                    request.AdminUserName,
                    request.AdminEmail,
                    request.AdminPassword);
        }
        catch
        {
            // Compensation: If Security bootstrap fails, roll back PlatformDB records
            // so the tenant is not left in an unrecoverable corrupted state.
            try
            {
                await _tenantDatabaseRepository.DeleteAsync(tenantDatabase);
                await _tenantConfigurationRepository.DeleteAsync(tenantConfiguration);
                if (newlyProvisionedDomain != null)
                {
                    await _tenantDomainRepository.DeleteAsync(newlyProvisionedDomain);
                }
                await _tenantDatabaseRepository.SaveChangesAsync();
            }
            catch
            {
                // Preserve original exception
            }

            throw;
        }

        return new TenantProvisioningResult
        {
            TenantId = tenantId,
            DatabaseProvisioned = true,
            ConfigurationProvisioned = true,
            IsProvisioned = true,
            StorageMode = subscription.StorageMode.ToString(),
            Hostname = primaryDomain?.Hostname,
            Subdomain = primaryDomain?.Subdomain,
            DomainProvisioned = primaryDomain != null
        };
    }
}
