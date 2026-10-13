using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domain.Features.MasterData.Product;
using Domain.Features.Sales.SalesOrder;
using Domain.Infrastructure.Persistence;
using ERP.Domain.Features.Inventory;
using ERP.Domain.Features.MasterData.Warehouse;
using Microsoft.EntityFrameworkCore;
using SaaS.Application.Interfaces;

namespace Domain.Features.Sales.Shipment;

public class SalesShipmentService : ISalesShipmentService
{
    private readonly ISalesShipmentRepository _shipmentRepository;
    private readonly ISalesOrderRepository _salesOrderRepository;
    private readonly IProductRepository _productRepository;
    private readonly IWarehouseRepository _warehouseRepository;
    private readonly IInventoryPostingService _postingService;
    private readonly DomainDbContext _context;
    private readonly ISubscriptionUsageService? _subscriptionUsageService;

    public SalesShipmentService(
        ISalesShipmentRepository shipmentRepository,
        ISalesOrderRepository salesOrderRepository,
        IProductRepository productRepository,
        IWarehouseRepository warehouseRepository,
        IInventoryPostingService postingService,
        DomainDbContext context,
        ISubscriptionUsageService? subscriptionUsageService = null)
    {
        _shipmentRepository = shipmentRepository;
        _salesOrderRepository = salesOrderRepository;
        _productRepository = productRepository;
        _warehouseRepository = warehouseRepository;
        _postingService = postingService;
        _context = context;
        _subscriptionUsageService = subscriptionUsageService;
    }

