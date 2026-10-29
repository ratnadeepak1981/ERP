using System.Linq;
using Domain.Infrastructure.Persistence;
using ERP.Domain.Features.MasterData.Warehouse;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Domain.Features.MasterData.Product;
using Domain.Features.MasterData.Supplier;
using Domain.Features.MasterData.UnitOfMeasure;
using Domain.Features.MasterData.TaxCode;
using Domain.Features.Procurement.Approval;
using ERP.Domain.Features.MasterData.Supplier;

namespace Domain.Features.Procurement.PurchaseOrder;

public class PurchaseOrderService : IPurchaseOrderService
{
    private readonly IPurchaseOrderRepository _repository;
    private readonly IProductRepository _productRepository;
    private readonly ISupplierRepository _supplierRepository;
    private readonly ISupplierProductPriceService _supplierPriceService;
    private readonly IUnitOfMeasureRepository _uomRepository;
    private readonly IApprovalWorkflowService _approvalService;
    private readonly ITaxCodeRepository? _taxCodeRepository;
    private readonly DomainDbContext? _context;
    private readonly IUnitOfMeasureService? _uomService;

    public PurchaseOrderService(
        IPurchaseOrderRepository repository,
        IProductRepository productRepository,
        ISupplierRepository supplierRepository,
        ISupplierProductPriceService supplierPriceService,
        IUnitOfMeasureRepository uomRepository,
        IApprovalWorkflowService approvalService,
        ITaxCodeRepository? taxCodeRepository = null,
        DomainDbContext? context = null,
        IUnitOfMeasureService? uomService = null)
    {
        _repository = repository;
        _productRepository = productRepository;
        _supplierRepository = supplierRepository;
        _supplierPriceService = supplierPriceService;
        _uomRepository = uomRepository;
        _approvalService = approvalService;
        _taxCodeRepository = taxCodeRepository;
        _context = context;
        _uomService = uomService;
    }

