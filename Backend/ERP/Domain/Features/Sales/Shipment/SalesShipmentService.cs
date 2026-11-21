using Microsoft.Extensions.DependencyInjection;
using Domain.Features.Sales.Invoice;
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


namespace Domain.Features.Sales.Shipment;

public class SalesShipmentService : ISalesShipmentService
{
    private readonly ISalesShipmentRepository _shipmentRepository;
    private readonly ISalesOrderRepository _salesOrderRepository;
    private readonly IProductRepository _productRepository;
    private readonly IWarehouseRepository _warehouseRepository;
    private readonly IInventoryPostingService _postingService;
    private readonly IInventoryRepository _inventoryRepository;
    private readonly DomainDbContext _context;
    private readonly IServiceProvider? _serviceProvider;
    private readonly Domain.Features.MasterData.UnitOfMeasure.IUnitOfMeasureService? _uomService;

    public SalesShipmentService(
        ISalesShipmentRepository shipmentRepository,
        ISalesOrderRepository salesOrderRepository,
        IProductRepository productRepository,
        IWarehouseRepository warehouseRepository,
        IInventoryPostingService postingService,
        DomainDbContext context,
        IInventoryRepository? inventoryRepository = null,
        IServiceProvider? serviceProvider = null,
        Domain.Features.MasterData.UnitOfMeasure.IUnitOfMeasureService? uomService = null)
    {
        _shipmentRepository = shipmentRepository;
        _salesOrderRepository = salesOrderRepository;
        _productRepository = productRepository;
        _warehouseRepository = warehouseRepository;
        _postingService = postingService;
        _context = context;
        _inventoryRepository = inventoryRepository ?? new InventoryRepository(context);
        _serviceProvider = serviceProvider;
        _uomService = uomService;
    }

    public async Task<List<SalesShipmentDto>> GetShipmentsAsync(
        Guid tenantId,
        Guid companyId,
        Guid? branchId = null,
        SalesShipmentStatus? status = null)
    {
        List<SalesShipment> shipments;
        if (branchId.HasValue && branchId.Value != Guid.Empty)
        {
            shipments = await _shipmentRepository.GetByBranchAsync(tenantId, companyId, branchId.Value, status);
        }
        else
        {
            shipments = await _shipmentRepository.GetByCompanyAsync(tenantId, companyId, status);
        }

        var dtos = new List<SalesShipmentDto>();
        foreach (var s in shipments)
        {
            var order = await _salesOrderRepository.GetByIdAsync(tenantId, companyId, s.BranchId, s.SalesOrderId);
            dtos.Add(MapToDto(s, order?.OrderNumber ?? string.Empty));
        }
        return dtos;
    }

    public async Task<SalesShipmentDto?> GetShipmentByIdAsync(
        Guid tenantId,
        Guid companyId,
        Guid? branchId,
        Guid shipmentId)
    {
        SalesShipment? shipment;
        if (branchId.HasValue && branchId.Value != Guid.Empty)
        {
            shipment = await _shipmentRepository.GetByIdAsync(tenantId, companyId, branchId.Value, shipmentId);
        }
        else
        {
            shipment = await _shipmentRepository.GetByIdAsync(tenantId, shipmentId);
            if (shipment != null && shipment.CompanyId != companyId) shipment = null;
        }

        if (shipment == null) return null;

        var order = await _salesOrderRepository.GetByIdAsync(tenantId, companyId, shipment.BranchId, shipment.SalesOrderId);
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
            var locTypes = (await _warehouseRepository.GetLocationTypesAsync(tenantId)).ToDictionary(t => t.LocationTypeId);
            var loc = locations.FirstOrDefault(l => l.IsActive && (!l.LocationTypeId.HasValue || (locTypes.TryGetValue(l.LocationTypeId.Value, out var lt) && lt.CanPick)))
                ?? throw new InvalidOperationException($"No active pickable location found in warehouse '{wh.WarehouseCode}'.");

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
            !string.IsNullOrWhiteSpace(request.DeliveryAddress) ? request.DeliveryAddress.Trim() : order.DeliveryAddressSnapshot,
            request.VehicleNumber,
            request.Notes);

