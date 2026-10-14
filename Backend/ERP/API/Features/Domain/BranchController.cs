using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using API.Common;
using API.Common.Sorting;
using API.Security.Authorization;
using Domain.Features.MasterData.Branch;
using Microsoft.AspNetCore.Mvc;
using Security.Interfaces;

namespace API.Features.Domain;

[ApiController]
[Route("api/companies/{companyId:guid}/branches")]
[RequirePermission("BRANCH.VIEW")]
public class BranchController : BaseApiController
{
    private readonly IBranchService _branchService;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IUserAccessService _userAccessService;

    private static readonly SortMapping<Branch> _sortMapping = new SortMapping<Branch>()
        .Add("name", b => b.Name, isDefault: true)
        .Add("code", b => b.Code)
        .SetSecondary(b => b.Id);

    public BranchController(
        IBranchService branchService,
        ICurrentUserContext currentUserContext,
        IUserAccessService userAccessService)
    {
        _branchService = branchService;
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

        return PaginateAndSort(branches, page, pageSize, sortBy, sortDirection, _sortMapping);
    }

    [HttpGet("{id:guid}")]
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

        var canAccessCompany = await _userAccessService.CanAccessCompanyAsync(
            _currentUserContext.UserId,
            tenantId.Value,
            companyId);

        if (!canAccessCompany)
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
}