    public async Task<List<PurchaseOrder>> GetOrdersAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId)
    {
        return await _repository.GetByBranchAsync(
            tenantId,
            companyId,
            branchId);
    }

    public async Task<List<PurchaseOrder>> GetOrdersByCompanyAsync(
        Guid tenantId,
        Guid companyId,
        IEnumerable<Guid>? branchIds = null)
    {
        return await _repository.GetByCompanyAsync(
            tenantId,
            companyId,
            branchIds);
    }

    public async Task<PurchaseOrder?> GetOrderAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid orderId)
    {
        return await _repository.GetByIdAsync(
            tenantId,
            companyId,
            branchId,
            orderId);
    }

    public async Task<PurchaseOrder?> GetOrderByIdAsync(
        Guid tenantId,
        Guid companyId,
        Guid orderId)
    {
        return await _repository.GetByIdAndCompanyAsync(
            tenantId,
            companyId,
            orderId);
    }

    public async Task<PurchaseOrder> CreateOrderAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        CreatePurchaseOrderRequest request,
        Guid? actorUserId = null,
        string? actorUserName = null)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        if (string.IsNullOrWhiteSpace(request.OrderNumber))
            throw new ArgumentException("Order number is required.", nameof(request.OrderNumber));

        string orderNumber = request.OrderNumber.Trim();

        if (await _repository.ExistsOrderNumberAsync(tenantId, companyId, orderNumber))
        {
            throw new InvalidOperationException(
                $"Purchase Order '{orderNumber}' already exists for this company.");
        }

        // Validate supplier if provided
        if (request.SupplierId.HasValue)
        {
            var supplier = await _supplierRepository.GetSupplierByIdAsync(tenantId, request.SupplierId.Value);
            if (supplier == null)
            {
                throw new InvalidOperationException($"Supplier '{request.SupplierId.Value}' was not found or does not belong to this tenant.");
            }
        }

        var order = PurchaseOrder.Create(
            tenantId,
            companyId,
            branchId,
            orderNumber,
            request.OrderDate,
            request.SupplierId,
            actorUserId);

        if (request.Items != null && request.Items.Count > 0)
        {
            foreach (var itemReq in request.Items)
            {
                var product = await _productRepository.GetByIdAsync(tenantId, itemReq.ProductId);
                if (product == null)
                {
                    throw new InvalidOperationException(
                        $"Product '{itemReq.ProductId}' was not found or does not belong to this tenant.");
                }

                // Resolve UOM: either specified and verified, or fall back to product's UOM
                Guid? uomId = itemReq.UnitOfMeasureId ?? product.UnitOfMeasureId;
                if (uomId.HasValue)
                {
                    var uom = await _uomRepository.GetUnitByIdAsync(tenantId, uomId.Value);
                    if (uom == null)
                    {
                        throw new InvalidOperationException($"Unit of measure '{uomId.Value}' was not found or does not belong to this tenant.");
                    }
                }

                // Resolve UnitPrice: if specified, use it directly (manual PO without price card).
                // If not specified and supplier is present, attempt lookup from active price card.
                decimal unitPrice;
                if (itemReq.UnitPrice.HasValue)
                {
                    unitPrice = itemReq.UnitPrice.Value;
                }
                else if (request.SupplierId.HasValue)
                {
                    var activePrice = await _supplierPriceService.GetActivePriceAsync(
                        tenantId,
                        request.SupplierId.Value,
                        itemReq.ProductId,
                        request.OrderDate ?? DateTime.UtcNow);

                    if (activePrice != null)
                    {
                        unitPrice = activePrice.UnitPrice;
                    }
                    else
                    {
                        throw new ArgumentException($"UnitPrice is required because no active price card exists for product '{product.ProductCode}' and supplier.");
                    }
                }
                else
                {
                    throw new ArgumentException("UnitPrice must be specified.");
                }

                decimal taxRate = await ResolveTaxRateAsync(tenantId, itemReq.TaxCodeId, itemReq.TaxRate, order.OrderDate);
                order.AddItem(
                    itemReq.ProductId,
                    itemReq.Quantity,
                    unitPrice,
                    uomId,
                    itemReq.DiscountRate,
                    itemReq.TaxCodeId,
                    taxRate,
                    itemReq.IsTaxInclusive,
                    itemReq.Notes);

            }
        }

        try
        {
            await _repository.AddAsync(order);
            await _repository.SaveChangesAsync();
            return order;
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException ex)
        {
            var msg = ex.InnerException?.Message ?? ex.Message;
            if (msg.Contains("IX_PurchaseOrders_TenantId_CompanyId_OrderNumber") ||
                msg.Contains("UNIQUE KEY") ||
                msg.Contains("duplicate key"))
            {
                throw new InvalidOperationException(
                    $"Purchase Order '{orderNumber}' already exists for this company.", ex);
            }

            throw;
        }
    }

    public async Task<PurchaseOrderItem> AddItemAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid orderId,
        CreatePurchaseOrderItemRequest request)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        var order = await _repository.GetByIdAsync(
            tenantId,
            companyId,
            branchId,
            orderId);

        if (order == null)
        {
            throw new KeyNotFoundException(
                "Purchase order was not found in the authorized branch/company scope.");
        }

        var product = await _productRepository.GetByIdAsync(tenantId, request.ProductId);
        if (product == null)
        {
            throw new InvalidOperationException(
                $"Product '{request.ProductId}' was not found or does not belong to this tenant.");
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

        decimal unitPrice;
        if (request.UnitPrice.HasValue)
        {
            unitPrice = request.UnitPrice.Value;
        }
        else if (order.SupplierId.HasValue)
        {
            var activePrice = await _supplierPriceService.GetActivePriceAsync(
                tenantId,
                order.SupplierId.Value,
                request.ProductId,
                order.OrderDate);

            if (activePrice != null)
            {
                unitPrice = activePrice.UnitPrice;
            }
            else
            {
                throw new ArgumentException($"UnitPrice is required because no active price card exists for product '{product.ProductCode}' and supplier.");
            }
        }
        else
        {
            throw new ArgumentException("UnitPrice must be specified.");
        }

        decimal taxRate = await ResolveTaxRateAsync(tenantId, request.TaxCodeId, request.TaxRate, order.OrderDate);
        order.AddItem(
            request.ProductId,
            request.Quantity,
            unitPrice,
            uomId,
            request.DiscountRate,
            request.TaxCodeId,
            taxRate,
            request.IsTaxInclusive,
            request.Notes);


        await _repository.SaveChangesAsync();

        return order.Items[^1];
    }

    public async Task<PurchaseOrder> SubmitOrderAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid orderId,
        Guid actorUserId,
        string actorUserName)
    {
        var order = await _repository.GetByIdAsync(tenantId, companyId, branchId, orderId)
            ?? throw new KeyNotFoundException("Purchase order not found in authorized scope.");

        var settings = await _approvalService.GetApprovalSettingsAsync(tenantId);

        if (settings.ApprovalRequired)
        {
            order.Submit();
            await _repository.SaveChangesAsync();

            await _approvalService.RecordSubmissionAsync(
                tenantId,
                "PurchaseOrder",
                order.Id,
                actorUserId,
                actorUserName,
                order.TotalAmount);
        }
        else
        {
            // Simple Approval disabled: automatically approve
            order.AutoApprove();
            await ApplyPurchaseOrderOnOrderDeltaAsync(order, isApproval: true);
            await _repository.SaveChangesAsync();

            await _approvalService.RecordAutoApprovalAsync(
                tenantId,
                "PurchaseOrder",
                order.Id,
                actorUserId,
                actorUserName,
                order.TotalAmount);
        }

        return order;
    }

    public async Task<PurchaseOrder> ApproveOrderAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid orderId,
        Guid approverUserId,
        string approverUserName,
        string? remarks = null)
    {
        var order = await _repository.GetByIdAsync(tenantId, companyId, branchId, orderId)
            ?? throw new KeyNotFoundException("Purchase order not found in authorized scope.");

        // Guard self-approval
        Guid creatorId = order.CreatedByUserId ?? Guid.Empty;
        await _approvalService.RecordApprovalAsync(
            tenantId,
            "PurchaseOrder",
            order.Id,
            approverUserId,
            approverUserName,
            creatorId,
            order.TotalAmount,
            remarks);

        order.Approve();
        await ApplyPurchaseOrderOnOrderDeltaAsync(order, isApproval: true);
        await _repository.SaveChangesAsync();

        return order;
    }

    public async Task<PurchaseOrder> RejectOrderAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid orderId,
        Guid approverUserId,
        string approverUserName,
        string remarks)
    {
        var order = await _repository.GetByIdAsync(tenantId, companyId, branchId, orderId)
            ?? throw new KeyNotFoundException("Purchase order not found in authorized scope.");

        await _approvalService.RecordRejectionAsync(
            tenantId,
            "PurchaseOrder",
            order.Id,
            approverUserId,
            approverUserName,
            remarks,
            order.TotalAmount);

        order.Reject();
        await _repository.SaveChangesAsync();

        return order;
    }

    public async Task<PurchaseOrder> CancelOrderAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid orderId,
        Guid actorUserId,
        string actorUserName,
        string? remarks = null)
    {
        var order = await _repository.GetByIdAsync(tenantId, companyId, branchId, orderId)
            ?? throw new KeyNotFoundException("Purchase order not found in authorized scope.");

        if (order.Status == PurchaseOrderStatus.Approved)
        {
            await ApplyPurchaseOrderOnOrderDeltaAsync(order, isApproval: false);
        }

        order.Cancel();
        await _repository.SaveChangesAsync();

        await _approvalService.RecordCancellationAsync(
            tenantId,
            "PurchaseOrder",
            order.Id,
            actorUserId,
            actorUserName,
            order.TotalAmount,
            remarks);

        return order;
    }

    public async Task<List<ApprovalAuditRecord>> GetApprovalHistoryAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid orderId)
    {
        var order = await _repository.GetByIdAsync(tenantId, companyId, branchId, orderId)
            ?? throw new KeyNotFoundException("Purchase order not found in authorized scope.");

        return await _approvalService.GetApprovalHistoryAsync(
            tenantId,
            "PurchaseOrder",
            order.Id);
    }
    private async Task<decimal> ResolveTaxRateAsync(Guid tenantId, Guid? taxCodeId, decimal explicitTaxRate, DateTime orderDate)
    {
        if (taxCodeId.HasValue && _taxCodeRepository != null)
        {
            var taxCode = await _taxCodeRepository.GetTaxCodeByIdAsync(tenantId, taxCodeId.Value);
            if (taxCode == null)
            {
                throw new InvalidOperationException($"Tax code '{taxCodeId.Value}' was not found or does not belong to this tenant.");
            }
            if (!taxCode.IsActive)
            {
                throw new InvalidOperationException($"Tax code '{taxCode.Code}' is inactive.");
            }
            if (!taxCode.AppliesTo(TaxApplicability.Purchase))
            {
                throw new InvalidOperationException($"Tax code '{taxCode.Code}' cannot be applied to purchase transactions.");
            }
            if (!taxCode.IsEffectiveAt(orderDate))
            {
                throw new InvalidOperationException($"Tax code '{taxCode.Code}' is not effective on {orderDate:yyyy-MM-dd}.");
            }
            return taxCode.Rate;
        }

        return explicitTaxRate;
    }

    private async Task ApplyPurchaseOrderOnOrderDeltaAsync(PurchaseOrder order, bool isApproval)
    {
        if (_context == null) return;

        var activeItems = order.Items.Where(i => i.IsActive && (i.Quantity - i.ReceivedQuantity) > 0).ToList();
        if (activeItems.Count == 0) return;

        var warehouses = await _context.Warehouses
            .Where(w => w.TenantId == order.TenantId && w.BranchId == order.BranchId && w.IsActive)
            .ToListAsync();

        var targetWarehouse = warehouses.FirstOrDefault();
        if (targetWarehouse == null)
        {
            targetWarehouse = await _context.Warehouses
                .FirstOrDefaultAsync(w => w.TenantId == order.TenantId && w.CompanyId == order.CompanyId && w.IsActive);
        }

        if (targetWarehouse == null) return;

        var locations = await _context.WarehouseLocations
            .Where(l => l.TenantId == order.TenantId && l.WarehouseId == targetWarehouse.WarehouseId && l.IsActive)
            .ToListAsync();

        var targetLocation = locations.FirstOrDefault();
        if (targetLocation == null) return;

        foreach (var item in activeItems)
        {
            var product = await _productRepository.GetByIdAsync(order.TenantId, item.ProductId);
            if (product == null || !product.IsStockTracked) continue;

            decimal delta = item.Quantity - item.ReceivedQuantity;
            if (delta <= 0) continue;

            decimal baseDelta = delta;
            if (item.UnitOfMeasureId.HasValue && product.UnitOfMeasureId.HasValue && item.UnitOfMeasureId.Value != product.UnitOfMeasureId.Value)
            {
                if (_uomService != null)
                {
                    var converted = await _uomService.ConvertQuantityAsync(
                        order.TenantId,
                        item.UnitOfMeasureId.Value,
                        product.UnitOfMeasureId.Value,
                        delta
                    );
                    baseDelta = converted.ConvertedQuantity;
                }
            }

            var balance = await _context.InventoryBalances
                .FirstOrDefaultAsync(b => b.TenantId == order.TenantId
                                       && b.WarehouseId == targetWarehouse.WarehouseId
                                       && b.WarehouseLocationId == targetLocation.WarehouseLocationId
                                       && b.ProductId == item.ProductId
                                       && b.BatchNumber == string.Empty
                                       && b.SerialNumber == string.Empty);

            if (balance == null && isApproval)
            {
                balance = ERP.Domain.Features.Inventory.InventoryBalance.Create(
                    order.TenantId,
                    targetWarehouse.WarehouseId,
                    targetLocation.WarehouseLocationId,
                    item.ProductId,
                    batchNumber: null,
                    serialNumber: null);
                _context.InventoryBalances.Add(balance);
            }

            if (balance != null)
            {
                decimal appliedDelta = isApproval ? baseDelta : -Math.Min(balance.QuantityOnOrder, baseDelta);
                if (appliedDelta != 0)
                {
                    balance.ApplyOnOrderChange(appliedDelta);
                }
            }
        }
    }
}