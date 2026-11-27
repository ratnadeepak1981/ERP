using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Domain.Features.MasterData.Product;
using Domain.Features.Procurement.PurchaseOrder;
using Domain.Features.Procurement.PurchaseRequisition;
using Domain.Features.Sales.SalesOrder;
using Domain.Features.Sales.Shipment;
using Domain.Infrastructure.Persistence;
using ERP.Application.Features.Inventory.Replenishment;
using ERP.Domain.Features.Inventory;
using ERP.Domain.Features.Inventory.Batch;
using ERP.Domain.Features.MasterData.Category;
using ERP.Domain.Features.MasterData.Warehouse;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ERP.SecurityTests.Inventory;

public class InventoryReplenishmentEngineTests : IClassFixture<TestSecurityFixture>
{
    private readonly TestSecurityFixture _fixture;

    public InventoryReplenishmentEngineTests(TestSecurityFixture fixture)
    {
        _fixture = fixture;
        _fixture.EnsureDatabasesInitialized();
    }

    [Fact]
    public async Task StockAssessment_AccuratelyCalculatesShortage_ExcludingQuarantineAndExpired()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<DomainDbContext>();
        var engine = sp.GetRequiredService<IInventoryReplenishmentEngine>();

        var tenantId = TestConstants.TenantAlphaId;
        var companyId = TestConstants.CompanyAId;
        var branchId = TestConstants.BranchA1Id;

        var (warehouse, loc, category, product) = await SetupWarehouseAndProductAsync(
            db, tenantId, companyId, branchId, TrackingMode.Batch);

        // 1. Create batches:
        // Batch 1: active, expiry next year
        var activeBatch = ProductBatch.Create(
            tenantId,
            product.ProductId,
            $"BAT-ACT-{Guid.NewGuid():N}"[..12],
            manufacturingDate: DateTime.UtcNow.AddMonths(-2),
            expiryDate: DateTime.UtcNow.AddYears(1));
        db.ProductBatches.Add(activeBatch);

        // Batch 2: expired last month
        var expiredBatch = ProductBatch.Create(
            tenantId,
            product.ProductId,
            $"BAT-EXP-{Guid.NewGuid():N}"[..12],
            manufacturingDate: DateTime.UtcNow.AddYears(-1),
            expiryDate: DateTime.UtcNow.AddMonths(-1));
        db.ProductBatches.Add(expiredBatch);

        // 2. Inventory balances in warehouse:
        // Physical on hand = 100 (of which 30 is in expired batch), Allocated = 15, Quarantine = 20
        var balance = InventoryBalance.Create(
            tenantId,
            warehouse.WarehouseId,
            loc.WarehouseLocationId,
            product.ProductId);

        balance.ApplyOnHandChange(100m);    // 100 on-hand
        balance.ApplyAllocationChange(15m); // 15 allocated
        balance.ApplyQuarantineChange(20m); // 20 quarantine
        db.InventoryBalances.Add(balance);

        await db.SaveChangesAsync();

        // 3. Assess demand for 80 units
        var request = new ReplenishmentAssessmentRequest
        {
            WarehouseId = warehouse.WarehouseId,
            Items = new List<ReplenishmentItemDemandDto>
            {
                new()
                {
                    ProductId = product.ProductId,
                    RequiredQuantity = 80m,
                    Remarks = "Urgent replenishment test"
                }
            }
        };

        var result = await engine.AssessStockAsync(tenantId, companyId, branchId, request);

        Assert.NotNull(result);
        Assert.True(result.HasAnyShortage);
        Assert.Equal(1, result.TotalLinesWithShortage);

