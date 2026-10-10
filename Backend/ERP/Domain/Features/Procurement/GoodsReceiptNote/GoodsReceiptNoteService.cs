using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domain.Features.MasterData.Product;
using Domain.Features.Procurement.PurchaseOrder;
using Domain.Infrastructure.Persistence;
using ERP.Domain.Features.Inventory;
using ERP.Domain.Features.MasterData.Warehouse;
using Microsoft.EntityFrameworkCore;
using SaaS.Application.Interfaces;

namespace Domain.Features.Procurement.GoodsReceiptNote;

public class GoodsReceiptNoteService : IGoodsReceiptNoteService
{
    private readonly IGoodsReceiptNoteRepository _grnRepository;
    private readonly IPurchaseOrderRepository _poRepository;
    private readonly IProductRepository _productRepository;
    private readonly IWarehouseRepository _warehouseRepository;
    private readonly IInventoryPostingService _postingService;
    private readonly ISubscriptionUsageService _subscriptionUsageService;
    private readonly DomainDbContext _context;

    public GoodsReceiptNoteService(
        IGoodsReceiptNoteRepository grnRepository,
        IPurchaseOrderRepository poRepository,
        IProductRepository productRepository,
        IWarehouseRepository warehouseRepository,
        IInventoryPostingService postingService,
        ISubscriptionUsageService subscriptionUsageService,
        DomainDbContext context)
    {
        _grnRepository = grnRepository;
        _poRepository = poRepository;
        _productRepository = productRepository;
        _warehouseRepository = warehouseRepository;
        _postingService = postingService;
        _subscriptionUsageService = subscriptionUsageService;
        _context = context;
    }

    public async Task<List<GoodsReceiptNoteDto>> GetGrnsAsync(Guid tenantId, Guid companyId, Guid branchId)
    {
        var list = await _grnRepository.GetByBranchAsync(tenantId, companyId, branchId);
        return list.Select(MapToDto).ToList();
    }

    public async Task<GoodsReceiptNoteDto?> GetGrnByIdAsync(Guid tenantId, Guid companyId, Guid branchId, Guid grnId)
    {
        var grn = await _grnRepository.GetByIdAsync(tenantId, companyId, branchId, grnId);
        return grn == null ? null : MapToDto(grn);
    }

