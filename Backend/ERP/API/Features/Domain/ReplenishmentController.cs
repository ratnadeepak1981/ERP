using System;
using System.Threading.Tasks;
using API.Common;
using API.Security.Authorization;
using ERP.Application.Features.Inventory.Replenishment;
using Microsoft.AspNetCore.Mvc;
using Security.Interfaces;

namespace API.Features.Domain;

/// <summary>
/// Centralised API controller for inventory replenishment assessments and authentic Draft Purchase Requisition generation.
/// Strictly enforces tenant, company and branch scope boundaries.
/// </summary>
[ApiController]
[Route("api/companies/{companyId:guid}/branches/{branchId:guid}/inventory/replenishment")]
public class ReplenishmentController : BaseApiController
{
    private readonly IInventoryReplenishmentEngine _replenishmentEngine;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IUserAccessService _userAccessService;

    public ReplenishmentController(
        IInventoryReplenishmentEngine replenishmentEngine,
        ICurrentUserContext currentUserContext,
        IUserAccessService userAccessService)
    {
        _replenishmentEngine = replenishmentEngine ?? throw new ArgumentNullException(nameof(replenishmentEngine));
        _currentUserContext = currentUserContext ?? throw new ArgumentNullException(nameof(currentUserContext));
        _userAccessService = userAccessService ?? throw new ArgumentNullException(nameof(userAccessService));
    }

    /// <summary>
    /// Evaluates eligible on-hand stock, active allocations, quarantine, expired batches,
    /// and approved open purchase order supply to calculate line shortages.
    /// Requires INVENTORY.REPLENISH.CREATE permission.
    /// </summary>
    [HttpPost("assess")]
    [RequirePermission("INVENTORY.REPLENISH.CREATE")]
    public async Task<IActionResult> AssessStock(
        Guid companyId,
        Guid branchId,
        [FromBody] ReplenishmentAssessmentRequest request)
    {
        var authResult = await ValidateScopeAccess(companyId, branchId);
        if (authResult.Action != null) return authResult.Action;

        if (request == null)
        {
            return BadRequestResponse("Assessment request body cannot be null.");
        }

        if (request.WarehouseId == Guid.Empty)
        {
            return BadRequestResponse("A valid WarehouseId is required for replenishment assessment.");
        }

        try
        {
            var result = await _replenishmentEngine.AssessStockAsync(
                authResult.TenantId,
                companyId,
                branchId,
                request);

            return SuccessResponse(result);
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    /// <summary>
    /// Generates a genuine persistent Draft Purchase Requisition in the Procurement module
    /// for identified item shortages. Does not allocate stock or create supplier commitments.
    /// </summary>
    [HttpPost("draft-purchase-requisition")]
    [RequirePermission("INVENTORY.REPLENISH.CREATE")]
    public async Task<IActionResult> CreateDraftPurchaseRequisition(
        Guid companyId,
        Guid branchId,
        [FromBody] CreateReplenishmentPrRequest request)
    {
        var authResult = await ValidateScopeAccess(companyId, branchId);
        if (authResult.Action != null) return authResult.Action;

        if (request == null)
        {
            return BadRequestResponse("Replenishment request body cannot be null.");
        }

        if (request.Items == null || request.Items.Count == 0)
        {
            return BadRequestResponse("At least one line item is required to create a replenishment draft requisition.");
        }

        // Support idempotency key supplied via HTTP header if not in request body
        if (Request.Headers.TryGetValue("X-Idempotency-Key", out var headerKey) && !string.IsNullOrWhiteSpace(headerKey))
        {
            if (string.IsNullOrWhiteSpace(request.IdempotencyKey))
            {
                request.IdempotencyKey = headerKey.ToString().Trim();
            }
        }

        try
        {
            var actorUserId = _currentUserContext.UserId;
            var actorUserName = User.Identity?.Name ?? actorUserId.ToString();

            var result = await _replenishmentEngine.CreateDraftPurchaseRequisitionAsync(
                authResult.TenantId,
                companyId,
                branchId,
                request,
                actorUserId,
                actorUserName);

            return SuccessResponse(result);
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
