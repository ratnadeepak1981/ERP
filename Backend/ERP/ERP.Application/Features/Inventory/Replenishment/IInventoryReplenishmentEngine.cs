using System;
using System.Threading.Tasks;

namespace ERP.Application.Features.Inventory.Replenishment;

/// <summary>
/// Authoritative shared inventory replenishment engine for ApexERP.
/// Consolidates stock shortage calculations, multi-scope isolation,
/// batch expiry/quarantine exclusion, and open PO supply resolution
/// to prepare authentic Draft Purchase Requisitions.
/// </summary>
public interface IInventoryReplenishmentEngine
{
    /// <summary>
    /// Evaluates stock availability, allocations, quarantine, expired batches,
    /// and open PO supply for one or more items within a company and warehouse scope.
    /// </summary>
    Task<ReplenishmentAssessmentResult> AssessStockAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        ReplenishmentAssessmentRequest request);

    /// <summary>
    /// Evaluates stock shortages and generates a genuine persistent Draft Purchase Requisition
    /// via IPurchaseRequisitionService within the authorized company and branch scope.
    /// </summary>
    Task<DraftPurchaseRequisitionResult> CreateDraftPurchaseRequisitionAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        CreateReplenishmentPrRequest request,
        Guid actorUserId,
        string actorUserName);
}