    public async Task<GoodsReceiptNoteDto> CreateDraftFromPoAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid purchaseOrderId,
        string grnNumber,
        Guid? defaultWarehouseId = null,
        Guid? defaultLocationId = null,
        string? remarks = null)
    {
        if (string.IsNullOrWhiteSpace(grnNumber))
            throw new ArgumentException("GRN number is required.", nameof(grnNumber));

        string normGrnNumber = grnNumber.Trim();
        if (await _grnRepository.ExistsGrnNumberAsync(tenantId, companyId, normGrnNumber))
            throw new InvalidOperationException($"GRN number '{normGrnNumber}' already exists for this company.");

        var po = await _poRepository.GetByIdAsync(tenantId, companyId, branchId, purchaseOrderId)
            ?? throw new KeyNotFoundException("Purchase Order not found in authorized branch/company scope.");

        if (po.Status != PurchaseOrderStatus.Approved)
            throw new InvalidOperationException($"Cannot receive against Purchase Order in status '{po.Status}'. Only Approved POs can be received.");

        // Check if there are active lines with unreceived quantity
        var activeLines = po.Items.Where(i => i.IsActive).ToList();
        if (activeLines.Count == 0)
            throw new InvalidOperationException("Purchase Order has no active lines to receive.");

        var receivableLines = activeLines.Where(i => (i.Quantity - i.ReceivedQuantity) > 0).ToList();
        if (receivableLines.Count == 0)
            throw new InvalidOperationException("All items on this Purchase Order have already been fully received.");

        // If warehouse/location are not provided, we attempt to find the first active warehouse and location
        Guid targetWarehouseId;
        Guid targetLocationId;

        if (defaultWarehouseId.HasValue && defaultLocationId.HasValue)
        {
            targetWarehouseId = defaultWarehouseId.Value;
            targetLocationId = defaultLocationId.Value;
        }
        else
        {
            var warehouses = await _warehouseRepository.GetWarehousesAsync(tenantId);
            var wh = warehouses.FirstOrDefault(w => w.IsActive)
                ?? throw new InvalidOperationException("No active warehouse found for tenant. Please provide warehouse and location.");
            var locations = await _warehouseRepository.GetLocationsAsync(tenantId, wh.WarehouseId);
            var loc = locations.FirstOrDefault(l => l.IsActive)
                ?? throw new InvalidOperationException("No active location found in warehouse. Please provide warehouse and location.");
            targetWarehouseId = wh.WarehouseId;
            targetLocationId = loc.WarehouseLocationId;
        }

        var grn = GoodsReceiptNote.Create(
            tenantId,
            companyId,
            branchId,
            po.Id,
            normGrnNumber,
            DateTime.UtcNow,
            po.SupplierId,
            remarks: remarks);

        foreach (var poItem in receivableLines)
        {
            decimal remainingQty = poItem.Quantity - poItem.ReceivedQuantity;
            var line = GoodsReceiptNoteLine.Create(
                grn.GoodsReceiptNoteId,
                poItem.Id,
                poItem.ProductId,
                targetWarehouseId,
                targetLocationId,
                remainingQty,
                poItem.UnitPrice,
                poItem.UnitOfMeasureId);

            grn.AddLine(line);
        }

        await _grnRepository.AddAsync(grn);
        await _grnRepository.SaveChangesAsync();

        return MapToDto(grn);
    }

    public async Task<GoodsReceiptNoteDto> CreateManualDraftAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        CreateGoodsReceiptNoteRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        if (string.IsNullOrWhiteSpace(request.GrnNumber))
            throw new ArgumentException("GRN number is required.", nameof(request.GrnNumber));

        string normGrnNumber = request.GrnNumber.Trim();
        if (await _grnRepository.ExistsGrnNumberAsync(tenantId, companyId, normGrnNumber))
            throw new InvalidOperationException($"GRN number '{normGrnNumber}' already exists for this company.");

        var po = await _poRepository.GetByIdAsync(tenantId, companyId, branchId, request.PurchaseOrderId)
            ?? throw new KeyNotFoundException("Purchase Order not found in authorized branch/company scope.");

        if (po.Status != PurchaseOrderStatus.Approved)
            throw new InvalidOperationException($"Cannot receive against Purchase Order in status '{po.Status}'. Only Approved POs can be received.");

        if (request.Lines == null || request.Lines.Count == 0)
            throw new ArgumentException("At least one line is required to create a GRN.", nameof(request.Lines));

        var grn = GoodsReceiptNote.Create(
            tenantId,
            companyId,
            branchId,
            po.Id,
            normGrnNumber,
            request.ReceiptDate,
            po.SupplierId,
            request.DeliveryNoteNumber,
            request.Remarks);

        foreach (var lineReq in request.Lines)
        {
            var poItem = po.Items.FirstOrDefault(i => i.Id == lineReq.PurchaseOrderItemId && i.IsActive)
                ?? throw new InvalidOperationException($"PO item '{lineReq.PurchaseOrderItemId}' not found on PO '{po.OrderNumber}'.");

            if (poItem.ProductId != lineReq.ProductId)
                throw new InvalidOperationException($"Product mismatch for PO item '{poItem.Id}'.");

            var line = GoodsReceiptNoteLine.Create(
                grn.GoodsReceiptNoteId,
                poItem.Id,
                lineReq.ProductId,
                lineReq.WarehouseId,
                lineReq.WarehouseLocationId,
                lineReq.QuantityReceived,
                lineReq.UnitCost,
                lineReq.UnitOfMeasureId ?? poItem.UnitOfMeasureId,
                lineReq.BatchNumber,
                lineReq.SerialNumber,
                lineReq.ExpiryDate,
                lineReq.IsQuarantine,
                lineReq.Remarks);

            grn.AddLine(line);
        }

        await _grnRepository.AddAsync(grn);
        await _grnRepository.SaveChangesAsync();

        return MapToDto(grn);
    }

    public async Task<GoodsReceiptNoteDto> UpdateDraftAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid grnId,
        UpdateGoodsReceiptNoteRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        var grn = await _grnRepository.GetByIdAsync(tenantId, companyId, branchId, grnId)
            ?? throw new KeyNotFoundException("GRN not found in authorized branch/company scope.");

        if (grn.Status != GoodsReceiptNoteStatus.Draft)
            throw new InvalidOperationException($"Only Draft GRNs can be modified. Current status: '{grn.Status}'.");

        var po = await _poRepository.GetByIdAsync(tenantId, companyId, branchId, grn.PurchaseOrderId)
            ?? throw new KeyNotFoundException("Associated Purchase Order not found.");

        if (request.ReceiptDate.HasValue)
            grn.ReceiptDate = request.ReceiptDate.Value;

        grn.DeliveryNoteNumber = request.DeliveryNoteNumber?.Trim();
        grn.Remarks = request.Remarks?.Trim();

        if (request.Lines != null && request.Lines.Count > 0)
        {
            // Update existing lines or replace
            // To ensure change tracking doesn't conflict with child navigation, modify in-place or clear cleanly
            var existingLines = grn.Lines.ToList();
            grn.Lines.Clear();
            _context.GoodsReceiptNoteLines.RemoveRange(existingLines);

            foreach (var lineReq in request.Lines)
            {
                var poItem = po.Items.FirstOrDefault(i => i.Id == lineReq.PurchaseOrderItemId && i.IsActive)
                    ?? throw new InvalidOperationException($"PO item '{lineReq.PurchaseOrderItemId}' not found on PO.");

                var line = GoodsReceiptNoteLine.Create(
                    grn.GoodsReceiptNoteId,
                    poItem.Id,
                    lineReq.ProductId,
                    lineReq.WarehouseId,
                    lineReq.WarehouseLocationId,
                    lineReq.QuantityReceived,
                    lineReq.UnitCost,
                    lineReq.UnitOfMeasureId ?? poItem.UnitOfMeasureId,
                    lineReq.BatchNumber,
                    lineReq.SerialNumber,
                    lineReq.ExpiryDate,
                    lineReq.IsQuarantine,
                    lineReq.Remarks);

                grn.Lines.Add(line);
                _context.GoodsReceiptNoteLines.Add(line);
            }

            grn.RecalculateTotal();
        }
        else
        {
            grn.RecalculateTotal();
        }

        await _grnRepository.SaveChangesAsync();
        return MapToDto(grn);
    }

    public async Task<GoodsReceiptNoteDto> CancelDraftAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid grnId)
    {
        var grn = await _grnRepository.GetByIdAsync(tenantId, companyId, branchId, grnId)
            ?? throw new KeyNotFoundException("GRN not found in authorized branch/company scope.");

        grn.Cancel();
        await _grnRepository.SaveChangesAsync();
        return MapToDto(grn);
    }

    public async Task<GoodsReceiptNoteDto> ConfirmGrnAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid grnId)
    {
        // 1. Initial check: Is GRN already confirmed? (Idempotent early return)
        var existingGrn = await _grnRepository.GetByIdAsync(tenantId, companyId, branchId, grnId)
            ?? throw new KeyNotFoundException("GRN not found in authorized branch/company scope.");

        if (existingGrn.Status == GoodsReceiptNoteStatus.Confirmed)
        {
            // Already confirmed - return cleanly without re-charging quota or re-posting inventory
            return MapToDto(existingGrn);
        }

        if (existingGrn.Status == GoodsReceiptNoteStatus.Cancelled)
        {
            throw new InvalidOperationException("Cancelled GRN cannot be confirmed.");
        }

        // 2. Wrap confirmation execution inside Subscription Usage limit ("TRANSACTIONS" increment = 1)
        return await _subscriptionUsageService.ExecuteWithUsageLimitAsync(
            tenantId,
            "TRANSACTIONS",
            1,
            async () =>
            {
                // Open Domain Transaction
                await using var domainTx = await _context.Database.BeginTransactionAsync();

                // Re-fetch GRN with row version tracking and lock
                var grn = await _grnRepository.GetByIdAsync(tenantId, companyId, branchId, grnId)
                    ?? throw new KeyNotFoundException("GRN not found.");

                if (grn.Status == GoodsReceiptNoteStatus.Confirmed)
                {
                    // Concurrent request already committed
                    return MapToDto(grn);
                }

                if (grn.Status == GoodsReceiptNoteStatus.Cancelled)
                {
                    throw new InvalidOperationException("Cancelled GRN cannot be confirmed.");
                }

                var po = await _poRepository.GetByIdAsync(tenantId, companyId, branchId, grn.PurchaseOrderId)
                    ?? throw new KeyNotFoundException("Associated Purchase Order not found.");

                if (po.Status != PurchaseOrderStatus.Approved)
                {
                    throw new InvalidOperationException($"Cannot receive against PO in status '{po.Status}'. Only Approved POs can be received.");
                }

                // Group received quantities by PO Item to validate zero over-receipt tolerance
                var grnQtyByPoItem = grn.Lines
                    .GroupBy(l => l.PurchaseOrderItemId)
                    .ToDictionary(g => g.Key, g => g.Sum(l => l.QuantityReceived));

                foreach (var (poItemId, receivedInThisGrn) in grnQtyByPoItem)
                {
                    var poItem = po.Items.FirstOrDefault(i => i.Id == poItemId && i.IsActive)
                        ?? throw new InvalidOperationException($"PO item '{poItemId}' not found or inactive.");

                    decimal maxAllowable = poItem.Quantity - poItem.ReceivedQuantity;
                    if (receivedInThisGrn > maxAllowable)
                    {
                        throw new InvalidOperationException(
                            $"Over-receipt rejected for item '{poItem.Id}'. Ordered: {poItem.Quantity}, Already Received: {poItem.ReceivedQuantity}, Attempted in GRN: {receivedInThisGrn}. Over-receipt tolerance is 0%.");
                    }

                    // Increment ReceivedQuantity on PO item
                    poItem.ReceivedQuantity += receivedInThisGrn;
                }

                // Check if all active lines on PO are fully fulfilled
                bool allFulfilled = po.Items
                    .Where(i => i.IsActive)
                    .All(i => i.ReceivedQuantity >= i.Quantity);

                if (allFulfilled)
                {
                    po.Status = PurchaseOrderStatus.Fulfilled;
                }

                // Mark GRN as Confirmed
                grn.MarkConfirmed();

                // Post Inventory ledger for each line using existing IInventoryPostingService
                int sequence = 1;
                foreach (var line in grn.Lines)
                {
                    var postReq = new PostInventoryMovementRequest
                    {
                        MovementType = InventoryMovementType.GoodsReceipt,
                        SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
                        SourceDocumentId = grn.GoodsReceiptNoteId,
                        SourceDocumentLineId = line.GoodsReceiptNoteLineId,
                        MovementSequence = sequence++,
                        ProductId = line.ProductId,
                        WarehouseId = line.WarehouseId,
                        WarehouseLocationId = line.WarehouseLocationId,
                        Quantity = line.QuantityReceived,
                        UnitCost = line.UnitCost,
                        UnitOfMeasureId = line.UnitOfMeasureId,
                        BatchNumber = line.BatchNumber,
                        SerialNumber = line.SerialNumber,
                        ExpiryDate = line.ExpiryDate,
                        TransactionDate = grn.ReceiptDate,
                        IsQuarantine = line.IsQuarantine
                    };

                    await _postingService.PostMovementAsync(tenantId, postReq);
                }

                // Commit Domain Db Changes & Domain Transaction
                await _context.SaveChangesAsync();
                await domainTx.CommitAsync();

                return MapToDto(grn);
            });
    }

    private static GoodsReceiptNoteDto MapToDto(GoodsReceiptNote grn)
    {
        return new GoodsReceiptNoteDto
        {
            GoodsReceiptNoteId = grn.GoodsReceiptNoteId,
            TenantId = grn.TenantId,
            CompanyId = grn.CompanyId,
            BranchId = grn.BranchId,
            PurchaseOrderId = grn.PurchaseOrderId,
            SupplierId = grn.SupplierId,
            GrnNumber = grn.GrnNumber,
            ReceiptDate = grn.ReceiptDate,
            Status = grn.Status,
            DeliveryNoteNumber = grn.DeliveryNoteNumber,
            Remarks = grn.Remarks,
            TotalAmount = grn.TotalAmount,
            Lines = grn.Lines.Select(l => new GoodsReceiptNoteLineDto
            {
                GoodsReceiptNoteLineId = l.GoodsReceiptNoteLineId,
                GoodsReceiptNoteId = l.GoodsReceiptNoteId,
                PurchaseOrderItemId = l.PurchaseOrderItemId,
                ProductId = l.ProductId,
                WarehouseId = l.WarehouseId,
                WarehouseLocationId = l.WarehouseLocationId,
                QuantityReceived = l.QuantityReceived,
                UnitCost = l.UnitCost,
                LineTotal = l.LineTotal,
                UnitOfMeasureId = l.UnitOfMeasureId,
                BatchNumber = l.BatchNumber,
                SerialNumber = l.SerialNumber,
                ExpiryDate = l.ExpiryDate,
                IsQuarantine = l.IsQuarantine,
                Remarks = l.Remarks
            }).ToList()
        };
    }
}
