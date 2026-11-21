using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domain.Features.MasterData.Currency;
using Domain.Features.MasterData.Product;
using Domain.Features.MasterData.TaxCode;
using Domain.Features.MasterData.UnitOfMeasure;
using Domain.Features.Sales.SalesOrder;
using Domain.Features.Sales.Shipment;
using ERP.Domain.Features.MasterData.Customer;
using Microsoft.Extensions.Logging;

namespace ERP.Application.Features.Sales.SalesOrder;

public class SalesOrderService : ISalesOrderService
{
    private readonly ISalesOrderRepository _repository;
    private readonly ICustomerRepository _customerRepository;
    private readonly IProductRepository _productRepository;
    private readonly IUnitOfMeasureRepository _uomRepository;
    private readonly global::Domain.Features.MasterData.Address.IAddressRepository? _addressRepository;
    private readonly ITaxCodeRepository? _taxCodeRepository;
    private readonly ICurrencyRepository? _currencyRepository;
    private readonly IExchangeRateService? _exchangeRateService;
    private readonly ISalesPersistenceCoordinator? _persistenceCoordinator;
    private readonly ISalesShipmentService? _shipmentService;
    private readonly ISalesShipmentRepository? _shipmentRepository;
    private readonly ILogger<SalesOrderService>? _logger;

    public SalesOrderService(
        ISalesOrderRepository repository,
        ICustomerRepository customerRepository,
        IProductRepository productRepository,
        IUnitOfMeasureRepository uomRepository,
        global::Domain.Features.MasterData.Address.IAddressRepository? addressRepository = null,
        ITaxCodeRepository? taxCodeRepository = null,
        ICurrencyRepository? currencyRepository = null,
        IExchangeRateService? exchangeRateService = null,
        ISalesPersistenceCoordinator? persistenceCoordinator = null,
        ISalesShipmentService? shipmentService = null,
        ISalesShipmentRepository? shipmentRepository = null,
        ILogger<SalesOrderService>? logger = null)
    {
        _repository = repository;
        _customerRepository = customerRepository;
        _productRepository = productRepository;
        _uomRepository = uomRepository;
        _addressRepository = addressRepository;
        _taxCodeRepository = taxCodeRepository;
        _currencyRepository = currencyRepository;
        _exchangeRateService = exchangeRateService;
        _persistenceCoordinator = persistenceCoordinator;
        _shipmentService = shipmentService;
        _shipmentRepository = shipmentRepository;
        _logger = logger;
    }

