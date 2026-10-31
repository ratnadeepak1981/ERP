using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using Domain.Features.MasterData.Product;
using Domain.Features.MasterData.UnitOfMeasure;
using Domain.Features.Procurement.Approval;

namespace Domain.Features.Procurement.PurchaseRequisition;

public class PurchaseRequisitionService : IPurchaseRequisitionService
{
    private readonly IPurchaseRequisitionRepository _repository;
    private readonly IProductRepository _productRepository;
    private readonly IUnitOfMeasureRepository _uomRepository;
    private readonly IApprovalWorkflowService _approvalService;

    public PurchaseRequisitionService(
        IPurchaseRequisitionRepository repository,
        IProductRepository productRepository,
        IUnitOfMeasureRepository uomRepository,
        IApprovalWorkflowService approvalService)
    {
        _repository = repository;
        _productRepository = productRepository;
        _uomRepository = uomRepository;
        _approvalService = approvalService;
    }

    public async Task<List<PurchaseRequisition>> GetRequisitionsAsync(Guid tenantId, Guid companyId, Guid branchId)
    {
        return await _repository.GetByBranchAsync(tenantId, companyId, branchId);
    }

    public async Task<PurchaseRequisition?> GetRequisitionAsync(Guid tenantId, Guid companyId, Guid branchId, Guid requisitionId)
    {
        return await _repository.GetByIdAsync(tenantId, companyId, branchId, requisitionId);
    }

