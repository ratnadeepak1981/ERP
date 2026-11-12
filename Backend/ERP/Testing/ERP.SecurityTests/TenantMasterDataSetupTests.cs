using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Domain.Features.MasterData.Company;
using Domain.Features.MasterData.Currency;
using Domain.Features.Setup;
using Domain.Infrastructure.Persistence;
using ERP.Application.Features.Setup;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ERP.SecurityTests;

public class TenantMasterDataSetupTests : IClassFixture<TestSecurityFixture>
{
    private readonly TestSecurityFixture _fixture;

    public TenantMasterDataSetupTests(TestSecurityFixture fixture)
    {
        _fixture = fixture;
        _fixture.EnsureDatabasesInitialized();
    }

    private DomainDbContext CreateDomainDb()
    {
        DatabaseSafetyGuard.AssertSafeTestDatabase(TestConstants.DomainDbTest);
        var options = new DbContextOptionsBuilder<DomainDbContext>()
            .UseSqlServer(TestConstants.DomainDbTest)
            .Options;
        return new DomainDbContext(options);
    }

    [Fact]
    public async Task SetupMasterDataAsync_SeedsCompleteRetailMasterData_WhenMissing()
    {
        using var db = CreateDomainDb();
        var setupService = new TenantMasterDataSetupService(db);

        var tenantId = Guid.NewGuid();

        // 1. Create base currency, company and branch
        var baseCurrency = Currency.Create(tenantId, "LKR", "Sri Lankan Rupee", "Rs", 2);
        db.Currencies.Add(baseCurrency);

        var company = new Company
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Test Retail Mart",
            Code = "TESTRET",
            BaseCurrencyId = baseCurrency.CurrencyId,
            SpecialUomConversionEnabled = true,
            IsActive = true
        };
        db.Companies.Add(company);

