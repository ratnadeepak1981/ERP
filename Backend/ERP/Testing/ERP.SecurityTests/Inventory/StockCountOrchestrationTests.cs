using System.Reflection;
using Domain.Features.MasterData.Company;
using Domain.Features.MasterData.Product;
using Domain.Features.MasterData.UnitOfMeasure;
using Domain.Infrastructure.Persistence;
using ERP.Domain.Features.Inventory;
using ERP.Domain.Features.Inventory.StockCount;
using ERP.Domain.Features.Inventory.Valuation;
using ERP.Domain.Features.MasterData.Category;
using ERP.Domain.Features.MasterData.Warehouse;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace ERP.SecurityTests.Inventory;

public class StockCountOrchestrationTests
{
    [Fact]
    public async Task CreateDraft_UsesInventorySnapshotAndConvertsPhysicalQuantityToBaseUom()
    {
        await using var test = await StockCountTestContext.CreateAsync();
        var product = test.AddProduct(ValuationMethod.FIFO);
        var alternateUnit = UnitOfMeasure.Create(test.TenantId, "BOX", "Box");
        test.Db.UnitsOfMeasure.Add(alternateUnit);
        var balance = InventoryBalance.Create(test.TenantId, test.Warehouse.WarehouseId, test.Location.WarehouseLocationId, product.ProductId);
        balance.ApplyOnHandChange(5m);
        test.Db.InventoryBalances.Add(balance);
        await test.Db.SaveChangesAsync();

        test.UnitOfMeasureProxy.Handler = (_, _) => Task.FromResult(new ConvertUnitOfMeasureResult
        {
            ConvertedQuantity = 24m
        });

        var count = await test.Service.CreateDraftAsync(test.TenantId, new CreateStockCountDraftRequest
        {
            CompanyId = test.Company.Id,
            WarehouseId = test.Warehouse.WarehouseId,
            WarehouseLocationId = test.Location.WarehouseLocationId,
            Lines =
            [
                new CreateStockCountLineRequest
                {
                    ProductId = product.ProductId,
                    UnitOfMeasureId = alternateUnit.UnitOfMeasureId,
                    ExpectedQuantity = 999m,
                    PhysicalQuantity = 2m
                }
            ]
        });

        var line = Assert.Single(count.Lines);
        Assert.Equal(5m, line.ExpectedQuantityBase);
        Assert.Equal(24m, line.PhysicalQuantityBase);
        Assert.Equal(alternateUnit.UnitOfMeasureId, line.UnitOfMeasureId);
        Assert.Equal(-5m + 24m, line.VarianceQuantityBase);
    }

    [Theory]
    [InlineData(ValuationMethod.FIFO)]
    [InlineData(ValuationMethod.WeightedAverage)]
    public async Task PostAsync_RequiresExplicitCostForFifoAndWeightedAverage(ValuationMethod method)
    {
        await using var test = await StockCountTestContext.CreateAsync();
        var product = test.AddProduct(method);
        await test.AddApprovedCountAsync((product, 5m, 6m, 0m, null, null));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            test.Service.PostAsync(test.TenantId, test.Count!.StockCountId, new PostStockCountRequest()));

