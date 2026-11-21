using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Domain.Features.Sales.Invoice;
using Domain.Features.Sales.SalesOrder;
using Domain.Features.Sales.SalesQuotation;
using Domain.Features.Sales.Shipment;
using Domain.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Features.Sales.CustomerPortal;

public class CustomerOrderPortalService : ICustomerOrderPortalService
{
    private readonly DomainDbContext _dbContext;

    public CustomerOrderPortalService(DomainDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<List<CustomerOrderSummaryDto>> GetOrdersAsync(
        Guid tenantId,
        Guid customerId,
        Guid? sellingCompanyId = null,
        SalesOrderStatus? status = null,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant ID is required.", nameof(tenantId));
        if (customerId == Guid.Empty) throw new ArgumentException("Customer ID is required.", nameof(customerId));

        var query = _dbContext.SalesOrders
            .AsNoTracking()
            .Where(o => o.TenantId == tenantId && o.CustomerId == customerId && o.IsActive);

        if (sellingCompanyId.HasValue && sellingCompanyId.Value != Guid.Empty)
        {
            query = query.Where(o => o.CompanyId == sellingCompanyId.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(o => o.Status == status.Value);
        }

        var orders = await query
            .OrderByDescending(o => o.OrderDate)
            .Select(o => new
            {
                o.Id,
                o.OrderNumber,
                o.OrderDate,
                o.DeliveryDate,
                Status = o.Status.ToString(),
                o.Currency,
                o.SubTotal,
                o.DiscountAmount,
                o.TaxAmount,
                o.TotalAmount,
                o.DeliveryAddressSnapshot,
                ItemCount = o.Items.Count(i => i.IsActive)
            })
            .ToListAsync(ct);

        var orderIds = orders.Select(o => o.Id).ToList();

        var shipments = await _dbContext.SalesShipments
            .AsNoTracking()
            .Where(s => s.TenantId == tenantId && s.CustomerId == customerId && orderIds.Contains(s.SalesOrderId) && s.IsActive)
            .OrderByDescending(s => s.ShipmentDate)
            .Select(s => new
            {
                s.SalesOrderId,
                Status = s.Status.ToString(),
                s.TrackingNumber
            })
            .ToListAsync(ct);

        var latestShipmentByOrder = shipments
            .GroupBy(s => s.SalesOrderId)
            .ToDictionary(g => g.Key, g => g.First());

        var result = new List<CustomerOrderSummaryDto>();
        foreach (var o in orders)
        {
            latestShipmentByOrder.TryGetValue(o.Id, out var shipment);
            result.Add(new CustomerOrderSummaryDto
            {
                OrderId = o.Id,
                OrderNumber = o.OrderNumber,
                OrderDate = o.OrderDate,
                DeliveryDate = o.DeliveryDate,
                Status = o.Status,
                Currency = o.Currency,
                SubTotal = o.SubTotal,
                DiscountAmount = o.DiscountAmount,
                TaxAmount = o.TaxAmount,
                TotalAmount = o.TotalAmount,
                DeliveryAddressSnapshot = o.DeliveryAddressSnapshot,
                ItemCount = o.ItemCount,
                LatestShipmentStatus = shipment?.Status,
                TrackingNumber = shipment?.TrackingNumber
            });
        }

        return result;
    }

    public async Task<CustomerOrderDetailDto> GetOrderByIdAsync(
        Guid tenantId,
        Guid customerId,
        Guid orderId,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant ID is required.", nameof(tenantId));
        if (customerId == Guid.Empty) throw new ArgumentException("Customer ID is required.", nameof(customerId));
        if (orderId == Guid.Empty) throw new ArgumentException("Order ID is required.", nameof(orderId));

        var order = await _dbContext.SalesOrders
            .AsNoTracking()
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.TenantId == tenantId && o.CustomerId == customerId && o.Id == orderId && o.IsActive, ct)
            ?? throw new KeyNotFoundException("Order not found or not authorized for this customer context.");

        var productIds = order.Items.Select(i => i.ProductId).Distinct().ToList();
        var products = await _dbContext.Products
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId && productIds.Contains(p.ProductId))
            .ToDictionaryAsync(p => p.ProductId, p => new { p.ProductCode, p.ProductName }, ct);

        var uomIds = order.Items.Select(i => i.UnitOfMeasureId).Distinct().ToList();
        var uoms = await _dbContext.UnitsOfMeasure
            .AsNoTracking()
            .Where(u => u.TenantId == tenantId && uomIds.Contains(u.UnitOfMeasureId))
            .ToDictionaryAsync(u => u.UnitOfMeasureId, u => u.Code, ct);

        var shipments = await _dbContext.SalesShipments
            .AsNoTracking()
            .Where(s => s.TenantId == tenantId && s.SalesOrderId == orderId && s.IsActive)
            .OrderByDescending(s => s.ShipmentDate)
            .Select(s => new CustomerShipmentSummaryDto
            {
                ShipmentId = s.Id,
                ShipmentNumber = s.ShipmentNumber,
                ShipmentDate = s.ShipmentDate,
                DispatchedAt = s.DispatchedAt,
                DeliveredAt = s.DeliveredAt,
                CarrierName = s.CarrierName,
                TrackingNumber = s.TrackingNumber,
                Status = s.Status.ToString(),
                Method = s.Method.ToString()
            })
            .ToListAsync(ct);

        var detail = new CustomerOrderDetailDto
        {
            OrderId = order.Id,
            OrderNumber = order.OrderNumber,
            OrderDate = order.OrderDate,
            DeliveryDate = order.DeliveryDate,
            Status = order.Status.ToString(),
            Currency = order.Currency,
            SubTotal = order.SubTotal,
            DiscountAmount = order.DiscountAmount,
            TaxAmount = order.TaxAmount,
            TotalAmount = order.TotalAmount,
            DeliveryAddressSnapshot = order.DeliveryAddressSnapshot,
            PaymentTerms = order.PaymentTerms,
            Shipments = shipments
        };

        foreach (var item in order.Items.Where(i => i.IsActive))
        {
            products.TryGetValue(item.ProductId, out var prod);
            string? uomCode = null;
            if (item.UnitOfMeasureId.HasValue)
            {
                uoms.TryGetValue(item.UnitOfMeasureId.Value, out uomCode);
            }

            detail.Items.Add(new CustomerOrderItemDto
            {
                ItemId = item.Id,
                ProductId = item.ProductId,
                ProductCode = prod?.ProductCode ?? string.Empty,
                ProductName = prod?.ProductName ?? "Product",
                Quantity = item.Quantity,
                UomCode = uomCode ?? "PCS",
                UnitPrice = item.UnitPrice,
                DiscountRate = item.DiscountRate,
                DiscountAmount = item.DiscountAmount,
                TaxRate = item.TaxRate,
                TaxAmount = item.TaxAmount,
                LineTotal = item.LineTotal
            });
        }

        return detail;
    }

