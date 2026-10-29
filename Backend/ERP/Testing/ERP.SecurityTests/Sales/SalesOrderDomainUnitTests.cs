using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Domain.Features.MasterData.Product;
using Domain.Features.MasterData.UnitOfMeasure;
using Domain.Features.Sales.SalesOrder;
using ERP.Domain.Features.MasterData.Category;
using ERP.Domain.Features.MasterData.Customer;
using Xunit;

namespace ERP.SecurityTests.Sales;

public class SalesOrderDomainUnitTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _branchId = Guid.NewGuid();
    private readonly Guid _customerId = Guid.NewGuid();
    private readonly Guid _productId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    [Fact]
    public void SalesOrder_Create_InitializesWithCorrectDefaults()
    {
        var order = SalesOrder.Create(
            _tenantId,
            _companyId,
            _branchId,
            _customerId,
            "SO-001",
            SalesOrderType.Standard,
            notes: "Initial test order",
            createdByUserId: _userId);

        Assert.Equal(_tenantId, order.TenantId);
        Assert.Equal(_companyId, order.CompanyId);
        Assert.Equal(_branchId, order.BranchId);
        Assert.Equal(_customerId, order.CustomerId);
        Assert.Equal("SO-001", order.OrderNumber);
        Assert.Equal(SalesOrderType.Standard, order.DocumentType);
        Assert.Equal(SalesOrderStatus.Draft, order.Status);
        Assert.Equal("USD", order.Currency);
        Assert.Equal(0m, order.SubTotal);
        Assert.Equal(0m, order.DiscountAmount);
        Assert.Equal(0m, order.TaxAmount);
        Assert.Equal(0m, order.TotalAmount);
        Assert.True(order.IsActive);
        Assert.Empty(order.Items);
    }

    [Fact]
    public void SalesOrder_AddItem_CalculatesAwayFromZeroRoundingAndTotals()
    {
        var order = SalesOrder.Create(_tenantId, _companyId, _branchId, _customerId, "SO-CALC");

        // Item 1: Quantity: 3.33333 -> 3.3333, UnitPrice: 10.555 -> 10.56, Discount: 10%, Tax: 5%
        // SubTotal: 3.3333 * 10.56 = 35.199648 -> 35.20
        // DiscountAmount: 35.20 * 0.10 = 3.52
        // Taxable: 35.20 - 3.52 = 31.68
        // TaxAmount: 31.68 * 0.05 = 1.584 -> 1.58
        // LineTotal: 31.68 + 1.58 = 33.26
        order.AddItem(_productId, 3.33333m, 10.555m, discountRate: 10m, taxRate: 5m);

        Assert.Single(order.Items);
        var item = order.Items[0];
        Assert.Equal(3.3333m, item.Quantity);
        Assert.Equal(10.56m, item.UnitPrice);
        Assert.Equal(35.20m, item.SubTotal);
        Assert.Equal(3.52m, item.DiscountAmount);
        Assert.Equal(1.58m, item.TaxAmount);
        Assert.Equal(33.26m, item.LineTotal);

        Assert.Equal(35.20m, order.SubTotal);
        Assert.Equal(3.52m, order.DiscountAmount);
        Assert.Equal(1.58m, order.TaxAmount);
        Assert.Equal(33.26m, order.TotalAmount);
    }

    [Fact]
    public void SalesOrder_AddItem_WithInvalidInputs_ThrowsArgumentException()
    {
        var order = SalesOrder.Create(_tenantId, _companyId, _branchId, _customerId, "SO-INVALID");

        // Non-positive quantity
        Assert.Throws<ArgumentException>(() => order.AddItem(_productId, 0, 10m));
        Assert.Throws<ArgumentException>(() => order.AddItem(_productId, -5, 10m));

        // Negative unit price
        Assert.Throws<ArgumentException>(() => order.AddItem(_productId, 2, -1m));

        // Invalid discount rate (< 0 or > 100)
        Assert.Throws<ArgumentException>(() => order.AddItem(_productId, 2, 10m, discountRate: -1m));
        Assert.Throws<ArgumentException>(() => order.AddItem(_productId, 2, 10m, discountRate: 101m));

        // Negative tax rate
        Assert.Throws<ArgumentException>(() => order.AddItem(_productId, 2, 10m, taxRate: -5m));
    }

    [Fact]
    public void SalesOrder_LifecycleTransitions_EnforceValidRules()
    {
        var order = SalesOrder.Create(_tenantId, _companyId, _branchId, _customerId, "SO-LIFECYCLE");

        // Cannot submit with no items
        Assert.Throws<InvalidOperationException>(() => order.Submit());

        order.AddItem(_productId, 2, 50m);
        Assert.Equal(SalesOrderStatus.Draft, order.Status);

        // Submit succeeds
        order.Submit();
        Assert.Equal(SalesOrderStatus.Submitted, order.Status);

        // Cannot modify items when submitted
        Assert.Throws<InvalidOperationException>(() => order.AddItem(_productId, 1, 10m));

        // Confirm from submitted succeeds
        order.Confirm();
        Assert.Equal(SalesOrderStatus.Confirmed, order.Status);

        // Cannot modify items when confirmed
        Assert.Throws<InvalidOperationException>(() => order.AddItem(_productId, 1, 10m));

        // Cancel order from confirmed succeeds
        order.Cancel();
        Assert.Equal(SalesOrderStatus.Cancelled, order.Status);

        // Already cancelled cannot cancel again
        Assert.Throws<InvalidOperationException>(() => order.Cancel());
    }

    [Fact]
    public void SalesOrder_DirectConfirm_FromDraft_Succeeds()
    {
        var order = SalesOrder.Create(_tenantId, _companyId, _branchId, _customerId, "SO-DIRECT-CONFIRM");
        order.AddItem(_productId, 1, 100m);

        order.Confirm();
        Assert.Equal(SalesOrderStatus.Confirmed, order.Status);
    }

    [Fact]
    public void SalesOrder_Reject_TransitionsFromSubmittedToRejected()
    {
        var order = SalesOrder.Create(_tenantId, _companyId, _branchId, _customerId, "SO-REJECT");
        order.AddItem(_productId, 1, 100m);
        order.Submit();

        order.Reject();
        Assert.Equal(SalesOrderStatus.Rejected, order.Status);

        // Can cancel a rejected order
        order.Cancel();
        Assert.Equal(SalesOrderStatus.Cancelled, order.Status);
    }

    [Fact]
    public async Task SalesOrderService_CreateOrder_ValidatesCustomerAndProduct()
    {
        var stubRepo = new InMemorySalesOrderRepository();
        var stubCustRepo = new InMemoryCustomerRepository();
        var stubProdRepo = new InMemoryProductRepository();
        var stubUomRepo = new InMemoryUomRepository();

        var service = new SalesOrderService(stubRepo, stubCustRepo, stubProdRepo, stubUomRepo);

        // 1. Missing Customer throws InvalidOperationException
        var reqNoCust = new CreateSalesOrderRequest
        {
            OrderNumber = "SO-V1",
            CustomerId = Guid.NewGuid()
        };
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateOrderAsync(_tenantId, _companyId, _branchId, reqNoCust));

        // 2. Inactive Customer throws InvalidOperationException
        var inactiveCust = Customer.Create(_tenantId, "CUST-INACTIVE", "Inactive Customer");
        inactiveCust.Deactivate();
        await stubCustRepo.AddCustomerAsync(inactiveCust);

        var reqInactiveCust = new CreateSalesOrderRequest
        {
            OrderNumber = "SO-V2",
            CustomerId = inactiveCust.CustomerId
        };
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateOrderAsync(_tenantId, _companyId, _branchId, reqInactiveCust));

        // 3. Active customer but non-sellable product throws InvalidOperationException
        var activeCust = Customer.Create(_tenantId, "CUST-ACTIVE", "Active Customer");
        await stubCustRepo.AddCustomerAsync(activeCust);

        var nonSellableProd = Product.Create(
            _tenantId, "PROD-NONSELL", "Non-Sellable Product",
            Guid.NewGuid(), isSellable: false);
        await stubProdRepo.AddAsync(nonSellableProd);

        var reqNonSellableProd = new CreateSalesOrderRequest
        {
            OrderNumber = "SO-V3",
            CustomerId = activeCust.CustomerId,
            Items = new List<CreateSalesOrderItemRequest>
            {
                new() { ProductId = nonSellableProd.ProductId, Quantity = 1, UnitPrice = 50m }
            }
        };
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateOrderAsync(_tenantId, _companyId, _branchId, reqNonSellableProd));
    }

    [Fact]
    public async Task SalesOrderService_FallbackToProductSellingPrice_WhenUnitPriceOmitted()
    {
        var stubRepo = new InMemorySalesOrderRepository();
        var stubCustRepo = new InMemoryCustomerRepository();
        var stubProdRepo = new InMemoryProductRepository();
        var stubUomRepo = new InMemoryUomRepository();

        var service = new SalesOrderService(stubRepo, stubCustRepo, stubProdRepo, stubUomRepo);

        var customer = Customer.Create(_tenantId, "CUST-OK", "Customer OK", paymentTerms: "Net 30");
        await stubCustRepo.AddCustomerAsync(customer);

        var productWithPrice = Product.Create(
            _tenantId, "PROD-PRICED", "Priced Product",
            Guid.NewGuid(), sellingPrice: 75.50m, isSellable: true);
        await stubProdRepo.AddAsync(productWithPrice);

        var req = new CreateSalesOrderRequest
        {
            OrderNumber = "SO-FALLBACK",
            CustomerId = customer.CustomerId,
            Items = new List<CreateSalesOrderItemRequest>
            {
                new()
                {
                    ProductId = productWithPrice.ProductId,
                    Quantity = 2,
                    UnitPrice = null // Omitted -> should resolve to 75.50m
                }
            }
        };

        var orderDto = await service.CreateOrderAsync(_tenantId, _companyId, _branchId, req);

        Assert.NotNull(orderDto);
        Assert.Single(orderDto.Items);
        Assert.Equal(75.50m, orderDto.Items[0].UnitPrice);
        Assert.Equal(151.00m, orderDto.TotalAmount);
        Assert.Equal("Net 30", orderDto.PaymentTerms); // Inherited from customer
    }

    private class InMemorySalesOrderRepository : ISalesOrderRepository
    {
        private readonly List<SalesOrder> _orders = new();

        public Task<List<SalesOrder>> GetByBranchAsync(Guid tenantId, Guid companyId, Guid branchId, SalesOrderStatus? status = null)
        {
            var result = _orders.FindAll(o => o.TenantId == tenantId && o.CompanyId == companyId && o.BranchId == branchId && o.IsActive);
            if (status.HasValue) result = result.FindAll(o => o.Status == status.Value);
            return Task.FromResult(result);
        }

        public Task<SalesOrder?> GetByIdAsync(Guid tenantId, Guid companyId, Guid branchId, Guid orderId)
        {
            return Task.FromResult(_orders.Find(o => o.Id == orderId && o.TenantId == tenantId && o.CompanyId == companyId && o.BranchId == branchId && o.IsActive));
        }

        public Task<bool> ExistsOrderNumberAsync(Guid tenantId, Guid companyId, string orderNumber)
        {
            return Task.FromResult(_orders.Exists(o => o.TenantId == tenantId && o.CompanyId == companyId && o.OrderNumber == orderNumber));
        }

        public Task AddAsync(SalesOrder order)
        {
            _orders.Add(order);
            return Task.CompletedTask;
        }

        public Task AddItemAsync(SalesOrderItem item) => Task.CompletedTask;

        public Task SaveChangesAsync() => Task.CompletedTask;
    }

    private class InMemoryCustomerRepository : ICustomerRepository
    {
        private readonly List<Customer> _customers = new();

        public Task<Customer?> GetCustomerByIdAsync(Guid tenantId, Guid customerId)
        {
            return Task.FromResult(_customers.Find(c => c.TenantId == tenantId && c.CustomerId == customerId));
        }

        public Task<List<Customer>> GetCustomersAsync(Guid tenantId)
        {
            return Task.FromResult(_customers.FindAll(c => c.TenantId == tenantId));
        }

        public Task<bool> ExistsByCodeAsync(Guid tenantId, string code)
        {
            return Task.FromResult(_customers.Exists(c => c.TenantId == tenantId && c.CustomerCode == code));
        }

        public Task<bool> ExistsByNameAsync(Guid tenantId, string name)
        {
            return Task.FromResult(_customers.Exists(c => c.TenantId == tenantId && c.CustomerName == name));
        }

        public Task AddCustomerAsync(Customer customer)
        {
            _customers.Add(customer);
            return Task.CompletedTask;
        }

        public Task<List<CustomerAddress>> GetAddressesAsync(Guid tenantId, Guid customerId) => Task.FromResult(new List<CustomerAddress>());
        public Task<CustomerAddress?> GetAddressAssociationAsync(Guid tenantId, Guid customerId, Guid addressId, Guid addressTypeId) => Task.FromResult<CustomerAddress?>(null);
        public Task<CustomerAddress?> GetAddressAssociationByIdAsync(Guid tenantId, Guid customerAddressId) => Task.FromResult<CustomerAddress?>(null);
        public Task AddAddressAssociationAsync(CustomerAddress association) => Task.CompletedTask;

        public Task<List<CustomerContact>> GetContactsAsync(Guid tenantId, Guid customerId) => Task.FromResult(new List<CustomerContact>());
        public Task<CustomerContact?> GetContactAssociationAsync(Guid tenantId, Guid customerId, Guid contactId, Guid contactTypeId) => Task.FromResult<CustomerContact?>(null);
        public Task<CustomerContact?> GetContactAssociationByIdAsync(Guid tenantId, Guid customerContactId) => Task.FromResult<CustomerContact?>(null);
        public Task AddContactAssociationAsync(CustomerContact association) => Task.CompletedTask;

        public Task SaveChangesAsync() => Task.CompletedTask;
    }

    private class InMemoryProductRepository : IProductRepository
    {
        private readonly List<Product> _products = new();

        public Task<Product?> GetByIdAsync(Guid tenantId, Guid productId)
        {
            return Task.FromResult(_products.Find(p => p.TenantId == tenantId && p.ProductId == productId));
        }

        public Task<List<Product>> GetByTenantAsync(Guid tenantId, Guid? brandId = null) =>
            Task.FromResult(_products.FindAll(p => p.TenantId == tenantId && (!brandId.HasValue || p.BrandId == brandId.Value)));
        public Task<bool> ExistsByCodeAsync(Guid tenantId, string productCode) => Task.FromResult(_products.Exists(p => p.TenantId == tenantId && p.ProductCode == productCode));
        public Task<bool> ExistsByNameAsync(Guid tenantId, string productName) => Task.FromResult(_products.Exists(p => p.TenantId == tenantId && p.ProductName == productName));
        public Task<bool> CategoryExistsAsync(Guid tenantId, Guid categoryId) => Task.FromResult(true);
        public Task<bool> BrandExistsAsync(Guid tenantId, Guid brandId) => Task.FromResult(true);

        public Task AddAsync(Product product)
        {
            _products.Add(product);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync() => Task.CompletedTask;
    }

    private class InMemoryUomRepository : IUnitOfMeasureRepository
    {
        public Task<List<UnitOfMeasure>> GetUnitsAsync(Guid tenantId) => Task.FromResult(new List<UnitOfMeasure>());
        public Task<UnitOfMeasure?> GetUnitByIdAsync(Guid tenantId, Guid unitOfMeasureId) => Task.FromResult<UnitOfMeasure?>(null);
        public Task<UnitOfMeasure?> GetUnitByCodeAsync(Guid tenantId, string code) => Task.FromResult<UnitOfMeasure?>(null);
        public Task<bool> ExistsByCodeAsync(Guid tenantId, string code) => Task.FromResult(false);
        public Task AddUnitAsync(UnitOfMeasure unitOfMeasure) => Task.CompletedTask;
        public Task<List<UnitOfMeasureConversion>> GetConversionsAsync(Guid tenantId) => Task.FromResult(new List<UnitOfMeasureConversion>());
        public Task<UnitOfMeasureConversion?> GetConversionByIdAsync(Guid tenantId, Guid conversionId) => Task.FromResult<UnitOfMeasureConversion?>(null);
        public Task<UnitOfMeasureConversion?> GetConversionAsync(Guid tenantId, Guid fromUnitId, Guid toUnitId) => Task.FromResult<UnitOfMeasureConversion?>(null);
        public Task AddConversionAsync(UnitOfMeasureConversion conversion) => Task.CompletedTask;
        public Task SaveChangesAsync() => Task.CompletedTask;
    }
}
