using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using API.Security.Authorization;
using Domain.Features.Procurement.GoodsReceiptNote;
using Microsoft.AspNetCore.Mvc;
using Security.Interfaces;

namespace API.Features.Domain;

[ApiController]
[Route("api/companies/{companyId:guid}/branches/{branchId:guid}/goods-receipt-notes")]
[RequirePermission("GRN.VIEW")]
public class GoodsReceiptNoteController : ControllerBase
{
    private readonly IGoodsReceiptNoteService _grnService;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IUserAccessService _userAccessService;

    public GoodsReceiptNoteController(
        IGoodsReceiptNoteService grnService,
        ICurrentUserContext currentUserContext,
        IUserAccessService userAccessService)
    {
        _grnService = grnService;
        _currentUserContext = currentUserContext;
        _userAccessService = userAccessService;
    }

    [HttpGet]
    public async Task<IActionResult> GetGrns(Guid companyId, Guid branchId)
    {
        var authResult = await ValidateScopeAccess(companyId, branchId);
        if (authResult.Action != null) return authResult.Action;

        var list = await _grnService.GetGrnsAsync(authResult.TenantId, companyId, branchId);
        return Ok(list);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetGrn(Guid companyId, Guid branchId, Guid id)
    {
        var authResult = await ValidateScopeAccess(companyId, branchId);
        if (authResult.Action != null) return authResult.Action;

        var grn = await _grnService.GetGrnByIdAsync(authResult.TenantId, companyId, branchId, id);
        if (grn == null)
        {
            return NotFound(new
            {
                status = "FAIL",
                message = "Goods Receipt Note not found in the authorized scope."
            });
        }

        return Ok(grn);
    }

    [HttpPost("from-po/{purchaseOrderId:guid}")]
    [RequirePermission("GRN.CREATE")]
    public async Task<IActionResult> CreateDraftFromPo(
        Guid companyId,
        Guid branchId,
        Guid purchaseOrderId,
        [FromQuery] string grnNumber,
        [FromQuery] Guid? warehouseId = null,
        [FromQuery] Guid? warehouseLocationId = null,
        [FromQuery] string? remarks = null)
    {
        var authResult = await ValidateScopeAccess(companyId, branchId);
        if (authResult.Action != null) return authResult.Action;

        try
        {
            var grn = await _grnService.CreateDraftFromPoAsync(
                authResult.TenantId,
                companyId,
                branchId,
                purchaseOrderId,
                grnNumber,
                warehouseId,
                warehouseLocationId,
                remarks);

            return CreatedAtAction(
                nameof(GetGrn),
                new { companyId, branchId, id = grn.GoodsReceiptNoteId },
                grn);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { status = "FAIL", message = ex.Message });
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

    [HttpPost]
    [RequirePermission("GRN.CREATE")]
    public async Task<IActionResult> CreateManualDraft(
        Guid companyId,
        Guid branchId,
        [FromBody] CreateGoodsReceiptNoteRequest request)
    {
        var authResult = await ValidateScopeAccess(companyId, branchId);
        if (authResult.Action != null) return authResult.Action;

        try
        {
            var grn = await _grnService.CreateManualDraftAsync(
                authResult.TenantId,
                companyId,
                branchId,
                request);

            return CreatedAtAction(
                nameof(GetGrn),
                new { companyId, branchId, id = grn.GoodsReceiptNoteId },
                grn);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { status = "FAIL", message = ex.Message });
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

    [HttpPut("{id:guid}")]
    [RequirePermission("GRN.EDIT")]
    public async Task<IActionResult> UpdateDraft(
        Guid companyId,
        Guid branchId,
        Guid id,
        [FromBody] UpdateGoodsReceiptNoteRequest request)
    {
        var authResult = await ValidateScopeAccess(companyId, branchId);
        if (authResult.Action != null) return authResult.Action;

        try
        {
            var grn = await _grnService.UpdateDraftAsync(
                authResult.TenantId,
                companyId,
                branchId,
                id,
                request);

            return Ok(grn);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { status = "FAIL", message = ex.Message });
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

    [HttpPost("{id:guid}/cancel")]
    [RequirePermission("GRN.CANCEL")]
    public async Task<IActionResult> CancelDraft(Guid companyId, Guid branchId, Guid id)
    {
        var authResult = await ValidateScopeAccess(companyId, branchId);
        if (authResult.Action != null) return authResult.Action;

        try
        {
            var grn = await _grnService.CancelDraftAsync(authResult.TenantId, companyId, branchId, id);
            return Ok(grn);
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

    [HttpPost("{id:guid}/confirm")]
    [RequirePermission("GRN.CONFIRM")]
    public async Task<IActionResult> ConfirmGrn(Guid companyId, Guid branchId, Guid id)
    {
        var authResult = await ValidateScopeAccess(companyId, branchId);
        if (authResult.Action != null) return authResult.Action;

        try
        {
            var grn = await _grnService.ConfirmGrnAsync(authResult.TenantId, companyId, branchId, id);
            return Ok(grn);
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

    private async Task<(IActionResult? Action, Guid TenantId)> ValidateScopeAccess(Guid companyId, Guid branchId)
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
