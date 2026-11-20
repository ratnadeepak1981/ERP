using System;
using System.Threading;
using System.Threading.Tasks;
using API.Common;
using API.Features.Portal.Context;
using API.Features.Portal.DTOs;
using API.Features.Portal.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Features.Portal;

/// <summary>
/// Authenticated Customer Portal controller managing profile retrieval, selling company context,
/// and contact membership state within the authorized tenant scope.
/// </summary>
[ApiController]
[Route("api/portal")]
[Authorize]
public class CustomerProfileController : BaseApiController
{
    private readonly ICustomerAuthService _authService;
    private readonly ICurrentCustomerContext _customerContext;

    public CustomerProfileController(
        ICustomerAuthService authService,
        ICurrentCustomerContext customerContext)
    {
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        _customerContext = customerContext ?? throw new ArgumentNullException(nameof(customerContext));
    }

    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile(CancellationToken ct = default)
    {
        if (!_customerContext.IsCustomer)
        {
            return Forbid();
        }

        try
        {
            var profile = await _authService.GetProfileAsync(
                _customerContext.UserId,
                _customerContext.TenantId,
                ct);

            return SuccessResponse(profile);
        }
        catch (UnauthorizedAccessException ex)
        {
            return UnauthorizedResponse(ex.Message);
        }
        catch (ArgumentException ex)
        {
            return BadRequestResponse(ex.Message);
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpGet("selling-companies")]
    [HttpGet("auth/selling-companies")]
    public async Task<IActionResult> GetSellingCompanies(CancellationToken ct = default)
    {
        if (!_customerContext.IsCustomer)
        {
            return Forbid();
        }

        try
        {
            var companies = await _authService.GetAvailableSellingCompaniesAsync(
                _customerContext.TenantId,
                ct);

            return SuccessResponse(companies);
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpPost("selling-companies/select")]
    public async Task<IActionResult> SelectSellingCompany(
        [FromBody] SelectSellingCompanyRequest request,
        CancellationToken ct = default)
    {
        if (!_customerContext.IsCustomer)
        {
            return Forbid();
        }

        if (request == null || request.SellingCompanyId == Guid.Empty)
        {
            return BadRequestResponse("Selling company ID is required.");
        }

        try
        {
            var selectedCompany = await _authService.ValidateSellingCompanyContextAsync(
                _customerContext.CustomerId,
                _customerContext.TenantId,
                request.SellingCompanyId,
                ct);

            return SuccessResponse(new
            {
                customerId = _customerContext.CustomerId,
                contactId = _customerContext.ContactId,
                tenantId = _customerContext.TenantId,
                activeSellingCompany = selectedCompany
            }, "Selling company context activated successfully.");
        }
        catch (ArgumentException ex)
        {
            return BadRequestResponse(ex.Message);
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }
}