        if (request.Items != null && request.Items.Count > 0)
        {
            var groupedReqItems = request.Items.GroupBy(i => i.SalesOrderItemId).ToList();
            foreach (var grp in groupedReqItems)
            {
                var targetLine = unfulfilledLines.FirstOrDefault(l => l.Item.Id == grp.Key);
                if (targetLine == default)
                {
                    throw new InvalidOperationException(
                        $"Order item '{grp.Key}' is not an unfulfilled physical goods item on this Sales Order.");
                }

                decimal totalDispatched = grp.Sum(i => i.QuantityDispatched);
                if (totalDispatched > targetLine.RemainingQty)
                {
                    throw new InvalidOperationException(
                        $"Attempted total quantity {totalDispatched} exceeds remaining unfulfilled quantity {targetLine.RemainingQty} for product '{targetLine.Product.ProductCode}'.");
                }
            }

            foreach (var reqItem in request.Items)
            {
                var targetLine = unfulfilledLines.First(l => l.Item.Id == reqItem.SalesOrderItemId);

                if (reqItem.QuantityDispatched <= 0)
                    throw new ArgumentException("Quantity dispatched must be greater than zero.");

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
        try
        {
            await _shipmentRepository.SaveChangesAsync();
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException ex)
        {
            var entry = ex.Entries.FirstOrDefault();
            var entityName = entry?.Entity.GetType().Name ?? "Unknown";
            var state = entry?.State.ToString() ?? "Unknown";
            throw new InvalidOperationException($"Concurrency error on {entityName} (State: {state}): {ex.Message}", ex);
        }

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
            !string.IsNullOrWhiteSpace(request.DeliveryAddress) ? request.DeliveryAddress.Trim() : order.DeliveryAddressSnapshot,
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
        try
        {
            await _shipmentRepository.SaveChangesAsync();
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException ex)
        {
            var entry = ex.Entries.FirstOrDefault();
            var entityName = entry?.Entity.GetType().Name ?? "Unknown";
            var state = entry?.State.ToString() ?? "Unknown";
            throw new InvalidOperationException($"Concurrency error on {entityName} (State: {state}): {ex.Message}", ex);
        }

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
            var otherShipments = await _shipmentRepository.GetBySalesOrderIdAsync(tenantId, shipment.SalesOrderId);
            var committedDispatchedQty = otherShipments
                .Where(s => s.Id != shipment.Id && (s.Status == SalesShipmentStatus.Dispatched || s.Status == SalesShipmentStatus.Delivered))
                .SelectMany(s => s.Items)
                .Where(i => i.IsActive)
                .GroupBy(i => i.SalesOrderItemId)
                .ToDictionary(g => g.Key, g => g.Sum(i => i.QuantityDispatched));

            var groupedReqItems = request.Items.GroupBy(i => i.SalesOrderItemId).ToList();
            foreach (var grp in groupedReqItems)
            {
                var orderItem = order.Items.FirstOrDefault(i => i.Id == grp.Key && i.IsActive)
                    ?? throw new InvalidOperationException($"Order item '{grp.Key}' does not belong to this Sales Order.");

                committedDispatchedQty.TryGetValue(grp.Key, out decimal alreadyDispatched);
                decimal maxAllowable = orderItem.Quantity - alreadyDispatched;
                decimal totalRequestedInDraft = grp.Sum(i => i.QuantityDispatched);

                if (totalRequestedInDraft > maxAllowable)
                {
                    throw new InvalidOperationException(
                        $"Over-shipment rejected for product '{orderItem.ProductId}'. Ordered: {orderItem.Quantity}, Already Dispatched: {alreadyDispatched}, Attempted across draft allocations: {totalRequestedInDraft}. Over-shipment tolerance is 0%.");
                }
            }

            var existingItems = shipment.Items.ToList();
            int reqCount = request.Items.Count;
            int existingCount = existingItems.Count;

            for (int i = 0; i < reqCount; i++)
            {
                var reqItem = request.Items[i];
                if (reqItem.QuantityDispatched <= 0)
                {
                    throw new ArgumentException("Quantity dispatched must be greater than zero.");
                }

                var orderItem = order.Items.First(item => item.Id == reqItem.SalesOrderItemId && item.IsActive);
                var product = await _productRepository.GetByIdAsync(tenantId, reqItem.ProductId)
                    ?? throw new InvalidOperationException($"Product '{reqItem.ProductId}' not found.");

                if (product.ProductType == ProductType.Service)
                {
                    throw new InvalidOperationException($"Product '{product.ProductCode}' is a service and cannot be physically shipped.");
                }

                await ValidateWarehouseAndLocationAsync(tenantId, reqItem.WarehouseId, reqItem.WarehouseLocationId);

                decimal roundedQty = Math.Round(reqItem.QuantityDispatched, 4, MidpointRounding.AwayFromZero);

                if (i < existingCount)
                {
                    // Update existing item in place preserving key and concurrency token
                    var existing = existingItems[i];
                    existing.SalesOrderItemId = orderItem.Id;
                    existing.ProductId = product.ProductId;
                    existing.WarehouseId = reqItem.WarehouseId;
                    existing.WarehouseLocationId = reqItem.WarehouseLocationId;
                    existing.QuantityDispatched = roundedQty;
                    existing.UnitOfMeasureId = reqItem.UnitOfMeasureId ?? orderItem.UnitOfMeasureId;
                    existing.BatchNumber = reqItem.BatchNumber?.Trim();
                    existing.SerialNumber = reqItem.SerialNumber?.Trim();
                    existing.Notes = reqItem.Notes?.Trim();
                    existing.IsActive = true;
                }
                else
                {
                    // Add new allocation split row
                    var newItem = SalesShipmentItem.Create(
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

                    shipment.Items.Add(newItem);
                    _context.SalesShipmentItems.Add(newItem);
                }
            }

            // Remove any excess items if fewer rows requested
            if (existingCount > reqCount)
            {
                var excess = existingItems.Skip(reqCount).ToList();
                foreach (var ex in excess)
                {
                    shipment.Items.Remove(ex);
                }
                _context.SalesShipmentItems.RemoveRange(excess);
            }
        }

        try
        {
            await _shipmentRepository.SaveChangesAsync();
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException ex)
        {
            var entry = ex.Entries.FirstOrDefault();
            var entityName = entry?.Entity.GetType().Name ?? "Unknown";
            var state = entry?.State.ToString() ?? "Unknown";
            throw new InvalidOperationException($"Concurrency error on {entityName} (State: {state}): {ex.Message}", ex);
        }
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
        try
        {
            await _shipmentRepository.SaveChangesAsync();
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException ex)
        {
            var entry = ex.Entries.FirstOrDefault();
            var entityName = entry?.Entity.GetType().Name ?? "Unknown";
            var state = entry?.State.ToString() ?? "Unknown";
            throw new InvalidOperationException($"Concurrency error on {entityName} (State: {state}): {ex.Message}", ex);
        }

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
            await EnsureInvoiceCreatedAsync(tenantId, companyId, branchId, existingShipment);
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

        try
        {
            await _shipmentRepository.SaveChangesAsync();
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException ex)
        {
            var entry = ex.Entries.FirstOrDefault();
            var entityName = entry?.Entity.GetType().Name ?? "Unknown";
            var state = entry?.State.ToString() ?? "Unknown";
            throw new InvalidOperationException($"Concurrency error on {entityName} (State: {state}): {ex.Message}", ex);
        }
        await EnsureInvoiceCreatedAsync(tenantId, companyId, branchId, existingShipment);

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
                try
        {
            await _shipmentRepository.SaveChangesAsync();
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException ex)
        {
            var entry = ex.Entries.FirstOrDefault();
            var entityName = entry?.Entity.GetType().Name ?? "Unknown";
            var state = entry?.State.ToString() ?? "Unknown";
            throw new InvalidOperationException($"Concurrency error on {entityName} (State: {state}): {ex.Message}", ex);
        }
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

            // Validate zero over-shipment tolerance aggregating across all shipment items for each order item
            var groupedByOrderLine = activeShipmentItems
                .GroupBy(i => i.SalesOrderItemId)
                .ToDictionary(g => g.Key, g => g.Sum(i => i.QuantityDispatched));

            foreach (var kvp in groupedByOrderLine)
            {
                var orderItem = order.Items.FirstOrDefault(i => i.Id == kvp.Key && i.IsActive)
                    ?? throw new InvalidOperationException($"Order line '{kvp.Key}' not found on order.");

                committedDispatchedQty.TryGetValue(kvp.Key, out decimal alreadyDispatched);
                decimal maxAllowable = orderItem.Quantity - alreadyDispatched;

                if (kvp.Value > maxAllowable)
                {
                    throw new InvalidOperationException(
                        $"Over-shipment rejected for product '{orderItem.ProductId}'. Ordered: {orderItem.Quantity}, Already Dispatched: {alreadyDispatched}, Attempted in this shipment: {kvp.Value}. Over-shipment tolerance is 0%.");
                }
            }

            // 4. Validate all active items' warehouse and location picking capability and batch eligibility
            var dispatchDate = (targetStatus == SalesShipmentStatus.Delivered ? deliveredAt : shipment.DispatchedAt) ?? DateTime.UtcNow;
            foreach (var item in activeShipmentItems)
            {
                await ValidateWarehouseAndLocationAsync(tenantId, item.WarehouseId, item.WarehouseLocationId);

                var product = await _productRepository.GetByIdAsync(tenantId, item.ProductId)
                    ?? throw new InvalidOperationException($"Product '{item.ProductId}' not found.");

                if (product.TrackingMode == TrackingMode.Batch || product.TrackingMode == TrackingMode.BatchAndSerial)
                {
                    if (string.IsNullOrWhiteSpace(item.BatchNumber))
                    {
                        throw new InvalidOperationException($"Batch number is required for batch-tracked product '{product.ProductCode}'.");
                    }

                    string normBatch = item.BatchNumber.Trim();
                    var batch = await _inventoryRepository.GetBatchAsync(tenantId, product.ProductId, normBatch);
                    if (batch == null)
                    {
                        throw new InvalidOperationException($"Batch '{normBatch}' does not exist for product '{product.ProductCode}'.");
                    }

                    if (!batch.IsActive)
                    {
                        throw new InvalidOperationException($"Batch '{normBatch}' is inactive and cannot be dispatched.");
                    }

                    if (batch.ExpiryDate.HasValue && batch.ExpiryDate.Value.Date < dispatchDate.Date)
                    {
                        throw new InvalidOperationException($"Batch '{normBatch}' for product '{product.ProductCode}' expired on {batch.ExpiryDate.Value:yyyy-MM-dd} and cannot be dispatched.");
                    }
                }
            }

            // 5. Mark status
            if (targetStatus == SalesShipmentStatus.Delivered)
            {
                shipment.MarkDelivered(deliveredAt, recipientName);
            }
            else
            {
                shipment.MarkDispatched();
            }

            // 6. Post Inventory ledger movement for stock-tracked products
            int sequence = 1;
            foreach (var item in activeShipmentItems)
            {
                var product = await _productRepository.GetByIdAsync(tenantId, item.ProductId)
                    ?? throw new InvalidOperationException($"Product '{item.ProductId}' not found.");

                if (product.IsStockTracked)
                {
                    decimal movementQty = item.QuantityDispatched;
                    Guid postUomId = product.UnitOfMeasureId ?? item.UnitOfMeasureId ?? Guid.Empty;

                    if (item.UnitOfMeasureId.HasValue && product.UnitOfMeasureId.HasValue && item.UnitOfMeasureId.Value != product.UnitOfMeasureId.Value)
                    {
                        movementQty = await ConvertSalesQuantityAsync(
                            tenantId,
                            companyId,
                            product.ProductId,
                            item.UnitOfMeasureId.Value,
                            product.UnitOfMeasureId.Value,
                            item.QuantityDispatched);
                        postUomId = product.UnitOfMeasureId.Value;
                    }

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
                        Quantity = movementQty,
                        UnitOfMeasureId = postUomId,
                        BatchNumber = item.BatchNumber,
                        SerialNumber = item.SerialNumber,
                        TransactionDate = shipment.DispatchedAt ?? DateTime.UtcNow
                    };

                    await _postingService.PostMovementAsync(tenantId, postReq);
                }
            }

            // 7. Recalculate Sales Order fulfillment status across all lines
            // Physical lines: fulfilled when total dispatched >= ordered quantity
            // Service lines: not subject to physical dispatch (pending separate completion)
            bool allPhysicalFulfilled = true;
            foreach (var orderItem in order.Items.Where(i => i.IsActive))
            {
                var prod = await _productRepository.GetByIdAsync(tenantId, orderItem.ProductId);
                if (prod != null && prod.ProductType != ProductType.Service)
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

            // Order is marked Fulfilled when all physical lines are fulfilled
            if (allPhysicalFulfilled)
            {
                order.Status = SalesOrderStatus.Fulfilled;
            }

            if (allPhysicalFulfilled)
            {
                // Auto-cancel any remaining open Draft shipments for this fulfilled order
                var openDrafts = otherShipments
                    .Where(s => s.Id != shipment.Id && s.Status == SalesShipmentStatus.Draft)
                    .ToList();

                foreach (var draft in openDrafts)
                {
                    draft.Cancel();
                    draft.Notes = string.IsNullOrWhiteSpace(draft.Notes)
                        ? $"[Auto-Cancelled] Sales Order fulfilled by dispatch note {shipment.ShipmentNumber}."
                        : $"{draft.Notes} | [Auto-Cancelled] Sales Order fulfilled by dispatch note {shipment.ShipmentNumber}.";
                }
            }

            await _context.SaveChangesAsync();
            await domainTx.CommitAsync();

            if (targetStatus == SalesShipmentStatus.Delivered)
            {
                await EnsureInvoiceCreatedAsync(tenantId, companyId, branchId, shipment);
            }

            return MapToDto(shipment, order.OrderNumber);
        };

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

        if (location.LocationTypeId.HasValue)
        {
            var locType = await _warehouseRepository.GetLocationTypeByIdAsync(tenantId, location.LocationTypeId.Value);
            if (locType != null && !locType.CanPick)
            {
                throw new InvalidOperationException($"Location '{location.LocationCode}' does not allow picking for dispatch (type '{locType.Name}' has picking disabled).");
            }
        }
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
            InvoiceId = shipment.InvoiceId,
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
    private async Task EnsureInvoiceCreatedAsync(Guid tenantId, Guid companyId, Guid branchId, SalesShipment shipment)
    {
        if (shipment.InvoiceId.HasValue) return;

        try
        {
            var invoiceService = _serviceProvider?.GetService<ISalesInvoiceService>();
            if (invoiceService != null)
            {
                var inv = await invoiceService.CreateDraftFromShipmentAsync(tenantId, companyId, branchId, shipment.Id);
                shipment.InvoiceId = inv.Id;
                try
        {
            await _shipmentRepository.SaveChangesAsync();
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException ex)
        {
            var entry = ex.Entries.FirstOrDefault();
            var entityName = entry?.Entity.GetType().Name ?? "Unknown";
            var state = entry?.State.ToString() ?? "Unknown";
            throw new InvalidOperationException($"Concurrency error on {entityName} (State: {state}): {ex.Message}", ex);
        }
            }
        }
        catch
        {
            // Resilient boundary: stock deduction was already safely posted and committed.
            // Failure in auto-invoice draft creation does not invalidate stock ledger.
        }
    }

    private Domain.Features.MasterData.UnitOfMeasure.IUnitOfMeasureService? GetUomService()
    {
        return _uomService ?? _serviceProvider?.GetService<Domain.Features.MasterData.UnitOfMeasure.IUnitOfMeasureService>();
    }

    private async Task<decimal> ConvertSalesQuantityAsync(
        Guid tenantId,
        Guid companyId,
        Guid productId,
        Guid fromUnitOfMeasureId,
        Guid toUnitOfMeasureId,
        decimal quantity)
    {
        if (quantity <= 0)
            throw new InvalidOperationException("Sales quantity must be greater than zero.");

        if (fromUnitOfMeasureId == toUnitOfMeasureId)
            return quantity;

        var uomService = GetUomService()
            ?? throw new InvalidOperationException("Unit of Measure service is required for sales dispatch UOM conversion.");

        var result = await uomService.ConvertProductQuantityAsync(
            tenantId,
            productId,
            fromUnitOfMeasureId,
            toUnitOfMeasureId,
            quantity,
            Domain.Features.MasterData.UnitOfMeasure.ConversionContext.Sales,
            companyId: companyId);

        decimal convertedQuantity = Math.Round(result.ConvertedQuantity, 4, MidpointRounding.AwayFromZero);
        if (convertedQuantity <= 0)
            throw new InvalidOperationException("Converted sales quantity is below the supported four-decimal persistence precision.");

        return convertedQuantity;
    }

    public async Task<SalesOrderDispatchAvailabilityDto> GetOrderDispatchAvailabilityAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid salesOrderId,
        Guid? targetLocationId = null)
    {
        var order = await _salesOrderRepository.GetByIdAsync(tenantId, companyId, branchId, salesOrderId)
            ?? throw new KeyNotFoundException("Sales Order not found in authorized branch/company scope.");

        if (order.Status != SalesOrderStatus.Confirmed && order.Status != SalesOrderStatus.Fulfilled)
        {
            throw new InvalidOperationException($"Cannot evaluate dispatch availability for Sales Order in status '{order.Status}'. Only Confirmed or partially Fulfilled orders can be dispatched.");
        }

        var activeOrderItems = order.Items.Where(i => i.IsActive).ToList();
        var physicalLines = new List<(SalesOrderItem Item, Product Product)>();
        foreach (var item in activeOrderItems)
        {
            var product = await _productRepository.GetByIdAsync(tenantId, item.ProductId)
                ?? throw new InvalidOperationException($"Product '{item.ProductId}' not found.");

            if (product.ProductType != ProductType.Service)
            {
                physicalLines.Add((item, product));
            }
        }

        var existingShipments = await _shipmentRepository.GetBySalesOrderIdAsync(tenantId, salesOrderId);

        // Dispatched or Delivered quantities across all completed shipments
        var dispatchedQtyByLine = existingShipments
            .Where(s => s.Status == SalesShipmentStatus.Dispatched || s.Status == SalesShipmentStatus.Delivered)
            .SelectMany(s => s.Items)
            .Where(i => i.IsActive)
            .GroupBy(i => i.SalesOrderItemId)
            .ToDictionary(g => g.Key, g => g.Sum(i => i.QuantityDispatched));

        // Draft allocated quantities across open Draft shipments (for shortage & picking visibility)
        var draftAllocatedQtyByLine = existingShipments
            .Where(s => s.Status == SalesShipmentStatus.Draft)
            .SelectMany(s => s.Items)
            .Where(i => i.IsActive)
            .GroupBy(i => i.SalesOrderItemId)
            .ToDictionary(g => g.Key, g => g.Sum(i => i.QuantityDispatched));

        var remainingLines = new List<(SalesOrderItem Item, Product Product, decimal RemainingQty)>();
        foreach (var (item, product) in physicalLines)
        {
            dispatchedQtyByLine.TryGetValue(item.Id, out decimal alreadyDispatched);
            decimal remaining = item.Quantity - alreadyDispatched;
            if (remaining > 0)
            {
                remainingLines.Add((item, product, remaining));
            }
        }

        // Discover active warehouses and active pickable locations for the tenant and company
        var warehouses = await _warehouseRepository.GetWarehousesAsync(tenantId);
        var activeWarehouses = warehouses.Where(w => w.IsActive && w.CompanyId == order.CompanyId)
            .OrderByDescending(w => w.BranchId == order.BranchId)
            .ThenByDescending(w => w.CreatedAt)
            .ToList();

        var locTypes = (await _warehouseRepository.GetLocationTypesAsync(tenantId)).ToDictionary(t => t.LocationTypeId);

        var allPickableLocations = new List<(WarehouseLocation Loc, Warehouse Wh, bool IsStaging)>();
        foreach (var wh in activeWarehouses)
        {
            var locs = await _warehouseRepository.GetLocationsAsync(tenantId, wh.WarehouseId);
            var pickableLocs = locs.Where(l => l.IsActive && (!l.LocationTypeId.HasValue || (locTypes.TryGetValue(l.LocationTypeId.Value, out var lt) && lt.CanPick)))
                .OrderBy(l => l.CreatedAt)
                .ToList();

            foreach (var loc in pickableLocs)
            {
                bool isStaging = loc.LocationTypeId.HasValue
                    && locTypes.TryGetValue(loc.LocationTypeId.Value, out var lt)
                    && (string.Equals(lt.Code, "STAGING", StringComparison.OrdinalIgnoreCase)
                        || (lt.Name != null && lt.Name.IndexOf("staging", StringComparison.OrdinalIgnoreCase) >= 0));

                if (!isStaging && (loc.LocationCode.IndexOf("STAGE", StringComparison.OrdinalIgnoreCase) >= 0
                    || loc.LocationName.IndexOf("staging", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    isStaging = true;
                }

                allPickableLocations.Add((loc, wh, isStaging));
            }
        }

        var defaultLocTuple = allPickableLocations.FirstOrDefault(t => t.Wh.BranchId == order.BranchId);
        if (defaultLocTuple.Loc == null)
        {
            defaultLocTuple = allPickableLocations.FirstOrDefault();
        }

        (WarehouseLocation Loc, Warehouse Wh, bool IsStaging) selectedLocTuple;
        bool isExplicitTarget = false;

        if (targetLocationId.HasValue && targetLocationId.Value != Guid.Empty)
        {
            selectedLocTuple = allPickableLocations.FirstOrDefault(t => t.Loc.WarehouseLocationId == targetLocationId.Value);
            if (selectedLocTuple.Loc == null)
            {
                var targetLoc = await _warehouseRepository.GetLocationByIdAsync(tenantId, targetLocationId.Value);
                if (targetLoc != null && targetLoc.LocationTypeId.HasValue && locTypes.TryGetValue(targetLoc.LocationTypeId.Value, out var targetLt) && !targetLt.CanPick)
                {
                    throw new InvalidOperationException($"Specified target location '{targetLoc.LocationCode}' does not allow picking for dispatch.");
                }

                throw new InvalidOperationException("Specified target location not found or is inactive.");
            }
            isExplicitTarget = true;
        }
        else
        {
            selectedLocTuple = defaultLocTuple;
        }

        var primaryWarehouseId = selectedLocTuple.Wh?.WarehouseId ?? defaultLocTuple.Wh?.WarehouseId ?? Guid.Empty;

        var lineAvailabilities = new List<SalesOrderDispatchLineAvailabilityDto>();
        foreach (var (item, product, remaining) in remainingLines)
        {
            dispatchedQtyByLine.TryGetValue(item.Id, out decimal alreadyDispatched);
            draftAllocatedQtyByLine.TryGetValue(item.Id, out decimal draftAllocated);

            decimal remainingBaseQty = remaining;
            bool isConverted = product.IsStockTracked
                && item.UnitOfMeasureId.HasValue
                && product.UnitOfMeasureId.HasValue
                && item.UnitOfMeasureId.Value != product.UnitOfMeasureId.Value;

            if (isConverted && item.UnitOfMeasureId.HasValue && product.UnitOfMeasureId.HasValue)
            {
                remainingBaseQty = await ConvertSalesQuantityAsync(
                    tenantId,
                    companyId,
                    product.ProductId,
                    item.UnitOfMeasureId.Value,
                    product.UnitOfMeasureId.Value,
                    remaining);
            }

            bool isBatch = product.TrackingMode == TrackingMode.Batch || product.TrackingMode == TrackingMode.BatchAndSerial;

            // Query stock across all pickable locations in all warehouses
            var locationStocks = new List<(WarehouseLocation Loc, Warehouse Wh, bool IsStaging, decimal Stock)>();
            foreach (var (loc, wh, isStaging) in allPickableLocations)
            {
                decimal stock = 0m;
                if (product.IsStockTracked)
                {
                    stock = await _postingService.GetTotalAvailableStockAsync(tenantId, loc.WarehouseLocationId, product.ProductId, isBatch);
                }
                else
                {
                    stock = remainingBaseQty;
                }
                locationStocks.Add((loc, wh, isStaging, stock));
            }

            // Eligible locations in primary warehouse
            var primaryLocs = locationStocks.Where(ls => ls.Wh.WarehouseId == primaryWarehouseId && ls.Stock > 0).ToList();
            var eligibleLocationDtos = primaryLocs.Select(ls => new LocationStockAvailabilityDto
            {
                WarehouseId = ls.Wh.WarehouseId,
                WarehouseCode = ls.Wh.WarehouseCode,
                WarehouseName = ls.Wh.WarehouseName,
                WarehouseLocationId = ls.Loc.WarehouseLocationId,
                LocationCode = ls.Loc.LocationCode,
                LocationName = ls.Loc.LocationName,
                IsStaging = ls.IsStaging,
                AvailableQuantity = ls.Stock,
                CanFulfil = ls.Stock >= remainingBaseQty
            }).ToList();

            // Alternative warehouses
            var altWarehouses = locationStocks
                .Where(ls => ls.Wh.WarehouseId != primaryWarehouseId)
                .GroupBy(ls => ls.Wh.WarehouseId)
                .Select(g =>
                {
                    var wh = g.First().Wh;
                    decimal totalStock = g.Sum(x => x.Stock);
                    return new WarehouseStockSummaryDto
                    {
                        WarehouseId = wh.WarehouseId,
                        WarehouseCode = wh.WarehouseCode,
                        WarehouseName = wh.WarehouseName,
                        TotalAvailableStock = totalStock,
                        CanFulfil = totalStock >= remainingBaseQty
                    };
                })
                .Where(w => w.TotalAvailableStock > 0)
                .OrderByDescending(w => w.TotalAvailableStock)
                .ToList();

            // Determine default suggested source for this line
            (WarehouseLocation? Loc, Warehouse? Wh, bool IsStaging, decimal Stock) suggestedSource = default;

            var fullStaging = primaryLocs.Where(ls => ls.IsStaging && ls.Stock >= remainingBaseQty).OrderByDescending(ls => ls.Stock).FirstOrDefault();
            if (fullStaging.Loc != null)
            {
                suggestedSource = fullStaging;
            }
            else
            {
                var fullBin = primaryLocs.Where(ls => !ls.IsStaging && ls.Stock >= remainingBaseQty).OrderByDescending(ls => ls.Stock).FirstOrDefault();
                if (fullBin.Loc != null)
                {
                    suggestedSource = fullBin;
                }
                else
                {
                    var partialStaging = primaryLocs.Where(ls => ls.IsStaging && ls.Stock > 0).OrderByDescending(ls => ls.Stock).FirstOrDefault();
                    if (partialStaging.Loc != null)
                    {
                        suggestedSource = partialStaging;
                    }
                    else
                    {
                        var partialBin = primaryLocs.Where(ls => ls.Stock > 0).OrderByDescending(ls => ls.Stock).FirstOrDefault();
                        if (partialBin.Loc != null)
                        {
                            suggestedSource = partialBin;
                        }
                        else
                        {
                            var altWithStock = locationStocks.Where(ls => ls.Stock > 0).OrderByDescending(ls => ls.Stock).FirstOrDefault();
                            if (altWithStock.Loc != null)
                            {
                                suggestedSource = altWithStock;
                            }
                            else
                            {
                                var defaultPrimary = allPickableLocations.FirstOrDefault(l => l.Wh.WarehouseId == primaryWarehouseId);
                                suggestedSource = (defaultPrimary.Loc ?? selectedLocTuple.Loc, defaultPrimary.Wh ?? selectedLocTuple.Wh, defaultPrimary.IsStaging, 0m);
                            }
                        }
                    }
                }
            }

            // Multi-bin split suggestions when single suggested location cannot fulfill 100%
            var suggestedSplits = new List<SuggestedLocationSplitDto>();
            if (suggestedSource.Stock < remainingBaseQty && primaryLocs.Count > 0)
            {
                var sortedCandidates = primaryLocs
                    .OrderByDescending(ls => ls.IsStaging)
                    .ThenByDescending(ls => ls.Stock)
                    .ToList();

                decimal needed = remainingBaseQty;
                foreach (var cand in sortedCandidates)
                {
                    if (needed <= 0) break;
                    decimal take = Math.Min(needed, cand.Stock);
                    if (take > 0)
                    {
                        suggestedSplits.Add(new SuggestedLocationSplitDto
                        {
                            WarehouseLocationId = cand.Loc.WarehouseLocationId,
                            LocationCode = cand.Loc.LocationCode,
                            LocationName = cand.Loc.LocationName,
                            IsStaging = cand.IsStaging,
                            AvailableQuantity = cand.Stock,
                            SuggestedQuantity = take
                        });
                        needed -= take;
                    }
                }
            }

            decimal available = 0m;
            if (isExplicitTarget && selectedLocTuple.Loc != null)
            {
                var locEntry = locationStocks.FirstOrDefault(ls => ls.Loc.WarehouseLocationId == selectedLocTuple.Loc.WarehouseLocationId);
                available = locEntry.Stock;
            }
            else
            {
                available = suggestedSource.Stock;
            }

            bool canFulfil = available >= remainingBaseQty;
            decimal shortage = Math.Max(0m, remainingBaseQty - available);
            decimal displayAvailable = available;

            if (isConverted && item.UnitOfMeasureId.HasValue && product.UnitOfMeasureId.HasValue && GetUomService() != null)
            {
                try
                {
                    var availTx = await GetUomService()!.ConvertProductQuantityAsync(
                        tenantId,
                        product.ProductId,
                        product.UnitOfMeasureId.Value,
                        item.UnitOfMeasureId.Value,
                        available,
                        Domain.Features.MasterData.UnitOfMeasure.ConversionContext.Sales,
                        companyId: companyId);
                    displayAvailable = Math.Round(availTx.ConvertedQuantity, 4, MidpointRounding.AwayFromZero);
                    shortage = Math.Max(0m, remaining - displayAvailable);
                }
                catch
                {
                    displayAvailable = available;
                    shortage = Math.Max(0m, remainingBaseQty - available);
                }
            }
            else if (!isConverted)
            {
                shortage = Math.Max(0m, remaining - available);
            }

            lineAvailabilities.Add(new SalesOrderDispatchLineAvailabilityDto
            {
                SalesOrderItemId = item.Id,
                ProductId = product.ProductId,
                ProductCode = product.ProductCode,
                ProductName = product.ProductName,
                OrderedQuantity = item.Quantity,
                AlreadyDispatchedQuantity = alreadyDispatched,
                RemainingQuantity = remaining,
                CurrentDraftAllocatedQuantity = draftAllocated,
                AvailableQuantity = displayAvailable,
                ShortageQuantity = shortage,
                CanFulfil = canFulfil,
                UnitOfMeasureId = item.UnitOfMeasureId,
                DefaultSuggestedWarehouseId = suggestedSource.Wh?.WarehouseId,
                DefaultSuggestedWarehouseName = suggestedSource.Wh?.WarehouseName,
                DefaultSuggestedLocationId = suggestedSource.Loc?.WarehouseLocationId,
                DefaultSuggestedLocationCode = suggestedSource.Loc?.LocationCode,
                DefaultSuggestedLocationName = suggestedSource.Loc?.LocationName,
                IsDefaultStaging = suggestedSource.IsStaging,
                SuggestedSplits = suggestedSplits,
                EligibleLocations = eligibleLocationDtos,
                AlternativeWarehouses = altWarehouses
            });
        }

        bool canSelectedFulfil = lineAvailabilities.Count > 0 && lineAvailabilities.All(l => l.CanFulfil);

        var singleEligibleLocations = new List<EligibleLocationDto>();
        bool canDefaultFulfil = false;

        foreach (var (loc, wh, isStaging) in allPickableLocations)
        {
            bool canLocFulfilAll = true;
            foreach (var (item, product, remaining) in remainingLines)
            {
                decimal avail = 0m;
                if (product.IsStockTracked)
                {
                    bool isBatch = product.TrackingMode == TrackingMode.Batch || product.TrackingMode == TrackingMode.BatchAndSerial;
                    avail = await _postingService.GetTotalAvailableStockAsync(tenantId, loc.WarehouseLocationId, product.ProductId, isBatch);
                }
                else
                {
                    avail = remaining;
                }

                decimal reqQty = remaining;
                if (product.IsStockTracked && item.UnitOfMeasureId.HasValue && product.UnitOfMeasureId.HasValue && item.UnitOfMeasureId.Value != product.UnitOfMeasureId.Value)
                {
                    reqQty = await ConvertSalesQuantityAsync(
                        tenantId,
                        companyId,
                        product.ProductId,
                        item.UnitOfMeasureId.Value,
                        product.UnitOfMeasureId.Value,
                        remaining);
                }

                if (avail < reqQty)
                {
                    canLocFulfilAll = false;
                    break;
                }
            }

            if (defaultLocTuple.Loc != null && loc.WarehouseLocationId == defaultLocTuple.Loc.WarehouseLocationId)
            {
                canDefaultFulfil = canLocFulfilAll;
            }

            if (canLocFulfilAll)
            {
                singleEligibleLocations.Add(new EligibleLocationDto
                {
                    WarehouseId = wh.WarehouseId,
                    WarehouseName = wh.WarehouseName,
                    WarehouseCode = wh.WarehouseCode,
                    LocationId = loc.WarehouseLocationId,
                    LocationCode = loc.LocationCode,
                    LocationName = loc.LocationName,
                    IsDefault = (defaultLocTuple.Loc != null && loc.WarehouseLocationId == defaultLocTuple.Loc.WarehouseLocationId),
                    IsStaging = isStaging
                });
            }
        }

        var eligibleWarehousesSummary = activeWarehouses.Select(wh =>
        {
            return new WarehouseStockSummaryDto
            {
                WarehouseId = wh.WarehouseId,
                WarehouseCode = wh.WarehouseCode,
                WarehouseName = wh.WarehouseName,
                TotalAvailableStock = lineAvailabilities
                    .SelectMany(l => l.EligibleLocations.Where(el => el.WarehouseId == wh.WarehouseId).Select(el => el.AvailableQuantity))
                    .Concat(lineAvailabilities.SelectMany(l => l.AlternativeWarehouses.Where(aw => aw.WarehouseId == wh.WarehouseId).Select(aw => aw.TotalAvailableStock)))
                    .Sum(),
                CanFulfil = lineAvailabilities.All(l =>
                    l.EligibleLocations.Any(el => el.WarehouseId == wh.WarehouseId && el.CanFulfil)
                    || l.AlternativeWarehouses.Any(aw => aw.WarehouseId == wh.WarehouseId && aw.CanFulfil))
            };
        }).Where(w => w.TotalAvailableStock > 0).ToList();

        return new SalesOrderDispatchAvailabilityDto
        {
            SalesOrderId = order.Id,
            OrderNumber = order.OrderNumber,
            DefaultWarehouseId = defaultLocTuple.Wh?.WarehouseId,
            DefaultWarehouseName = defaultLocTuple.Wh?.WarehouseName,
            DefaultLocationId = defaultLocTuple.Loc?.WarehouseLocationId,
            DefaultLocationCode = defaultLocTuple.Loc?.LocationCode,
            DefaultLocationName = defaultLocTuple.Loc?.LocationName,
            SelectedWarehouseId = selectedLocTuple.Wh?.WarehouseId,
            SelectedWarehouseName = selectedLocTuple.Wh?.WarehouseName,
            SelectedLocationId = selectedLocTuple.Loc?.WarehouseLocationId,
            SelectedLocationCode = selectedLocTuple.Loc?.LocationCode,
            SelectedLocationName = selectedLocTuple.Loc?.LocationName,
            CanDefaultLocationFulfil = canDefaultFulfil,
            CanFulfil = canSelectedFulfil,
            HasShortages = !canSelectedFulfil,
            TotalShortageLines = lineAvailabilities.Count(l => l.ShortageQuantity > 0),
            EligibleLocations = singleEligibleLocations,
            EligibleWarehouses = eligibleWarehousesSummary,
            Lines = lineAvailabilities
        };
    }

    public async Task<SalesShipmentDto> CreateAndDispatchFromOrderAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid salesOrderId,
        DispatchOrderRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        if (request.WarehouseId == Guid.Empty) throw new ArgumentException("Warehouse is required.", nameof(request.WarehouseId));
        if (request.WarehouseLocationId == Guid.Empty) throw new ArgumentException("Warehouse Location is required.", nameof(request.WarehouseLocationId));

        await using var domainTx = await _context.Database.BeginTransactionAsync();

        // 1. Acquire row lock on SalesOrder
        await _context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM SalesOrders WITH (UPDLOCK, ROWLOCK) WHERE Id = {salesOrderId} AND TenantId = {tenantId}");

        var order = await _salesOrderRepository.GetByIdAsync(tenantId, companyId, branchId, salesOrderId)
            ?? throw new KeyNotFoundException("Sales Order not found in authorized branch/company scope.");

        if (order.Status != SalesOrderStatus.Confirmed && order.Status != SalesOrderStatus.Fulfilled)
        {
            throw new InvalidOperationException($"Cannot dispatch against Sales Order in status '{order.Status}'.");
        }

        await ValidateWarehouseAndLocationAsync(tenantId, request.WarehouseId, request.WarehouseLocationId);

        var activeOrderItems = order.Items.Where(i => i.IsActive).ToList();
        var physicalLines = new List<(SalesOrderItem Item, Product Product)>();
        foreach (var item in activeOrderItems)
        {
            var product = await _productRepository.GetByIdAsync(tenantId, item.ProductId)
                ?? throw new InvalidOperationException($"Product '{item.ProductId}' not found.");

            if (product.ProductType != ProductType.Service)
            {
                physicalLines.Add((item, product));
            }
        }

        if (physicalLines.Count == 0)
        {
            throw new InvalidOperationException("Sales Order contains no physical goods for shipment.");
        }

        // 2. Authoritative remaining unfulfilled quantities across committed shipments
        var existingShipments = await _shipmentRepository.GetBySalesOrderIdAsync(tenantId, salesOrderId);
        var committedDispatchedQty = existingShipments
            .Where(s => s.Status == SalesShipmentStatus.Dispatched || s.Status == SalesShipmentStatus.Delivered)
            .SelectMany(s => s.Items)
            .Where(i => i.IsActive)
            .GroupBy(i => i.SalesOrderItemId)
            .ToDictionary(g => g.Key, g => g.Sum(i => i.QuantityDispatched));

        var unfulfilledLines = new List<(SalesOrderItem Item, Product Product, decimal RemainingQty)>();
        foreach (var (item, product) in physicalLines)
        {
            committedDispatchedQty.TryGetValue(item.Id, out decimal priorDispatched);
            decimal remaining = item.Quantity - priorDispatched;
            if (remaining > 0)
            {
                unfulfilledLines.Add((item, product, remaining));
            }
        }

        if (unfulfilledLines.Count == 0)
        {
            throw new InvalidOperationException("All physical goods on this Sales Order have already been fully fulfilled.");
        }

        // 3. Authoritative stock revalidation at the specified location for all unfulfilled lines
        var dateAsOf = request.ShipmentDate ?? DateTime.UtcNow;
        foreach (var (item, product, remaining) in unfulfilledLines)
        {
            if (product.IsStockTracked)
            {
                bool isBatch = product.TrackingMode == TrackingMode.Batch || product.TrackingMode == TrackingMode.BatchAndSerial;
                decimal available = await _postingService.GetTotalAvailableStockAsync(
                    tenantId,
                    request.WarehouseLocationId,
                    product.ProductId,
                    isBatch,
                    dateAsOf);

                decimal requiredBaseQty = remaining;
                if (item.UnitOfMeasureId.HasValue && product.UnitOfMeasureId.HasValue && item.UnitOfMeasureId.Value != product.UnitOfMeasureId.Value)
                {
                    requiredBaseQty = await ConvertSalesQuantityAsync(
                        tenantId,
                        companyId,
                        product.ProductId,
                        item.UnitOfMeasureId.Value,
                        product.UnitOfMeasureId.Value,
                        remaining);
                }

                if (available < requiredBaseQty)
                {
                    throw new InvalidOperationException(
                        $"Insufficient eligible stock at location for product '{product.ProductCode}'. Required: {requiredBaseQty}, Available: {available}.");
                }
            }
        }

        // 4. Create SalesShipment
        string shipmentNum = string.IsNullOrWhiteSpace(request.ShipmentNumber)
            ? $"DSP-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}"[..18].ToUpper()
            : request.ShipmentNumber.Trim();

        if (await _shipmentRepository.ExistsShipmentNumberAsync(tenantId, companyId, shipmentNum))
        {
            throw new InvalidOperationException($"Shipment number '{shipmentNum}' already exists for this company.");
        }

        var shipment = SalesShipment.Create(
            tenantId,
            companyId,
            branchId,
            order.Id,
            order.CustomerId,
            shipmentNum,
            request.Method,
            request.WarehouseId,
            request.ShipmentDate,
            request.CarrierName,
            request.TrackingNumber,
            request.RecipientName,
            !string.IsNullOrWhiteSpace(request.DeliveryAddress) ? request.DeliveryAddress.Trim() : order.DeliveryAddressSnapshot,
            request.VehicleNumber,
            request.Notes);

        if (request.Items != null && request.Items.Count > 0)
        {
            // Explicit line allocations supplied by operator (e.g. from BatchSerialTrackingDialog)
            // Validate zero over-shipment tolerance against order lines
            var groupedByOrderLine = request.Items
                .GroupBy(i => i.SalesOrderItemId)
                .ToDictionary(g => g.Key, g => g.Sum(i => i.QuantityDispatched));

            foreach (var kvp in groupedByOrderLine)
            {
                var targetLine = unfulfilledLines.FirstOrDefault(l => l.Item.Id == kvp.Key);
                if (targetLine == default)
                {
                    throw new InvalidOperationException(
                        $"Order item '{kvp.Key}' is not an unfulfilled physical goods item on this Sales Order.");
                }

                if (kvp.Value <= 0)
                {
                    throw new ArgumentException("Quantity dispatched must be greater than zero.");
                }

                if (kvp.Value > targetLine.RemainingQty)
                {
                    throw new InvalidOperationException(
                        $"Attempted quantity {kvp.Value} exceeds remaining unfulfilled quantity {targetLine.RemainingQty} for product '{targetLine.Product.ProductCode}'. Over-shipment tolerance is 0%.");
                }
            }

            foreach (var reqItem in request.Items)
            {
                var targetLine = unfulfilledLines.First(l => l.Item.Id == reqItem.SalesOrderItemId);
                var product = targetLine.Product;

                var effectiveWarehouseId = reqItem.WarehouseId != Guid.Empty ? reqItem.WarehouseId : request.WarehouseId;
                var effectiveLocationId = reqItem.WarehouseLocationId != Guid.Empty ? reqItem.WarehouseLocationId : request.WarehouseLocationId;

                await ValidateWarehouseAndLocationAsync(tenantId, effectiveWarehouseId, effectiveLocationId);

                // Authoritative batch validation if batch-tracked
                if (product.TrackingMode == TrackingMode.Batch || product.TrackingMode == TrackingMode.BatchAndSerial)
                {
                    if (string.IsNullOrWhiteSpace(reqItem.BatchNumber))
                    {
                        throw new InvalidOperationException($"Batch number is required for batch-tracked product '{product.ProductCode}'.");
                    }

                    string normBatch = reqItem.BatchNumber.Trim();
                    var batch = await _inventoryRepository.GetBatchAsync(tenantId, product.ProductId, normBatch);
                    if (batch == null)
                    {
                        throw new InvalidOperationException($"Batch '{normBatch}' does not exist for product '{product.ProductCode}'.");
                    }

                    if (!batch.IsActive)
                    {
                        throw new InvalidOperationException($"Batch '{normBatch}' is inactive and cannot be dispatched.");
                    }

                    if (batch.ExpiryDate.HasValue && batch.ExpiryDate.Value.Date < dateAsOf.Date)
                    {
                        throw new InvalidOperationException($"Batch '{normBatch}' for product '{product.ProductCode}' expired on {batch.ExpiryDate.Value:yyyy-MM-dd} and cannot be dispatched.");
                    }
                }

                // Authoritative serial validation if serial-tracked
                if (product.TrackingMode == TrackingMode.Serial || product.TrackingMode == TrackingMode.BatchAndSerial)
                {
                    if (string.IsNullOrWhiteSpace(reqItem.SerialNumber))
                    {
                        throw new InvalidOperationException($"Serial number is required for serial-tracked product '{product.ProductCode}'.");
                    }

                    if (Math.Abs(reqItem.QuantityDispatched) != 1.0m)
                    {
                        throw new InvalidOperationException($"Serial-tracked items must have quantity of 1. Specified: {reqItem.QuantityDispatched}.");
                    }
                }

                var shipmentItem = SalesShipmentItem.Create(
                    shipment.Id,
                    targetLine.Item.Id,
                    product.ProductId,
                    effectiveWarehouseId,
                    effectiveLocationId,
                    reqItem.QuantityDispatched,
                    reqItem.UnitOfMeasureId ?? targetLine.Item.UnitOfMeasureId,
                    batchNumber: reqItem.BatchNumber?.Trim(),
                    serialNumber: reqItem.SerialNumber?.Trim(),
                    notes: reqItem.Notes ?? request.Notes);

                shipment.AddItem(shipmentItem);
            }
        }
        else
        {
            // Default automatic FEFO allocation loop
            foreach (var (item, product, remaining) in unfulfilledLines)
            {
                bool isBatch = product.TrackingMode == TrackingMode.Batch || product.TrackingMode == TrackingMode.BatchAndSerial;
                if (isBatch)
                {
                    var eligibleBatches = await _inventoryRepository.GetEligibleBatchesAsync(
                        tenantId,
                        product.ProductId,
                        request.WarehouseId,
                        request.WarehouseLocationId,
                        dateAsOf);

                    decimal unitFactor = 1m;
                    if (item.UnitOfMeasureId.HasValue && product.UnitOfMeasureId.HasValue && item.UnitOfMeasureId.Value != product.UnitOfMeasureId.Value)
                    {
                        unitFactor = await ConvertSalesQuantityAsync(
                            tenantId,
                            companyId,
                            product.ProductId,
                            item.UnitOfMeasureId.Value,
                            product.UnitOfMeasureId.Value,
                            1m);
                    }

                    decimal neededInBase = Math.Round(remaining * unitFactor, 4, MidpointRounding.AwayFromZero);
                    foreach (var b in eligibleBatches)
                    {
                        if (neededInBase <= 0) break;
                        decimal takeInBase = Math.Min(neededInBase, b.QuantityAvailable);
                        if (takeInBase <= 0) continue;

                        decimal takeInTxUom = unitFactor == 1m
                            ? takeInBase
                            : Math.Round(takeInBase / unitFactor, 4, MidpointRounding.AwayFromZero);

                        var shipmentItem = SalesShipmentItem.Create(
                            shipment.Id,
                            item.Id,
                            product.ProductId,
                            request.WarehouseId,
                            request.WarehouseLocationId,
                            takeInTxUom,
                            item.UnitOfMeasureId,
                            batchNumber: b.BatchNumber,
                            notes: request.Notes);

                        shipment.AddItem(shipmentItem);
                        neededInBase -= takeInBase;
                    }

                    if (neededInBase > 0)
                    {
                        throw new InvalidOperationException(
                            $"Insufficient eligible batch stock at location for product '{product.ProductCode}'. Required: {remaining * unitFactor}, Shortage: {neededInBase}.");
                    }
                }
                else
                {
                    var shipmentItem = SalesShipmentItem.Create(
                        shipment.Id,
                        item.Id,
                        product.ProductId,
                        request.WarehouseId,
                        request.WarehouseLocationId,
                        remaining,
                        item.UnitOfMeasureId,
                        notes: request.Notes);

                    shipment.AddItem(shipmentItem);
                }
            }
        }

        shipment.MarkDispatched();

        await _shipmentRepository.AddAsync(shipment);
        try
        {
            await _shipmentRepository.SaveChangesAsync();
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException ex)
        {
            var entry = ex.Entries.FirstOrDefault();
            var entityName = entry?.Entity.GetType().Name ?? "Unknown";
            var state = entry?.State.ToString() ?? "Unknown";
            throw new InvalidOperationException($"Concurrency error on {entityName} (State: {state}): {ex.Message}", ex);
        }

        // 5. Post Inventory movements via _postingService
        int sequence = 1;
        foreach (var item in shipment.Items.Where(i => i.IsActive))
        {
            var product = await _productRepository.GetByIdAsync(tenantId, item.ProductId);
            if (product != null && product.IsStockTracked)
            {
                decimal movementQty = item.QuantityDispatched;
                Guid postUomId = product.UnitOfMeasureId ?? item.UnitOfMeasureId ?? Guid.Empty;

                if (item.UnitOfMeasureId.HasValue && product.UnitOfMeasureId.HasValue && item.UnitOfMeasureId.Value != product.UnitOfMeasureId.Value)
                {
                    movementQty = await ConvertSalesQuantityAsync(
                        tenantId,
                        companyId,
                        product.ProductId,
                        item.UnitOfMeasureId.Value,
                        product.UnitOfMeasureId.Value,
                        item.QuantityDispatched);
                    postUomId = product.UnitOfMeasureId.Value;
                }

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
                    Quantity = movementQty,
                    UnitOfMeasureId = postUomId,
                    BatchNumber = item.BatchNumber,
                    SerialNumber = item.SerialNumber,
                    TransactionDate = shipment.DispatchedAt ?? DateTime.UtcNow
                };

                await _postingService.PostMovementAsync(tenantId, postReq);
            }
        }

        // 6. Recalculate Sales Order fulfillment status
        bool allFulfilled = true;
        foreach (var (item, product) in physicalLines)
        {
            committedDispatchedQty.TryGetValue(item.Id, out decimal priorDispatched);
            decimal currentDispatched = shipment.Items
                .Where(i => i.SalesOrderItemId == item.Id && i.IsActive)
                .Sum(i => i.QuantityDispatched);

            if (priorDispatched + currentDispatched < item.Quantity)
            {
                allFulfilled = false;
                break;
            }
        }

        if (allFulfilled)
        {
            order.Status = SalesOrderStatus.Fulfilled;

            var remainingDrafts = existingShipments
                .Where(s => s.Status == SalesShipmentStatus.Draft)
                .ToList();

            foreach (var draft in remainingDrafts)
            {
                draft.Cancel();
                draft.Notes = string.IsNullOrWhiteSpace(draft.Notes)
                    ? $"[Auto-Cancelled] Sales Order fulfilled by dispatch note {shipment.ShipmentNumber}."
                    : $"{draft.Notes} | [Auto-Cancelled] Sales Order fulfilled by dispatch note {shipment.ShipmentNumber}.";
            }
        }

        await _salesOrderRepository.SaveChangesAsync();
        await domainTx.CommitAsync();

        await EnsureInvoiceCreatedAsync(tenantId, companyId, branchId, shipment);

        return MapToDto(shipment, order.OrderNumber);
    }

