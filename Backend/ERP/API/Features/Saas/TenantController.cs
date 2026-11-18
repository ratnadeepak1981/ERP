using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Common;
using API.Common.TenantResolution;
using API.Security.Authorization;
using Domain.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SaaS.Application.DTOs;
using SaaS.Application.Interfaces;
using SaaS.Infrastructure.Persistence;
using Security.Infrastructure.Persistence;
using Security.Interfaces;

namespace SaaS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TenantController : BaseApiController
{
    private readonly ITenantService _tenantService;
    private readonly ITenantProvisioningService _tenantProvisioningService;
    private readonly ICurrentUserContext _currentUserContext;

    public TenantController(
        ITenantService tenantService,
        ITenantProvisioningService tenantProvisioningService,
        ICurrentUserContext currentUserContext)
    {
        _tenantService = tenantService;
        _tenantProvisioningService = tenantProvisioningService;
        _currentUserContext = currentUserContext;
    }

    [HttpGet("context")]
    [AllowAnonymous]
    public IActionResult GetCurrentTenantContext(
        [FromServices] ITenantContext tenantContext)
    {
        return SuccessResponse(new
        {
            isTenant = tenantContext.IsTenantDomainRequest,
            tenantId = tenantContext.TenantId,
            name = tenantContext.TenantName,
            subdomain = tenantContext.Subdomain,
            industryType = tenantContext.IndustryType,
            companyStructure = tenantContext.CompanyStructure,
            eCommerceEnabled = tenantContext.ECommerceEnabled
        });
    }

