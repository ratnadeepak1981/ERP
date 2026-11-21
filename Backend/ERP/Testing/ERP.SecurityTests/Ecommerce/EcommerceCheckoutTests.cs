using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domain.Features.Ecommerce;
using Domain.Features.MasterData.Branch;
using Domain.Features.MasterData.Company;
using Domain.Features.MasterData.Currency;
using Domain.Features.MasterData.Product;
using Domain.Features.Sales.SalesOrder;
using Domain.Infrastructure.Persistence;
using ERP.Application.Features.Ecommerce.Cart;
using ERP.Domain.Features.MasterData.Category;
using ERP.Domain.Features.MasterData.Customer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using AddressEntity = Domain.Features.MasterData.Address.Address;

namespace ERP.SecurityTests.Ecommerce;

public class EcommerceCheckoutTests
{
    private readonly DomainDbContext _context;
    private readonly FakePricingResolutionService _pricingService;
    private readonly FakeSalesOrderService _salesOrderService;
    private readonly EcommerceCartService _cartService;

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _branchId = Guid.NewGuid();
    private readonly Guid _categoryId = Guid.NewGuid();
    private readonly Guid _currencyId = Guid.NewGuid();
    private readonly Guid _walkinCustomerId = Guid.NewGuid();

    public EcommerceCheckoutTests()
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

        var branch = new Branch
        {
            Id = _branchId,
            CompanyId = _companyId,
            Code = "MAIN",
            Name = "Main Branch",
            IsActive = true
        };
        _context.Branches.Add(branch);

        var category = Category.Create(_tenantId, "CAT-01", "General", categoryId: _categoryId);
        _context.Categories.Add(category);

        var walkinCustomer = Customer.Create(_tenantId, "CUST-WALKIN", "Walk-In Customer", customerId: _walkinCustomerId);
        _context.Customers.Add(walkinCustomer);

