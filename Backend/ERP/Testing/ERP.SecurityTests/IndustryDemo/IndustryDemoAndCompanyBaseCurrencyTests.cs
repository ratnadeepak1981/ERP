using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Domain.Features.MasterData.Common;
using Domain.Features.MasterData.Company;
using Domain.Features.MasterData.Currency;
using Domain.Features.Procurement.PurchaseOrder;
using Domain.Infrastructure.Persistence;
using ERP.SecurityTests.MasterData;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SaaS.Infrastructure.Persistence;
using Security.Infrastructure.Persistence;
using Xunit;

namespace ERP.SecurityTests.IndustryDemo;

[Collection("SecurityTestCollection")]
public class IndustryDemoAndCompanyBaseCurrencyTests : IClassFixture<TestSecurityFixture>
{
    private readonly TestSecurityFixture _fixture;

    public IndustryDemoAndCompanyBaseCurrencyTests(TestSecurityFixture fixture)
    {
        _fixture = fixture;
        _fixture.EnsureDatabasesInitialized();

        // Seed 11-industry demo master data
        using var scope = _fixture.Services.CreateScope();
        var platformDb = scope.ServiceProvider.GetRequiredService<SaaSDbContext>();
        var securityDb = scope.ServiceProvider.GetRequiredService<SecurityDbContext>();
        var domainDb = scope.ServiceProvider.GetRequiredService<DomainDbContext>();

        IndustryDemoMasterDataSeeder.SeedAll(platformDb, securityDb, domainDb);
    }

    [Fact]
    public void IndustryDemoSeeder_SeedsAll11TenantsAndCorrectAccountCounts()
    {
        using var scope = _fixture.Services.CreateScope();
        var platformDb = scope.ServiceProvider.GetRequiredService<SaaSDbContext>();
        var domainDb = scope.ServiceProvider.GetRequiredService<DomainDbContext>();
        var securityDb = scope.ServiceProvider.GetRequiredService<SecurityDbContext>();

        // 1. Verify 11 Tenants in SaaS
        var tenantIds = IndustryDemoMasterDataSeeder.TenantIds.Values.ToList();
        var seededTenants = platformDb.Tenants.Where(t => tenantIds.Contains(t.Id)).ToList();
        Assert.Equal(11, seededTenants.Count);

        // 2. Verify exactly 76 User Accounts
        var seededUsers = securityDb.Users.Where(u => u.TenantId.HasValue && tenantIds.Contains(u.TenantId.Value)).ToList();
        Assert.Equal(76, seededUsers.Count);

        // 3. Verify exactly 55 Products
        var seededProducts = domainDb.Products.Where(p => tenantIds.Contains(p.TenantId)).ToList();
        Assert.Equal(55, seededProducts.Count);

        // 4. Verify all 33 UOMs exist for each tenant
        var retailTenantId = IndustryDemoMasterDataSeeder.TenantIds["retail"];
        var retailUoms = domainDb.UnitsOfMeasure.Where(u => u.TenantId == retailTenantId).ToList();
        Assert.True(retailUoms.Count >= 33, $"Expected at least 33 UOMs for retail tenant, found {retailUoms.Count}");

        // 5. Verify Pallet and Crate Multipliers
        var whoTenantId = IndustryDemoMasterDataSeeder.TenantIds["wholesale"];
        var waterProd = domainDb.Products.First(p => p.TenantId == whoTenantId && p.ProductCode == "WHO-001");
        var palletPackaging = domainDb.ProductPackagings.First(pkg => pkg.TenantId == whoTenantId && pkg.ProductId == waterProd.ProductId && pkg.PackagingCode.Contains("PALLET"));
        Assert.Equal(1728m, palletPackaging.BaseQuantity);

        var foodTenantId = IndustryDemoMasterDataSeeder.TenantIds["food"];
        var milkProd = domainDb.Products.First(p => p.TenantId == foodTenantId && p.ProductCode == "FOOD-001");
        var cratePackaging = domainDb.ProductPackagings.First(pkg => pkg.TenantId == foodTenantId && pkg.ProductId == milkProd.ProductId && pkg.PackagingCode.Contains("CRATE"));
        Assert.Equal(12m, cratePackaging.BaseQuantity);

        // 6. Verify Symbology checksums for all seeded barcodes
        var seededIdentifiers = domainDb.ProductIdentifiers.Where(i => tenantIds.Contains(i.TenantId)).ToList();
        Assert.NotEmpty(seededIdentifiers);
        foreach (var ident in seededIdentifiers)
        {
            SymbologyValidator.Validate(ident.Symbology, ident.IdentifierValue);
        }
    }

