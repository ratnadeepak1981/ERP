using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using API.Common;
using API.Common.Sorting;
using API.Security.Authorization;
using Domain.Features.Sales.SalesQuotation;
using ERP.Application.Features.Sales.SalesQuotation;
using Microsoft.AspNetCore.Mvc;
using Security.Interfaces;

namespace API.Features.Domain;

[ApiController]
[Route("api/companies/{companyId:guid}/branches/{branchId:guid}/sales-quotations")]
[RequirePermission("SALES_QUOTATION.VIEW")]
public class SalesQuotationController : BaseApiController
{
    private readonly ISalesQuotationService _quotationService;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IUserAccessService _userAccessService;

    private static readonly SortMapping<SalesQuotationDto> _sortMapping = new SortMapping<SalesQuotationDto>()
        .Add("quotationNumber", q => q.QuotationNumber, isDefault: true)
        .Add("quotationDate", q => q.QuotationDate)
        .Add("validUntilDateUtc", q => q.ValidUntilDateUtc)
        .Add("totalAmount", q => q.TotalAmount)
        .Add("status", q => q.Status)
        .SetSecondary(q => q.Id);

    public SalesQuotationController(
        ISalesQuotationService quotationService,
        ICurrentUserContext currentUserContext,
        IUserAccessService userAccessService)
    {
        _quotationService = quotationService ?? throw new ArgumentNullException(nameof(quotationService));
        _currentUserContext = currentUserContext ?? throw new ArgumentNullException(nameof(currentUserContext));
        _userAccessService = userAccessService ?? throw new ArgumentNullException(nameof(userAccessService));
    }