    public async Task<CustomerOrderTrackingDto> GetOrderTrackingAsync(
        Guid tenantId,
        Guid customerId,
        Guid orderId,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant ID is required.", nameof(tenantId));
        if (customerId == Guid.Empty) throw new ArgumentException("Customer ID is required.", nameof(customerId));
        if (orderId == Guid.Empty) throw new ArgumentException("Order ID is required.", nameof(orderId));

        var order = await _dbContext.SalesOrders
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.TenantId == tenantId && o.CustomerId == customerId && o.Id == orderId && o.IsActive, ct)
            ?? throw new KeyNotFoundException("Order not found or not authorized for this customer context.");

        var latestShipment = await _dbContext.SalesShipments
            .AsNoTracking()
            .Where(s => s.TenantId == tenantId && s.SalesOrderId == orderId && s.IsActive)
            .OrderByDescending(s => s.ShipmentDate)
            .FirstOrDefaultAsync(ct);

        var tracking = new CustomerOrderTrackingDto
        {
            OrderId = order.Id,
            OrderNumber = order.OrderNumber,
            CarrierName = latestShipment?.CarrierName,
            TrackingNumber = latestShipment?.TrackingNumber,
            EstimatedDeliveryDate = order.DeliveryDate
        };

        // Determine current stage & status across 8 stages
        bool isCancelled = order.Status == SalesOrderStatus.Cancelled || order.Status == SalesOrderStatus.Rejected;
        bool isFulfilled = order.Status == SalesOrderStatus.Fulfilled || latestShipment?.Status == SalesShipmentStatus.Delivered;
        bool isDispatched = latestShipment != null && (latestShipment.Status == SalesShipmentStatus.Dispatched || latestShipment.DispatchedAt.HasValue);
        bool hasDraftShipment = latestShipment != null && latestShipment.Status == SalesShipmentStatus.Draft;
        bool isConfirmed = order.Status == SalesOrderStatus.Confirmed || isFulfilled || isDispatched || hasDraftShipment;
        bool isSubmitted = order.Status == SalesOrderStatus.Submitted || isConfirmed;

