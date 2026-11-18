using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domain.Features.MasterData.Branch;
using Domain.Features.MasterData.Company;
using Domain.Features.MasterData.Currency;
using Domain.Features.MasterData.Product;
using Domain.Features.MasterData.TaxCode;
using Domain.Features.MasterData.UnitOfMeasure;
using Domain.Features.Procurement.Approval;
using Domain.Features.Procurement.PurchaseOrder;
using Domain.Features.Procurement.PurchaseRequisition;
using Domain.Features.Procurement.RequestForQuotation;
using Domain.Features.Procurement.Route;
using Domain.Infrastructure.Persistence;
using ERP.Domain.Features.MasterData.Supplier;
using ERP.Infrastructure.Persistence.Auditing;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ERP.SecurityTests;

public class ProcurementRouteConsistencyTests
{
    private static DomainDbContext CreateInMemoryDomainDb()
    {
        var options = new DbContextOptionsBuilder<DomainDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new DomainDbContext(options);
    }

    private static async Task<(Company Company, Branch Branch, Product Product, UnitOfMeasure Uom, Supplier Supplier, Currency Currency)> SeedBaseAsync(
        DomainDbContext db, Guid tenantId, Guid companyId, Guid branchId)
    {
        var currency = Currency.Create(tenantId, "USD", "US Dollar", "$", 2, Guid.NewGuid());

        var company = new Company
        {
            Id = companyId,
            TenantId = tenantId,
            Name = "ApexCorp Global",
            BaseCurrencyId = currency.CurrencyId,
            BaseCurrency = currency
        };

        var branch = new Branch
        {
            Id = branchId,
            CompanyId = companyId,
            Name = "Central Hub"
        };

        var uom = UnitOfMeasure.Create(tenantId, "PCS", "Pieces");

        var product = Product.Create(
            tenantId: tenantId,
            productCode: "PRD-STEEL-001",
            productName: "High Grade Steel Beams",
            categoryId: Guid.NewGuid(),
            unitOfMeasure: uom.Code,
            standardCost: 50.0m,
            unitOfMeasureId: uom.UnitOfMeasureId);

        var supplier = Supplier.Create(
            tenantId: tenantId,
            supplierCode: "SUP-APEX-01",
            supplierName: "Apex Industrial Supplies",
            email: "apex@supplies.test",
            currencyId: currency.CurrencyId);

        await db.Companies.AddAsync(company);
        await db.Branches.AddAsync(branch);
        await db.UnitsOfMeasure.AddAsync(uom);
        await db.Currencies.AddAsync(currency);
        await db.Products.AddAsync(product);
        await db.Suppliers.AddAsync(supplier);
        await db.SaveChangesAsync();

        return (company, branch, product, uom, supplier, currency);
    }

    private static async Task<PurchaseRequisition> SeedApprovedPrAsync(
        DomainDbContext db, Guid tenantId, Guid companyId, Guid branchId, Product product, UnitOfMeasure uom, decimal qty = 100m, decimal price = 45m)
    {
        var req = PurchaseRequisition.Create(
            tenantId,
            companyId,
            branchId,
            $"PR-{Guid.NewGuid().ToString().Substring(0, 8)}",
            Guid.NewGuid(),
            DateTime.UtcNow,
            DateTime.UtcNow.AddDays(15),
            "Critical materials required for Q4 infrastructure expansion");

        req.AddItem(product.ProductId, qty, price, uom.UnitOfMeasureId, "High priority specification");
        req.Submit();
        req.Approve();

        await db.PurchaseRequisitions.AddAsync(req);
        await db.SaveChangesAsync();
        return req;
    }

    [Fact]
    public async Task Scenario1_ApprovedPr_CanUseExistingDirectPoRoute()
    {
        using var db = CreateInMemoryDomainDb();
        var tenantId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var baseSeed = await SeedBaseAsync(db, tenantId, companyId, branchId);
        var pr = await SeedApprovedPrAsync(db, tenantId, companyId, branchId, baseSeed.Product, baseSeed.Uom);

        var tokenService = new RfqTokenService(db);
        var rfqService = new RFQService(db, tokenService);
        var routeService = new ProcurementRouteService(db, rfqService);

        // Act: Check route status
        var status = await routeService.GetRouteStatusAsync(tenantId, companyId, branchId, pr.Id);

        // Assert: Both routes available
        Assert.Equal(ProcurementRouteType.None, status.SelectedRoute);
        Assert.True(status.CanCreateDirectPO);
        Assert.True(status.CanCreateRFQ);
        Assert.Null(status.BlockReason);

        // Validate direct PO pre-flight
        await routeService.ValidateDirectPoRouteAsync(tenantId, companyId, branchId, pr.Id);
    }

