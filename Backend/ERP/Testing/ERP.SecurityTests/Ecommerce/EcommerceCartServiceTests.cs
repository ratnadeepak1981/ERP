using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Domain.Features.Ecommerce;
using Domain.Features.MasterData.Company;
using Domain.Features.MasterData.Currency;
using Domain.Features.MasterData.PriceCard;
using Domain.Features.MasterData.Product;
using Domain.Features.Sales.SalesOrder;
using Domain.Infrastructure.Persistence;
using ERP.Application.Features.Ecommerce.Cart;
using ERP.Domain.Features.MasterData.Category;
using ERP.Domain.Features.MasterData.Customer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ERP.SecurityTests.Ecommerce;

public class EcommerceCartServiceTests
{
    private readonly DomainDbContext _context;
    private readonly FakePricingResolutionService _pricingService;
    private readonly FakeSalesOrderService _salesOrderService;
    private readonly EcommerceCartService _cartService;

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _categoryId = Guid.NewGuid();
    private readonly Guid _currencyId = Guid.NewGuid();

    public EcommerceCartServiceTests()
    {
        var options = new DbContextOptionsBuilder<DomainDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new DomainDbContext(options);
        _pricingService = new FakePricingResolutionService();
        _salesOrderService = new FakeSalesOrderService(_context);
        _cartService = new EcommerceCartService(
            _context,
            _pricingService,
            _salesOrderService,
            NullLogger<EcommerceCartService>.Instance);

        SeedInitialData();
    }

    private void SeedInitialData()
    {
        var currency = Currency.Create(_tenantId, "USD", "US Dollar", "$", 2, currencyId: _currencyId);
        _context.Currencies.Add(currency);

        var company = new Company
        {
            Id = _companyId,
            TenantId = _tenantId,
            Code = "CMP-01",
            Name = "Apex Selling Co",
            IsActive = true,
            BaseCurrencyId = _currencyId,
            BaseCurrency = currency
        };
        _context.Companies.Add(company);

        var category = Category.Create(_tenantId, "CAT-01", "General", categoryId: _categoryId);
        _context.Categories.Add(category);

        _context.SaveChanges();
    }

    private Product CreateProduct(string code, string name, decimal sellingPrice, bool isSellable = true, bool isActive = true)
    {
        var p = Product.Create(
            _tenantId,
            code,
            name,
            _categoryId,
            unitOfMeasure: "PCS",
            sellingPrice: sellingPrice,
            isSellable: isSellable);

        if (!isActive)
        {
            p.Deactivate();
        }

        _context.Products.Add(p);
        _context.SaveChanges();
        return p;
    }

    private EcommerceProduct PublishProduct(Guid productId, bool isPublished = true)
    {
        var pub = EcommerceProduct.Create(_tenantId, _companyId, productId, isPublished: isPublished);
        _context.EcommerceProducts.Add(pub);
        _context.SaveChanges();
        return pub;
    }

    [Fact]
    public async Task AddToCart_PublishedProduct_CreatesCartAndCalculatesServerPrice()
    {
        // Arrange
        var product = CreateProduct("PROD-01", "Wireless Mouse", sellingPrice: 25.00m);
        PublishProduct(product.ProductId, isPublished: true);

        var request = new AddToCartRequest
        {
            ProductId = product.ProductId,
            Quantity = 2,
            SessionId = "session-101"
        };

        // Act
        var cart = await _cartService.AddToCartAsync(_tenantId, _companyId, request, customerId: null);

        // Assert
        Assert.NotNull(cart);
        Assert.Equal(1, cart.TotalItems);
        Assert.Single(cart.Lines);
        var line = cart.Lines[0];
        Assert.Equal(product.ProductId, line.ProductId);
        Assert.Equal(2, line.Quantity);
        Assert.Equal(25.00m, line.UnitPrice);
        Assert.Equal(50.00m, line.SubTotal);
        Assert.Equal(50.00m, cart.GrandTotal);
    }

