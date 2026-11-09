using Domain.Features.MasterData.Company;
using Domain.Features.MasterData.Product;
using Domain.Features.MasterData.UnitOfMeasure;
using Domain.Infrastructure.Persistence;
using System.Reflection;
using ERP.Domain.Features.Inventory;
using ERP.Domain.Features.Inventory.StockCount;
using ERP.Domain.Features.Inventory.Valuation;
using ERP.Domain.Features.MasterData.Category;
using ERP.Domain.Features.MasterData.Warehouse;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ERP.SecurityTests.Inventory;

public class StockCountSqlIntegrationTests : IClassFixture<TestSecurityFixture>
{
    private readonly TestSecurityFixture _fixture;

    public StockCountSqlIntegrationTests(TestSecurityFixture fixture)
    {
        _fixture = fixture;
        _fixture.EnsureDatabasesInitialized();
    }

    [Fact]
    public async Task MixedVariance_PostsIssueReceiptAndZeroVarianceAtomically()
    {
        using var scope = _fixture.Services.CreateScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<DomainDbContext>();
        var posting = services.GetRequiredService<IInventoryPostingService>();
        var stockCounts = services.GetRequiredService<IStockCountService>();
        var tenantId = TestConstants.TenantAlphaId;
        var setup = await CreateSetupAsync(db, tenantId, 3);

        foreach (var product in setup.Products)
        {
            await posting.PostMovementAsync(tenantId, new PostInventoryMovementRequest
            {
                MovementType = InventoryMovementType.GoodsReceipt,
                SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
                SourceDocumentId = Guid.NewGuid(),
                SourceDocumentLineId = Guid.NewGuid(),
                MovementSequence = 1,
                ProductId = product.ProductId,
                WarehouseId = setup.Warehouse.WarehouseId,
                WarehouseLocationId = setup.Location.WarehouseLocationId,
                Quantity = 10m,
                UnitCost = 25m
            });
        }

        var draft = await stockCounts.CreateDraftAsync(tenantId, new CreateStockCountDraftRequest
        {
            CompanyId = setup.Company.Id,
            WarehouseId = setup.Warehouse.WarehouseId,
            WarehouseLocationId = setup.Location.WarehouseLocationId,
            Lines =
            [
                new CreateStockCountLineRequest { ProductId = setup.Products[0].ProductId, PhysicalQuantity = 8m },
                new CreateStockCountLineRequest { ProductId = setup.Products[1].ProductId, PhysicalQuantity = 12m, UnitCost = 25m },
                new CreateStockCountLineRequest { ProductId = setup.Products[2].ProductId, PhysicalQuantity = 10m }
            ]
        });
        await stockCounts.SubmitAsync(tenantId, draft.StockCountId, new SubmitStockCountRequest { SubmittedByUserId = Guid.NewGuid() });
        await stockCounts.ApproveAsync(tenantId, draft.StockCountId, new ApproveStockCountRequest { ApprovedByUserId = Guid.NewGuid() });

        var posted = await stockCounts.PostAsync(tenantId, draft.StockCountId, new PostStockCountRequest { PostedByUserId = Guid.NewGuid() });

        Assert.Equal(StockCountStatus.Posted, posted.Status);
        Assert.Equal(3, posted.Lines.Count);
        Assert.Single(posted.Lines.Single(x => x.ProductId == setup.Products[0].ProductId).InventoryTransactions);
        Assert.Single(posted.Lines.Single(x => x.ProductId == setup.Products[1].ProductId).InventoryTransactions);
        Assert.Empty(posted.Lines.Single(x => x.ProductId == setup.Products[2].ProductId).InventoryTransactions);

        var movements = await db.InventoryTransactions.AsNoTracking()
            .Where(x => x.TenantId == tenantId
                        && x.SourceDocumentType == SourceDocumentType.StockAdjustment
                        && x.SourceDocumentId == draft.StockCountId)
            .ToListAsync();
        Assert.Equal(2, movements.Count);
        Assert.Contains(movements, x => x.MovementType == InventoryMovementType.StockAdjustmentOut && x.Quantity == -2m);
        Assert.Contains(movements, x => x.MovementType == InventoryMovementType.StockAdjustmentIn && x.Quantity == 2m);
        Assert.All(movements, x => Assert.Contains(draft.Lines, line => line.StockCountLineId == x.SourceDocumentLineId));

        var balances = await posting.GetBalanceAsync(tenantId, setup.Location.WarehouseLocationId, setup.Products[0].ProductId);
        Assert.Equal(8m, balances?.QuantityOnHand);
        balances = await posting.GetBalanceAsync(tenantId, setup.Location.WarehouseLocationId, setup.Products[1].ProductId);
        Assert.Equal(12m, balances?.QuantityOnHand);
        balances = await posting.GetBalanceAsync(tenantId, setup.Location.WarehouseLocationId, setup.Products[2].ProductId);
        Assert.Equal(10m, balances?.QuantityOnHand);
    }

