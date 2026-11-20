using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domain.Features.MasterData.Branch;
using Domain.Features.MasterData.Company;
using Domain.Features.MasterData.Currency;
using Domain.Features.Setup;
using Domain.Infrastructure.Persistence;
using ERP.Application.Features.Setup;
using ERP.AI.Infrastructure.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using SaaS.Bootstrap.Subscription;
using SaaS.Core.Models;
using SaaS.Infrastructure.Persistence;
using SaaS.Infrastructure.Persistence.Repositories;
using Security.Infrastructure.Persistence;
using Security.Infrastructure.Persistence.Repositories;
using Security.Services;

namespace ERP.SecurityTests.DatabaseSync;

public class ProfileEntityCounts
{
    public string ProfileCode { get; set; } = string.Empty;
    public Guid TenantId { get; set; }
    public int CurrencyCount { get; set; }
    public int TaxCodeCount { get; set; }
    public int PaymentTermCount { get; set; }
    public int CategoryCount { get; set; }
    public int BrandCount { get; set; }
    public int UomCount { get; set; }
    public int ConversionCount { get; set; }
    public int ProductCount { get; set; }
    public int WarehouseCount { get; set; }
    public int PriceCardCount { get; set; }
    public int SupplierCount { get; set; }
    public int CustomerCount { get; set; }

    public int TotalEntities =>
        CurrencyCount + TaxCodeCount + PaymentTermCount + CategoryCount +
        BrandCount + UomCount + ConversionCount + ProductCount +
        WarehouseCount + PriceCardCount + SupplierCount + CustomerCount;
}

public static class DatabaseBootstrapRunner
{
    public static SaaSDbContext CreateSaaSDbContext()
    {
        DatabaseSyncSafetyGuard.AssertSafeTestDatabase(DatabaseSyncSafetyGuard.PlatformDbTestConnection);
        var options = new DbContextOptionsBuilder<SaaSDbContext>()
            .UseSqlServer(DatabaseSyncSafetyGuard.PlatformDbTestConnection)
            .Options;
        return new SaaSDbContext(options);
    }

    public static SecurityDbContext CreateSecurityDbContext()
    {
        DatabaseSyncSafetyGuard.AssertSafeTestDatabase(DatabaseSyncSafetyGuard.SecurityDbTestConnection);
        var options = new DbContextOptionsBuilder<SecurityDbContext>()
            .UseSqlServer(DatabaseSyncSafetyGuard.SecurityDbTestConnection)
            .Options;
        return new SecurityDbContext(options);
    }

    public static DomainDbContext CreateDomainDbContext()
    {
        DatabaseSyncSafetyGuard.AssertSafeTestDatabase(DatabaseSyncSafetyGuard.DomainDbTestConnection);
        var options = new DbContextOptionsBuilder<DomainDbContext>()
            .UseSqlServer(DatabaseSyncSafetyGuard.DomainDbTestConnection)
            .Options;
        return new DomainDbContext(options);
    }

    public static AIDbContext CreateAIDbContext()
    {
        DatabaseSyncSafetyGuard.AssertSafeTestDatabase(DatabaseSyncSafetyGuard.AIDbTestConnection);
        var options = new DbContextOptionsBuilder<AIDbContext>()
            .UseSqlServer(DatabaseSyncSafetyGuard.AIDbTestConnection)
            .Options;
        return new AIDbContext(options);
    }

    public static async Task ApplyAllMigrationsAsync()
    {
        // 1. PlatformDB_Test
        DatabaseSyncSafetyGuard.EnsureDatabaseExists(DatabaseSyncSafetyGuard.PlatformDbTestConnection);
        using (var saasDb = CreateSaaSDbContext())
        {
            await saasDb.Database.MigrateAsync();
        }

        // 2. SecurityDB_Test
        DatabaseSyncSafetyGuard.EnsureDatabaseExists(DatabaseSyncSafetyGuard.SecurityDbTestConnection);
        using (var secDb = CreateSecurityDbContext())
        {
            await secDb.Database.MigrateAsync();
        }

        // 3. DomainDB_Test
        DatabaseSyncSafetyGuard.EnsureDatabaseExists(DatabaseSyncSafetyGuard.DomainDbTestConnection);
        using (var domDb = CreateDomainDbContext())
        {
            await domDb.Database.MigrateAsync();
        }

        // 4. AIDatabase_Test
        DatabaseSyncSafetyGuard.EnsureDatabaseExists(DatabaseSyncSafetyGuard.AIDbTestConnection);
        using (var aiDb = CreateAIDbContext())
        {
            await aiDb.Database.MigrateAsync();
        }
    }

