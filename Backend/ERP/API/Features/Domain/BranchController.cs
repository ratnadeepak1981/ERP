using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using API.Common;
using API.Common.Sorting;
using API.Security.Authorization;
using Domain.Features.MasterData.Branch;
using Domain.Features.MasterData.Company;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Security.Interfaces;

namespace API.Features.Domain;

public class CreateBranchRequest
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
}

[ApiController]
[Route("api/companies/{companyId:guid}/branches")]
[Authorize]
public class BranchController : BaseApiController
{
    private readonly IBranchService _branchService;
    private readonly ICompanyService _companyService;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IUserAccessService _userAccessService;

    private static readonly SortMapping<Branch> _sortMapping = new SortMapping<Branch>()
        .Add("name", b => b.Name, isDefault: true)
        .Add("code", b => b.Code)
        .SetSecondary(b => b.Id);

    public BranchController(
        IBranchService branchService,
        ICompanyService companyService,
        ICurrentUserContext currentUserContext,
        IUserAccessService userAccessService)
    {
        _branchService = branchService;
        _companyService = companyService;
        _currentUserContext = currentUserContext;
        _userAccessService = userAccessService;
    }

    [HttpGet]
    public async Task<IActionResult> GetBranches(
        Guid companyId,
        [FromQuery] int? page = null,
        [FromQuery] int? pageSize = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortDirection = null)
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

        var company = await _companyService.GetCompanyAsync(tenantId.Value, companyId);
        if (company == null)
        {
            return Forbid();
        }

        var canAccessCompany = await _userAccessService.CanAccessCompanyAsync(
            _currentUserContext.UserId,
            tenantId.Value,
            companyId);

        if (!canAccessCompany)
        {
            return Forbid();
        }

        var branches = await _branchService.GetBranchesAsync(
            tenantId.Value,
            companyId);

        var accessibleBranches = new List<Branch>();
        foreach (var branch in branches)
        {
            var canAccess = await _userAccessService.CanAccessBranchAsync(
                _currentUserContext.UserId,
                tenantId.Value,
                companyId,
                branch.Id);

            if (canAccess)
            {
                accessibleBranches.Add(branch);
            }
        }

        return PaginateAndSort(accessibleBranches, page, pageSize, sortBy, sortDirection, _sortMapping);
    }

    [HttpGet("{id:guid}")]
    [RequirePermission("BRANCH.VIEW")]
    public async Task<IActionResult> GetBranch(
        Guid companyId,
        Guid id)
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

        var company = await _companyService.GetCompanyAsync(tenantId.Value, companyId);
        if (company == null)
        {
            return Forbid();
        }

        var canAccessCompany = await _userAccessService.CanAccessCompanyAsync(
            _currentUserContext.UserId,
            tenantId.Value,
            companyId);

        if (!canAccessCompany)
        {
            return Forbid();
        }

        var canAccessBranch = await _userAccessService.CanAccessBranchAsync(
            _currentUserContext.UserId,
            tenantId.Value,
            companyId,
            id);

        if (!canAccessBranch)
        {
            return Forbid();
        }

        var branch = await _branchService.GetBranchAsync(
            tenantId.Value,
            companyId,
            id);

        if (branch == null)
        {
            return NotFoundResponse("Branch not found.");
        }

        return SuccessResponse(branch);
    }

    [HttpPost]
    [RequirePermission("BRANCH.CREATE")]
    public async Task<IActionResult> CreateBranch(
        Guid companyId,
        [FromBody] CreateBranchRequest request)
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

        var company = await _companyService.GetCompanyAsync(tenantId.Value, companyId);
        if (company == null)
        {
            return Forbid();
        }

        if (request == null || string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Code))
        {
            return BadRequestResponse("Branch name and code are required.");
        }

        var canAccessCompany = await _userAccessService.CanAccessCompanyAsync(
            _currentUserContext.UserId,
            tenantId.Value,
            companyId);

        if (!canAccessCompany)
        {
            return Forbid();
        }

        try
        {
            var branch = await _branchService.CreateBranchAsync(
                tenantId.Value,
                companyId,
                request.Name,
                request.Code);

            return CreatedResponse(branch);
        }
        catch (ArgumentException ex)
        {
            return BadRequestResponse(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequestResponse(ex.Message);
        }
    }
}