    [Fact]
    public async Task ReceiptFailureAfterIssue_RollsBackLedgerBalancesValuationAndCountStatus()
    {
        using var scope = _fixture.Services.CreateScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<DomainDbContext>();
        var posting = services.GetRequiredService<IInventoryPostingService>();
        var tenantId = TestConstants.TenantAlphaId;
        var setup = await CreateSetupAsync(db, tenantId, 2, ValuationMethod.WeightedAverage);

        foreach (var product in setup.Products)
        {
            await posting.PostMovementAsync(tenantId, new PostInventoryMovementRequest
            {
                MovementType = InventoryMovementType.GoodsReceipt,
                SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
                SourceDocumentId = Guid.NewGuid(),
                SourceDocumentLineId = Guid.NewGuid(),
                MovementSequence = 1,
                ProductId = product.ProductId,
                WarehouseId = setup.Warehouse.WarehouseId,
                WarehouseLocationId = setup.Location.WarehouseLocationId,
                Quantity = 10m,
                UnitCost = 25m
            });
        }

        var inventoryService = services.GetRequiredService<IInventoryService>();
        var failingInventory = DispatchProxy.Create<IInventoryService, FailOnReceiptProxy>();
        ((FailOnReceiptProxy)(object)failingInventory).Inner = inventoryService;
        var stockCountService = new StockCountService(
            db,
            failingInventory,
            services.GetRequiredService<IUnitOfMeasureService>(),
            services.GetRequiredService<IInventoryValuationService>());
        var draft = await stockCountService.CreateDraftAsync(tenantId, new CreateStockCountDraftRequest
        {
            CompanyId = setup.Company.Id,
            WarehouseId = setup.Warehouse.WarehouseId,
            WarehouseLocationId = setup.Location.WarehouseLocationId,
            Lines =
            [
                new CreateStockCountLineRequest { ProductId = setup.Products[0].ProductId, PhysicalQuantity = 8m },
                new CreateStockCountLineRequest { ProductId = setup.Products[1].ProductId, PhysicalQuantity = 12m, UnitCost = 25m }
            ]
        });
        await stockCountService.SubmitAsync(tenantId, draft.StockCountId, new SubmitStockCountRequest { SubmittedByUserId = Guid.NewGuid() });
        await stockCountService.ApproveAsync(tenantId, draft.StockCountId, new ApproveStockCountRequest { ApprovedByUserId = Guid.NewGuid() });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            stockCountService.PostAsync(tenantId, draft.StockCountId, new PostStockCountRequest { PostedByUserId = Guid.NewGuid() }));

        db.ChangeTracker.Clear();
        var persistedCount = await db.StockCounts.AsNoTracking().SingleAsync(x => x.StockCountId == draft.StockCountId);
        Assert.Equal(StockCountStatus.Approved, persistedCount.Status);
        Assert.Empty(await db.InventoryTransactions.AsNoTracking()
            .Where(x => x.TenantId == tenantId
                        && x.SourceDocumentType == SourceDocumentType.StockAdjustment
                        && x.SourceDocumentId == draft.StockCountId)
            .ToListAsync());

