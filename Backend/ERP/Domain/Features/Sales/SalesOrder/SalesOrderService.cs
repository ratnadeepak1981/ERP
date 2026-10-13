using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domain.Features.MasterData.Product;
using Domain.Features.MasterData.UnitOfMeasure;
using ERP.Domain.Features.MasterData.Customer;
using Microsoft.EntityFrameworkCore;

namespace Domain.Features.Sales.SalesOrder;

public class SalesOrderService : ISalesOrderService
{
    private readonly ISalesOrderRepository _repository;
    private readonly ICustomerRepository _customerRepository;
    private readonly IProductRepository _productRepository;
    private readonly IUnitOfMeasureRepository _uomRepository;

    public SalesOrderService(
        ISalesOrderRepository repository,
        ICustomerRepository customerRepository,
        IProductRepository productRepository,
        IUnitOfMeasureRepository uomRepository)
    {
        _repository = repository;
        _customerRepository = customerRepository;
        _productRepository = productRepository;
        _uomRepository = uomRepository;
    }

    public async Task<List<SalesOrderDto>> GetOrdersAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        SalesOrderStatus? status = null)
    {
        var orders = await _repository.GetByBranchAsync(tenantId, companyId, branchId, status);
        return orders.Select(MapToDto).ToList();
    }

    public async Task<SalesOrderDto?> GetOrderAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid orderId)
    {
        var order = await _repository.GetByIdAsync(tenantId, companyId, branchId, orderId);
        return order == null ? null : MapToDto(order);
    }

    public async Task<SalesOrderDto> CreateOrderAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        CreateSalesOrderRequest request,
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
                $"Sales Order '{orderNumber}' already exists for this company.");
        }

        // Validate Customer
        var customer = await _customerRepository.GetCustomerByIdAsync(tenantId, request.CustomerId);
        if (customer == null)
        {
            throw new InvalidOperationException(
                $"Customer '{request.CustomerId}' was not found or does not belong to this tenant.");
        }

        if (!customer.IsActive)
        {
            throw new InvalidOperationException($"Customer '{customer.CustomerCode}' is inactive.");
        }

        string? paymentTerms = !string.IsNullOrWhiteSpace(request.PaymentTerms)
            ? request.PaymentTerms.Trim()
            : customer.PaymentTerms;

        var order = SalesOrder.Create(
            tenantId,
            companyId,
            branchId,
            customer.CustomerId,
            orderNumber,
            request.DocumentType,
            request.OrderDate,
            request.DeliveryDate,
            request.Currency,
            paymentTerms,
            request.Notes,
            actorUserId,
            request.OpportunityId);

        if (request.Items != null && request.Items.Count > 0)
        {
            foreach (var itemReq in request.Items)
            {
                var (product, uomId, unitPrice) = await ValidateAndResolveItemAsync(tenantId, itemReq);

                order.AddItem(
                    product.ProductId,
                    itemReq.Quantity,
                    unitPrice,
                    itemReq.DiscountRate,
                    itemReq.TaxRate,
                    uomId,
                    itemReq.Notes);
            }
        }

        try
        {
            await _repository.AddAsync(order);
            await _repository.SaveChangesAsync();
            return MapToDto(order);
        }
        catch (DbUpdateException ex)
        {
            var msg = ex.InnerException?.Message ?? ex.Message;
            if (msg.Contains("IX_SalesOrders_TenantId_CompanyId_OrderNumber") ||
                msg.Contains("UNIQUE KEY") ||
                msg.Contains("duplicate key"))
            {
                throw new InvalidOperationException(
                    $"Sales Order '{orderNumber}' already exists for this company.", ex);
            }

            throw;
        }
    }

    public async Task<SalesOrderItemDto> AddItemAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid orderId,
        CreateSalesOrderItemRequest request)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        var order = await _repository.GetByIdAsync(tenantId, companyId, branchId, orderId);
        if (order == null)
        {
            throw new KeyNotFoundException(
                "Sales order was not found in the authorized branch/company scope.");
        }

        var (product, uomId, unitPrice) = await ValidateAndResolveItemAsync(tenantId, request);

        var addedItem = order.AddItem(
            product.ProductId,
            request.Quantity,
            unitPrice,
            request.DiscountRate,
            request.TaxRate,
            uomId,
            request.Notes);

        await _repository.AddItemAsync(addedItem);
        await _repository.SaveChangesAsync();

        return MapItemToDto(addedItem);
    }

    public async Task<SalesOrderDto> SubmitOrderAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid orderId,
        Guid actorUserId,
        string actorUserName)
    {
        var order = await _repository.GetByIdAsync(tenantId, companyId, branchId, orderId)
            ?? throw new KeyNotFoundException("Sales order not found in authorized scope.");

        order.Submit();
        await _repository.SaveChangesAsync();

        return MapToDto(order);
    }

    public async Task<SalesOrderDto> ConfirmOrderAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid orderId,
        Guid actorUserId,
        string actorUserName,
        string? remarks = null)
    {
        var order = await _repository.GetByIdAsync(tenantId, companyId, branchId, orderId)
            ?? throw new KeyNotFoundException("Sales order not found in authorized scope.");

        order.Confirm();
        if (!string.IsNullOrWhiteSpace(remarks))
        {
            order.Notes = string.IsNullOrWhiteSpace(order.Notes)
                ? $"[Confirmed] {remarks}"
                : $"{order.Notes} | [Confirmed] {remarks}";
        }

        await _repository.SaveChangesAsync();

        return MapToDto(order);
    }

    public async Task<SalesOrderDto> RejectOrderAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid orderId,
        Guid actorUserId,
        string actorUserName,
        string remarks)
    {
        if (string.IsNullOrWhiteSpace(remarks))
            throw new ArgumentException("Rejection remarks are required.", nameof(remarks));

        var order = await _repository.GetByIdAsync(tenantId, companyId, branchId, orderId)
            ?? throw new KeyNotFoundException("Sales order not found in authorized scope.");

        order.Reject();
        order.Notes = string.IsNullOrWhiteSpace(order.Notes)
            ? $"[Rejected] {remarks}"
            : $"{order.Notes} | [Rejected] {remarks}";

        await _repository.SaveChangesAsync();

        return MapToDto(order);
    }

    public async Task<SalesOrderDto> CancelOrderAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid orderId,
        Guid actorUserId,
        string actorUserName,
        string? remarks = null)
    {
        var order = await _repository.GetByIdAsync(tenantId, companyId, branchId, orderId)
            ?? throw new KeyNotFoundException("Sales order not found in authorized scope.");

        order.Cancel();
        if (!string.IsNullOrWhiteSpace(remarks))
        {
            order.Notes = string.IsNullOrWhiteSpace(order.Notes)
                ? $"[Cancelled] {remarks}"
                : $"{order.Notes} | [Cancelled] {remarks}";
        }

        await _repository.SaveChangesAsync();

        return MapToDto(order);
    }

    private async Task<(Product Product, Guid? UomId, decimal UnitPrice)> ValidateAndResolveItemAsync(
        Guid tenantId,
        CreateSalesOrderItemRequest itemReq)
    {
        var product = await _productRepository.GetByIdAsync(tenantId, itemReq.ProductId);
        if (product == null)
        {
            throw new InvalidOperationException(
                $"Product '{itemReq.ProductId}' was not found or does not belong to this tenant.");
        }

        if (!product.IsActive)
        {
            throw new InvalidOperationException($"Product '{product.ProductCode}' is inactive.");
        }

        if (!product.IsSellable)
        {
            throw new InvalidOperationException($"Product '{product.ProductCode}' is not configured as sellable.");
        }

        Guid? uomId = itemReq.UnitOfMeasureId ?? product.UnitOfMeasureId;
        if (uomId.HasValue)
        {
            var uom = await _uomRepository.GetUnitByIdAsync(tenantId, uomId.Value);
            if (uom == null)
            {
                throw new InvalidOperationException(
                    $"Unit of measure '{uomId.Value}' was not found or does not belong to this tenant.");
            }
        }

        decimal unitPrice;
        if (itemReq.UnitPrice.HasValue)
        {
            unitPrice = itemReq.UnitPrice.Value;
        }
        else if (product.SellingPrice > 0)
        {
            unitPrice = product.SellingPrice;
        }
        else
        {
            throw new ArgumentException($"UnitPrice is required for product '{product.ProductCode}' because it has no default selling price.");
        }

        return (product, uomId, unitPrice);
    }

    private static SalesOrderDto MapToDto(SalesOrder order)
    {
        return new SalesOrderDto
        {
            Id = order.Id,
            TenantId = order.TenantId,
            CompanyId = order.CompanyId,
            BranchId = order.BranchId,
            CustomerId = order.CustomerId,
            OrderNumber = order.OrderNumber,
            DocumentType = order.DocumentType,
            OrderDate = order.OrderDate,
            DeliveryDate = order.DeliveryDate,
            Currency = order.Currency,
            PaymentTerms = order.PaymentTerms,
            Status = order.Status,
            SubTotal = order.SubTotal,
            DiscountAmount = order.DiscountAmount,
            TaxAmount = order.TaxAmount,
            TotalAmount = order.TotalAmount,
            Notes = order.Notes,
            CreatedByUserId = order.CreatedByUserId,
            OpportunityId = order.OpportunityId,
            IsActive = order.IsActive,
            Items = order.Items.Select(MapItemToDto).ToList()
        };
    }

    private static SalesOrderItemDto MapItemToDto(SalesOrderItem item)
    {
        return new SalesOrderItemDto
        {
            Id = item.Id,
            SalesOrderId = item.SalesOrderId,
            ProductId = item.ProductId,
            UnitOfMeasureId = item.UnitOfMeasureId,
            Quantity = item.Quantity,
            UnitPrice = item.UnitPrice,
            DiscountRate = item.DiscountRate,
            DiscountAmount = item.DiscountAmount,
            TaxRate = item.TaxRate,
            TaxAmount = item.TaxAmount,
            SubTotal = item.SubTotal,
            LineTotal = item.LineTotal,
            Notes = item.Notes,
            IsActive = item.IsActive
        };
    }
}
