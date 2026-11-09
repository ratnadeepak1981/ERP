using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domain.Features.MasterData.Product;
using Domain.Features.Sales.Invoice;
using Domain.Features.Sales.SalesOrder;
using Domain.Features.Sales.Shipment;
using ERP.Domain.Features.Inventory;
using ERP.Domain.Features.MasterData.Warehouse;
using global::Domain.Features.MasterData.UnitOfMeasure;

namespace ERP.Application.Features.Sales.Shipment;

public class SalesShipmentService : ISalesShipmentService
{
    private readonly ISalesShipmentRepository _shipmentRepository;
    private readonly ISalesOrderRepository _salesOrderRepository;
    private readonly IProductRepository _productRepository;
    private readonly IWarehouseRepository _warehouseRepository;
    private readonly IInventoryPostingService _postingService;
    private readonly IInventoryRepository _inventoryRepository;
    private readonly ISalesPersistenceCoordinator _persistenceCoordinator;
    private readonly ISalesInvoiceService? _invoiceService;
    private readonly IUnitOfMeasureService? _uomService;

    public SalesShipmentService(
        ISalesShipmentRepository shipmentRepository,
        ISalesOrderRepository salesOrderRepository,
        IProductRepository productRepository,
        IWarehouseRepository warehouseRepository,
        IInventoryPostingService postingService,
        ISalesPersistenceCoordinator persistenceCoordinator,
        IInventoryRepository inventoryRepository,
        ISalesInvoiceService? invoiceService = null,
        IUnitOfMeasureService? uomService = null)
    {
        _shipmentRepository = shipmentRepository ?? throw new ArgumentNullException(nameof(shipmentRepository));
        _salesOrderRepository = salesOrderRepository ?? throw new ArgumentNullException(nameof(salesOrderRepository));
        _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
        _warehouseRepository = warehouseRepository ?? throw new ArgumentNullException(nameof(warehouseRepository));
        _postingService = postingService ?? throw new ArgumentNullException(nameof(postingService));
        _persistenceCoordinator = persistenceCoordinator ?? throw new ArgumentNullException(nameof(persistenceCoordinator));
        _inventoryRepository = inventoryRepository ?? throw new ArgumentNullException(nameof(inventoryRepository));
        _invoiceService = invoiceService;
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

        var existingShipments = await _shipmentRepository.GetBySalesOrderIdAsync(tenantId, salesOrderId);
        var dispatchedQtyByLine = existingShipments
            .Where(s => s.Status == SalesShipmentStatus.Dispatched || s.Status == SalesShipmentStatus.Delivered)
            .SelectMany(s => s.Items)
            .Where(i => i.IsActive)
            .GroupBy(i => i.SalesOrderItemId)
            .ToDictionary(g => g.Key, g => g.Sum(i => i.QuantityDispatched));

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
            _persistenceCoordinator.RemoveShipmentItems(existingItems);

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
            await EnsureInvoiceCreatedAsync(tenantId, companyId, branchId, existingShipment);
            var order = await _salesOrderRepository.GetByIdAsync(tenantId, companyId, branchId, existingShipment.SalesOrderId);
            return MapToDto(existingShipment, order?.OrderNumber ?? string.Empty);
        }

        if (existingShipment.Status == SalesShipmentStatus.Draft)
        {
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

        Func<Task<SalesShipmentDto>> executionAction = async () =>
        {
            var domainTx = await _persistenceCoordinator.BeginTransactionIfNeededAsync();
            try
            {
                await _persistenceCoordinator.AcquireSalesOrderUpdateLockAsync(tenantId, existingShipment.SalesOrderId);

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

                var otherShipments = await _shipmentRepository.GetBySalesOrderIdAsync(tenantId, shipment.SalesOrderId);
                var committedDispatchedQty = otherShipments
                    .Where(s => s.Id != shipment.Id && (s.Status == SalesShipmentStatus.Dispatched || s.Status == SalesShipmentStatus.Delivered))
                    .SelectMany(s => s.Items)
                    .Where(i => i.IsActive)
                    .GroupBy(i => i.SalesOrderItemId)
                    .ToDictionary(g => g.Key, g => g.Sum(i => i.QuantityDispatched));

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

                if (targetStatus == SalesShipmentStatus.Delivered)
                {
                    shipment.MarkDelivered(deliveredAt, recipientName);
                }
                else
                {
                    shipment.MarkDispatched();
                }

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

                if (allPhysicalFulfilled && !hasServiceLines)
                {
                    order.Status = SalesOrderStatus.Fulfilled;
                }

                await _salesOrderRepository.SaveChangesAsync();
                await _shipmentRepository.SaveChangesAsync();
                await _persistenceCoordinator.CommitTransactionIfNeededAsync(domainTx);

                if (targetStatus == SalesShipmentStatus.Delivered)
                {
                    await EnsureInvoiceCreatedAsync(tenantId, companyId, branchId, shipment);
                }

                return MapToDto(shipment, order.OrderNumber);
            }
            catch
            {
                await _persistenceCoordinator.RollbackTransactionIfNeededAsync(domainTx);
                throw;
            }
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
            if (_invoiceService != null)
            {
                var inv = await _invoiceService.CreateDraftFromShipmentAsync(tenantId, companyId, branchId, shipment.Id);
                shipment.InvoiceId = inv.Id;
                await _shipmentRepository.SaveChangesAsync();
            }
        }
        catch
        {
            // Resilient boundary: stock deduction was already safely posted and committed.
            // Failure in auto-invoice draft creation does not invalidate stock ledger.
        }
    }