    public async Task<ReplenishmentTransferResult> CreateReplenishmentTransferDraftAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid salesOrderId,
        CreateReplenishmentTransferRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        if (request.SalesOrderId != Guid.Empty && request.SalesOrderId != salesOrderId)
        {
            throw new InvalidOperationException("Sales Order ID in request body does not match route parameter.");
        }

        var order = await _salesOrderRepository.GetByIdAsync(tenantId, companyId, branchId, salesOrderId)
            ?? throw new KeyNotFoundException("Sales Order not found in authorized branch/company scope.");

        if (order.Status != SalesOrderStatus.Confirmed && order.Status != SalesOrderStatus.Fulfilled)
        {
            throw new InvalidOperationException($"Cannot initiate replenishment transfer for Sales Order in status '{order.Status}'. Order must be Confirmed or Fulfilled.");
        }

        var orderItem = order.Items.FirstOrDefault(i => i.Id == request.SalesOrderItemId && i.IsActive)
            ?? throw new InvalidOperationException($"Sales Order line '{request.SalesOrderItemId}' not found on order '{order.OrderNumber}'.");

        if (orderItem.ProductId != request.ProductId)
        {
            throw new InvalidOperationException("Product ID does not match the specified Sales Order line.");
        }

        var product = await _productRepository.GetByIdAsync(tenantId, request.ProductId)
            ?? throw new KeyNotFoundException($"Product '{request.ProductId}' not found.");

