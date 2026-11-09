using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Domain.Features.MasterData.Product;
using Domain.Features.MasterData.UnitOfMeasure;
using Domain.Infrastructure.Persistence;
using ERP.Domain.Features.MasterData.Warehouse;
using ERP.Domain.Features.Inventory.Valuation;
using Microsoft.EntityFrameworkCore;

namespace ERP.Domain.Features.Inventory.StockCount;

public class StockCountService : IStockCountService
{
    private readonly DomainDbContext _context;
    private readonly IInventoryService _inventoryService;
    private readonly IUnitOfMeasureService _unitOfMeasureService;
    private readonly IInventoryValuationService _valuationService;

    public StockCountService(
        DomainDbContext context,
        IInventoryService inventoryService,
        IUnitOfMeasureService unitOfMeasureService,
        IInventoryValuationService valuationService)
    {
        _context = context;
        _inventoryService = inventoryService;
        _unitOfMeasureService = unitOfMeasureService;
        _valuationService = valuationService;
    }

    public async Task<StockCountDto> CreateDraftAsync(Guid tenantId, CreateStockCountDraftRequest request)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        if (request.WarehouseId == Guid.Empty)
            throw new ArgumentException("Warehouse is required.", nameof(request.WarehouseId));

        if (request.WarehouseLocationId == Guid.Empty)
            throw new ArgumentException("Warehouse location is required.", nameof(request.WarehouseLocationId));

        if (request.CompanyId == Guid.Empty)
            throw new ArgumentException("Company is required.", nameof(request.CompanyId));

        if (request.Lines == null || request.Lines.Count == 0)
            throw new ArgumentException("At least one physical count line is required.");

