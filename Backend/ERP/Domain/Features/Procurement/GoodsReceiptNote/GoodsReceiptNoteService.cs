using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domain.Features.MasterData.Product;
using Domain.Features.MasterData.UnitOfMeasure;
using Domain.Features.Procurement.PurchaseOrder;
using Domain.Infrastructure.Persistence;
using ERP.Domain.Features.Inventory;
using ERP.Domain.Features.MasterData.Warehouse;
using Microsoft.EntityFrameworkCore;


namespace Domain.Features.Procurement.GoodsReceiptNote;

public class GoodsReceiptNoteService : IGoodsReceiptNoteService
{
    private readonly IGoodsReceiptNoteRepository _grnRepository;
    private readonly IPurchaseOrderRepository _poRepository;
    private readonly IProductRepository _productRepository;
    private readonly IWarehouseRepository _warehouseRepository;
    private readonly IInventoryPostingService _postingService;

    private readonly DomainDbContext _context;
    private readonly IUnitOfMeasureService? _uomService;

    public GoodsReceiptNoteService(
        IGoodsReceiptNoteRepository grnRepository,
        IPurchaseOrderRepository poRepository,
        IProductRepository productRepository,
        IWarehouseRepository warehouseRepository,
        IInventoryPostingService postingService,
        DomainDbContext context,
        IUnitOfMeasureService? uomService = null)
    {
        _grnRepository = grnRepository;
        _poRepository = poRepository;
        _productRepository = productRepository;
        _warehouseRepository = warehouseRepository;
        _postingService = postingService;
        _context = context;
        _uomService = uomService;
    }

    public async Task<List<GoodsReceiptNoteDto>> GetGrnsAsync(Guid tenantId, Guid companyId, Guid branchId)
    {
        var list = await _grnRepository.GetByBranchAsync(tenantId, companyId, branchId);
        var result = new List<GoodsReceiptNoteDto>(list.Count);
        foreach (var grn in list)
            result.Add(await MapToDtoAsync(tenantId, grn));
        return result;
    }

    public async Task<GoodsReceiptNoteDto?> GetGrnByIdAsync(Guid tenantId, Guid companyId, Guid branchId, Guid grnId)
    {
        var grn = await _grnRepository.GetByIdAsync(tenantId, companyId, branchId, grnId);
        return grn == null ? null : await MapToDtoAsync(tenantId, grn);
    }