        foreach (var product in setup.Products)
        {
            var balance = await db.InventoryBalances.AsNoTracking()
                .SingleAsync(x => x.TenantId == tenantId && x.ProductId == product.ProductId && x.WarehouseId == setup.Warehouse.WarehouseId);
            Assert.Equal(10m, balance.QuantityOnHand);
        }

        var issueValuation = await db.ProductValuationBalances.AsNoTracking()
            .SingleAsync(x => x.TenantId == tenantId && x.ProductId == setup.Products[0].ProductId && x.WarehouseId == setup.Warehouse.WarehouseId);
        Assert.Equal(10m, issueValuation.TotalQuantity);
        Assert.Equal(250m, issueValuation.TotalCostValue);
    }

    [Fact]
    public async Task PostAsync_RetainsInventoryLockUntilCountTransactionCompletes()
    {
        using var scope = _fixture.Services.CreateScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<DomainDbContext>();
        var posting = services.GetRequiredService<IInventoryPostingService>();
        var tenantId = TestConstants.TenantAlphaId;
        var setup = await CreateSetupAsync(db, tenantId, 1, ValuationMethod.WeightedAverage);
        var product = setup.Products[0];

        await posting.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.GoodsReceipt,
            SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentLineId = Guid.NewGuid(),
            MovementSequence = 1,
            ProductId = product.ProductId,
            WarehouseId = setup.Warehouse.WarehouseId,
            WarehouseLocationId = setup.Location.WarehouseLocationId,
            Quantity = 10m,
            UnitCost = 25m
        });

        var inventoryService = services.GetRequiredService<IInventoryService>();
        var gatedInventory = DispatchProxy.Create<IInventoryService, PauseAfterIssueProxy>();
        var proxy = (PauseAfterIssueProxy)(object)gatedInventory;
        proxy.Inner = inventoryService;
        var stockCountService = new StockCountService(
            db,
            gatedInventory,
            services.GetRequiredService<IUnitOfMeasureService>(),
            services.GetRequiredService<IInventoryValuationService>());
        var draft = await stockCountService.CreateDraftAsync(tenantId, new CreateStockCountDraftRequest
        {
            CompanyId = setup.Company.Id,
            WarehouseId = setup.Warehouse.WarehouseId,
            WarehouseLocationId = setup.Location.WarehouseLocationId,
            Lines = [new CreateStockCountLineRequest { ProductId = product.ProductId, PhysicalQuantity = 8m }]
        });
        await stockCountService.SubmitAsync(tenantId, draft.StockCountId, new SubmitStockCountRequest { SubmittedByUserId = Guid.NewGuid() });
        await stockCountService.ApproveAsync(tenantId, draft.StockCountId, new ApproveStockCountRequest { ApprovedByUserId = Guid.NewGuid() });

        var countPostingTask = stockCountService.PostAsync(
            tenantId,
            draft.StockCountId,
            new PostStockCountRequest { PostedByUserId = Guid.NewGuid() });
        await proxy.IssuePosted.Task.WaitAsync(TimeSpan.FromSeconds(15));

        using var competingScope = _fixture.Services.CreateScope();
        var competingPosting = competingScope.ServiceProvider.GetRequiredService<IInventoryPostingService>();
        var competingMovementId = Guid.NewGuid();
        var competingMovementTask = competingPosting.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.GoodsReceipt,
            SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
            SourceDocumentId = competingMovementId,
            SourceDocumentLineId = Guid.NewGuid(),
            MovementSequence = 1,
            ProductId = product.ProductId,
            WarehouseId = setup.Warehouse.WarehouseId,
            WarehouseLocationId = setup.Location.WarehouseLocationId,
            Quantity = 1m,
            UnitCost = 25m
        });

        try
        {
            var completedTask = await Task.WhenAny(competingMovementTask, Task.Delay(TimeSpan.FromMilliseconds(500)));
            Assert.NotSame(competingMovementTask, completedTask);
        }
        finally
        {
            proxy.ContinuePosting.TrySetResult();
        }

        var postedCount = await countPostingTask.WaitAsync(TimeSpan.FromSeconds(15));
        await competingMovementTask.WaitAsync(TimeSpan.FromSeconds(15));
        Assert.Equal(StockCountStatus.Posted, postedCount.Status);

        db.ChangeTracker.Clear();
        Assert.Single(await db.InventoryTransactions.AsNoTracking()
            .Where(x => x.TenantId == tenantId
                        && x.SourceDocumentType == SourceDocumentType.GoodsReceiptNote
                        && x.SourceDocumentId == competingMovementId)
            .ToListAsync());
        var finalBalance = await db.InventoryBalances.AsNoTracking().SingleAsync(
            x => x.TenantId == tenantId
                 && x.ProductId == product.ProductId
                 && x.WarehouseLocationId == setup.Location.WarehouseLocationId);
        Assert.Equal(9m, finalBalance.QuantityOnHand);
    }

    private static async Task<TestSetup> CreateSetupAsync(
        DomainDbContext db,
        Guid tenantId,
        int productCount,
        ValuationMethod valuationMethod = ValuationMethod.FIFO)
    {
        var category = await db.Categories.FirstAsync(x => x.TenantId == tenantId);
        var unit = await db.UnitsOfMeasure.FirstAsync(x => x.TenantId == tenantId && x.IsActive);
        var company = new Company
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = $"Stock Count Company {Guid.NewGuid():N}",
            Code = $"SCC-{Guid.NewGuid():N}"[..20],
            IsActive = true
        };
        var warehouse = Warehouse.Create(
            tenantId,
            $"SCWH-{Guid.NewGuid():N}"[..20],
            "Stock Count Warehouse",
            companyId: company.Id);
        var location = WarehouseLocation.Create(
            tenantId,
            warehouse.WarehouseId,
            null,
            $"SCLOC-{Guid.NewGuid():N}"[..20],
            "Stock Count Location");
        var products = Enumerable.Range(0, productCount)
            .Select(index => Product.Create(
                tenantId,
                $"SCP-{Guid.NewGuid():N}"[..20],
                $"Stock Count Product {index}-{Guid.NewGuid():N}",
                category.CategoryId,
                unit.Code,
                valuationMethod: valuationMethod,
                unitOfMeasureId: unit.UnitOfMeasureId))
            .ToList();

        db.AddRange(company, warehouse, location);
        db.Products.AddRange(products);
        await db.SaveChangesAsync();
        return new TestSetup(company, warehouse, location, products);
    }

    private sealed record TestSetup(
        Company Company,
        Warehouse Warehouse,
        WarehouseLocation Location,
        List<Product> Products);

    public class FailOnReceiptProxy : DispatchProxy
    {
        public IInventoryService Inner { get; set; } = null!;

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == nameof(IInventoryService.CreateStockReceiptAsync))
                throw new InvalidOperationException("Injected receipt failure.");

            if (targetMethod?.Name == nameof(IInventoryService.CreateStockIssueAsync))
                return Inner.CreateStockIssueAsync((Guid)args![0]!, (CreateStockIssueRequest)args[1]!);

            throw new NotSupportedException($"Unexpected inventory service call '{targetMethod?.Name}'.");
        }
    }

    public class PauseAfterIssueProxy : DispatchProxy
    {
        public IInventoryService Inner { get; set; } = null!;
        public TaskCompletionSource IssuePosted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ContinuePosting { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == nameof(IInventoryService.CreateStockIssueAsync))
                return PostIssueAndPauseAsync((Guid)args![0]!, (CreateStockIssueRequest)args[1]!);

            throw new NotSupportedException($"Unexpected inventory service call '{targetMethod?.Name}'.");
        }

        private async Task<List<StockIssueDto>> PostIssueAndPauseAsync(Guid tenantId, CreateStockIssueRequest request)
        {
            var issues = await Inner.CreateStockIssueAsync(tenantId, request);
            IssuePosted.TrySetResult();
            await ContinuePosting.Task;
            return issues;
        }
    }
}
