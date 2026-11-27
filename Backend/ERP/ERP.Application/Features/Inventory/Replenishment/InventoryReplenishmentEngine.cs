using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Domain.Features.MasterData.Product;
using Domain.Features.Procurement.PurchaseOrder;
using Domain.Features.Procurement.PurchaseRequisition;
using Domain.Infrastructure.Persistence;
using ERP.Domain.Features.Inventory;
using ERP.Domain.Features.MasterData.Warehouse;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ERP.Application.Features.Inventory.Replenishment;

public class InventoryReplenishmentEngine : IInventoryReplenishmentEngine
{
    private readonly DomainDbContext _context;
    private readonly IPurchaseRequisitionService _purchaseRequisitionService;
    private readonly ILogger<InventoryReplenishmentEngine>? _logger;

    public InventoryReplenishmentEngine(
        DomainDbContext context,
        IPurchaseRequisitionService purchaseRequisitionService,
        ILogger<InventoryReplenishmentEngine>? logger = null)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _purchaseRequisitionService = purchaseRequisitionService ?? throw new ArgumentNullException(nameof(purchaseRequisitionService));
        _logger = logger;
    }

    public async Task<ReplenishmentAssessmentResult> AssessStockAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        ReplenishmentAssessmentRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (companyId == Guid.Empty) throw new ArgumentException("CompanyId is required.", nameof(companyId));
        if (branchId == Guid.Empty) throw new ArgumentException("BranchId is required.", nameof(branchId));
        if (request.WarehouseId == Guid.Empty) throw new ArgumentException("WarehouseId is required.", nameof(request.WarehouseId));

        var effectiveDate = (request.AsOfDate ?? DateTime.UtcNow).Date;

        // 1. Verify target warehouse ownership and company boundary
        var warehouse = await _context.Warehouses
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.TenantId == tenantId && w.WarehouseId == request.WarehouseId && w.IsActive);

        if (warehouse == null)
        {
            throw new KeyNotFoundException($"Warehouse '{request.WarehouseId}' was not found, is inactive, or does not belong to this tenant.");
        }

        if (warehouse.CompanyId.HasValue && warehouse.CompanyId.Value != companyId)
        {
            throw new InvalidOperationException(
                $"Warehouse '{warehouse.WarehouseCode}' belongs to company '{warehouse.CompanyId.Value}', not authorized company '{companyId}'.");
        }

        var result = new ReplenishmentAssessmentResult
        {
            WarehouseId = warehouse.WarehouseId,
            WarehouseCode = warehouse.WarehouseCode,
            WarehouseName = warehouse.WarehouseName,
            CompanyId = companyId
        };

        if (request.Items == null || request.Items.Count == 0)
        {
            return result;
        }

        var requestedProductIds = request.Items.Select(i => i.ProductId).Distinct().ToList();

        // 2. Query target products
        var products = await _context.Products
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId && requestedProductIds.Contains(p.ProductId) && p.IsActive)
            .ToDictionaryAsync(p => p.ProductId);

        foreach (var reqItem in request.Items)
        {
            if (!products.ContainsKey(reqItem.ProductId))
            {
                throw new KeyNotFoundException($"Product '{reqItem.ProductId}' was not found, is inactive, or does not belong to this tenant.");
            }
        }

        // 3. Query inventory balances for warehouse and products
        var balances = await _context.InventoryBalances
            .AsNoTracking()
            .Where(b => b.TenantId == tenantId
                     && b.WarehouseId == warehouse.WarehouseId
                     && requestedProductIds.Contains(b.ProductId))
            .ToListAsync();

        var balancesByProduct = balances
            .GroupBy(b => b.ProductId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // 4. Batch expiry identification
        var batchTrackedProductIds = products.Values
            .Where(p => p.TrackingMode == TrackingMode.Batch || p.TrackingMode == TrackingMode.BatchAndSerial)
            .Select(p => p.ProductId)
            .ToList();

        Dictionary<(Guid ProductId, string BatchNumber), DateTime?> batchExpiryDict = new();
        if (batchTrackedProductIds.Count > 0)
        {
            var trackedBatches = await _context.ProductBatches
                .AsNoTracking()
                .Where(pb => pb.TenantId == tenantId
                          && batchTrackedProductIds.Contains(pb.ProductId)
                          && pb.IsActive)
                .Select(pb => new { pb.ProductId, pb.BatchNumber, pb.ExpiryDate })
                .ToListAsync();

            batchExpiryDict = trackedBatches
                .ToDictionary(b => (b.ProductId, b.BatchNumber), b => b.ExpiryDate);
        }

        // 5. Inbound supply from Approved POs strictly filtered by Tenant AND Company
        var approvedPoLines = await _context.PurchaseOrders
            .AsNoTracking()
            .Where(po => po.TenantId == tenantId
                      && po.CompanyId == companyId
                      && po.Status == PurchaseOrderStatus.Approved
                      && po.IsActive)
            .SelectMany(po => po.Items.Where(i => i.IsActive && i.Quantity > i.ReceivedQuantity), (po, item) => new
            {
                po.BranchId,
                item.ProductId,
                OutstandingQty = item.Quantity - item.ReceivedQuantity
            })
            .Where(x => requestedProductIds.Contains(x.ProductId))
            .ToListAsync();

        // 6. Preferred supplier pricing for estimated unit price resolution
        var preferredPrices = await _context.SupplierProductPrices
            .AsNoTracking()
            .Where(s => s.TenantId == tenantId
                     && requestedProductIds.Contains(s.ProductId)
                     && s.IsPreferred
                     && s.IsActive)
            .GroupBy(s => s.ProductId)
            .ToDictionaryAsync(g => g.Key, g => g.First().UnitPrice);

        foreach (var reqItem in request.Items)
        {
            var product = products[reqItem.ProductId];
            balancesByProduct.TryGetValue(product.ProductId, out var whBalances);
            whBalances ??= new List<InventoryBalance>();

            decimal totalOnHand = 0m;
            decimal totalAllocated = 0m;
            decimal totalQuarantine = 0m;
            decimal expiredQuantity = 0m;

            bool isBatch = product.TrackingMode == TrackingMode.Batch || product.TrackingMode == TrackingMode.BatchAndSerial;

            foreach (var b in whBalances)
            {
                totalOnHand += b.QuantityOnHand;
                totalAllocated += b.QuantityAllocated;
                totalQuarantine += b.QuantityQuarantine;

                if (isBatch && !string.IsNullOrWhiteSpace(b.BatchNumber) && b.QuantityOnHand > 0)
                {
                    if (batchExpiryDict.TryGetValue((product.ProductId, b.BatchNumber), out var expDate) && expDate.HasValue)
                    {
                        if (expDate.Value.Date < effectiveDate)
                        {
                            expiredQuantity += b.QuantityOnHand;
                        }
                    }
                }
            }

            decimal eligibleOnHand = Math.Max(0m, totalOnHand - expiredQuantity);

            // Inbound supply: from maintained balance or PO lines
            decimal balanceOnOrder = whBalances.Sum(b => b.QuantityOnOrder);
            decimal poLineOutstanding = 0m;
            if (warehouse.BranchId.HasValue)
            {
                poLineOutstanding = approvedPoLines
                    .Where(x => x.ProductId == product.ProductId && x.BranchId == warehouse.BranchId.Value)
                    .Sum(x => x.OutstandingQty);
            }
            else
            {
                // If warehouse is company-wide without specific branch, consider company PO lines for the product
                poLineOutstanding = approvedPoLines
                    .Where(x => x.ProductId == product.ProductId)
                    .Sum(x => x.OutstandingQty);
            }

            decimal quantityOnOrder = Math.Max(balanceOnOrder, poLineOutstanding);
            decimal netAvailable = eligibleOnHand - totalAllocated - totalQuarantine + quantityOnOrder;

            decimal reqQty = Math.Max(0m, reqItem.RequiredQuantity);
            decimal shortage = Math.Max(0m, reqQty - netAvailable);
            bool hasShortage = shortage > 0m;

            decimal? estPrice = preferredPrices.TryGetValue(product.ProductId, out decimal prefPrice)
                ? prefPrice
                : (product.StandardCost > 0 ? product.StandardCost : null);

            result.Lines.Add(new ReplenishmentLineAssessmentDto
            {
                ProductId = product.ProductId,
                ProductCode = product.ProductCode,
                ProductName = product.ProductName,
                RequiredQuantity = reqQty,
                QuantityOnHand = totalOnHand,
                ExpiredQuantity = expiredQuantity,
                EligibleOnHandQuantity = eligibleOnHand,
                QuantityAllocated = totalAllocated,
                QuantityQuarantine = totalQuarantine,
                QuantityOnOrder = quantityOnOrder,
                NetAvailableStock = netAvailable,
                ShortageQuantity = shortage,
                HasShortage = hasShortage,
                UnitOfMeasureId = reqItem.UnitOfMeasureId ?? product.UnitOfMeasureId,
                UnitOfMeasure = product.UnitOfMeasure,
                EstimatedUnitPrice = estPrice
            });
        }

        result.TotalLinesWithShortage = result.Lines.Count(l => l.HasShortage);
        result.HasAnyShortage = result.TotalLinesWithShortage > 0;

        return result;
    }

    public async Task<DraftPurchaseRequisitionResult> CreateDraftPurchaseRequisitionAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        CreateReplenishmentPrRequest request,
        Guid actorUserId,
        string actorUserName)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (companyId == Guid.Empty) throw new ArgumentException("CompanyId is required.", nameof(companyId));
        if (branchId == Guid.Empty) throw new ArgumentException("BranchId is required.", nameof(branchId));

        if (request.Items == null || request.Items.Count == 0)
        {
            throw new InvalidOperationException("At least one line item is required to create a replenishment Purchase Requisition.");
        }

        // 1. Validate source-document scope (Tenant, Company, and existence)
        if (request.SourceType == ReplenishmentSourceType.SalesOrder || request.SourceType == ReplenishmentSourceType.SalesShipment)
        {
            if (request.SourceEntityId.HasValue && request.SourceEntityId.Value != Guid.Empty)
            {
                var sourceOrder = await _context.SalesOrders
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Id == request.SourceEntityId.Value && s.IsActive);

                if (sourceOrder == null)
                {
                    throw new KeyNotFoundException($"Source Sales Order '{request.SourceEntityId.Value}' was not found or is inactive.");
                }

                if (sourceOrder.CompanyId != companyId)
                {
                    throw new InvalidOperationException(
                        $"Source Sales Order '{sourceOrder.OrderNumber}' belongs to company '{sourceOrder.CompanyId}', not authorized company '{companyId}'.");
                }
            }
        }

        // 2. Formulate audit & idempotency tags
        string? idemKey = !string.IsNullOrWhiteSpace(request.IdempotencyKey) ? request.IdempotencyKey.Trim() : null;
        string? idemTag = idemKey != null ? $"[IdempotencyKey:{idemKey}]" : null;

        string? sourceTag = (request.SourceEntityId.HasValue && request.SourceEntityId.Value != Guid.Empty)
            ? $"[Source:{request.SourceType}:{request.SourceEntityId.Value}]"
            : null;

        // 3. Identify existing active Draft PR for the same replenishment operation
        // (A cancelled PR must not permanently block a legitimate new replenishment operation)
        if (idemTag != null || sourceTag != null)
        {
            var existingQuery = _context.PurchaseRequisitions
                .Include(pr => pr.Items)
                .AsNoTracking()
                .Where(pr => pr.TenantId == tenantId
                          && pr.CompanyId == companyId
                          && pr.IsActive
                          && pr.Status != PurchaseRequisitionStatus.Cancelled);

            if (idemTag != null && sourceTag != null)
            {
                existingQuery = existingQuery.Where(pr => pr.Notes != null && (pr.Notes.Contains(idemTag) || pr.Notes.Contains(sourceTag)));
            }
            else if (idemTag != null)
            {
                existingQuery = existingQuery.Where(pr => pr.Notes != null && pr.Notes.Contains(idemTag));
            }
            else if (sourceTag != null)
            {
                existingQuery = existingQuery.Where(pr => pr.Notes != null && pr.Notes.Contains(sourceTag));
            }

            var existingPr = await existingQuery.OrderByDescending(pr => pr.RequisitionDate).FirstOrDefaultAsync();
            if (existingPr != null)
            {
                return await MapExistingToResultAsync(
                    existingPr,
                    isReused: true,
                    message: $"Existing active Purchase Requisition '{existingPr.RequisitionNumber}' retrieved (idempotency key / source replay). Zero stock deducted or allocated.");
            }
        }

        // 4. Resolve effective warehouse scope
        Guid effectiveWarehouseId;
        if (request.WarehouseId.HasValue && request.WarehouseId.Value != Guid.Empty)
        {
            var wh = await _context.Warehouses
                .AsNoTracking()
                .FirstOrDefaultAsync(w => w.TenantId == tenantId && w.WarehouseId == request.WarehouseId.Value && w.IsActive);

            if (wh == null || (wh.CompanyId.HasValue && wh.CompanyId.Value != companyId))
            {
                throw new KeyNotFoundException($"Warehouse '{request.WarehouseId.Value}' not found in authorized company scope.");
            }
            effectiveWarehouseId = wh.WarehouseId;
        }
        else
        {
            var branchWh = await _context.Warehouses
                .AsNoTracking()
                .FirstOrDefaultAsync(w => w.TenantId == tenantId && w.CompanyId == companyId && w.BranchId == branchId && w.IsActive);

            var companyWh = branchWh ?? await _context.Warehouses
                .AsNoTracking()
                .FirstOrDefaultAsync(w => w.TenantId == tenantId && w.CompanyId == companyId && w.IsActive);

            if (companyWh == null)
            {
                throw new InvalidOperationException("No warehouse found for authorized company/branch scope.");
            }
            effectiveWarehouseId = companyWh.WarehouseId;
        }

        // 5. Run stock assessment
        var assessmentReq = new ReplenishmentAssessmentRequest
        {
            WarehouseId = effectiveWarehouseId,
            Items = request.Items.Select(i => new ReplenishmentItemDemandDto
            {
                ProductId = i.ProductId,
                RequiredQuantity = i.RequestedQuantity ?? 0m,
                UnitOfMeasureId = i.UnitOfMeasureId,
                Remarks = i.Remarks
            }).ToList()
        };

        var assessment = await AssessStockAsync(tenantId, companyId, branchId, assessmentReq);

        // 6. Build PR lines from shortages or explicit requested quantities
        var prItems = new List<CreatePurchaseRequisitionItemRequest>();
        var lineResults = new List<DraftPrLineResultDto>();

        foreach (var reqItem in request.Items)
        {
            var assessedLine = assessment.Lines.FirstOrDefault(l => l.ProductId == reqItem.ProductId);
            if (assessedLine == null) continue;

            decimal orderQty;
            if (reqItem.RequestedQuantity.HasValue && reqItem.RequestedQuantity.Value > 0)
            {
                orderQty = reqItem.RequestedQuantity.Value;
            }
            else
            {
                orderQty = assessedLine.ShortageQuantity;
            }

            if (orderQty <= 0)
            {
                continue;
            }

            decimal? estPrice = reqItem.EstimatedUnitPrice ?? assessedLine.EstimatedUnitPrice;

            prItems.Add(new CreatePurchaseRequisitionItemRequest
            {
                ProductId = reqItem.ProductId,
                UnitOfMeasureId = reqItem.UnitOfMeasureId ?? assessedLine.UnitOfMeasureId,
                Quantity = orderQty,
                EstimatedUnitPrice = estPrice,
                Remarks = !string.IsNullOrWhiteSpace(reqItem.Remarks)
                    ? reqItem.Remarks
                    : $"Replenishment for {assessedLine.ProductCode} (Warehouse: {assessment.WarehouseCode})"
            });

            decimal price = estPrice ?? 0m;
            decimal lineTotal = Math.Round(orderQty * price, 2, MidpointRounding.AwayFromZero);

            lineResults.Add(new DraftPrLineResultDto
            {
                ProductId = assessedLine.ProductId,
                ProductCode = assessedLine.ProductCode,
                RequestedQuantity = assessedLine.RequiredQuantity,
                ShortageQuantity = assessedLine.ShortageQuantity,
                RequisitionQuantity = orderQty,
                EstimatedUnitPrice = price,
                EstimatedLineTotal = lineTotal
            });
        }

        if (prItems.Count == 0)
        {
            return new DraftPurchaseRequisitionResult
            {
                Status = "None",
                Message = $"No replenishment purchase requisition created. All items have sufficient available stock at warehouse '{assessment.WarehouseName}'."
            };
        }

        // 7. Determine deterministic Requisition Number for atomic DB-level concurrency deduplication
        string reqNumber;
        if (!string.IsNullOrWhiteSpace(request.RequisitionNumber))
        {
            reqNumber = request.RequisitionNumber.Trim();
        }
        else if (idemKey != null && idemTag != null)
        {
            string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(idemKey)))[..12];
            int cancelledCount = await _context.PurchaseRequisitions
                .CountAsync(pr => pr.TenantId == tenantId && pr.CompanyId == companyId && pr.Notes != null && pr.Notes.Contains(idemTag) && pr.Status == PurchaseRequisitionStatus.Cancelled);
            reqNumber = cancelledCount == 0 ? $"PR-REPL-{hash}".ToUpperInvariant() : $"PR-REPL-{hash}-A{cancelledCount + 1}".ToUpperInvariant();
        }
        else if (sourceTag != null)
        {
            string shortId = request.SourceEntityId!.Value.ToString("N")[..8].ToUpperInvariant();
            int cancelledCount = await _context.PurchaseRequisitions
                .CountAsync(pr => pr.TenantId == tenantId && pr.CompanyId == companyId && pr.Notes != null && pr.Notes.Contains(sourceTag) && pr.Status == PurchaseRequisitionStatus.Cancelled);
            reqNumber = cancelledCount == 0 ? $"PR-REPL-{shortId}".ToUpperInvariant() : $"PR-REPL-{shortId}-A{cancelledCount + 1}".ToUpperInvariant();
        }
        else
        {
            reqNumber = $"PR-REPL-{Guid.NewGuid():N}"[..14].ToUpperInvariant();
        }

        // 8. Re-check right before insert in case a concurrent request committed an active PR during stock assessment
        if (idemTag != null || sourceTag != null)
        {
            var activeWinner = await _context.PurchaseRequisitions
                .Include(pr => pr.Items)
                .AsNoTracking()
                .FirstOrDefaultAsync(pr => pr.TenantId == tenantId
                                        && pr.CompanyId == companyId
                                        && pr.IsActive
                                        && pr.Status != PurchaseRequisitionStatus.Cancelled
                                        && ((idemTag != null && pr.Notes != null && pr.Notes.Contains(idemTag))
                                            || (sourceTag != null && pr.Notes != null && pr.Notes.Contains(sourceTag))));

            if (activeWinner != null)
            {
                return await MapExistingToResultAsync(
                    activeWinner,
                    isReused: true,
                    message: $"Existing active Purchase Requisition '{activeWinner.RequisitionNumber}' retrieved (concurrent race reconciled). Zero stock deducted or allocated.");
            }
        }

        // 9. Assemble structured Notes with audit and idempotency headers
        string sourceRef = request.SourceType != ReplenishmentSourceType.Manual
            ? $"[{request.SourceType}" + (!string.IsNullOrWhiteSpace(request.SourceDocumentNumber) ? $" {request.SourceDocumentNumber}" : "") + "]"
            : "[Replenishment]";

        var auditTags = new List<string>();
        if (idemTag != null) auditTags.Add(idemTag);
        if (sourceTag != null) auditTags.Add(sourceTag);
        auditTags.Add(sourceRef);

        string notesHeader = string.Join(" ", auditTags);
        string notes = $"{notesHeader} Stock replenishment for warehouse '{assessment.WarehouseCode}'." +
                       (!string.IsNullOrWhiteSpace(request.Notes) ? $" {request.Notes.Trim()}" : "");

        var prCreateRequest = new CreatePurchaseRequisitionRequest
        {
            RequisitionNumber = reqNumber,
            RequisitionDate = DateTime.UtcNow,
            RequiredDate = request.RequiredDate ?? DateTime.UtcNow.AddDays(7),
            Notes = notes,
            Items = prItems
        };

        // 9. Delegate to IPurchaseRequisitionService with atomic unique-index collision recovery
        PurchaseRequisition createdPr;
        try
        {
            createdPr = await _purchaseRequisitionService.CreateRequisitionAsync(
                tenantId,
                companyId,
                branchId,
                prCreateRequest,
                actorUserId,
                actorUserName);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase))
        {
            // Database-level atomic concurrency protection: Another concurrent request committed with the same requisition number.
            var collisionWinner = await _context.PurchaseRequisitions
                .Include(pr => pr.Items)
                .AsNoTracking()
                .FirstOrDefaultAsync(pr => pr.TenantId == tenantId && pr.CompanyId == companyId && pr.RequisitionNumber == reqNumber);

            if (collisionWinner != null)
            {
                return await MapExistingToResultAsync(
                    collisionWinner,
                    isReused: true,
                    message: $"Existing active Purchase Requisition '{collisionWinner.RequisitionNumber}' retrieved (concurrent race reconciled). Zero stock deducted or allocated.");
            }

            throw;
        }

        return new DraftPurchaseRequisitionResult
        {
            RequisitionId = createdPr.Id,
            RequisitionNumber = createdPr.RequisitionNumber,
            Status = createdPr.Status.ToString(),
            RequisitionDate = createdPr.RequisitionDate,
            RequiredDate = createdPr.RequiredDate,
            ItemCount = createdPr.Items.Count,
            TotalAmount = createdPr.TotalAmount,
            Items = lineResults,
            IsReused = false,
            Message = $"Draft Purchase Requisition '{createdPr.RequisitionNumber}' created successfully for {createdPr.Items.Count} item(s). Zero stock deducted or allocated."
        };
    }

    private async Task<DraftPurchaseRequisitionResult> MapExistingToResultAsync(
        PurchaseRequisition pr,
        bool isReused,
        string message)
    {
        var productIds = pr.Items.Select(i => i.ProductId).Distinct().ToList();
        var productMap = await _context.Products
            .AsNoTracking()
            .Where(p => p.TenantId == pr.TenantId && productIds.Contains(p.ProductId))
            .ToDictionaryAsync(p => p.ProductId, p => p.ProductCode);

        var lineResults = pr.Items.Select(item => new DraftPrLineResultDto
        {
            ProductId = item.ProductId,
            ProductCode = productMap.TryGetValue(item.ProductId, out var code) ? code : item.ProductId.ToString(),
            RequestedQuantity = item.Quantity,
            ShortageQuantity = item.Quantity,
            RequisitionQuantity = item.Quantity,
            EstimatedUnitPrice = item.EstimatedUnitPrice,
            EstimatedLineTotal = item.EstimatedLineTotal
        }).ToList();

        return new DraftPurchaseRequisitionResult
        {
            RequisitionId = pr.Id,
            RequisitionNumber = pr.RequisitionNumber,
            Status = pr.Status.ToString(),
            RequisitionDate = pr.RequisitionDate,
            RequiredDate = pr.RequiredDate,
            ItemCount = pr.Items.Count,
            TotalAmount = pr.TotalAmount,
            Items = lineResults,
            IsReused = isReused,
            Message = message
        };
    }
}