    private IUnitOfMeasureService? GetUomService()
    {
        return _uomService;
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
            ConversionContext.Sales,
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
        var dispatchedQtyByLine = existingShipments
            .Where(s => s.Status == SalesShipmentStatus.Dispatched || s.Status == SalesShipmentStatus.Delivered)
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

        var warehouses = await _warehouseRepository.GetWarehousesAsync(tenantId);
        var activeWarehouses = warehouses.Where(w => w.IsActive)
            .OrderByDescending(w => w.BranchId == order.BranchId)
            .ThenByDescending(w => w.CompanyId == order.CompanyId)
            .ThenByDescending(w => w.CreatedAt)
            .ToList();

        var locTypes = (await _warehouseRepository.GetLocationTypesAsync(tenantId)).ToDictionary(t => t.LocationTypeId);
        var allActiveLocations = new List<(WarehouseLocation Loc, Warehouse Wh)>();
        foreach (var wh in activeWarehouses)
        {
            var locs = await _warehouseRepository.GetLocationsAsync(tenantId, wh.WarehouseId);
            foreach (var loc in locs.Where(l => l.IsActive && (!l.LocationTypeId.HasValue || (locTypes.TryGetValue(l.LocationTypeId.Value, out var lt) && lt.CanPick))).OrderBy(l => l.CreatedAt))
            {
                allActiveLocations.Add((loc, wh));
            }
        }

        var defaultLocTuple = allActiveLocations.FirstOrDefault();
        (WarehouseLocation Loc, Warehouse Wh) selectedLocTuple;

        if (targetLocationId.HasValue && targetLocationId.Value != Guid.Empty)
        {
            selectedLocTuple = allActiveLocations.FirstOrDefault(t => t.Loc.WarehouseLocationId == targetLocationId.Value);
            if (selectedLocTuple.Loc == null)
            {
                var targetLoc = await _warehouseRepository.GetLocationByIdAsync(tenantId, targetLocationId.Value);
                if (targetLoc != null && targetLoc.LocationTypeId.HasValue && locTypes.TryGetValue(targetLoc.LocationTypeId.Value, out var targetLt) && !targetLt.CanPick)
                {
                    throw new InvalidOperationException($"Specified target location '{targetLoc.LocationCode}' does not allow picking for dispatch.");
                }

                throw new InvalidOperationException("Specified target location not found or is inactive.");
            }
        }
        else
        {
            selectedLocTuple = defaultLocTuple;
        }

        var lineAvailabilities = new List<SalesOrderDispatchLineAvailabilityDto>();
        foreach (var (item, product, remaining) in remainingLines)
        {
            dispatchedQtyByLine.TryGetValue(item.Id, out decimal alreadyDispatched);
            decimal available = 0m;
            if (selectedLocTuple.Loc != null)
            {
                if (product.IsStockTracked)
                {
                    bool isBatch = product.TrackingMode == TrackingMode.Batch || product.TrackingMode == TrackingMode.BatchAndSerial;
                    available = await _postingService.GetTotalAvailableStockAsync(tenantId, selectedLocTuple.Loc.WarehouseLocationId, product.ProductId, isBatch);
                }
                else
                {
                    available = remaining;
                }
            }

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
                        ConversionContext.Sales,
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
                AvailableQuantity = displayAvailable,
                ShortageQuantity = shortage,
                CanFulfil = canFulfil,
                UnitOfMeasureId = item.UnitOfMeasureId
            });
        }

        bool canSelectedFulfil = lineAvailabilities.Count > 0 && lineAvailabilities.All(l => l.CanFulfil);

        var eligibleLocations = new List<EligibleLocationDto>();
        bool canDefaultFulfil = false;

        foreach (var (loc, wh) in allActiveLocations)
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
                eligibleLocations.Add(new EligibleLocationDto
                {
                    WarehouseId = wh.WarehouseId,
                    WarehouseName = wh.WarehouseName,
                    WarehouseCode = wh.WarehouseCode,
                    LocationId = loc.WarehouseLocationId,
                    LocationCode = loc.LocationCode,
                    LocationName = loc.LocationName,
                    IsDefault = (defaultLocTuple.Loc != null && loc.WarehouseLocationId == defaultLocTuple.Loc.WarehouseLocationId)
                });
            }
        }

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
            EligibleLocations = eligibleLocations,
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

        var domainTx = await _persistenceCoordinator.BeginTransactionIfNeededAsync();
        try
        {
            await _persistenceCoordinator.AcquireSalesOrderUpdateLockAsync(tenantId, salesOrderId);

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

            shipment.MarkDispatched();

            await _shipmentRepository.AddAsync(shipment);
            await _shipmentRepository.SaveChangesAsync();

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
            }

            await _salesOrderRepository.SaveChangesAsync();
            await _persistenceCoordinator.CommitTransactionIfNeededAsync(domainTx);

            await EnsureInvoiceCreatedAsync(tenantId, companyId, branchId, shipment);

            return MapToDto(shipment, order.OrderNumber);
        }
        catch
        {
            await _persistenceCoordinator.RollbackTransactionIfNeededAsync(domainTx);
            throw;
        }
    }
}
