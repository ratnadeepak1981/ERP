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
[Route("api/companies/{companyId:guid}/crm/leads")]
[RequirePermission("CRM.LEAD.VIEW")]
public class LeadController : ControllerBase
{
    private readonly ILeadService _leadService;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IUserAccessService _userAccessService;
    private readonly global::Domain.Features.MasterData.Company.ICompanyRepository _companyRepository;

    public LeadController(
        ILeadService leadService,
        ICurrentUserContext currentUserContext,
        IUserAccessService userAccessService,
        global::Domain.Features.MasterData.Company.ICompanyRepository companyRepository)
    {
        _leadService = leadService;
        _currentUserContext = currentUserContext;
        _userAccessService = userAccessService;
        _companyRepository = companyRepository;
    }

    [HttpGet]
    public async Task<IActionResult> GetLeads(
        Guid companyId,
        [FromQuery] Guid? branchId = null,
        [FromQuery] Guid? assignedUserId = null,
        [FromQuery] LeadStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        var authResult = await ValidateScopeAccess(companyId, branchId);
        if (authResult.Action != null) return authResult.Action;

        var leads = await _leadService.GetLeadsAsync(
            authResult.TenantId,
            companyId,
            branchId,
            assignedUserId,
            status,
            cancellationToken);

        return Ok(leads);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetLeadById(
        Guid companyId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var authResult = await ValidateScopeAccess(companyId);
        if (authResult.Action != null) return authResult.Action;

        var lead = await _leadService.GetLeadByIdAsync(authResult.TenantId, companyId, id, cancellationToken);
        if (lead == null)
        {
            return NotFound(new { status = "FAIL", message = "Lead not found in the authorized scope." });
        }

        if (lead.BranchId.HasValue)
        {
            var branchAuth = await ValidateScopeAccess(companyId, lead.BranchId);
            if (branchAuth.Action != null) return branchAuth.Action;
        }

        return Ok(lead);
    }

    [HttpPost]
    [RequirePermission("CRM.LEAD.CREATE")]
    public async Task<IActionResult> CreateLead(
        Guid companyId,
        [FromBody] CreateLeadRequest request,
        CancellationToken cancellationToken = default)
    {
        var authResult = await ValidateScopeAccess(companyId, request.BranchId);
        if (authResult.Action != null) return authResult.Action;

        try
        {
            var lead = await _leadService.CreateLeadAsync(
                authResult.TenantId,
                companyId,
                request,
                cancellationToken);

            return CreatedAtAction(nameof(GetLeadById), new { companyId, id = lead.LeadId }, lead);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { status = "FAIL", message = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    [RequirePermission("CRM.LEAD.EDIT")]
    public async Task<IActionResult> UpdateLead(
        Guid companyId,
        Guid id,
        [FromBody] UpdateLeadRequest request,
        CancellationToken cancellationToken = default)
    {
        var authResult = await ValidateScopeAccess(companyId);
        if (authResult.Action != null) return authResult.Action;

        try
        {
            var lead = await _leadService.UpdateLeadAsync(
                authResult.TenantId,
                companyId,
                id,
                request,
                cancellationToken);

            return Ok(lead);
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

    [HttpPost("{id:guid}/assign")]
    [RequirePermission("CRM.LEAD.EDIT")]
    public async Task<IActionResult> AssignLead(
        Guid companyId,
        Guid id,
        [FromBody] AssignLeadRequest request,
        CancellationToken cancellationToken = default)
    {
        var authResult = await ValidateScopeAccess(companyId);
        if (authResult.Action != null) return authResult.Action;

        try
        {
            var lead = await _leadService.AssignLeadAsync(
                authResult.TenantId,
                companyId,
                id,
                request,
                cancellationToken);

            return Ok(lead);
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

    [HttpPost("{id:guid}/qualify")]
    [RequirePermission("CRM.LEAD.QUALIFY")]
    public async Task<IActionResult> QualifyLead(
        Guid companyId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var authResult = await ValidateScopeAccess(companyId);
        if (authResult.Action != null) return authResult.Action;

        try
        {
            var lead = await _leadService.QualifyLeadAsync(
                authResult.TenantId,
                companyId,
                id,
                cancellationToken);

            return Ok(lead);
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

    [HttpPost("{id:guid}/disqualify")]
    [RequirePermission("CRM.LEAD.QUALIFY")]
    public async Task<IActionResult> DisqualifyLead(
        Guid companyId,
        Guid id,
        [FromBody] DisqualifyLeadRequest request,
        CancellationToken cancellationToken = default)
    {
        var authResult = await ValidateScopeAccess(companyId);
        if (authResult.Action != null) return authResult.Action;

        try
        {
            var lead = await _leadService.DisqualifyLeadAsync(
                authResult.TenantId,
                companyId,
                id,
                request,
                cancellationToken);

            return Ok(lead);
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

    [HttpPost("{id:guid}/convert")]
    [RequirePermission("CRM.LEAD.CONVERT")]
    public async Task<IActionResult> ConvertLead(
        Guid companyId,
        Guid id,
        [FromBody] ConvertLeadRequest request,
        CancellationToken cancellationToken = default)
    {
        var authResult = await ValidateScopeAccess(companyId);
        if (authResult.Action != null) return authResult.Action;

        try
        {
            var result = await _leadService.ConvertLeadAsync(
                authResult.TenantId,
                companyId,
                id,
                request,
                cancellationToken);

            return Ok(result);
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
