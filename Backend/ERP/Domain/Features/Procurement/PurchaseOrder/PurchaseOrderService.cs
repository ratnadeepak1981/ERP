using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Domain.Features.MasterData.Product;
using Domain.Features.MasterData.Supplier;
using Domain.Features.MasterData.UnitOfMeasure;
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

    public PurchaseOrderService(
        IPurchaseOrderRepository repository,
        IProductRepository productRepository,
        ISupplierRepository supplierRepository,
        ISupplierProductPriceService supplierPriceService,
        IUnitOfMeasureRepository uomRepository,
        IApprovalWorkflowService approvalService)
    {
        _repository = repository;
        _productRepository = productRepository;
        _supplierRepository = supplierRepository;
        _supplierPriceService = supplierPriceService;
        _uomRepository = uomRepository;
        _approvalService = approvalService;
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

                order.AddItem(itemReq.ProductId, itemReq.Quantity, unitPrice, uomId);
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

        order.AddItem(request.ProductId, request.Quantity, unitPrice, uomId);

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
}