    [HttpGet]
    public async Task<IActionResult> GetQuotations(
        Guid companyId,
        Guid branchId,
        [FromQuery] SalesQuotationStatus? status = null,
        [FromQuery] SalesQuotationType? documentType = null,
        [FromQuery] int? page = null,
        [FromQuery] int? pageSize = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortDirection = null)
    {
        var authResult = await ValidateScopeAccess(companyId, branchId);
        if (authResult.Action != null) return authResult.Action;

        var quotations = await _quotationService.GetQuotationsAsync(
            authResult.TenantId,
            companyId,
            branchId,
            status,
            documentType);

        return PaginateAndSort(quotations, page, pageSize, sortBy, sortDirection, _sortMapping);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetQuotation(
        Guid companyId,
        Guid branchId,
        Guid id)
    {
        var authResult = await ValidateScopeAccess(companyId, branchId);
        if (authResult.Action != null) return authResult.Action;

        var quotation = await _quotationService.GetQuotationAsync(
            authResult.TenantId,
            companyId,
            branchId,
            id);

        if (quotation == null)
        {
            return NotFoundResponse("Sales quotation not found in the authorized scope.");
        }

        return SuccessResponse(quotation);
    }

    [HttpPost]
    [RequirePermission("SALES_QUOTATION.CREATE")]
    public async Task<IActionResult> CreateDraft(
        Guid companyId,
        Guid branchId,
        [FromBody] CreateSalesQuotationDraftRequest request)
    {
        var authResult = await ValidateScopeAccess(companyId, branchId);
        if (authResult.Action != null) return authResult.Action;

        try
        {
            var quotation = await _quotationService.CreateQuotationDraftAsync(
                authResult.TenantId,
                companyId,
                branchId,
                request,
                _currentUserContext.UserId,
                User.Identity?.Name ?? _currentUserContext.UserId.ToString());

            return CreatedResponse(
                nameof(GetQuotation),
                new { companyId, branchId, id = quotation.Id },
                quotation);
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpPost("request")]
    [RequirePermission("SALES_QUOTATION.CREATE")]
    public async Task<IActionResult> CreateQuotationRequest(
        Guid companyId,
        Guid branchId,
        [FromBody] CreateCustomerQuotationRequest request)
    {
        var authResult = await ValidateScopeAccess(companyId, branchId);
        if (authResult.Action != null) return authResult.Action;

        try
        {
            var quotation = await _quotationService.CreateCustomerQuotationRequestAsync(
                authResult.TenantId,
                companyId,
                branchId,
                request,
                _currentUserContext.UserId,
                User.Identity?.Name ?? _currentUserContext.UserId.ToString());

            return CreatedResponse(
                nameof(GetQuotation),
                new { companyId, branchId, id = quotation.Id },
                quotation);
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpPut("{id:guid}")]
    [RequirePermission("SALES_QUOTATION.EDIT")]
    public async Task<IActionResult> UpdateDraft(
        Guid companyId,
        Guid branchId,
        Guid id,
        [FromBody] UpdateSalesQuotationDraftRequest request)
    {
        var authResult = await ValidateScopeAccess(companyId, branchId);
        if (authResult.Action != null) return authResult.Action;

        try
        {
            var quotation = await _quotationService.UpdateQuotationDraftAsync(
                authResult.TenantId,
                companyId,
                branchId,
                id,
                request);

            return SuccessResponse(quotation, "Sales quotation updated successfully.");
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpPost("{id:guid}/items")]
    [RequirePermission("SALES_QUOTATION.EDIT")]
    public async Task<IActionResult> AddItem(
        Guid companyId,
        Guid branchId,
        Guid id,
        [FromBody] CreateSalesQuotationItemRequest request)
    {
        var authResult = await ValidateScopeAccess(companyId, branchId);
        if (authResult.Action != null) return authResult.Action;

        try
        {
            var item = await _quotationService.AddItemAsync(
                authResult.TenantId,
                companyId,
                branchId,
                id,
                request);

            return SuccessResponse(item, "Item added to sales quotation successfully.");
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpPut("{id:guid}/items/{itemId:guid}")]
    [RequirePermission("SALES_QUOTATION.EDIT")]
    public async Task<IActionResult> UpdateItem(
        Guid companyId,
        Guid branchId,
        Guid id,
        Guid itemId,
        [FromBody] UpdateSalesQuotationItemRequest request)
    {
        var authResult = await ValidateScopeAccess(companyId, branchId);
        if (authResult.Action != null) return authResult.Action;

        try
        {
            var item = await _quotationService.UpdateItemAsync(
                authResult.TenantId,
                companyId,
                branchId,
                id,
                itemId,
                request);

            return SuccessResponse(item, "Quotation item updated successfully.");
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpDelete("{id:guid}/items/{itemId:guid}")]
    [RequirePermission("SALES_QUOTATION.EDIT")]
    public async Task<IActionResult> RemoveItem(
        Guid companyId,
        Guid branchId,
        Guid id,
        Guid itemId)
    {
        var authResult = await ValidateScopeAccess(companyId, branchId);
        if (authResult.Action != null) return authResult.Action;

        try
        {
            await _quotationService.RemoveItemAsync(
                authResult.TenantId,
                companyId,
                branchId,
                id,
                itemId);

            return SuccessResponse<string?>(null, "Item removed from sales quotation successfully.");
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpPost("{id:guid}/submit")]
    [RequirePermission("SALES_QUOTATION.SUBMIT")]
    public async Task<IActionResult> SubmitQuotation(
        Guid companyId,
        Guid branchId,
        Guid id)
    {
        var authResult = await ValidateScopeAccess(companyId, branchId);
        if (authResult.Action != null) return authResult.Action;

        try
        {
            var quotation = await _quotationService.SubmitQuotationAsync(
                authResult.TenantId,
                companyId,
                branchId,
                id,
                _currentUserContext.UserId,
                User.Identity?.Name ?? _currentUserContext.UserId.ToString());

            return SuccessResponse(quotation, "Sales quotation submitted for approval successfully.");
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpPost("{id:guid}/approve")]
    [RequirePermission("SALES_QUOTATION.CONFIRM")]
    public async Task<IActionResult> ApproveQuotation(
        Guid companyId,
        Guid branchId,
        Guid id,
        [FromBody] SalesQuotationDecisionRequest? request)
    {
        var authResult = await ValidateScopeAccess(companyId, branchId);
        if (authResult.Action != null) return authResult.Action;

        try
        {
            var quotation = await _quotationService.ApproveQuotationAsync(
                authResult.TenantId,
                companyId,
                branchId,
                id,
                _currentUserContext.UserId,
                User.Identity?.Name ?? _currentUserContext.UserId.ToString(),
                request?.Remarks);

            return SuccessResponse(quotation, "Sales quotation approved successfully.");
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpPost("{id:guid}/reject")]
    [RequirePermission("SALES_QUOTATION.CONFIRM")]
    public async Task<IActionResult> RejectQuotation(
        Guid companyId,
        Guid branchId,
        Guid id,
        [FromBody] SalesQuotationDecisionRequest request)
    {
        var authResult = await ValidateScopeAccess(companyId, branchId);
        if (authResult.Action != null) return authResult.Action;

        try
        {
            var quotation = await _quotationService.RejectQuotationAsync(
                authResult.TenantId,
                companyId,
                branchId,
                id,
                _currentUserContext.UserId,
                User.Identity?.Name ?? _currentUserContext.UserId.ToString(),
                request?.Remarks ?? string.Empty);

            return SuccessResponse(quotation, "Sales quotation rejected successfully.");
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpPost("{id:guid}/cancel")]
    [RequirePermission("SALES_QUOTATION.CANCEL")]
    public async Task<IActionResult> CancelQuotation(
        Guid companyId,
        Guid branchId,
        Guid id,
        [FromBody] SalesQuotationDecisionRequest? request)
    {
        var authResult = await ValidateScopeAccess(companyId, branchId);
        if (authResult.Action != null) return authResult.Action;

        try
        {
            var quotation = await _quotationService.CancelQuotationAsync(
                authResult.TenantId,
                companyId,
                branchId,
                id,
                _currentUserContext.UserId,
                User.Identity?.Name ?? _currentUserContext.UserId.ToString(),
                request?.Remarks);

            return SuccessResponse(quotation, "Sales quotation cancelled successfully.");
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpPost("{id:guid}/return-to-draft")]
    [RequirePermission("SALES_QUOTATION.EDIT")]
    public async Task<IActionResult> ReturnToDraft(
        Guid companyId,
        Guid branchId,
        Guid id,
        [FromBody] ReturnToDraftRequest? request = null)
    {
        var authResult = await ValidateScopeAccess(companyId, branchId);
        if (authResult.Action != null) return authResult.Action;

        try
        {
            var quotation = await _quotationService.ReturnToDraftAsync(
                authResult.TenantId,
                companyId,
                branchId,
                id,
                _currentUserContext.UserId,
                User.Identity?.Name ?? _currentUserContext.UserId.ToString(),
                request?.Remarks);

            return SuccessResponse(quotation, "Sales quotation returned to Draft for offer preparation.");
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpPost("{id:guid}/send")]
    [RequirePermission("SALES_QUOTATION.EDIT")]
    public async Task<IActionResult> SendQuotation(
        Guid companyId,
        Guid branchId,
        Guid id,
        [FromQuery] bool retry = false)
    {
        var authResult = await ValidateScopeAccess(companyId, branchId);
        if (authResult.Action != null) return authResult.Action;

        try
        {
            var quotation = await _quotationService.SendQuotationAsync(
                authResult.TenantId,
                companyId,
                branchId,
                id,
                _currentUserContext.UserId,
                User.Identity?.Name ?? _currentUserContext.UserId.ToString(),
                retry);

            return SuccessResponse(quotation, "Sales quotation formally issued to customer successfully.");
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpGet("{id:guid}/approval-history")]
    public async Task<IActionResult> GetApprovalHistory(
        Guid companyId,
        Guid branchId,
        Guid id)
    {
        var authResult = await ValidateScopeAccess(companyId, branchId);
        if (authResult.Action != null) return authResult.Action;

        var history = await _quotationService.GetApprovalHistoryAsync(authResult.TenantId, id);
        return SuccessResponse(history);
    }

    private async Task<(IActionResult? Action, Guid TenantId)> ValidateScopeAccess(
        Guid companyId,
        Guid branchId)
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

        var canAccessCompany = await _userAccessService.CanAccessCompanyAsync(
            _currentUserContext.UserId,
            tenantId.Value,
            companyId);

        if (!canAccessCompany)
        {
            return (Forbid(), Guid.Empty);
        }

        var canAccessBranch = await _userAccessService.CanAccessBranchAsync(
            _currentUserContext.UserId,
            tenantId.Value,
            companyId,
            branchId);

        if (!canAccessBranch)
        {
            return (Forbid(), Guid.Empty);
        }

        return (null, tenantId.Value);
    }
}
