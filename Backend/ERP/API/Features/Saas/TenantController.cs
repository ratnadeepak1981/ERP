using Microsoft.AspNetCore.Mvc;
using SaaS.Application.DTOs;
using SaaS.Application.Interfaces;

namespace SaaS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TenantController : ControllerBase
{
    private readonly ITenantService _tenantService;
    private readonly ITenantProvisioningService _tenantProvisioningService;

    public TenantController(
        ITenantService tenantService,
        ITenantProvisioningService tenantProvisioningService)
    {
        _tenantService = tenantService;
        _tenantProvisioningService = tenantProvisioningService;
    }

    [HttpPost("tenant-registration-test")]
    public async Task<IActionResult> TenantRegistrationTest(
        [FromBody] CreateTenantRequest request)
    {
        TenantRegistrationResult result =
            await _tenantService.RegisterTenant(request);

        return Ok(new
        {
            status = "PASS",
            message = "Tenant registration use case completed",

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
        });
    }

    [HttpPost("provision")]
    public async Task<IActionResult> Provision(
    [FromBody] TenantProvisioningRequest request)
    {
        try
        {
            TenantProvisioningResult result =
                await _tenantProvisioningService.ProvisionTenant(request);

            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new
            {
                status = "FAIL",
                message = ex.Message
            });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new
            {
                status = "FAIL",
                message = ex.Message
            });
        }
    }
}