using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Domain.Features.MasterData.Branch;
using Domain.Features.MasterData.Company;
using Domain.Features.MasterData.Currency;
using Domain.Features.MasterData.PriceCard;
using Domain.Features.MasterData.Product;
using Domain.Features.MasterData.TaxCode;
using Domain.Features.MasterData.UnitOfMeasure;
using Domain.Features.Procurement.Approval;
using Domain.Features.Sales.Invoice;
using Domain.Features.Sales.SalesOrder;
using Domain.Features.Sales.SalesQuotation;
using Domain.Features.Sales.Shipment;
using Domain.Infrastructure.Persistence;
using ERP.Application.Features.Sales.CustomerPortal;
using ERP.Application.Features.Sales.DirectOrder;
using ERP.Domain.Features.MasterData.Customer;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ERP.SecurityTests.Sales;

public class SalesBackendCompletionTests
{
    private readonly DomainDbContext _context;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _branchId = Guid.NewGuid();
    private readonly Guid _customerId = Guid.NewGuid();
    private readonly Guid _contactId = Guid.NewGuid();
    private readonly Guid _priceCardId = Guid.NewGuid();
    private readonly Guid _uomId = Guid.NewGuid();
    private readonly Guid _currencyId = Guid.NewGuid();
    private readonly Guid _taxCodeId = Guid.NewGuid();
    private readonly Guid _physicalProductId = Guid.NewGuid();
    private readonly Guid _serviceProductId = Guid.NewGuid();

    public SalesBackendCompletionTests()
    {
        var options = new DbContextOptionsBuilder<DomainDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new DomainDbContext(options);
        SeedData();
    }

    private void SeedData()
    {
        var company = new Company
        {
            Id = _companyId,
            TenantId = _tenantId,
            Name = "Apex Selling Company Ltd",
            Code = "ASC",
            IsActive = true
        };
        _context.Companies.Add(company);

        var branch = new Branch
        {
            Id = _branchId,
            CompanyId = _companyId,
            Name = "Main Branch",
            Code = "MAIN",
            IsActive = true
        };
        _context.Branches.Add(branch);

        var currency = Currency.Create(_tenantId, "USD", "US Dollar", "$", 2, currencyId: _currencyId);
        _context.Currencies.Add(currency);

        var uom = UnitOfMeasure.Create(_tenantId, "PCS", "Pieces", unitOfMeasureId: _uomId);
        _context.UnitsOfMeasure.Add(uom);

        var taxCode = TaxCode.Create(_tenantId, "VAT10", "10% Standard Tax", 10m, TaxApplicability.Sales, taxCodeId: _taxCodeId);
        _context.TaxCodes.Add(taxCode);

        var categoryId = Guid.NewGuid();
        var physicalProduct = Product.Create(
            _tenantId,
            "VALVE-100",
            "Industrial Valve X-100",
            categoryId,
            "PCS",
            productType: ProductType.StockItem,
            productId: _physicalProductId);
        _context.Products.Add(physicalProduct);

        var serviceProduct = Product.Create(
            _tenantId,
            "SRV-CONSULT",
            "Consulting Service",
            categoryId,
            "PCS",
            isStockTracked: false,
            productType: ProductType.Service,
            productId: _serviceProductId);
        _context.Products.Add(serviceProduct);

        var priceCard = PriceCard.Create(
            _tenantId,
            "PC-CORP-2026",
            "Corporate B2B Price Card",
            PriceCardType.Sales,
            "USD",
            DateTime.UtcNow.AddDays(-10),
            DateTime.UtcNow.AddDays(365),
            priceCardId: _priceCardId);
        _context.PriceCards.Add(priceCard);

        var cardLine = PriceCardLine.Create(
            _tenantId,
            _priceCardId,
            _physicalProductId,
            150.00m,
            _uomId,
            minimumQuantity: 5m,
            effectiveFrom: DateTime.UtcNow.AddDays(-10),
            effectiveTo: DateTime.UtcNow.AddDays(365));
        _context.PriceCardLines.Add(cardLine);

        var customer = Customer.Create(
            _tenantId,
            "CUST-001",
            "Global Manufacturing Corp",
            contactPerson: "Procurement Manager",
            customerId: _customerId,
            defaultSalesPriceCardId: _priceCardId);
        _context.Customers.Add(customer);

        var contactTypeId = Guid.NewGuid();
        var contact = CustomerContact.Create(
            _tenantId,
            _customerId,
            _contactId,
            contactTypeId,
            isPrimary: true);
        _context.CustomerContacts.Add(contact);

        _context.SaveChanges();
    }