    public async Task<PurchaseRequisition> CreateRequisitionAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        CreatePurchaseRequisitionRequest request,
        Guid requestorUserId,
        string requestorUserName)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        if (string.IsNullOrWhiteSpace(request.RequisitionNumber))
            throw new ArgumentException("Requisition number is required.", nameof(request.RequisitionNumber));

        string reqNumber = request.RequisitionNumber.Trim();

        if (await _repository.ExistsRequisitionNumberAsync(tenantId, companyId, reqNumber))
        {
            throw new InvalidOperationException($"Purchase Requisition '{reqNumber}' already exists for this company.");
        }

        var req = PurchaseRequisition.Create(
            tenantId,
            companyId,
            branchId,
            reqNumber,
            requestorUserId,
            request.RequisitionDate,
            request.RequiredDate,
            request.Notes);

        if (request.Items != null && request.Items.Count > 0)
        {
            foreach (var itemReq in request.Items)
            {
                var product = await _productRepository.GetByIdAsync(tenantId, itemReq.ProductId);
                if (product == null)
                {
                    throw new InvalidOperationException($"Product '{itemReq.ProductId}' was not found or does not belong to this tenant.");
                }

                if (!product.IsPurchasable)
                {
                    throw new InvalidOperationException($"Product '{product.ProductCode}' is not marked as purchasable.");
                }

                Guid? uomId = itemReq.UnitOfMeasureId ?? product.UnitOfMeasureId;
                if (uomId.HasValue)
                {
                    var uom = await _uomRepository.GetUnitByIdAsync(tenantId, uomId.Value);
                    if (uom == null)
                    {
                        throw new InvalidOperationException($"Unit of measure '{uomId.Value}' was not found or does not belong to this tenant.");
                    }
                }

                req.AddItem(itemReq.ProductId, itemReq.Quantity, itemReq.EstimatedUnitPrice, uomId, itemReq.Remarks);
            }
        }

        try
        {
            await _repository.AddAsync(req);
            await _repository.SaveChangesAsync();
            return req;
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException ex)
        {
            var msg = ex.InnerException?.Message ?? ex.Message;
            if (msg.Contains("IX_PurchaseRequisitions_TenantId_CompanyId_RequisitionNumber") ||
                msg.Contains("UNIQUE KEY") ||
                msg.Contains("duplicate key"))
            {
                throw new InvalidOperationException($"Purchase Requisition '{reqNumber}' already exists for this company.", ex);
            }

            throw;
        }
    }

    public async Task<PurchaseRequisitionItem> AddItemAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid requisitionId,
        CreatePurchaseRequisitionItemRequest request)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        var req = await _repository.GetByIdAsync(tenantId, companyId, branchId, requisitionId)
            ?? throw new KeyNotFoundException("Purchase requisition was not found in the authorized branch/company scope.");

        var product = await _productRepository.GetByIdAsync(tenantId, request.ProductId);
        if (product == null)
        {
            throw new InvalidOperationException($"Product '{request.ProductId}' was not found or does not belong to this tenant.");
        }

        if (!product.IsPurchasable)
        {
            throw new InvalidOperationException($"Product '{product.ProductCode}' is not marked as purchasable.");
        }

        Guid? uomId = request.UnitOfMeasureId ?? product.UnitOfMeasureId;
        if (uomId.HasValue)
        {
            var uom = await _uomRepository.GetUnitByIdAsync(tenantId, uomId.Value);
            if (uom == null)
            {
                throw new InvalidOperationException($"Unit of measure '{uomId.Value}' was not found or does not belong to this tenant.");
            }
        }

        req.AddItem(request.ProductId, request.Quantity, request.EstimatedUnitPrice, uomId, request.Remarks);

        await _repository.SaveChangesAsync();

        return req.Items[^1];
    }

    public async Task<PurchaseRequisition> SubmitRequisitionAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid requisitionId,
        Guid actorUserId,
        string actorUserName)
    {
        var req = await _repository.GetByIdAsync(tenantId, companyId, branchId, requisitionId)
            ?? throw new KeyNotFoundException("Purchase requisition not found in authorized scope.");

        var settings = await _approvalService.GetApprovalSettingsAsync(tenantId);

        if (settings.ApprovalRequired)
        {
            req.Submit();
            await _repository.SaveChangesAsync();

            await _approvalService.RecordSubmissionAsync(
                tenantId,
                "PurchaseRequisition",
                req.Id,
                actorUserId,
                actorUserName,
                req.TotalAmount);
        }
        else
        {
            // Simple Approval disabled: automatically approve
            req.AutoApprove();
            await _repository.SaveChangesAsync();

            await _approvalService.RecordAutoApprovalAsync(
                tenantId,
                "PurchaseRequisition",
                req.Id,
                actorUserId,
                actorUserName,
                req.TotalAmount);
        }

        return req;
    }

    public async Task<PurchaseRequisition> ApproveRequisitionAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid requisitionId,
        Guid approverUserId,
        string approverUserName,
        string? remarks = null)
    {
        var req = await _repository.GetByIdAsync(tenantId, companyId, branchId, requisitionId)
            ?? throw new KeyNotFoundException("Purchase requisition not found in authorized scope.");

        // Guard against self-approval
        await _approvalService.RecordApprovalAsync(
            tenantId,
            "PurchaseRequisition",
            req.Id,
            approverUserId,
            approverUserName,
            req.RequestorUserId,
            req.TotalAmount,
            remarks);

        req.Approve();
        await _repository.SaveChangesAsync();

        return req;
    }

    public async Task<PurchaseRequisition> RejectRequisitionAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid requisitionId,
        Guid approverUserId,
        string approverUserName,
        string remarks)
    {
        var req = await _repository.GetByIdAsync(tenantId, companyId, branchId, requisitionId)
            ?? throw new KeyNotFoundException("Purchase requisition not found in authorized scope.");

        await _approvalService.RecordRejectionAsync(
            tenantId,
            "PurchaseRequisition",
            req.Id,
            approverUserId,
            approverUserName,
            remarks,
            req.TotalAmount);

        req.Reject();
        await _repository.SaveChangesAsync();

        return req;
    }

    public async Task<PurchaseRequisition> CancelRequisitionAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid requisitionId,
        Guid actorUserId,
        string actorUserName,
        string? remarks = null)
    {
        var req = await _repository.GetByIdAsync(tenantId, companyId, branchId, requisitionId)
            ?? throw new KeyNotFoundException("Purchase requisition not found in authorized scope.");

        req.Cancel();
        await _repository.SaveChangesAsync();

        await _approvalService.RecordCancellationAsync(
            tenantId,
            "PurchaseRequisition",
            req.Id,
            actorUserId,
            actorUserName,
            req.TotalAmount,
            remarks);

        return req;
    }