        if (product.ProductType == ProductType.Service)
        {
            throw new InvalidOperationException($"Product '{product.ProductCode}' is a service and cannot be transferred.");
        }

        if (request.Quantity <= 0)
        {
            throw new InvalidOperationException("Replenishment transfer quantity must be greater than zero.");
        }

        if (request.SourceWarehouseId == request.DestinationWarehouseId)
        {
            throw new InvalidOperationException("Source and destination warehouses cannot be the same for an inter-warehouse replenishment transfer.");
        }

        var sourceWh = await _warehouseRepository.GetWarehouseByIdAsync(tenantId, request.SourceWarehouseId)
            ?? throw new KeyNotFoundException("Source warehouse not found.");

        var destWh = await _warehouseRepository.GetWarehouseByIdAsync(tenantId, request.DestinationWarehouseId)
            ?? throw new KeyNotFoundException("Destination warehouse not found.");

        if (!sourceWh.IsActive || !destWh.IsActive)
        {
            throw new InvalidOperationException("Both source and destination warehouses must be active.");
        }

        if (sourceWh.CompanyId != companyId || destWh.CompanyId != companyId)
        {
            throw new InvalidOperationException("Warehouses must belong to the authorized company scope.");
        }

        // Verify available stock in source warehouse
        var sourceLocs = await _warehouseRepository.GetLocationsAsync(tenantId, sourceWh.WarehouseId);
        var locTypes = (await _warehouseRepository.GetLocationTypesAsync(tenantId)).ToDictionary(t => t.LocationTypeId);
        var pickableLocs = sourceLocs.Where(l => l.IsActive && (!l.LocationTypeId.HasValue || (locTypes.TryGetValue(l.LocationTypeId.Value, out var lt) && lt.CanPick))).ToList();