    [Fact]
    public async Task DirectB2BOrder_ValidRequest_CreatesDraftSalesOrder_WithContractPricesAndTaxes()
    {
        var fakeOrderService = new FakeDirectOrderSalesOrderService(_context);
        var directOrderService = new DirectCustomerOrderService(_context, fakeOrderService);

        var request = new CreateDirectB2BOrderRequest
        {
            SellingCompanyId = _companyId,
            DeliveryAddress = "Warehouse Dock 4, Sector 7",
            Items = new List<DirectB2BOrderLineRequest>
            {
                new()
                {
                    ProductId = _physicalProductId,
                    Quantity = 10m
                }
            }
        };

        var order = await directOrderService.CreateDirectB2BOrderAsync(
            _tenantId,
            _customerId,
            _contactId,
            request);

        Assert.NotNull(order);
        Assert.Equal(SalesOrderStatus.Draft, order.Status);
        Assert.Equal(_customerId, order.CustomerId);
        Assert.Equal(_companyId, order.CompanyId);
        Assert.Single(order.Items);

        var item = order.Items.First();
        Assert.Equal(_physicalProductId, item.ProductId);
        Assert.Equal(10m, item.Quantity);
        Assert.Equal(150.00m, item.UnitPrice);
        Assert.Equal(1650.00m, item.LineTotal); // 1500 subtotal + 10% tax (150)
        Assert.Contains("Direct B2B Order", order.Notes);
    }

