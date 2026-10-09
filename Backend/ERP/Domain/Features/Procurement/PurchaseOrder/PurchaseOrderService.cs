using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Domain.Features.MasterData.Product;

namespace Domain.Features.Procurement.PurchaseOrder;

public class PurchaseOrderService : IPurchaseOrderService
{
    private readonly IPurchaseOrderRepository _repository;
    private readonly IProductRepository _productRepository;

    public PurchaseOrderService(
        IPurchaseOrderRepository repository,
        IProductRepository productRepository)
    {
        _repository = repository;
        _productRepository = productRepository;
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
        CreatePurchaseOrderRequest request)
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

        var order = PurchaseOrder.Create(
            tenantId,
            companyId,
            branchId,
            orderNumber,
            request.OrderDate);

        if (request.Items != null && request.Items.Count > 0)
        {
            foreach (var itemReq in request.Items)
            {
                // Verify Product belongs to the tenant (master table: shared across companies in the same tenant)
                var product = await _productRepository.GetByIdAsync(tenantId, itemReq.ProductId);
                if (product == null)
                {
                    throw new InvalidOperationException(
                        $"Product '{itemReq.ProductId}' was not found or does not belong to this tenant.");
                }

                order.AddItem(itemReq.ProductId, itemReq.Quantity, itemReq.UnitPrice);
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
            // Concurrency safeguard: handle database unique index violation on (TenantId, CompanyId, OrderNumber)
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

        // Load the order scoped to tenant, company, and branch
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

        // Verify product belongs to the tenant
        var product = await _productRepository.GetByIdAsync(tenantId, request.ProductId);
        if (product == null)
        {
            throw new InvalidOperationException(
                $"Product '{request.ProductId}' was not found or does not belong to this tenant.");
        }

        order.AddItem(request.ProductId, request.Quantity, request.UnitPrice);

        await _repository.SaveChangesAsync();

        return order.Items[^1];
    }
}
