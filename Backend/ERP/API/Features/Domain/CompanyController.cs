using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using API.Common;
using API.Common.Sorting;
using API.Security.Authorization;
using Domain.Features.MasterData.Company;
using Microsoft.AspNetCore.Mvc;
using Security.Interfaces;
using CompanyEntity = Domain.Features.MasterData.Company.Company;

namespace API.Features.MasterMasterData.Company;

public class CreateCompanyRequest
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public Guid? BaseCurrencyId { get; set; }
}

public class UpdateCompanyRequest
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public Guid? BaseCurrencyId { get; set; }
    public bool? SpecialUomConversionEnabled { get; set; }
}

[ApiController]
[Route("api/companies")]
[RequirePermission("COMPANY.VIEW")]
public class CompanyController : BaseApiController
{
    private readonly ICompanyService _companyService;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IUserAccessService _userAccessService;

    private static readonly SortMapping<CompanyEntity> _sortMapping = new SortMapping<CompanyEntity>()
        .Add("name", c => c.Name, isDefault: true)
        .Add("code", c => c.Code)
        .SetSecondary(c => c.Id);

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
    public async Task<IActionResult> GetCompanies(
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

        var companies = await _companyService.GetCompaniesAsync(tenantId.Value);
        var accessibleCompanies = new List<CompanyEntity>();

        foreach (var company in companies)
        {
            var canAccess = await _userAccessService.CanAccessCompanyAsync(
                _currentUserContext.UserId,
                tenantId.Value,
                company.Id);

            if (canAccess)
            {
                accessibleCompanies.Add(company);
            }
        }

        return PaginateAndSort(accessibleCompanies, page, pageSize, sortBy, sortDirection, _sortMapping);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetCompany(Guid id)
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

        var company = await _companyService.GetCompanyAsync(tenantId.Value, id);
        if (company == null)
        {
            return NotFoundResponse("Company not found.");
        }

        var canAccess = await _userAccessService.CanAccessCompanyAsync(
            _currentUserContext.UserId,
            tenantId.Value,
            id);

        if (!canAccess)
        {
            return Forbid();
        }

        return SuccessResponse(company);
    }

    [HttpPost]
    [RequirePermission("COMPANY.CREATE")]
    public async Task<IActionResult> CreateCompany([FromBody] CreateCompanyRequest request)
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

        try
        {
            var company = await _companyService.CreateCompanyAsync(
                tenantId.Value,
                request.Name,
                request.Code,
                request.BaseCurrencyId);

            return CreatedResponse(company);
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

    [HttpPut("{id:guid}")]
    [RequirePermission("COMPANY.EDIT")]
    public async Task<IActionResult> UpdateCompany(Guid id, [FromBody] UpdateCompanyRequest request)
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

        var canAccess = await _userAccessService.CanAccessCompanyAsync(
            _currentUserContext.UserId,
            tenantId.Value,
            id);

        if (!canAccess)
        {
            return Forbid();
        }

        try
        {
            var company = await _companyService.UpdateCompanyAsync(
                tenantId.Value,
                id,
                request.Name,
                request.Code,
                request.IsActive,
                request.BaseCurrencyId,
                request.SpecialUomConversionEnabled);

            return SuccessResponse(company);
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
