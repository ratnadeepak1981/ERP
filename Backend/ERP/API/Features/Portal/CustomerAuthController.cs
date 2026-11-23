using System;
using System.Threading;
using System.Threading.Tasks;
using API.Common;
using API.Features.Portal.DTOs;
using API.Features.Portal.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Features.Portal;

/// <summary>
/// Public-facing controller governing customer registration and authentication.
/// Follows Customer-Workspace-and-Registration-Workflow.md (v1.3.0) Section 4, 12, and 13.
/// </summary>
[ApiController]
[Route("api/portal/auth")]
[AllowAnonymous]
public class CustomerAuthController : BaseApiController
{
    private readonly ICustomerRegistrationService _registrationService;
    private readonly ICustomerAuthService _authService;
    private readonly API.Common.TenantResolution.ITenantContext _tenantContext;

    public CustomerAuthController(
        ICustomerRegistrationService registrationService,
        ICustomerAuthService authService,
        API.Common.TenantResolution.ITenantContext tenantContext)
    {
        _registrationService = registrationService ?? throw new ArgumentNullException(nameof(registrationService));
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
    }

    [HttpPost("register")]
    [HttpPost("register-company")]
    public async Task<IActionResult> RegisterCompanyCustomer(
        [FromBody] RegisterCompanyCustomerRequest request,
        CancellationToken ct = default)
    {
        if (request == null)
            return BadRequestResponse("Registration payload is required.");

        if (request.TenantId == Guid.Empty && _tenantContext.IsTenantDomainRequest && _tenantContext.TenantId.HasValue)
        {
            request.TenantId = _tenantContext.TenantId.Value;
        }

        try
        {
            var result = await _registrationService.RegisterCompanyCustomerAsync(request, ct);
            return CreatedResponse(result, result.Message);
        }
        catch (ArgumentException ex)
        {
            return BadRequestResponse(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return ConflictResponse(ex.Message);
        }
    }

    [HttpPost("register-individual")]
    public async Task<IActionResult> RegisterIndividualCustomer(
        [FromBody] RegisterIndividualCustomerRequest request,
        CancellationToken ct = default)
    {
        if (request == null)
            return BadRequestResponse("Registration payload is required.");

        if (request.TenantId == Guid.Empty && _tenantContext.IsTenantDomainRequest && _tenantContext.TenantId.HasValue)
        {
            request.TenantId = _tenantContext.TenantId.Value;
        }

        try
        {
            var result = await _registrationService.RegisterIndividualCustomerAsync(request, ct);
            return CreatedResponse(result, result.Message);
        }
        catch (ArgumentException ex)
        {
            return BadRequestResponse(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return ConflictResponse(ex.Message);
        }
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(
        [FromBody] CustomerLoginRequest request,
        CancellationToken ct = default)
    {
        if (request == null)
            return BadRequestResponse("Login credentials are required.");

        try
        {
            var result = await _authService.LoginAsync(request, ct);
            return SuccessResponse(result, "Customer authentication successful.");
        }
        catch (ArgumentException ex)
        {
            return BadRequestResponse(ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return UnauthorizedResponse(ex.Message);
        }
    }
}