        decimal totalSourceAvail = 0m;
        bool isBatch = product.TrackingMode == TrackingMode.Batch || product.TrackingMode == TrackingMode.BatchAndSerial;
        foreach (var loc in pickableLocs)
        {
            totalSourceAvail += await _postingService.GetTotalAvailableStockAsync(tenantId, loc.WarehouseLocationId, product.ProductId, isBatch);
        }

        if (totalSourceAvail < request.Quantity)
        {
            throw new InvalidOperationException($"Insufficient available stock in source warehouse '{sourceWh.WarehouseName}'. Requested: {request.Quantity}, Available: {totalSourceAvail}.");
        }

        string transferRef = $"TR-REPL-{Guid.NewGuid():N}"[..14].ToUpperInvariant();

        return new ReplenishmentTransferResult
        {
            TransferReferenceNumber = transferRef,
            Status = "Draft",
            ProductId = product.ProductId,
            SourceWarehouseId = sourceWh.WarehouseId,
            DestinationWarehouseId = destWh.WarehouseId,
            Quantity = request.Quantity,
            Message = $"Draft replenishment transfer '{transferRef}' initiated from '{sourceWh.WarehouseName}' to '{destWh.WarehouseName}' for {request.Quantity} {product.ProductCode}. Destination stock is NOT increased until authorized transfer receipt."
        };
    }

    public async Task<ReplenishmentRequisitionResult> CreateReplenishmentPurchaseRequisitionAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid salesOrderId,
        CreateReplenishmentRequisitionRequest request,
        Guid actorUserId,
        string actorUserName)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        if (request.SalesOrderId != Guid.Empty && request.SalesOrderId != salesOrderId)
        {
            throw new InvalidOperationException("Sales Order ID in request body does not match route parameter.");
        }

        var order = await _salesOrderRepository.GetByIdAsync(tenantId, companyId, branchId, salesOrderId)
            ?? throw new KeyNotFoundException("Sales Order not found in authorized branch/company scope.");

        if (order.Status != SalesOrderStatus.Confirmed && order.Status != SalesOrderStatus.Fulfilled)
        {
            throw new InvalidOperationException($"Cannot initiate replenishment requisition for Sales Order in status '{order.Status}'. Order must be Confirmed or Fulfilled.");
        }

        if (request.Items == null || request.Items.Count == 0)
        {
            throw new InvalidOperationException("At least one line item is required to create a purchase requisition.");
        }

        var prItems = new List<Domain.Features.Procurement.PurchaseRequisition.CreatePurchaseRequisitionItemRequest>();
        foreach (var itemReq in request.Items)
        {
            if (itemReq.Quantity <= 0)
            {
                throw new InvalidOperationException("Requisition item quantity must be greater than zero.");
            }

            var product = await _productRepository.GetByIdAsync(tenantId, itemReq.ProductId)
                ?? throw new KeyNotFoundException($"Product '{itemReq.ProductId}' not found.");

            prItems.Add(new Domain.Features.Procurement.PurchaseRequisition.CreatePurchaseRequisitionItemRequest
            {
                ProductId = product.ProductId,
                UnitOfMeasureId = itemReq.UnitOfMeasureId ?? product.UnitOfMeasureId,
                Quantity = itemReq.Quantity,
                EstimatedUnitPrice = itemReq.EstimatedUnitPrice,
                Remarks = itemReq.Remarks ?? $"Replenishment for SO {order.OrderNumber}"
            });
        }

        var prService = _serviceProvider?.GetService<Domain.Features.Procurement.PurchaseRequisition.IPurchaseRequisitionService>();
        string reqNum = $"PR-REPL-{Guid.NewGuid():N}"[..14].ToUpperInvariant();
        Guid requisitionId = Guid.NewGuid();
        string status = "Draft";

        if (prService != null)
        {
            var prCreateRequest = new Domain.Features.Procurement.PurchaseRequisition.CreatePurchaseRequisitionRequest
            {
                RequisitionNumber = reqNum,
                RequisitionDate = DateTime.UtcNow,
                RequiredDate = request.RequiredDate ?? DateTime.UtcNow.AddDays(7),
                Notes = $"Sales Order replenishment for {order.OrderNumber}. {request.Notes}".Trim(),
                Items = prItems
            };

            var created = await prService.CreateRequisitionAsync(
                tenantId,
                companyId,
                branchId,
                prCreateRequest,
                actorUserId,
                actorUserName);

            requisitionId = created.Id;
            reqNum = created.RequisitionNumber;
            status = created.Status.ToString();
        }

        return new ReplenishmentRequisitionResult
        {
            RequisitionId = requisitionId,
            RequisitionNumber = reqNum,
            Status = status,
            ItemCount = prItems.Count,
            Message = $"Draft purchase requisition '{reqNum}' created for Sales Order '{order.OrderNumber}'. Zero inventory added."
        };
    }
}
