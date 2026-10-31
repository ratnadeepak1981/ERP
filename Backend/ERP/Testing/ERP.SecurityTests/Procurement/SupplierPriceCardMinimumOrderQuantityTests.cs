using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domain.Features.MasterData.Product;
using Domain.Features.MasterData.Supplier;
using Domain.Features.MasterData.UnitOfMeasure;
using Domain.Features.Procurement.Approval;
using Domain.Features.Procurement.PurchaseOrder;
using ERP.Domain.Features.MasterData.Supplier;
using Xunit;

namespace ERP.SecurityTests.Procurement;

public class SupplierPriceCardMinimumOrderQuantityTests
{
    [Theory]
    [InlineData(5, true)]
    [InlineData(8, true)]
    [InlineData(4, false)]
    public async Task CreateOrder_EnforcesMinimumOrderQuantity(decimal quantity, bool isAllowed)
    {
        var fixture = new Fixture(minimumOrderQuantity: 5);
        var request = fixture.CreateRequest(quantity, unitPrice: null);

        if (isAllowed)
        {
            var order = await fixture.Service.CreateOrderAsync(
                fixture.TenantId,
                fixture.CompanyId,
                fixture.BranchId,
                request);

            Assert.Equal(fixture.Price!.SupplierProductPriceId, order.Items.Single().SupplierProductPriceId);
        }
        else
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                fixture.Service.CreateOrderAsync(
                    fixture.TenantId,
                    fixture.CompanyId,
                    fixture.BranchId,
                    request));
        }
    }

    [Fact]
    public async Task CreateOrder_ManualPriceStillEnforcesSelectedPriceCardMinimum()
    {
        var fixture = new Fixture(minimumOrderQuantity: 5);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Service.CreateOrderAsync(
                fixture.TenantId,
                fixture.CompanyId,
                fixture.BranchId,
                fixture.CreateRequest(quantity: 4, unitPrice: 12)));
    }

    [Fact]
    public async Task CreateOrder_ManualPriceWithoutPriceCardDoesNotApplyMinimum()
    {
        var fixture = new Fixture(usePriceCard: false);

        var order = await fixture.Service.CreateOrderAsync(
            fixture.TenantId,
            fixture.CompanyId,
            fixture.BranchId,
            fixture.CreateRequest(quantity: 1, unitPrice: 12));

        Assert.Null(order.Items.Single().SupplierProductPriceId);
    }

    [Fact]
    public async Task CreateOrder_ConvertsOrderedQuantityToPriceCardUnitBeforeCheckingMinimum()
    {
        var cardUnitId = Guid.NewGuid();
        var orderUnitId = Guid.NewGuid();
        var fixture = new Fixture(minimumOrderQuantity: 20, priceUnitId: cardUnitId);
        fixture.UomRepository.AddUnit(orderUnitId);
        fixture.UomService.ConversionFactor = 10;

        var order = await fixture.Service.CreateOrderAsync(
            fixture.TenantId,
            fixture.CompanyId,
            fixture.BranchId,
            fixture.CreateRequest(quantity: 2, unitPrice: 9, unitOfMeasureId: orderUnitId));

        Assert.Equal(orderUnitId, order.Items.Single().UnitOfMeasureId);
        Assert.Equal(cardUnitId, fixture.UomService.LastToUnitId);
        Assert.Equal(ConversionContext.Purchase, fixture.UomService.LastContext);
        Assert.Equal(fixture.TenantId, fixture.PriceService.LastTenantId);
        Assert.Equal(fixture.TenantId, fixture.UomService.LastTenantId);
    }

    [Fact]
    public async Task CreateOrder_WithMOQAndNoEstablishedUnits_ThrowsBusinessValidationError()
    {
        var fixture = new Fixture(minimumOrderQuantity: 5, omitProductUnit: true);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Service.CreateOrderAsync(
                fixture.TenantId,
                fixture.CompanyId,
                fixture.BranchId,
                fixture.CreateRequest(quantity: 5, unitPrice: 12)));

        Assert.Contains("units of measure could not be established", exception.Message);
    }

    [Theory]
    [InlineData(4, false)]
    [InlineData(5, true)]
    [InlineData(8, true)]
    public async Task AddItem_EnforcesMinimumOrderQuantityAtBoundaries(decimal quantity, bool isAllowed)
    {
        var fixture = new Fixture(minimumOrderQuantity: 5);
        fixture.Order = PurchaseOrder.Create(
            fixture.TenantId,
            fixture.CompanyId,
            fixture.BranchId,
            "PO-ADD-BOUNDARY",
            supplierId: fixture.SupplierId);

        if (isAllowed)
        {
            var item = await fixture.Service.AddItemAsync(
                fixture.TenantId,
                fixture.CompanyId,
                fixture.BranchId,
                fixture.Order.Id,
                new CreatePurchaseOrderItemRequest
                {
                    ProductId = fixture.Product.ProductId,
                    Quantity = quantity,
                    UnitPrice = 12
                });

            Assert.Equal(quantity, item.Quantity);
            Assert.Equal(fixture.Price!.SupplierProductPriceId, item.SupplierProductPriceId);
        }
        else
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                fixture.Service.AddItemAsync(
                    fixture.TenantId,
                    fixture.CompanyId,
                    fixture.BranchId,
                    fixture.Order.Id,
                    new CreatePurchaseOrderItemRequest
                    {
                        ProductId = fixture.Product.ProductId,
                        Quantity = quantity,
                        UnitPrice = 12
                    }));

            Assert.Empty(fixture.Order.Items);
        }
    }

    [Fact]
    public async Task AddItem_EnforcesPriceCardMinimumEvenWhenUnitPriceIsOverridden()
    {
        var fixture = new Fixture(minimumOrderQuantity: 5);
        fixture.Order = PurchaseOrder.Create(
            fixture.TenantId,
            fixture.CompanyId,
            fixture.BranchId,
            "PO-ADD",
            supplierId: fixture.SupplierId);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Service.AddItemAsync(
                fixture.TenantId,
                fixture.CompanyId,
                fixture.BranchId,
                fixture.Order.Id,
                new CreatePurchaseOrderItemRequest
                {
                    ProductId = fixture.Product.ProductId,
                    Quantity = 4,
                    UnitPrice = 12
                }));

        Assert.Empty(fixture.Order.Items);
    }

    [Fact]
    public async Task AddItem_UsesConvertedQuantityForPriceCardMinimum()
    {
        var cardUnitId = Guid.NewGuid();
        var orderUnitId = Guid.NewGuid();
        var fixture = new Fixture(minimumOrderQuantity: 20, priceUnitId: cardUnitId);
        fixture.UomRepository.AddUnit(orderUnitId);
        fixture.UomService.ConversionFactor = 10;
        fixture.Order = PurchaseOrder.Create(
            fixture.TenantId,
            fixture.CompanyId,
            fixture.BranchId,
            "PO-ADD-UOM",
            supplierId: fixture.SupplierId);

        var item = await fixture.Service.AddItemAsync(
            fixture.TenantId,
            fixture.CompanyId,
            fixture.BranchId,
            fixture.Order.Id,
            new CreatePurchaseOrderItemRequest
            {
                ProductId = fixture.Product.ProductId,
                UnitOfMeasureId = orderUnitId,
                Quantity = 2,
                UnitPrice = 9
            });

        Assert.Equal(fixture.Price!.SupplierProductPriceId, item.SupplierProductPriceId);
        Assert.Equal(orderUnitId, item.UnitOfMeasureId);
        Assert.Equal(cardUnitId, fixture.UomService.LastToUnitId);
        Assert.Equal(fixture.TenantId, fixture.PriceService.LastTenantId);
        Assert.Equal(fixture.TenantId, fixture.UomService.LastTenantId);
    }

    [Fact]
    public async Task AddItem_UsesPersistedFourDecimalQuantityAtConvertedMOQBoundary()
    {
        var cardUnitId = Guid.NewGuid();
        var orderUnitId = Guid.NewGuid();
        var fixture = new Fixture(minimumOrderQuantity: 2.4692m, priceUnitId: cardUnitId);
        fixture.UomRepository.AddUnit(orderUnitId);
        fixture.UomService.ConversionFactor = 2;
        fixture.Order = PurchaseOrder.Create(
            fixture.TenantId,
            fixture.CompanyId,
            fixture.BranchId,
            "PO-ADD-ROUNDING",
            supplierId: fixture.SupplierId);

        var item = await fixture.Service.AddItemAsync(
            fixture.TenantId,
            fixture.CompanyId,
            fixture.BranchId,
            fixture.Order.Id,
            new CreatePurchaseOrderItemRequest
            {
                ProductId = fixture.Product.ProductId,
                UnitOfMeasureId = orderUnitId,
                Quantity = 1.23456m,
                UnitPrice = 9
            });

        Assert.Equal(1.2346m, item.Quantity);
        Assert.Equal(1.2346m, fixture.UomService.LastConvertedQuantity);
        Assert.Equal(fixture.TenantId, fixture.UomService.LastTenantId);
    }

    private sealed class Fixture
    {
        public Guid TenantId { get; } = Guid.NewGuid();
        public Guid CompanyId { get; } = Guid.NewGuid();
        public Guid BranchId { get; } = Guid.NewGuid();
        public Guid SupplierId { get; } = Guid.NewGuid();
        public Product Product { get; }
        public SupplierProductPrice? Price { get; }
        public PurchaseOrder? Order { get; set; }
        public StubUomRepository UomRepository { get; } = new();
        public StubUomService UomService { get; } = new();
        public StubPriceService PriceService { get; }
        public PurchaseOrderService Service { get; }

        public Fixture(
            decimal minimumOrderQuantity = 1,
            Guid? priceUnitId = null,
            bool usePriceCard = true,
            bool omitProductUnit = false)
        {
            Guid? productUnitId = omitProductUnit ? null : Guid.NewGuid();
            Product = Product.Create(
                TenantId,
                "ITEM-1",
                "Item 1",
                Guid.NewGuid(),
                unitOfMeasureId: productUnitId);
            if (productUnitId.HasValue)
                UomRepository.AddUnit(productUnitId.Value);

            if (usePriceCard)
            {
                Price = SupplierProductPrice.Create(
                    TenantId,
                    SupplierId,
                    Product.ProductId,
                    10,
                    DateTime.UtcNow.AddDays(-1),
                    unitOfMeasureId: priceUnitId ?? productUnitId,
                    minimumOrderQuantity: minimumOrderQuantity);
            }

            if (Price?.UnitOfMeasureId.HasValue == true)
                UomRepository.AddUnit(Price.UnitOfMeasureId.Value);

            var orderRepository = new StubPurchaseOrderRepository(() => Order);
            PriceService = new StubPriceService(TenantId, Price);
            Service = new PurchaseOrderService(
                orderRepository,
                new StubProductRepository(Product),
                new StubSupplierRepository(Supplier.Create(TenantId, "SUP-1", "Supplier 1", supplierId: SupplierId)),
                PriceService,
                UomRepository,
                new StubApprovalService(),
                uomService: UomService);
        }

        public CreatePurchaseOrderRequest CreateRequest(decimal quantity, decimal? unitPrice, Guid? unitOfMeasureId = null) =>
            new()
            {
                OrderNumber = "PO-NEW",
                SupplierId = SupplierId,
                OrderDate = DateTime.UtcNow,
                Items =
                {
                    new CreatePurchaseOrderItemRequest
                    {
                        ProductId = Product.ProductId,
                        UnitOfMeasureId = unitOfMeasureId,
                        Quantity = quantity,
                        UnitPrice = unitPrice
                    }
                }
            };
    }

    private sealed class StubPurchaseOrderRepository : IPurchaseOrderRepository
    {
        private readonly Func<PurchaseOrder?> _getOrder;

        public StubPurchaseOrderRepository(Func<PurchaseOrder?> getOrder) => _getOrder = getOrder;
        public Task<List<PurchaseOrder>> GetByBranchAsync(Guid tenantId, Guid companyId, Guid branchId) => Task.FromResult(new List<PurchaseOrder>());
        public Task<List<PurchaseOrder>> GetByCompanyAsync(Guid tenantId, Guid companyId, IEnumerable<Guid>? branchIds = null) => Task.FromResult(new List<PurchaseOrder>());
        public Task<PurchaseOrder?> GetByIdAsync(Guid tenantId, Guid companyId, Guid branchId, Guid orderId) => Task.FromResult(_getOrder());
        public Task<PurchaseOrder?> GetByIdAndCompanyAsync(Guid tenantId, Guid companyId, Guid orderId) => Task.FromResult(_getOrder());
        public Task<bool> ExistsOrderNumberAsync(Guid tenantId, Guid companyId, string orderNumber) => Task.FromResult(false);
        public Task AddAsync(PurchaseOrder order) { return Task.CompletedTask; }
        public Task SaveChangesAsync() => Task.CompletedTask;
    }

    private sealed class StubProductRepository : IProductRepository
    {
        private readonly Product _product;
        public StubProductRepository(Product product) => _product = product;
        public Task<List<Product>> GetByTenantAsync(Guid tenantId, Guid? brandId = null) => Task.FromResult(new List<Product> { _product });
        public Task<Product?> GetByIdAsync(Guid tenantId, Guid productId) => Task.FromResult<Product?>(productId == _product.ProductId ? _product : null);
        public Task<bool> ExistsByCodeAsync(Guid tenantId, string productCode) => Task.FromResult(false);
        public Task<bool> ExistsByNameAsync(Guid tenantId, string productName) => Task.FromResult(false);
        public Task<bool> CategoryExistsAsync(Guid tenantId, Guid categoryId) => Task.FromResult(true);
        public Task<bool> BrandExistsAsync(Guid tenantId, Guid brandId) => Task.FromResult(true);
        public Task AddAsync(Product product) => Task.CompletedTask;
        public Task SaveChangesAsync() => Task.CompletedTask;
    }

    private sealed class StubSupplierRepository : ISupplierRepository
    {
        private readonly Supplier _supplier;
        public StubSupplierRepository(Supplier supplier) => _supplier = supplier;
        public Task<List<Supplier>> GetSuppliersAsync(Guid tenantId) => Task.FromResult(new List<Supplier> { _supplier });
        public Task<Supplier?> GetSupplierByIdAsync(Guid tenantId, Guid supplierId) => Task.FromResult<Supplier?>(supplierId == _supplier.SupplierId ? _supplier : null);
        public Task<bool> ExistsByCodeAsync(Guid tenantId, string code) => Task.FromResult(false);
        public Task<bool> ExistsByNameAsync(Guid tenantId, string name) => Task.FromResult(false);
        public Task AddSupplierAsync(Supplier supplier) => Task.CompletedTask;
        public Task<List<SupplierAddress>> GetAddressesAsync(Guid tenantId, Guid supplierId) => Task.FromResult(new List<SupplierAddress>());
        public Task<SupplierAddress?> GetAddressAssociationAsync(Guid tenantId, Guid supplierId, Guid addressId, Guid addressTypeId) => Task.FromResult<SupplierAddress?>(null);
        public Task<SupplierAddress?> GetAddressAssociationByIdAsync(Guid tenantId, Guid supplierAddressId) => Task.FromResult<SupplierAddress?>(null);
        public Task AddAddressAssociationAsync(SupplierAddress association) => Task.CompletedTask;
        public Task<List<SupplierContact>> GetContactsAsync(Guid tenantId, Guid supplierId) => Task.FromResult(new List<SupplierContact>());
        public Task<SupplierContact?> GetContactAssociationAsync(Guid tenantId, Guid supplierId, Guid contactId, Guid contactTypeId) => Task.FromResult<SupplierContact?>(null);
        public Task<SupplierContact?> GetContactAssociationByIdAsync(Guid tenantId, Guid supplierContactId) => Task.FromResult<SupplierContact?>(null);
        public Task AddContactAssociationAsync(SupplierContact association) => Task.CompletedTask;
        public Task SaveChangesAsync() => Task.CompletedTask;
    }

    private sealed class StubPriceService : ISupplierProductPriceService
    {
        private readonly SupplierProductPrice? _price;
        private readonly Guid _expectedTenantId;
        public Guid? LastTenantId { get; private set; }

        public StubPriceService(Guid expectedTenantId, SupplierProductPrice? price)
        {
            _expectedTenantId = expectedTenantId;
            _price = price;
        }

        public Task<List<SupplierProductPrice>> GetPricesAsync(Guid tenantId, Guid? supplierId = null, Guid? productId = null) => Task.FromResult(_price == null ? new List<SupplierProductPrice>() : new List<SupplierProductPrice> { _price });
        public Task<SupplierProductPrice?> GetPriceAsync(Guid tenantId, Guid priceId) => Task.FromResult<SupplierProductPrice?>(_price);
        public Task<SupplierProductPrice> CreatePriceAsync(Guid tenantId, CreateSupplierProductPriceRequest request) => throw new NotSupportedException();
        public Task<SupplierProductPrice> UpdatePriceAsync(Guid tenantId, Guid priceId, UpdateSupplierProductPriceRequest request) => throw new NotSupportedException();
        public Task DeactivatePriceAsync(Guid tenantId, Guid priceId) => throw new NotSupportedException();
        public Task<SupplierProductPrice?> GetActivePriceAsync(Guid tenantId, Guid supplierId, Guid productId, DateTime? asOfDate = null)
        {
            LastTenantId = tenantId;
            return Task.FromResult(tenantId == _expectedTenantId ? _price : null);
        }
    }

    private sealed class StubUomRepository : IUnitOfMeasureRepository
    {
        private readonly Dictionary<Guid, UnitOfMeasure> _units = new();
        public void AddUnit(Guid id) => _units[id] = UnitOfMeasure.Create(Guid.Empty, "UOM", "Unit", unitOfMeasureId: id);
        public Task<List<UnitOfMeasure>> GetUnitsAsync(Guid tenantId) => Task.FromResult(_units.Values.ToList());
        public Task<UnitOfMeasure?> GetUnitByIdAsync(Guid tenantId, Guid unitOfMeasureId) => Task.FromResult(_units.GetValueOrDefault(unitOfMeasureId));
        public Task<UnitOfMeasure?> GetUnitByCodeAsync(Guid tenantId, string code) => Task.FromResult(_units.Values.FirstOrDefault(unit => unit.Code == code));
        public Task<bool> ExistsByCodeAsync(Guid tenantId, string code) => Task.FromResult(false);
        public Task AddUnitAsync(UnitOfMeasure unitOfMeasure) => Task.CompletedTask;
        public Task<List<UnitOfMeasureConversion>> GetConversionsAsync(Guid tenantId) => Task.FromResult(new List<UnitOfMeasureConversion>());
        public Task<UnitOfMeasureConversion?> GetConversionByIdAsync(Guid tenantId, Guid conversionId) => Task.FromResult<UnitOfMeasureConversion?>(null);
        public Task<UnitOfMeasureConversion?> GetConversionAsync(Guid tenantId, Guid fromUnitId, Guid toUnitId) => Task.FromResult<UnitOfMeasureConversion?>(null);
        public Task AddConversionAsync(UnitOfMeasureConversion conversion) => Task.CompletedTask;
        public Task SaveChangesAsync() => Task.CompletedTask;
    }

    private sealed class StubUomService : IUnitOfMeasureService
    {
        public decimal ConversionFactor { get; set; } = 1;
        public decimal? LastConvertedQuantity { get; private set; }
        public Guid? LastTenantId { get; private set; }
        public Guid? LastToUnitId { get; private set; }
        public ConversionContext LastContext { get; private set; }
        public Task<List<UnitOfMeasure>> GetUnitsAsync(Guid tenantId) => throw new NotSupportedException();
        public Task<UnitOfMeasure?> GetUnitAsync(Guid tenantId, Guid unitOfMeasureId) => throw new NotSupportedException();
        public Task<UnitOfMeasure> CreateUnitAsync(Guid tenantId, CreateUnitOfMeasureRequest request) => throw new NotSupportedException();
        public Task<UnitOfMeasure> UpdateUnitAsync(Guid tenantId, Guid unitOfMeasureId, UpdateUnitOfMeasureRequest request) => throw new NotSupportedException();
        public Task DeactivateUnitAsync(Guid tenantId, Guid unitOfMeasureId) => throw new NotSupportedException();
        public Task<UnitOfMeasure> ReactivateUnitAsync(Guid tenantId, Guid unitOfMeasureId) => throw new NotSupportedException();
        public Task<List<UnitOfMeasureConversion>> GetConversionsAsync(Guid tenantId) => throw new NotSupportedException();
        public Task<UnitOfMeasureConversion> CreateConversionAsync(Guid tenantId, CreateUnitOfMeasureConversionRequest request) => throw new NotSupportedException();
        public Task<UnitOfMeasureConversion> UpdateConversionAsync(Guid tenantId, Guid conversionId, UpdateUnitOfMeasureConversionRequest request) => throw new NotSupportedException();
        public Task DeactivateConversionAsync(Guid tenantId, Guid conversionId) => throw new NotSupportedException();
        public Task<ConvertUnitOfMeasureResult> ConvertQuantityAsync(Guid tenantId, Guid fromUnitId, Guid toUnitId, decimal quantity) => throw new NotSupportedException();
        public Task<ConvertUnitOfMeasureResult> ConvertProductQuantityAsync(Guid tenantId, Guid productId, Guid fromUnitId, Guid toUnitId, decimal quantity, ConversionContext context = ConversionContext.General, Guid? productPackagingId = null, Guid? companyId = null)
        {
            LastTenantId = tenantId;
            LastConvertedQuantity = quantity;
            LastToUnitId = toUnitId;
            LastContext = context;
            return Task.FromResult(new ConvertUnitOfMeasureResult
            {
                FromUnitId = fromUnitId,
                ToUnitId = toUnitId,
                OriginalQuantity = quantity,
                ConvertedQuantity = quantity * ConversionFactor,
                EffectiveFactor = ConversionFactor
            });
        }
    }

    private sealed class StubApprovalService : IApprovalWorkflowService
    {
        public Task<ProcurementApprovalSettings> GetApprovalSettingsAsync(Guid tenantId) => throw new NotSupportedException();
        public Task RecordSubmissionAsync(Guid tenantId, string documentType, Guid documentId, Guid actorUserId, string actorUserName, decimal snapshotAmount) => throw new NotSupportedException();
        public Task RecordAutoApprovalAsync(Guid tenantId, string documentType, Guid documentId, Guid actorUserId, string actorUserName, decimal snapshotAmount) => throw new NotSupportedException();
        public Task RecordApprovalAsync(Guid tenantId, string documentType, Guid documentId, Guid approverUserId, string approverUserName, Guid creatorUserId, decimal snapshotAmount, string? remarks = null) => throw new NotSupportedException();
        public Task RecordRejectionAsync(Guid tenantId, string documentType, Guid documentId, Guid approverUserId, string approverUserName, string remarks, decimal snapshotAmount) => throw new NotSupportedException();
        public Task RecordCancellationAsync(Guid tenantId, string documentType, Guid documentId, Guid actorUserId, string actorUserName, decimal snapshotAmount, string? remarks = null) => throw new NotSupportedException();
        public Task<List<ApprovalAuditRecord>> GetApprovalHistoryAsync(Guid tenantId, string documentType, Guid documentId) => throw new NotSupportedException();
    }
}
