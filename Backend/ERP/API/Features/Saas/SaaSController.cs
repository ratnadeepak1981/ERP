using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaaS.Application.DTOs;
using SaaS.Application.Interfaces;
using SaaS.Services;
using System.Security.Claims;

namespace API.Features.SaaS;

[ApiController]
[Route("api/[controller]")]
public class SaaSController : ControllerBase
{
    private readonly SaaSService _saasService;
    private readonly ITenantService _tenantService;

    public SaaSController(
        SaaSService saasService,
        ITenantService tenantService)
    {
        _saasService = saasService;
        _tenantService = tenantService;
    }

    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        return Ok(_saasService.GetStatus());
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
            tenant = result.Tenant,
            subscription = result.Subscription
        });
    }
}