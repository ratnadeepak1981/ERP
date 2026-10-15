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

    [HttpGet]
    [RequirePermission("TENANT.VIEW")]
    public async Task<IActionResult> GetAll()
    {
        if (!_currentUserContext.IsPlatformUser)
        {
            return Forbid();
        }

        try
        {
            var tenants = await _tenantService.GetTenantListAsync();
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