        var branch = new global::Domain.Features.MasterData.Branch.Branch
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            Name = "Test Retail Main Branch",
            Code = "MAIN",
            IsActive = true
        };
        db.Branches.Add(branch);
        await db.SaveChangesAsync();

        var request = new TenantMasterDataSetupRequest
        {
            CompanyId = company.Id,
            BranchId = branch.Id,
            ProfileCode = "retail"
        };

        // Act
        var result = await setupService.SetupMasterDataAsync(tenantId, request);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(tenantId, result.TenantId);
        Assert.Equal(company.Id, result.CompanyId);
        Assert.Equal(branch.Id, result.BranchId);

        // 1. Currencies & Exchange Rates
        var tenantCurrencies = await db.Currencies.Where(c => c.TenantId == tenantId).ToListAsync();
        Assert.True(tenantCurrencies.Count >= 5);
        Assert.Contains(tenantCurrencies, c => c.CurrencyCode == "LKR");
        Assert.Contains(tenantCurrencies, c => c.CurrencyCode == "USD");

        var tenantRates = await db.ExchangeRates.Where(r => r.TenantId == tenantId).ToListAsync();
        Assert.True(tenantRates.Count >= 4);

        // 2. Payment Terms & Tax Codes
        var paymentTerms = await db.PaymentTerms.Where(p => p.TenantId == tenantId).ToListAsync();
        Assert.True(paymentTerms.Count >= 4);
        Assert.Contains(paymentTerms, p => p.TermCode == "NET30");

        var taxCodes = await db.TaxCodes.Where(t => t.TenantId == tenantId).ToListAsync();
        Assert.True(taxCodes.Count >= 3);
        Assert.Contains(taxCodes, t => t.Code == "TAX-EXEMPT");
        Assert.Contains(taxCodes, t => t.Code == "VAT-18");

        // 3. Categories & Brands
        var categories = await db.Categories.Where(c => c.TenantId == tenantId).ToListAsync();
        Assert.True(categories.Count >= 5);
        Assert.Contains(categories, c => c.CategoryCode == "STAPLES");

        var brands = await db.Brands.Where(b => b.TenantId == tenantId).ToListAsync();
        Assert.True(brands.Count >= 3);

        // 4. Units of Measure & Conversions
        var uoms = await db.UnitsOfMeasure.Where(u => u.TenantId == tenantId).ToListAsync();
        Assert.True(uoms.Count >= 10);
        Assert.Contains(uoms, u => u.Code == "BAG");
        Assert.Contains(uoms, u => u.Code == "BTL");
        Assert.Contains(uoms, u => u.Code == "PKT");

        var conversions = await db.UnitOfMeasureConversions.Where(c => c.TenantId == tenantId).ToListAsync();
        Assert.True(conversions.Count >= 6);

        // 5. Products, Packagings, Identifiers
        var products = await db.Products.Where(p => p.TenantId == tenantId).ToListAsync();
        Assert.Equal(5, products.Count);
        Assert.Contains(products, p => p.ProductCode == "RET-001");
        Assert.Contains(products, p => p.ProductCode == "RET-002");
        Assert.Contains(products, p => p.ProductCode == "RET-003");

        var ret001 = products.First(p => p.ProductCode == "RET-001");
        Assert.Equal(global::Domain.Features.MasterData.Product.TrackingMode.Batch, ret001.TrackingMode);
        Assert.Equal(1500m, ret001.StandardCost);
        Assert.Equal(1850m, ret001.SellingPrice);

        var packagings = await db.ProductPackagings.Where(pkg => pkg.TenantId == tenantId).ToListAsync();
        Assert.True(packagings.Count >= 5);

        var identifiers = await db.ProductIdentifiers.Where(i => i.TenantId == tenantId).ToListAsync();
        Assert.True(identifiers.Count >= 5);

        // 6. Warehouses, Zones, Locations
        var warehouses = await db.Warehouses.Where(w => w.TenantId == tenantId).ToListAsync();
        Assert.Equal(2, warehouses.Count);
        Assert.Contains(warehouses, w => w.WarehouseCode == "RETAIL-WH1");
        Assert.Contains(warehouses, w => w.WarehouseCode == "RETAIL-WH2");

        var zones = await db.WarehouseZones.Where(z => z.TenantId == tenantId).ToListAsync();
        Assert.Equal(3, zones.Count);
        Assert.Contains(zones, z => z.ZoneCode == "ZONE-REC");
        Assert.Contains(zones, z => z.ZoneCode == "ZONE-STK");
        Assert.Contains(zones, z => z.ZoneCode == "ZONE-WH2");

        var locations = await db.WarehouseLocations.Where(l => l.TenantId == tenantId).ToListAsync();
        Assert.Equal(3, locations.Count);
        Assert.Contains(locations, l => l.LocationCode == "LOC-REC-01");
        Assert.Contains(locations, l => l.LocationCode == "LOC-STK-01");
        Assert.Contains(locations, l => l.LocationCode == "LOC-WH2-01");

        // 7. Suppliers & Customers
        var suppliers = await db.Suppliers.Where(s => s.TenantId == tenantId).ToListAsync();
        Assert.Single(suppliers);
        Assert.Equal("SUP-RET-001", suppliers[0].SupplierCode);

        var customers = await db.Customers.Where(c => c.TenantId == tenantId).ToListAsync();
        Assert.Single(customers);
        Assert.Equal("CUST-RET-001", customers[0].CustomerCode);

        // 8. Price Cards & Lines
        var priceCards = await db.PriceCards.Where(pc => pc.TenantId == tenantId).ToListAsync();
        Assert.Equal(2, priceCards.Count);
        Assert.Contains(priceCards, pc => pc.PriceCardCode == "PPC-SUP-RET-001");
        Assert.Contains(priceCards, pc => pc.PriceCardCode == "SPC-RETAIL-STD");

        var priceCardLines = await db.PriceCardLines.Where(l => l.TenantId == tenantId).ToListAsync();
        Assert.Equal(10, priceCardLines.Count);
    }

    [Fact]
    public async Task SetupMasterDataAsync_IsIdempotent_DoesNotDuplicateOnRepeatedExecution()
    {
        using var db = CreateDomainDb();
        var setupService = new TenantMasterDataSetupService(db);

        var tenantId = Guid.NewGuid();

        var baseCurrency = Currency.Create(tenantId, "LKR", "Sri Lankan Rupee", "Rs", 2);
        db.Currencies.Add(baseCurrency);

        var company = new Company
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Idempotency Mart",
            Code = "IDEMMART",
            BaseCurrencyId = baseCurrency.CurrencyId,
            IsActive = true
        };
        db.Companies.Add(company);

        var branch = new global::Domain.Features.MasterData.Branch.Branch
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            Name = "Idempotency Branch",
            Code = "MAIN",
            IsActive = true
        };
        db.Branches.Add(branch);
        await db.SaveChangesAsync();

        var request = new TenantMasterDataSetupRequest
        {
            CompanyId = company.Id,
            BranchId = branch.Id,
            ProfileCode = "retail"
        };

        // Pass 1: Initial seed
        var result1 = await setupService.SetupMasterDataAsync(tenantId, request);
        Assert.True(result1.Success);

        int prodCount1 = await db.Products.CountAsync(p => p.TenantId == tenantId);
        int whCount1 = await db.Warehouses.CountAsync(w => w.TenantId == tenantId);
        int locCount1 = await db.WarehouseLocations.CountAsync(l => l.TenantId == tenantId);
        int supCount1 = await db.Suppliers.CountAsync(s => s.TenantId == tenantId);
        int custCount1 = await db.Customers.CountAsync(c => c.TenantId == tenantId);
        int pcCount1 = await db.PriceCards.CountAsync(pc => pc.TenantId == tenantId);

        // Pass 2: Repeated seed
        var result2 = await setupService.SetupMasterDataAsync(tenantId, request);
        Assert.True(result2.Success);

        int prodCount2 = await db.Products.CountAsync(p => p.TenantId == tenantId);
        int whCount2 = await db.Warehouses.CountAsync(w => w.TenantId == tenantId);
        int locCount2 = await db.WarehouseLocations.CountAsync(l => l.TenantId == tenantId);
        int supCount2 = await db.Suppliers.CountAsync(s => s.TenantId == tenantId);
        int custCount2 = await db.Customers.CountAsync(c => c.TenantId == tenantId);
        int pcCount2 = await db.PriceCards.CountAsync(pc => pc.TenantId == tenantId);

        // Verify exact zero duplicates created
        Assert.Equal(prodCount1, prodCount2);
        Assert.Equal(whCount1, whCount2);
        Assert.Equal(locCount1, locCount2);
        Assert.Equal(supCount1, supCount2);
        Assert.Equal(custCount1, custCount2);
        Assert.Equal(pcCount1, pcCount2);
    }

    [Fact]
    public async Task SetupMasterDataAsync_ThrowsArgumentException_WhenProfileIsUnknown()
    {
        using var db = CreateDomainDb();
        var setupService = new TenantMasterDataSetupService(db);

        var tenantId = Guid.NewGuid();
        var curr = Currency.Create(tenantId, "LKR", "Sri Lankan Rupee", "Rs", 2);
        db.Currencies.Add(curr);
        await db.SaveChangesAsync();

        var company = new Company
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Test Mart",
            Code = "TEST",
            BaseCurrencyId = curr.CurrencyId,
            IsActive = true
        };
        db.Companies.Add(company);
        var branch = new global::Domain.Features.MasterData.Branch.Branch
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            Name = "Branch 1",
            Code = "B1",
            IsActive = true
        };
        db.Branches.Add(branch);
        await db.SaveChangesAsync();

        var request = new TenantMasterDataSetupRequest
        {
            CompanyId = company.Id,
            BranchId = branch.Id,
            ProfileCode = "APXVAL" // User typed tenant code instead of profile
        };

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            setupService.SetupMasterDataAsync(tenantId, request));

        Assert.Contains("Industry profile 'APXVAL' is not supported", ex.Message);
        Assert.Contains("retail", ex.Message);
    }
}
