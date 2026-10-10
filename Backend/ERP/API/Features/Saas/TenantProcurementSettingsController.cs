using System;
using System.Threading.Tasks;
using API.Security.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaaS.Application.Interfaces.Repositories;
using Security.Interfaces;

namespace API.Features.Saas;

public class UpdateProcurementApprovalSettingsRequest
{
    public bool ApprovalRequired { get; set; }

    public int ApprovalMode { get; set; } // 1 = Simple, 2 = Advanced
}

[ApiController]
[Route("api/tenants/current/settings/procurement-approval")]
[RequirePermission("PROCUREMENT.SETTINGS.VIEW")]
public class TenantProcurementSettingsController : ControllerBase
{
    private readonly ITenantConfigurationRepository _configRepository;
    private readonly ICurrentUserContext _currentUserContext;

    public TenantProcurementSettingsController(
        ITenantConfigurationRepository configRepository,
        ICurrentUserContext currentUserContext)
    {
        _configRepository = configRepository;
        _currentUserContext = currentUserContext;
    }

    [HttpGet]
    public async Task<IActionResult> GetSettings()
    {
        if (_currentUserContext.IsPlatformUser)
        {
            return Forbid();
        }

        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
        {
            return Unauthorized(new { status = "FAIL", message = "Tenant identity is missing or invalid." });
        }

        var config = await _configRepository.GetByTenantIdAsync(tenantId.Value);
        if (config == null)
        {
            return Ok(new
            {
                approvalRequired = true,
                approvalMode = 1
            });
        }

        return Ok(new
        {
            approvalRequired = config.ProcurementApprovalRequired,
            approvalMode = config.ProcurementApprovalMode
        });
    }

    [HttpPut]
    [RequirePermission("PROCUREMENT.SETTINGS.EDIT")]
    public async Task<IActionResult> UpdateSettings([FromBody] UpdateProcurementApprovalSettingsRequest request)
    {
        if (_currentUserContext.IsPlatformUser)
        {
            return Forbid();
        }

        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
        {
            return Unauthorized(new { status = "FAIL", message = "Tenant identity is missing or invalid." });
        }

        if (request == null)
        {
            return BadRequest(new { status = "FAIL", message = "Request body is required." });
        }

        var config = await _configRepository.GetByTenantIdAsync(tenantId.Value);
        if (config == null)
        {
            config = global::SaaS.Core.Models.TenantConfiguration.CreateDefault(tenantId.Value);
            await _configRepository.AddAsync(config);
        }

        try
        {
            config.UpdateProcurementApproval(request.ApprovalRequired, request.ApprovalMode);
            await _configRepository.SaveChangesAsync();

            return Ok(new
            {
                status = "PASS",
                message = "Procurement approval settings updated successfully.",
                settings = new
                {
                    approvalRequired = config.ProcurementApprovalRequired,
                    approvalMode = config.ProcurementApprovalMode
                }
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { status = "FAIL", message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { status = "FAIL", message = ex.Message });
        }
    }
}
