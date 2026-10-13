using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using API.Security.Authorization;
using ERP.Domain.Features.CRM;
using Microsoft.AspNetCore.Mvc;
using Security.Interfaces;

namespace API.Features.Domain.CRM;

[ApiController]
[Route("api/companies/{companyId:guid}/crm/activities")]
[RequirePermission("CRM.ACTIVITY.VIEW")]
public class CrmActivityController : ControllerBase
{
    private readonly ICrmActivityService _activityService;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IUserAccessService _userAccessService;
    private readonly global::Domain.Features.MasterData.Company.ICompanyRepository _companyRepository;

    public CrmActivityController(
        ICrmActivityService activityService,
        ICurrentUserContext currentUserContext,
        IUserAccessService userAccessService,
        global::Domain.Features.MasterData.Company.ICompanyRepository companyRepository)
    {
        _activityService = activityService;
        _currentUserContext = currentUserContext;
        _userAccessService = userAccessService;
        _companyRepository = companyRepository;
    }

    [HttpGet]
    public async Task<IActionResult> GetActivities(
        Guid companyId,
        [FromQuery] Guid? branchId = null,
        [FromQuery] Guid? leadId = null,
        [FromQuery] Guid? opportunityId = null,
        [FromQuery] Guid? customerId = null,
        [FromQuery] bool? isCompleted = null,
        CancellationToken cancellationToken = default)
    {
        var authResult = await ValidateScopeAccess(companyId, branchId);
        if (authResult.Action != null) return authResult.Action;

        var activities = await _activityService.GetActivitiesAsync(
            authResult.TenantId,
            companyId,
            branchId,
            leadId,
            opportunityId,
            customerId,
            isCompleted,
            cancellationToken);

        return Ok(activities);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetActivityById(
        Guid companyId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var authResult = await ValidateScopeAccess(companyId);
        if (authResult.Action != null) return authResult.Action;

        var activity = await _activityService.GetActivityByIdAsync(
            authResult.TenantId,
            companyId,
            id,
            cancellationToken);

        if (activity == null)
        {
            return NotFound(new { status = "FAIL", message = "CRM activity not found in the authorized scope." });
        }

        if (activity.BranchId.HasValue)
        {
            var branchAuth = await ValidateScopeAccess(companyId, activity.BranchId);
            if (branchAuth.Action != null) return branchAuth.Action;
        }

        return Ok(activity);
    }

    [HttpPost]
    [RequirePermission("CRM.ACTIVITY.MANAGE")]
    public async Task<IActionResult> CreateActivity(
        Guid companyId,
        [FromBody] CreateCrmActivityRequest request,
        CancellationToken cancellationToken = default)
    {
        var authResult = await ValidateScopeAccess(companyId, request.BranchId);
        if (authResult.Action != null) return authResult.Action;

        try
        {
            var activity = await _activityService.CreateActivityAsync(
                authResult.TenantId,
                companyId,
                request,
                cancellationToken);

            return CreatedAtAction(nameof(GetActivityById), new { companyId, id = activity.CrmActivityId }, activity);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { status = "FAIL", message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { status = "FAIL", message = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    [RequirePermission("CRM.ACTIVITY.MANAGE")]
    public async Task<IActionResult> UpdateActivity(
        Guid companyId,
        Guid id,
        [FromBody] UpdateCrmActivityRequest request,
        CancellationToken cancellationToken = default)
    {
        var authResult = await ValidateScopeAccess(companyId);
        if (authResult.Action != null) return authResult.Action;

        try
        {
            var activity = await _activityService.UpdateActivityAsync(
                authResult.TenantId,
                companyId,
                id,
                request,
                cancellationToken);

            return Ok(activity);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { status = "FAIL", message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { status = "FAIL", message = ex.Message });
        }
    }

    [HttpPost("{id:guid}/complete")]
    [RequirePermission("CRM.ACTIVITY.MANAGE")]
    public async Task<IActionResult> CompleteActivity(
        Guid companyId,
        Guid id,
        [FromBody] CompleteCrmActivityRequest request,
        CancellationToken cancellationToken = default)
    {
        var authResult = await ValidateScopeAccess(companyId);
        if (authResult.Action != null) return authResult.Action;

        try
        {
            var activity = await _activityService.CompleteActivityAsync(
                authResult.TenantId,
                companyId,
                id,
                request,
                cancellationToken);

            return Ok(activity);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { status = "FAIL", message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { status = "FAIL", message = ex.Message });
        }
    }

    private async Task<(IActionResult? Action, Guid TenantId)> ValidateScopeAccess(Guid companyId, Guid? branchId = null)
    {
        if (_currentUserContext.IsPlatformUser)
        {
            return (Forbid(), Guid.Empty);
        }

        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
        {
            return (Unauthorized(new
            {
                status = "FAIL",
                message = "Tenant identity is missing or invalid."
            }), Guid.Empty);
        }

        var company = await _companyRepository.GetByIdAsync(tenantId.Value, companyId);
        if (company == null)
        {
            return (Forbid(), Guid.Empty);
        }

        var canAccessCompany = await _userAccessService.CanAccessCompanyAsync(
            _currentUserContext.UserId,
            tenantId.Value,
            companyId);

        if (!canAccessCompany)
        {
            return (Forbid(), Guid.Empty);
        }

        if (branchId.HasValue)
        {
            var canAccessBranch = await _userAccessService.CanAccessBranchAsync(
                _currentUserContext.UserId,
                tenantId.Value,
                companyId,
                branchId.Value);

            if (!canAccessBranch)
            {
                return (Forbid(), Guid.Empty);
            }
        }

        return (null, tenantId.Value);
    }
}