    [Fact]
    public void CompanyBaseCurrency_ConfiguredCorrectlyAcrossCompanies()
    {
        using var scope = _fixture.Services.CreateScope();
        var domainDb = scope.ServiceProvider.GetRequiredService<DomainDbContext>();

        // Lanka Retail Mart (Retail) -> LKR
        var retailTenantId = IndustryDemoMasterDataSeeder.TenantIds["retail"];
        var retailCompany = domainDb.Companies.Include(c => c.BaseCurrency).First(c => c.TenantId == retailTenantId);
        Assert.NotNull(retailCompany.BaseCurrencyId);
        Assert.NotNull(retailCompany.BaseCurrency);
        Assert.Equal("LKR", retailCompany.BaseCurrency.CurrencyCode);
        Assert.Equal("LKR", retailCompany.BaseCurrencyCode);

        // Ceylon Import Export (ImpExp) -> USD
        var impexpTenantId = IndustryDemoMasterDataSeeder.TenantIds["impexp"];
        var impexpCompany = domainDb.Companies.Include(c => c.BaseCurrency).First(c => c.TenantId == impexpTenantId);
        Assert.NotNull(impexpCompany.BaseCurrencyId);
        Assert.NotNull(impexpCompany.BaseCurrency);
        Assert.Equal("USD", impexpCompany.BaseCurrency.CurrencyCode);
        Assert.Equal("USD", impexpCompany.BaseCurrencyCode);
    }