    public async Task<List<SalesShipmentDto>> GetShipmentsAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        SalesShipmentStatus? status = null)
    {
        var shipments = await _shipmentRepository.GetByBranchAsync(tenantId, companyId, branchId, status);
        var dtos = new List<SalesShipmentDto>();
        foreach (var s in shipments)
        {
            var order = await _salesOrderRepository.GetByIdAsync(tenantId, companyId, branchId, s.SalesOrderId);
            dtos.Add(MapToDto(s, order?.OrderNumber ?? string.Empty));
        }
        return dtos;
    }

    public async Task<SalesShipmentDto?> GetShipmentByIdAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid shipmentId)
    {
        var shipment = await _shipmentRepository.GetByIdAsync(tenantId, companyId, branchId, shipmentId);
        if (shipment == null) return null;

        var order = await _salesOrderRepository.GetByIdAsync(tenantId, companyId, branchId, shipment.SalesOrderId);
        return MapToDto(shipment, order?.OrderNumber ?? string.Empty);
    }

    public async Task<SalesShipmentDto> CreateFromOrderAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid salesOrderId,
        CreateShipmentFromOrderRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        if (string.IsNullOrWhiteSpace(request.ShipmentNumber))
            throw new ArgumentException("Shipment number is required.", nameof(request.ShipmentNumber));

        string normShipmentNum = request.ShipmentNumber.Trim();
        if (await _shipmentRepository.ExistsShipmentNumberAsync(tenantId, companyId, normShipmentNum))
            throw new InvalidOperationException($"Shipment number '{normShipmentNum}' already exists for this company.");

        var order = await _salesOrderRepository.GetByIdAsync(tenantId, companyId, branchId, salesOrderId)
            ?? throw new KeyNotFoundException("Sales Order not found in authorized branch/company scope.");

        if (order.Status != SalesOrderStatus.Confirmed)
        {
            throw new InvalidOperationException(
                $"Cannot create shipment for Sales Order '{order.OrderNumber}' in status '{order.Status}'. Only Confirmed orders can be shipped.");
        }

        var activeOrderItems = order.Items.Where(i => i.IsActive).ToList();
        if (activeOrderItems.Count == 0)
        {
            throw new InvalidOperationException("Sales Order has no active line items.");
        }

        // Distinguish physical goods from services using the actual product model
        var physicalOrderItems = new List<(SalesOrderItem Item, Product Product)>();
        foreach (var item in activeOrderItems)
        {
            var product = await _productRepository.GetByIdAsync(tenantId, item.ProductId)
                ?? throw new InvalidOperationException($"Product '{item.ProductId}' not found or does not belong to tenant.");

            if (product.ProductType != ProductType.Service)
            {
                physicalOrderItems.Add((item, product));
            }
        }

        if (physicalOrderItems.Count == 0)
        {
            throw new InvalidOperationException(
                "Sales Order contains only service lines and does not require physical shipment.");
        }

        // Calculate already dispatched quantities across all completed shipments
        var existingShipments = await _shipmentRepository.GetBySalesOrderIdAsync(tenantId, salesOrderId);
        var dispatchedQtyByLine = existingShipments
            .Where(s => s.Status == SalesShipmentStatus.Dispatched || s.Status == SalesShipmentStatus.Delivered)
            .SelectMany(s => s.Items)
            .Where(i => i.IsActive)
            .GroupBy(i => i.SalesOrderItemId)
            .ToDictionary(g => g.Key, g => g.Sum(i => i.QuantityDispatched));

        // Check unfulfilled physical items
        var unfulfilledLines = new List<(SalesOrderItem Item, Product Product, decimal RemainingQty)>();
        foreach (var (item, product) in physicalOrderItems)
        {
            dispatchedQtyByLine.TryGetValue(item.Id, out decimal alreadyDispatched);
            decimal remaining = item.Quantity - alreadyDispatched;
            if (remaining > 0)
            {
                unfulfilledLines.Add((item, product, remaining));
            }
        }

        if (unfulfilledLines.Count == 0)
        {
            throw new InvalidOperationException(
                "All physical goods on this Sales Order have already been fully fulfilled.");
        }

        // Resolve default warehouse & location
        Guid defaultWarehouseId;
        Guid defaultLocationId;

        if (request.DefaultWarehouseId.HasValue && request.DefaultLocationId.HasValue)
        {
            await ValidateWarehouseAndLocationAsync(tenantId, request.DefaultWarehouseId.Value, request.DefaultLocationId.Value);
            defaultWarehouseId = request.DefaultWarehouseId.Value;
            defaultLocationId = request.DefaultLocationId.Value;
        }
        else
        {
            var warehouses = await _warehouseRepository.GetWarehousesAsync(tenantId);
            var wh = warehouses.FirstOrDefault(w => w.IsActive)
                ?? throw new InvalidOperationException("No active warehouse found for tenant. Please provide warehouse and location.");

            var locations = await _warehouseRepository.GetLocationsAsync(tenantId, wh.WarehouseId);
            var loc = locations.FirstOrDefault(l => l.IsActive)
                ?? throw new InvalidOperationException($"No active location found in warehouse '{wh.WarehouseCode}'.");

            defaultWarehouseId = wh.WarehouseId;
            defaultLocationId = loc.WarehouseLocationId;
        }

        var shipment = SalesShipment.Create(
            tenantId,
            companyId,
            branchId,
            order.Id,
            order.CustomerId,
            normShipmentNum,
            request.Method,
            defaultWarehouseId,
            request.ShipmentDate,
            request.CarrierName,
            request.TrackingNumber,
            request.RecipientName,
            request.DeliveryAddress,
            request.VehicleNumber,
            request.Notes);

        if (request.Items != null && request.Items.Count > 0)
        {
            // Explicit line overrides
            foreach (var reqItem in request.Items)
            {
                var targetLine = unfulfilledLines.FirstOrDefault(l => l.Item.Id == reqItem.SalesOrderItemId);
                if (targetLine == default)
                {
                    throw new InvalidOperationException(
                        $"Order item '{reqItem.SalesOrderItemId}' is not an unfulfilled physical goods item on this Sales Order.");
                }

                if (reqItem.QuantityDispatched <= 0)
                    throw new ArgumentException("Quantity dispatched must be greater than zero.");

                if (reqItem.QuantityDispatched > targetLine.RemainingQty)
                {
                    throw new InvalidOperationException(
                        $"Attempted quantity {reqItem.QuantityDispatched} exceeds remaining unfulfilled quantity {targetLine.RemainingQty} for product '{targetLine.Product.ProductCode}'.");
                }

                await ValidateWarehouseAndLocationAsync(tenantId, reqItem.WarehouseId, reqItem.WarehouseLocationId);

                var item = SalesShipmentItem.Create(
                    shipment.Id,
                    targetLine.Item.Id,
                    targetLine.Product.ProductId,
                    reqItem.WarehouseId,
                    reqItem.WarehouseLocationId,
                    reqItem.QuantityDispatched,
                    reqItem.UnitOfMeasureId ?? targetLine.Item.UnitOfMeasureId,
                    reqItem.BatchNumber,
                    reqItem.SerialNumber,
                    reqItem.Notes);

                shipment.AddItem(item);
            }
        }
        else
        {
            // Default to full remaining quantities
            foreach (var (orderItem, product, remaining) in unfulfilledLines)
            {
                var item = SalesShipmentItem.Create(
                    shipment.Id,
                    orderItem.Id,
                    product.ProductId,
                    defaultWarehouseId,
                    defaultLocationId,
                    remaining,
                    orderItem.UnitOfMeasureId,
                    notes: null);

                shipment.AddItem(item);
            }
        }

        await _shipmentRepository.AddAsync(shipment);
        await _shipmentRepository.SaveChangesAsync();

        return MapToDto(shipment, order.OrderNumber);
    }

    public async Task<SalesShipmentDto> CreateManualDraftAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        CreateSalesShipmentRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        if (string.IsNullOrWhiteSpace(request.ShipmentNumber))
            throw new ArgumentException("Shipment number is required.", nameof(request.ShipmentNumber));

        string normShipmentNum = request.ShipmentNumber.Trim();
        if (await _shipmentRepository.ExistsShipmentNumberAsync(tenantId, companyId, normShipmentNum))
            throw new InvalidOperationException($"Shipment number '{normShipmentNum}' already exists for this company.");

        var order = await _salesOrderRepository.GetByIdAsync(tenantId, companyId, branchId, request.SalesOrderId)
            ?? throw new KeyNotFoundException("Sales Order not found in authorized branch/company scope.");

        if (order.Status != SalesOrderStatus.Confirmed)
        {
            throw new InvalidOperationException(
                $"Cannot create shipment for Sales Order in status '{order.Status}'. Only Confirmed orders can be shipped.");
        }

        if (request.Items == null || request.Items.Count == 0)
        {
            throw new InvalidOperationException("Shipment must contain at least one line item.");
        }

        var shipment = SalesShipment.Create(
            tenantId,
            companyId,
            branchId,
            order.Id,
            order.CustomerId,
            normShipmentNum,
            request.Method,
            request.WarehouseId,
            request.ShipmentDate,
            request.CarrierName,
            request.TrackingNumber,
            request.RecipientName,
            request.DeliveryAddress,
            request.VehicleNumber,
            request.Notes);

        foreach (var reqItem in request.Items)
        {
            var orderItem = order.Items.FirstOrDefault(i => i.Id == reqItem.SalesOrderItemId && i.IsActive)
                ?? throw new InvalidOperationException($"Order item '{reqItem.SalesOrderItemId}' does not belong to this Sales Order.");

            var product = await _productRepository.GetByIdAsync(tenantId, reqItem.ProductId)
                ?? throw new InvalidOperationException($"Product '{reqItem.ProductId}' not found.");

            if (product.ProductType == ProductType.Service)
            {
                throw new InvalidOperationException($"Product '{product.ProductCode}' is a service and cannot be physically shipped.");
            }

            await ValidateWarehouseAndLocationAsync(tenantId, reqItem.WarehouseId, reqItem.WarehouseLocationId);

            var item = SalesShipmentItem.Create(
                shipment.Id,
                orderItem.Id,
                product.ProductId,
                reqItem.WarehouseId,
                reqItem.WarehouseLocationId,
                reqItem.QuantityDispatched,
                reqItem.UnitOfMeasureId ?? orderItem.UnitOfMeasureId,
                reqItem.BatchNumber,
                reqItem.SerialNumber,
                reqItem.Notes);

            shipment.AddItem(item);
        }

        await _shipmentRepository.AddAsync(shipment);
        await _shipmentRepository.SaveChangesAsync();

        return MapToDto(shipment, order.OrderNumber);
    }

    public async Task<SalesShipmentDto> UpdateDraftAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid shipmentId,
        UpdateSalesShipmentRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        var shipment = await _shipmentRepository.GetByIdAsync(tenantId, companyId, branchId, shipmentId)
            ?? throw new KeyNotFoundException("Shipment not found in authorized branch/company scope.");

        if (shipment.Status != SalesShipmentStatus.Draft)
        {
            throw new InvalidOperationException($"Only Draft shipments can be modified. Current status: '{shipment.Status}'.");
        }

        var order = await _salesOrderRepository.GetByIdAsync(tenantId, companyId, branchId, shipment.SalesOrderId)
            ?? throw new KeyNotFoundException("Associated Sales Order not found.");

        if (request.Method.HasValue) shipment.Method = request.Method.Value;
        if (request.ShipmentDate.HasValue) shipment.ShipmentDate = request.ShipmentDate.Value;
        if (request.CarrierName != null) shipment.CarrierName = request.CarrierName.Trim();
        if (request.TrackingNumber != null) shipment.TrackingNumber = request.TrackingNumber.Trim();
        if (request.RecipientName != null) shipment.RecipientName = request.RecipientName.Trim();
        if (request.DeliveryAddress != null) shipment.DeliveryAddress = request.DeliveryAddress.Trim();
        if (request.VehicleNumber != null) shipment.VehicleNumber = request.VehicleNumber.Trim();
        if (request.Notes != null) shipment.Notes = request.Notes.Trim();

        if (request.Items != null && request.Items.Count > 0)
        {
            var existingItems = shipment.Items.ToList();
            shipment.Items.Clear();
            _context.SalesShipmentItems.RemoveRange(existingItems);

            foreach (var reqItem in request.Items)
            {
                var orderItem = order.Items.FirstOrDefault(i => i.Id == reqItem.SalesOrderItemId && i.IsActive)
                    ?? throw new InvalidOperationException($"Order item '{reqItem.SalesOrderItemId}' does not belong to this Sales Order.");

                var product = await _productRepository.GetByIdAsync(tenantId, reqItem.ProductId)
                    ?? throw new InvalidOperationException($"Product '{reqItem.ProductId}' not found.");

                if (product.ProductType == ProductType.Service)
                {
                    throw new InvalidOperationException($"Product '{product.ProductCode}' is a service and cannot be physically shipped.");
                }

                await ValidateWarehouseAndLocationAsync(tenantId, reqItem.WarehouseId, reqItem.WarehouseLocationId);

                var item = SalesShipmentItem.Create(
                    shipment.Id,
                    orderItem.Id,
                    product.ProductId,
                    reqItem.WarehouseId,
                    reqItem.WarehouseLocationId,
                    reqItem.QuantityDispatched,
                    reqItem.UnitOfMeasureId ?? orderItem.UnitOfMeasureId,
                    reqItem.BatchNumber,
                    reqItem.SerialNumber,
                    reqItem.Notes);

                shipment.Items.Add(item);
                _context.SalesShipmentItems.Add(item);
            }
        }

        await _shipmentRepository.SaveChangesAsync();
        return MapToDto(shipment, order.OrderNumber);
    }

    public async Task<SalesShipmentDto> CancelDraftAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid shipmentId)
    {
        var shipment = await _shipmentRepository.GetByIdAsync(tenantId, companyId, branchId, shipmentId)
            ?? throw new KeyNotFoundException("Shipment not found in authorized branch/company scope.");

        shipment.Cancel();
        await _shipmentRepository.SaveChangesAsync();

        var order = await _salesOrderRepository.GetByIdAsync(tenantId, companyId, branchId, shipment.SalesOrderId);
        return MapToDto(shipment, order?.OrderNumber ?? string.Empty);
    }

    public async Task<SalesShipmentDto> DispatchShipmentAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid shipmentId)
    {
        return await ExecuteDispatchCoreAsync(tenantId, companyId, branchId, shipmentId, targetStatus: SalesShipmentStatus.Dispatched);
    }

    public async Task<SalesShipmentDto> DeliverShipmentAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid shipmentId,
        DeliverShipmentRequest? request = null)
    {
        var existingShipment = await _shipmentRepository.GetByIdAsync(tenantId, companyId, branchId, shipmentId)
            ?? throw new KeyNotFoundException("Shipment not found in authorized branch/company scope.");

        if (existingShipment.Status == SalesShipmentStatus.Delivered)
        {
            var order = await _salesOrderRepository.GetByIdAsync(tenantId, companyId, branchId, existingShipment.SalesOrderId);
            return MapToDto(existingShipment, order?.OrderNumber ?? string.Empty);
        }

        if (existingShipment.Status == SalesShipmentStatus.Draft)
        {
            // Direct delivery from draft (e.g. StoreHandover or CustomerPickup)
            return await ExecuteDispatchCoreAsync(
                tenantId,
                companyId,
                branchId,
                shipmentId,
                targetStatus: SalesShipmentStatus.Delivered,
                deliveredAt: request?.DeliveredAt,
                recipientName: request?.RecipientName);
        }

        if (existingShipment.Status != SalesShipmentStatus.Dispatched)
        {
            throw new InvalidOperationException($"Cannot deliver shipment in status '{existingShipment.Status}'.");
        }

        existingShipment.MarkDelivered(request?.DeliveredAt, request?.RecipientName);
        if (!string.IsNullOrWhiteSpace(request?.Notes))
        {
            existingShipment.Notes = string.IsNullOrWhiteSpace(existingShipment.Notes)
                ? request.Notes.Trim()
                : $"{existingShipment.Notes}; {request.Notes.Trim()}";
        }

        await _shipmentRepository.SaveChangesAsync();

        var ord = await _salesOrderRepository.GetByIdAsync(tenantId, companyId, branchId, existingShipment.SalesOrderId);
        return MapToDto(existingShipment, ord?.OrderNumber ?? string.Empty);
    }

    private async Task<SalesShipmentDto> ExecuteDispatchCoreAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid shipmentId,
        SalesShipmentStatus targetStatus,
        DateTime? deliveredAt = null,
        string? recipientName = null)
    {
        // 1. Check idempotency early
        var existingShipment = await _shipmentRepository.GetByIdAsync(tenantId, companyId, branchId, shipmentId)
            ?? throw new KeyNotFoundException("Shipment not found in authorized branch/company scope.");

        if (existingShipment.Status == SalesShipmentStatus.Dispatched || existingShipment.Status == SalesShipmentStatus.Delivered)
        {
            if (targetStatus == SalesShipmentStatus.Delivered && existingShipment.Status == SalesShipmentStatus.Dispatched)
            {
                existingShipment.MarkDelivered(deliveredAt, recipientName);
                await _shipmentRepository.SaveChangesAsync();
            }

            var ord = await _salesOrderRepository.GetByIdAsync(tenantId, companyId, branchId, existingShipment.SalesOrderId);
            return MapToDto(existingShipment, ord?.OrderNumber ?? string.Empty);
        }

        if (existingShipment.Status == SalesShipmentStatus.Cancelled)
        {
            throw new InvalidOperationException("Cancelled shipment cannot be dispatched or delivered.");
        }

        // 2. Wrap dispatch execution in transactional boundary
        Func<Task<SalesShipmentDto>> executionAction = async () =>
        {
            await using var domainTx = await _context.Database.BeginTransactionAsync();

            // Acquire row-level update lock on the parent Sales Order to serialize concurrent dispatches against the same order
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT 1 FROM SalesOrders WITH (UPDLOCK, ROWLOCK) WHERE Id = {existingShipment.SalesOrderId} AND TenantId = {tenantId}");

            var shipment = await _shipmentRepository.GetByIdAsync(tenantId, companyId, branchId, shipmentId)
                ?? throw new KeyNotFoundException("Shipment not found.");

            if (shipment.Status == SalesShipmentStatus.Dispatched || shipment.Status == SalesShipmentStatus.Delivered)
            {
                var ord = await _salesOrderRepository.GetByIdAsync(tenantId, companyId, branchId, shipment.SalesOrderId);
                return MapToDto(shipment, ord?.OrderNumber ?? string.Empty);
            }

            var order = await _salesOrderRepository.GetByIdAsync(tenantId, companyId, branchId, shipment.SalesOrderId)
                ?? throw new KeyNotFoundException("Associated Sales Order not found.");

            if (order.Status != SalesOrderStatus.Confirmed && order.Status != SalesOrderStatus.Fulfilled)
            {
                throw new InvalidOperationException($"Cannot dispatch against Sales Order in status '{order.Status}'.");
            }

            var activeShipmentItems = shipment.Items.Where(i => i.IsActive).ToList();
            if (activeShipmentItems.Count == 0)
            {
                throw new InvalidOperationException("Cannot dispatch shipment with no active line items.");
            }

            // 3. Concurrency check: authoritatively calculate remaining unfulfilled quantities across committed shipments
            var otherShipments = await _shipmentRepository.GetBySalesOrderIdAsync(tenantId, shipment.SalesOrderId);
            var committedDispatchedQty = otherShipments
                .Where(s => s.Id != shipment.Id && (s.Status == SalesShipmentStatus.Dispatched || s.Status == SalesShipmentStatus.Delivered))
                .SelectMany(s => s.Items)
                .Where(i => i.IsActive)
                .GroupBy(i => i.SalesOrderItemId)
                .ToDictionary(g => g.Key, g => g.Sum(i => i.QuantityDispatched));

            // Validate zero over-shipment tolerance
            foreach (var item in activeShipmentItems)
            {
                var orderItem = order.Items.FirstOrDefault(i => i.Id == item.SalesOrderItemId && i.IsActive)
                    ?? throw new InvalidOperationException($"Order line '{item.SalesOrderItemId}' not found on order.");

                committedDispatchedQty.TryGetValue(item.SalesOrderItemId, out decimal alreadyDispatched);
                decimal maxAllowable = orderItem.Quantity - alreadyDispatched;

                if (item.QuantityDispatched > maxAllowable)
                {
                    throw new InvalidOperationException(
                        $"Over-shipment rejected for product '{item.ProductId}'. Ordered: {orderItem.Quantity}, Already Dispatched: {alreadyDispatched}, Attempted in this shipment: {item.QuantityDispatched}. Over-shipment tolerance is 0%.");
                }
            }

            // 4. Mark status
            if (targetStatus == SalesShipmentStatus.Delivered)
            {
                shipment.MarkDelivered(deliveredAt, recipientName);
            }
            else
            {
                shipment.MarkDispatched();
            }

            // 5. Post Inventory ledger movement for stock-tracked products
            int sequence = 1;
            foreach (var item in activeShipmentItems)
            {
                var product = await _productRepository.GetByIdAsync(tenantId, item.ProductId)
                    ?? throw new InvalidOperationException($"Product '{item.ProductId}' not found.");

                if (product.IsStockTracked)
                {
                    var postReq = new PostInventoryMovementRequest
                    {
                        MovementType = InventoryMovementType.ShipmentDispatch,
                        SourceDocumentType = SourceDocumentType.SalesShipment,
                        SourceDocumentId = shipment.Id,
                        SourceDocumentLineId = item.Id,
                        MovementSequence = sequence++,
                        ProductId = item.ProductId,
                        WarehouseId = item.WarehouseId,
                        WarehouseLocationId = item.WarehouseLocationId,
                        Quantity = item.QuantityDispatched,
                        UnitOfMeasureId = item.UnitOfMeasureId,
                        BatchNumber = item.BatchNumber,
                        SerialNumber = item.SerialNumber,
                        TransactionDate = shipment.DispatchedAt ?? DateTime.UtcNow
                    };

                    await _postingService.PostMovementAsync(tenantId, postReq);
                }
            }

            // 6. Recalculate Sales Order fulfillment status across all lines
            // Physical lines: fulfilled when total dispatched >= ordered quantity
            // Service lines: not subject to physical dispatch (pending separate completion)
            bool allPhysicalFulfilled = true;
            bool hasServiceLines = false;
            foreach (var orderItem in order.Items.Where(i => i.IsActive))
            {
                var prod = await _productRepository.GetByIdAsync(tenantId, orderItem.ProductId);
                if (prod != null)
                {
                    if (prod.ProductType == ProductType.Service)
                    {
                        hasServiceLines = true;
                    }
                    else
                    {
                        committedDispatchedQty.TryGetValue(orderItem.Id, out decimal priorDispatched);
                        decimal currentDispatched = activeShipmentItems
                            .Where(i => i.SalesOrderItemId == orderItem.Id)
                            .Sum(i => i.QuantityDispatched);

                        if (priorDispatched + currentDispatched < orderItem.Quantity)
                        {
                            allPhysicalFulfilled = false;
                        }
                    }
                }
            }

            // Order is only marked Fulfilled if all physical lines are fulfilled AND there are no unfulfilled service lines pending
            if (allPhysicalFulfilled && !hasServiceLines)
            {
                order.Status = SalesOrderStatus.Fulfilled;
            }

            await _context.SaveChangesAsync();
            await domainTx.CommitAsync();

            return MapToDto(shipment, order.OrderNumber);
        };

        if (_subscriptionUsageService != null)
        {
            return await _subscriptionUsageService.ExecuteWithUsageLimitAsync(
                tenantId,
                "TRANSACTIONS",
                1,
                executionAction);
        }

        return await executionAction();
    }

    private async Task ValidateWarehouseAndLocationAsync(Guid tenantId, Guid warehouseId, Guid locationId)
    {
        var warehouse = await _warehouseRepository.GetWarehouseByIdAsync(tenantId, warehouseId)
            ?? throw new KeyNotFoundException("Warehouse not found or does not belong to the authorized tenant.");

        if (!warehouse.IsActive)
            throw new InvalidOperationException($"Warehouse '{warehouse.WarehouseCode}' is inactive.");

        var location = await _warehouseRepository.GetLocationByIdAsync(tenantId, locationId)
            ?? throw new KeyNotFoundException("Warehouse location not found or does not belong to the authorized tenant.");

        if (!location.IsActive)
            throw new InvalidOperationException($"Location '{location.LocationCode}' is inactive.");

        if (location.WarehouseId != warehouseId)
            throw new InvalidOperationException("Location does not belong to the specified warehouse.");
    }

    private static SalesShipmentDto MapToDto(SalesShipment shipment, string orderNumber)
    {
        return new SalesShipmentDto
        {
            Id = shipment.Id,
            TenantId = shipment.TenantId,
            CompanyId = shipment.CompanyId,
            BranchId = shipment.BranchId,
            SalesOrderId = shipment.SalesOrderId,
            OrderNumber = orderNumber,
            CustomerId = shipment.CustomerId,
            WarehouseId = shipment.WarehouseId,
            ShipmentNumber = shipment.ShipmentNumber,
            Status = shipment.Status,
            Method = shipment.Method,
            ShipmentDate = shipment.ShipmentDate,
            DispatchedAt = shipment.DispatchedAt,
            DeliveredAt = shipment.DeliveredAt,
            CarrierName = shipment.CarrierName,
            TrackingNumber = shipment.TrackingNumber,
            RecipientName = shipment.RecipientName,
            DeliveryAddress = shipment.DeliveryAddress,
            VehicleNumber = shipment.VehicleNumber,
            Notes = shipment.Notes,
            Items = shipment.Items.Where(i => i.IsActive).Select(i => new SalesShipmentItemDto
            {
                Id = i.Id,
                SalesShipmentId = i.SalesShipmentId,
                SalesOrderItemId = i.SalesOrderItemId,
                ProductId = i.ProductId,
                WarehouseId = i.WarehouseId,
                WarehouseLocationId = i.WarehouseLocationId,
                UnitOfMeasureId = i.UnitOfMeasureId,
                QuantityDispatched = i.QuantityDispatched,
                BatchNumber = i.BatchNumber,
                SerialNumber = i.SerialNumber,
                Notes = i.Notes
            }).ToList()
        };
    }
}
