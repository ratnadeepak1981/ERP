using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using API.Common;
using API.Common.Sorting;
using API.Security.Authorization;
using Domain.Features.Inventory.Putaway;
using Microsoft.AspNetCore.Mvc;
using Security.Interfaces;

namespace API.Features.Domain;

[ApiController]
[Route("api/companies/{companyId:guid}/branches/{branchId:guid}/putaways")]
[RequirePermission("INVENTORY.VIEW")]
public class PutawayController : BaseApiController
{
    private readonly IPutawayService _putawayService;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IUserAccessService _userAccessService;

    private static readonly SortMapping<PutawayDto> _sortMapping = new SortMapping<PutawayDto>()
        .Add("putawayNumber", p => p.PutawayNumber, isDefault: true)
        .Add("putawayDate", p => p.PutawayDate)
        .Add("status", p => p.Status)
        .Add("totalQuantity", p => p.TotalQuantity)
        .SetSecondary(p => p.PutawayId);

    public PutawayController(
        IPutawayService putawayService,
        ICurrentUserContext currentUserContext,
        IUserAccessService userAccessService)
    {
        _putawayService = putawayService;
        _currentUserContext = currentUserContext;
        _userAccessService = userAccessService;
    }

    [HttpGet]
    public async Task<IActionResult> GetPutaways(
        Guid companyId,
        Guid branchId,
        [FromQuery] Guid? warehouseId = null,
        [FromQuery] PutawayStatus? status = null,
        [FromQuery] int? page = null,
        [FromQuery] int? pageSize = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortDirection = null)
    {
        var authResult = await ValidateScopeAccess(companyId, branchId);
        if (authResult.Action != null) return authResult.Action;

        var list = await _putawayService.GetPutawaysAsync(authResult.TenantId, companyId, branchId, warehouseId, status);
        return PaginateAndSort(list, page, pageSize, sortBy, sortDirection, _sortMapping);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetPutaway(Guid companyId, Guid branchId, Guid id)
    {
        var authResult = await ValidateScopeAccess(companyId, branchId);
        if (authResult.Action != null) return authResult.Action;

        var putaway = await _putawayService.GetPutawayByIdAsync(authResult.TenantId, companyId, branchId, id);
        if (putaway == null)
        {
            return NotFoundResponse("Putaway transaction not found in the authorized scope.");
        }

        return SuccessResponse(putaway);
    }

    [HttpGet("eligible-receipts")]
    public async Task<IActionResult> GetEligibleReceipts(
        Guid companyId,
        Guid branchId,
        [FromQuery] Guid? warehouseId = null)
    {
        var authResult = await ValidateScopeAccess(companyId, branchId);
        if (authResult.Action != null) return authResult.Action;

        var list = await _putawayService.GetEligibleSourceReceiptsAsync(authResult.TenantId, companyId, branchId, warehouseId);
        return SuccessResponse(list);
    }

    [HttpGet("eligible-receipts/{sourceType}/{sourceId:guid}/lines")]
    public async Task<IActionResult> GetEligibleReceiptLines(
        Guid companyId,
        Guid branchId,
        PutawaySourceDocumentType sourceType,
        Guid sourceId)
    {
        var authResult = await ValidateScopeAccess(companyId, branchId);
        if (authResult.Action != null) return authResult.Action;

        var lines = await _putawayService.GetEligibleReceiptLinesAsync(authResult.TenantId, companyId, branchId, sourceType, sourceId);
        return SuccessResponse(lines);
    }

    [HttpPost]
    [RequirePermission("PUTAWAY.POST")]
    public async Task<IActionResult> CreateDraft(
        Guid companyId,
        Guid branchId,
        [FromBody] CreatePutawayDraftRequest request)
    {
        var authResult = await ValidateScopeAccess(companyId, branchId);
        if (authResult.Action != null) return authResult.Action;

        try
        {
            var result = await _putawayService.CreateDraftAsync(authResult.TenantId, companyId, branchId, request);
            return CreatedResponse(
                nameof(GetPutaway),
                new { companyId, branchId, id = result.PutawayId },
                result);
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpPut("{id:guid}")]
    [RequirePermission("PUTAWAY.POST")]
    public async Task<IActionResult> UpdateDraft(
        Guid companyId,
        Guid branchId,
        Guid id,
        [FromBody] UpdatePutawayDraftRequest request)
    {
        var authResult = await ValidateScopeAccess(companyId, branchId);
        if (authResult.Action != null) return authResult.Action;

        try
        {
            var result = await _putawayService.UpdateDraftAsync(authResult.TenantId, companyId, branchId, id, request);
            return SuccessResponse(result, "Putaway draft updated successfully.");
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpPost("{id:guid}/confirm")]
    [RequirePermission("PUTAWAY.POST")]
    public async Task<IActionResult> ConfirmPutaway(Guid companyId, Guid branchId, Guid id)
    {
        var authResult = await ValidateScopeAccess(companyId, branchId);
        if (authResult.Action != null) return authResult.Action;

        try
        {
            var result = await _putawayService.ConfirmPutawayAsync(authResult.TenantId, companyId, branchId, id);
            return SuccessResponse(result, "Putaway confirmed and inventory posted successfully.");
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpPost("{id:guid}/cancel")]
    [RequirePermission("PUTAWAY.POST")]
    public async Task<IActionResult> CancelPutaway(Guid companyId, Guid branchId, Guid id)
    {
        var authResult = await ValidateScopeAccess(companyId, branchId);
        if (authResult.Action != null) return authResult.Action;

        try
        {
            var result = await _putawayService.CancelDraftAsync(authResult.TenantId, companyId, branchId, id);
            return SuccessResponse(result, "Putaway cancelled successfully.");
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
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
