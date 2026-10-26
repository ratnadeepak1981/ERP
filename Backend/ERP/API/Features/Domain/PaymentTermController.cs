using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using API.Common;
using API.Common.Sorting;
using API.Security.Authorization;
using Domain.Features.MasterData.PaymentTerm;
using Microsoft.AspNetCore.Mvc;
using Security.Interfaces;

namespace API.Features.MasterData.PaymentTerm;

[ApiController]
[Route("api/payment-terms")]
[RequirePermission("PAYMENT_TERM.VIEW")]
public class PaymentTermController : BaseApiController
{
    private readonly IPaymentTermService _paymentTermService;
    private readonly ICurrentUserContext _currentUserContext;

    private static readonly SortMapping<global::Domain.Features.MasterData.PaymentTerm.PaymentTerm> _sortMapping =
        new SortMapping<global::Domain.Features.MasterData.PaymentTerm.PaymentTerm>()
            .Add("termCode", p => p.TermCode, isDefault: true)
            .Add("termName", p => p.TermName)
            .Add("days", p => p.Days)
            .SetSecondary(p => p.PaymentTermId);

    public PaymentTermController(
        IPaymentTermService paymentTermService,
        ICurrentUserContext currentUserContext)
    {
        _paymentTermService = paymentTermService;
        _currentUserContext = currentUserContext;
    }

    [HttpGet]
    public async Task<IActionResult> GetPaymentTerms(
        [FromQuery] int? page = null,
        [FromQuery] int? pageSize = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortDirection = null)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        var terms = await _paymentTermService.GetPaymentTermsAsync(tenantId.Value);
        return PaginateAndSort(terms, page, pageSize, sortBy, sortDirection, _sortMapping);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetPaymentTerm(Guid id)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        var term = await _paymentTermService.GetPaymentTermAsync(tenantId.Value, id);
        if (term == null)
            return NotFoundResponse("Payment term not found.");

        return SuccessResponse(term);
    }

    [HttpPost]
    [RequirePermission("PAYMENT_TERM.CREATE")]
    public async Task<IActionResult> CreatePaymentTerm([FromBody] CreatePaymentTermRequest request)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        try
        {
            var term = await _paymentTermService.CreatePaymentTermAsync(tenantId.Value, request);
            return CreatedResponse(nameof(GetPaymentTerm), new { id = term.PaymentTermId }, term);
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpPut("{id:guid}")]
    [RequirePermission("PAYMENT_TERM.EDIT")]
    public async Task<IActionResult> UpdatePaymentTerm(Guid id, [FromBody] UpdatePaymentTermRequest request)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        try
        {
            var term = await _paymentTermService.UpdatePaymentTermAsync(tenantId.Value, id, request);
            return SuccessResponse(term);
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission("PAYMENT_TERM.DELETE")]
    public async Task<IActionResult> DeactivatePaymentTerm(Guid id)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        try
        {
            await _paymentTermService.DeactivatePaymentTermAsync(tenantId.Value, id);
            return NoContent();
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpPost("{id:guid}/reactivate")]
    [RequirePermission("PAYMENT_TERM.EDIT")]
    public async Task<IActionResult> ReactivatePaymentTerm(Guid id)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        try
        {
            var term = await _paymentTermService.ReactivatePaymentTermAsync(tenantId.Value, id);
            return SuccessResponse(term, "Payment term reactivated successfully.");
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }
}