    [Fact]
    public async Task DirectB2BOrder_BelowMoq_ThrowsInvalidOperationException()
    {
        var fakeOrderService = new FakeDirectOrderSalesOrderService(_context);
        var directOrderService = new DirectCustomerOrderService(_context, fakeOrderService);

        var request = new CreateDirectB2BOrderRequest
        {
            SellingCompanyId = _companyId,
            Items = new List<DirectB2BOrderLineRequest>
            {
                new()
                {
                    ProductId = _physicalProductId,
                    Quantity = 2m // MOQ is 5m
                }
            }
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            directOrderService.CreateDirectB2BOrderAsync(_tenantId, _customerId, _contactId, request));

        Assert.Contains("minimum order quantity", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SalesQuotation_DomainTransitions_MarkSent_Accept_Decline()
    {
        var quote = SalesQuotation.Create(
            _tenantId,
            _companyId,
            _branchId,
            _customerId,
            "SQ-TRANS-01",
            DateTime.UtcNow.AddDays(15));
        quote.AddItem(_physicalProductId, 10m, 140.00m);

        // Cannot mark Sent from Submitted
        quote.SubmitForApproval(Guid.NewGuid());
        var exSubmit = Assert.Throws<InvalidOperationException>(() => quote.MarkSent());
        Assert.Contains("Only Confirmed", exSubmit.Message);

        quote.Approve(Guid.NewGuid());
        Assert.Equal(SalesQuotationStatus.Confirmed, quote.Status);

        // Mark Sent
        quote.MarkSent();
        Assert.Equal(SalesQuotationStatus.Sent, quote.Status);
        Assert.NotNull(quote.SentAtUtc);

        // Customer decline test
        var quoteDecline = SalesQuotation.Create(_tenantId, _companyId, _branchId, _customerId, "SQ-DEC-01", DateTime.UtcNow.AddDays(10));
        quoteDecline.AddItem(_physicalProductId, 5m, 140m);
        quoteDecline.SubmitForApproval(Guid.NewGuid());
        quoteDecline.Approve(Guid.NewGuid());
        quoteDecline.MarkSent();

        quoteDecline.Decline(_contactId, "Price exceeds project budget.");
        Assert.Equal(SalesQuotationStatus.DeclinedByCustomer, quoteDecline.Status);
        Assert.Equal("Price exceeds project budget.", quoteDecline.DeclinedRemarks);
        Assert.Equal(_contactId, quoteDecline.CustomerContactId);
    }

    [Fact]
    public void SalesQuotation_AcceptQuotation_Expired_ThrowsInvalidOperationException()
    {
        var expiredQuote = SalesQuotation.Create(
            _tenantId,
            _companyId,
            _branchId,
            _customerId,
            "SQ-EXP-01",
            DateTime.UtcNow.AddDays(5));
        expiredQuote.AddItem(_physicalProductId, 5m, 140m);
        expiredQuote.SubmitForApproval(Guid.NewGuid());
        expiredQuote.Approve(Guid.NewGuid());
        expiredQuote.MarkSent();

        // Expire the proposal
        expiredQuote.ValidUntilDateUtc = DateTime.UtcNow.AddDays(-1);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            expiredQuote.Accept(_contactId));
        Assert.Contains("expired", ex.Message);
    }

    [Fact]
    public async Task CustomerOrderPortal_GetOrders_FiltersByCustomerContext()
    {
        var portalService = new CustomerOrderPortalService(_context);

        // Create an order for our customer
        var order = global::Domain.Features.Sales.SalesOrder.SalesOrder.Create(
            _tenantId,
            _companyId,
            _branchId,
            _customerId,
            "SO-CUST-100");
        order.AddItem(_physicalProductId, 5m, 150m, unitOfMeasureId: _uomId);
        _context.SalesOrders.Add(order);

        // Create an order for a different customer
        var otherCustomerId = Guid.NewGuid();
        var otherOrder = global::Domain.Features.Sales.SalesOrder.SalesOrder.Create(
            _tenantId,
            _companyId,
            _branchId,
            otherCustomerId,
            "SO-OTHER-200");
        otherOrder.AddItem(_physicalProductId, 10m, 150m, unitOfMeasureId: _uomId);
        _context.SalesOrders.Add(otherOrder);

        await _context.SaveChangesAsync();

        var orders = await portalService.GetOrdersAsync(_tenantId, _customerId);

        Assert.Single(orders);
        Assert.Equal("SO-CUST-100", orders.First().OrderNumber);
        Assert.Equal(750.00m, orders.First().TotalAmount);
    }

    [Fact]
    public async Task CustomerOrderPortal_GetOrderTracking_Calculates8StagesAccurately()
    {
        var portalService = new CustomerOrderPortalService(_context);

        var order = global::Domain.Features.Sales.SalesOrder.SalesOrder.Create(
            _tenantId,
            _companyId,
            _branchId,
            _customerId,
            "SO-TRACK-01");
        order.AddItem(_physicalProductId, 5m, 150m, unitOfMeasureId: _uomId);
        order.Confirm();
        _context.SalesOrders.Add(order);

        // Add dispatched shipment
        var shipment = SalesShipment.Create(
            _tenantId,
            _companyId,
            _branchId,
            order.Id,
            _customerId,
            "SHP-TRACK-01",
            carrierName: "Express Logistics",
            trackingNumber: "TRK-987654");
        shipment.Status = SalesShipmentStatus.Dispatched;
        shipment.DispatchedAt = DateTime.UtcNow;
        _context.SalesShipments.Add(shipment);

        await _context.SaveChangesAsync();

        var tracking = await portalService.GetOrderTrackingAsync(_tenantId, _customerId, order.Id);

        Assert.NotNull(tracking);
        Assert.Equal("Dispatched", tracking.CurrentStage);
        Assert.Equal(6, tracking.CurrentStageIndex);
        Assert.Equal("Express Logistics", tracking.CarrierName);
        Assert.Equal("TRK-987654", tracking.TrackingNumber);
        Assert.Equal(8, tracking.Stages.Count);

        // Stages 1 to 6 should be completed
        for (int i = 0; i < 6; i++)
        {
            Assert.True(tracking.Stages[i].IsCompleted);
        }
        // Stage 7 (Delivered) is not yet completed
        Assert.False(tracking.Stages[6].IsCompleted);
        // Stage 8 (Cancelled) is not completed
        Assert.False(tracking.Stages[7].IsCompleted);
    }

    [Fact]
    public async Task CustomerOrderPortal_GetOrderById_ScrubsInternalNotesAndMargins()
    {
        var portalService = new CustomerOrderPortalService(_context);

        var order = global::Domain.Features.Sales.SalesOrder.SalesOrder.Create(
            _tenantId,
            _companyId,
            _branchId,
            _customerId,
            "SO-SCRUB-01",
            notes: "[INTERNAL ONLY] Cost: $80, Margin: 45%. Do not disclose to customer.",
            createdByUserId: Guid.NewGuid());
        order.AddItem(_physicalProductId, 5m, 150m, unitOfMeasureId: _uomId);
        _context.SalesOrders.Add(order);
        await _context.SaveChangesAsync();

        var detail = await portalService.GetOrderByIdAsync(_tenantId, _customerId, order.Id);

        Assert.NotNull(detail);
        Assert.Equal("SO-SCRUB-01", detail.OrderNumber);
        // Sensitive employee ID, internal margin, and internal cost fields are NOT exposed on CustomerOrderDetailDto
        Assert.Single(detail.Items);
        Assert.Equal(150m, detail.Items[0].UnitPrice);
        Assert.Equal("Industrial Valve X-100", detail.Items[0].ProductName);
    }

    [Fact]
    public async Task CustomerOrderPortal_GetDashboard_AggregatesCountsAndAlerts()
    {
        var portalService = new CustomerOrderPortalService(_context);

        // 1 active order
        var order = global::Domain.Features.Sales.SalesOrder.SalesOrder.Create(_tenantId, _companyId, _branchId, _customerId, "SO-DASH-01");
        _context.SalesOrders.Add(order);

        // 1 pending quotation awaiting action (status = Sent)
        var quote = SalesQuotation.Create(_tenantId, _companyId, _branchId, _customerId, "SQ-DASH-01", DateTime.UtcNow.AddDays(7));
        quote.AddItem(_physicalProductId, 5m, 140m);
        quote.SubmitForApproval(Guid.NewGuid());
        quote.Approve(Guid.NewGuid());
        quote.MarkSent();
        _context.SalesQuotations.Add(quote);

        await _context.SaveChangesAsync();

        var dashboard = await portalService.GetDashboardAsync(_tenantId, _customerId);

        Assert.NotNull(dashboard);
        Assert.True(dashboard.TotalOrdersCount >= 1);
        Assert.True(dashboard.ActiveOrdersCount >= 1);
        Assert.Equal(1, dashboard.PendingQuotesCount);
        Assert.Contains(dashboard.Alerts, a => a.Contains("awaiting your review"));
    }

    [Fact]
    public async Task SalesOrder_Confirm_CreatesDraftDispatchNote_ForPhysicalItems()
    {
        var orderRepo = new FakeOrderRepo(_context);
        var productRepo = new FakeProdRepo(_context);
        var recordingShipmentService = new FakeRecordingShipmentService();

        var orderService = new ERP.Application.Features.Sales.SalesOrder.SalesOrderService(
            orderRepo,
            null!,
            productRepo,
            null!,
            shipmentService: recordingShipmentService);

        var order = global::Domain.Features.Sales.SalesOrder.SalesOrder.Create(
            _tenantId,
            _companyId,
            _branchId,
            _customerId,
            "SO-SHIP-01");
        order.AddItem(_physicalProductId, 5m, 150m, unitOfMeasureId: _uomId);
        _context.SalesOrders.Add(order);
        await _context.SaveChangesAsync();

        await orderService.ConfirmOrderAsync(
            _tenantId,
            _companyId,
            _branchId,
            order.Id,
            Guid.NewGuid(),
            "admin");

        Assert.Equal(SalesOrderStatus.Confirmed, order.Status);
        Assert.Single(recordingShipmentService.CreatedShipmentRequests);
        Assert.Contains("SHP-SO-SHIP-01", recordingShipmentService.CreatedShipmentRequests[0].ShipmentNumber);
    }
}

// Minimal in-memory Fake SalesOrderService for testing DirectCustomerOrderService
internal class FakeDirectOrderSalesOrderService : ISalesOrderService
{
    private readonly DomainDbContext _dbContext;

