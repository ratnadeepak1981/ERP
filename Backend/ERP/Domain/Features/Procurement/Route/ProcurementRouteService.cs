using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Domain.Features.Procurement.PurchaseOrder;
using Domain.Features.Procurement.PurchaseRequisition;
using Domain.Features.Procurement.RequestForQuotation;
using RfqEntity = Domain.Features.Procurement.RequestForQuotation.RequestForQuotation;
using Domain.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Auditing;
using Microsoft.EntityFrameworkCore;

namespace Domain.Features.Procurement.Route;

public class ProcurementRouteService : IProcurementRouteService
{
    private readonly DomainDbContext _dbContext;
    private readonly IRFQService _rfqService;
    private static readonly SemaphoreSlim _routeLock = new(1, 1);

    public ProcurementRouteService(
        DomainDbContext dbContext,
        IRFQService rfqService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _rfqService = rfqService ?? throw new ArgumentNullException(nameof(rfqService));
    }

    public async Task<ProcurementRouteStatusDto> GetRouteStatusAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid requisitionId,
        CancellationToken ct = default)
    {
        var req = await _dbContext.PurchaseRequisitions
            .Include(r => r.Items)
            .AsNoTracking()
            .FirstOrDefaultAsync(r =>
                r.TenantId == tenantId &&
                r.CompanyId == companyId &&
                r.BranchId == branchId &&
                r.Id == requisitionId, ct);

        if (req == null)
            throw new KeyNotFoundException($"Purchase requisition '{requisitionId}' not found in the authorized scope.");

        decimal totalRequested = req.Items?.Where(i => i.IsActive).Sum(i => i.Quantity) ?? 0m;

        // 1. Find linked Purchase Orders
        var activeOrders = await _dbContext.PurchaseOrders
            .Include(o => o.Items)
            .AsNoTracking()
            .Where(o =>
                o.TenantId == tenantId &&
                o.CompanyId == companyId &&
                o.BranchId == branchId &&
                o.Status != PurchaseOrderStatus.Cancelled &&
                o.Status != PurchaseOrderStatus.Rejected)
            .ToListAsync(ct);

        var linkedOrders = new List<LinkedPoDto>();
        decimal totalOrdered = 0m;
        string reqTag = $"[PR: {req.RequisitionNumber}".ToLowerInvariant();

        foreach (var ord in activeOrders)
        {
            bool isLinked = false;
            foreach (var item in ord.Items)
            {
                var notes = (item.Notes ?? string.Empty).ToLowerInvariant();
                bool matchesPr = notes.Contains(reqTag) ||
                                (req.Items != null && req.Items.Any(pi => notes.Contains($"line: {pi.Id}".ToLowerInvariant()) || notes.Contains($"line:{pi.Id}".ToLowerInvariant())));

                if (matchesPr)
                {
                    totalOrdered += item.Quantity;
                    isLinked = true;
                }
            }

            if (isLinked)
            {
                linkedOrders.Add(new LinkedPoDto
                {
                    Id = ord.Id,
                    OrderNumber = ord.OrderNumber,
                    Status = ord.Status.ToString(),
                    TotalAmount = ord.TotalAmount,
                    OrderDate = ord.OrderDate
                });
            }
        }

        decimal totalRemaining = Math.Max(0m, totalRequested - totalOrdered);

        // 2. Find linked RFQs
        var allRfqs = await _dbContext.RequestForQuotations
            .Include(r => r.Lines)
            .AsNoTracking()
            .Where(r =>
                r.TenantId == tenantId &&
                r.CompanyId == companyId &&
                r.BranchId == branchId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);

        RfqEntity? linkedRfq = null;
        foreach (var rfq in allRfqs)
        {
            var instructions = (rfq.Instructions ?? string.Empty).ToLowerInvariant();
            var title = (rfq.Title ?? string.Empty).ToLowerInvariant();
            bool matchesPr = instructions.Contains(reqTag) ||
                             title.Contains(req.RequisitionNumber.ToLowerInvariant()) ||
                             rfq.Lines.Any(l => (l.Notes ?? string.Empty).ToLowerInvariant().Contains(reqTag));

            if (matchesPr)
            {
                linkedRfq = rfq;
                break;
            }
        }

        var statusDto = new ProcurementRouteStatusDto
        {
            RequisitionId = req.Id,
            RequisitionNumber = req.RequisitionNumber,
            RequisitionStatus = req.Status.ToString(),
            TotalRequestedQuantity = totalRequested,
            TotalOrderedQuantity = totalOrdered,
            TotalRemainingQuantity = totalRemaining,
            LinkedPurchaseOrders = linkedOrders
        };

        if (linkedRfq != null)
        {
            statusDto.LinkedRfq = new LinkedRfqDto
            {
                Id = linkedRfq.Id,
                RFQNumber = linkedRfq.RFQNumber ?? string.Empty,
                Title = linkedRfq.Title ?? string.Empty,
                Status = linkedRfq.Status.ToString(),
                ClosingDate = linkedRfq.ClosingDate,
                LineCount = linkedRfq.Lines?.Count ?? 0
            };
        }

        // 3. Determine Route & Exclusivity
        if (req.Status != PurchaseRequisitionStatus.Approved)
        {
            statusDto.SelectedRoute = ProcurementRouteType.None;
            statusDto.CanCreateDirectPO = false;
            statusDto.CanCreateRFQ = false;
            statusDto.CanResetRoute = false;
            statusDto.BlockReason = $"Purchase Requisition is in '{req.Status}' status. Procurement routes can only be initiated once the requisition is Approved.";
            return statusDto;
        }

        bool hasActiveRfq = linkedRfq != null && linkedRfq.Status != RequestForQuotationStatus.Cancelled;
        bool hasActivePo = linkedOrders.Count > 0;

        if (hasActiveRfq)
        {
            statusDto.SelectedRoute = ProcurementRouteType.RFQ;
            statusDto.CanCreateRFQ = false;
            statusDto.CanCreateDirectPO = false; // Exclusivity: PR is routed to RFQ
            statusDto.CanResetRoute = false;
            statusDto.BlockReason = $"Procurement requirement is currently routed to Request for Quotation '{linkedRfq!.RFQNumber}' (Status: {linkedRfq.Status}). Direct Purchase Order creation is prohibited by procurement route exclusivity while an active RFQ exists.";
        }
        else if (hasActivePo)
        {
            statusDto.SelectedRoute = ProcurementRouteType.DirectPO;
            statusDto.CanCreateRFQ = false; // Exclusivity: PR is committed to Direct PO route
            statusDto.CanCreateDirectPO = totalRemaining > 0;
            statusDto.CanResetRoute = false;
            if (totalRemaining <= 0)
            {
                statusDto.BlockReason = $"All items from Requisition '{req.RequisitionNumber}' have already been fully ordered in Purchase Order(s). Duplicate ordering is prevented.";
            }
        }
        else if (linkedRfq != null && linkedRfq.Status == RequestForQuotationStatus.Cancelled)
        {
            // Cancelled RFQ path
            statusDto.SelectedRoute = ProcurementRouteType.None;
            statusDto.CanCreateDirectPO = true;
            statusDto.CanCreateRFQ = true;
            statusDto.CanResetRoute = true;
            statusDto.BlockReason = $"Linked Request for Quotation '{linkedRfq.RFQNumber}' was Cancelled. You may initiate a Direct PO or a new RFQ, or formally reset the route.";
        }
        else
        {
            // Initial Approved state with no route selected
            statusDto.SelectedRoute = ProcurementRouteType.None;
            statusDto.CanCreateDirectPO = true;
            statusDto.CanCreateRFQ = true;
            statusDto.CanResetRoute = false;
        }

        return statusDto;
    }

    public async Task<RfqDetailsDto> CreateRfqFromRequisitionAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid requisitionId,
        CreateRfqFromRequisitionRequest request,
        Guid userId,
        CancellationToken ct = default)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        if (request.ClosingDate <= DateTime.UtcNow)
            throw new ArgumentException("RFQ closing date must be in the future.", nameof(request.ClosingDate));

        await _routeLock.WaitAsync(ct);
        try
        {
            var req = await _dbContext.PurchaseRequisitions
                .Include(r => r.Items)
                .FirstOrDefaultAsync(r =>
                    r.TenantId == tenantId &&
                    r.CompanyId == companyId &&
                    r.BranchId == branchId &&
                    r.Id == requisitionId, ct);

            if (req == null)
                throw new KeyNotFoundException($"Purchase requisition '{requisitionId}' not found in the authorized scope.");

            if (req.Status != PurchaseRequisitionStatus.Approved)
            {
                throw new InvalidOperationException(
                    $"Cannot create RFQ for Purchase Requisition '{req.RequisitionNumber}' because it is in status '{req.Status}'. Only Approved requisitions can be routed to RFQ.");
            }

            var activeLines = req.Items?.Where(i => i.IsActive).ToList() ?? new List<PurchaseRequisitionItem>();
            if (activeLines.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Purchase Requisition '{req.RequisitionNumber}' has no active line items.");
            }

            string reqTag = $"[PR: {req.RequisitionNumber}".ToLowerInvariant();

            // Guard 1: Direct PO route exclusivity
            var existingPo = await _dbContext.PurchaseOrders
                .Include(o => o.Items)
                .AsNoTracking()
                .Where(o =>
                    o.TenantId == tenantId &&
                    o.CompanyId == companyId &&
                    o.BranchId == branchId &&
                    o.Status != PurchaseOrderStatus.Cancelled &&
                    o.Status != PurchaseOrderStatus.Rejected)
                .FirstOrDefaultAsync(o =>
                    o.Items.Any(i => (i.Notes ?? string.Empty).ToLowerInvariant().Contains(reqTag)), ct);

            if (existingPo != null)
            {
                throw new InvalidOperationException(
                    $"Purchase Requisition '{req.RequisitionNumber}' is already committed to the Direct Purchase Order route (Purchase Order '{existingPo.OrderNumber}'). Conflicting RFQ creation is prohibited by procurement route exclusivity.");
            }

            // Guard 2: Prevent duplicate active RFQs for same PR requirement
            var existingRfq = await _dbContext.RequestForQuotations
                .AsNoTracking()
                .Where(r =>
                    r.TenantId == tenantId &&
                    r.CompanyId == companyId &&
                    r.BranchId == branchId &&
                    r.Status != RequestForQuotationStatus.Cancelled)
                .FirstOrDefaultAsync(r =>
                    (r.Instructions ?? string.Empty).ToLowerInvariant().Contains(reqTag) ||
                    (r.Title ?? string.Empty).ToLowerInvariant().Contains(req.RequisitionNumber.ToLowerInvariant()), ct);

            if (existingRfq != null)
            {
                throw new InvalidOperationException(
                    $"Purchase Requisition '{req.RequisitionNumber}' already has an active Request for Quotation '{existingRfq.RFQNumber}' (Status: {existingRfq.Status}). Duplicate RFQ creation is prohibited.");
            }

            // Build RFQ line requests from PR items
            var rfqLines = new List<CreateRfqLineRequest>();
            foreach (var item in activeLines)
            {
                var product = await _dbContext.Products
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.ProductId == item.ProductId, ct);

                if (product == null)
                    throw new InvalidOperationException($"Product '{item.ProductId}' from requisition was not found.");

                Guid uomId = item.UnitOfMeasureId ?? product.UnitOfMeasureId ?? Guid.Empty;
                if (uomId == Guid.Empty)
                {
                    var fallbackUom = await _dbContext.UnitsOfMeasure
                        .AsNoTracking()
                        .FirstOrDefaultAsync(u => u.TenantId == tenantId && u.IsActive, ct);
                    if (fallbackUom != null)
                        uomId = fallbackUom.UnitOfMeasureId;
                }

                rfqLines.Add(new CreateRfqLineRequest
                {
                    ProductId = item.ProductId,
                    Quantity = item.Quantity,
                    UnitOfMeasureId = uomId,
                    TargetUnitPrice = item.EstimatedUnitPrice > 0 ? item.EstimatedUnitPrice : null,
                    RequiredDate = req.RequiredDate ?? request.DeliveryRequiredDate,
                    TechnicalSpecifications = item.Remarks?.Trim(),
                    Notes = $"[PR: {req.RequisitionNumber} Line: {item.Id}]"
                });
            }

            string rfqTitle = !string.IsNullOrWhiteSpace(request.Title)
                ? request.Title.Trim()
                : $"RFQ for Requisition {req.RequisitionNumber}";

            string prInstructions = $"[PR: {req.RequisitionNumber} Id: {req.Id}] " +
                                   (!string.IsNullOrWhiteSpace(request.Instructions)
                                       ? request.Instructions.Trim()
                                       : (req.Notes ?? string.Empty));

            var createRfqRequest = new CreateRfqRequest
            {
                Title = rfqTitle,
                ClosingDate = request.ClosingDate,
                DeliveryRequiredDate = request.DeliveryRequiredDate ?? req.RequiredDate,
                DeliveryTerms = request.DeliveryTerms?.Trim() ?? "DDP - Standard Warehouse Delivery",
                Instructions = prInstructions.Trim(),
                Lines = rfqLines,
                SupplierIds = request.SupplierIds
            };

            var createdRfq = await _rfqService.CreateRfqAsync(
                createRfqRequest,
                userId,
                tenantId,
                companyId,
                branchId,
                ct);

            // Record audit trail of route selection
            var audit = new DomainAuditRecord
            {
                AuditId = Guid.NewGuid(),
                TenantId = tenantId,
                EntityName = "PurchaseRequisition",
                EntityId = requisitionId.ToString(),
                Action = "RouteSelected_RFQ",
                UserId = userId,
                Timestamp = DateTime.UtcNow,
                NewValues = JsonSerializer.Serialize(new
                {
                    Route = "RFQ",
                    RfqId = createdRfq.Id,
                    RfqNumber = createdRfq.RFQNumber,
                    RequisitionNumber = req.RequisitionNumber
                })
            };

            await _dbContext.DomainAuditRecords.AddAsync(audit, ct);
            await _dbContext.SaveChangesAsync(ct);

            return createdRfq;
        }
        finally
        {
            _routeLock.Release();
        }
    }

    public async Task<bool> ResetCancelledRfqRouteAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid requisitionId,
        ResetProcurementRouteRequest request,
        Guid userId,
        CancellationToken ct = default)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new ArgumentException("A reason is required to reset the procurement route.", nameof(request.Reason));

        var req = await _dbContext.PurchaseRequisitions
            .AsNoTracking()
            .FirstOrDefaultAsync(r =>
                r.TenantId == tenantId &&
                r.CompanyId == companyId &&
                r.BranchId == branchId &&
                r.Id == requisitionId, ct);

        if (req == null)
            throw new KeyNotFoundException($"Purchase requisition '{requisitionId}' not found in the authorized scope.");

        string reqTag = $"[PR: {req.RequisitionNumber}".ToLowerInvariant();

        var linkedRfq = await _dbContext.RequestForQuotations
            .AsNoTracking()
            .Where(r =>
                r.TenantId == tenantId &&
                r.CompanyId == companyId &&
                r.BranchId == branchId)
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync(r =>
                (r.Instructions ?? string.Empty).ToLowerInvariant().Contains(reqTag) ||
                (r.Title ?? string.Empty).ToLowerInvariant().Contains(req.RequisitionNumber.ToLowerInvariant()), ct);

        if (linkedRfq == null)
        {
            throw new InvalidOperationException(
                $"No linked Request for Quotation found for Requisition '{req.RequisitionNumber}'. Route reset is not applicable.");
        }

        if (linkedRfq.Status != RequestForQuotationStatus.Cancelled)
        {
            throw new InvalidOperationException(
                $"Procurement route cannot be reset because linked Request for Quotation '{linkedRfq.RFQNumber}' is currently in '{linkedRfq.Status}' status. Only Cancelled RFQs can be reset.");
        }

        // Record controlled, audited route change
        var audit = new DomainAuditRecord
        {
            AuditId = Guid.NewGuid(),
            TenantId = tenantId,
            EntityName = "PurchaseRequisition",
            EntityId = requisitionId.ToString(),
            Action = "RouteChange_RFQReset",
            UserId = userId,
            Timestamp = DateTime.UtcNow,
            OldValues = JsonSerializer.Serialize(new
            {
                Route = "RFQ",
                RfqId = linkedRfq.Id,
                RfqNumber = linkedRfq.RFQNumber,
                Status = linkedRfq.Status.ToString()
            }),
            NewValues = JsonSerializer.Serialize(new
            {
                Reason = request.Reason.Trim(),
                ResetByUserId = userId,
                RouteResetTo = "Available"
            })
        };

        await _dbContext.DomainAuditRecords.AddAsync(audit, ct);
        await _dbContext.SaveChangesAsync(ct);

        return true;
    }

    public async Task ValidateDirectPoRouteAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid requisitionId,
        CancellationToken ct = default)
    {
        var req = await _dbContext.PurchaseRequisitions
            .Include(r => r.Items)
            .AsNoTracking()
            .FirstOrDefaultAsync(r =>
                r.TenantId == tenantId &&
                r.CompanyId == companyId &&
                r.BranchId == branchId &&
                r.Id == requisitionId, ct);

        if (req == null)
            throw new KeyNotFoundException($"Purchase requisition '{requisitionId}' not found in the authorized scope.");

        if (req.Status != PurchaseRequisitionStatus.Approved)
        {
            throw new InvalidOperationException(
                $"Cannot create Purchase Order for Requisition '{req.RequisitionNumber}' because it is in status '{req.Status}'. Only Approved requisitions can be converted to Purchase Orders.");
        }

        string reqTag = $"[PR: {req.RequisitionNumber}".ToLowerInvariant();

        // Check if an active RFQ exists
        var activeRfq = await _dbContext.RequestForQuotations
            .AsNoTracking()
            .Where(r =>
                r.TenantId == tenantId &&
                r.CompanyId == companyId &&
                r.BranchId == branchId &&
                r.Status != RequestForQuotationStatus.Cancelled)
            .FirstOrDefaultAsync(r =>
                (r.Instructions ?? string.Empty).ToLowerInvariant().Contains(reqTag) ||
                (r.Title ?? string.Empty).ToLowerInvariant().Contains(req.RequisitionNumber.ToLowerInvariant()), ct);

        if (activeRfq != null)
        {
            throw new InvalidOperationException(
                $"Purchase Requisition '{req.RequisitionNumber}' is currently routed to Request for Quotation '{activeRfq.RFQNumber}' (Status: {activeRfq.Status}). Direct Purchase Order creation is prohibited while an active RFQ exists.");
        }

        // Check remaining quantity
        decimal totalRequested = req.Items?.Where(i => i.IsActive).Sum(i => i.Quantity) ?? 0m;
        var activeOrders = await _dbContext.PurchaseOrders
            .Include(o => o.Items)
            .AsNoTracking()
            .Where(o =>
                o.TenantId == tenantId &&
                o.CompanyId == companyId &&
                o.BranchId == branchId &&
                o.Status != PurchaseOrderStatus.Cancelled &&
                o.Status != PurchaseOrderStatus.Rejected)
            .ToListAsync(ct);

        decimal totalOrdered = 0m;
        foreach (var ord in activeOrders)
        {
            foreach (var item in ord.Items)
            {
                var notes = (item.Notes ?? string.Empty).ToLowerInvariant();
                if (notes.Contains(reqTag) ||
                    (req.Items != null && req.Items.Any(pi => notes.Contains($"line: {pi.Id}".ToLowerInvariant()) || notes.Contains($"line:{pi.Id}".ToLowerInvariant()))))
                {
                    totalOrdered += item.Quantity;
                }
            }
        }

        if (totalRequested > 0 && totalOrdered >= totalRequested)
        {
            throw new InvalidOperationException(
                $"Purchase Requisition '{req.RequisitionNumber}' has already been fully ordered ({totalOrdered} of {totalRequested} ordered). Duplicate ordering is prevented.");
        }
    }

    public async Task ValidateRfqAwardPoRouteAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid rfqId,
        Guid quotationId,
        CancellationToken ct = default)
    {
        var rfq = await _dbContext.RequestForQuotations
            .Include(r => r.Quotations)
                .ThenInclude(q => q.Lines)
            .AsNoTracking()
            .FirstOrDefaultAsync(r =>
                r.TenantId == tenantId &&
                r.CompanyId == companyId &&
                r.BranchId == branchId &&
                r.Id == rfqId, ct);

        if (rfq == null)
            throw new KeyNotFoundException($"RFQ with ID '{rfqId}' not found.");

        if (rfq.Status != RequestForQuotationStatus.Awarded)
        {
            throw new InvalidOperationException(
                $"Cannot create Purchase Order for RFQ '{rfq.RFQNumber}' because it is in status '{rfq.Status}'. Only Awarded RFQs can proceed to Purchase Order.");
        }

        var quote = rfq.Quotations?.FirstOrDefault(q => q.Id == quotationId);
        if (quote == null)
            throw new KeyNotFoundException($"Quotation '{quotationId}' not found on RFQ '{rfq.RFQNumber}'.");

        if (quote.Status != SupplierQuotationStatus.Approved)
        {
            throw new InvalidOperationException(
                $"Quotation '{quote.QuotationReference}' is in status '{quote.Status}'. Only Approved (Awarded) quotations can be converted to Purchase Orders.");
        }

        // Check if a PO has already been created for this award
        string rfqQuoteTag = $"[RFQ: {rfq.RFQNumber} Quote: {quote.QuotationReference}".ToLowerInvariant();

        var existingPos = await _dbContext.PurchaseOrders
            .Include(o => o.Items)
            .AsNoTracking()
            .Where(o =>
                o.TenantId == tenantId &&
                o.CompanyId == companyId &&
                o.BranchId == branchId &&
                o.Status != PurchaseOrderStatus.Cancelled &&
                o.Status != PurchaseOrderStatus.Rejected)
            .ToListAsync(ct);

        decimal totalQuoted = quote.Lines?.Sum(l => l.QuotedQuantity) ?? 0m;
        decimal totalOrdered = 0m;

        foreach (var ord in existingPos)
        {
            foreach (var item in ord.Items)
            {
                var notes = (item.Notes ?? string.Empty).ToLowerInvariant();
                if (notes.Contains(rfqQuoteTag) ||
                    (quote.Lines != null && quote.Lines.Any(ql => notes.Contains($"line: {ql.Id}".ToLowerInvariant()) || notes.Contains($"line:{ql.Id}".ToLowerInvariant()))))
                {
                    totalOrdered += item.Quantity;
                }
            }
        }

        if (totalQuoted > 0 && totalOrdered >= totalQuoted)
        {
            throw new InvalidOperationException(
                $"A Purchase Order has already been created for Awarded Quotation '{quote.QuotationReference}' on RFQ '{rfq.RFQNumber}'. Duplicate order creation is prevented.");
        }
    }
}