    public async Task<GoodsReceiptNoteDto> CreateDraftFromPoAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid purchaseOrderId,
        string? grnNumber = null,
        Guid? defaultWarehouseId = null,
        Guid? defaultLocationId = null,
        string? remarks = null)
    {
        string normGrnNumber = grnNumber?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normGrnNumber))
        {
            normGrnNumber = $"GRN-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}"[..18].ToUpperInvariant();
        }

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

        Guid targetWarehouseId;
        Guid targetLocationId;

        if (defaultWarehouseId.HasValue != defaultLocationId.HasValue)
            throw new ArgumentException("Warehouse and location must be provided together.");

        if (defaultWarehouseId.HasValue)
        {
            targetWarehouseId = defaultWarehouseId.GetValueOrDefault();
            targetLocationId = defaultLocationId.GetValueOrDefault();
        }
        else
        {
            (targetWarehouseId, targetLocationId) =
                await GetDefaultWarehouseLocationAsync(tenantId, companyId, branchId);
        }

        await ValidateReceiptWarehouseScopeAsync(
            tenantId, companyId, branchId, targetWarehouseId, targetLocationId);

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
            var units = await ResolveReceiptUnitsAsync(tenantId, poItem, poItem.ProductId, poItem.UnitOfMeasureId);
            decimal lineTotal = await CalculateReceiptValueAsync(tenantId, companyId, poItem, remainingQty);
            var line = GoodsReceiptNoteLine.Create(
                grn.GoodsReceiptNoteId,
                poItem.Id,
                poItem.ProductId,
                targetWarehouseId,
                targetLocationId,
                remainingQty,
                Math.Round(lineTotal / remainingQty, 2, MidpointRounding.AwayFromZero),
                units.GrnUnitOfMeasureId,
                lineTotalOverride: lineTotal);

            grn.AddLine(line);
        }

        await _grnRepository.AddAsync(grn);
        await _grnRepository.SaveChangesAsync();

        return await MapToDtoAsync(tenantId, grn);
    }

    public async Task<GoodsReceiptNoteDto> CreateManualDraftAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        CreateGoodsReceiptNoteRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        string normGrnNumber = request.GrnNumber?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normGrnNumber))
        {
            normGrnNumber = $"GRN-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}"[..18].ToUpperInvariant();
        }

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

        Guid defaultWhId = Guid.Empty;
        Guid defaultLocId = Guid.Empty;

        foreach (var lineReq in request.Lines)
        {
            var poItem = po.Items.FirstOrDefault(i => i.Id == lineReq.PurchaseOrderItemId && i.IsActive)
                ?? throw new InvalidOperationException($"PO item '{lineReq.PurchaseOrderItemId}' not found on PO '{po.OrderNumber}'.");

            if (poItem.ProductId != lineReq.ProductId)
                throw new InvalidOperationException($"Product mismatch for PO item '{poItem.Id}'.");

            var units = await ResolveReceiptUnitsAsync(
                tenantId,
                poItem,
                lineReq.ProductId,
                lineReq.UnitOfMeasureId);
            decimal quantityInPoUom = await ConvertReceiptQuantityAsync(
                tenantId,
                companyId,
                lineReq.ProductId,
                units.GrnUnitOfMeasureId,
                units.PurchaseOrderUnitOfMeasureId,
                lineReq.QuantityReceived);
            decimal lineTotal = await CalculateReceiptValueAsync(tenantId, companyId, poItem, quantityInPoUom);
            decimal grnUnitCost = Math.Round(lineTotal / Math.Round(lineReq.QuantityReceived, 4, MidpointRounding.AwayFromZero), 2, MidpointRounding.AwayFromZero);

            Guid targetWhId = lineReq.WarehouseId;
            Guid targetLocId = lineReq.WarehouseLocationId;

            if (targetWhId == Guid.Empty != (targetLocId == Guid.Empty))
                throw new InvalidOperationException("Warehouse and location must be provided together.");

            if (targetWhId == Guid.Empty)
            {
                if (defaultWhId == Guid.Empty || defaultLocId == Guid.Empty)
                {
                    (defaultWhId, defaultLocId) =
                        await GetDefaultWarehouseLocationAsync(tenantId, companyId, branchId);
                }
                targetWhId = defaultWhId;
                targetLocId = defaultLocId;
            }

            await ValidateReceiptWarehouseScopeAsync(
                tenantId, companyId, branchId, targetWhId, targetLocId);

            // CRITICAL BUSINESS RULE: Price protection - unit price is copied from approved PO line by server; client prices are ignored
            var line = GoodsReceiptNoteLine.Create(
                grn.GoodsReceiptNoteId,
                poItem.Id,
                lineReq.ProductId,
                targetWhId,
                targetLocId,
                lineReq.QuantityReceived,
                grnUnitCost, // PO value expressed per GRN unit
                units.GrnUnitOfMeasureId,
                lineReq.BatchNumber,
                lineReq.SerialNumber,
                lineReq.ExpiryDate,
                lineReq.IsQuarantine,
                lineReq.Remarks,
                lineReq.ManufacturingDate,
                lineTotal);

            grn.AddLine(line);
        }

        await _grnRepository.AddAsync(grn);
        await _grnRepository.SaveChangesAsync();

        return await MapToDtoAsync(tenantId, grn);
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

        if (request.Lines != null)
        {
            Guid defaultWhId = Guid.Empty;
            Guid defaultLocId = Guid.Empty;
            var replacementLines = new List<GoodsReceiptNoteLine>();

            foreach (var lineReq in request.Lines)
            {
                var poItem = po.Items.FirstOrDefault(i => i.Id == lineReq.PurchaseOrderItemId && i.IsActive)
                    ?? throw new InvalidOperationException($"PO item '{lineReq.PurchaseOrderItemId}' not found on PO.");

                if (poItem.ProductId != lineReq.ProductId)
                    throw new InvalidOperationException($"Product mismatch for PO item '{poItem.Id}'.");

                var units = await ResolveReceiptUnitsAsync(
                    tenantId,
                    poItem,
                    lineReq.ProductId,
                    lineReq.UnitOfMeasureId);
                decimal quantityInPoUom = await ConvertReceiptQuantityAsync(
                    tenantId,
                    companyId,
                    lineReq.ProductId,
                    units.GrnUnitOfMeasureId,
                    units.PurchaseOrderUnitOfMeasureId,
                    lineReq.QuantityReceived);
                decimal lineTotal = await CalculateReceiptValueAsync(tenantId, companyId, poItem, quantityInPoUom);
                decimal grnUnitCost = Math.Round(lineTotal / Math.Round(lineReq.QuantityReceived, 4, MidpointRounding.AwayFromZero), 2, MidpointRounding.AwayFromZero);

                Guid targetWhId = lineReq.WarehouseId;
                Guid targetLocId = lineReq.WarehouseLocationId;

                if (targetWhId == Guid.Empty != (targetLocId == Guid.Empty))
                    throw new InvalidOperationException("Warehouse and location must be provided together.");

                if (targetWhId == Guid.Empty)
                {
                    if (defaultWhId == Guid.Empty || defaultLocId == Guid.Empty)
                    {
                        (defaultWhId, defaultLocId) =
                            await GetDefaultWarehouseLocationAsync(tenantId, companyId, branchId);
                    }
                    targetWhId = defaultWhId;
                    targetLocId = defaultLocId;
                }

                await ValidateReceiptWarehouseScopeAsync(
                    tenantId, companyId, branchId, targetWhId, targetLocId);

                // CRITICAL BUSINESS RULE: Price protection - unit price is copied from approved PO line by server; client prices are ignored
                var line = GoodsReceiptNoteLine.Create(
                    grn.GoodsReceiptNoteId,
                    poItem.Id,
                    lineReq.ProductId,
                    targetWhId,
                    targetLocId,
                    lineReq.QuantityReceived,
                    grnUnitCost, // PO value expressed per GRN unit
                    units.GrnUnitOfMeasureId,
                    lineReq.BatchNumber,
                    lineReq.SerialNumber,
                    lineReq.ExpiryDate,
                    lineReq.IsQuarantine,
                    lineReq.Remarks,
                    lineReq.ManufacturingDate,
                    lineTotal);

                replacementLines.Add(line);
            }

            var existingLines = grn.Lines.ToList();
            grn.Lines.Clear();
            _context.GoodsReceiptNoteLines.RemoveRange(existingLines);
            grn.Lines.AddRange(replacementLines);
            _context.GoodsReceiptNoteLines.AddRange(replacementLines);
            grn.RecalculateTotal();
        }

        await _grnRepository.SaveChangesAsync();
        return await MapToDtoAsync(tenantId, grn);
    }

    public async Task DeleteDraftAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid grnId)
    {
        var grn = await _grnRepository.GetByIdAsync(tenantId, companyId, branchId, grnId)
            ?? throw new KeyNotFoundException("GRN not found in authorized branch/company scope.");

        if (grn.Status != GoodsReceiptNoteStatus.Draft)
            throw new InvalidOperationException($"Only Draft GRNs can be deleted. Current status: '{grn.Status}'.");

        await _grnRepository.DeleteDraftAsync(grn);
        await _grnRepository.SaveChangesAsync();
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
        return await MapToDtoAsync(tenantId, grn);
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
            return await MapToDtoAsync(tenantId, existingGrn);
        }

        if (existingGrn.Status == GoodsReceiptNoteStatus.Cancelled)
        {
            throw new InvalidOperationException("Cancelled GRN cannot be confirmed.");
        }

        // Open Domain Transaction
        await using var domainTx = await _context.Database.BeginTransactionAsync();

                // Re-fetch GRN with row version tracking and lock
                var grn = await _grnRepository.GetByIdAsync(tenantId, companyId, branchId, grnId)
                    ?? throw new KeyNotFoundException("GRN not found.");

                if (grn.Status == GoodsReceiptNoteStatus.Confirmed)
                {
                    // Concurrent request already committed
                    return await MapToDtoAsync(tenantId, grn);
                }

                if (grn.Status == GoodsReceiptNoteStatus.Cancelled)
                {
                    throw new InvalidOperationException("Cancelled GRN cannot be confirmed.");
                }

                var po = await _poRepository.GetByIdAsync(tenantId, companyId, branchId, grn.PurchaseOrderId)
                    ?? throw new KeyNotFoundException("Associated Purchase Order not found.");

                var productIds = grn.Lines.Select(l => l.ProductId).Distinct().ToList();
                var products = await _context.Products
                    .Where(p => p.TenantId == tenantId && productIds.Contains(p.ProductId))
                    .ToDictionaryAsync(p => p.ProductId);

                if (po.Status != PurchaseOrderStatus.Approved)
                {
                    throw new InvalidOperationException($"Cannot receive against PO in status '{po.Status}'. Only Approved POs can be received.");
                }

                // Group received quantities by PO Item to validate zero over-receipt tolerance.
                // If GRN line has a different UOM than the PO item, convert received qty to the PO item's UOM.
                var grnQtyByPoItem = new Dictionary<Guid, decimal>();
                foreach (var line in grn.Lines)
                {
                    await ValidateReceiptWarehouseScopeAsync(
                        tenantId, companyId, branchId, line.WarehouseId, line.WarehouseLocationId);

                    var poItem = po.Items.FirstOrDefault(i => i.Id == line.PurchaseOrderItemId && i.IsActive)
                        ?? throw new InvalidOperationException($"PO item '{line.PurchaseOrderItemId}' not found or inactive.");

                    var product = await GetActiveProductAsync(tenantId, line.ProductId, products);
                    var units = await ResolveReceiptUnitsAsync(tenantId, poItem, line.ProductId, line.UnitOfMeasureId, product);
                    decimal qtyInPoUom = await ConvertReceiptQuantityAsync(
                        tenantId,
                        companyId,
                        line.ProductId,
                        units.GrnUnitOfMeasureId,
                        units.PurchaseOrderUnitOfMeasureId,
                        line.QuantityReceived);

                    if (!grnQtyByPoItem.ContainsKey(line.PurchaseOrderItemId))
                        grnQtyByPoItem[line.PurchaseOrderItemId] = 0;
                    grnQtyByPoItem[line.PurchaseOrderItemId] += qtyInPoUom;
                }

                var activePoItems = po.Items.Where(item => item.IsActive).ToList();
                var activePoItemIds = activePoItems.Select(item => item.Id).ToList();
                var previouslyConfirmedLines = await _context.GoodsReceiptNotes
                    .Where(existing => existing.TenantId == tenantId &&
                                       existing.CompanyId == companyId &&
                                       existing.PurchaseOrderId == po.Id &&
                                       existing.GoodsReceiptNoteId != grn.GoodsReceiptNoteId &&
                                       existing.Status == GoodsReceiptNoteStatus.Confirmed)
                    .SelectMany(existing => existing.Lines)
                    .Where(line => activePoItemIds.Contains(line.PurchaseOrderItemId))
                    .ToListAsync();

                var exactReceivedByPoItem = activePoItems.ToDictionary(item => item.Id, _ => 0m);
                foreach (var priorLine in previouslyConfirmedLines)
                {
                    var poItem = activePoItems.First(item => item.Id == priorLine.PurchaseOrderItemId);
                    var product = await GetActiveProductAsync(tenantId, priorLine.ProductId, products);
                    var units = await ResolveReceiptUnitsAsync(
                        tenantId, poItem, priorLine.ProductId, priorLine.UnitOfMeasureId, product);
                    decimal priorQuantity = await ConvertReceiptQuantityAsync(
                        tenantId,
                        companyId,
                        priorLine.ProductId,
                        units.GrnUnitOfMeasureId,
                        units.PurchaseOrderUnitOfMeasureId,
                        priorLine.QuantityReceived);
                    exactReceivedByPoItem[poItem.Id] += priorQuantity;
                }

                foreach (var poItem in activePoItems)
                {
                    decimal receivedInThisGrn = grnQtyByPoItem.GetValueOrDefault(poItem.Id);
                    decimal receivedBeforeThisGrn = exactReceivedByPoItem[poItem.Id];
                    if (receivedInThisGrn == 0m)
                        continue;

                    decimal maxAllowable = poItem.Quantity - receivedBeforeThisGrn;
                    if (receivedInThisGrn > maxAllowable)
                    {
                        throw new InvalidOperationException(
                            $"Over-receipt rejected for item '{poItem.Id}'. Ordered: {poItem.Quantity}, Already Received: {receivedBeforeThisGrn}, Attempted in GRN: {receivedInThisGrn}. Over-receipt tolerance is 0%.");
                    }

                    decimal exactReceivedQuantity = receivedBeforeThisGrn + receivedInThisGrn;
                    exactReceivedByPoItem[poItem.Id] = exactReceivedQuantity;
                    decimal persistedReceivedQuantity = Math.Round(
                        exactReceivedQuantity,
                        4,
                        MidpointRounding.AwayFromZero);
                    if (persistedReceivedQuantity > poItem.Quantity)
                    {
                        throw new InvalidOperationException(
                            $"Receipt quantity for item '{poItem.Id}' cannot be represented within the remaining PO quantity at four-decimal persistence precision.");
                    }

                    if (receivedInThisGrn > 0)
                    {
                        if (persistedReceivedQuantity >= poItem.Quantity &&
                            exactReceivedQuantity < poItem.Quantity)
                        {
                            persistedReceivedQuantity = decimal.Truncate(exactReceivedQuantity * 10_000m) / 10_000m;
                        }

                        poItem.ReceivedQuantity = persistedReceivedQuantity;
                    }
                }

                // Check if all active lines on PO are fully fulfilled
                bool allFulfilled = activePoItems.All(item =>
                    (previouslyConfirmedLines.Any(line => line.PurchaseOrderItemId == item.Id) ||
                     grnQtyByPoItem.ContainsKey(item.Id))
                        ? exactReceivedByPoItem[item.Id] >= item.Quantity
                        : item.ReceivedQuantity >= item.Quantity);

                if (allFulfilled)
                {
                    po.Status = PurchaseOrderStatus.Fulfilled;
                }

                // Mark GRN as Confirmed
                grn.MarkConfirmed();

                // Post Inventory ledger for each stock-tracked line using existing IInventoryPostingService
                int sequence = 1;
                foreach (var line in grn.Lines)
                {
                    var product = await GetActiveProductAsync(tenantId, line.ProductId, products);

                    // Non-stock and service lines do NOT create inventory movements or track inventory balances
                    if (!product.IsStockTracked)
                    {
                        continue;
                    }

                    if (!product.UnitOfMeasureId.HasValue)
                        throw new InvalidOperationException($"Stock-tracked product '{product.ProductCode}' is missing its base unit of measure.");

                    var poItem = po.Items.First(i => i.Id == line.PurchaseOrderItemId && i.IsActive);
                    var units = await ResolveReceiptUnitsAsync(tenantId, poItem, line.ProductId, line.UnitOfMeasureId, product);
                    Guid targetUomId = product.UnitOfMeasureId.Value;
                    decimal baseQuantity = Math.Round(await ConvertReceiptQuantityAsync(
                        tenantId,
                        companyId,
                        line.ProductId,
                        units.GrnUnitOfMeasureId,
                        targetUomId,
                        line.QuantityReceived),
                        4,
                        MidpointRounding.AwayFromZero);
                    if (baseQuantity <= 0)
                        throw new InvalidOperationException("Converted stock quantity is below the supported four-decimal persistence precision.");

                    // Compute effective base unit cost preserving total line valuation
                    decimal effectiveUnitCost = baseQuantity > 0
                        ? Math.Round(line.LineTotal / baseQuantity, 4, MidpointRounding.AwayFromZero)
                        : line.UnitCost;

                    // Decrement on-order balance for received quantity in the corresponding warehouse
                    var onOrderBalance = await _context.InventoryBalances
                        .FirstOrDefaultAsync(b => b.TenantId == tenantId
                                                   && b.WarehouseId == line.WarehouseId
                                                   && b.ProductId == line.ProductId
                                                   && b.QuantityOnOrder > 0);

                    if (onOrderBalance != null)
                    {
                        decimal decrement = Math.Min(onOrderBalance.QuantityOnOrder, baseQuantity);
                        if (decrement > 0)
                        {
                            onOrderBalance.ApplyOnOrderChange(-decrement);
                        }
                    }

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
                        Quantity = baseQuantity,
                        UnitCost = effectiveUnitCost,
                        UnitOfMeasureId = targetUomId,
                        BatchNumber = line.BatchNumber,
                        SerialNumber = line.SerialNumber,
                        ManufacturingDate = line.ManufacturingDate,
                        ExpiryDate = line.ExpiryDate,
                        TransactionDate = grn.ReceiptDate,
                        IsQuarantine = line.IsQuarantine
                    };

                    await _postingService.PostMovementAsync(tenantId, postReq);
                }

                // Commit Domain Db Changes & Domain Transaction
                await _context.SaveChangesAsync();
                await domainTx.CommitAsync();

                return await MapToDtoAsync(tenantId, grn);
    }

    private async Task<(Product Product, Guid PurchaseOrderUnitOfMeasureId, Guid GrnUnitOfMeasureId)> ResolveReceiptUnitsAsync(
        Guid tenantId,
        PurchaseOrderItem poItem,
        Guid productId,
        Guid? requestedGrnUnitOfMeasureId,
        Product? product = null)
    {
        product ??= await _productRepository.GetByIdAsync(tenantId, productId)
            ?? throw new KeyNotFoundException($"Product '{productId}' not found in this tenant.");

        if (product.ProductId != poItem.ProductId || !product.IsActive)
            throw new InvalidOperationException($"Product '{product.ProductCode}' is inactive or does not match the PO item.");

        Guid purchaseOrderUnitId = poItem.UnitOfMeasureId ?? product.UnitOfMeasureId
            ?? throw new InvalidOperationException(
                $"PO item '{poItem.Id}' and product '{product.ProductCode}' do not define a unit of measure.");
        Guid grnUnitId = requestedGrnUnitOfMeasureId ?? purchaseOrderUnitId;

        await ValidateActiveUnitOfMeasureAsync(tenantId, purchaseOrderUnitId, "Purchase Order");
        await ValidateActiveUnitOfMeasureAsync(tenantId, grnUnitId, "Goods Receipt");

        if (product.IsStockTracked)
        {
            if (!product.UnitOfMeasureId.HasValue)
                throw new InvalidOperationException($"Stock-tracked product '{product.ProductCode}' is missing its base unit of measure.");

            await ValidateActiveUnitOfMeasureAsync(tenantId, product.UnitOfMeasureId.Value, "Product stock");
        }

        return (product, purchaseOrderUnitId, grnUnitId);
    }

    private async Task ValidateActiveUnitOfMeasureAsync(Guid tenantId, Guid unitOfMeasureId, string context)
    {
        if (_uomService == null)
            throw new InvalidOperationException("Unit of Measure service is required to validate goods receipts.");

        if (unitOfMeasureId == Guid.Empty)
            throw new InvalidOperationException($"{context} unit of measure is missing.");

        await _uomService.ConvertQuantityAsync(tenantId, unitOfMeasureId, unitOfMeasureId, 0m);
    }

    private async Task<decimal> ConvertReceiptQuantityAsync(
        Guid tenantId,
        Guid companyId,
        Guid productId,
        Guid fromUnitOfMeasureId,
        Guid toUnitOfMeasureId,
        decimal quantity)
    {
        if (quantity <= 0)
            throw new InvalidOperationException("Receipt quantity must be greater than zero.");
        decimal persistedQuantity = Math.Round(quantity, 4, MidpointRounding.AwayFromZero);
        if (persistedQuantity != quantity)
            throw new InvalidOperationException("Receipt quantity cannot exceed four-decimal persistence precision.");

        await ValidateActiveUnitOfMeasureAsync(tenantId, fromUnitOfMeasureId, "Goods Receipt");
        await ValidateActiveUnitOfMeasureAsync(tenantId, toUnitOfMeasureId, "Target");

        decimal convertedQuantity = quantity;
        if (fromUnitOfMeasureId != toUnitOfMeasureId)
        {
            if (_uomService == null)
                throw new InvalidOperationException("Unit of Measure service is required for receipt conversions.");

            var result = await _uomService.ConvertProductQuantityAsync(
                tenantId,
                productId,
                fromUnitOfMeasureId,
                toUnitOfMeasureId,
                quantity,
                ConversionContext.Purchase,
                companyId: companyId);
            convertedQuantity = result.ConvertedQuantity;
        }

        if (convertedQuantity <= 0)
            throw new InvalidOperationException("Converted receipt quantity must be greater than zero.");

        return convertedQuantity;
    }

    private async Task<(Guid WarehouseId, Guid LocationId)> GetDefaultWarehouseLocationAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId)
    {
        var warehouses = await _warehouseRepository.GetWarehousesAsync(tenantId);
        foreach (var warehouse in warehouses.Where(warehouse =>
                     warehouse.IsActive &&
                     warehouse.CompanyId == companyId &&
                     (!warehouse.BranchId.HasValue || warehouse.BranchId == branchId)))
        {
            var locations = await _warehouseRepository.GetLocationsAsync(tenantId, warehouse.WarehouseId);
            var location = locations.FirstOrDefault(candidate => candidate.IsActive);
            if (location != null)
                return (warehouse.WarehouseId, location.WarehouseLocationId);
        }

        throw new InvalidOperationException(
            "No active warehouse and location are available for this company and branch.");
    }

    private async Task ValidateReceiptWarehouseScopeAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid warehouseId,
        Guid locationId)
    {
        var warehouse = await _warehouseRepository.GetWarehouseByIdAsync(tenantId, warehouseId);
        if (warehouse == null ||
            !warehouse.IsActive ||
            warehouse.CompanyId != companyId ||
            (warehouse.BranchId.HasValue && warehouse.BranchId != branchId))
        {
            throw new InvalidOperationException(
                "The selected warehouse is not active or is outside the authorized company and branch scope.");
        }

        var location = await _warehouseRepository.GetLocationByIdAsync(tenantId, locationId);
        if (location == null ||
            !location.IsActive ||
            location.WarehouseId != warehouseId ||
            location.TenantId != tenantId)
        {
            throw new InvalidOperationException(
                "The selected location is not active in the selected authorized warehouse.");
        }
    }

    private async Task<decimal> CalculateReceiptValueAsync(
        Guid tenantId,
        Guid companyId,
        PurchaseOrderItem poItem,
        decimal receiptQuantityInPoUom)
    {
        decimal netLineValue = poItem.SubTotal - poItem.DiscountAmount;
        if (netLineValue < 0 || poItem.Quantity <= 0)
            throw new InvalidOperationException($"Purchase Order item '{poItem.Id}' has invalid valuation data.");

        decimal previouslyAllocated = await _context.GoodsReceiptNotes
            .Where(grn => grn.TenantId == tenantId &&
                          grn.CompanyId == companyId &&
                          grn.PurchaseOrderId == poItem.PurchaseOrderId &&
                          grn.Status == GoodsReceiptNoteStatus.Confirmed)
            .SelectMany(grn => grn.Lines)
            .Where(line => line.PurchaseOrderItemId == poItem.Id)
            .SumAsync(line => (decimal?)line.LineTotal) ?? 0m;

        decimal outstanding = poItem.Quantity - poItem.ReceivedQuantity;
        if (receiptQuantityInPoUom >= outstanding)
            return Math.Round(netLineValue - previouslyAllocated, 2, MidpointRounding.AwayFromZero);

        return Math.Round(
            netLineValue * receiptQuantityInPoUom / poItem.Quantity,
            2,
            MidpointRounding.AwayFromZero);
    }

    private async Task<Product> GetActiveProductAsync(
        Guid tenantId,
        Guid productId,
        IReadOnlyDictionary<Guid, Product> products)
    {
        Product? product = products.TryGetValue(productId, out var loadedProduct)
            ? loadedProduct
            : await _productRepository.GetByIdAsync(tenantId, productId);

        if (product == null || !product.IsActive)
            throw new KeyNotFoundException($"Product '{productId}' was not found or is inactive in this tenant.");

        return product;
    }


    private async Task<GoodsReceiptNoteDto> MapToDtoAsync(Guid tenantId, GoodsReceiptNote grn)
    {
        var uomCodes = new Dictionary<Guid, string>();
        if (_uomService != null)
        {
            foreach (var unitId in grn.Lines
                         .Where(line => line.UnitOfMeasureId.HasValue)
                         .Select(line => line.UnitOfMeasureId!.Value)
                         .Distinct())
            {
                var unit = await _uomService.GetUnitAsync(tenantId, unitId);
                if (unit != null)
                    uomCodes[unitId] = unit.Code;
            }
        }

        var dto = new GoodsReceiptNoteDto
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
                UnitOfMeasure = l.UnitOfMeasureId.HasValue &&
                                uomCodes.TryGetValue(l.UnitOfMeasureId.Value, out var uomCode)
                    ? uomCode
                    : null,
                BatchNumber = l.BatchNumber,
                SerialNumber = l.SerialNumber,
                ManufacturingDate = l.ManufacturingDate,
                ExpiryDate = l.ExpiryDate,
                IsQuarantine = l.IsQuarantine,
                Remarks = l.Remarks
            }).ToList()
        };
        return dto;
    }
}