    public async Task<List<SalesOrderDto>> GetOrdersAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        SalesOrderStatus? status = null)
    {
        var orders = await _repository.GetByBranchAsync(tenantId, companyId, branchId, status);
        Dictionary<Guid, decimal> dispatchedByOrderLine = new();
        if (_shipmentRepository != null)
        {
            var branchShipments = await _shipmentRepository.GetByBranchAsync(tenantId, companyId, branchId);
            dispatchedByOrderLine = branchShipments
                .Where(s => s.Status == SalesShipmentStatus.Dispatched || s.Status == SalesShipmentStatus.Delivered)
                .SelectMany(s => s.Items)
                .Where(i => i.IsActive)
                .GroupBy(i => i.SalesOrderItemId)
                .ToDictionary(g => g.Key, g => g.Sum(i => i.QuantityDispatched));
        }

        return orders.Select(o => MapToDto(o, dispatchedByOrderLine)).ToList();
    }

    public async Task<SalesOrderDto?> GetOrderAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid orderId)
    {
        var order = await _repository.GetByIdAsync(tenantId, companyId, branchId, orderId);
        if (order == null) return null;

        Dictionary<Guid, decimal> dispatchedByOrderLine = new();
        if (_shipmentRepository != null)
        {
            var orderShipments = await _shipmentRepository.GetBySalesOrderIdAsync(tenantId, orderId);
            dispatchedByOrderLine = orderShipments
                .Where(s => s.Status == SalesShipmentStatus.Dispatched || s.Status == SalesShipmentStatus.Delivered)
                .SelectMany(s => s.Items)
                .Where(i => i.IsActive)
                .GroupBy(i => i.SalesOrderItemId)
                .ToDictionary(g => g.Key, g => g.Sum(i => i.QuantityDispatched));
        }

        return MapToDto(order, dispatchedByOrderLine);
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

        Guid? deliveryAddressId = request.DeliveryAddressId;
        string? deliverySnapshot = null;

        if (deliveryAddressId.HasValue && _addressRepository != null)
        {
            var addr = await _addressRepository.GetAddressByIdAsync(tenantId, deliveryAddressId.Value);
            if (addr != null)
            {
                deliverySnapshot = FormatAddressSnapshot(addr.AddressName, addr.AddressLine1, addr.AddressLine2, addr.City, addr.StateProvince, addr.PostalCode);
            }
        }

        if (string.IsNullOrWhiteSpace(deliverySnapshot))
        {
            var custAddresses = await _customerRepository.GetAddressesAsync(tenantId, customer.CustomerId);
            var defaultAssoc = custAddresses.FirstOrDefault(a => a.IsDefault && a.IsActive) ?? custAddresses.FirstOrDefault(a => a.IsActive);
            if (defaultAssoc != null && _addressRepository != null)
            {
                var addr = await _addressRepository.GetAddressByIdAsync(tenantId, defaultAssoc.AddressId);
                if (addr != null)
                {
                    deliveryAddressId = addr.AddressId;
                    deliverySnapshot = FormatAddressSnapshot(addr.AddressName, addr.AddressLine1, addr.AddressLine2, addr.City, addr.StateProvince, addr.PostalCode);
                }
            }

            if (string.IsNullOrWhiteSpace(deliverySnapshot) && !string.IsNullOrWhiteSpace(customer.AddressLine1))
            {
                deliverySnapshot = FormatAddressSnapshot(customer.CustomerName, customer.AddressLine1, customer.AddressLine2, customer.City, null, customer.PostalCode, customer.Country);
            }
        }

        string currencyCode = !string.IsNullOrWhiteSpace(request.Currency)
            ? request.Currency.Trim()
            : (!string.IsNullOrWhiteSpace(customer.CurrencyCode) ? customer.CurrencyCode : "USD");

        var order = global::Domain.Features.Sales.SalesOrder.SalesOrder.Create(
            tenantId,
            companyId,
            branchId,
            customer.CustomerId,
            orderNumber,
            request.DocumentType,
            request.OrderDate,
            request.DeliveryDate,
            currencyCode,
            paymentTerms,
            request.Notes,
            actorUserId,
            request.OpportunityId,
            deliveryAddressId,
            deliverySnapshot);

        if (request.Items != null && request.Items.Count > 0)
        {
            foreach (var itemReq in request.Items)
            {
                var (product, uomId, unitPrice) = await ValidateAndResolveItemAsync(tenantId, itemReq);
                decimal taxRate = await ResolveTaxRateAsync(tenantId, itemReq.TaxCodeId, itemReq.TaxRate, order.OrderDate);

                order.AddItem(
                    product.ProductId,
                    itemReq.Quantity,
                    unitPrice,
                    itemReq.DiscountRate,
                    taxRate,
                    uomId,
                    itemReq.Notes,
                    itemReq.TaxCodeId,
                    itemReq.IsTaxInclusive);
            }
        }

        await ApplyCurrencySnapshotAsync(order, currencyCode, order.OrderDate);

        try
        {
            await _repository.AddAsync(order);
            await _repository.SaveChangesAsync();
            return MapToDto(order);
        }
        catch (Exception ex)
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
        decimal taxRate = await ResolveTaxRateAsync(tenantId, request.TaxCodeId, request.TaxRate, order.OrderDate);

        var addedItem = order.AddItem(
            product.ProductId,
            request.Quantity,
            unitPrice,
            request.DiscountRate,
            taxRate,
            uomId,
            request.Notes,
            request.TaxCodeId,
            request.IsTaxInclusive);

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

        await ValidateCurrencySnapshotAsync(order);
        order.Confirm();
        if (!string.IsNullOrWhiteSpace(remarks))
        {
            order.Notes = string.IsNullOrWhiteSpace(order.Notes)
                ? $"[Confirmed] {remarks}"
                : $"{order.Notes} | [Confirmed] {remarks}";
        }

        await _repository.SaveChangesAsync();

        if (_shipmentService != null)
        {
            try
            {
                var activeOrderItems = order.Items.Where(i => i.IsActive).ToList();
                bool hasPhysicalItems = false;
                foreach (var item in activeOrderItems)
                {
                    var product = await _productRepository.GetByIdAsync(tenantId, item.ProductId);
                    if (product != null && product.ProductType != ProductType.Service)
                    {
                        hasPhysicalItems = true;
                        break;
                    }
                }

                if (hasPhysicalItems)
                {
                    bool shipmentAlreadyExists = false;
                    if (_shipmentRepository != null)
                    {
                        var existingShipments = await _shipmentRepository.GetBySalesOrderIdAsync(tenantId, order.Id);
                        shipmentAlreadyExists = existingShipments != null && existingShipments.Any();
                    }

                    if (!shipmentAlreadyExists)
                    {
                        var shipmentNum = $"SHP-{order.OrderNumber}";
                        if (_shipmentRepository != null && await _shipmentRepository.ExistsShipmentNumberAsync(tenantId, companyId, shipmentNum))
                        {
                            shipmentNum = $"SHP-{order.OrderNumber}-{DateTime.UtcNow:yyyyMMddHHmmss}";
                        }

                        var createReq = new CreateShipmentFromOrderRequest
                        {
                            ShipmentNumber = shipmentNum,
                            DeliveryAddress = order.DeliveryAddressSnapshot,
                            Notes = $"Auto-generated draft dispatch note for confirmed Sales Order {order.OrderNumber}"
                        };

                        await _shipmentService.CreateFromOrderAsync(
                            tenantId,
                            companyId,
                            branchId,
                            order.Id,
                            createReq);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Automatic draft shipment generation for confirmed order {OrderId} failed.", order.Id);
            }
        }

        return MapToDto(order);
    }

    public async Task<SalesOrderDto> RefreshCurrencyAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid orderId,
        RefreshSalesOrderCurrencyRequest request)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        var order = await _repository.GetByIdAsync(tenantId, companyId, branchId, orderId)
            ?? throw new KeyNotFoundException("Sales order was not found in the authorized branch/company scope.");

        if (order.Status != SalesOrderStatus.Draft)
            throw new InvalidOperationException("Only draft sales orders can have their exchange rate refreshed.");

        string currencyCode = order.Currency;
        if (request.TransactionCurrencyId.HasValue && _currencyRepository != null)
        {
            var requestedCurrency = await _currencyRepository.GetCurrencyByIdAsync(tenantId, request.TransactionCurrencyId.Value);
            if (requestedCurrency == null || !requestedCurrency.IsActive)
                throw new InvalidOperationException("The requested transaction currency was not found or is inactive.");
            currencyCode = requestedCurrency.CurrencyCode;
        }

        await ApplyCurrencySnapshotAsync(order, currencyCode, request.OrderDate);
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

    private static SalesOrderDto MapToDto(
        global::Domain.Features.Sales.SalesOrder.SalesOrder order,
        IReadOnlyDictionary<Guid, decimal>? dispatchedByOrderLine = null)
    {
        var itemDtos = new List<SalesOrderItemDto>();
        decimal totalOrdered = 0m;
        decimal totalDispatched = 0m;

        foreach (var item in order.Items)
        {
            decimal dispatched = 0m;
            if (dispatchedByOrderLine != null && dispatchedByOrderLine.TryGetValue(item.Id, out decimal d))
            {
                dispatched = d;
            }

            decimal outstanding = Math.Max(0m, item.Quantity - dispatched);
            if (item.IsActive)
            {
                totalOrdered += item.Quantity;
                totalDispatched += dispatched;
            }

            itemDtos.Add(new SalesOrderItemDto
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
                TaxCodeId = item.TaxCodeId,
                IsTaxInclusive = item.IsTaxInclusive,
                Notes = item.Notes,
                DispatchedQuantity = dispatched,
                OutstandingQuantity = outstanding,
                IsActive = item.IsActive
            });
        }

        decimal totalOutstanding = Math.Max(0m, totalOrdered - totalDispatched);
        string fulfillmentStatus = ComputeFulfillmentStatus(order.Status, totalOrdered, totalDispatched);

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
            TransactionCurrencyId = order.TransactionCurrencyId,
            BaseCurrencyId = order.BaseCurrencyId,
            BaseCurrencyCode = order.BaseCurrencyCode,
            BaseCurrencyDecimalPlaces = order.BaseCurrencyDecimalPlaces,
            AppliedExchangeRate = order.AppliedExchangeRate,
            ExchangeRateSource = order.ExchangeRateSource,
            ExchangeRateId = order.ExchangeRateId,
            ExchangeRateEffectiveDate = order.ExchangeRateEffectiveDate,
            BaseSubTotal = order.BaseSubTotal,
            BaseDiscountAmount = order.BaseDiscountAmount,
            BaseTaxAmount = order.BaseTaxAmount,
            BaseTotalAmount = order.BaseTotalAmount,
            PaymentTerms = order.PaymentTerms,
            Status = order.Status,
            SubTotal = order.SubTotal,
            DiscountAmount = order.DiscountAmount,
            TaxAmount = order.TaxAmount,
            TotalAmount = order.TotalAmount,
            Notes = order.Notes,
            CreatedByUserId = order.CreatedByUserId,
            OpportunityId = order.OpportunityId,
            DeliveryAddressId = order.DeliveryAddressId,
            DeliveryAddressSnapshot = order.DeliveryAddressSnapshot,
            IsActive = order.IsActive,
            TotalOrderedQuantity = totalOrdered,
            TotalDispatchedQuantity = totalDispatched,
            TotalOutstandingQuantity = totalOutstanding,
            FulfillmentStatus = fulfillmentStatus,
            Items = itemDtos
        };
    }

    private static string ComputeFulfillmentStatus(SalesOrderStatus status, decimal totalOrdered, decimal totalDispatched)
    {
        if (status == SalesOrderStatus.Cancelled) return "Cancelled";
        if (status == SalesOrderStatus.Rejected) return "Rejected";
        if (status == SalesOrderStatus.Draft) return "Draft";
        if (status == SalesOrderStatus.Submitted) return "Pending Confirmation";

        if (status == SalesOrderStatus.Fulfilled || (totalOrdered > 0 && totalDispatched >= totalOrdered))
        {
            return "Fulfilled";
        }

        if (totalDispatched > 0)
        {
            return "Partially Dispatched";
        }

        return "Confirmed";
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
            TaxCodeId = item.TaxCodeId,
            IsTaxInclusive = item.IsTaxInclusive,
            Notes = item.Notes,
            IsActive = item.IsActive
        };
    }

    private static string FormatAddressSnapshot(string? name, string? line1, string? line2, string? city, string? state, string? postalCode, string? country = null)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(name)) parts.Add(name.Trim());
        if (!string.IsNullOrWhiteSpace(line1)) parts.Add(line1.Trim());
        if (!string.IsNullOrWhiteSpace(line2)) parts.Add(line2.Trim());
        if (!string.IsNullOrWhiteSpace(city)) parts.Add(city.Trim());
        if (!string.IsNullOrWhiteSpace(state)) parts.Add(state.Trim());
        if (!string.IsNullOrWhiteSpace(postalCode)) parts.Add(postalCode.Trim());
        if (!string.IsNullOrWhiteSpace(country)) parts.Add(country.Trim());
        return string.Join(", ", parts);
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
            if (!taxCode.AppliesTo(TaxApplicability.Sales))
            {
                throw new InvalidOperationException($"Tax code '{taxCode.Code}' cannot be applied to sales transactions.");
            }
            if (!taxCode.IsEffectiveAt(orderDate))
            {
                throw new InvalidOperationException($"Tax code '{taxCode.Code}' is not effective on {orderDate:yyyy-MM-dd}.");
            }
            return taxCode.Rate;
        }

        return explicitTaxRate;
    }

    private async Task<Currency> GetActiveCompanyBaseCurrencyAsync(Guid tenantId, Guid companyId)
    {
        if (_persistenceCoordinator == null)
            throw new InvalidOperationException("Company base currency lookup is unavailable.");

        return await _persistenceCoordinator.GetActiveCompanyBaseCurrencyAsync(tenantId, companyId);
    }

    private async Task ApplyCurrencySnapshotAsync(
        global::Domain.Features.Sales.SalesOrder.SalesOrder order,
        string transactionCurrencyCode,
        DateTime? transactionDate = null)
    {
        if (_currencyRepository == null || _exchangeRateService == null || _persistenceCoordinator == null)
            return;

        var transactionCurrency = await _currencyRepository.GetCurrencyByCodeAsync(order.TenantId, transactionCurrencyCode);
        if (transactionCurrency == null || !transactionCurrency.IsActive)
            throw new InvalidOperationException($"The currency '{transactionCurrencyCode}' is not active for this tenant.");

        var baseCurrency = await GetActiveCompanyBaseCurrencyAsync(order.TenantId, order.CompanyId);
        var effectiveDate = transactionDate ?? order.OrderDate;
        var rate = await _exchangeRateService.ResolveAsync(
            order.TenantId,
            transactionCurrency.CurrencyId,
            baseCurrency.CurrencyId,
            effectiveDate);

        if (transactionDate.HasValue)
            order.ChangeDraftOrderDate(transactionDate.Value);
        order.SetCurrencySnapshot(
            transactionCurrency.CurrencyId,
            transactionCurrency.CurrencyCode,
            baseCurrency.CurrencyId,
            baseCurrency.CurrencyCode,
            baseCurrency.DecimalPlaces,
            rate);
    }

    private async Task ValidateCurrencySnapshotAsync(global::Domain.Features.Sales.SalesOrder.SalesOrder order)
    {
        if (_currencyRepository == null || _exchangeRateService == null || _persistenceCoordinator == null)
            return;

        if (!order.TransactionCurrencyId.HasValue ||
            !order.BaseCurrencyId.HasValue ||
            !order.AppliedExchangeRate.HasValue ||
            !order.ExchangeRateSource.HasValue)
            throw new InvalidOperationException("Sales order currency snapshot is incomplete; refresh the exchange rate while the order is a draft.");

        var transactionCurrency = await _currencyRepository.GetCurrencyByIdAsync(order.TenantId, order.TransactionCurrencyId.Value);
        if (transactionCurrency == null || !transactionCurrency.IsActive)
            throw new InvalidOperationException("The order transaction currency is no longer active; refresh the currency snapshot.");

        var baseCurrency = await GetActiveCompanyBaseCurrencyAsync(order.TenantId, order.CompanyId);
        if (order.BaseCurrencyId.Value != baseCurrency.CurrencyId)
            throw new InvalidOperationException("The company base currency has changed; refresh the currency snapshot before confirming the order.");

        var latestRate = await _exchangeRateService.ResolveAsync(
            order.TenantId,
            transactionCurrency.CurrencyId,
            baseCurrency.CurrencyId,
            order.OrderDate);

        if (order.AppliedExchangeRate.Value != latestRate.Rate)
            throw new InvalidOperationException("The exchange rate for this sales order has changed; refresh the currency snapshot before confirming the order.");
    }
}
