using System;
using System.Linq;
using System.Threading.Tasks;
using API.Common;
using API.Security.Authorization;
using Domain.Features.MasterData.Branch;
using Domain.Features.MasterData.Company;
using Domain.Features.Setup;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaaS.Application.Interfaces;
using Security.Interfaces;
using Security.Services;

namespace API.Features.Setup;

public class InitializeOrganizationRequest : TenantOrganizationSetupRequest
{
    public Guid? TargetTenantId { get; set; }
}

public class InitializeUsersRequest : TenantUserSetupRequest
{
}

public class InitializeMasterDataRequest : TenantMasterDataSetupRequest
{
    public Guid? TargetTenantId { get; set; }
}

[ApiController]
[Route("api/tenant-setup")]
[Authorize]
public class TenantSetupController : BaseApiController
{
    private readonly ITenantOrganizationSetupService _setupService;
    private readonly ITenantUserSetupService _userSetupService;
    private readonly ITenantMasterDataSetupService _masterDataSetupService;
    private readonly ITenantRepository _tenantRepository;
    private readonly ICompanyRepository _companyRepository;
    private readonly IBranchRepository _branchRepository;
    private readonly ICurrentUserContext _currentUserContext;

    public TenantSetupController(
        ITenantOrganizationSetupService setupService,
        ITenantUserSetupService userSetupService,
        ITenantMasterDataSetupService masterDataSetupService,
        ITenantRepository tenantRepository,
        ICompanyRepository companyRepository,
        IBranchRepository branchRepository,
        ICurrentUserContext currentUserContext)
    {
        _setupService = setupService ?? throw new ArgumentNullException(nameof(setupService));
        _userSetupService = userSetupService ?? throw new ArgumentNullException(nameof(userSetupService));
        _masterDataSetupService = masterDataSetupService ?? throw new ArgumentNullException(nameof(masterDataSetupService));
        _tenantRepository = tenantRepository ?? throw new ArgumentNullException(nameof(tenantRepository));
        _companyRepository = companyRepository ?? throw new ArgumentNullException(nameof(companyRepository));
        _branchRepository = branchRepository ?? throw new ArgumentNullException(nameof(branchRepository));
        _currentUserContext = currentUserContext ?? throw new ArgumentNullException(nameof(currentUserContext));
    }

