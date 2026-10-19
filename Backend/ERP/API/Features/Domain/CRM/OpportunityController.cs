using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using API.Common;
using API.Common.Sorting;
using API.Security.Authorization;
using ERP.Domain.Features.CRM;
using Microsoft.AspNetCore.Mvc;
using Security.Interfaces;

namespace API.Features.Domain.CRM;

[ApiController]
[Route("api/companies/{companyId:guid}/crm/opportunities")]
[RequirePermission("CRM.OPPORTUNITY.VIEW")]
public class OpportunityController : BaseApiController
{
    private readonly IOpportunityService _opportunityService;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IUserAccessService _userAccessService;
    private readonly global::Domain.Features.MasterData.Company.ICompanyRepository _companyRepository;

    private static readonly SortMapping<OpportunityDto> _sortMapping = new SortMapping<OpportunityDto>()
        .Add("title", o => o.Title, isDefault: true)
        .Add("opportunityNumber", o => o.OpportunityNumber)
        .Add("stage", o => o.Stage)
        .Add("estimatedValue", o => o.EstimatedValue)
        .Add("createdAt", o => o.CreatedAt)
        .SetSecondary(o => o.OpportunityId);

    public OpportunityController(
        IOpportunityService opportunityService,
        ICurrentUserContext currentUserContext,
        IUserAccessService userAccessService,
        global::Domain.Features.MasterData.Company.ICompanyRepository companyRepository)
    {
        _opportunityService = opportunityService;
        _currentUserContext = currentUserContext;
        _userAccessService = userAccessService;
        _companyRepository = companyRepository;
    }

    [HttpGet]
    public async Task<IActionResult> GetOpportunities(
        Guid companyId,
        [FromQuery] Guid? branchId = null,
        [FromQuery] Guid? customerId = null,
        [FromQuery] OpportunityStage? stage = null,
        [FromQuery] int? page = null,
        [FromQuery] int? pageSize = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortDirection = null,
        CancellationToken cancellationToken = default)
    {
        var authResult = await ValidateScopeAccess(companyId, branchId);
        if (authResult.Action != null) return authResult.Action;

        var opportunities = await _opportunityService.GetOpportunitiesAsync(
            authResult.TenantId,
            companyId,
            branchId,
            customerId,
            stage,
            cancellationToken);

        return PaginateAndSort(opportunities, page, pageSize, sortBy, sortDirection, _sortMapping);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetOpportunityById(
        Guid companyId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var authResult = await ValidateScopeAccess(companyId);
        if (authResult.Action != null) return authResult.Action;

        var opp = await _opportunityService.GetOpportunityByIdAsync(
            authResult.TenantId,
            companyId,
            id,
            cancellationToken);

        if (opp == null)
        {
            return NotFoundResponse("Opportunity not found in the authorized scope.");
        }

        if (opp.BranchId.HasValue)
        {
            var branchAuth = await ValidateScopeAccess(companyId, opp.BranchId);
            if (branchAuth.Action != null) return branchAuth.Action;
        }

        return SuccessResponse(opp);
    }

    [HttpPost]
    [RequirePermission("CRM.OPPORTUNITY.MANAGE")]
    public async Task<IActionResult> CreateOpportunity(
        Guid companyId,
        [FromBody] CreateOpportunityRequest request,
        CancellationToken cancellationToken = default)
    {
        var authResult = await ValidateScopeAccess(companyId, request.BranchId);
        if (authResult.Action != null) return authResult.Action;

        try
        {
            var opp = await _opportunityService.CreateOpportunityAsync(
                authResult.TenantId,
                companyId,
                request,
                cancellationToken);

            return CreatedResponse(nameof(GetOpportunityById), new { companyId, id = opp.OpportunityId }, opp, "Opportunity created successfully.");
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpPut("{id:guid}")]
    [RequirePermission("CRM.OPPORTUNITY.MANAGE")]
    public async Task<IActionResult> UpdateOpportunity(
        Guid companyId,
        Guid id,
        [FromBody] UpdateOpportunityRequest request,
        CancellationToken cancellationToken = default)
    {
        var authResult = await ValidateScopeAccess(companyId);
        if (authResult.Action != null) return authResult.Action;

        try
        {
            var opp = await _opportunityService.UpdateOpportunityAsync(
                authResult.TenantId,
                companyId,
                id,
                request,
                cancellationToken);

            return SuccessResponse(opp, "Opportunity updated successfully.");
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpPost("{id:guid}/stage")]
    [RequirePermission("CRM.OPPORTUNITY.MANAGE")]
    public async Task<IActionResult> UpdateOpportunityStage(
        Guid companyId,
        Guid id,
        [FromBody] UpdateOpportunityStageRequest request,
        CancellationToken cancellationToken = default)
    {
        var authResult = await ValidateScopeAccess(companyId);
        if (authResult.Action != null) return authResult.Action;

        try
        {
            var opp = await _opportunityService.UpdateOpportunityStageAsync(
                authResult.TenantId,
                companyId,
                id,
                request,
                cancellationToken);

            return SuccessResponse(opp, "Opportunity stage updated successfully.");
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
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
            return (UnauthorizedResponse("Tenant identity is missing or invalid."), Guid.Empty);
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