    [Fact]
    public async Task Scenario2_ExistingDirectPo_BehavesExactlyAsBefore_AndSupportsPartialProcurement()
    {
        using var db = CreateInMemoryDomainDb();
        var tenantId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var baseSeed = await SeedBaseAsync(db, tenantId, companyId, branchId);
        var pr = await SeedApprovedPrAsync(db, tenantId, companyId, branchId, baseSeed.Product, baseSeed.Uom, qty: 100m);

        var tokenService = new RfqTokenService(db);
        var rfqService = new RFQService(db, tokenService);
        var routeService = new ProcurementRouteService(db, rfqService);

        // Create partial Direct PO (40 units out of 100)
        var po = PurchaseOrder.Create(tenantId, companyId, branchId, "PO-DIRECT-001", DateTime.UtcNow, baseSeed.Supplier.SupplierId);
        var prItem = pr.Items.First();
        po.AddItem(baseSeed.Product.ProductId, 40m, 45m, baseSeed.Uom.UnitOfMeasureId, notes: $"[PR: {pr.RequisitionNumber} Line: {prItem.Id}]");
        await db.PurchaseOrders.AddAsync(po);
        await db.SaveChangesAsync();

        // Act: Get route status
        var status = await routeService.GetRouteStatusAsync(tenantId, companyId, branchId, pr.Id);

        // Assert: Route is DirectPO, 60 units remaining, partial PO creation remains allowed!
        Assert.Equal(ProcurementRouteType.DirectPO, status.SelectedRoute);
        Assert.Equal(100m, status.TotalRequestedQuantity);
        Assert.Equal(40m, status.TotalOrderedQuantity);
        Assert.Equal(60m, status.TotalRemainingQuantity);
        Assert.True(status.CanCreateDirectPO);
        Assert.False(status.CanCreateRFQ); // Exclusivity prevents RFQ creation
    }

    [Fact]
    public async Task Scenario3And4_ApprovedPr_CanCreateRfq_AndDetailsTransferCorrectly()
    {
        using var db = CreateInMemoryDomainDb();
        var tenantId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var baseSeed = await SeedBaseAsync(db, tenantId, companyId, branchId);
        var pr = await SeedApprovedPrAsync(db, tenantId, companyId, branchId, baseSeed.Product, baseSeed.Uom, qty: 250m, price: 48.5m);

        var tokenService = new RfqTokenService(db);
        var rfqService = new RFQService(db, tokenService);
        var routeService = new ProcurementRouteService(db, rfqService);

        var userId = Guid.NewGuid();
        var rfqRequest = new CreateRfqFromRequisitionRequest
        {
            Title = "RFQ for Structural Construction",
            ClosingDate = DateTime.UtcNow.AddDays(10),
            DeliveryTerms = "FOB Destination",
            Instructions = "Supplier must provide material test certificate.",
            SupplierIds = new List<Guid> { baseSeed.Supplier.SupplierId }
        };

        // Act
        var createdRfq = await routeService.CreateRfqFromRequisitionAsync(
            tenantId, companyId, branchId, pr.Id, rfqRequest, userId);

        // Assert: RFQ created successfully
        Assert.NotNull(createdRfq);
        Assert.Equal("RFQ for Structural Construction", createdRfq.Title);
        Assert.Single(createdRfq.Lines);

        var line = createdRfq.Lines[0];
        Assert.Equal(baseSeed.Product.ProductId, line.ProductId);
        Assert.Equal(250m, line.Quantity);
        Assert.Equal(baseSeed.Uom.UnitOfMeasureId, line.UnitOfMeasureId);
        Assert.Equal(48.5m, line.TargetUnitPrice);
        Assert.Equal("High priority specification", line.TechnicalSpecifications);
        Assert.Contains(pr.RequisitionNumber, line.Notes);

        // Assert: Instructions contain source PR reference
        Assert.Contains($"[PR: {pr.RequisitionNumber}", createdRfq.Instructions);
        Assert.Contains("Supplier must provide material test certificate.", createdRfq.Instructions);

        // Assert: Audit record created for route selection
        var audit = await db.DomainAuditRecords.FirstOrDefaultAsync(
            a => a.EntityName == "PurchaseRequisition" && a.EntityId == pr.Id.ToString() && a.Action == "RouteSelected_RFQ");
        Assert.NotNull(audit);
        Assert.Equal(userId, audit.UserId);
    }