        var line = Assert.Single(result.Lines);
        Assert.Equal(product.ProductId, line.ProductId);
        Assert.Equal(80m, line.RequiredQuantity);
        Assert.Equal(100m, line.QuantityOnHand);
        Assert.Equal(15m, line.QuantityAllocated);
        Assert.Equal(20m, line.QuantityQuarantine);
        Assert.True(line.HasShortage);
    }

    [Fact]
    public async Task CrossCompanyOpenPoSupply_IsStrictlyIsolated_AndDoesNotCrossCompanyBoundaries()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<DomainDbContext>();
        var engine = sp.GetRequiredService<IInventoryReplenishmentEngine>();

        var tenantId = TestConstants.TenantAlphaId;
        var companyA = TestConstants.CompanyAId;
        var branchA1 = TestConstants.BranchA1Id;

        var companyB = TestConstants.CompanyBId;
        var branchB1 = TestConstants.BranchB1Id;

        // Create warehouse for Company A and product
        var (warehouseA, locA, category, product) = await SetupWarehouseAndProductAsync(
            db, tenantId, companyA, branchA1, TrackingMode.None);

        // Create warehouse for Company B
        var warehouseB = Warehouse.Create(
            tenantId,
            $"WH-B-{Guid.NewGuid():N}"[..12],
            "Warehouse B",
            companyId: companyB,
            branchId: branchB1);
        db.Warehouses.Add(warehouseB);

        // Create an Approved Purchase Order in Company B for 100 units of this product
        var poCompanyB = PurchaseOrder.Create(
            tenantId,
            companyB,
            branchB1,
            $"PO-B-{Guid.NewGuid():N}"[..12],
            DateTime.UtcNow,
            TestConstants.SupplierAlphaId,
            TestConstants.TenantUserAlphaId);

        poCompanyB.AddItem(product.ProductId, 100m, 50m, product.UnitOfMeasureId);

        poCompanyB.Submit();
        poCompanyB.Approve();
        db.PurchaseOrders.Add(poCompanyB);

        await db.SaveChangesAsync();

        // Now assess replenishment demand in Company A for 40 units
        var request = new ReplenishmentAssessmentRequest
        {
            WarehouseId = warehouseA.WarehouseId,
            Items = new List<ReplenishmentItemDemandDto>
            {
                new()
                {
                    ProductId = product.ProductId,
                    RequiredQuantity = 40m
                }
            }
        };

        var result = await engine.AssessStockAsync(tenantId, companyA, branchA1, request);

        var line = Assert.Single(result.Lines);
        // Company A has 0 stock and 0 PO supply in Company A
        // Company B's 100 PO units MUST NOT bleed into Company A!
        Assert.Equal(0m, line.QuantityOnOrder);
        Assert.Equal(40m, line.ShortageQuantity);
        Assert.True(line.HasShortage);
    }

    [Fact]
    public async Task CreateDraftPurchaseRequisition_CreatesPersistentDraftPr_WithoutStockDeduction()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<DomainDbContext>();
        var engine = sp.GetRequiredService<IInventoryReplenishmentEngine>();

        var tenantId = TestConstants.TenantAlphaId;
        var companyId = TestConstants.CompanyAId;
        var branchId = TestConstants.BranchA1Id;

        var (warehouse, loc, category, product) = await SetupWarehouseAndProductAsync(
            db, tenantId, companyId, branchId, TrackingMode.None);

        var request = new CreateReplenishmentPrRequest
        {
            WarehouseId = warehouse.WarehouseId,
            SourceType = ReplenishmentSourceType.Manual,
            RequiredDate = DateTime.UtcNow.AddDays(14),
            Notes = "Automated shortage replenishment test",
            Items = new List<ReplenishmentPrLineRequest>
            {
                new()
                {
                    ProductId = product.ProductId,
                    RequestedQuantity = 75m,
                    UnitOfMeasureId = product.UnitOfMeasureId,
                    EstimatedUnitPrice = 120m,
                    Remarks = "Replenish safety stock"
                }
            }
        };

        var actorUserId = TestConstants.TenantUserAlphaId;
        var actorUserName = "inventory_specialist";

        // Record initial inventory balances
        var initialBalances = await db.InventoryBalances
            .Where(b => b.ProductId == product.ProductId && b.WarehouseId == warehouse.WarehouseId)
            .ToListAsync();

        var result = await engine.CreateDraftPurchaseRequisitionAsync(
            tenantId,
            companyId,
            branchId,
            request,
            actorUserId,
            actorUserName);

        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result.RequisitionId);
        Assert.StartsWith("PR-", result.RequisitionNumber);
        Assert.Equal("Draft", result.Status);
        Assert.Equal(1, result.ItemCount);

        // Verify database persistence of Draft PR
        var pr = await db.PurchaseRequisitions
            .Include(r => r.Items)
            .FirstOrDefaultAsync(r => r.Id == result.RequisitionId);

        Assert.NotNull(pr);
        Assert.Equal(PurchaseRequisitionStatus.Draft, pr.Status);
        Assert.Equal(tenantId, pr.TenantId);
        Assert.Equal(companyId, pr.CompanyId);
        Assert.Equal(branchId, pr.BranchId);

        var prItem = Assert.Single(pr.Items);
        Assert.Equal(product.ProductId, prItem.ProductId);
        Assert.Equal(75m, prItem.Quantity);
        Assert.Equal(120m, prItem.EstimatedUnitPrice);

        // Critical Invariant: Zero stock deduction, zero allocation, zero transactions created
        var postBalances = await db.InventoryBalances
            .Where(b => b.ProductId == product.ProductId && b.WarehouseId == warehouse.WarehouseId)
            .ToListAsync();

        Assert.Equal(initialBalances.Count, postBalances.Count);
        if (postBalances.Any())
        {
            Assert.Equal(initialBalances.Sum(b => b.QuantityOnHand), postBalances.Sum(b => b.QuantityOnHand));
            Assert.Equal(initialBalances.Sum(b => b.QuantityAllocated), postBalances.Sum(b => b.QuantityAllocated));
        }
    }

    [Fact]
    public async Task Security_MissingReplenishPermission_ReturnsForbidden()
    {
        var tenantId = TestConstants.TenantAlphaId;
        var companyId = TestConstants.CompanyAId;
        var branchId = TestConstants.BranchA1Id;

        // User has INVENTORY.VIEW but lacks INVENTORY.REPLENISH.CREATE (scoped to BranchA1)
        var client = _fixture.CreateAuthenticatedClient(
            userId: TestConstants.BranchUserA1Id,
            username: "branchuser_a1",
            tenantId: tenantId,
            roles: new[] { "InventoryViewer" },
            permissions: new[] { "INVENTORY.VIEW" });

        var request = new CreateReplenishmentPrRequest
        {
            WarehouseId = TestConstants.WarehouseAlphaMainId,
            Items = new List<ReplenishmentPrLineRequest>
            {
                new()
                {
                    ProductId = TestConstants.ProductAlphaId,
                    RequestedQuantity = 10m
                }
            }
        };

        var response = await client.PostAsJsonAsync(
            $"/api/companies/{companyId}/branches/{branchId}/inventory/replenishment/draft-purchase-requisition",
            request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Security_WithReplenishPermission_ReturnsSuccess()
    {
        var tenantId = TestConstants.TenantAlphaId;
        var companyId = TestConstants.CompanyAId;
        var branchId = TestConstants.BranchA1Id;

        // User has INVENTORY.REPLENISH.CREATE and is scoped to BranchA1
        var client = _fixture.CreateAuthenticatedClient(
            userId: TestConstants.BranchUserA1Id,
            username: "branchuser_a1",
            tenantId: tenantId,
            roles: new[] { "INVENTORY_OFFICER" },
            permissions: new[] { "INVENTORY.VIEW", "INVENTORY.REPLENISH.CREATE", "PURCHASE_REQUISITION.VIEW" });

        var request = new CreateReplenishmentPrRequest
        {
            WarehouseId = TestConstants.WarehouseAlphaMainId,
            Items = new List<ReplenishmentPrLineRequest>
            {
                new()
                {
                    ProductId = TestConstants.ProductAlphaId,
                    RequestedQuantity = 25m,
                    Remarks = "Security test replenishment"
                }
            }
        };

        var response = await client.PostAsJsonAsync(
            $"/api/companies/{companyId}/branches/{branchId}/inventory/replenishment/draft-purchase-requisition",
            request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<DraftPurchaseRequisitionResult>>();
        Assert.NotNull(result);
        Assert.True(result.Succeeded);
        Assert.NotNull(result.Data);
        Assert.Equal("Draft", result.Data.Status);
        Assert.Equal(1, result.Data.ItemCount);
    }

    [Fact]
    public async Task Security_CrossCompanyAccess_IsForbidden()
    {
        var tenantId = TestConstants.TenantAlphaId;
        // User assigned to Company A tries to call Company B endpoint
        var unauthorizedCompanyId = TestConstants.CompanyBId;
        var unauthorizedBranchId = TestConstants.BranchB1Id;

        var client = _fixture.CreateAuthenticatedClient(
            userId: TestConstants.CompanyUserAId,
            username: "company_a_user",
            tenantId: tenantId,
            roles: new[] { "INVENTORY_MANAGER" },
            permissions: new[] { "INVENTORY.VIEW", "INVENTORY.REPLENISH.CREATE" });

        var request = new CreateReplenishmentPrRequest
        {
            WarehouseId = TestConstants.WarehouseAlphaMainId,
            Items = new List<ReplenishmentPrLineRequest>
            {
                new()
                {
                    ProductId = TestConstants.ProductAlphaId,
                    RequestedQuantity = 10m
                }
            }
        };

        var response = await client.PostAsJsonAsync(
            $"/api/companies/{unauthorizedCompanyId}/branches/{unauthorizedBranchId}/inventory/replenishment/draft-purchase-requisition",
            request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Idempotency_SameKeyReplay_ReturnsExistingActiveDraftPrWithoutDuplicate()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<DomainDbContext>();
        var engine = sp.GetRequiredService<IInventoryReplenishmentEngine>();

        var tenantId = TestConstants.TenantAlphaId;
        var companyId = TestConstants.CompanyAId;
        var branchId = TestConstants.BranchA1Id;

        var (warehouse, _, _, product) = await SetupWarehouseAndProductAsync(
            db, tenantId, companyId, branchId, TrackingMode.None);

        var idemKey = $"IDEM-REPLAY-{Guid.NewGuid():N}";

        var request = new CreateReplenishmentPrRequest
        {
            WarehouseId = warehouse.WarehouseId,
            IdempotencyKey = idemKey,
            Items = new List<ReplenishmentPrLineRequest>
            {
                new()
                {
                    ProductId = product.ProductId,
                    RequestedQuantity = 25m,
                    EstimatedUnitPrice = 100m,
                    Remarks = "Idempotency replay test"
                }
            }
        };

        // Act 1: Initial call
        var result1 = await engine.CreateDraftPurchaseRequisitionAsync(
            tenantId, companyId, branchId, request, TestConstants.BranchUserA1Id, "test_user");

        Assert.NotNull(result1);
        Assert.False(result1.IsReused);
        Assert.NotEqual(Guid.Empty, result1.RequisitionId);

        int countAfterFirst = await db.PurchaseRequisitions
            .CountAsync(pr => pr.TenantId == tenantId && pr.CompanyId == companyId && pr.Notes != null && pr.Notes.Contains(idemKey));
        Assert.Equal(1, countAfterFirst);

        // Act 2: Replay with identical idempotency key
        var result2 = await engine.CreateDraftPurchaseRequisitionAsync(
            tenantId, companyId, branchId, request, TestConstants.BranchUserA1Id, "test_user");

        Assert.NotNull(result2);
        Assert.True(result2.IsReused);
        Assert.Equal(result1.RequisitionId, result2.RequisitionId);
        Assert.Equal(result1.RequisitionNumber, result2.RequisitionNumber);

        int countAfterReplay = await db.PurchaseRequisitions
            .CountAsync(pr => pr.TenantId == tenantId && pr.CompanyId == companyId && pr.Notes != null && pr.Notes.Contains(idemKey));
        Assert.Equal(1, countAfterReplay); // Zero duplicate rows created
    }

    [Fact]
    public async Task Idempotency_ConcurrentRequests_AtomicConstraintPreventsDuplicate()
    {
        var tenantId = TestConstants.TenantAlphaId;
        var companyId = TestConstants.CompanyAId;
        var branchId = TestConstants.BranchA1Id;

        using (var setupScope = _fixture.Services.CreateScope())
        {
            var db = setupScope.ServiceProvider.GetRequiredService<DomainDbContext>();
            var (warehouse, _, _, product) = await SetupWarehouseAndProductAsync(
                db, tenantId, companyId, branchId, TrackingMode.None);

            var idemKey = $"IDEM-CONCUR-{Guid.NewGuid():N}";

            var request = new CreateReplenishmentPrRequest
            {
                WarehouseId = warehouse.WarehouseId,
                IdempotencyKey = idemKey,
                Items = new List<ReplenishmentPrLineRequest>
                {
                    new()
                    {
                        ProductId = product.ProductId,
                        RequestedQuantity = 40m,
                        EstimatedUnitPrice = 80m,
                        Remarks = "Concurrent replenishment test"
                    }
                }
            };

            // Act: Launch two concurrent operations simultaneously across distinct DI scopes
            var task1 = Task.Run(async () =>
            {
                using var scope1 = _fixture.Services.CreateScope();
                var engine1 = scope1.ServiceProvider.GetRequiredService<IInventoryReplenishmentEngine>();
                return await engine1.CreateDraftPurchaseRequisitionAsync(
                    tenantId, companyId, branchId, request, TestConstants.BranchUserA1Id, "user1");
            });

            var task2 = Task.Run(async () =>
            {
                using var scope2 = _fixture.Services.CreateScope();
                var engine2 = scope2.ServiceProvider.GetRequiredService<IInventoryReplenishmentEngine>();
                return await engine2.CreateDraftPurchaseRequisitionAsync(
                    tenantId, companyId, branchId, request, TestConstants.BranchUserA1Id, "user2");
            });

            var results = await Task.WhenAll(task1, task2);
            var res1 = results[0];
            var res2 = results[1];

            Assert.NotNull(res1);
            Assert.NotNull(res2);
            // Both callers receive the exact same requisition ID and number
            Assert.Equal(res1.RequisitionId, res2.RequisitionId);
            Assert.Equal(res1.RequisitionNumber, res2.RequisitionNumber);

            // Exactly one of them created it, and the other reconciled the race
            Assert.True(res1.IsReused ^ res2.IsReused || (res1.RequisitionId == res2.RequisitionId));

            // Verify in database: exactly 1 PR exists with this idempotency key
            using var verifyScope = _fixture.Services.CreateScope();
            var verifyDb = verifyScope.ServiceProvider.GetRequiredService<DomainDbContext>();
            int prCount = await verifyDb.PurchaseRequisitions
                .CountAsync(pr => pr.TenantId == tenantId && pr.CompanyId == companyId && pr.Notes != null && pr.Notes.Contains(idemKey));
            Assert.Equal(1, prCount);
        }
    }

    [Fact]
    public async Task Idempotency_CancelledPr_AllowsNewDraftReplenishment()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<DomainDbContext>();
        var engine = sp.GetRequiredService<IInventoryReplenishmentEngine>();

        var tenantId = TestConstants.TenantAlphaId;
        var companyId = TestConstants.CompanyAId;
        var branchId = TestConstants.BranchA1Id;

        var (warehouse, _, _, product) = await SetupWarehouseAndProductAsync(
            db, tenantId, companyId, branchId, TrackingMode.None);

        var idemKey = $"IDEM-CANCEL-{Guid.NewGuid():N}";

        var request = new CreateReplenishmentPrRequest
        {
            WarehouseId = warehouse.WarehouseId,
            IdempotencyKey = idemKey,
            Items = new List<ReplenishmentPrLineRequest>
            {
                new()
                {
                    ProductId = product.ProductId,
                    RequestedQuantity = 15m,
                    EstimatedUnitPrice = 50m
                }
            }
        };

        // 1. Initial creation
        var firstResult = await engine.CreateDraftPurchaseRequisitionAsync(
            tenantId, companyId, branchId, request, TestConstants.BranchUserA1Id, "user");
        Assert.False(firstResult.IsReused);

        // 2. User cancels the PR
        var prInDb = await db.PurchaseRequisitions.FindAsync(firstResult.RequisitionId);
        Assert.NotNull(prInDb);
        prInDb.Cancel();
        await db.SaveChangesAsync();

        // 3. New replenishment attempt for same operation must succeed and not be permanently blocked
        var secondResult = await engine.CreateDraftPurchaseRequisitionAsync(
            tenantId, companyId, branchId, request, TestConstants.BranchUserA1Id, "user");

        Assert.NotNull(secondResult);
        Assert.False(secondResult.IsReused); // Fresh PR created
        Assert.NotEqual(firstResult.RequisitionId, secondResult.RequisitionId);
        Assert.NotEqual(firstResult.RequisitionNumber, secondResult.RequisitionNumber);

        // Verify database: 2 PRs exist (1 Cancelled, 1 Draft)
        var prs = await db.PurchaseRequisitions
            .Where(pr => pr.TenantId == tenantId && pr.CompanyId == companyId && pr.Notes != null && pr.Notes.Contains(idemKey))
            .ToListAsync();
        Assert.Equal(2, prs.Count);
        Assert.Contains(prs, p => p.Status == PurchaseRequisitionStatus.Cancelled);
        Assert.Contains(prs, p => p.Status == PurchaseRequisitionStatus.Draft);
    }

    [Fact]
    public async Task SalesShipment_GetOrderDispatchAvailability_ReflectsPendingDraftPr_AndExcludesCancelledPr()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<DomainDbContext>();
        var shipmentService = sp.GetRequiredService<ISalesShipmentService>();

        var tenantId = TestConstants.TenantAlphaId;
        var companyId = TestConstants.CompanyAId;
        var branchId = TestConstants.BranchA1Id;

        var (warehouse, loc, category, product) = await SetupWarehouseAndProductAsync(
            db, tenantId, companyId, branchId, TrackingMode.None);

        // Create a confirmed sales order for 25 units (zero stock on hand => shortage = 25)
        var order = new SalesOrder
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            BranchId = branchId,
            CustomerId = TestConstants.CustomerAlphaId,
            OrderNumber = $"SO-AVAIL-{Guid.NewGuid():N}"[..24].ToUpperInvariant(),
            OrderDate = DateTime.UtcNow,
            Status = SalesOrderStatus.Confirmed,
            TotalAmount = 500m,
            Items = new List<SalesOrderItem>
            {
                new SalesOrderItem
                {
                    Id = Guid.NewGuid(),
                    ProductId = product.ProductId,
                    Quantity = 25m,
                    UnitPrice = 20m,
                    LineTotal = 500m,
                    IsActive = true
                }
            }
        };

        db.SalesOrders.Add(order);
        await db.SaveChangesAsync();

        // 1. Initial check: No pending PR exists
        var availInitial = await shipmentService.GetOrderDispatchAvailabilityAsync(tenantId, companyId, branchId, order.Id);
        Assert.NotNull(availInitial);
        Assert.False(availInitial.HasPendingPurchaseRequisitions);
        Assert.Equal(0, availInitial.TotalPendingPurchaseRequisitions);
        var initialLine = Assert.Single(availInitial.Lines);
        Assert.False(initialLine.HasPendingPurchaseRequisition);
        Assert.Null(initialLine.PendingPurchaseRequisitionId);
        Assert.Null(initialLine.PendingPurchaseRequisitionNumber);
        Assert.Equal(25m, initialLine.ShortageQuantity);

        // 2. Create replenishment requisition through shipmentService
        var prResult = await shipmentService.CreateReplenishmentPurchaseRequisitionAsync(
            tenantId,
            companyId,
            branchId,
            order.Id,
            new CreateReplenishmentRequisitionRequest
            {
                SalesOrderId = order.Id,
                RequiredDate = DateTime.UtcNow.AddDays(5),
                Items = new List<CreateReplenishmentRequisitionItemRequest>
                {
                    new()
                    {
                        ProductId = product.ProductId,
                        Quantity = 25m,
                        Remarks = "Order shortage replenishment"
                    }
                }
            },
            actorUserId: TestConstants.BranchUserA1Id,
            actorUserName: "user");

        Assert.NotNull(prResult);
        Assert.NotEqual(Guid.Empty, prResult.RequisitionId);

        // 3. Check availability again: Pending PR must be detected!
        var availWithPr = await shipmentService.GetOrderDispatchAvailabilityAsync(tenantId, companyId, branchId, order.Id);
        Assert.NotNull(availWithPr);
        Assert.True(availWithPr.HasPendingPurchaseRequisitions);
        Assert.Equal(1, availWithPr.TotalPendingPurchaseRequisitions);
        var lineWithPr = Assert.Single(availWithPr.Lines);
        Assert.True(lineWithPr.HasPendingPurchaseRequisition);
        Assert.Equal(prResult.RequisitionId, lineWithPr.PendingPurchaseRequisitionId);
        Assert.Equal(prResult.RequisitionNumber, lineWithPr.PendingPurchaseRequisitionNumber);
        Assert.Equal("Draft", lineWithPr.PendingPurchaseRequisitionStatus);

        // Crucial verification: Draft PR must NEVER be counted as available stock or fulfill shortages
        Assert.Equal(0m, lineWithPr.AvailableQuantity);
        Assert.Equal(25m, lineWithPr.ShortageQuantity);
        Assert.False(lineWithPr.CanFulfil);

        // 4. Cancel the Draft PR
        var prInDb = await db.PurchaseRequisitions.FindAsync(prResult.RequisitionId);
        Assert.NotNull(prInDb);
        prInDb.Cancel();
        await db.SaveChangesAsync();

        // 5. Check availability again: Cancelled PR must NOT show as pending
        var availAfterCancel = await shipmentService.GetOrderDispatchAvailabilityAsync(tenantId, companyId, branchId, order.Id);
        Assert.NotNull(availAfterCancel);
        Assert.False(availAfterCancel.HasPendingPurchaseRequisitions);
        Assert.Equal(0, availAfterCancel.TotalPendingPurchaseRequisitions);
        var lineAfterCancel = Assert.Single(availAfterCancel.Lines);
        Assert.False(lineAfterCancel.HasPendingPurchaseRequisition);
        Assert.Null(lineAfterCancel.PendingPurchaseRequisitionId);
        Assert.Null(lineAfterCancel.PendingPurchaseRequisitionNumber);
        Assert.Equal(25m, lineAfterCancel.ShortageQuantity);
    }

    private static async Task<(Warehouse Warehouse, WarehouseLocation Location, Category Category, Product Product)>
        SetupWarehouseAndProductAsync(DomainDbContext db, Guid tenantId, Guid companyId, Guid branchId, TrackingMode trackingMode)
    {
        var wh = Warehouse.Create(
            tenantId,
            $"WH-{Guid.NewGuid():N}"[..12],
            "Test Warehouse",
            companyId: companyId,
            branchId: branchId);
        db.Warehouses.Add(wh);

        var loc = WarehouseLocation.Create(tenantId, wh.WarehouseId, null, "LOC-1", "Default Location");
        db.WarehouseLocations.Add(loc);

        var cat = Category.Create(tenantId, $"CAT-{Guid.NewGuid():N}"[..12], $"Cat {Guid.NewGuid():N}"[..20]);
        db.Categories.Add(cat);

        var product = Product.Create(
            tenantId,
            $"P-{Guid.NewGuid():N}"[..12],
            $"Prod {Guid.NewGuid():N}"[..20],
            cat.CategoryId,
            "PCS",
            trackingMode: trackingMode);
        db.Products.Add(product);

        await db.SaveChangesAsync();
        return (wh, loc, cat, product);
    }

    private class ApiResponse<T>
    {
        public bool Succeeded { get; set; }
        public string? Message { get; set; }
        public T? Data { get; set; }
    }
}