    public FakeDirectOrderSalesOrderService(DomainDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<SalesOrderDto> CreateOrderAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        CreateSalesOrderRequest request,
        Guid? actorUserId = null,
        string? actorUserName = null)
    {
        var order = global::Domain.Features.Sales.SalesOrder.SalesOrder.Create(
            tenantId,
            companyId,
            branchId,
            request.CustomerId,
            request.OrderNumber,
            request.DocumentType,
            request.OrderDate,
            request.DeliveryDate,
            request.Currency,
            request.PaymentTerms,
            request.Notes,
            actorUserId);

        order.DeliveryAddressId = request.DeliveryAddressId;

        foreach (var line in request.Items)
        {
            order.AddItem(
                line.ProductId,
                line.Quantity,
                line.UnitPrice ?? 0m,
                line.DiscountRate,
                line.TaxRate,
                line.UnitOfMeasureId,
                line.Notes,
                line.TaxCodeId,
                line.IsTaxInclusive);
        }

        _dbContext.SalesOrders.Add(order);
        await _dbContext.SaveChangesAsync();

        return new SalesOrderDto
        {
            Id = order.Id,
            TenantId = order.TenantId,
            CompanyId = order.CompanyId,
            BranchId = order.BranchId,
            CustomerId = order.CustomerId,
            OrderNumber = order.OrderNumber,
            DocumentType = order.DocumentType,
            Status = order.Status,
            Currency = order.Currency,
            Notes = order.Notes,
            Items = order.Items.Select(i => new SalesOrderItemDto
            {
                Id = i.Id,
                ProductId = i.ProductId,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                LineTotal = i.LineTotal
            }).ToList()
        };
    }