    [HttpPost("initialize-organization")]
    public async Task<IActionResult> InitializeOrganization([FromBody] InitializeOrganizationRequest? request)
    {
        // 1. Resolve target tenant
        Guid targetTenantId;
        if (_currentUserContext.IsPlatformUser)
        {
            if (request?.TargetTenantId == null || request.TargetTenantId == Guid.Empty)
            {
                return BadRequestResponse("Platform administrators must specify TargetTenantId.");
            }
            targetTenantId = request.TargetTenantId.Value;
        }
        else
        {
            if (!_currentUserContext.TenantId.HasValue || _currentUserContext.TenantId.Value == Guid.Empty)
            {
                return UnauthorizedResponse("Tenant context is required.");
            }
            targetTenantId = _currentUserContext.TenantId.Value;
        }

        // 2. Lookup tenant metadata to default company name and code
        var tenant = await _tenantRepository.GetByIdAsync(targetTenantId);
        if (tenant == null)
        {
            return NotFoundResponse($"Tenant with ID '{targetTenantId}' was not found.");
        }

        var setupRequest = request ?? new InitializeOrganizationRequest();
        if (string.IsNullOrWhiteSpace(setupRequest.CompanyName))
        {
            setupRequest.CompanyName = tenant.Name;
        }
        if (string.IsNullOrWhiteSpace(setupRequest.CompanyCode))
        {
            setupRequest.CompanyCode = tenant.Code;
        }

        try
        {
            var result = await _setupService.SetupOrganizationAsync(targetTenantId, setupRequest);
            return SuccessResponse(result, "Tenant organization setup completed successfully.");
        }
        catch (ArgumentException ex)
        {
            return BadRequestResponse(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequestResponse(ex.Message);
        }
        catch (FileNotFoundException ex)
        {
            return BadRequestResponse(ex.Message);
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpPost("initialize-users")]
    public async Task<IActionResult> InitializeUsers([FromBody] InitializeUsersRequest? request)
    {
        // 1. Resolve target tenant
        Guid targetTenantId;
        if (_currentUserContext.IsPlatformUser)
        {
            if (request?.TargetTenantId == null || request.TargetTenantId == Guid.Empty)
            {
                return BadRequestResponse("Platform administrators must specify TargetTenantId.");
            }
            targetTenantId = request.TargetTenantId.Value;
        }
        else
        {
            if (!_currentUserContext.TenantId.HasValue || _currentUserContext.TenantId.Value == Guid.Empty)
            {
                return UnauthorizedResponse("Tenant context is required.");
            }
            targetTenantId = _currentUserContext.TenantId.Value;
        }

        var tenant = await _tenantRepository.GetByIdAsync(targetTenantId);
        if (tenant == null)
        {
            return NotFoundResponse($"Tenant with ID '{targetTenantId}' was not found.");
        }

        var userSetupRequest = request ?? new InitializeUsersRequest();

        // 2. Resolve default CompanyId and BranchId if not supplied, or validate supplied scopes
        if (!userSetupRequest.CompanyId.HasValue || userSetupRequest.CompanyId == Guid.Empty)
        {
            var companies = await _companyRepository.GetByTenantAsync(targetTenantId);
            var primaryCompany = companies.FirstOrDefault();
            if (primaryCompany != null)
            {
                userSetupRequest.CompanyId = primaryCompany.Id;

                if (!userSetupRequest.BranchId.HasValue || userSetupRequest.BranchId == Guid.Empty)
                {
                    var branches = await _branchRepository.GetByCompanyAsync(targetTenantId, primaryCompany.Id);
                    var primaryBranch = branches.FirstOrDefault();
                    if (primaryBranch != null)
                    {
                        userSetupRequest.BranchId = primaryBranch.Id;
                    }
                }
            }
        }
        else
        {
            // Verify Company and Branch strictly belong to targetTenantId
            var company = await _companyRepository.GetByIdAsync(targetTenantId, userSetupRequest.CompanyId.Value);
            if (company == null)
            {
                return BadRequestResponse($"Company '{userSetupRequest.CompanyId}' does not belong to target tenant '{targetTenantId}'.");
            }

            if (userSetupRequest.BranchId.HasValue && userSetupRequest.BranchId.Value != Guid.Empty)
            {
                var branch = await _branchRepository.GetByIdAsync(targetTenantId, company.Id, userSetupRequest.BranchId.Value);
                if (branch == null)
                {
                    return BadRequestResponse($"Branch '{userSetupRequest.BranchId}' does not belong to company '{company.Id}'.");
                }
            }
        }

        // 3. Resolve tenant code and email domain if not supplied
        string rawTenantCode = !string.IsNullOrWhiteSpace(userSetupRequest.TenantCode)
            ? userSetupRequest.TenantCode
            : (!string.IsNullOrWhiteSpace(userSetupRequest.TenantShortCode)
                ? userSetupRequest.TenantShortCode
                : (tenant.Code ?? tenant.Name ?? "APX"));

        string cleanTenantCode = TenantUserSetupService.NormalizeTenantCode(rawTenantCode, tenant.Name);
        userSetupRequest.TenantCode = cleanTenantCode;
        userSetupRequest.TenantShortCode = cleanTenantCode;

        if (string.IsNullOrWhiteSpace(userSetupRequest.EmailDomain))
        {
            userSetupRequest.EmailDomain = $"{cleanTenantCode.ToLowerInvariant()}.local";
        }

        try
        {
            var result = await _userSetupService.SetupTenantUsersAsync(targetTenantId, userSetupRequest);
            return SuccessResponse(result, "Tenant user setup completed successfully.");
        }
        catch (ArgumentException ex)
        {
            return BadRequestResponse(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequestResponse(ex.Message);
        }
        catch (FileNotFoundException ex)
        {
            return BadRequestResponse(ex.Message);
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpPost("initialize-master-data")]
    public async Task<IActionResult> InitializeMasterData([FromBody] InitializeMasterDataRequest? request)
    {
        // 1. Resolve target tenant
        Guid targetTenantId;
        if (_currentUserContext.IsPlatformUser)
        {
            if (request?.TargetTenantId == null || request.TargetTenantId == Guid.Empty)
            {
                return BadRequestResponse("Platform administrators must specify TargetTenantId.");
            }
            targetTenantId = request.TargetTenantId.Value;
        }
        else
        {
            if (!_currentUserContext.TenantId.HasValue || _currentUserContext.TenantId.Value == Guid.Empty)
            {
                return UnauthorizedResponse("Tenant context is required.");
            }
            targetTenantId = _currentUserContext.TenantId.Value;
        }

        var tenant = await _tenantRepository.GetByIdAsync(targetTenantId);
        if (tenant == null)
        {
            return NotFoundResponse($"Tenant with ID '{targetTenantId}' was not found.");
        }

        var masterDataRequest = request ?? new InitializeMasterDataRequest();

        // 2. Resolve default CompanyId and BranchId if not supplied
        if (!masterDataRequest.CompanyId.HasValue || masterDataRequest.CompanyId == Guid.Empty)
        {
            var companies = await _companyRepository.GetByTenantAsync(targetTenantId);
            var primaryCompany = companies.FirstOrDefault();
            if (primaryCompany != null)
            {
                masterDataRequest.CompanyId = primaryCompany.Id;

                if (!masterDataRequest.BranchId.HasValue || masterDataRequest.BranchId == Guid.Empty)
                {
                    var branches = await _branchRepository.GetByCompanyAsync(targetTenantId, primaryCompany.Id);
                    var primaryBranch = branches.FirstOrDefault();
                    if (primaryBranch != null)
                    {
                        masterDataRequest.BranchId = primaryBranch.Id;
                    }
                }
            }
        }

        try
        {
            var result = await _masterDataSetupService.SetupMasterDataAsync(targetTenantId, masterDataRequest);
            return SuccessResponse(result, "Tenant master data setup completed successfully.");
        }
        catch (ArgumentException ex)
        {
            return BadRequestResponse(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequestResponse(ex.Message);
        }
        catch (FileNotFoundException ex)
        {
            return BadRequestResponse(ex.Message);
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }
}
