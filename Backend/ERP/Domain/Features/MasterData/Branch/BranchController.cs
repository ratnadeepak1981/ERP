using Domain.Features.MasterData.Branch;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Security.Interfaces;
using System.Runtime.InteropServices;

namespace API.Features.MasterData.Branch;

[ApiController]
[Route("api/companies/{companyId:guid}/branches")]
[Authorize(Policy = "BRANCH_VIEW")]
public class BranchController : ControllerBase
{
    private readonly IBranchService _branchService;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IUserAccessService _userAccessService;

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
        Guid companyId)
    {
        if (_currentUserContext.IsPlatformUser)
        {
            return Forbid();
        }

        var tenantId = _currentUserContext.TenantId;

        if (!tenantId.HasValue)
        {
            return Unauthorized(new
            {
                status = "FAIL",
                message = "Tenant identity is missing or invalid."
            });
        }

        var canAccessCompany =
            await _userAccessService.CanAccessCompanyAsync(
                _currentUserContext.UserId,
                tenantId.Value,
                companyId);

        if (!canAccessCompany)
        {
            return Forbid();
        }

        var branches =
            await _branchService.GetBranchesAsync(
                tenantId.Value,
                companyId);

        return Ok(branches);
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
            return Unauthorized(new
            {
                status = "FAIL",
                message = "Tenant identity is missing or invalid."
            });
        }

        var canAccessCompany =
            await _userAccessService.CanAccessCompanyAsync(
                _currentUserContext.UserId,
                tenantId.Value,
                companyId);

        if (!canAccessCompany)
        {
            return Forbid();
        }

        var branch =
            await _branchService.GetBranchAsync(
                tenantId.Value,
                companyId,
                id);

        if (branch == null)
        {
            return NotFound(new
            {
                status = "FAIL",
                message = "Branch not found."
            });
        }

        return Ok(branch);
    }
}