    public Task<List<SalesOrderDto>> GetOrdersAsync(Guid tenantId, Guid companyId, Guid branchId, SalesOrderStatus? status = null) => throw new NotImplementedException();
    public Task<SalesOrderDto?> GetOrderAsync(Guid tenantId, Guid companyId, Guid branchId, Guid orderId) => throw new NotImplementedException();
    public Task<SalesOrderItemDto> AddItemAsync(Guid tenantId, Guid companyId, Guid branchId, Guid orderId, CreateSalesOrderItemRequest request) => throw new NotImplementedException();
    public Task<SalesOrderDto> SubmitOrderAsync(Guid tenantId, Guid companyId, Guid branchId, Guid orderId, Guid actorUserId, string actorUserName) => throw new NotImplementedException();
    public Task<SalesOrderDto> ConfirmOrderAsync(Guid tenantId, Guid companyId, Guid branchId, Guid orderId, Guid actorUserId, string actorUserName, string? remarks = null) => throw new NotImplementedException();
    public Task<SalesOrderDto> RefreshCurrencyAsync(Guid tenantId, Guid companyId, Guid branchId, Guid orderId, RefreshSalesOrderCurrencyRequest request) => throw new NotImplementedException();
    public Task<SalesOrderDto> RejectOrderAsync(Guid tenantId, Guid companyId, Guid branchId, Guid orderId, Guid actorUserId, string actorUserName, string remarks) => throw new NotImplementedException();
    public Task<SalesOrderDto> CancelOrderAsync(Guid tenantId, Guid companyId, Guid branchId, Guid orderId, Guid actorUserId, string actorUserName, string? remarks = null) => throw new NotImplementedException();
}

internal class FakeOrderRepo : ISalesOrderRepository
{
    private readonly DomainDbContext _context;
    public FakeOrderRepo(DomainDbContext context) => _context = context;

    public Task<global::Domain.Features.Sales.SalesOrder.SalesOrder?> GetByIdAsync(Guid tenantId, Guid companyId, Guid branchId, Guid orderId)
    {
        return _context.SalesOrders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.TenantId == tenantId && o.CompanyId == companyId && o.BranchId == branchId && o.Id == orderId);
    }

    public Task<List<global::Domain.Features.Sales.SalesOrder.SalesOrder>> GetByBranchAsync(Guid tenantId, Guid companyId, Guid branchId, SalesOrderStatus? status = null) => throw new NotImplementedException();
    public Task<bool> ExistsOrderNumberAsync(Guid tenantId, Guid companyId, string orderNumber) => throw new NotImplementedException();
    public Task AddAsync(global::Domain.Features.Sales.SalesOrder.SalesOrder order) => throw new NotImplementedException();
    public Task AddItemAsync(SalesOrderItem item) => throw new NotImplementedException();
    public Task SaveChangesAsync() => _context.SaveChangesAsync();
}

