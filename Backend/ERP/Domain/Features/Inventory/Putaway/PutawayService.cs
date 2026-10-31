using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domain.Features.Procurement.GoodsReceiptNote;
using Domain.Infrastructure.Persistence;
using ERP.Domain.Features.Inventory;
using ERP.Domain.Features.MasterData.Warehouse;
using Microsoft.EntityFrameworkCore;

namespace Domain.Features.Inventory.Putaway;

public class PutawayService : IPutawayService
{
    private readonly DomainDbContext _context;
    private readonly IPutawayRepository _putawayRepository;
    private readonly IInventoryPostingService _inventoryPostingService;

    public PutawayService(
        DomainDbContext context,
        IPutawayRepository putawayRepository,
        IInventoryPostingService inventoryPostingService)
    {
        _context = context;
        _putawayRepository = putawayRepository;
        _inventoryPostingService = inventoryPostingService;
    }

    public async Task<List<PutawayDto>> GetPutawaysAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid? warehouseId = null,
        PutawayStatus? status = null)
    {
        var records = await _putawayRepository.GetPutawaysAsync(tenantId, companyId, branchId, warehouseId, status);
        return await MapToDtosAsync(tenantId, records);
    }

    public async Task<PutawayDto?> GetPutawayByIdAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid putawayId)
    {
        var record = await _putawayRepository.GetByIdAsync(tenantId, companyId, branchId, putawayId);
        if (record == null) return null;
        var dtos = await MapToDtosAsync(tenantId, new List<Putaway> { record });
        return dtos.FirstOrDefault();
    }

    public async Task<List<EligibleSourceReceiptDto>> GetEligibleSourceReceiptsAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid? warehouseId = null)
    {
        var results = new List<EligibleSourceReceiptDto>();

        // 1. Confirmed Goods Receipt Notes
        var grnQuery = _context.GoodsReceiptNotes
            .Include(g => g.Lines)
            .Where(g => g.TenantId == tenantId &&
                        g.CompanyId == companyId &&
                        g.BranchId == branchId &&
                        g.Status == GoodsReceiptNoteStatus.Confirmed);

        if (warehouseId.HasValue && warehouseId.Value != Guid.Empty)
        {
            grnQuery = grnQuery.Where(g => g.Lines.Any(l => l.WarehouseId == warehouseId.Value));
        }

        var confirmedGrns = await grnQuery.ToListAsync();

        foreach (var grn in confirmedGrns)
        {
            var productIds = grn.Lines.Select(l => l.ProductId).Distinct().ToList();
            var stockTrackedProductIds = (await _context.Products
                .Where(p => p.TenantId == tenantId && productIds.Contains(p.ProductId) && p.IsStockTracked)
                .Select(p => p.ProductId)
                .ToListAsync())
                .ToHashSet();

            var eligibleLines = grn.Lines.Where(l => stockTrackedProductIds.Contains(l.ProductId)).ToList();
            if (!eligibleLines.Any()) continue;

            var putawayQuantities = await _putawayRepository.GetCompletedPutawayQuantitiesByReceiptLineAsync(tenantId, grn.GoodsReceiptNoteId);
            decimal totalReceived = eligibleLines.Sum(l => l.QuantityReceived);
            decimal totalPutaway = eligibleLines.Sum(l => putawayQuantities.GetValueOrDefault(l.GoodsReceiptNoteLineId, 0m));
            decimal outstanding = Math.Max(0m, totalReceived - totalPutaway);

            if (outstanding > 0m)
            {
                var firstLine = eligibleLines.FirstOrDefault();
                var wh = firstLine != null ? await _context.Warehouses.FindAsync(firstLine.WarehouseId) : null;

                results.Add(new EligibleSourceReceiptDto
                {
                    Id = grn.GoodsReceiptNoteId,
                    Number = grn.GrnNumber,
                    Type = PutawaySourceDocumentType.GoodsReceipt,
                    TypeName = "Goods Receipt Note",
                    Date = grn.ReceiptDate,
                    WarehouseId = firstLine?.WarehouseId ?? Guid.Empty,
                    WarehouseCode = wh?.WarehouseCode ?? "MAIN",
                    WarehouseName = wh?.WarehouseName ?? "Main Warehouse",
                    TotalLines = eligibleLines.Count,
                    TotalQuantity = totalReceived,
                    OutstandingQuantity = outstanding
                });
            }
        }

        // 2. Posted Stock Receipts
        var stockReceiptQuery = _context.InventoryTransactions
            .Where(t => t.TenantId == tenantId &&
                        t.SourceDocumentType == SourceDocumentType.StockAdjustment &&
                        t.MovementType == InventoryMovementType.StockAdjustmentIn &&
                        t.Quantity > 0);

        if (warehouseId.HasValue && warehouseId.Value != Guid.Empty)
        {
            stockReceiptQuery = stockReceiptQuery.Where(t => t.WarehouseId == warehouseId.Value);
        }

        var stockReceiptTxns = await stockReceiptQuery.ToListAsync();

        var groupedStock = stockReceiptTxns
            .GroupBy(t => t.SourceDocumentId)
            .ToList();

        foreach (var group in groupedStock)
        {
            Guid sourceDocId = group.Key;
            var txns = group.ToList();
            var first = txns.First();

            var putawayQuantities = await _putawayRepository.GetCompletedPutawayQuantitiesByReceiptLineAsync(tenantId, sourceDocId);
            decimal totalReceived = txns.Sum(t => t.Quantity);
            decimal totalPutaway = txns.Sum(t =>
            {
                Guid lineKey = t.SourceDocumentLineId != Guid.Empty ? t.SourceDocumentLineId : t.InventoryTransactionId;
                return putawayQuantities.GetValueOrDefault(lineKey, 0m);
            });
            decimal outstanding = Math.Max(0m, totalReceived - totalPutaway);

            if (outstanding > 0m)
            {
                var wh = await _context.Warehouses.FindAsync(first.WarehouseId);

                results.Add(new EligibleSourceReceiptDto
                {
                    Id = sourceDocId,
                    Number = first.TransactionNumber,
                    Type = PutawaySourceDocumentType.StockReceipt,
                    TypeName = "Stock Receipt",
                    Date = first.TransactionDate,
                    WarehouseId = first.WarehouseId,
                    WarehouseCode = wh?.WarehouseCode ?? "",
                    WarehouseName = wh?.WarehouseName ?? "",
                    TotalLines = txns.Count,
                    TotalQuantity = totalReceived,
                    OutstandingQuantity = outstanding
                });
            }
        }

        return results.OrderByDescending(r => r.Date).ToList();
    }

    public async Task<List<EligibleSourceReceiptLineDto>> GetEligibleReceiptLinesAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        PutawaySourceDocumentType docType,
        Guid sourceDocId)
    {
        var lines = new List<EligibleSourceReceiptLineDto>();

        if (docType == PutawaySourceDocumentType.GoodsReceipt)
        {
            var grn = await _context.GoodsReceiptNotes
                .Include(g => g.Lines)
                .FirstOrDefaultAsync(g => g.TenantId == tenantId &&
                                          g.CompanyId == companyId &&
                                          g.BranchId == branchId &&
                                          g.GoodsReceiptNoteId == sourceDocId);

            if (grn == null) return lines;

            var putawayQuantities = await _putawayRepository.GetCompletedPutawayQuantitiesByReceiptLineAsync(tenantId, sourceDocId);

            foreach (var grnLine in grn.Lines)
            {
                var product = await _context.Products.FindAsync(grnLine.ProductId);
                if (product != null && !product.IsStockTracked) continue;

                decimal alreadyPut = putawayQuantities.GetValueOrDefault(grnLine.GoodsReceiptNoteLineId, 0m);
                decimal outstanding = Math.Max(0m, grnLine.QuantityReceived - alreadyPut);

                var location = await _context.WarehouseLocations.FindAsync(grnLine.WarehouseLocationId);
                var uom = grnLine.UnitOfMeasureId.HasValue ? await _context.UnitsOfMeasure.FindAsync(grnLine.UnitOfMeasureId.Value) : null;

                lines.Add(new EligibleSourceReceiptLineDto
                {
                    SourceDocumentLineId = grnLine.GoodsReceiptNoteLineId,
                    ProductId = grnLine.ProductId,
                    ProductCode = product?.ProductCode ?? "",
                    ProductName = product?.ProductName ?? "",
                    UnitOfMeasureId = grnLine.UnitOfMeasureId ?? product?.UnitOfMeasureId,
                    UnitOfMeasure = uom?.Code ?? product?.UnitOfMeasure ?? "EA",
                    SourceWarehouseLocationId = grnLine.WarehouseLocationId,
                    SourceLocationCode = location?.LocationCode ?? "",
                    SourceLocationName = location?.LocationName ?? "",
                    ReceivedQuantity = grnLine.QuantityReceived,
                    PreviouslyPutawayQuantity = alreadyPut,
                    OutstandingQuantity = outstanding,
                    BatchNumber = grnLine.BatchNumber,
                    SerialNumber = grnLine.SerialNumber,
                    ExpiryDate = grnLine.ExpiryDate
                });
            }
        }
        else
        {
            var txns = await _context.InventoryTransactions
                .Where(t => t.TenantId == tenantId &&
                            t.SourceDocumentId == sourceDocId &&
                            t.MovementType == InventoryMovementType.StockAdjustmentIn)
                .ToListAsync();

            var putawayQuantities = await _putawayRepository.GetCompletedPutawayQuantitiesByReceiptLineAsync(tenantId, sourceDocId);

            foreach (var txn in txns)
            {
                Guid lineKey = txn.SourceDocumentLineId != Guid.Empty ? txn.SourceDocumentLineId : txn.InventoryTransactionId;
                decimal alreadyPut = putawayQuantities.GetValueOrDefault(lineKey, 0m);
                decimal outstanding = Math.Max(0m, txn.Quantity - alreadyPut);

                var product = await _context.Products.FindAsync(txn.ProductId);
                var location = await _context.WarehouseLocations.FindAsync(txn.WarehouseLocationId);
                var uom = txn.UnitOfMeasureId.HasValue ? await _context.UnitsOfMeasure.FindAsync(txn.UnitOfMeasureId.Value) : null;

                lines.Add(new EligibleSourceReceiptLineDto
                {
                    SourceDocumentLineId = lineKey,
                    ProductId = txn.ProductId,
                    ProductCode = product?.ProductCode ?? "",
                    ProductName = product?.ProductName ?? "",
                    UnitOfMeasureId = txn.UnitOfMeasureId ?? product?.UnitOfMeasureId,
                    UnitOfMeasure = uom?.Code ?? product?.UnitOfMeasure ?? "EA",
                    SourceWarehouseLocationId = txn.WarehouseLocationId,
                    SourceLocationCode = location?.LocationCode ?? "",
                    SourceLocationName = location?.LocationName ?? "",
                    ReceivedQuantity = txn.Quantity,
                    PreviouslyPutawayQuantity = alreadyPut,
                    OutstandingQuantity = outstanding,
                    BatchNumber = txn.BatchNumber,
                    SerialNumber = txn.SerialNumber,
                    ExpiryDate = txn.ExpiryDate
                });
            }
        }

        return lines;
    }

    public async Task<PutawayDto> CreateDraftAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        CreatePutawayDraftRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        if (request.WarehouseId == Guid.Empty) throw new ArgumentException("Warehouse is required.");
        if (request.SourceDocumentId == Guid.Empty) throw new ArgumentException("Source receipt document is required.");
        if (request.Lines == null || request.Lines.Count == 0) throw new ArgumentException("At least one line item is required for putaway.");

        var eligibleLines = await GetEligibleReceiptLinesAsync(tenantId, companyId, branchId, request.SourceDocumentType, request.SourceDocumentId);

        int year = (request.PutawayDate ?? DateTime.UtcNow).Year;
        string putawayNumber = await _putawayRepository.GenerateNextPutawayNumberAsync(tenantId, year);

        var putaway = Putaway.Create(
            tenantId,
            companyId,
            branchId,
            putawayNumber,
            request.SourceDocumentType,
            request.SourceDocumentId,
            request.SourceDocumentNumber ?? putawayNumber,
            request.WarehouseId,
            request.PutawayDate,
            request.Notes);

        var totalRequestedBySourceLine = request.Lines
            .GroupBy(l => l.SourceDocumentLineId.HasValue ? l.SourceDocumentLineId.Value.ToString() : l.ProductId.ToString())
            .ToDictionary(g => g.Key, g => g.Sum(l => l.PutawayQuantity));

        foreach (var lineReq in request.Lines)
        {
            if (lineReq.PutawayQuantity <= 0)
                throw new ArgumentException($"Putaway quantity must be greater than zero for product '{lineReq.ProductId}'.");

            var matchedEligible = eligibleLines.FirstOrDefault(e =>
                (lineReq.SourceDocumentLineId.HasValue && e.SourceDocumentLineId == lineReq.SourceDocumentLineId.Value) ||
                (e.ProductId == lineReq.ProductId));

            if (matchedEligible == null)
                throw new InvalidOperationException($"Line item for product '{lineReq.ProductId}' is not part of the source receipt.");

            string lineKey = lineReq.SourceDocumentLineId.HasValue ? lineReq.SourceDocumentLineId.Value.ToString() : lineReq.ProductId.ToString();
            decimal totalForSourceLine = totalRequestedBySourceLine[lineKey];
            if (totalForSourceLine > matchedEligible.OutstandingQuantity + 0.0001m)
                throw new InvalidOperationException($"Total putaway quantity ({totalForSourceLine}) exceeds outstanding quantity ({matchedEligible.OutstandingQuantity}) for item '{matchedEligible.ProductCode}'.");

            if ((lineReq.Allocations == null || lineReq.Allocations.Count == 0) && lineReq.DestinationWarehouseLocationId.HasValue)
            {
                lineReq.Allocations = new List<CreatePutawayAllocationRequest>
                {
                    new()
                    {
                        DestinationWarehouseLocationId = lineReq.DestinationWarehouseLocationId.Value,
                        AllocatedQuantity = lineReq.PutawayQuantity
                    }
                };
            }

            if (lineReq.Allocations == null || lineReq.Allocations.Count == 0)
                throw new InvalidOperationException($"At least one destination storage bin allocation is required for item '{matchedEligible.ProductCode}'.");

            decimal allocationSum = lineReq.Allocations.Sum(a => a.AllocatedQuantity);
            if (Math.Abs(allocationSum - lineReq.PutawayQuantity) > 0.0001m)
                throw new InvalidOperationException($"Destination allocations total ({allocationSum}) must equal the putaway quantity ({lineReq.PutawayQuantity}) for item '{matchedEligible.ProductCode}'.");

            // Validate destination locations
            foreach (var allocReq in lineReq.Allocations)
            {
                await ValidateDestinationLocationAsync(tenantId, request.WarehouseId, allocReq.DestinationWarehouseLocationId);
            }

            var line = PutawayLine.Create(
                putaway.PutawayId,
                lineReq.SourceDocumentLineId ?? matchedEligible.SourceDocumentLineId,
                lineReq.ProductId,
                matchedEligible.SourceWarehouseLocationId,
                matchedEligible.ReceivedQuantity,
                matchedEligible.PreviouslyPutawayQuantity,
                matchedEligible.OutstandingQuantity,
                lineReq.PutawayQuantity,
                lineReq.UnitOfMeasureId ?? matchedEligible.UnitOfMeasureId,
                lineReq.BatchNumber ?? matchedEligible.BatchNumber,
                lineReq.SerialNumber ?? matchedEligible.SerialNumber,
                lineReq.ExpiryDate ?? matchedEligible.ExpiryDate,
                lineReq.Notes);

            int seq = 1;
            foreach (var allocReq in lineReq.Allocations)
            {
                var alloc = PutawayLineAllocation.Create(
                    line.PutawayLineId,
                    allocReq.DestinationWarehouseLocationId,
                    allocReq.AllocatedQuantity,
                    seq++);
                line.AddAllocation(alloc);
            }

            putaway.AddLine(line);
        }

        await _putawayRepository.AddAsync(putaway);
        await _context.SaveChangesAsync();

        var dtos = await MapToDtosAsync(tenantId, new List<Putaway> { putaway });
        return dtos.First();
    }

    public async Task<PutawayDto> UpdateDraftAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid putawayId,
        UpdatePutawayDraftRequest request)
    {
        var putaway = await _putawayRepository.GetByIdAsync(tenantId, companyId, branchId, putawayId)
            ?? throw new KeyNotFoundException("Stores Putaway record not found.");

        if (putaway.Status != PutawayStatus.Draft)
            throw new InvalidOperationException($"Putaway '{putaway.PutawayNumber}' is in status '{putaway.Status}' and cannot be modified.");

        if (request.PutawayDate.HasValue)
            putaway.PutawayDate = request.PutawayDate.Value;

        if (request.Notes != null)
            putaway.Notes = request.Notes.Trim();

        if (request.Lines != null && request.Lines.Count > 0)
        {
            var eligibleLines = await GetEligibleReceiptLinesAsync(tenantId, companyId, branchId, putaway.SourceDocumentType, putaway.SourceDocumentId);

            // Remove existing lines and allocations
            _context.PutawayLineAllocations.RemoveRange(putaway.Lines.SelectMany(l => l.Allocations));
            _context.PutawayLines.RemoveRange(putaway.Lines);
            putaway.Lines.Clear();

            var totalRequestedBySourceLine = request.Lines
                .GroupBy(l => l.SourceDocumentLineId.HasValue ? l.SourceDocumentLineId.Value.ToString() : l.ProductId.ToString())
                .ToDictionary(g => g.Key, g => g.Sum(l => l.PutawayQuantity));

            foreach (var lineReq in request.Lines)
            {
                var matchedEligible = eligibleLines.FirstOrDefault(e =>
                    (lineReq.SourceDocumentLineId.HasValue && e.SourceDocumentLineId == lineReq.SourceDocumentLineId.Value) ||
                    (e.ProductId == lineReq.ProductId))
                    ?? throw new InvalidOperationException($"Line item for product '{lineReq.ProductId}' is not part of the source receipt.");

                string lineKey = lineReq.SourceDocumentLineId.HasValue ? lineReq.SourceDocumentLineId.Value.ToString() : lineReq.ProductId.ToString();
                decimal totalForSourceLine = totalRequestedBySourceLine[lineKey];
                if (totalForSourceLine > matchedEligible.OutstandingQuantity + 0.0001m)
                    throw new InvalidOperationException($"Total putaway quantity ({totalForSourceLine}) exceeds outstanding quantity ({matchedEligible.OutstandingQuantity}) for item '{matchedEligible.ProductCode}'.");

                if ((lineReq.Allocations == null || lineReq.Allocations.Count == 0) && lineReq.DestinationWarehouseLocationId.HasValue)
                {
                    lineReq.Allocations = new List<CreatePutawayAllocationRequest>
                    {
                        new()
                        {
                            DestinationWarehouseLocationId = lineReq.DestinationWarehouseLocationId.Value,
                            AllocatedQuantity = lineReq.PutawayQuantity
                        }
                    };
                }

                if (lineReq.Allocations == null || lineReq.Allocations.Count == 0)
                    throw new InvalidOperationException($"At least one destination storage bin allocation is required for item '{matchedEligible.ProductCode}'.");

                decimal allocationSum = lineReq.Allocations.Sum(a => a.AllocatedQuantity);
                if (Math.Abs(allocationSum - lineReq.PutawayQuantity) > 0.0001m)
                    throw new InvalidOperationException($"Destination allocations total ({allocationSum}) must equal the line putaway quantity ({lineReq.PutawayQuantity}).");

                foreach (var allocReq in lineReq.Allocations)
                {
                    await ValidateDestinationLocationAsync(tenantId, putaway.WarehouseId, allocReq.DestinationWarehouseLocationId);
                }

                var line = PutawayLine.Create(
                    putaway.PutawayId,
                    lineReq.SourceDocumentLineId ?? matchedEligible.SourceDocumentLineId,
                    lineReq.ProductId,
                    matchedEligible.SourceWarehouseLocationId,
                    matchedEligible.ReceivedQuantity,
                    matchedEligible.PreviouslyPutawayQuantity,
                    matchedEligible.OutstandingQuantity,
                    lineReq.PutawayQuantity,
                    lineReq.UnitOfMeasureId ?? matchedEligible.UnitOfMeasureId,
                    lineReq.BatchNumber ?? matchedEligible.BatchNumber,
                    lineReq.SerialNumber ?? matchedEligible.SerialNumber,
                    lineReq.ExpiryDate ?? matchedEligible.ExpiryDate,
                    lineReq.Notes);

                int seq = 1;
                foreach (var allocReq in lineReq.Allocations)
                {
                    var alloc = PutawayLineAllocation.Create(
                        line.PutawayLineId,
                        allocReq.DestinationWarehouseLocationId,
                        allocReq.AllocatedQuantity,
                        seq++);
                    line.AddAllocation(alloc);
                }

                putaway.AddLine(line);
            }
        }

        await _context.SaveChangesAsync();
        var dtos = await MapToDtosAsync(tenantId, new List<Putaway> { putaway });
        return dtos.First();
    }

    public async Task<PutawayDto> ConfirmPutawayAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid putawayId)
    {
        var putaway = await _putawayRepository.GetByIdAsync(tenantId, companyId, branchId, putawayId)
            ?? throw new KeyNotFoundException("Stores Putaway record not found.");

        if (putaway.Status != PutawayStatus.Draft)
            throw new InvalidOperationException($"Putaway '{putaway.PutawayNumber}' cannot be confirmed because it is in status '{putaway.Status}'. Only Draft putaways can be confirmed.");

        if (putaway.Lines.Count == 0)
            throw new InvalidOperationException("At least one line item is required to confirm putaway.");

        // Atomic transaction wrapping Putaway confirmation and all inventory transfers
        var executionStrategy = _context.Database.CreateExecutionStrategy();

        await executionStrategy.ExecuteAsync(async () =>
        {
            using var dbTransaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Re-verify authoritative outstanding quantities immediately before posting
                var eligibleLines = await GetEligibleReceiptLinesAsync(tenantId, companyId, branchId, putaway.SourceDocumentType, putaway.SourceDocumentId);

                var sourceDocTypeForPosting = putaway.SourceDocumentType == PutawaySourceDocumentType.GoodsReceipt
                    ? SourceDocumentType.GoodsReceiptNote
                    : SourceDocumentType.StockAdjustment;

                foreach (var line in putaway.Lines)
                {
                    var matchedEligible = eligibleLines.FirstOrDefault(e =>
                        (line.SourceDocumentLineId.HasValue && e.SourceDocumentLineId == line.SourceDocumentLineId.Value) ||
                        (e.ProductId == line.ProductId))
                        ?? throw new InvalidOperationException($"Line item for product '{line.ProductId}' is not part of the source receipt.");

                    if (line.PutawayQuantity > matchedEligible.OutstandingQuantity)
                        throw new InvalidOperationException($"Putaway quantity ({line.PutawayQuantity}) exceeds authoritative outstanding quantity ({matchedEligible.OutstandingQuantity}) for item '{matchedEligible.ProductCode}'.");

                    if (line.Allocations.Count == 0)
                        throw new InvalidOperationException($"No destination bin allocation found for line '{matchedEligible.ProductCode}'.");

                    decimal allocTotal = line.Allocations.Sum(a => a.AllocatedQuantity);
                    if (Math.Abs(allocTotal - line.PutawayQuantity) > 0.0001m)
                        throw new InvalidOperationException($"Allocations total ({allocTotal}) does not match line putaway quantity ({line.PutawayQuantity}) for '{matchedEligible.ProductCode}'.");

                    // Post inventory transfer for each allocation
                    foreach (var alloc in line.Allocations)
                    {
                        await ValidateDestinationLocationAsync(tenantId, putaway.WarehouseId, alloc.DestinationWarehouseLocationId);

                        var transferRequest = new PostTransferMovementRequest
                        {
                            SourceDocumentType = sourceDocTypeForPosting,
                            SourceDocumentId = putaway.PutawayId,
                            SourceDocumentLineId = alloc.PutawayLineAllocationId,
                            ProductId = line.ProductId,
                            SourceWarehouseId = putaway.WarehouseId,
                            SourceLocationId = line.SourceWarehouseLocationId,
                            DestinationWarehouseId = putaway.WarehouseId,
                            DestinationLocationId = alloc.DestinationWarehouseLocationId,
                            Quantity = alloc.AllocatedQuantity,
                            UnitOfMeasureId = line.UnitOfMeasureId,
                            BatchNumber = line.BatchNumber,
                            SerialNumber = line.SerialNumber,
                            ExpiryDate = line.ExpiryDate,
                            TransactionDate = putaway.PutawayDate
                        };

                        var (outflowTxn, inflowTxn) = await _inventoryPostingService.PostTransferAsync(tenantId, transferRequest);
                        alloc.InventoryTransactionId = inflowTxn.InventoryTransactionId;
                    }
                }

                putaway.Complete();
                await _context.SaveChangesAsync();
                await dbTransaction.CommitAsync();
            }
            catch
            {
                await dbTransaction.RollbackAsync();
                throw;
            }
        });

        var dtos = await MapToDtosAsync(tenantId, new List<Putaway> { putaway });
        return dtos.First();
    }

    public async Task<PutawayDto> CancelDraftAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid putawayId)
    {
        var putaway = await _putawayRepository.GetByIdAsync(tenantId, companyId, branchId, putawayId)
            ?? throw new KeyNotFoundException("Stores Putaway record not found.");

        putaway.Cancel();
        await _context.SaveChangesAsync();

        var dtos = await MapToDtosAsync(tenantId, new List<Putaway> { putaway });
        return dtos.First();
    }

    private async Task ValidateDestinationLocationAsync(Guid tenantId, Guid warehouseId, Guid destinationLocationId)
    {
        var destLoc = await _context.WarehouseLocations.AsNoTracking()
            .FirstOrDefaultAsync(l => l.TenantId == tenantId && l.WarehouseLocationId == destinationLocationId && l.IsActive)
            ?? throw new KeyNotFoundException($"Destination warehouse location '{destinationLocationId}' not found or is inactive.");

        if (destLoc.WarehouseId != warehouseId)
            throw new InvalidOperationException($"Destination location '{destLoc.LocationCode}' does not belong to the selected warehouse.");

        if (destLoc.LocationTypeId.HasValue)
        {
            var locType = await _context.LocationTypes.AsNoTracking()
                .FirstOrDefaultAsync(t => t.TenantId == tenantId && t.LocationTypeId == destLoc.LocationTypeId.Value);

            if (locType != null)
            {
                if (!locType.CanStoreInventory)
                    throw new InvalidOperationException($"Destination location '{destLoc.LocationCode}' cannot store inventory.");

                if (!locType.CanPutAway)
                    throw new InvalidOperationException($"Destination location '{destLoc.LocationCode}' is not configured to receive putaways (CanPutAway is disabled).");
            }
        }
    }

    private async Task<List<PutawayDto>> MapToDtosAsync(Guid tenantId, List<Putaway> records)
    {
        var dtos = new List<PutawayDto>();
        if (records.Count == 0) return dtos;

        var warehouseIds = records.Select(r => r.WarehouseId).Distinct().ToList();
        var warehouses = await _context.Warehouses
            .Where(w => warehouseIds.Contains(w.WarehouseId))
            .ToDictionaryAsync(w => w.WarehouseId, w => w);

        var productIds = records.SelectMany(r => r.Lines).Select(l => l.ProductId).Distinct().ToList();
        var products = await _context.Products
            .Where(p => productIds.Contains(p.ProductId))
            .ToDictionaryAsync(p => p.ProductId, p => p);

        var locIds = records.SelectMany(r => r.Lines).Select(l => l.SourceWarehouseLocationId)
            .Concat(records.SelectMany(r => r.Lines).SelectMany(l => l.Allocations).Select(a => a.DestinationWarehouseLocationId))
            .Distinct().ToList();
        var locations = await _context.WarehouseLocations
            .Where(l => locIds.Contains(l.WarehouseLocationId))
            .ToDictionaryAsync(l => l.WarehouseLocationId, l => l);

        var uomIds = records.SelectMany(r => r.Lines).Where(l => l.UnitOfMeasureId.HasValue).Select(l => l.UnitOfMeasureId!.Value).Distinct().ToList();
        var uoms = await _context.UnitsOfMeasure
            .Where(u => uomIds.Contains(u.UnitOfMeasureId))
            .ToDictionaryAsync(u => u.UnitOfMeasureId, u => u);

        foreach (var r in records)
        {
            var wh = warehouses.GetValueOrDefault(r.WarehouseId);

            var dto = new PutawayDto
            {
                PutawayId = r.PutawayId,
                TenantId = r.TenantId,
                CompanyId = r.CompanyId,
                BranchId = r.BranchId,
                PutawayNumber = r.PutawayNumber,
                PutawayDate = r.PutawayDate,
                SourceDocumentType = r.SourceDocumentType,
                SourceDocumentTypeName = r.SourceDocumentType == PutawaySourceDocumentType.StockReceipt ? "Stock Receipt" : "Goods Receipt Note",
                SourceDocumentId = r.SourceDocumentId,
                SourceDocumentNumber = r.SourceDocumentNumber,
                WarehouseId = r.WarehouseId,
                WarehouseCode = wh?.WarehouseCode ?? "",
                WarehouseName = wh?.WarehouseName ?? "",
                Status = r.Status,
                StatusName = r.Status.ToString(),
                Notes = r.Notes,
                TotalLines = r.Lines.Count,
                TotalQuantity = r.Lines.Sum(l => l.PutawayQuantity),
                CreatedAt = r.CreatedAt
            };

            foreach (var l in r.Lines)
            {
                var prod = products.GetValueOrDefault(l.ProductId);
                var srcLoc = locations.GetValueOrDefault(l.SourceWarehouseLocationId);
                var uom = l.UnitOfMeasureId.HasValue ? uoms.GetValueOrDefault(l.UnitOfMeasureId.Value) : null;

                var lDto = new PutawayLineDto
                {
                    PutawayLineId = l.PutawayLineId,
                    PutawayId = l.PutawayId,
                    SourceDocumentLineId = l.SourceDocumentLineId,
                    ProductId = l.ProductId,
                    ProductCode = prod?.ProductCode ?? "",
                    ProductName = prod?.ProductName ?? "",
                    UnitOfMeasureId = l.UnitOfMeasureId ?? prod?.UnitOfMeasureId,
                    UnitOfMeasure = uom?.Code ?? prod?.UnitOfMeasure ?? "EA",
                    SourceWarehouseLocationId = l.SourceWarehouseLocationId,
                    SourceLocationCode = srcLoc?.LocationCode ?? "",
                    ReceivedQuantity = l.ReceivedQuantity,
                    PreviouslyPutawayQuantity = l.PreviouslyPutawayQuantity,
                    OutstandingQuantity = l.OutstandingQuantity,
                    PutawayQuantity = l.PutawayQuantity,
                    BatchNumber = l.BatchNumber,
                    SerialNumber = l.SerialNumber,
                    ExpiryDate = l.ExpiryDate,
                    Notes = l.Notes
                };

                foreach (var a in l.Allocations)
                {
                    var dstLoc = locations.GetValueOrDefault(a.DestinationWarehouseLocationId);

                    lDto.Allocations.Add(new PutawayLineAllocationDto
                    {
                        PutawayLineAllocationId = a.PutawayLineAllocationId,
                        PutawayLineId = a.PutawayLineId,
                        DestinationWarehouseLocationId = a.DestinationWarehouseLocationId,
                        DestinationLocationCode = dstLoc?.LocationCode ?? "",
                        DestinationLocationName = dstLoc?.LocationName ?? "",
                        AllocatedQuantity = a.AllocatedQuantity,
                        MovementSequence = a.MovementSequence,
                        InventoryTransactionId = a.InventoryTransactionId
                    });
                }

                dto.Lines.Add(lDto);
            }

            dtos.Add(dto);
        }

        return dtos;
    }
}