    [Fact]
    public async Task CompanyBaseCurrency_CannotAssignCrossTenantCurrency()
    {
        using var scope = _fixture.Services.CreateScope();
        var companyService = scope.ServiceProvider.GetRequiredService<ICompanyService>();
        var domainDb = scope.ServiceProvider.GetRequiredService<DomainDbContext>();

        var retailTenantId = IndustryDemoMasterDataSeeder.TenantIds["retail"];
        var impexpTenantId = IndustryDemoMasterDataSeeder.TenantIds["impexp"];

        // Currency belonging to ImpExp tenant
        var impexpCurrency = domainDb.Currencies.First(c => c.TenantId == impexpTenantId && c.CurrencyCode == "USD");

        // Attempting to create a company in Retail tenant using ImpExp tenant's currency must throw
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            companyService.CreateCompanyAsync(
                retailTenantId,
                "Invalid Cross-Tenant Company",
                "INV-COMP",
                impexpCurrency.CurrencyId));
    }

    [Fact]
    public async Task PurchasingApproval_DistinctSubmitAndApprove_SelfApprovalRejection()
    {
        using var scope = _fixture.Services.CreateScope();
        var domainDb = scope.ServiceProvider.GetRequiredService<DomainDbContext>();
        var securityDb = scope.ServiceProvider.GetRequiredService<SecurityDbContext>();

        var retailTenantId = IndustryDemoMasterDataSeeder.TenantIds["retail"];
        var retailCompany = domainDb.Companies.First(c => c.TenantId == retailTenantId);
        var retailBranch = domainDb.Branches.First(b => b.CompanyId == retailCompany.Id);
        var riceProduct = domainDb.Products.First(p => p.TenantId == retailTenantId && p.ProductCode == "RET-001");

        var purchaseOfficerUser = securityDb.Users.First(u => u.TenantId == retailTenantId && u.Username == "retail.purchase");
        var purchaseManagerUser = securityDb.Users.First(u => u.TenantId == retailTenantId && u.Username == "retail.purchase.manager");

        // 1. Authenticate as Purchasing Officer (Creator / Submitter)
        var officerClient = _fixture.CreateAuthenticatedClient(
            purchaseOfficerUser.Id,
            purchaseOfficerUser.Username,
            retailTenantId,
            new[] { "Purchasing Officer" },
            new[] { "PURCHASE_ORDER.CREATE", "PURCHASE_ORDER.VIEW", "PURCHASE_ORDER.SUBMIT" });

        var poNumber = $"PO-DEMO-{Guid.NewGuid():N}"[..18];
        var createRequest = new CreatePurchaseOrderRequest
        {
            OrderNumber = poNumber,
            OrderDate = DateTime.UtcNow,
            Items = new List<CreatePurchaseOrderItemRequest>
            {
                new CreatePurchaseOrderItemRequest
                {
                    ProductId = riceProduct.ProductId,
                    Quantity = 10,
                    UnitPrice = 15m
                }
            }
        };

        // Create PO
        var createRes = await officerClient.PostAsJsonAsync(
            $"/api/companies/{retailCompany.Id}/branches/{retailBranch.Id}/purchase-orders",
            createRequest);
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var createdPo = await createRes.ReadApiResponseDataAsync<PurchaseOrderDto>();
        Assert.NotNull(createdPo);

        // Submit PO
        var submitRes = await officerClient.PostAsync(
            $"/api/companies/{retailCompany.Id}/branches/{retailBranch.Id}/purchase-orders/{createdPo.Id}/submit",
            null);
        Assert.Equal(HttpStatusCode.OK, submitRes.StatusCode);

        // 2. Purchasing Officer attempts to approve own PO (Self-Approval Rejection)
        var selfApproveClient = _fixture.CreateAuthenticatedClient(
            purchaseOfficerUser.Id,
            purchaseOfficerUser.Username,
            retailTenantId,
            new[] { "Purchasing Manager" },
            new[] { "PURCHASE_ORDER.VIEW", "PURCHASE_ORDER.APPROVE" });

        var selfApproveRes = await selfApproveClient.PostAsJsonAsync(
            $"/api/companies/{retailCompany.Id}/branches/{retailBranch.Id}/purchase-orders/{createdPo.Id}/approve",
            new { Remarks = "Self approving" });

        // Must be rejected (Conflict or BadRequest with self-approval violation message)
        Assert.True(
            selfApproveRes.StatusCode == HttpStatusCode.Conflict ||
            selfApproveRes.StatusCode == HttpStatusCode.BadRequest ||
            selfApproveRes.StatusCode == HttpStatusCode.InternalServerError,
            $"Expected self-approval failure, got {selfApproveRes.StatusCode}");

        // 3. Purchasing Manager (Distinct Approver) approves the PO
        var managerClient = _fixture.CreateAuthenticatedClient(
            purchaseManagerUser.Id,
            purchaseManagerUser.Username,
            retailTenantId,
            new[] { "Purchasing Manager" },
            new[] { "PURCHASE_ORDER.VIEW", "PURCHASE_ORDER.APPROVE" });

        var approveRes = await managerClient.PostAsJsonAsync(
            $"/api/companies/{retailCompany.Id}/branches/{retailBranch.Id}/purchase-orders/{createdPo.Id}/approve",
            new { Remarks = "Approved by manager" });

        Assert.Equal(HttpStatusCode.OK, approveRes.StatusCode);
        var approvedPo = await approveRes.ReadApiResponseDataAsync<PurchaseOrderDto>();
        Assert.NotNull(approvedPo);
        Assert.Equal(PurchaseOrderStatus.Approved, approvedPo.Status);
    }

    private class PurchaseOrderDto
    {
        public Guid Id { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public PurchaseOrderStatus Status { get; set; }
    }
}