// New CRUD operations
    public async Task<PurchaseRequisition> UpdateRequisitionAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid requisitionId,
        UpdatePurchaseRequisitionRequest request)
    {
        var req = await _repository.GetByIdAsync(tenantId, companyId, branchId, requisitionId)
            ?? throw new KeyNotFoundException("Purchase requisition not found in authorized scope.");

        // Allowed statuses: Draft, Rejected
        if (req.Status != PurchaseRequisitionStatus.Draft && req.Status != PurchaseRequisitionStatus.Rejected)
            throw new InvalidOperationException($"Only Draft or Rejected requisitions can be updated. Current status: {req.Status}.");

        // Update mutable fields
        if (request.RequisitionDate.HasValue)
            req.RequisitionDate = request.RequisitionDate.Value;
        if (request.RequiredDate.HasValue)
            req.RequiredDate = request.RequiredDate.Value;
        if (request.Notes != null)
            req.Notes = request.Notes?.Trim();

        await _repository.SaveChangesAsync();
        return req;
    }

    public async Task<PurchaseRequisitionItem> UpdateItemAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid requisitionId,
        Guid itemId,
        UpdatePurchaseRequisitionItemRequest request)
    {
        var req = await _repository.GetByIdAsync(tenantId, companyId, branchId, requisitionId)
            ?? throw new KeyNotFoundException("Purchase requisition not found in authorized scope.");

        if (req.Status != PurchaseRequisitionStatus.Draft && req.Status != PurchaseRequisitionStatus.Rejected)
            throw new InvalidOperationException($"Only Draft or Rejected requisitions can have line items updated. Current status: {req.Status}.");

        var item = req.Items.FirstOrDefault(i => i.Id == itemId && i.IsActive)
            ?? throw new KeyNotFoundException("Purchase requisition line item not found or is inactive.");

        // Validate product & UOM similar to AddItem
        var product = await _productRepository.GetByIdAsync(tenantId, request.ProductId)
            ?? throw new InvalidOperationException($"Product '{request.ProductId}' was not found or does not belong to this tenant.");
        if (!product.IsPurchasable)
            throw new InvalidOperationException($"Product '{product.ProductCode}' is not marked as purchasable.");

        Guid? uomId = request.UnitOfMeasureId ?? product.UnitOfMeasureId;
        if (uomId.HasValue)
        {
            var uom = await _uomRepository.GetUnitByIdAsync(tenantId, uomId.Value);
            if (uom == null)
                throw new InvalidOperationException($"Unit of measure '{uomId.Value}' was not found or does not belong to this tenant.");
        }

        // Apply updates
        item.ProductId = request.ProductId;
        item.UnitOfMeasureId = uomId;
        item.Quantity = Math.Round(request.Quantity, 4, MidpointRounding.AwayFromZero);
        item.EstimatedUnitPrice = Math.Round(request.EstimatedUnitPrice ?? 0m, 2, MidpointRounding.AwayFromZero);
        item.EstimatedLineTotal = Math.Round(item.Quantity * item.EstimatedUnitPrice, 2, MidpointRounding.AwayFromZero);
        item.Remarks = request.Remarks?.Trim();

        // Recalculate total on parent
        req.RecalculateTotal();
        await _repository.SaveChangesAsync();
        return item;
    }

    public async Task DeleteItemAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid requisitionId,
        Guid itemId)
    {
        var req = await _repository.GetByIdAsync(tenantId, companyId, branchId, requisitionId)
            ?? throw new KeyNotFoundException("Purchase requisition not found in authorized scope.");

        if (req.Status != PurchaseRequisitionStatus.Draft && req.Status != PurchaseRequisitionStatus.Rejected)
            throw new InvalidOperationException($"Only Draft or Rejected requisitions can have line items deleted. Current status: {req.Status}.");

        var item = req.Items.FirstOrDefault(i => i.Id == itemId && i.IsActive)
            ?? throw new KeyNotFoundException("Purchase requisition line item not found or already inactive.");

        // Soft delete line
        item.IsActive = false;
        req.RecalculateTotal();
        await _repository.SaveChangesAsync();
    }

    public async Task DeleteRequisitionAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid requisitionId)
    {
        var req = await _repository.GetByIdAsync(tenantId, companyId, branchId, requisitionId)
            ?? throw new KeyNotFoundException("Purchase requisition not found in authorized scope.");

        // Only Draft can be deleted
        if (req.Status != PurchaseRequisitionStatus.Draft)
            throw new InvalidOperationException($"Only Draft requisitions can be deleted. Current status: {req.Status}.");

        // Soft delete
        req.IsActive = false;
        foreach (var item in req.Items)
        {
            item.IsActive = false;
        }
        await _repository.SaveChangesAsync();
    }

    // Existing method continues
    public async Task<List<ApprovalAuditRecord>> GetApprovalHistoryAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid requisitionId)
    {
        var req = await _repository.GetByIdAsync(tenantId, companyId, branchId, requisitionId)
            ?? throw new KeyNotFoundException("Purchase requisition not found in authorized scope.");

        return await _approvalService.GetApprovalHistoryAsync(
            tenantId,
            "PurchaseRequisition",
            req.Id);
    }
}