    [HttpPost("tenant-registration-test")]
    [AllowAnonymous]
    public async Task<IActionResult> TenantRegistrationTest(
        [FromBody] CreateTenantRequest request)
    {
        try
        {
            TenantRegistrationResult result =
                await _tenantService.RegisterTenant(request);

            return SuccessResponse(new
            {
                tenant = new
                {
                    id = result.Tenant.Id,
                    name = result.Tenant.Name,
                    code = result.Tenant.Code,
                    isActive = result.Tenant.IsActive
                },
                subscription = new
                {
                    id = result.Subscription.Id,
                    tenantId = result.Subscription.TenantId,
                    subscriptionPlanId = result.Subscription.SubscriptionPlanId,
                    subscriptionName = result.Subscription.SubscriptionName,
                    subscriptionType = result.Subscription.SubscriptionType,
                    storageMode = result.Subscription.StorageMode,
                    startDate = result.Subscription.StartDate,
                    endDate = result.Subscription.EndDate,
                    isActive = result.Subscription.IsActive
                },
                domain = result.PrimaryDomain != null ? new
                {
                    id = result.PrimaryDomain.Id,
                    hostname = result.PrimaryDomain.Hostname,
                    subdomain = result.PrimaryDomain.Subdomain,
                    domainType = result.PrimaryDomain.DomainType.ToString(),
                    isPrimary = result.PrimaryDomain.IsPrimary,
                    isVerified = result.PrimaryDomain.IsVerified
                } : null,
                hostname = result.Hostname,
                subdomain = result.Subdomain
            }, "Tenant registration use case completed");
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException ex)
        {
            var innerMsg = ex.InnerException?.Message ?? ex.Message;
            if (innerMsg.Contains("IX_Tenants_Name"))
            {
                return ConflictResponse($"An organization with the name '{request.Name}' already exists. Please choose a different name.");
            }
            if (innerMsg.Contains("IX_Tenants_Subdomain") || innerMsg.Contains("IX_TenantDomains_Subdomain") || innerMsg.Contains("IX_TenantDomains_Hostname"))
            {
                return ConflictResponse($"The subdomain '{request.Subdomain}' is already registered. Please choose another subdomain.");
            }
            if (innerMsg.Contains("IX_Tenants_Code"))
            {
                return ConflictResponse("A tenant code collision occurred. Please retry.");
            }
            if (innerMsg.Contains("duplicate key") || innerMsg.Contains("UNIQUE KEY"))
            {
                return ConflictResponse($"A record collision was detected: {innerMsg}");
            }

            return BadRequestResponse($"Database error: {innerMsg}");
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpPost("provision")]
    [RequirePermission("TENANT.CREATE")]
    public async Task<IActionResult> Provision(
        [FromBody] TenantProvisioningRequest request)
    {
        if (!_currentUserContext.IsPlatformUser)
        {
            return Forbid();
        }

        try
        {
            TenantProvisioningResult result =
                await _tenantProvisioningService.ProvisionTenant(request);

            return SuccessResponse(result, "Tenant provisioned successfully.");
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpGet]
    [RequirePermission("TENANT.VIEW")]
    public async Task<IActionResult> GetAll(
        [FromServices] SaaSDbContext saasDb,
        [FromServices] SecurityDbContext securityDb,
        [FromServices] DomainDbContext domainDb)
    {
        if (!_currentUserContext.IsPlatformUser)
        {
            return Forbid();
        }

        try
        {
            var tenants = await _tenantService.GetTenantListAsync();
            var tenantIds = tenants.Select(t => t.Id).ToList();

            // 1. Fetch SaaS details from SaaSDbContext
            var billingProfiles = await saasDb.TenantBillingProfiles
                .AsNoTracking()
                .Where(b => tenantIds.Contains(b.TenantId))
                .ToDictionaryAsync(b => b.TenantId);

            var configurations = await saasDb.TenantConfigurations
                .AsNoTracking()
                .Where(c => tenantIds.Contains(c.TenantId))
                .ToDictionaryAsync(c => c.TenantId);

            var subscriptions = await saasDb.Subscriptions
                .AsNoTracking()
                .Include(s => s.SubscriptionPlan)
                .Where(s => tenantIds.Contains(s.TenantId))
                .ToDictionaryAsync(s => s.TenantId);

            var tenantDatabases = await saasDb.TenantDatabases
                .AsNoTracking()
                .Where(d => tenantIds.Contains(d.TenantId))
                .ToDictionaryAsync(d => d.TenantId);

            var tenantMetadata = await saasDb.Tenants
                .AsNoTracking()
                .Where(t => tenantIds.Contains(t.Id))
                .ToDictionaryAsync(t => t.Id);

            // 2. Fetch User & Admin metrics from SecurityDbContext
            var users = await securityDb.Users
                .AsNoTracking()
                .Where(u => u.TenantId.HasValue && tenantIds.Contains(u.TenantId.Value))
                .Select(u => new
                {
                    u.Id,
                    TenantId = u.TenantId!.Value,
                    u.Username,
                    u.Email,
                    u.IsActive,
                    IsAdmin = u.UserRoles.Any(ur => ur.Role.Name == "Tenant Admin" || ur.Role.TemplateKey == "TENANT_ADMIN")
                })
                .ToListAsync();

            var usersByTenant = users.GroupBy(u => u.TenantId).ToDictionary(g => g.Key, g => g.ToList());

            // 3. Fetch Company & Branch metrics from DomainDbContext
            var companies = await domainDb.Companies
                .AsNoTracking()
                .Where(c => tenantIds.Contains(c.TenantId))
                .Select(c => new
                {
                    c.Id,
                    c.TenantId,
                    c.Name,
                    c.IsActive
                })
                .ToListAsync();

            var companyIds = companies.Select(c => c.Id).ToList();
            var branches = await domainDb.Branches
                .AsNoTracking()
                .Where(b => companyIds.Contains(b.CompanyId))
                .Select(b => new { b.Id, b.CompanyId })
                .ToListAsync();

            var companiesByTenant = companies.GroupBy(c => c.TenantId).ToDictionary(g => g.Key, g => g.ToList());
            var branchCountByCompany = branches.GroupBy(b => b.CompanyId).ToDictionary(g => g.Key, g => g.Count());

            // 4. Enrich each TenantQueueItemDto
            foreach (var item in tenants)
            {
                if (tenantMetadata.TryGetValue(item.Id, out var tm))
                {
                    item.ModifiedAt = tm.ModifiedAt;
                }

                if (billingProfiles.TryGetValue(item.Id, out var bp))
                {
                    item.LegalName = bp.CompanyName;
                    item.TaxId = bp.TaxId;
                    item.BillingEmail = bp.BillingEmail;
                    item.City = bp.City;
                    item.CountryCode = bp.CountryCode;
                    item.Currency = bp.Currency;
                }

                if (configurations.TryGetValue(item.Id, out var cfg))
                {
                    item.ConfigurationExists = true;
                    item.CompanyEnabled = cfg.CompanyEnabled;
                    item.BranchEnabled = cfg.BranchEnabled;
                    item.WarehouseZoneEnabled = cfg.WarehouseZoneEnabled;
                    item.WarehouseBinEnabled = cfg.WarehouseBinEnabled;
                    item.ProcurementApprovalRequired = cfg.ProcurementApprovalRequired;
                    item.ProcurementApprovalMode = cfg.ProcurementApprovalMode;
                    item.SpecialUomConversionEnabled = cfg.SpecialUomConversionEnabled;
                    item.IndustryType = cfg.IndustryType;
                    item.CompanyStructure = cfg.CompanyStructure;
                    item.ECommerceEnabled = cfg.ECommerceEnabled;
                }

                if (subscriptions.TryGetValue(item.Id, out var sub))
                {
                    item.SubscriptionId = sub.Id;
                    item.SubscriptionPlanId = sub.SubscriptionPlanId;
                    item.SubscriptionName = sub.SubscriptionName;
                    item.SubscriptionStatus = sub.Status.ToString();
                    item.SubscriptionStartDate = sub.StartDate;
                    item.SubscriptionEndDate = sub.EndDate;
                    item.NextBillingDate = sub.NextBillingDate;
                    item.BillingCycle = sub.BillingCycle.ToString();
                    item.BillingPlanPriceSnapshot = sub.BillingPlanPriceSnapshot;
                    item.AutoRenew = sub.AutoRenew;
                    if (string.IsNullOrEmpty(item.Currency))
                    {
                        item.Currency = "USD";
                    }
                    if (sub.SubscriptionPlan != null)
                    {
                        item.PlanName = sub.SubscriptionPlan.Name;
                        item.PlanCode = sub.SubscriptionPlan.Code;
                        item.PlanIsActive = sub.SubscriptionPlan.IsActive;
                    }
                }

                if (tenantDatabases.TryGetValue(item.Id, out var tdb))
                {
                    item.DatabaseName = tdb.DatabaseName;
                    item.DatabaseServer = tdb.DatabaseServer;
                    item.DatabaseIsActive = tdb.IsActive;
                    item.ProvisioningDate = tdb.CreatedAt;
                }

                if (usersByTenant.TryGetValue(item.Id, out var tenantUserList))
                {
                    item.TotalUserCount = tenantUserList.Count;
                    item.ActiveUserCount = tenantUserList.Count(u => u.IsActive);
                    item.InactiveUserCount = tenantUserList.Count(u => !u.IsActive);

                    var adminUsers = tenantUserList.Where(u => u.IsAdmin).ToList();
                    item.TenantAdminCount = adminUsers.Count;
                    item.TenantAdminUsernames = adminUsers.Select(u => u.Username).ToList();
                    item.TenantAdminEmails = adminUsers.Select(u => u.Email).ToList();
                    item.TenantAdminAccountStatus = adminUsers.Count > 0 && adminUsers.All(u => u.IsActive) ? "Active" : (adminUsers.Count > 0 ? "Suspended" : "Unassigned");
                    if (string.IsNullOrWhiteSpace(item.AdminEmail) && adminUsers.Count > 0)
                    {
                        item.AdminEmail = adminUsers[0].Email;
                        item.AdminUsername = adminUsers[0].Username;
                    }
                }

                if (companiesByTenant.TryGetValue(item.Id, out var tenantCompanies))
                {
                    item.CompanyCount = tenantCompanies.Count;
                    var primaryComp = tenantCompanies.FirstOrDefault(c => c.IsActive) ?? tenantCompanies.FirstOrDefault();
                    if (primaryComp != null)
                    {
                        item.PrimaryCompanyName = primaryComp.Name;
                        item.PrimaryCompanyStatus = primaryComp.IsActive ? "Active" : "Inactive";
                    }
                    item.BranchCount = tenantCompanies.Sum(c => branchCountByCompany.TryGetValue(c.Id, out var bCount) ? bCount : 0);
                }
            }

            return SuccessResponse(tenants, "Tenants retrieved successfully.");
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpGet("metrics")]
    [RequirePermission("TENANT.VIEW")]
    public async Task<IActionResult> GetMetrics()
    {
        if (!_currentUserContext.IsPlatformUser)
        {
            return Forbid();
        }

        try
        {
            var metrics = await _tenantService.GetTenantMetricsAsync();
            return SuccessResponse(metrics, "Tenant metrics retrieved successfully.");
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpGet("{tenantId:guid}")]
    [RequirePermission("TENANT.VIEW")]
    public async Task<IActionResult> GetById([FromRoute] Guid tenantId)
    {
        if (!_currentUserContext.IsPlatformUser)
        {
            return Forbid();
        }

        if (tenantId == Guid.Empty)
        {
            return BadRequestResponse("Valid Tenant Identifier is required.");
        }

        try
        {
            var tenant = await _tenantService.GetTenantDetailsAsync(tenantId);
            if (tenant == null)
            {
                return NotFoundResponse($"Tenant with ID '{tenantId}' was not found.");
            }

            return SuccessResponse(tenant, "Tenant details retrieved successfully.");
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpGet("{tenantId:guid}/status")]
    [HttpGet("{tenantId:guid}/provisioning-status")]
    [RequirePermission("TENANT.VIEW")]
    public async Task<IActionResult> GetStatus([FromRoute] Guid tenantId)
    {
        if (!_currentUserContext.IsPlatformUser)
        {
            return Forbid();
        }

        if (tenantId == Guid.Empty)
        {
            return BadRequestResponse("Valid Tenant Identifier is required.");
        }

        try
        {
            var status = await _tenantService.GetTenantStatusAsync(tenantId);
            if (status == null)
            {
                return NotFoundResponse($"Tenant with ID '{tenantId}' was not found.");
            }

            return SuccessResponse(status, "Tenant lifecycle status retrieved successfully.");
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }
}