    public static void RunSaaSBootstrap(SaaSDbContext saasDb)
    {
        var planRepo = new SubscriptionPlanRepository(saasDb);
        var paramRepo = new SubscriptionParameterRepository(saasDb);
        var limitRepo = new SubscriptionLimitRepository(saasDb);
        var planParamRepo = new SubscriptionPlanParameterRepository(saasDb);

        SubscriptionSeed.Seed(planRepo, paramRepo, limitRepo, planParamRepo);
    }

    public static async Task RunSecurityBootstrapAsync(SecurityDbContext secDb)
    {
        var repo = new SecurityBootstrapRepository(secDb);
        var pwdService = new PasswordService();
        var bootstrap = new SecurityBootstrapService(repo, pwdService);

        await bootstrap.EnsurePlatformRbacAsync();
        await bootstrap.EnsureDomainPermissionsAsync();
        await bootstrap.UpgradeExistingTenantsAsync();
    }

    public static async Task<TenantMasterDataSetupResult> RunIndustryProfileBootstrapAsync(
        DomainDbContext domainDb,
        Guid tenantId,
        string profileCode,
        string companyCode,
        string companyName,
        string branchCode,
        string branchName)
    {
        // 1. Ensure Tenant Base Currency (LKR)
        var currency = await domainDb.Currencies.FirstOrDefaultAsync(c => c.TenantId == tenantId && c.CurrencyCode == "LKR");
        if (currency == null)
        {
            currency = Currency.Create(tenantId, "LKR", "Sri Lankan Rupee", "Rs", 2);
            domainDb.Currencies.Add(currency);
            await domainDb.SaveChangesAsync();
        }

        // 2. Ensure Company
        var company = await domainDb.Companies.FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Code == companyCode);
        if (company == null)
        {
            company = new Company
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Code = companyCode,
                Name = companyName,
                BaseCurrencyId = currency.CurrencyId,
                IsActive = true
            };
            domainDb.Companies.Add(company);
            await domainDb.SaveChangesAsync();
        }

        // 3. Ensure Branch
        var branch = await domainDb.Branches.FirstOrDefaultAsync(b => b.CompanyId == company.Id && b.Code == branchCode);
        if (branch == null)
        {
            branch = new Branch
            {
                Id = Guid.NewGuid(),
                CompanyId = company.Id,
                Code = branchCode,
                Name = branchName,
                IsActive = true
            };
            domainDb.Branches.Add(branch);
            await domainDb.SaveChangesAsync();
        }

        // 4. Run Master Data Setup
        var setupService = new TenantMasterDataSetupService(domainDb);
        var result = await setupService.SetupMasterDataAsync(tenantId, new TenantMasterDataSetupRequest
        {
            ProfileCode = profileCode,
            CompanyId = company.Id,
            BranchId = branch.Id
        });

        return result;
    }

    public static async Task<ProfileEntityCounts> CountTenantMasterDataAsync(DomainDbContext db, Guid tenantId, string profileCode)
    {
        return new ProfileEntityCounts
        {
            ProfileCode = profileCode,
            TenantId = tenantId,
            CurrencyCount = await db.Currencies.CountAsync(x => x.TenantId == tenantId),
            TaxCodeCount = await db.TaxCodes.CountAsync(x => x.TenantId == tenantId),
            PaymentTermCount = await db.PaymentTerms.CountAsync(x => x.TenantId == tenantId),
            CategoryCount = await db.Categories.CountAsync(x => x.TenantId == tenantId),
            BrandCount = await db.Brands.CountAsync(x => x.TenantId == tenantId),
            UomCount = await db.UnitsOfMeasure.CountAsync(x => x.TenantId == tenantId),
            ConversionCount = await db.UnitOfMeasureConversions.CountAsync(x => x.TenantId == tenantId),
            ProductCount = await db.Products.CountAsync(x => x.TenantId == tenantId),
            WarehouseCount = await db.Warehouses.CountAsync(x => x.TenantId == tenantId),
            PriceCardCount = await db.PriceCards.CountAsync(x => x.TenantId == tenantId),
            SupplierCount = await db.Suppliers.CountAsync(x => x.TenantId == tenantId),
            CustomerCount = await db.Customers.CountAsync(x => x.TenantId == tenantId)
        };
    }
}