internal class FakeProdRepo : IProductRepository
{
    private readonly DomainDbContext _context;
    public FakeProdRepo(DomainDbContext context) => _context = context;

    public Task<Product?> GetByIdAsync(Guid tenantId, Guid productId)
    {
        return _context.Products.FirstOrDefaultAsync(p => p.TenantId == tenantId && p.ProductId == productId);
    }

    public Task<List<Product>> GetByTenantAsync(Guid tenantId, Guid? brandId = null) => throw new NotImplementedException();
    public Task<bool> ExistsByCodeAsync(Guid tenantId, string productCode) => throw new NotImplementedException();
    public Task<bool> ExistsByNameAsync(Guid tenantId, string productName) => throw new NotImplementedException();
    public Task<bool> CategoryExistsAsync(Guid tenantId, Guid categoryId) => throw new NotImplementedException();
    public Task<bool> BrandExistsAsync(Guid tenantId, Guid brandId) => throw new NotImplementedException();
    public Task AddAsync(Product product) => throw new NotImplementedException();
    public Task<decimal> GetTotalOnHandQuantityAsync(Guid tenantId, Guid productId) => throw new NotImplementedException();
    public Task SaveChangesAsync() => _context.SaveChangesAsync();
}

internal class FakeRecordingShipmentService : ISalesShipmentService
{
    public List<CreateShipmentFromOrderRequest> CreatedShipmentRequests { get; } = new();

    public Task<SalesShipmentDto> CreateFromOrderAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid salesOrderId,
        CreateShipmentFromOrderRequest request)
    {
        CreatedShipmentRequests.Add(request);
        return Task.FromResult(new SalesShipmentDto
        {
            Id = Guid.NewGuid(),
            ShipmentNumber = request.ShipmentNumber,
            Status = SalesShipmentStatus.Draft
        });
    }

    public Task<List<SalesShipmentDto>> GetShipmentsAsync(Guid tenantId, Guid companyId, Guid? branchId = null, SalesShipmentStatus? status = null) => throw new NotImplementedException();
    public Task<SalesShipmentDto?> GetShipmentByIdAsync(Guid tenantId, Guid companyId, Guid? branchId, Guid shipmentId) => throw new NotImplementedException();
    public Task<SalesOrderDispatchAvailabilityDto> GetOrderDispatchAvailabilityAsync(Guid tenantId, Guid companyId, Guid branchId, Guid salesOrderId, Guid? targetLocationId = null) => throw new NotImplementedException();
    public Task<SalesShipmentDto> CreateAndDispatchFromOrderAsync(Guid tenantId, Guid companyId, Guid branchId, Guid salesOrderId, DispatchOrderRequest request) => throw new NotImplementedException();
    public Task<SalesShipmentDto> CreateManualDraftAsync(Guid tenantId, Guid companyId, Guid branchId, CreateSalesShipmentRequest request) => throw new NotImplementedException();
    public Task<SalesShipmentDto> UpdateDraftAsync(Guid tenantId, Guid companyId, Guid branchId, Guid shipmentId, UpdateSalesShipmentRequest request) => throw new NotImplementedException();
    public Task<SalesShipmentDto> DispatchShipmentAsync(Guid tenantId, Guid companyId, Guid branchId, Guid shipmentId) => throw new NotImplementedException();
    public Task<SalesShipmentDto> DeliverShipmentAsync(Guid tenantId, Guid companyId, Guid branchId, Guid shipmentId, DeliverShipmentRequest? request = null) => throw new NotImplementedException();
    public Task<SalesShipmentDto> CancelDraftAsync(Guid tenantId, Guid companyId, Guid branchId, Guid shipmentId) => throw new NotImplementedException();
    public Task<ReplenishmentTransferResult> CreateReplenishmentTransferDraftAsync(Guid tenantId, Guid companyId, Guid branchId, Guid salesOrderId, CreateReplenishmentTransferRequest request) => throw new NotImplementedException();
    public Task<ReplenishmentRequisitionResult> CreateReplenishmentPurchaseRequisitionAsync(Guid tenantId, Guid companyId, Guid branchId, Guid salesOrderId, CreateReplenishmentRequisitionRequest request, Guid actorUserId, string actorUserName) => throw new NotImplementedException();
}