        var company = await _context.Companies
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == request.CompanyId && c.IsActive)
            ?? throw new KeyNotFoundException("Company not found or is inactive.");

        var warehouse = await _context.Warehouses
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.TenantId == tenantId && w.WarehouseId == request.WarehouseId && w.IsActive)
            ?? throw new KeyNotFoundException("Warehouse not found or is inactive.");
        if (warehouse.CompanyId.HasValue && warehouse.CompanyId.Value != company.Id)
            throw new InvalidOperationException("Warehouse does not belong to the selected company.");

        var location = await _context.WarehouseLocations
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.TenantId == tenantId && l.WarehouseLocationId == request.WarehouseLocationId && l.IsActive)
            ?? throw new KeyNotFoundException("Warehouse location not found or is inactive.");

        if (location.WarehouseId != request.WarehouseId)
            throw new InvalidOperationException("Location does not belong to the warehouse selected for the stock count.");

        var count = StockCount.Create(
            tenantId,
            request.CompanyId,
            request.WarehouseId,
            request.WarehouseLocationId,
            string.IsNullOrWhiteSpace(request.CountNumber) ? $"SC-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..32] : request.CountNumber.Trim(),
            request.CountDate,
            request.Remarks);

        var productIds = request.Lines.Select(x => x.ProductId).Distinct().ToList();
        if (productIds.Any(x => x == Guid.Empty))
            throw new ArgumentException("Product is required for each count line.");

        var products = await _context.Products
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId && productIds.Contains(p.ProductId) && p.IsActive)
            .ToDictionaryAsync(p => p.ProductId);

        var observations = new Dictionary<InventoryIdentity, PhysicalObservation>(InventoryIdentityComparer.Instance);
        var observedSerials = new Dictionary<Guid, HashSet<string>>();
        foreach (var lineRequest in request.Lines)
        {
            if (!products.TryGetValue(lineRequest.ProductId, out var product))
                throw new KeyNotFoundException($"Product '{lineRequest.ProductId}' not found or is inactive.");

            if (!product.IsStockTracked)
                throw new InvalidOperationException($"Product '{product.ProductCode}' is not configured as stock tracked.");

            if (lineRequest.WarehouseLocationId != Guid.Empty
                && lineRequest.WarehouseLocationId != request.WarehouseLocationId)
                throw new InvalidOperationException("All count lines must use the selected warehouse location.");

            if (lineRequest.PhysicalQuantity < 0)
                throw new ArgumentOutOfRangeException(nameof(lineRequest.PhysicalQuantity), "Physical quantity cannot be negative.");
            if (lineRequest.UnitCost < 0)
                throw new ArgumentOutOfRangeException(nameof(lineRequest.UnitCost), "Unit cost cannot be negative.");

            ValidateTrackingIdentity(product, lineRequest.BatchNumber, lineRequest.SerialNumber, 0m);
            if (lineRequest.PhysicalQuantity == 0
                && ((product.TrackingMode == TrackingMode.Batch && string.IsNullOrWhiteSpace(lineRequest.BatchNumber))
                    || (product.TrackingMode == TrackingMode.Serial && string.IsNullOrWhiteSpace(lineRequest.SerialNumber))
                    || (product.TrackingMode == TrackingMode.BatchAndSerial
                        && (string.IsNullOrWhiteSpace(lineRequest.BatchNumber) || string.IsNullOrWhiteSpace(lineRequest.SerialNumber)))))
                continue;

            var baseUnitId = product.UnitOfMeasureId
                ?? throw new InvalidOperationException($"Product '{product.ProductCode}' has no base unit of measure configured.");
            var enteredUnitId = lineRequest.UnitOfMeasureId ?? baseUnitId;
            var unit = await _context.UnitsOfMeasure
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.TenantId == tenantId && u.UnitOfMeasureId == enteredUnitId && u.IsActive)
                ?? throw new KeyNotFoundException("Unit of measure not found for the product.");

            var physicalBase = await ConvertToBaseAsync(tenantId, product, lineRequest.PhysicalQuantity, enteredUnitId, baseUnitId, company.Id);
            var identity = CreateIdentity(product, lineRequest.BatchNumber, lineRequest.SerialNumber);
            if ((product.TrackingMode == TrackingMode.Serial || product.TrackingMode == TrackingMode.BatchAndSerial)
                && !string.IsNullOrWhiteSpace(lineRequest.SerialNumber))
            {
                if (!observedSerials.TryGetValue(product.ProductId, out var serials))
                {
                    serials = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    observedSerials.Add(product.ProductId, serials);
                }

                if (!serials.Add(lineRequest.SerialNumber.Trim()))
                    throw new InvalidOperationException($"Serial '{lineRequest.SerialNumber.Trim()}' is counted more than once for product '{product.ProductCode}'.");
            }

            if (observations.TryGetValue(identity, out var existingObservation))
            {
                if (product.TrackingMode == TrackingMode.Serial || product.TrackingMode == TrackingMode.BatchAndSerial)
                    throw new InvalidOperationException($"Serial '{identity.SerialNumber}' is counted more than once.");
                if (existingObservation.UnitOfMeasureId != enteredUnitId)
                    throw new InvalidOperationException("Repeated physical count lines for one inventory identity must use the same unit of measure.");

                existingObservation.Add(
                    physicalBase,
                    lineRequest.PhysicalQuantity,
                    lineRequest.UnitCost,
                    lineRequest.BatchNumber?.Trim() ?? string.Empty,
                    lineRequest.SerialNumber?.Trim() ?? string.Empty,
                    lineRequest.ManufacturingDate,
                    lineRequest.ExpiryDate,
                    lineRequest.Remarks);
            }
            else
            {
                observations.Add(identity, new PhysicalObservation(
                    physicalBase,
                    lineRequest.PhysicalQuantity,
                    enteredUnitId,
                    unit.Code,
                    lineRequest.UnitCost,
                    lineRequest.BatchNumber?.Trim() ?? string.Empty,
                    lineRequest.SerialNumber?.Trim() ?? string.Empty,
                    lineRequest.ManufacturingDate,
                    lineRequest.ExpiryDate,
                    lineRequest.Remarks));
            }
        }

        var balances = await _context.InventoryBalances
            .AsNoTracking()
            .Where(b => b.TenantId == tenantId
                        && b.WarehouseId == request.WarehouseId
                        && b.WarehouseLocationId == request.WarehouseLocationId
                        && productIds.Contains(b.ProductId)
                        && b.QuantityOnHand != 0)
            .ToListAsync();

        var expectedQuantities = balances
            .GroupBy(balance =>
            {
                var product = products[balance.ProductId];
                ValidateTrackingIdentity(product, balance.BatchNumber, balance.SerialNumber, balance.QuantityOnHand);
                return CreateIdentity(product, balance.BatchNumber, balance.SerialNumber);
            }, InventoryIdentityComparer.Instance)
            .ToDictionary(group => group.Key, group => group.Sum(balance => balance.QuantityOnHand));

        var batchNumbers = balances
            .Select(x => x.BatchNumber)
            .Concat(observations.Values.Select(x => x.BatchNumber))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .ToList();
        var batchDetailList = batchNumbers.Count == 0
            ? new List<ERP.Domain.Features.Inventory.Batch.ProductBatch>()
            : await _context.ProductBatches
                .AsNoTracking()
                .Where(x => x.TenantId == tenantId && batchNumbers.Contains(x.BatchNumber))
                .ToListAsync();
        var batchIdentities = balances
            .Select(x => new InventoryBatchIdentity(x.ProductId, x.BatchNumber))
            .Concat(observations.Keys.Select(x => new InventoryBatchIdentity(x.ProductId, x.BatchNumber)))
            .Where(x => !string.IsNullOrWhiteSpace(x.BatchNumber))
            .Distinct(InventoryBatchIdentityComparer.Instance);
        foreach (var identity in batchIdentities)
        {
            if (batchDetailList.Any(x =>
                    StringComparer.OrdinalIgnoreCase.Equals(x.BatchNumber.Trim(), identity.BatchNumber)
                    && x.ProductId != identity.ProductId))
                throw new InvalidOperationException($"Batch '{identity.BatchNumber}' is already associated with another product.");
        }

        var matchingBatchDetails = batchDetailList
            .Where(x => productIds.Contains(x.ProductId))
            .GroupBy(x => new InventoryBatchIdentity(x.ProductId, x.BatchNumber), InventoryBatchIdentityComparer.Instance)
            .ToList();
        if (matchingBatchDetails.Any(x => x.Count() > 1))
            throw new InvalidOperationException("The inventory snapshot contains ambiguous product batch identifiers.");
        var batchDetails = matchingBatchDetails.ToDictionary(
            x => x.Key,
            x => x.Single(),
            InventoryBatchIdentityComparer.Instance);
        var identityNames = new Dictionary<InventoryIdentity, (string BatchNumber, string SerialNumber)>(InventoryIdentityComparer.Instance);
        foreach (var balance in balances)
        {
            var identity = CreateIdentity(products[balance.ProductId], balance.BatchNumber, balance.SerialNumber);
            identityNames.TryAdd(identity, (balance.BatchNumber, balance.SerialNumber));
        }
        foreach (var observation in observations)
        {
            identityNames.TryAdd(
                observation.Key,
                (observation.Value.BatchNumber, observation.Value.SerialNumber));
        }

        var identities = expectedQuantities.Keys
            .Concat(observations.Keys)
            .Distinct(InventoryIdentityComparer.Instance)
            .OrderBy(x => x.ProductId)
            .ThenBy(x => x.BatchNumber, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.SerialNumber, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var identity in identities)
        {
            var product = products[identity.ProductId];
            var expectedBase = expectedQuantities.GetValueOrDefault(identity);
            observations.TryGetValue(identity, out var observation);
            batchDetails.TryGetValue(new InventoryBatchIdentity(identity.ProductId, identity.BatchNumber), out var batch);
            identityNames.TryGetValue(identity, out var identityName);

            var line = StockCountLine.Create(
                product.ProductId,
                request.WarehouseLocationId,
                product.UnitOfMeasureId!.Value,
                product.ProductCode,
                product.ProductName,
                product.UnitOfMeasure,
                expectedBase,
                observation?.PhysicalQuantityBase ?? 0m,
                identityName.BatchNumber,
                identityName.SerialNumber,
                batch?.ManufacturingDate ?? observation?.ManufacturingDate,
                batch?.ExpiryDate ?? observation?.ExpiryDate,
                observation?.UnitCost ?? 0m,
                observation?.Remarks);

            line.ExpectedQuantityEntered = expectedBase;
            line.PhysicalQuantityEntered = observation?.PhysicalQuantityEntered ?? 0m;
            line.UnitOfMeasureId = observation?.UnitOfMeasureId ?? product.UnitOfMeasureId.Value;
            line.UnitOfMeasureCode = observation?.UnitOfMeasureCode ?? product.UnitOfMeasure;
            line.StockCountId = count.StockCountId;
            line.TenantId = tenantId;
            count.Lines.Add(line);
        }

        count.UpdateDraft();
        count.SnapshotFingerprint = CalculateFingerprint(count);

        await _context.Set<StockCount>().AddAsync(count);
        await _context.SaveChangesAsync();

        return await MapAsync(count);
    }

    public async Task<List<StockCountDto>> GetCountsAsync(Guid tenantId, Guid? warehouseId = null, Guid? warehouseLocationId = null, StockCountStatus? status = null)
    {
        var query = _context.Set<StockCount>()
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .AsQueryable();

        if (warehouseId.HasValue && warehouseId.Value != Guid.Empty)
            query = query.Where(x => x.WarehouseId == warehouseId.Value);

        if (warehouseLocationId.HasValue && warehouseLocationId.Value != Guid.Empty)
            query = query.Where(x => x.WarehouseLocationId == warehouseLocationId.Value);

        if (status.HasValue)
            query = query.Where(x => x.Status == status.Value);

        var items = await query
            .Include(x => x.Lines)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();

        return await MapManyAsync(items);
    }

    public async Task<StockCountDto?> GetByIdAsync(Guid tenantId, Guid stockCountId)
    {
        var count = await _context.Set<StockCount>()
            .AsNoTracking()
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.StockCountId == stockCountId);

        return count == null ? null : await MapAsync(count);
    }

    public async Task<StockCountDto> UpdateDraftAsync(Guid tenantId, Guid stockCountId, UpdateStockCountDraftRequest request)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        var count = await _context.Set<StockCount>()
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.StockCountId == stockCountId)
            ?? throw new KeyNotFoundException("Stock count not found.");

        if (count.Status != StockCountStatus.Draft)
            throw new InvalidOperationException($"Only Draft stock counts can be edited. Current status is {count.Status}.");

        if (request.WarehouseId.HasValue && request.WarehouseId.Value != count.WarehouseId
            || request.WarehouseLocationId.HasValue && request.WarehouseLocationId.Value != count.WarehouseLocationId)
            throw new InvalidOperationException("Warehouse and location are part of the captured stock snapshot and cannot be changed.");

        if (!string.IsNullOrWhiteSpace(request.CountNumber))
            count.CountNumber = request.CountNumber.Trim();

        if (request.CountDate.HasValue)
            count.CountDate = request.CountDate.Value;

        if (request.Remarks != null)
            count.Remarks = request.Remarks;

        foreach (var lineRequest in request.Lines)
        {
            var line = count.Lines.FirstOrDefault(x => x.StockCountLineId == lineRequest.StockCountLineId)
                ?? throw new KeyNotFoundException($"Stock count line '{lineRequest.StockCountLineId}' not found.");

            if (!string.Equals(line.BatchNumber ?? string.Empty, lineRequest.BatchNumber?.Trim() ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(line.SerialNumber ?? string.Empty, lineRequest.SerialNumber?.Trim() ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Batch and serial identities are part of the captured snapshot and cannot be changed.");

            var product = await _context.Products
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.ProductId == line.ProductId && p.IsActive)
                ?? throw new KeyNotFoundException($"Product '{line.ProductId}' not found or is inactive.");

            var unitId = product.UnitOfMeasureId
                ?? throw new InvalidOperationException($"Product '{product.ProductCode}' has no base unit of measure configured.");
            var enteredUnitId = lineRequest.UnitOfMeasureId ?? unitId;
            var unit = await _context.UnitsOfMeasure
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.TenantId == tenantId && u.UnitOfMeasureId == enteredUnitId && u.IsActive)
                ?? throw new KeyNotFoundException("Unit of measure not found or is inactive.");
            if (lineRequest.PhysicalQuantity < 0)
                throw new ArgumentOutOfRangeException(nameof(lineRequest.PhysicalQuantity), "Physical quantity cannot be negative.");
            if (lineRequest.UnitCost < 0)
                throw new ArgumentOutOfRangeException(nameof(lineRequest.UnitCost), "Unit cost cannot be negative.");
            ValidateTrackingIdentity(product, line.BatchNumber, line.SerialNumber, lineRequest.PhysicalQuantity);
            var physicalBase = await ConvertToBaseAsync(tenantId, product, lineRequest.PhysicalQuantity, enteredUnitId, unitId, count.CompanyId);

            line.PhysicalQuantityBase = physicalBase;
            line.PhysicalQuantityEntered = lineRequest.PhysicalQuantity;
            line.UnitOfMeasureId = enteredUnitId;
            line.UnitOfMeasureCode = unit.Code;
            line.ManufacturingDate = lineRequest.ManufacturingDate ?? line.ManufacturingDate;
            line.ExpiryDate = lineRequest.ExpiryDate ?? line.ExpiryDate;
            line.UnitCost = lineRequest.UnitCost;
            line.Remarks = lineRequest.Remarks;
            line.RecalculateVariance();
        }

        count.UpdateDraft();
        count.SnapshotFingerprint = CalculateFingerprint(count);
        await _context.SaveChangesAsync();
        return await MapAsync(count);
    }

    public async Task<StockCountDto> SubmitAsync(Guid tenantId, Guid stockCountId, SubmitStockCountRequest request)
    {
        var count = await _context.Set<StockCount>()
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.StockCountId == stockCountId)
            ?? throw new KeyNotFoundException("Stock count not found.");

        if (count.Status != StockCountStatus.Draft)
            throw new InvalidOperationException($"Stock count '{count.CountNumber}' is not in Draft status and cannot be submitted.");

        count.Submit(request.SubmittedByUserId);
        count.Remarks = string.IsNullOrWhiteSpace(count.Remarks)
            ? request.Notes
            : count.Remarks + (string.IsNullOrWhiteSpace(request.Notes) ? string.Empty : $" | {request.Notes}");
        count.SnapshotFingerprint = CalculateFingerprint(count);

        await _context.SaveChangesAsync();
        return await MapAsync(count);
    }

    public async Task<StockCountDto> ApproveAsync(Guid tenantId, Guid stockCountId, ApproveStockCountRequest request)
    {
        var count = await _context.Set<StockCount>()
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.StockCountId == stockCountId)
            ?? throw new KeyNotFoundException("Stock count not found.");

        if (count.Status != StockCountStatus.Submitted)
            throw new InvalidOperationException($"Stock count '{count.CountNumber}' is not in Submitted status and cannot be approved.");

        count.Approve(request.ApprovedByUserId);
        await _context.SaveChangesAsync();
        return await MapAsync(count);
    }

    public async Task<StockCountDto> PostAsync(Guid tenantId, Guid stockCountId, PostStockCountRequest request)
    {
        var count = await _context.Set<StockCount>()
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.StockCountId == stockCountId)
            ?? throw new KeyNotFoundException("Stock count not found.");

        if (count.Status != StockCountStatus.Approved)
            throw new InvalidOperationException($"Stock count '{count.CountNumber}' is not in Approved status and cannot be posted.");

        var dbTransaction = _context.Database.CurrentTransaction;
        var ownsTransaction = dbTransaction == null;
        if (ownsTransaction)
            dbTransaction = await _context.Database.BeginTransactionAsync();

        try
        {
            var lockScopes = count.Lines
                .Select(x => (TenantId: tenantId, ProductId: x.ProductId, WarehouseId: count.WarehouseId))
                .Distinct()
                .OrderBy(x => x.TenantId)
                .ThenBy(x => x.ProductId)
                .ThenBy(x => x.WarehouseId)
                .ToList();
            foreach (var lockScope in lockScopes)
                await _valuationService.AcquireValuationLockAsync(lockScope.TenantId, lockScope.ProductId, lockScope.WarehouseId);

            var currentStatus = await _context.Set<StockCount>()
                .AsNoTracking()
                .Where(x => x.TenantId == tenantId && x.StockCountId == stockCountId)
                .Select(x => x.Status)
                .SingleAsync();
            if (currentStatus != StockCountStatus.Approved)
                throw new InvalidOperationException($"Stock count '{count.CountNumber}' is no longer Approved and cannot be posted.");

            var productIds = count.Lines.Select(x => x.ProductId).Distinct().ToList();
            if (!string.Equals(count.SnapshotFingerprint, CalculateFingerprint(count), StringComparison.Ordinal))
                throw new InvalidOperationException("The approved stock count data has changed. Recount and resubmit before posting.");

            await ValidateInventorySnapshotAsync(tenantId, count);

            var products = await _context.Products
                .AsNoTracking()
                .Where(p => p.TenantId == tenantId && productIds.Contains(p.ProductId))
                .ToDictionaryAsync(p => p.ProductId);
            var issueLines = count.Lines
                .Where(x => x.VarianceQuantityBase < 0)
                .OrderBy(x => x.StockCountLineId)
                .ToList();
            var receiptLines = count.Lines
                .Where(x => x.VarianceQuantityBase > 0)
                .OrderBy(x => x.StockCountLineId)
                .ToList();

            if (issueLines.Count > 0)
            {
                var issueRequest = new CreateStockIssueRequest
                {
                    StockIssueId = count.StockCountId,
                    SourceDocumentReference = count.StockCountId.ToString(),
                    WarehouseId = count.WarehouseId,
                    Purpose = StockIssuePurpose.InternalUse,
                    Remarks = count.Remarks,
                    Lines = issueLines.Select(line => new CreateStockIssueLineRequest
                    {
                        StockIssueLineId = line.StockCountLineId,
                        ProductId = line.ProductId,
                        WarehouseLocationId = count.WarehouseLocationId,
                        Quantity = Math.Abs(line.VarianceQuantityBase),
                        BatchNumber = line.BatchNumber,
                        SerialNumber = line.SerialNumber,
                        Remarks = line.Remarks
                    }).ToList()
                };

                await _inventoryService.CreateStockIssueAsync(tenantId, issueRequest);
            }

            if (receiptLines.Count > 0)
            {
                var receiptRequest = new CreateStockReceiptRequest
                {
                    StockReceiptId = count.StockCountId,
                    SourceDocumentReference = count.StockCountId.ToString(),
                    WarehouseId = count.WarehouseId,
                    Reason = StockReceiptReason.Miscellaneous,
                    Remarks = count.Remarks,
                    Lines = receiptLines.Select(line =>
                    {
                        var product = products[line.ProductId];
                        var unitCost = line.UnitCost;
                        if (product.ValuationMethod == ValuationMethod.StandardCost)
                        {
                            if (product.StandardCost <= 0)
                            {
                                throw new InvalidOperationException(
                                    $"Product '{line.ProductCode}' requires a positive standard cost for Standard Cost valuation.");
                            }

                            if (unitCost <= 0)
                                unitCost = product.StandardCost;
                        }

                        if (unitCost <= 0)
                        {
                            throw new InvalidOperationException(
                                $"Positive variance for product '{line.ProductCode}' requires an approved unit cost for {product.ValuationMethod} valuation.");
                        }

                        return new CreateStockReceiptLineRequest
                        {
                            StockReceiptLineId = line.StockCountLineId,
                            ProductId = line.ProductId,
                            WarehouseLocationId = count.WarehouseLocationId,
                            Quantity = line.VarianceQuantityBase,
                            UnitCost = unitCost,
                            BatchNumber = line.BatchNumber,
                            SerialNumber = line.SerialNumber,
                            ManufacturingDate = line.ManufacturingDate,
                            ExpiryDate = line.ExpiryDate,
                            Remarks = line.Remarks
                        };
                    }).ToList()
                };

                await _inventoryService.CreateStockReceiptAsync(tenantId, receiptRequest);
            }

            count.Post(request.PostedByUserId);
            count.Remarks = string.IsNullOrWhiteSpace(count.Remarks)
                ? request.Notes
                : count.Remarks + (string.IsNullOrWhiteSpace(request.Notes) ? string.Empty : $" | {request.Notes}");

            await _context.SaveChangesAsync();

            var result = await MapAsync(count);
            if (ownsTransaction && dbTransaction != null)
                await dbTransaction.CommitAsync();

            return result;
        }
        catch
        {
            if (ownsTransaction && dbTransaction != null)
                await dbTransaction.RollbackAsync();
            throw;
        }
        finally
        {
            if (ownsTransaction && dbTransaction != null)
                await dbTransaction.DisposeAsync();
        }
    }

    public async Task<StockCountDto> CancelAsync(Guid tenantId, Guid stockCountId, CancelStockCountRequest request)
    {
        var count = await _context.Set<StockCount>()
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.StockCountId == stockCountId)
            ?? throw new KeyNotFoundException("Stock count not found.");

        if (count.Status == StockCountStatus.Posted)
            throw new InvalidOperationException("Posted stock counts cannot be cancelled.");

        count.Cancel(request.CancelledByUserId, request.Reason);
        await _context.SaveChangesAsync();
        return await MapAsync(count);
    }

    private static string CalculateFingerprint(StockCount count)
    {
        using var sha = SHA256.Create();
        var payload = string.Join("|",
            count.Lines
                .OrderBy(x => x.ProductId)
                .ThenBy(x => x.BatchNumber ?? string.Empty)
                .ThenBy(x => x.SerialNumber ?? string.Empty)
                .ThenBy(x => x.StockCountLineId)
                .Select(x => string.Join(":",
                    x.StockCountLineId.ToString("N"),
                    x.ProductId.ToString("N"),
                    x.ExpectedQuantityBase.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    x.PhysicalQuantityBase.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    x.UnitOfMeasureId.ToString("N"),
                    x.UnitCost.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    x.BatchNumber ?? string.Empty,
                    x.SerialNumber ?? string.Empty,
                    x.ManufacturingDate?.ToString("O", System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
                    x.ExpiryDate?.ToString("O", System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
                    x.WarehouseLocationId.ToString("N"))));

        var bytes = Encoding.UTF8.GetBytes($"{count.WarehouseId}:{count.WarehouseLocationId}:{payload}");
        return Convert.ToHexString(sha.ComputeHash(bytes));
    }

    private async Task<decimal> ConvertToBaseAsync(
        Guid tenantId,
        Product product,
        decimal quantity,
        Guid fromUnitId,
        Guid toUnitId,
        Guid companyId)
    {
        if (fromUnitId == toUnitId)
            return quantity;
        if (fromUnitId == Guid.Empty || toUnitId == Guid.Empty)
            throw new InvalidOperationException("A base and entered unit of measure are required for stock count conversion.");

        var conversion = await _unitOfMeasureService.ConvertProductQuantityAsync(
            tenantId,
            product.ProductId,
            fromUnitId,
            toUnitId,
            quantity,
            global::Domain.Features.MasterData.UnitOfMeasure.ConversionContext.General,
            companyId: companyId);

        return conversion.ConvertedQuantity;
    }

    private async Task ValidateInventorySnapshotAsync(Guid tenantId, StockCount count)
    {
        var productIds = count.Lines.Select(x => x.ProductId).Distinct().ToList();
        var products = await _context.Products
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId && productIds.Contains(p.ProductId))
            .ToDictionaryAsync(p => p.ProductId);

        var expected = count.Lines
            .Where(x => x.ExpectedQuantityBase != 0)
            .GroupBy(x => CreateIdentity(products[x.ProductId], x.BatchNumber, x.SerialNumber), InventoryIdentityComparer.Instance)
            .ToDictionary(x => x.Key, x => x.Sum(line => line.ExpectedQuantityBase), InventoryIdentityComparer.Instance);

        var currentBalances = await _context.InventoryBalances
            .AsNoTracking()
            .Where(b => b.TenantId == tenantId
                        && b.WarehouseId == count.WarehouseId
                        && b.WarehouseLocationId == count.WarehouseLocationId
                        && productIds.Contains(b.ProductId)
                        && b.QuantityOnHand != 0)
            .ToListAsync();
        var current = currentBalances
            .GroupBy(balance =>
            {
                var product = products[balance.ProductId];
                ValidateTrackingIdentity(product, balance.BatchNumber, balance.SerialNumber, balance.QuantityOnHand);
                return CreateIdentity(product, balance.BatchNumber, balance.SerialNumber);
            }, InventoryIdentityComparer.Instance)
            .ToDictionary(group => group.Key, group => group.Sum(balance => balance.QuantityOnHand), InventoryIdentityComparer.Instance);

        if (expected.Count != current.Count
            || expected.Any(entry => !current.TryGetValue(entry.Key, out var quantity) || quantity != entry.Value))
            throw new InvalidOperationException("Inventory changed after this count snapshot was captured. Create and submit a new count before posting.");
    }

    private static StockCountDto Map(StockCount count)
    {
        return new StockCountDto
        {
            StockCountId = count.StockCountId,
            CountNumber = count.CountNumber,
            CompanyId = count.CompanyId,
            WarehouseId = count.WarehouseId,
            WarehouseLocationId = count.WarehouseLocationId,
            CountDate = count.CountDate,
            Status = count.Status,
            Remarks = count.Remarks,
            TotalExpectedQuantity = count.TotalExpectedQuantity,
            TotalPhysicalQuantity = count.TotalPhysicalQuantity,
            TotalVarianceQuantity = count.TotalVarianceQuantity,
            SubmittedByUserId = count.SubmittedByUserId,
            SubmittedAt = count.SubmittedAt,
            ApprovedByUserId = count.ApprovedByUserId,
            ApprovedAt = count.ApprovedAt,
            PostedByUserId = count.PostedByUserId,
            PostedAt = count.PostedAt,
            CreatedAt = count.CreatedAt,
            UpdatedAt = count.UpdatedAt,
            Lines = count.Lines.Select(line => new StockCountLineDto
            {
                StockCountLineId = line.StockCountLineId,
                ProductId = line.ProductId,
                ProductCode = line.ProductCode,
                ProductName = line.ProductName,
                UnitOfMeasureId = line.UnitOfMeasureId,
                UnitOfMeasureCode = line.UnitOfMeasureCode,
                ExpectedQuantityBase = line.ExpectedQuantityBase,
                PhysicalQuantityBase = line.PhysicalQuantityBase,
                VarianceQuantityBase = line.VarianceQuantityBase,
                BatchNumber = line.BatchNumber,
                SerialNumber = line.SerialNumber,
                ManufacturingDate = line.ManufacturingDate,
                ExpiryDate = line.ExpiryDate,
                UnitCost = line.UnitCost,
                Remarks = line.Remarks
            }).ToList()
        };
    }

    private async Task<StockCountDto> MapAsync(StockCount count)
    {
        var result = Map(count);
        var lineIds = count.Lines.Select(x => x.StockCountLineId).ToList();
        var transactionReferences = await _context.InventoryTransactions
            .AsNoTracking()
            .Where(x => x.TenantId == count.TenantId
                        && x.SourceDocumentType == SourceDocumentType.StockAdjustment
                        && x.SourceDocumentId == count.StockCountId
                        && lineIds.Contains(x.SourceDocumentLineId))
            .ToListAsync();

        foreach (var line in result.Lines)
        {
            line.InventoryTransactions = transactionReferences
                .Where(x => x.SourceDocumentLineId == line.StockCountLineId)
                .OrderBy(x => x.MovementSequence)
                .ThenBy(x => x.TransactionDate)
                .Select(MapTransaction)
                .ToList();
        }

        return result;
    }

    private async Task<List<StockCountDto>> MapManyAsync(List<StockCount> counts)
    {
        var results = counts.Select(Map).ToList();
        var countIds = counts.Select(x => x.StockCountId).ToList();
        var transactionReferences = counts.Count == 0
            ? new List<InventoryTransaction>()
            : await _context.InventoryTransactions
            .AsNoTracking()
            .Where(x => x.TenantId == counts[0].TenantId
                        && x.SourceDocumentType == SourceDocumentType.StockAdjustment
                        && countIds.Contains(x.SourceDocumentId))
            .ToListAsync();

        foreach (var result in results)
        {
            foreach (var line in result.Lines)
            {
                line.InventoryTransactions = transactionReferences
                    .Where(x => x.SourceDocumentId == result.StockCountId && x.SourceDocumentLineId == line.StockCountLineId)
                    .OrderBy(x => x.MovementSequence)
                    .ThenBy(x => x.TransactionDate)
                    .Select(MapTransaction)
                    .ToList();
            }
        }

        return results;
    }

    private static InventoryIdentity CreateIdentity(Product product, string? batchNumber, string? serialNumber)
    {
        return product.TrackingMode switch
        {
            TrackingMode.None => new InventoryIdentity(product.ProductId, string.Empty, string.Empty),
            TrackingMode.Batch => new InventoryIdentity(product.ProductId, NormalizeIdentity(batchNumber), string.Empty),
            TrackingMode.Serial => new InventoryIdentity(product.ProductId, string.Empty, NormalizeIdentity(serialNumber)),
            TrackingMode.BatchAndSerial => new InventoryIdentity(product.ProductId, NormalizeIdentity(batchNumber), NormalizeIdentity(serialNumber)),
            _ => throw new InvalidOperationException($"Unsupported tracking mode '{product.TrackingMode}'.")
        };
    }

    private static void ValidateTrackingIdentity(Product product, string? batchNumber, string? serialNumber, decimal physicalQuantity)
    {
        var hasBatch = !string.IsNullOrWhiteSpace(batchNumber);
        var hasSerial = !string.IsNullOrWhiteSpace(serialNumber);

        switch (product.TrackingMode)
        {
            case TrackingMode.None when hasBatch || hasSerial:
                throw new InvalidOperationException($"Product '{product.ProductCode}' does not use batch or serial tracking.");
            case TrackingMode.Batch when hasSerial || (!hasBatch && physicalQuantity != 0m):
                throw new InvalidOperationException($"Product '{product.ProductCode}' requires a batch number and does not allow a serial number.");
            case TrackingMode.Serial when hasBatch || (!hasSerial && physicalQuantity != 0m):
                throw new InvalidOperationException($"Product '{product.ProductCode}' requires a serial number and does not allow a batch number.");
            case TrackingMode.BatchAndSerial when hasBatch != hasSerial || (!hasBatch && physicalQuantity != 0m):
                throw new InvalidOperationException($"Product '{product.ProductCode}' requires both a batch and a serial number.");
        }

        if ((product.TrackingMode == TrackingMode.Serial || product.TrackingMode == TrackingMode.BatchAndSerial)
            && physicalQuantity != 0m
            && physicalQuantity != 1m)
            throw new InvalidOperationException($"Serial-tracked product '{product.ProductCode}' must have physical quantity zero or one per serial.");
    }

    private static string NormalizeIdentity(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private readonly record struct InventoryIdentity(Guid ProductId, string BatchNumber, string SerialNumber);
    private readonly record struct InventoryBatchIdentity(Guid ProductId, string BatchNumber);

    private sealed class InventoryIdentityComparer : IEqualityComparer<InventoryIdentity>
    {
        public static InventoryIdentityComparer Instance { get; } = new();

        public bool Equals(InventoryIdentity x, InventoryIdentity y)
        {
            return x.ProductId == y.ProductId
                && StringComparer.OrdinalIgnoreCase.Equals(x.BatchNumber, y.BatchNumber)
                && StringComparer.OrdinalIgnoreCase.Equals(x.SerialNumber, y.SerialNumber);
        }

        public int GetHashCode(InventoryIdentity obj)
        {
            return HashCode.Combine(
                obj.ProductId,
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.BatchNumber),
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.SerialNumber));
        }
    }

    private sealed class InventoryBatchIdentityComparer : IEqualityComparer<InventoryBatchIdentity>
    {
        public static InventoryBatchIdentityComparer Instance { get; } = new();

        public bool Equals(InventoryBatchIdentity x, InventoryBatchIdentity y)
        {
            return x.ProductId == y.ProductId
                && StringComparer.OrdinalIgnoreCase.Equals(x.BatchNumber, y.BatchNumber);
        }

        public int GetHashCode(InventoryBatchIdentity obj)
        {
            return HashCode.Combine(
                obj.ProductId,
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.BatchNumber));
        }
    }

    private static StockCountInventoryTransactionDto MapTransaction(InventoryTransaction transaction)
    {
        return new StockCountInventoryTransactionDto
        {
            InventoryTransactionId = transaction.InventoryTransactionId,
            MovementType = transaction.MovementType,
            SourceDocumentType = transaction.SourceDocumentType,
            SourceDocumentId = transaction.SourceDocumentId,
            SourceDocumentLineId = transaction.SourceDocumentLineId,
            MovementSequence = transaction.MovementSequence,
            Quantity = transaction.Quantity,
            UnitCost = transaction.UnitCost,
            TotalCost = transaction.TotalCost,
            TransactionDate = transaction.TransactionDate,
            ReversalOfTransactionId = transaction.ReversalOfTransactionId
        };
    }

    private sealed class PhysicalObservation
    {
        public PhysicalObservation(
            decimal physicalQuantityBase,
            decimal physicalQuantityEntered,
            Guid unitOfMeasureId,
            string unitOfMeasureCode,
            decimal unitCost,
            string batchNumber,
            string serialNumber,
            DateTime? manufacturingDate,
            DateTime? expiryDate,
            string? remarks)
        {
            PhysicalQuantityBase = physicalQuantityBase;
            PhysicalQuantityEntered = physicalQuantityEntered;
            UnitOfMeasureId = unitOfMeasureId;
            UnitOfMeasureCode = unitOfMeasureCode;
            UnitCost = unitCost;
            BatchNumber = batchNumber;
            SerialNumber = serialNumber;
            ManufacturingDate = manufacturingDate;
            ExpiryDate = expiryDate;
            Remarks = remarks;
        }

        public decimal PhysicalQuantityBase { get; private set; }
        public decimal PhysicalQuantityEntered { get; private set; }
        public Guid UnitOfMeasureId { get; }
        public string UnitOfMeasureCode { get; }
        public string BatchNumber { get; private set; }
        public string SerialNumber { get; private set; }
        public decimal UnitCost { get; private set; }
        public DateTime? ManufacturingDate { get; private set; }
        public DateTime? ExpiryDate { get; private set; }
        public string? Remarks { get; private set; }

        public void Add(
            decimal physicalQuantityBase,
            decimal physicalQuantityEntered,
            decimal unitCost,
            string batchNumber,
            string serialNumber,
            DateTime? manufacturingDate,
            DateTime? expiryDate,
            string? remarks)
        {
            PhysicalQuantityBase += physicalQuantityBase;
            PhysicalQuantityEntered += physicalQuantityEntered;
            if (unitCost > 0)
                UnitCost = unitCost;
            if (BatchNumber.Length == 0 && batchNumber.Length > 0)
                BatchNumber = batchNumber;
            if (SerialNumber.Length == 0 && serialNumber.Length > 0)
                SerialNumber = serialNumber;
            ManufacturingDate ??= manufacturingDate;
            ExpiryDate ??= expiryDate;
            Remarks ??= remarks;
        }
    }
}