        Assert.Empty(test.InventoryProxy.ReceiptRequests);
        Assert.Equal(StockCountStatus.Approved, test.Count!.Status);
    }

    [Fact]
    public async Task PostAsync_UsesStandardCostForStandardCostProduct()
    {
        await using var test = await StockCountTestContext.CreateAsync();
        var product = test.AddProduct(ValuationMethod.StandardCost, standardCost: 17m);
        await test.AddApprovedCountAsync((product, 5m, 6m, 0m, null, null));

        await test.Service.PostAsync(test.TenantId, test.Count!.StockCountId, new PostStockCountRequest());

        var receipt = Assert.Single(test.InventoryProxy.ReceiptRequests);
        Assert.Equal(17m, Assert.Single(receipt.Lines).UnitCost);
        Assert.Equal(StockCountStatus.Posted, test.Count.Status);
    }

    [Fact]
    public async Task PostAsync_RejectsStandardCostProductWithoutPositiveStandardCost()
    {
        await using var test = await StockCountTestContext.CreateAsync();
        var product = test.AddProduct(ValuationMethod.StandardCost);
        await test.AddApprovedCountAsync((product, 5m, 6m, 20m, null, null));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            test.Service.PostAsync(test.TenantId, test.Count!.StockCountId, new PostStockCountRequest()));

        Assert.Empty(test.InventoryProxy.ReceiptRequests);
        Assert.Equal(StockCountStatus.Approved, test.Count!.Status);
    }

    [Fact]
    public async Task PostAsync_RejectsStaleSnapshotBeforeRequestingMovements()
    {
        await using var test = await StockCountTestContext.CreateAsync();
        var product = test.AddProduct(ValuationMethod.FIFO);
        await test.AddApprovedCountAsync((product, 5m, 4m, 0m, null, null));
        var balance = await test.Db.InventoryBalances.SingleAsync(x => x.ProductId == product.ProductId);
        balance.ApplyOnHandChange(1m);
        await test.Db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            test.Service.PostAsync(test.TenantId, test.Count!.StockCountId, new PostStockCountRequest()));

        Assert.Contains("Inventory changed", ex.Message);
        Assert.Empty(test.InventoryProxy.IssueRequests);
        Assert.Empty(test.InventoryProxy.ReceiptRequests);
        Assert.Equal(StockCountStatus.Approved, test.Count!.Status);
    }

    [Fact]
    public async Task PostAsync_MixedVarianceUsesPersistentLineIdsAndDeterministicLocks()
    {
        await using var test = await StockCountTestContext.CreateAsync();
        var firstId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var secondId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var issueProduct = test.AddProduct(ValuationMethod.FIFO, productId: secondId);
        var receiptProduct = test.AddProduct(ValuationMethod.FIFO, productId: firstId);
        var zeroProduct = test.AddProduct(ValuationMethod.FIFO);
        await test.AddApprovedCountAsync(
            (issueProduct, 5m, 3m, 0m, null, null),
            (receiptProduct, 5m, 7m, 4m, null, null),
            (zeroProduct, 2m, 2m, 0m, null, null));

        var issueLineId = test.Count!.Lines.Single(x => x.ProductId == issueProduct.ProductId).StockCountLineId;
        var receiptLineId = test.Count.Lines.Single(x => x.ProductId == receiptProduct.ProductId).StockCountLineId;
        await test.Service.PostAsync(test.TenantId, test.Count.StockCountId, new PostStockCountRequest());

        var issueLine = Assert.Single(Assert.Single(test.InventoryProxy.IssueRequests).Lines);
        var receiptLine = Assert.Single(Assert.Single(test.InventoryProxy.ReceiptRequests).Lines);
        Assert.Equal(issueLineId, issueLine.StockIssueLineId);
        Assert.Equal(receiptLineId, receiptLine.StockReceiptLineId);
        Assert.NotEqual(zeroProduct.ProductId, issueLine.ProductId);
        Assert.NotEqual(zeroProduct.ProductId, receiptLine.ProductId);
        Assert.Equal(3, test.ValuationProxy.LockRequests.Count);
        Assert.Equal(
            test.ValuationProxy.LockRequests.Select(x => x.ProductId).OrderBy(x => x).ToArray(),
            test.ValuationProxy.LockRequests.Select(x => x.ProductId).ToArray());
        Assert.Equal(StockCountStatus.Posted, test.Count.Status);
    }

    [Fact]
    public async Task PostAsync_ReceiptFailureAfterIssueDoesNotMarkCountPosted()
    {
        await using var test = await StockCountTestContext.CreateAsync();
        var issueProduct = test.AddProduct(ValuationMethod.FIFO);
        var receiptProduct = test.AddProduct(ValuationMethod.FIFO);
        await test.AddApprovedCountAsync(
            (issueProduct, 5m, 4m, 0m, null, null),
            (receiptProduct, 5m, 6m, 5m, null, null));
        test.InventoryProxy.ReceiptFailure = new InvalidOperationException("Injected receipt failure.");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            test.Service.PostAsync(test.TenantId, test.Count!.StockCountId, new PostStockCountRequest()));

        Assert.Single(test.InventoryProxy.IssueRequests);
        Assert.Equal(StockCountStatus.Approved, test.Count!.Status);
    }

    [Fact]
    public async Task InventoryService_AllowsReceiptAfterIssueButStillRejectsDuplicateIssue()
    {
        await using var test = await StockCountTestContext.CreateAsync();
        var product = test.AddProduct(ValuationMethod.FIFO);
        await test.Db.SaveChangesAsync();
        var documentId = Guid.NewGuid();
        var lineId = Guid.NewGuid();
        test.Db.InventoryTransactions.Add(InventoryTransaction.Create(
            test.TenantId, "SC-EXISTING-ISSUE", InventoryMovementType.StockAdjustmentOut,
            SourceDocumentType.StockAdjustment, documentId, lineId, 1,
            product.ProductId, test.Warehouse.WarehouseId, test.Location.WarehouseLocationId,
            -1m, 5m));
        await test.Db.SaveChangesAsync();

        var postingProxy = DispatchProxy.Create<IInventoryPostingService, TestDispatchProxy>();
        ((TestDispatchProxy)(object)postingProxy).Handler = (method, args) =>
        {
            if (method.Name == nameof(IInventoryPostingService.PostMovementAsync))
            {
                var movement = (PostInventoryMovementRequest)args![1]!;
                return Task.FromResult(InventoryTransaction.Create(
                    test.TenantId,
                    $"SC-RECEIPT-{Guid.NewGuid():N}",
                    movement.MovementType,
                    movement.SourceDocumentType,
                    movement.SourceDocumentId,
                    movement.SourceDocumentLineId ?? Guid.Empty,
                    movement.MovementSequence,
                    movement.ProductId,
                    movement.WarehouseId,
                    movement.WarehouseLocationId,
                    movement.Quantity,
                    movement.UnitCost,
                    batchNumber: movement.BatchNumber,
                    serialNumber: movement.SerialNumber));
            }

            return TestDispatchProxy.DefaultResult(method.ReturnType);
        };
        var inventoryService = new InventoryService(test.Db, postingProxy);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            inventoryService.CreateStockIssueAsync(test.TenantId, new CreateStockIssueRequest
            {
                StockIssueId = documentId,
                WarehouseId = test.Warehouse.WarehouseId,
                Purpose = StockIssuePurpose.InternalUse,
                Lines =
                [
                    new CreateStockIssueLineRequest
                    {
                        ProductId = product.ProductId,
                        WarehouseLocationId = test.Location.WarehouseLocationId,
                        Quantity = 1m,
                        StockIssueLineId = lineId
                    }
                ]
            }));

        var receipts = await inventoryService.CreateStockReceiptAsync(test.TenantId, new CreateStockReceiptRequest
        {
            StockReceiptId = documentId,
            WarehouseId = test.Warehouse.WarehouseId,
            Reason = StockReceiptReason.Miscellaneous,
            Lines =
            [
                new CreateStockReceiptLineRequest
                {
                    ProductId = product.ProductId,
                    WarehouseLocationId = test.Location.WarehouseLocationId,
                    Quantity = 1m,
                    UnitCost = 5m,
                    StockReceiptLineId = Guid.NewGuid()
                }
            ]
        });

        Assert.Single(receipts);
        Assert.Equal(InventoryMovementType.StockAdjustmentIn.ToString(), receipts[0].MovementType);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsOriginalAndReversalTransactionsForOneLine()
    {
        await using var test = await StockCountTestContext.CreateAsync();
        var product = test.AddProduct(ValuationMethod.FIFO);
        await test.AddApprovedCountAsync((product, 5m, 4m, 0m, null, null));
        var lineId = test.Count!.Lines.Single().StockCountLineId;
        var original = InventoryTransaction.Create(
            test.TenantId, "SC-ORIGINAL", InventoryMovementType.StockAdjustmentOut,
            SourceDocumentType.StockAdjustment, test.Count.StockCountId, lineId, 1,
            product.ProductId, test.Warehouse.WarehouseId, test.Location.WarehouseLocationId,
            -1m, 2m);
        var reversal = InventoryTransaction.Create(
            test.TenantId, "SC-REVERSAL", InventoryMovementType.Reversal,
            SourceDocumentType.StockAdjustment, test.Count.StockCountId, lineId, 1,
            product.ProductId, test.Warehouse.WarehouseId, test.Location.WarehouseLocationId,
            1m, 2m, reversalOfTransactionId: original.InventoryTransactionId);
        test.Db.InventoryTransactions.AddRange(original, reversal);
        await test.Db.SaveChangesAsync();

        var result = await test.Service.GetByIdAsync(test.TenantId, test.Count.StockCountId);

        var transactions = Assert.Single(result!.Lines).InventoryTransactions;
        Assert.Equal(2, transactions.Count);
        Assert.Contains(transactions, x => x.InventoryTransactionId == original.InventoryTransactionId);
        Assert.Contains(transactions, x => x.ReversalOfTransactionId == original.InventoryTransactionId);
    }

    [Fact]
    public async Task CreateDraft_RejectsDuplicateSerialAcrossDifferentBatches()
    {
        await using var test = await StockCountTestContext.CreateAsync();
        var product = test.AddProduct(ValuationMethod.FIFO, trackingMode: TrackingMode.BatchAndSerial);
        await test.Db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            test.Service.CreateDraftAsync(test.TenantId, new CreateStockCountDraftRequest
            {
                CompanyId = test.Company.Id,
                WarehouseId = test.Warehouse.WarehouseId,
                WarehouseLocationId = test.Location.WarehouseLocationId,
                Lines =
                [
                    new CreateStockCountLineRequest
                    {
                        ProductId = product.ProductId,
                        PhysicalQuantity = 1m,
                        BatchNumber = "LOT-A",
                        SerialNumber = "SERIAL-1"
                    },
                    new CreateStockCountLineRequest
                    {
                        ProductId = product.ProductId,
                        PhysicalQuantity = 1m,
                        BatchNumber = "LOT-B",
                        SerialNumber = "serial-1"
                    }
                ]
            }));
    }

    private sealed class StockCountTestContext : IAsyncDisposable
    {
        private StockCountTestContext(DomainDbContext db)
        {
            Db = db;
            TenantId = Guid.NewGuid();
            Category = Category.Create(TenantId, $"CAT-{Guid.NewGuid():N}", "Stock Count Tests");
            BaseUnit = UnitOfMeasure.Create(TenantId, "PCS", "Pieces");
            Company = new Company
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                Name = "Stock Count Test Company",
                Code = $"CO-{Guid.NewGuid():N}",
                IsActive = true
            };
            Warehouse = Warehouse.Create(TenantId, $"WH-{Guid.NewGuid():N}", "Stock Count Test Warehouse", companyId: Company.Id);
            Location = WarehouseLocation.Create(TenantId, Warehouse.WarehouseId, null, $"LOC-{Guid.NewGuid():N}", "Stock Count Test Location");
            Db.AddRange(Category, BaseUnit, Company, Warehouse, Location);
            UnitOfMeasureProxy = new ProxyState<IUnitOfMeasureService>();
            InventoryProxy = new InventoryProxyState();
            ValuationProxy = new ValuationProxyState();
            Service = new StockCountService(
                Db,
                InventoryProxy.Create(),
                UnitOfMeasureProxy.Create(),
                ValuationProxy.Create());
        }

        public DomainDbContext Db { get; }
        public Guid TenantId { get; }
        public Category Category { get; }
        public UnitOfMeasure BaseUnit { get; }
        public Company Company { get; }
        public Warehouse Warehouse { get; }
        public WarehouseLocation Location { get; }
        public StockCount? Count { get; private set; }
        public StockCountService Service { get; }
        public ProxyState<IUnitOfMeasureService> UnitOfMeasureProxy { get; }
        public InventoryProxyState InventoryProxy { get; }
        public ValuationProxyState ValuationProxy { get; }

        public static async Task<StockCountTestContext> CreateAsync()
        {
            var options = new DbContextOptionsBuilder<DomainDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            var test = new StockCountTestContext(new DomainDbContext(options));
            await test.Db.SaveChangesAsync();
            return test;
        }

        public Product AddProduct(
            ValuationMethod method,
            decimal standardCost = 0m,
            TrackingMode trackingMode = TrackingMode.None,
            Guid? productId = null)
        {
            var product = Product.Create(
                TenantId,
                $"P-{Guid.NewGuid():N}",
                "Stock Count Product",
                Category.CategoryId,
                "PCS",
                valuationMethod: method,
                standardCost: standardCost,
                trackingMode: trackingMode,
                unitOfMeasureId: BaseUnit.UnitOfMeasureId,
                productId: productId);
            Db.Products.Add(product);
            return product;
        }

        public async Task AddApprovedCountAsync(params (Product Product, decimal Expected, decimal Physical, decimal Cost, string? Batch, string? Serial)[] items)
        {
            await Db.SaveChangesAsync();
            var count = StockCount.Create(
                TenantId,
                Company.Id,
                Warehouse.WarehouseId,
                Location.WarehouseLocationId,
                $"SC-{Guid.NewGuid():N}");

            foreach (var item in items)
            {
                var line = StockCountLine.Create(
                    item.Product.ProductId,
                    Location.WarehouseLocationId,
                    BaseUnit.UnitOfMeasureId,
                    item.Product.ProductCode,
                    item.Product.ProductName,
                    BaseUnit.Code,
                    item.Expected,
                    item.Physical,
                    item.Batch,
                    item.Serial,
                    unitCost: item.Cost);
                line.TenantId = TenantId;
                line.StockCountId = count.StockCountId;
                count.Lines.Add(line);

                if (item.Expected > 0)
                {
                    var balance = InventoryBalance.Create(
                        TenantId,
                        Warehouse.WarehouseId,
                        Location.WarehouseLocationId,
                        item.Product.ProductId,
                        item.Batch,
                        item.Serial);
                    balance.ApplyOnHandChange(item.Expected);
                    Db.InventoryBalances.Add(balance);
                }
            }

            Count = count;
            Db.StockCounts.Add(count);
            await Db.SaveChangesAsync();
            await Service.SubmitAsync(TenantId, count.StockCountId, new SubmitStockCountRequest
            {
                SubmittedByUserId = Guid.NewGuid()
            });
            await Service.ApproveAsync(TenantId, count.StockCountId, new ApproveStockCountRequest
            {
                ApprovedByUserId = Guid.NewGuid()
            });
        }

        public async ValueTask DisposeAsync() => await Db.DisposeAsync();
    }

    private class ProxyState<T> where T : class
    {
        public Func<MethodInfo, object?[]?, object?>? Handler { get; set; }

        public T Create()
        {
            var proxy = DispatchProxy.Create<T, TestDispatchProxy>();
            ((TestDispatchProxy)(object)proxy).Handler = (method, args) =>
                Handler?.Invoke(method, args) ?? TestDispatchProxy.DefaultResult(method.ReturnType);
            return proxy;
        }
    }

    private sealed class InventoryProxyState : ProxyState<IInventoryService>
    {
        public List<CreateStockIssueRequest> IssueRequests { get; } = new();
        public List<CreateStockReceiptRequest> ReceiptRequests { get; } = new();
        public Exception? ReceiptFailure { get; set; }

        public new IInventoryService Create()
        {
            Handler = (method, args) =>
            {
                if (method.Name == nameof(IInventoryService.CreateStockIssueAsync))
                {
                    IssueRequests.Add((CreateStockIssueRequest)args![1]!);
                    return Task.FromResult(new List<StockIssueDto>());
                }

                if (method.Name == nameof(IInventoryService.CreateStockReceiptAsync))
                {
                    ReceiptRequests.Add((CreateStockReceiptRequest)args![1]!);
                    if (ReceiptFailure != null)
                        throw ReceiptFailure;
                    return Task.FromResult(new List<StockReceiptDto>());
                }

                return TestDispatchProxy.DefaultResult(method.ReturnType);
            };
            return base.Create();
        }
    }

    private sealed class ValuationProxyState : ProxyState<IInventoryValuationService>
    {
        public List<(Guid TenantId, Guid ProductId, Guid WarehouseId)> LockRequests { get; } = new();

        public new IInventoryValuationService Create()
        {
            Handler = (method, args) =>
            {
                if (method.Name == nameof(IInventoryValuationService.AcquireValuationLockAsync))
                {
                    LockRequests.Add(((Guid)args![0]!, (Guid)args[1]!, (Guid)args[2]!));
                    return Task.CompletedTask;
                }

                return TestDispatchProxy.DefaultResult(method.ReturnType);
            };
            return base.Create();
        }
    }

    public class TestDispatchProxy : DispatchProxy
    {
        public Func<MethodInfo, object?[]?, object?>? Handler { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod == null)
                throw new InvalidOperationException("Proxy target method is missing.");

            return Handler?.Invoke(targetMethod, args) ?? DefaultResult(targetMethod.ReturnType);
        }

        public static object? DefaultResult(Type returnType)
        {
            if (returnType == typeof(Task))
                return Task.CompletedTask;
            if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
            {
                var resultType = returnType.GenericTypeArguments[0];
                var result = resultType.IsValueType ? Activator.CreateInstance(resultType) : null;
                return typeof(Task).GetMethod(nameof(Task.FromResult))!
                    .MakeGenericMethod(resultType)
                    .Invoke(null, [result]);
            }

            return returnType.IsValueType ? Activator.CreateInstance(returnType) : null;
        }
    }
}