    [Fact]
    public async Task Scenario5And6_RfqRoute_MaintainsTraceability_AndPreventsConflictingDirectPoCreation()
    {
        using var db = CreateInMemoryDomainDb();
        var tenantId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var baseSeed = await SeedBaseAsync(db, tenantId, companyId, branchId);
        var pr = await SeedApprovedPrAsync(db, tenantId, companyId, branchId, baseSeed.Product, baseSeed.Uom);

        var tokenService = new RfqTokenService(db);
        var rfqService = new RFQService(db, tokenService);
        var routeService = new ProcurementRouteService(db, rfqService);

        // Create RFQ from PR
        var createdRfq = await routeService.CreateRfqFromRequisitionAsync(
            tenantId, companyId, branchId, pr.Id, new CreateRfqFromRequisitionRequest
            {
                ClosingDate = DateTime.UtcNow.AddDays(7)
            }, Guid.NewGuid());

        // Check route status
        var status = await routeService.GetRouteStatusAsync(tenantId, companyId, branchId, pr.Id);
        Assert.Equal(ProcurementRouteType.RFQ, status.SelectedRoute);
        Assert.False(status.CanCreateDirectPO);
        Assert.False(status.CanCreateRFQ);
        Assert.NotNull(status.LinkedRfq);
        Assert.Equal(createdRfq.RFQNumber, status.LinkedRfq.RFQNumber);
        Assert.Contains("Direct Purchase Order creation is prohibited by procurement route exclusivity", status.BlockReason);

        // Attempt direct PO creation for this PR requirement -> MUST BE REJECTED
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            routeService.ValidateDirectPoRouteAsync(tenantId, companyId, branchId, pr.Id));
        Assert.Contains("Direct Purchase Order creation is prohibited while an active RFQ exists", ex.Message);
    }

    [Fact]
    public async Task Scenario7_DirectPoRoute_PreventsConflictingRfqCreation()
    {
        using var db = CreateInMemoryDomainDb();
        var tenantId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var baseSeed = await SeedBaseAsync(db, tenantId, companyId, branchId);
        var pr = await SeedApprovedPrAsync(db, tenantId, companyId, branchId, baseSeed.Product, baseSeed.Uom);

        var tokenService = new RfqTokenService(db);
        var rfqService = new RFQService(db, tokenService);
        var routeService = new ProcurementRouteService(db, rfqService);

        // Direct PO created
        var po = PurchaseOrder.Create(tenantId, companyId, branchId, "PO-DIRECT-999", DateTime.UtcNow, baseSeed.Supplier.SupplierId);
        po.AddItem(baseSeed.Product.ProductId, 100m, 45m, baseSeed.Uom.UnitOfMeasureId, notes: $"[PR: {pr.RequisitionNumber} Line: {pr.Items.First().Id}]");
        await db.PurchaseOrders.AddAsync(po);
        await db.SaveChangesAsync();

        // Attempting to create RFQ for the same requirement -> MUST BE REJECTED
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            routeService.CreateRfqFromRequisitionAsync(tenantId, companyId, branchId, pr.Id, new CreateRfqFromRequisitionRequest
            {
                ClosingDate = DateTime.UtcNow.AddDays(7)
            }, Guid.NewGuid()));

        Assert.Contains("Conflicting RFQ creation is prohibited by procurement route exclusivity", ex.Message);
    }

    [Fact]
    public async Task Scenario8_FailedAttempts_DoNotLockThePr()
    {
        using var db = CreateInMemoryDomainDb();
        var tenantId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var baseSeed = await SeedBaseAsync(db, tenantId, companyId, branchId);
        var pr = await SeedApprovedPrAsync(db, tenantId, companyId, branchId, baseSeed.Product, baseSeed.Uom);

        var tokenService = new RfqTokenService(db);
        var rfqService = new RFQService(db, tokenService);
        var routeService = new ProcurementRouteService(db, rfqService);

        // Attempt RFQ creation with invalid past date
        await Assert.ThrowsAsync<ArgumentException>(() =>
            routeService.CreateRfqFromRequisitionAsync(tenantId, companyId, branchId, pr.Id, new CreateRfqFromRequisitionRequest
            {
                ClosingDate = DateTime.UtcNow.AddDays(-2) // Past date!
            }, Guid.NewGuid()));

        // Assert: PR is still Approved and both routes remain available
        var persistedPr = await db.PurchaseRequisitions.FindAsync(pr.Id);
        Assert.NotNull(persistedPr);
        Assert.Equal(PurchaseRequisitionStatus.Approved, persistedPr.Status);

        var status = await routeService.GetRouteStatusAsync(tenantId, companyId, branchId, pr.Id);
        Assert.Equal(ProcurementRouteType.None, status.SelectedRoute);
        Assert.True(status.CanCreateDirectPO);
        Assert.True(status.CanCreateRFQ);
    }

    [Fact]
    public async Task Scenario9_CancelledRfq_AllowsAuditedRouteChange_ToDirectPo()
    {
        using var db = CreateInMemoryDomainDb();
        var tenantId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var baseSeed = await SeedBaseAsync(db, tenantId, companyId, branchId);
        var pr = await SeedApprovedPrAsync(db, tenantId, companyId, branchId, baseSeed.Product, baseSeed.Uom);

        var tokenService = new RfqTokenService(db);
        var rfqService = new RFQService(db, tokenService);
        var routeService = new ProcurementRouteService(db, rfqService);

        // 1. Create RFQ from PR
        var createdRfq = await routeService.CreateRfqFromRequisitionAsync(
            tenantId, companyId, branchId, pr.Id, new CreateRfqFromRequisitionRequest
            {
                ClosingDate = DateTime.UtcNow.AddDays(7)
            }, Guid.NewGuid());

        // 2. Buyer cancels the RFQ
        await rfqService.CancelRfqAsync(createdRfq.Id, "Supplier bids unacceptable, switching to direct purchase", Guid.NewGuid(), tenantId);

        // 3. Status shows RFQ is cancelled and route can be reset
        var status = await routeService.GetRouteStatusAsync(tenantId, companyId, branchId, pr.Id);
        Assert.True(status.CanResetRoute);

        // 4. Reset route with mandatory audit reason
        var userId = Guid.NewGuid();
        var resetResult = await routeService.ResetCancelledRfqRouteAsync(
            tenantId, companyId, branchId, pr.Id, new ResetProcurementRouteRequest
            {
                Reason = "Approved by Procurement Director to switch to direct pre-negotiated contract PO."
            }, userId);

        Assert.True(resetResult);

        // Verify audit record of route change
        var audit = await db.DomainAuditRecords.FirstOrDefaultAsync(
            a => a.EntityName == "PurchaseRequisition" && a.EntityId == pr.Id.ToString() && a.Action == "RouteChange_RFQReset");
        Assert.NotNull(audit);
        Assert.Equal(userId, audit.UserId);
        Assert.Contains("Approved by Procurement Director", audit.NewValues);

        // 5. Direct PO validation now succeeds!
        await routeService.ValidateDirectPoRouteAsync(tenantId, companyId, branchId, pr.Id);
    }

    [Fact]
    public async Task Scenario11And12_AwardedQuotation_CanCreatePo_WithCommercialValuesTransferred()
    {
        using var db = CreateInMemoryDomainDb();
        var tenantId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var baseSeed = await SeedBaseAsync(db, tenantId, companyId, branchId);
        var pr = await SeedApprovedPrAsync(db, tenantId, companyId, branchId, baseSeed.Product, baseSeed.Uom);

        var tokenService = new RfqTokenService(db);
        var rfqService = new RFQService(db, tokenService);
        var routeService = new ProcurementRouteService(db, rfqService);
        var quotationService = new SupplierQuotationService(db, tokenService);

        // 1. Create RFQ from PR
        var rfq = await routeService.CreateRfqFromRequisitionAsync(
            tenantId, companyId, branchId, pr.Id, new CreateRfqFromRequisitionRequest
            {
                ClosingDate = DateTime.UtcNow.AddDays(7)
            }, Guid.NewGuid());

        // 2. Record supplier quotation
        var quote = await quotationService.RecordManualQuotationAsync(
            rfq.Id,
            new RecordManualQuotationRequest
            {
                SupplierId = baseSeed.Supplier.SupplierId,
                QuotationReference = "QT-SUPPLIER-WINNER-01",
                QuotationDate = DateTime.UtcNow,
                ValidUntil = DateTime.UtcNow.AddDays(30),
                CurrencyId = baseSeed.Currency.CurrencyId,
                Lines = new List<RecordQuotationLineRequest>
                {
                    new()
                    {
                        RFQLineId = rfq.Lines[0].Id,
                        ProductId = baseSeed.Product.ProductId,
                        QuotedQuantity = 100m,
                        UnitOfMeasureId = baseSeed.Uom.UnitOfMeasureId,
                        UnitPrice = 42.0m, // Supplier entered commercial price
                        DiscountPercentage = 5.0m, // 5% discount
                        TaxRate = 0m,
                        LeadTimeDays = 7
                    }
                }
            },
            Guid.NewGuid(),
            tenantId,
            companyId,
            branchId);

        // 3. Award the quotation
        await quotationService.ApproveQuotationAsync(
            rfq.Id,
            quote.Id,
            new ApproveQuotationRequest { Remarks = "Awarded lowest compliant commercial quote" },
            Guid.NewGuid(),
            tenantId,
            companyId,
            branchId);

        // 4. Validate RFQ -> PO creation
        await routeService.ValidateRfqAwardPoRouteAsync(tenantId, companyId, branchId, rfq.Id, quote.Id);

        // 5. Create PO from Awarded Quotation
        var po = PurchaseOrder.Create(tenantId, companyId, branchId, "PO-AWARD-001", DateTime.UtcNow, quote.SupplierId);
        var qLine = quote.Lines[0];
        po.AddItem(
            qLine.ProductId,
            qLine.QuotedQuantity,
            qLine.UnitPrice,
            qLine.UnitOfMeasureId,
            discountRate: qLine.DiscountPercentage,
            notes: $"[RFQ: {rfq.RFQNumber} Quote: {quote.QuotationReference} Line: {qLine.Id}] [PR: {pr.RequisitionNumber} Line: {pr.Items.First().Id}]");

        await db.PurchaseOrders.AddAsync(po);
        await db.SaveChangesAsync();

        // Assert: Commercial values transferred correctly
        Assert.Equal(PurchaseOrderStatus.Draft, po.Status);
        Assert.Equal(baseSeed.Supplier.SupplierId, po.SupplierId);
        Assert.Single(po.Items);
        var item = po.Items[0];
        Assert.Equal(100m, item.Quantity);
        Assert.Equal(42.0m, item.UnitPrice);
        Assert.Equal(5.0m, item.DiscountRate);
        Assert.Equal(3990.0m, po.TotalAmount); // 100 * 42 = 4200 - 5% (210) = 3990
    }

    [Fact]
    public async Task Scenario13_DuplicatePoCreation_FromSameAward_IsPrevented()
    {
        using var db = CreateInMemoryDomainDb();
        var tenantId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var baseSeed = await SeedBaseAsync(db, tenantId, companyId, branchId);
        var pr = await SeedApprovedPrAsync(db, tenantId, companyId, branchId, baseSeed.Product, baseSeed.Uom);

        var tokenService = new RfqTokenService(db);
        var rfqService = new RFQService(db, tokenService);
        var routeService = new ProcurementRouteService(db, rfqService);
        var quotationService = new SupplierQuotationService(db, tokenService);

        var rfq = await routeService.CreateRfqFromRequisitionAsync(
            tenantId, companyId, branchId, pr.Id, new CreateRfqFromRequisitionRequest
            {
                ClosingDate = DateTime.UtcNow.AddDays(7)
            }, Guid.NewGuid());

        var quote = await quotationService.RecordManualQuotationAsync(
            rfq.Id,
            new RecordManualQuotationRequest
            {
                SupplierId = baseSeed.Supplier.SupplierId,
                QuotationReference = "QT-SUPPLIER-DUP-01",
                QuotationDate = DateTime.UtcNow,
                ValidUntil = DateTime.UtcNow.AddDays(30),
                CurrencyId = baseSeed.Currency.CurrencyId,
                Lines = new List<RecordQuotationLineRequest>
                {
                    new()
                    {
                        RFQLineId = rfq.Lines[0].Id,
                        ProductId = baseSeed.Product.ProductId,
                        QuotedQuantity = 50m,
                        UnitOfMeasureId = baseSeed.Uom.UnitOfMeasureId,
                        UnitPrice = 40.0m
                    }
                }
            },
            Guid.NewGuid(),
            tenantId,
            companyId,
            branchId);

        await quotationService.ApproveQuotationAsync(
            rfq.Id, quote.Id, new ApproveQuotationRequest { Remarks = "Awarded" }, Guid.NewGuid(), tenantId, companyId, branchId);

        // Create PO that fully fulfills the 50 units
        var po = PurchaseOrder.Create(tenantId, companyId, branchId, "PO-AWARD-FULFILLED", DateTime.UtcNow, quote.SupplierId);
        po.AddItem(baseSeed.Product.ProductId, 50m, 40m, baseSeed.Uom.UnitOfMeasureId, notes: $"[RFQ: {rfq.RFQNumber} Quote: {quote.QuotationReference} Line: {quote.Lines[0].Id}]");
        await db.PurchaseOrders.AddAsync(po);
        await db.SaveChangesAsync();

        // Attempting to create duplicate PO from the already fulfilled award -> MUST BE REJECTED
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            routeService.ValidateRfqAwardPoRouteAsync(tenantId, companyId, branchId, rfq.Id, quote.Id));
        Assert.Contains("already been created", ex.Message);
    }

    [Fact]
    public async Task Scenario14_PoCreation_DoesNotTouchInventory_InventoryFollowsGoodsReceipt()
    {
        using var db = CreateInMemoryDomainDb();
        var tenantId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var baseSeed = await SeedBaseAsync(db, tenantId, companyId, branchId);
        var pr = await SeedApprovedPrAsync(db, tenantId, companyId, branchId, baseSeed.Product, baseSeed.Uom);

        // Create PO
        var po = PurchaseOrder.Create(tenantId, companyId, branchId, "PO-INV-TEST", DateTime.UtcNow, baseSeed.Supplier.SupplierId);
        po.AddItem(baseSeed.Product.ProductId, 100m, 50m, baseSeed.Uom.UnitOfMeasureId);
        await db.PurchaseOrders.AddAsync(po);
        await db.SaveChangesAsync();

        // Assert: No InventoryTransactions or Balances created
        var txCount = await db.InventoryTransactions.CountAsync();
        var balanceCount = await db.InventoryBalances.CountAsync();
        Assert.Equal(0, txCount);
        Assert.Equal(0, balanceCount);
    }

    [Fact]
    public async Task Scenario15_TenantScopeEnforcement_PreventsCrossTenantAccess()
    {
        using var db = CreateInMemoryDomainDb();
        var tenantIdA = Guid.NewGuid();
        var tenantIdB = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var baseSeedA = await SeedBaseAsync(db, tenantIdA, companyId, branchId);
        var prA = await SeedApprovedPrAsync(db, tenantIdA, companyId, branchId, baseSeedA.Product, baseSeedA.Uom);

        var tokenService = new RfqTokenService(db);
        var rfqService = new RFQService(db, tokenService);
        var routeService = new ProcurementRouteService(db, rfqService);

        // Attempting to query PR from Tenant B -> MUST THROW KeyNotFoundException
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            routeService.GetRouteStatusAsync(tenantIdB, companyId, branchId, prA.Id));

        // Attempting to create RFQ from Tenant B -> MUST THROW KeyNotFoundException
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            routeService.CreateRfqFromRequisitionAsync(tenantIdB, companyId, branchId, prA.Id, new CreateRfqFromRequisitionRequest
            {
                ClosingDate = DateTime.UtcNow.AddDays(7)
            }, Guid.NewGuid()));
    }

    [Fact]
    public async Task Scenario16_AwardedQuotation_QuantityAmendment_RecalculatesTotals_WithLockedPrice()
    {
        using var db = CreateInMemoryDomainDb();
        var tenantId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var baseSeed = await SeedBaseAsync(db, tenantId, companyId, branchId);
        var pr = await SeedApprovedPrAsync(db, tenantId, companyId, branchId, baseSeed.Product, baseSeed.Uom);

        var tokenService = new RfqTokenService(db);
        var rfqService = new RFQService(db, tokenService);
        var routeService = new ProcurementRouteService(db, rfqService);
        var quotationService = new SupplierQuotationService(db, tokenService);

        var rfq = await routeService.CreateRfqFromRequisitionAsync(
            tenantId, companyId, branchId, pr.Id, new CreateRfqFromRequisitionRequest
            {
                ClosingDate = DateTime.UtcNow.AddDays(7)
            }, Guid.NewGuid());

        var quote = await quotationService.RecordManualQuotationAsync(
            rfq.Id,
            new RecordManualQuotationRequest
            {
                SupplierId = baseSeed.Supplier.SupplierId,
                QuotationReference = "QT-AMEND-01",
                QuotationDate = DateTime.UtcNow,
                ValidUntil = DateTime.UtcNow.AddDays(30),
                CurrencyId = baseSeed.Currency.CurrencyId,
                Lines = new List<RecordQuotationLineRequest>
                {
                    new()
                    {
                        RFQLineId = rfq.Lines[0].Id,
                        ProductId = baseSeed.Product.ProductId,
                        QuotedQuantity = 100m,
                        UnitOfMeasureId = baseSeed.Uom.UnitOfMeasureId,
                        UnitPrice = 75.0m, // Quoted unit price
                        DiscountPercentage = 10.0m, // 10% discount
                        TaxRate = 0m
                    }
                }
            },
            Guid.NewGuid(), tenantId, companyId, branchId);

        await quotationService.ApproveQuotationAsync(
            rfq.Id, quote.Id, new ApproveQuotationRequest { Remarks = "Approved quote" },
            Guid.NewGuid(), tenantId, companyId, branchId);

        // Buyer amends quantity from 100 to 60 during PO creation
        decimal amendedQuantity = 60m;
        var qLine = quote.Lines[0];

        var po = PurchaseOrder.Create(tenantId, companyId, branchId, "PO-AMEND-TEST-01", DateTime.UtcNow, quote.SupplierId);
        po.AddItem(
            qLine.ProductId,
            amendedQuantity, // Amended quantity
            qLine.UnitPrice, // Unit price locked to quotation price (75.0)
            qLine.UnitOfMeasureId,
            discountRate: qLine.DiscountPercentage,
            notes: $"[RFQ: {rfq.RFQNumber} Quote: {quote.QuotationReference} Line: {qLine.Id}]");

        await db.PurchaseOrders.AddAsync(po);
        await db.SaveChangesAsync();

        // Verification:
        // Gross = 60 * 75 = 4500
        // Discount 10% = 450
        // Total = 4050
        Assert.Equal(60m, po.Items[0].Quantity);
        Assert.Equal(75.0m, po.Items[0].UnitPrice);
        Assert.Equal(10.0m, po.Items[0].DiscountRate);
        Assert.Equal(4050.0m, po.TotalAmount);
    }

    [Fact]
    public async Task Scenario17_DirectPrToPo_DoesNotFail_WhenExclusivityIsNotForced()
    {
        using var db = CreateInMemoryDomainDb();
        var tenantId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var baseSeed = await SeedBaseAsync(db, tenantId, companyId, branchId);
        var pr = await SeedApprovedPrAsync(db, tenantId, companyId, branchId, baseSeed.Product, baseSeed.Uom);

        // Direct PO creation from PR succeeds cleanly
        var po = PurchaseOrder.Create(tenantId, companyId, branchId, "PO-DIRECT-PR-01", DateTime.UtcNow, baseSeed.Supplier.SupplierId);
        po.AddItem(baseSeed.Product.ProductId, 50m, 120m, baseSeed.Uom.UnitOfMeasureId,
            notes: $"[PR: {pr.RequisitionNumber} Line: {pr.Items.First().Id}]");

        await db.PurchaseOrders.AddAsync(po);
        await db.SaveChangesAsync();

        Assert.Single(po.Items);
        Assert.Equal(50m, po.Items[0].Quantity);
        Assert.Equal(6000m, po.TotalAmount);
    }
}