        _context.SaveChanges();
    }

    private Product CreateProduct(string code, string name, decimal sellingPrice)
    {
        var p = Product.Create(
            _tenantId,
            code,
            name,
            _categoryId,
            unitOfMeasure: "PCS",
            sellingPrice: sellingPrice,
            isSellable: true);

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
    public async Task Checkout_ActiveCartWithItems_CreatesDraftSalesOrderAndMarksCartCheckedOut()
    {
        // Arrange
        var product = CreateProduct("PROD-CHK-01", "Wireless Router", sellingPrice: 75.00m);
        PublishProduct(product.ProductId, isPublished: true);

        string sessionId = "chk-session-01";
        await _cartService.AddToCartAsync(_tenantId, _companyId, new AddToCartRequest
        {
            ProductId = product.ProductId,
            Quantity = 2,
            SessionId = sessionId
        }, customerId: null);

        var checkoutRequest = new CheckoutRequest
        {
            SessionId = sessionId,
            IdempotencyKey = "idemp-001",
            CustomerNotes = "Please deliver before 5pm."
        };

        // Act
        var result = await _cartService.CheckoutAsync(
            _tenantId, _companyId, checkoutRequest, customerId: null);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotEqual(Guid.Empty, result.SalesOrderId);
        Assert.StartsWith("SO-WEB-", result.OrderNumber);
        Assert.Equal(SalesOrderStatus.Draft, result.OrderStatus);
        Assert.Equal(150.00m, result.TotalAmount);
        Assert.Equal("USD", result.Currency);

        // Verify order created in Sales Order service
        Assert.Single(_salesOrderService.CreatedOrders);
        var createdOrder = _salesOrderService.CreatedOrders[0];
        Assert.Equal(_walkinCustomerId, createdOrder.CustomerId);
        Assert.Single(createdOrder.Items);
        Assert.Equal(product.ProductId, createdOrder.Items[0].ProductId);
        Assert.Equal(2, createdOrder.Items[0].Quantity);
        Assert.Equal(75.00m, createdOrder.Items[0].UnitPrice);

        // Verify cart status in database
        var cartInDb = await _context.EcommerceCarts.FindAsync(result.CartId);
        Assert.NotNull(cartInDb);
        Assert.Equal(EcommerceCartStatus.CheckedOut, cartInDb.Status);
        Assert.Equal(result.SalesOrderId, cartInDb.SalesOrderId);
        Assert.Equal("idemp-001", cartInDb.IdempotencyKey);
    }

    [Fact]
    public async Task Checkout_IdempotencyKey_RepeatedCallReturnsExistingOrderWithoutDuplicate()
    {
        // Arrange
        var product = CreateProduct("PROD-CHK-02", "Bluetooth Speaker", sellingPrice: 60.00m);
        PublishProduct(product.ProductId, isPublished: true);

        string sessionId = "chk-session-02";
        await _cartService.AddToCartAsync(_tenantId, _companyId, new AddToCartRequest
        {
            ProductId = product.ProductId,
            Quantity = 1,
            SessionId = sessionId
        }, customerId: null);

        var request = new CheckoutRequest
        {
            SessionId = sessionId,
            IdempotencyKey = "idemp-duplicate-test",
            CustomerNotes = "Repeat check"
        };

        // Act 1: Initial checkout
        var result1 = await _cartService.CheckoutAsync(_tenantId, _companyId, request, customerId: null);

        // Act 2: Duplicate submission with identical idempotency key
        var result2 = await _cartService.CheckoutAsync(_tenantId, _companyId, request, customerId: null);

        // Assert
        Assert.True(result2.Success);
        Assert.Equal(result1.SalesOrderId, result2.SalesOrderId);
        Assert.Equal(result1.OrderNumber, result2.OrderNumber);
        Assert.Contains("idempotent", result2.Message, StringComparison.OrdinalIgnoreCase);

        // Ensure only ONE order was ever created in SalesOrderService
        Assert.Single(_salesOrderService.CreatedOrders);
    }

    [Fact]
    public async Task Checkout_EmptyCart_ThrowsInvalidOperationException()
    {
        // Arrange
        var request = new CheckoutRequest
        {
            SessionId = "empty-session-cart",
            IdempotencyKey = "idemp-empty"
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _cartService.CheckoutAsync(_tenantId, _companyId, request, customerId: null));

        Assert.Contains("empty", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Checkout_UnpublishedProductInCart_ThrowsInvalidOperationException()
    {
        // Arrange
        var product = CreateProduct("PROD-CHK-04", "Discontinued Item", sellingPrice: 50.00m);
        var publication = PublishProduct(product.ProductId, isPublished: true);

        string sessionId = "chk-session-04";
        await _cartService.AddToCartAsync(_tenantId, _companyId, new AddToCartRequest
        {
            ProductId = product.ProductId,
            Quantity = 1,
            SessionId = sessionId
        }, customerId: null);

        // Later, product was unpublished by admin before user completed checkout
        publication.Unpublish();
        _context.SaveChanges();

        var request = new CheckoutRequest
        {
            SessionId = sessionId,
            IdempotencyKey = "idemp-004"
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _cartService.CheckoutAsync(_tenantId, _companyId, request, customerId: null));

        Assert.Contains("no longer published", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Checkout_AuthenticatedCustomer_PreservesCustomerAndDeliveryAddress()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var customer = Customer.Create(_tenantId, "CUST-PORTAL", "Enterprise Customer", customerId: customerId);
        _context.Customers.Add(customer);

        var addressId = Guid.NewGuid();
        var address = AddressEntity.Create(
            _tenantId,
            addressName: "HQ Delivery",
            addressLine1: "100 Tech Blvd",
            city: "Metropolis",
            addressLine2: null,
            stateProvince: "NY",
            postalCode: "10001",
            addressId: addressId);
        _context.Addresses.Add(address);
        _context.SaveChanges();

        var product = CreateProduct("PROD-CHK-05", "Ergonomic Chair", sellingPrice: 350.00m);
        PublishProduct(product.ProductId, isPublished: true);

        string sessionId = "chk-session-05";
        await _cartService.AddToCartAsync(_tenantId, _companyId, new AddToCartRequest
        {
            ProductId = product.ProductId,
            Quantity = 1,
            SessionId = sessionId
        }, customerId: customerId);

        var request = new CheckoutRequest
        {
            SessionId = sessionId,
            CustomerId = customerId,
            DeliveryAddressId = addressId,
            IdempotencyKey = "idemp-b2b-01",
            CustomerNotes = "Deliver to dock 4"
        };

        // Act
        var result = await _cartService.CheckoutAsync(
            _tenantId, _companyId, request, customerId: customerId, actorUserId: Guid.NewGuid(), actorUserName: "b2b-admin");

        // Assert
        Assert.True(result.Success);
        Assert.Single(_salesOrderService.CreatedOrders);
        var createdOrder = _salesOrderService.CreatedOrders[0];
        Assert.Equal(customerId, createdOrder.CustomerId);
        Assert.Equal(addressId, createdOrder.DeliveryAddressId);
        Assert.Equal(350.00m, createdOrder.Items[0].UnitPrice);
        Assert.Contains("Deliver to dock 4", createdOrder.Notes);
    }
}
