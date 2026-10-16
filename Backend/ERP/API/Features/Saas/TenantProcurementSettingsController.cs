using System;
using System.Threading.Tasks;
using API.Common;
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
public class TenantProcurementSettingsController : BaseApiController
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
            return UnauthorizedResponse("Tenant identity is missing or invalid.");
        }

        var config = await _configRepository.GetByTenantIdAsync(tenantId.Value);
        if (config == null)
        {
            return SuccessResponse(new
            {
                approvalRequired = true,
                approvalMode = 1
            });
        }

        return SuccessResponse(new
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
            return UnauthorizedResponse("Tenant identity is missing or invalid.");
        }

        if (request == null)
        {
            return BadRequestResponse("Request body is required.");
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

            return SuccessResponse(new
            {
                approvalRequired = config.ProcurementApprovalRequired,
                approvalMode = config.ProcurementApprovalMode
            }, "Procurement approval settings updated successfully.");
        }
        catch (ArgumentException ex)
        {
            return BadRequestResponse(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequestResponse(ex.Message);
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }
}
