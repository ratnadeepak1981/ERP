using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using API.Common;
using API.Common.Sorting;
using API.Security.Authorization;
using Domain.Features.Procurement.Approval;
using Domain.Features.Procurement.PurchaseRequisition;
using Microsoft.AspNetCore.Mvc;
using Security.Interfaces;

namespace API.Features.Domain;

[ApiController]
[Route("api/companies/{companyId:guid}/branches/{branchId:guid}/purchase-requisitions")]
[RequirePermission("PURCHASE_REQUISITION.VIEW")]
public class PurchaseRequisitionController : BaseApiController
{
    private readonly IPurchaseRequisitionService _requisitionService;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IUserAccessService _userAccessService;

    private static readonly SortMapping<PurchaseRequisition> _sortMapping = new SortMapping<PurchaseRequisition>()
        .Add("requisitionNumber", r => r.RequisitionNumber, isDefault: true)
        .Add("requisitionDate", r => r.RequisitionDate)
        .Add("totalAmount", r => r.TotalAmount)
        .Add("status", r => r.Status)
        .SetSecondary(r => r.Id);

    public PurchaseRequisitionController(
        IPurchaseRequisitionService requisitionService,
        ICurrentUserContext currentUserContext,
        IUserAccessService userAccessService)
    {
        _requisitionService = requisitionService;
        _currentUserContext = currentUserContext;
        _userAccessService = userAccessService;
    }

    [HttpGet]
    public async Task<IActionResult> GetRequisitions(
        Guid companyId,
        Guid branchId,
        [FromQuery] int? page = null,
        [FromQuery] int? pageSize = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortDirection = null)
    {
        var authResult = await ValidateScopeAccess(companyId, branchId);
        if (authResult.Action != null) return authResult.Action;

        var requisitions = await _requisitionService.GetRequisitionsAsync(authResult.TenantId, companyId, branchId);
        return PaginateAndSort(requisitions, page, pageSize, sortBy, sortDirection, _sortMapping);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetRequisition(Guid companyId, Guid branchId, Guid id)
    {
        var authResult = await ValidateScopeAccess(companyId, branchId);
        if (authResult.Action != null) return authResult.Action;

        var requisition = await _requisitionService.GetRequisitionAsync(authResult.TenantId, companyId, branchId, id);
        if (requisition == null)
        {
            return NotFoundResponse("Purchase requisition not found in the authorized scope.");
        }

        return SuccessResponse(requisition);
    }

    [HttpPost]
    [RequirePermission("PURCHASE_REQUISITION.CREATE")]
    public async Task<IActionResult> CreateRequisition(
        Guid companyId,
        Guid branchId,
        [FromBody] CreatePurchaseRequisitionRequest request)
    {
        var authResult = await ValidateScopeAccess(companyId, branchId);
        if (authResult.Action != null) return authResult.Action;

        try
        {
            var requisition = await _requisitionService.CreateRequisitionAsync(
                authResult.TenantId,
                companyId,
                branchId,
                request,
                _currentUserContext.UserId,
                User.Identity?.Name ?? _currentUserContext.UserId.ToString());

            return CreatedResponse(
                nameof(GetRequisition),
                new { companyId, branchId, id = requisition.Id },
                requisition);
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpPost("{requisitionId:guid}/items")]
    [RequirePermission("PURCHASE_REQUISITION.EDIT")]
    public async Task<IActionResult> AddItem(
        Guid companyId,
        Guid branchId,
        Guid requisitionId,
        [FromBody] CreatePurchaseRequisitionItemRequest request)
    {
        var authResult = await ValidateScopeAccess(companyId, branchId);
        if (authResult.Action != null) return authResult.Action;

        try
        {
            var item = await _requisitionService.AddItemAsync(
                authResult.TenantId,
                companyId,
                branchId,
                requisitionId,
                request);

            return SuccessResponse(item, "Item added to purchase requisition successfully.");
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpPost("{requisitionId:guid}/submit")]
    [RequirePermission("PURCHASE_REQUISITION.SUBMIT")]
    public async Task<IActionResult> SubmitRequisition(
        Guid companyId,
        Guid branchId,
        Guid requisitionId)
    {
        var authResult = await ValidateScopeAccess(companyId, branchId);
        if (authResult.Action != null) return authResult.Action;

        try
        {
            var requisition = await _requisitionService.SubmitRequisitionAsync(
                authResult.TenantId,
                companyId,
                branchId,
                requisitionId,
                _currentUserContext.UserId,
                User.Identity?.Name ?? _currentUserContext.UserId.ToString());

            return SuccessResponse(requisition, "Purchase requisition submitted successfully.");
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpPost("{requisitionId:guid}/approve")]
    [RequirePermission("PURCHASE_REQUISITION.APPROVE")]
    public async Task<IActionResult> ApproveRequisition(
        Guid companyId,
        Guid branchId,
        Guid requisitionId,
        [FromBody] ApprovalDecisionRequest? request)
    {
        var authResult = await ValidateScopeAccess(companyId, branchId);
        if (authResult.Action != null) return authResult.Action;

        try
        {
            var requisition = await _requisitionService.ApproveRequisitionAsync(
                authResult.TenantId,
                companyId,
                branchId,
                requisitionId,
                _currentUserContext.UserId,
                User.Identity?.Name ?? _currentUserContext.UserId.ToString(),
                request?.Remarks);

            return SuccessResponse(requisition, "Purchase requisition approved successfully.");
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpPost("{requisitionId:guid}/reject")]
    [RequirePermission("PURCHASE_REQUISITION.APPROVE")]
    public async Task<IActionResult> RejectRequisition(
        Guid companyId,
        Guid branchId,
        Guid requisitionId,
        [FromBody] ApprovalDecisionRequest request)
    {
        var authResult = await ValidateScopeAccess(companyId, branchId);
        if (authResult.Action != null) return authResult.Action;

        try
        {
            var requisition = await _requisitionService.RejectRequisitionAsync(
                authResult.TenantId,
                companyId,
                branchId,
                requisitionId,
                _currentUserContext.UserId,
                User.Identity?.Name ?? _currentUserContext.UserId.ToString(),
                request?.Remarks ?? string.Empty);

            return SuccessResponse(requisition, "Purchase requisition rejected successfully.");
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpPost("{requisitionId:guid}/cancel")]
    [RequirePermission("PURCHASE_REQUISITION.CANCEL")]
    public async Task<IActionResult> CancelRequisition(
        Guid companyId,
        Guid branchId,
        Guid requisitionId,
        [FromBody] ApprovalDecisionRequest? request)
    {
        var authResult = await ValidateScopeAccess(companyId, branchId);
        if (authResult.Action != null) return authResult.Action;

        try
        {
            var requisition = await _requisitionService.CancelRequisitionAsync(
                authResult.TenantId,
                companyId,
                branchId,
                requisitionId,
                _currentUserContext.UserId,
                User.Identity?.Name ?? _currentUserContext.UserId.ToString(),
                request?.Remarks);

            return SuccessResponse(requisition, "Purchase requisition cancelled successfully.");
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpGet("{requisitionId:guid}/approval-history")]
    [RequirePermission("PURCHASE_REQUISITION.VIEW")]
    public async Task<IActionResult> GetApprovalHistory(
        Guid companyId,
        Guid branchId,
        Guid requisitionId)
    {
        var authResult = await ValidateScopeAccess(companyId, branchId);
        if (authResult.Action != null) return authResult.Action;

        try
        {
            var history = await _requisitionService.GetApprovalHistoryAsync(
                authResult.TenantId,
                companyId,
                branchId,
                requisitionId);

            return SuccessResponse(history);
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
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