        int activeStageIndex;
        string activeStageName;

        if (isCancelled)
        {
            activeStageIndex = 8;
            activeStageName = "Cancelled";
        }
        else if (isFulfilled)
        {
            activeStageIndex = 7;
            activeStageName = "Delivered";
        }
        else if (isDispatched)
        {
            activeStageIndex = 6;
            activeStageName = "Dispatched";
        }
        else if (hasDraftShipment)
        {
            activeStageIndex = 4; // Picking
            activeStageName = "Picking";
        }
        else if (isConfirmed)
        {
            activeStageIndex = 3;
            activeStageName = "Confirmed";
        }
        else if (isSubmitted)
        {
            activeStageIndex = 2;
            activeStageName = "Under Review";
        }
        else
        {
            activeStageIndex = 1;
            activeStageName = "Created";
        }

        tracking.CurrentStageIndex = activeStageIndex;
        tracking.CurrentStage = activeStageName;

        tracking.Stages = new List<CustomerTrackingStageDto>
        {
            new()
            {
                StageIndex = 1,
                StageName = "Created",
                IsCompleted = true,
                IsCurrent = activeStageIndex == 1,
                Timestamp = order.OrderDate,
                Description = "Order placed and draft recorded."
            },
            new()
            {
                StageIndex = 2,
                StageName = "Under Review",
                IsCompleted = isSubmitted,
                IsCurrent = activeStageIndex == 2,
                Timestamp = isSubmitted ? order.OrderDate : null,
                Description = "Order submitted for commercial and stock review."
            },
            new()
            {
                StageIndex = 3,
                StageName = "Confirmed",
                IsCompleted = isConfirmed,
                IsCurrent = activeStageIndex == 3,
                Timestamp = isConfirmed ? order.OrderDate : null,
                Description = "Order confirmed and approved for fulfilment."
            },
            new()
            {
                StageIndex = 4,
                StageName = "Picking",
                IsCompleted = hasDraftShipment || isDispatched || isFulfilled,
                IsCurrent = activeStageIndex == 4,
                Timestamp = latestShipment?.ShipmentDate,
                Description = "Dispatch note created; inventory allocation and picking in progress."
            },
            new()
            {
                StageIndex = 5,
                StageName = "Packing",
                IsCompleted = isDispatched || isFulfilled,
                IsCurrent = activeStageIndex == 5,
                Timestamp = latestShipment?.ShipmentDate,
                Description = "Items packed and staged for handover."
            },
            new()
            {
                StageIndex = 6,
                StageName = "Dispatched",
                IsCompleted = isDispatched || isFulfilled,
                IsCurrent = activeStageIndex == 6,
                Timestamp = latestShipment?.DispatchedAt,
                Description = latestShipment != null && !string.IsNullOrWhiteSpace(latestShipment.CarrierName)
                    ? $"Dispatched via {latestShipment.CarrierName}. Tracking: {latestShipment.TrackingNumber ?? "N/A"}"
                    : "Order dispatched with carrier."
            },
            new()
            {
                StageIndex = 7,
                StageName = "Delivered",
                IsCompleted = isFulfilled,
                IsCurrent = activeStageIndex == 7,
                Timestamp = latestShipment?.DeliveredAt,
                Description = "Shipment delivered to destination address."
            },
            new()
            {
                StageIndex = 8,
                StageName = "Cancelled",
                IsCompleted = isCancelled,
                IsCurrent = activeStageIndex == 8,
                Timestamp = isCancelled ? order.ModifiedAt ?? order.OrderDate : null,
                Description = isCancelled ? "Order was cancelled or rejected." : "Order is active."
            }
        };

