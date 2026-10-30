using System;
using System.Threading.Tasks;
using API.Common;
using API.Security.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaaS.Application.Interfaces.Repositories;
using Security.Interfaces;

namespace API.Features.Saas;

public class UpdateTenantUomSettingsRequest
{
    public bool SpecialUomConversionEnabled { get; set; }
}

[ApiController]
[Route("api/tenants/current/settings/uom")]
public class TenantUomSettingsController : BaseApiController
{
    private readonly ITenantConfigurationRepository _configRepository;
    private readonly ICurrentUserContext _currentUserContext;

    public TenantUomSettingsController(
        ITenantConfigurationRepository configRepository,
        ICurrentUserContext currentUserContext)
    {
        _configRepository = configRepository;
        _currentUserContext = currentUserContext;
    }

    [HttpGet]
    [RequirePermission("UOM.VIEW")]
    public async Task<IActionResult> GetSettings()
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue) return UnauthorizedResponse("Tenant identity is missing or invalid.");

        var config = await _configRepository.GetByTenantIdAsync(tenantId.Value);
        return SuccessResponse(new
        {
            specialUomConversionEnabled = config?.SpecialUomConversionEnabled ?? false
        });
    }

    [HttpPut]
    [RequirePermission("UOM.CONVERT")]
    public async Task<IActionResult> UpdateSettings([FromBody] UpdateTenantUomSettingsRequest request)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue) return UnauthorizedResponse("Tenant identity is missing or invalid.");

        if (request == null) return BadRequestResponse("Request body cannot be null.");

        var config = await _configRepository.GetByTenantIdAsync(tenantId.Value);
        if (config == null)
        {
            config = global::SaaS.Core.Models.TenantConfiguration.CreateDefault(tenantId.Value);
            config.UpdateSpecialUomConversion(request.SpecialUomConversionEnabled);
            await _configRepository.AddAsync(config);
        }
        else
        {
            config.UpdateSpecialUomConversion(request.SpecialUomConversionEnabled);
        }

        await _configRepository.SaveChangesAsync();

        return SuccessResponse(new
        {
            specialUomConversionEnabled = config.SpecialUomConversionEnabled
        }, "UOM settings updated successfully.");
    }
}