    [Fact]
    public async Task AddToCart_UnpublishedProduct_ThrowsInvalidOperationException()
    {
        // Arrange
        var product = CreateProduct("PROD-02", "Secret Gadget", sellingPrice: 99.00m);
        PublishProduct(product.ProductId, isPublished: false); // Unpublished

        var request = new AddToCartRequest
        {
            ProductId = product.ProductId,
            Quantity = 1,
            SessionId = "session-102"
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _cartService.AddToCartAsync(_tenantId, _companyId, request, customerId: null));

        Assert.Contains("not published for sale", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AddToCart_NonSellableProduct_ThrowsInvalidOperationException()
    {
        // Arrange
        var product = CreateProduct("PROD-03", "Internal Raw Material", sellingPrice: 10.00m, isSellable: false);
        PublishProduct(product.ProductId, isPublished: true);

        var request = new AddToCartRequest
        {
            ProductId = product.ProductId,
            Quantity = 1,
            SessionId = "session-103"
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _cartService.AddToCartAsync(_tenantId, _companyId, request, customerId: null));

        Assert.Contains("not configured as sellable", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AddToCart_ExistingItem_IncrementsQuantity()
    {
        // Arrange
        var product = CreateProduct("PROD-04", "Keyboard", sellingPrice: 45.00m);
        PublishProduct(product.ProductId, isPublished: true);

        var req1 = new AddToCartRequest { ProductId = product.ProductId, Quantity = 2, SessionId = "session-104" };
        var req2 = new AddToCartRequest { ProductId = product.ProductId, Quantity = 3, SessionId = "session-104" };

        // Act
        await _cartService.AddToCartAsync(_tenantId, _companyId, req1, customerId: null);
        var cart = await _cartService.AddToCartAsync(_tenantId, _companyId, req2, customerId: null);

        // Assert
        Assert.Single(cart.Lines);
        Assert.Equal(5, cart.Lines[0].Quantity);
        Assert.Equal(225.00m, cart.GrandTotal);
    }

    [Fact]
    public async Task UpdateLineQuantity_ValidQuantity_UpdatesSubtotal()
    {
        // Arrange
        var product = CreateProduct("PROD-05", "Monitor", sellingPrice: 200.00m);
        PublishProduct(product.ProductId, isPublished: true);

        var addReq = new AddToCartRequest { ProductId = product.ProductId, Quantity = 1, SessionId = "session-105" };
        var cart = await _cartService.AddToCartAsync(_tenantId, _companyId, addReq, customerId: null);
        var lineId = cart.Lines[0].LineId;

        // Act
        var updatedCart = await _cartService.UpdateLineQuantityAsync(
            _tenantId, _companyId, lineId, new UpdateCartLineRequest { Quantity = 3, SessionId = "session-105" }, customerId: null);

        // Assert
        Assert.Single(updatedCart.Lines);
        Assert.Equal(3, updatedCart.Lines[0].Quantity);
        Assert.Equal(600.00m, updatedCart.GrandTotal);
    }

    [Fact]
    public async Task UpdateLineQuantity_ZeroQuantity_RemovesLine()
    {
        // Arrange
        var product = CreateProduct("PROD-06", "Webcam", sellingPrice: 50.00m);
        PublishProduct(product.ProductId, isPublished: true);

        var addReq = new AddToCartRequest { ProductId = product.ProductId, Quantity = 2, SessionId = "session-106" };
        var cart = await _cartService.AddToCartAsync(_tenantId, _companyId, addReq, customerId: null);
        var lineId = cart.Lines[0].LineId;

        // Act
        var updatedCart = await _cartService.UpdateLineQuantityAsync(
            _tenantId, _companyId, lineId, new UpdateCartLineRequest { Quantity = 0, SessionId = "session-106" }, customerId: null);

        // Assert
        Assert.Empty(updatedCart.Lines);
        Assert.Equal(0, updatedCart.TotalItems);
        Assert.Equal(0m, updatedCart.GrandTotal);
    }

    [Fact]
    public async Task RemoveLine_RemovesTargetLineFromCart()
    {
        // Arrange
        var p1 = CreateProduct("PROD-07A", "Headset", sellingPrice: 30.00m);
        var p2 = CreateProduct("PROD-07B", "Mousepad", sellingPrice: 10.00m);
        PublishProduct(p1.ProductId, isPublished: true);
        PublishProduct(p2.ProductId, isPublished: true);

        await _cartService.AddToCartAsync(_tenantId, _companyId, new AddToCartRequest { ProductId = p1.ProductId, Quantity = 1, SessionId = "session-107" }, null);
        var cart = await _cartService.AddToCartAsync(_tenantId, _companyId, new AddToCartRequest { ProductId = p2.ProductId, Quantity = 1, SessionId = "session-107" }, null);

        var p1LineId = cart.Lines.First(l => l.ProductId == p1.ProductId).LineId;

        // Act
        var updatedCart = await _cartService.RemoveLineAsync(_tenantId, _companyId, p1LineId, sessionId: "session-107", customerId: null);

        // Assert
        Assert.Single(updatedCart.Lines);
        Assert.Equal(p2.ProductId, updatedCart.Lines[0].ProductId);
        Assert.Equal(10.00m, updatedCart.GrandTotal);
    }

    [Fact]
    public async Task ClearCart_RemovesAllItems()
    {
        // Arrange
        var p1 = CreateProduct("PROD-08A", "USB Cable", sellingPrice: 5.00m);
        PublishProduct(p1.ProductId, isPublished: true);

        await _cartService.AddToCartAsync(_tenantId, _companyId, new AddToCartRequest { ProductId = p1.ProductId, Quantity = 4, SessionId = "session-108" }, null);

        // Act
        var cart = await _cartService.ClearCartAsync(_tenantId, _companyId, sessionId: "session-108", customerId: null);

        // Assert
        Assert.Empty(cart.Lines);
        Assert.Equal(0, cart.TotalItems);
        Assert.Equal(0m, cart.GrandTotal);
    }

    [Fact]
    public async Task DynamicPricing_ReflectsUpdatedPriceImmediatelyWithoutMutatingCartStorage()
    {
        // Arrange
        var product = CreateProduct("PROD-09", "Dynamic Priced SSD", sellingPrice: 100.00m);
        PublishProduct(product.ProductId, isPublished: true);

        await _cartService.AddToCartAsync(_tenantId, _companyId, new AddToCartRequest { ProductId = product.ProductId, Quantity = 2, SessionId = "session-109" }, null);

        // Override price in pricing service
        _pricingService.SetCustomPrice(product.ProductId, 120.00m);

        // Act - Fetch cart
        var cart = await _cartService.GetCartAsync(_tenantId, _companyId, customerId: null, sessionId: "session-109");

        // Assert: price is dynamically evaluated from server pricing service
        Assert.Equal(120.00m, cart.Lines[0].UnitPrice);
        Assert.Equal(240.00m, cart.GrandTotal);
    }

    [Fact]
    public async Task CustomerLogin_AssociatesAnonymousSessionCartWithCustomer()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var customer = Customer.Create(_tenantId, "CUST-B2B", "Acme Corp", customerId: customerId);
        _context.Customers.Add(customer);
        _context.SaveChanges();

        var product = CreateProduct("PROD-10", "Desk Lamp", sellingPrice: 40.00m);
        PublishProduct(product.ProductId, isPublished: true);

        // Guest adds to cart
        await _cartService.AddToCartAsync(_tenantId, _companyId, new AddToCartRequest { ProductId = product.ProductId, Quantity = 1, SessionId = "session-110" }, customerId: null);

        // Act: User logs in, now accessing cart with customerId and sessionId
        var cart = await _cartService.GetCartAsync(_tenantId, _companyId, customerId: customerId, sessionId: "session-110");

        // Assert
        Assert.Equal(customerId, cart.CustomerId);
        Assert.Equal("Acme Corp", cart.CustomerName);
        Assert.Single(cart.Lines);
    }

    [Fact]
    public async Task GetCart_CorporateCustomerMissingPriceCardItem_MarksLineUnavailableAndUnitPriceZero()
    {
        // Arrange: Corporate customer with contracted Price Card, but product is not on it
        var customerId = Guid.NewGuid();
        var customer = Customer.Create(_tenantId, "CUST-CORP-CART", "Mega Corp", customerId: customerId);
        var priceCardId = Guid.NewGuid();
        customer.SetDefaultSalesPriceCard(priceCardId);
        _context.Customers.Add(customer);

        var product = CreateProduct("PROD-CORP-NO-PRICE", "Specialty Cable", sellingPrice: 45.00m);
        PublishProduct(product.ProductId, isPublished: true);

        // Add to cart with no price in FakePricingResolutionService
        await _cartService.AddToCartAsync(_tenantId, _companyId, new AddToCartRequest
        {
            ProductId = product.ProductId,
            Quantity = 2,
            SessionId = "session-corp-noprice"
        }, customerId: customerId);

        // Act
        var cart = await _cartService.GetCartAsync(_tenantId, _companyId, customerId: customerId, sessionId: "session-corp-noprice");

        // Assert: Line is unavailable, unit price is 0, and clear validation message returned without falling back to 45.00m
        Assert.Single(cart.Lines);
        var line = cart.Lines[0];
        Assert.False(line.IsAvailable);
        Assert.Equal(0m, line.UnitPrice);
        Assert.Equal(0m, line.LineTotal);
        Assert.NotNull(line.ValidationMessage);
        Assert.Contains("Price Card", line.ValidationMessage);
    }

    [Fact]
    public async Task GetCart_RetailCustomer_UsesSellingPriceAndMarksLineAvailable()
    {
        // Arrange: Retail individual customer (CUST-IND)
        var customerId = Guid.NewGuid();
        var customer = Customer.Create(_tenantId, "CUST-IND-CART", "Jane Doe", customerId: customerId);
        _context.Customers.Add(customer);

        var product = CreateProduct("PROD-RETAIL-01", "Coffee Maker", sellingPrice: 89.99m);
        PublishProduct(product.ProductId, isPublished: true);

        await _cartService.AddToCartAsync(_tenantId, _companyId, new AddToCartRequest
        {
            ProductId = product.ProductId,
            Quantity = 1,
            SessionId = "session-retail-cart"
        }, customerId: customerId);

        // Act
        var cart = await _cartService.GetCartAsync(_tenantId, _companyId, customerId: customerId, sessionId: "session-retail-cart");

        // Assert: Line is available and uses Product.SellingPrice
        Assert.Single(cart.Lines);
        var line = cart.Lines[0];
        Assert.True(line.IsAvailable);
        Assert.Equal(89.99m, line.UnitPrice);
        Assert.Null(line.ValidationMessage);
    }
}

public class FakePricingResolutionService : IPricingResolutionService
{
    private readonly Dictionary<Guid, decimal> _customPrices = new();

    public void SetCustomPrice(Guid productId, decimal price)
    {
        _customPrices[productId] = price;
    }

    public Task<PriceResolutionResult> ResolvePurchasePriceAsync(Guid tenantId, Guid? supplierId, Guid productId, Guid? requestedUomId, DateTime? transactionDate = null, Guid? overridePriceCardId = null, string? expectedCurrency = null)
    {
        return Task.FromResult(new PriceResolutionResult { Found = false, ProductId = productId });
    }

    public Task<PriceResolutionResult> ResolveSalesPriceAsync(Guid tenantId, Guid customerId, Guid productId, Guid? requestedUomId, DateTime? transactionDate = null, Guid? overridePriceCardId = null)
    {
        if (_customPrices.TryGetValue(productId, out var price))
        {
            return Task.FromResult(new PriceResolutionResult
            {
                Found = true,
                ProductId = productId,
                UnitPrice = price,
                Currency = "USD",
                Source = "PriceCard"
            });
        }

        return Task.FromResult(new PriceResolutionResult { Found = false, ProductId = productId });
    }
}

public class FakeSalesOrderService : ISalesOrderService
{
    private readonly DomainDbContext? _context;
    public List<CreateSalesOrderRequest> CreatedOrders { get; } = new();

    public FakeSalesOrderService(DomainDbContext? context = null)
    {
        _context = context;
    }

    public Task<SalesOrderDto> CreateOrderAsync(Guid tenantId, Guid companyId, Guid branchId, CreateSalesOrderRequest request, Guid? actorUserId = null, string? actorUserName = null)
    {
        CreatedOrders.Add(request);

        decimal subTotal = request.Items.Sum(i => i.Quantity * (i.UnitPrice ?? 0m));
        decimal tax = request.Items.Sum(i => Math.Round(i.Quantity * (i.UnitPrice ?? 0m) * (i.TaxRate / 100m), 2));

        var order = SalesOrder.Create(
            tenantId,
            companyId,
            branchId,
            request.CustomerId,
            request.OrderNumber,
            request.DocumentType,
            request.OrderDate,
            request.DeliveryDate,
            request.Currency ?? "USD",
            notes: request.Notes);

        if (_context != null)
        {
            _context.SalesOrders.Add(order);
            _context.SaveChanges();
        }

        var dto = new SalesOrderDto
        {
            Id = order.Id,
            TenantId = tenantId,
            CompanyId = companyId,
            BranchId = branchId,
            CustomerId = request.CustomerId,
            OrderNumber = request.OrderNumber,
            Status = SalesOrderStatus.Draft,
            OrderDate = request.OrderDate ?? DateTime.UtcNow,
            DeliveryDate = request.DeliveryDate,
            Currency = request.Currency ?? "USD",
            SubTotal = subTotal,
            TaxAmount = tax,
            TotalAmount = subTotal + tax,
            Notes = request.Notes
        };

        return Task.FromResult(dto);
    }

    public Task<List<SalesOrderDto>> GetOrdersAsync(Guid tenantId, Guid companyId, Guid branchId, SalesOrderStatus? status = null) => Task.FromResult(new List<SalesOrderDto>());
    public Task<SalesOrderDto?> GetOrderAsync(Guid tenantId, Guid companyId, Guid branchId, Guid orderId) => Task.FromResult<SalesOrderDto?>(null);
    public Task<SalesOrderItemDto> AddItemAsync(Guid tenantId, Guid companyId, Guid branchId, Guid orderId, CreateSalesOrderItemRequest request) => throw new NotImplementedException();
    public Task<SalesOrderDto> SubmitOrderAsync(Guid tenantId, Guid companyId, Guid branchId, Guid orderId, Guid actorUserId, string actorUserName) => throw new NotImplementedException();
    public Task<SalesOrderDto> ConfirmOrderAsync(Guid tenantId, Guid companyId, Guid branchId, Guid orderId, Guid actorUserId, string actorUserName, string? remarks = null) => throw new NotImplementedException();
    public Task<SalesOrderDto> RejectOrderAsync(Guid tenantId, Guid companyId, Guid branchId, Guid orderId, Guid actorUserId, string actorUserName, string remarks) => throw new NotImplementedException();
    public Task<SalesOrderDto> RefreshCurrencyAsync(Guid tenantId, Guid companyId, Guid branchId, Guid orderId, RefreshSalesOrderCurrencyRequest request) => throw new NotImplementedException();
    public Task<SalesOrderDto> CancelOrderAsync(Guid tenantId, Guid companyId, Guid branchId, Guid orderId, Guid actorUserId, string actorUserName, string? remarks = null) => throw new NotImplementedException();
}
