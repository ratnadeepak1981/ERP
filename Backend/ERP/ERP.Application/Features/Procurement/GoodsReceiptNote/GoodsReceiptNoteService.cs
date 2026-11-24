using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domain.Features.MasterData.Product;
using Domain.Features.MasterData.UnitOfMeasure;
using Domain.Features.Procurement.PurchaseOrder;
using ERP.Application.Features.Procurement;
using ERP.Domain.Features.Inventory;
using ERP.Domain.Features.MasterData.Warehouse;
using Microsoft.EntityFrameworkCore;


using Domain.Features.Procurement.GoodsReceiptNote;
namespace ERP.Application.Features.Procurement.GoodsReceiptNoteFeature;

public class GoodsReceiptNoteService : IGoodsReceiptNoteService
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, System.Threading.SemaphoreSlim> _draftLocks = new();

    private readonly IGoodsReceiptNoteRepository _grnRepository;
    private readonly IPurchaseOrderRepository _poRepository;
    private readonly IProductRepository _productRepository;
    private readonly IWarehouseRepository _warehouseRepository;
    private readonly IInventoryPostingService _postingService;

private readonly IProcurementPersistenceCoordinator _persistenceCoordinator;
    private readonly IUnitOfMeasureService? _uomService;

    public GoodsReceiptNoteService(
        IGoodsReceiptNoteRepository grnRepository,
        IPurchaseOrderRepository poRepository,
        IProductRepository productRepository,
        IWarehouseRepository warehouseRepository,
        IInventoryPostingService postingService,
IProcurementPersistenceCoordinator persistenceCoordinator,
        IUnitOfMeasureService? uomService = null)
    {
        _grnRepository = grnRepository;
        _poRepository = poRepository;
        _productRepository = productRepository;
        _warehouseRepository = warehouseRepository;
        _postingService = postingService;
_persistenceCoordinator = persistenceCoordinator;
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

    public async Task<GoodsReceiptNoteDto?> GetActiveDraftByPurchaseOrderIdAsync(
        Guid tenantId, Guid companyId, Guid branchId, Guid purchaseOrderId)
    {
        var draft = await _grnRepository.GetActiveDraftByPurchaseOrderIdAsync(tenantId, companyId, branchId, purchaseOrderId);
        return draft == null ? null : await MapToDtoAsync(tenantId, draft);
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
        var lockKey = $"{tenantId}:{companyId}:{branchId}:{purchaseOrderId}";
        var sem = _draftLocks.GetOrAdd(lockKey, _ => new System.Threading.SemaphoreSlim(1, 1));
        await sem.WaitAsync();
        try
        {
            // Safeguard 1: If an active editable Draft already exists for this PO, reopen and return it
            var existingDraft = await _grnRepository.GetActiveDraftByPurchaseOrderIdAsync(tenantId, companyId, branchId, purchaseOrderId);
            if (existingDraft != null)
            {
                return await MapToDtoAsync(tenantId, existingDraft);
            }

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

            if (!defaultWarehouseId.HasValue || !defaultLocationId.HasValue)
                throw new ArgumentException("Receiving warehouse and storage location must be provided to generate receipt lines from Purchase Order.");

            Guid targetWarehouseId = defaultWarehouseId.Value;
            Guid targetLocationId = defaultLocationId.Value;

            await ValidateReceiptWarehouseScopeAsync(
                tenantId, companyId, branchId, targetWarehouseId, targetLocationId);

            string normGrnNumber = grnNumber?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normGrnNumber))
            {
                normGrnNumber = $"GRN-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}"[..18].ToUpperInvariant();
            }

            if (await _grnRepository.ExistsGrnNumberAsync(tenantId, companyId, normGrnNumber))
                throw new InvalidOperationException($"GRN number '{normGrnNumber}' already exists for this company.");

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
        finally
        {
            sem.Release();
        }
    }

    public async Task<GoodsReceiptNoteDto> CreateManualDraftAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        CreateGoodsReceiptNoteRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        var lockKey = $"{tenantId}:{companyId}:{branchId}:{request.PurchaseOrderId}";
        var sem = _draftLocks.GetOrAdd(lockKey, _ => new System.Threading.SemaphoreSlim(1, 1));
        await sem.WaitAsync();
        try
        {
            // Safeguard 1: If an active editable Draft already exists for this PO, update and return it instead of creating duplicate
            var existingDraft = await _grnRepository.GetActiveDraftByPurchaseOrderIdAsync(tenantId, companyId, branchId, request.PurchaseOrderId);
            if (existingDraft != null)
            {
                var updateReq = new UpdateGoodsReceiptNoteRequest
                {
                    ReceiptDate = request.ReceiptDate,
                    DeliveryNoteNumber = request.DeliveryNoteNumber,
                    Remarks = request.Remarks,
                    Lines = request.Lines
                };
                return await UpdateDraftAsync(tenantId, companyId, branchId, existingDraft.GoodsReceiptNoteId, updateReq);
            }

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

            foreach (var lineReq in request.Lines)
            {
                var poItem = po.Items.FirstOrDefault(i => i.Id == lineReq.PurchaseOrderItemId && i.IsActive)
                    ?? throw new InvalidOperationException($"PO item '{lineReq.PurchaseOrderItemId}' not found on PO '{po.OrderNumber}'.");

                if (poItem.ProductId != lineReq.ProductId)
                    throw new InvalidOperationException($"Product mismatch for PO item '{poItem.Id}'.");

                if (lineReq.WarehouseId == Guid.Empty)
                    throw new ArgumentException("Receiving warehouse is required.");
                if (lineReq.WarehouseLocationId == Guid.Empty)
                    throw new ArgumentException("Storage location is required.");

                await ValidateReceiptWarehouseScopeAsync(
                    tenantId, companyId, branchId, lineReq.WarehouseId, lineReq.WarehouseLocationId);

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

                // CRITICAL BUSINESS RULE: Price protection - unit price is copied from approved PO line by server; client prices are ignored
                var line = GoodsReceiptNoteLine.Create(
                    grn.GoodsReceiptNoteId,
                    poItem.Id,
                    lineReq.ProductId,
                    lineReq.WarehouseId,
                    lineReq.WarehouseLocationId,
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
        finally
        {
            sem.Release();
        }
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
            var replacementLines = new List<GoodsReceiptNoteLine>();

            foreach (var lineReq in request.Lines)
            {
                var poItem = po.Items.FirstOrDefault(i => i.Id == lineReq.PurchaseOrderItemId && i.IsActive)
                    ?? throw new InvalidOperationException($"PO item '{lineReq.PurchaseOrderItemId}' not found on PO.");

                if (poItem.ProductId != lineReq.ProductId)
                    throw new InvalidOperationException($"Product mismatch for PO item '{poItem.Id}'.");

                if (lineReq.WarehouseId == Guid.Empty)
                    throw new ArgumentException("Receiving warehouse is required.");
                if (lineReq.WarehouseLocationId == Guid.Empty)
                    throw new ArgumentException("Storage location is required.");

                await ValidateReceiptWarehouseScopeAsync(
                    tenantId, companyId, branchId, lineReq.WarehouseId, lineReq.WarehouseLocationId);

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

                // CRITICAL BUSINESS RULE: Price protection - unit price is copied from approved PO line by server; client prices are ignored
                var line = GoodsReceiptNoteLine.Create(
                    grn.GoodsReceiptNoteId,
                    poItem.Id,
                    lineReq.ProductId,
                    lineReq.WarehouseId,
                    lineReq.WarehouseLocationId,
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

            await _persistenceCoordinator.ReplaceGrnLinesAsync(grn, replacementLines);
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
        Guid grnId,
        UpdateGoodsReceiptNoteRequest? updateRequest = null)
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
        await using var domainTx = await _persistenceCoordinator.BeginTransactionAsync();

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

        if (po.Status != PurchaseOrderStatus.Approved)
        {
            throw new InvalidOperationException($"Cannot receive against PO in status '{po.Status}'. Only Approved POs can be received.");
        }

        // If an update payload is provided (e.g. from Direct Post), persist amendments inside this single transaction
        if (updateRequest != null)
        {
            if (updateRequest.ReceiptDate.HasValue)
                grn.ReceiptDate = updateRequest.ReceiptDate.Value;

            grn.DeliveryNoteNumber = updateRequest.DeliveryNoteNumber?.Trim();
            grn.Remarks = updateRequest.Remarks?.Trim();

            if (updateRequest.Lines != null)
            {
                var replacementLines = new List<GoodsReceiptNoteLine>();

                foreach (var lineReq in updateRequest.Lines)
                {
                    var poItem = po.Items.FirstOrDefault(i => i.Id == lineReq.PurchaseOrderItemId && i.IsActive)
                        ?? throw new InvalidOperationException($"PO item '{lineReq.PurchaseOrderItemId}' not found on PO.");

                    if (poItem.ProductId != lineReq.ProductId)
                        throw new InvalidOperationException($"Product mismatch for PO item '{poItem.Id}'.");

                    if (lineReq.WarehouseId == Guid.Empty)
                        throw new InvalidOperationException("Receiving warehouse is required.");
                    if (lineReq.WarehouseLocationId == Guid.Empty)
                        throw new InvalidOperationException("Storage location is required.");

                    await ValidateReceiptWarehouseScopeAsync(
                        tenantId, companyId, branchId, lineReq.WarehouseId, lineReq.WarehouseLocationId);

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

                    var line = GoodsReceiptNoteLine.Create(
                        grn.GoodsReceiptNoteId,
                        poItem.Id,
                        lineReq.ProductId,
                        lineReq.WarehouseId,
                        lineReq.WarehouseLocationId,
                        lineReq.QuantityReceived,
                        grnUnitCost,
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

                await _persistenceCoordinator.ReplaceGrnLinesAsync(grn, replacementLines);
            }
        }

        if (grn.Lines.Count == 0)
        {
            throw new InvalidOperationException($"Cannot confirm GRN '{grn.GrnNumber}' because it contains no lines.");
        }

        if (!grn.Lines.Any(l => l.QuantityReceived > 0))
        {
            throw new InvalidOperationException($"Cannot confirm GRN '{grn.GrnNumber}' because all lines have zero received quantity.");
        }

        var productIds = grn.Lines.Select(l => l.ProductId).Distinct().ToList();
        var products = await _persistenceCoordinator.GetProductsByIdsAsync(tenantId, productIds);

        // Group received quantities by PO Item to validate zero over-receipt tolerance.
        // If GRN line has a different UOM than the PO item, convert received qty to the PO item's UOM.
        var grnQtyByPoItem = new Dictionary<Guid, decimal>();
        foreach (var line in grn.Lines)
        {
            if (line.QuantityReceived > 0)
            {
                await ValidateReceiptWarehouseScopeAsync(
                    tenantId, companyId, branchId, line.WarehouseId, line.WarehouseLocationId);
            }

            var poItem = po.Items.FirstOrDefault(i => i.Id == line.PurchaseOrderItemId && i.IsActive)
                ?? throw new InvalidOperationException($"PO item '{line.PurchaseOrderItemId}' not found or inactive.");

            var product = await GetActiveProductAsync(tenantId, line.ProductId, products);

            // Validate tracking requirements for stock-tracked products
            if (product.IsStockTracked && line.QuantityReceived > 0)
            {
                switch (product.TrackingMode)
                {
                    case TrackingMode.Batch:
                        if (string.IsNullOrWhiteSpace(line.BatchNumber))
                            throw new InvalidOperationException($"Batch number is required for batch-tracked product '{product.ProductCode}'.");
                        break;
                    case TrackingMode.Serial:
                        if (string.IsNullOrWhiteSpace(line.SerialNumber))
                            throw new InvalidOperationException($"Serial number is required for serial-tracked product '{product.ProductCode}'.");
                        break;
                    case TrackingMode.BatchAndSerial:
                        if (string.IsNullOrWhiteSpace(line.BatchNumber))
                            throw new InvalidOperationException($"Batch number is required for product '{product.ProductCode}'.");
                        if (string.IsNullOrWhiteSpace(line.SerialNumber))
                            throw new InvalidOperationException($"Serial number is required for product '{product.ProductCode}'.");
                        break;
                }
            }

            decimal qtyInPoUom = 0m;
            if (line.QuantityReceived > 0)
            {
                var units = await ResolveReceiptUnitsAsync(tenantId, poItem, line.ProductId, line.UnitOfMeasureId, product);
                qtyInPoUom = await ConvertReceiptQuantityAsync(
                    tenantId,
                    companyId,
                    line.ProductId,
                    units.GrnUnitOfMeasureId,
                    units.PurchaseOrderUnitOfMeasureId,
                    line.QuantityReceived);
            }

            if (!grnQtyByPoItem.ContainsKey(line.PurchaseOrderItemId))
                grnQtyByPoItem[line.PurchaseOrderItemId] = 0;
            grnQtyByPoItem[line.PurchaseOrderItemId] += qtyInPoUom;
        }

        var activePoItems = po.Items.Where(item => item.IsActive).ToList();
        var activePoItemIds = activePoItems.Select(item => item.Id).ToList();
        var previouslyConfirmedLines = await _persistenceCoordinator.GetPreviouslyConfirmedPoLinesAsync(tenantId, companyId, po.Id, grn.GoodsReceiptNoteId, activePoItemIds);

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

        // Resolve company base currency and PO exchange rate snapshot for inventory valuation
        string? baseCurrencyCode = po.BaseCurrencyCode;
        int baseDecimalPlaces = po.BaseCurrencyDecimalPlaces ?? 2;
        if (string.IsNullOrWhiteSpace(baseCurrencyCode))
        {
            var company = await _persistenceCoordinator.GetActiveCompanyWithBaseCurrencyAsync(tenantId, companyId);
            baseCurrencyCode = company?.BaseCurrencyCode ?? company?.BaseCurrency?.CurrencyCode;
        }

        decimal appliedRate = 1.0m;
        bool isForeignCurrency = !string.IsNullOrWhiteSpace(baseCurrencyCode) &&
            !string.IsNullOrWhiteSpace(po.Currency) &&
            !string.Equals(po.Currency, baseCurrencyCode, StringComparison.OrdinalIgnoreCase);

        if (isForeignCurrency)
        {
            if (!po.AppliedExchangeRate.HasValue || po.AppliedExchangeRate.Value <= 0)
            {
                throw new InvalidOperationException(
                    $"Purchase Order '{po.OrderNumber}' is in foreign currency '{po.Currency}' (Base: '{baseCurrencyCode}'), but AppliedExchangeRate is missing or invalid. Foreign-currency receipts cannot be confirmed without a valid positive exchange rate.");
            }
            appliedRate = po.AppliedExchangeRate.Value;
        }
        else
        {
            // Domestic purchases strictly use 1.0 to prevent stale exchange rate snapshots from corrupting base currency costs
            appliedRate = 1.0m;
        }

        // Post Inventory ledger for each stock-tracked line using existing IInventoryPostingService
        int sequence = 1;
        foreach (var line in grn.Lines)
        {
            if (line.QuantityReceived <= 0)
            {
                continue;
            }

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

            bool isExplicitFreeSample = (line.Remarks != null && line.Remarks.Contains("Free Sample", StringComparison.OrdinalIgnoreCase))
                || (grn.Remarks != null && grn.Remarks.Contains("Free Sample", StringComparison.OrdinalIgnoreCase));

            // Convert foreign-currency receipt value into company base currency using approved AppliedExchangeRate
            decimal lineTotalInBaseCurrency = Math.Round(line.LineTotal * appliedRate, baseDecimalPlaces, MidpointRounding.AwayFromZero);

            // Compute effective base unit cost preserving total line valuation
            decimal effectiveUnitCost = baseQuantity > 0
                ? Math.Round(lineTotalInBaseCurrency / baseQuantity, 4, MidpointRounding.AwayFromZero)
                : Math.Round(line.UnitCost * appliedRate, 4, MidpointRounding.AwayFromZero);

            if (effectiveUnitCost <= 0 && !isExplicitFreeSample)
            {
                if (product.StandardCost > 0)
                {
                    effectiveUnitCost = product.StandardCost;
                }
                else
                {
                    throw new InvalidOperationException(
                        "Inbound inventory receipt requires a valid positive cost rate. Zero-cost receipts are permitted only for explicitly identified free samples.");
                }
            }

            if (product.ValuationMethod == ValuationMethod.StandardCost && product.StandardCost > 0)
            {
                decimal variancePerUnit = effectiveUnitCost - product.StandardCost;
                decimal totalPpv = Math.Round(baseQuantity * variancePerUnit, 2, MidpointRounding.AwayFromZero);
                if (totalPpv != 0m)
                {
                    string ppvTag = $"[PPV: StandardCost={product.StandardCost:F4}, EffectiveCost={effectiveUnitCost:F4}, TotalVariance={totalPpv:F2}]";
                    line.Remarks = string.IsNullOrWhiteSpace(line.Remarks) ? ppvTag : $"{line.Remarks} {ppvTag}";
                }
            }

            await _persistenceCoordinator.DecrementOnOrderBalanceAsync(tenantId, line.WarehouseId, line.ProductId, baseQuantity);

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
                IsQuarantine = line.IsQuarantine,
                IsFreeSample = isExplicitFreeSample
            };

            await _postingService.PostMovementAsync(tenantId, postReq);
        }

        // Commit Domain Db Changes & Domain Transaction
        await _persistenceCoordinator.SaveChangesAsync();
        await _persistenceCoordinator.CommitTransactionAsync();

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
        if (warehouseId == Guid.Empty)
            throw new InvalidOperationException("Receiving warehouse is required.");
        if (locationId == Guid.Empty)
            throw new InvalidOperationException("Storage location is required.");

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

        decimal previouslyAllocated = await _persistenceCoordinator.GetPreviouslyAllocatedAmountAsync(tenantId, companyId, poItem.PurchaseOrderId, poItem.Id);

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
