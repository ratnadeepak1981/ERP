using System;
using System.Threading.Tasks;
using API.Common;
using API.Security.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaaS.Application.DTOs;
using SaaS.Application.Interfaces;
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
                }
            }, "Tenant registration use case completed");
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException)
        {
            return ConflictResponse("A tenant with this organization name already exists. Please choose a different name.");
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
}