        return tracking;
    }

    public async Task<List<CustomerInvoiceSummaryDto>> GetInvoicesAsync(
        Guid tenantId,
        Guid customerId,
        Guid? sellingCompanyId = null,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant ID is required.", nameof(tenantId));
        if (customerId == Guid.Empty) throw new ArgumentException("Customer ID is required.", nameof(customerId));

        var query = _dbContext.SalesInvoices
            .AsNoTracking()
            .Where(i => i.TenantId == tenantId && i.CustomerId == customerId && i.IsActive);

        if (sellingCompanyId.HasValue && sellingCompanyId.Value != Guid.Empty)
        {
            query = query.Where(i => i.CompanyId == sellingCompanyId.Value);
        }

        var invoices = await query
            .OrderByDescending(i => i.InvoiceDate)
            .Select(i => new CustomerInvoiceSummaryDto
            {
                InvoiceId = i.Id,
                InvoiceNumber = i.InvoiceNumber,
                InvoiceDate = i.InvoiceDate,
                DueDate = i.DueDate,
                SalesOrderId = i.SalesOrderId,
                Status = i.Status.ToString(),
                Currency = i.Currency,
                TotalAmount = i.TotalAmount,
                PaidAmount = i.Status == SalesInvoiceStatus.Paid ? i.TotalAmount : 0m,
                OutstandingBalance = i.Status == SalesInvoiceStatus.Paid ? 0m : i.TotalAmount
            })
            .ToListAsync(ct);

        return invoices;
    }

    public async Task<CustomerDashboardDto> GetDashboardAsync(
        Guid tenantId,
        Guid customerId,
        Guid? sellingCompanyId = null,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant ID is required.", nameof(tenantId));
        if (customerId == Guid.Empty) throw new ArgumentException("Customer ID is required.", nameof(customerId));

        var orderQuery = _dbContext.SalesOrders
            .AsNoTracking()
            .Where(o => o.TenantId == tenantId && o.CustomerId == customerId && o.IsActive);

        if (sellingCompanyId.HasValue && sellingCompanyId.Value != Guid.Empty)
        {
            orderQuery = orderQuery.Where(o => o.CompanyId == sellingCompanyId.Value);
        }

        var totalOrders = await orderQuery.CountAsync(ct);
        var activeOrders = await orderQuery.CountAsync(o =>
            o.Status == SalesOrderStatus.Draft ||
            o.Status == SalesOrderStatus.Submitted ||
            o.Status == SalesOrderStatus.Confirmed, ct);

        var quoteQuery = _dbContext.SalesQuotations
            .AsNoTracking()
            .Where(q => q.TenantId == tenantId && q.CustomerId == customerId && q.IsActive);

        if (sellingCompanyId.HasValue && sellingCompanyId.Value != Guid.Empty)
        {
            quoteQuery = quoteQuery.Where(q => q.CompanyId == sellingCompanyId.Value);
        }

        var pendingQuotes = await quoteQuery.CountAsync(q => q.Status == SalesQuotationStatus.Sent, ct);

        var shipmentQuery = _dbContext.SalesShipments
            .AsNoTracking()
            .Where(s => s.TenantId == tenantId && s.CustomerId == customerId && s.IsActive);

        if (sellingCompanyId.HasValue && sellingCompanyId.Value != Guid.Empty)
        {
            shipmentQuery = shipmentQuery.Where(s => s.CompanyId == sellingCompanyId.Value);
        }

        var dispatchedShipments = await shipmentQuery.CountAsync(s => s.Status == SalesShipmentStatus.Dispatched, ct);

        var recentOrders = await GetOrdersAsync(tenantId, customerId, sellingCompanyId, null, ct);

        var alerts = new List<string>();
        if (pendingQuotes > 0)
        {
            alerts.Add($"You have {pendingQuotes} quotation(s) awaiting your review and acceptance.");
        }
        if (dispatchedShipments > 0)
        {
            alerts.Add($"{dispatchedShipments} shipment(s) currently dispatched and in transit.");
        }

        return new CustomerDashboardDto
        {
            TotalOrdersCount = totalOrders,
            ActiveOrdersCount = activeOrders,
            PendingQuotesCount = pendingQuotes,
            DispatchedShipmentsCount = dispatchedShipments,
            RecentOrders = recentOrders.Take(5).ToList(),
            Alerts = alerts
        };
    }
}
