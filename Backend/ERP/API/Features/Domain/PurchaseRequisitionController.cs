using System;
using System.Threading.Tasks;
using API.Security.Authorization;
using Domain.Features.Procurement.Approval;
using Domain.Features.Procurement.PurchaseRequisition;
using Microsoft.AspNetCore.Mvc;
using Security.Interfaces;

namespace API.Features.Domain;

[ApiController]
[Route("api/companies/{companyId:guid}/branches/{branchId:guid}/purchase-requisitions")]
[RequirePermission("PURCHASE_REQUISITION.VIEW")]
public class PurchaseRequisitionController : ControllerBase
{
    private readonly IPurchaseRequisitionService _requisitionService;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IUserAccessService _userAccessService;

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
    public async Task<IActionResult> GetRequisitions(Guid companyId, Guid branchId)
    {
        var authResult = await ValidateScopeAccess(companyId, branchId);
        if (authResult.Action != null) return authResult.Action;

        var requisitions = await _requisitionService.GetRequisitionsAsync(authResult.TenantId, companyId, branchId);
        return Ok(requisitions);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetRequisition(Guid companyId, Guid branchId, Guid id)
    {
        var authResult = await ValidateScopeAccess(companyId, branchId);
        if (authResult.Action != null) return authResult.Action;

        var requisition = await _requisitionService.GetRequisitionAsync(authResult.TenantId, companyId, branchId, id);
        if (requisition == null)
        {
            return NotFound(new { status = "FAIL", message = "Purchase requisition not found in the authorized scope." });
        }

        return Ok(requisition);
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

            return CreatedAtAction(
                nameof(GetRequisition),
                new { companyId, branchId, id = requisition.Id },
                requisition);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { status = "FAIL", message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { status = "FAIL", message = ex.Message });
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

            return Ok(item);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { status = "FAIL", message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { status = "FAIL", message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { status = "FAIL", message = ex.Message });
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

            return Ok(requisition);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { status = "FAIL", message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { status = "FAIL", message = ex.Message });
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

            return Ok(requisition);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { status = "FAIL", message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { status = "FAIL", message = ex.Message });
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

            return Ok(requisition);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { status = "FAIL", message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { status = "FAIL", message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { status = "FAIL", message = ex.Message });
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

            return Ok(requisition);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { status = "FAIL", message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { status = "FAIL", message = ex.Message });
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

            return Ok(history);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { status = "FAIL", message = ex.Message });
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
            return (Unauthorized(new
            {
                status = "FAIL",
                message = "Tenant identity is missing or invalid."
            }), Guid.Empty);
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
