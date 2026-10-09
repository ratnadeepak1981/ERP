using API.Security.Authorization;
using Domain.Features.MasterData.Company;
using Microsoft.AspNetCore.Mvc;
using Security.Interfaces;
using CompanyEntity = Domain.Features.MasterData.Company.Company;

namespace API.Features.MasterMasterData.Company;

[ApiController]
[Route("api/companies")]
[RequirePermission("COMPANY.VIEW")]
public class CompanyController : ControllerBase
{
    private readonly ICompanyService _companyService;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IUserAccessService _userAccessService;

    public CompanyController(
        ICompanyService companyService,
        ICurrentUserContext currentUserContext,
        IUserAccessService userAccessService)
    {
        _companyService = companyService;
        _currentUserContext = currentUserContext;
        _userAccessService = userAccessService;
    }

    [HttpGet]
    public async Task<IActionResult> GetCompanies()
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

        var companies =
            await _companyService.GetCompaniesAsync(
                tenantId.Value);

        var accessibleCompanies =
            new List<CompanyEntity>();

        foreach (var company in companies)
        {
            var canAccess =
                await _userAccessService.CanAccessCompanyAsync(
                    _currentUserContext.UserId,
                    tenantId.Value,
                    company.Id);

            if (canAccess)
            {
                accessibleCompanies.Add(company);
            }
        }

        return Ok(accessibleCompanies);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetCompany(
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

        var company =
            await _companyService.GetCompanyAsync(
                tenantId.Value,
                id);

        if (company == null)
        {
            return NotFound(new
            {
                status = "FAIL",
                message = "Company not found."
            });
        }

        var canAccess =
            await _userAccessService.CanAccessCompanyAsync(
                _currentUserContext.UserId,
                tenantId.Value,
                id);

        if (!canAccess)
        {
            return Forbid();
        }

        return Ok(company);
    }
}