using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Domain.Features.MasterData.Company;
using Domain.Features.MasterData.Product;
using Domain.Infrastructure.Persistence;
using ERP.Application.Features.Setup;
using ERP.AI.Infrastructure.Infrastructure.Persistence;
using ERP.Domain.Features.MasterData.Category;
using Microsoft.EntityFrameworkCore;
using SaaS.Core.Models;
using SaaS.Infrastructure.Persistence;
using Security.Core.Models;
using Security.Infrastructure.Persistence;
using Security.Services;
using Xunit;

namespace ERP.SecurityTests.DatabaseSync;

[Collection("DatabaseSync")]
public class DatabaseBootstrapSyncIntegrationTests
{
    public static readonly Guid RetailTenantId = Guid.Parse("A1000000-0000-0000-0000-000000000001");
    public static readonly Guid ManufacturingTenantId = Guid.Parse("A2000000-0000-0000-0000-000000000002");
    public static readonly Guid ServiceTenantId = Guid.Parse("A3000000-0000-0000-0000-000000000003");
    public static readonly Guid PharmacyTenantId = Guid.Parse("A4000000-0000-0000-0000-000000000004");
    public static readonly Guid WholesaleTenantId = Guid.Parse("A5000000-0000-0000-0000-000000000005");

    [Fact]
    public void Test01_TestDatabaseCreation_ValidatesAndCreatesIsolatedDatabases()
    {
        // 1. Strict safety guard: Attempting to target dev databases MUST throw
        Assert.Throws<InvalidOperationException>(() =>
            DatabaseSyncSafetyGuard.AssertSafeTestDatabase("Server=(localdb)\\MSSQLLocalDB;Database=PlatformDB;Trusted_Connection=True;"));
        Assert.Throws<InvalidOperationException>(() =>
            DatabaseSyncSafetyGuard.AssertSafeTestDatabase("Server=(localdb)\\MSSQLLocalDB;Database=SecurityDB;Trusted_Connection=True;"));
        Assert.Throws<InvalidOperationException>(() =>
            DatabaseSyncSafetyGuard.AssertSafeTestDatabase("Server=(localdb)\\MSSQLLocalDB;Database=DomainDB;Trusted_Connection=True;"));
        Assert.Throws<InvalidOperationException>(() =>
            DatabaseSyncSafetyGuard.AssertSafeTestDatabase("Server=(localdb)\\MSSQLLocalDB;Database=AIDatabase;Trusted_Connection=True;"));

        // 2. Ensuring test databases creates or connects safely
        DatabaseSyncSafetyGuard.EnsureDatabaseExists(DatabaseSyncSafetyGuard.PlatformDbTestConnection);
        DatabaseSyncSafetyGuard.EnsureDatabaseExists(DatabaseSyncSafetyGuard.SecurityDbTestConnection);
        DatabaseSyncSafetyGuard.EnsureDatabaseExists(DatabaseSyncSafetyGuard.DomainDbTestConnection);
        DatabaseSyncSafetyGuard.EnsureDatabaseExists(DatabaseSyncSafetyGuard.AIDbTestConnection);

        Assert.True(true, "All isolated *_Test databases created or verified without touching dev databases.");
    }

    [Fact]
    public async Task Test02_EFCoreMigrations_AppliesAllMigrationsWithoutPending()
    {
        // Apply all migrations across all 4 contexts
        await DatabaseBootstrapRunner.ApplyAllMigrationsAsync();

        // Verify PlatformDB_Test
        using (var saasDb = DatabaseBootstrapRunner.CreateSaaSDbContext())
        {
            var pending = (await saasDb.Database.GetPendingMigrationsAsync()).ToList();
            Assert.Empty(pending);
            var applied = (await saasDb.Database.GetAppliedMigrationsAsync()).ToList();
            Assert.NotEmpty(applied);
        }

        // Verify SecurityDB_Test
        using (var secDb = DatabaseBootstrapRunner.CreateSecurityDbContext())
        {
            var pending = (await secDb.Database.GetPendingMigrationsAsync()).ToList();
            Assert.Empty(pending);
            var applied = (await secDb.Database.GetAppliedMigrationsAsync()).ToList();
            Assert.NotEmpty(applied);
        }

        // Verify DomainDB_Test
        using (var domDb = DatabaseBootstrapRunner.CreateDomainDbContext())
        {
            var pending = (await domDb.Database.GetPendingMigrationsAsync()).ToList();
            Assert.Empty(pending);
            var applied = (await domDb.Database.GetAppliedMigrationsAsync()).ToList();
            Assert.NotEmpty(applied);
        }

        // Verify AIDatabase_Test
        using (var aiDb = DatabaseBootstrapRunner.CreateAIDbContext())
        {
            var pending = (await aiDb.Database.GetPendingMigrationsAsync()).ToList();
            Assert.Empty(pending);
            var applied = (await aiDb.Database.GetAppliedMigrationsAsync()).ToList();
            Assert.NotEmpty(applied);
        }
    }

    [Fact]
    public void Test03_SchemaComparison_VerifiesStructureAgainstEfCoreSnapshots()
    {
        // 1. SaaS / Platform
        using (var saasDb = DatabaseBootstrapRunner.CreateSaaSDbContext())
        {
            var rep = DatabaseSchemaInspector.Compare(saasDb, DatabaseSyncSafetyGuard.PlatformDbTestConnection);
            Assert.Empty(rep.Discrepancies);
            Assert.True(rep.DatabaseTableCount > 0);
            Assert.True(rep.DatabaseColumnCount > 0);
        }

        // 2. Security
        using (var secDb = DatabaseBootstrapRunner.CreateSecurityDbContext())
        {
            var rep = DatabaseSchemaInspector.Compare(secDb, DatabaseSyncSafetyGuard.SecurityDbTestConnection);
            Assert.Empty(rep.Discrepancies);
            Assert.True(rep.DatabaseTableCount > 0);
            Assert.True(rep.DatabaseColumnCount > 0);
        }

        // 3. Domain
        using (var domDb = DatabaseBootstrapRunner.CreateDomainDbContext())
        {
            var rep = DatabaseSchemaInspector.Compare(domDb, DatabaseSyncSafetyGuard.DomainDbTestConnection);
            Assert.Empty(rep.Discrepancies);
            Assert.True(rep.DatabaseTableCount > 0);
            Assert.True(rep.DatabaseColumnCount > 0);
        }

        // 4. AI
        using (var aiDb = DatabaseBootstrapRunner.CreateAIDbContext())
        {
            var rep = DatabaseSchemaInspector.Compare(aiDb, DatabaseSyncSafetyGuard.AIDbTestConnection);
            Assert.Empty(rep.Discrepancies);
            Assert.True(rep.DatabaseTableCount > 0);
            Assert.True(rep.DatabaseColumnCount > 0);
        }
    }

    [Fact]
    public void Test04_ConstraintsAndIndexes_VerifiesKeysAndUniqueness()
    {
        using (var secDb = DatabaseBootstrapRunner.CreateSecurityDbContext())
        {
            var rep = DatabaseSchemaInspector.Compare(secDb, DatabaseSyncSafetyGuard.SecurityDbTestConnection);
            Assert.True(rep.PrimaryKeyCount > 0, "SecurityDB must have primary keys");
            Assert.True(rep.ForeignKeyCount > 0, "SecurityDB must have foreign keys");
            Assert.True(rep.IndexCount > 0, "SecurityDB must have indexes");
        }

        using (var domDb = DatabaseBootstrapRunner.CreateDomainDbContext())
        {
            var rep = DatabaseSchemaInspector.Compare(domDb, DatabaseSyncSafetyGuard.DomainDbTestConnection);
            Assert.True(rep.PrimaryKeyCount > 0, "DomainDB must have primary keys");
            Assert.True(rep.ForeignKeyCount > 0, "DomainDB must have foreign keys");
            Assert.True(rep.IndexCount > 0, "DomainDB must have indexes");
        }
    }

    [Fact]
    public async Task Test05_Bootstrap_Profile1_Retail_SeedsCorrectMasterData()
    {
        // 1. Seed SaaS & Security
        using (var saasDb = DatabaseBootstrapRunner.CreateSaaSDbContext())
        {
            DatabaseBootstrapRunner.RunSaaSBootstrap(saasDb);
        }

        using (var secDb = DatabaseBootstrapRunner.CreateSecurityDbContext())
        {
            await DatabaseBootstrapRunner.RunSecurityBootstrapAsync(secDb);
        }

        // 2. Seed Retail Tenant Master Data
        using (var domDb = DatabaseBootstrapRunner.CreateDomainDbContext())
        {
            var result = await DatabaseBootstrapRunner.RunIndustryProfileBootstrapAsync(
                domDb,
                RetailTenantId,
                "retail",
                "RET-01",
                "Retail Supermart Ltd",
                "RET-BR01",
                "Retail Central Store");

            Assert.True(result.Success);

            var counts = await DatabaseBootstrapRunner.CountTenantMasterDataAsync(domDb, RetailTenantId, "retail");
            Assert.True(counts.CategoryCount > 0, "Retail categories should be seeded");
            Assert.True(counts.UomCount > 0, "Retail UOMs should be seeded");
            Assert.True(counts.ProductCount > 0, "Retail products should be seeded");
            Assert.True(counts.WarehouseCount > 0, "Retail warehouse should be seeded");
            Assert.True(counts.PriceCardCount > 0, "Retail price cards should be seeded");
        }
    }

    [Fact]
    public async Task Test06_Bootstrap_Profile2_Manufacturing_SeedsCorrectMasterData()
    {
        using (var domDb = DatabaseBootstrapRunner.CreateDomainDbContext())
        {
            var result = await DatabaseBootstrapRunner.RunIndustryProfileBootstrapAsync(
                domDb,
                ManufacturingTenantId,
                "manufacturing",
                "MFG-01",
                "Apex Manufacturing Corp",
                "MFG-BR01",
                "Plant 1 Assembly");

            Assert.True(result.Success);

            var counts = await DatabaseBootstrapRunner.CountTenantMasterDataAsync(domDb, ManufacturingTenantId, "manufacturing");
            Assert.True(counts.CategoryCount > 0, "Manufacturing categories should be seeded");
            Assert.True(counts.ProductCount > 0, "Manufacturing products should be seeded");
            Assert.True(counts.WarehouseCount > 0, "Manufacturing warehouse should be seeded");
        }
    }

    [Fact]
    public async Task Test07_Bootstrap_Profile3_Service_SeedsCorrectMasterData()
    {
        using (var domDb = DatabaseBootstrapRunner.CreateDomainDbContext())
        {
            var result = await DatabaseBootstrapRunner.RunIndustryProfileBootstrapAsync(
                domDb,
                ServiceTenantId,
                "service",
                "SVC-01",
                "Apex Consulting & Services",
                "SVC-BR01",
                "Colombo Headquarters");

            Assert.True(result.Success);

            var counts = await DatabaseBootstrapRunner.CountTenantMasterDataAsync(domDb, ServiceTenantId, "service");
            Assert.True(counts.CategoryCount > 0, "Service categories should be seeded");
            Assert.True(counts.ProductCount > 0, "Service items should be seeded");
        }
    }

    [Fact]
    public async Task Test08_Bootstrap_Profile4_Pharmacy_SeedsCorrectMasterData()
    {
        using (var domDb = DatabaseBootstrapRunner.CreateDomainDbContext())
        {
            var result = await DatabaseBootstrapRunner.RunIndustryProfileBootstrapAsync(
                domDb,
                PharmacyTenantId,
                "pharmacy",
                "PHM-01",
                "Apex Health & Pharmacy",
                "PHM-BR01",
                "Dispensary Main");

            Assert.True(result.Success);

            var counts = await DatabaseBootstrapRunner.CountTenantMasterDataAsync(domDb, PharmacyTenantId, "pharmacy");
            Assert.True(counts.CategoryCount > 0, "Pharmacy categories should be seeded");
            Assert.True(counts.ProductCount > 0, "Pharmacy medicines should be seeded");
            Assert.True(counts.WarehouseCount > 0, "Pharmacy warehouse should be seeded");
        }
    }

    [Fact]
    public async Task Test09_Bootstrap_Profile5_Wholesale_SeedsCorrectMasterData()
    {
        using (var domDb = DatabaseBootstrapRunner.CreateDomainDbContext())
        {
            var result = await DatabaseBootstrapRunner.RunIndustryProfileBootstrapAsync(
                domDb,
                WholesaleTenantId,
                "wholesale",
                "WHL-01",
                "Apex Wholesale Distributors",
                "WHL-BR01",
                "Distribution Center Alpha");

            Assert.True(result.Success);

            var counts = await DatabaseBootstrapRunner.CountTenantMasterDataAsync(domDb, WholesaleTenantId, "wholesale");
            Assert.True(counts.CategoryCount > 0, "Wholesale categories should be seeded");
            Assert.True(counts.ProductCount > 0, "Wholesale bulk products should be seeded");
            Assert.True(counts.WarehouseCount > 0, "Wholesale warehouse should be seeded");
        }
    }

    [Fact]
    public async Task Test10_PlatformLoginAndSecurity_ValidatesAdminAuthAndSeparation()
    {
        using (var secDb = DatabaseBootstrapRunner.CreateSecurityDbContext())
        {
            var user = await secDb.Users.FirstOrDefaultAsync(u => u.Username == "admin");
            Assert.NotNull(user);
            Assert.Null(user.TenantId); // Platform admin must be globally scoped

            // Verify password using PasswordService (VerifyPassword expects password first, storedHash second)
            var pwdService = new PasswordService();
            bool validAuth = pwdService.VerifyPassword("123", user.PasswordHash);
            Assert.True(validAuth, "Platform admin password '123' must verify successfully");

            bool invalidAuth = pwdService.VerifyPassword("WrongPassword!999", user.PasswordHash);
            Assert.False(invalidAuth, "Invalid password must be rejected");

            // Verify platform admin role assignment
            var userRoles = await secDb.UserRoles
                .Where(ur => ur.UserId == user.Id)
                .Join(secDb.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => r.Name)
                .ToListAsync();

            Assert.Contains("Platform Admin", userRoles);
        }
    }

    [Fact]
    public async Task Test11_TenantIsolation_VerifiesDataAndRoleBoundaries()
    {
        using (var domDb = DatabaseBootstrapRunner.CreateDomainDbContext())
        {
            // Verify tenant data boundary: Retail products vs Manufacturing products
            var retailProducts = await domDb.Products.Where(p => p.TenantId == RetailTenantId).ToListAsync();
            var mfgProducts = await domDb.Products.Where(p => p.TenantId == ManufacturingTenantId).ToListAsync();

            Assert.NotEmpty(retailProducts);
            Assert.NotEmpty(mfgProducts);

            // Assert no overlap
            var retailIds = new HashSet<Guid>(retailProducts.Select(p => p.ProductId));
            var overlap = mfgProducts.Any(p => retailIds.Contains(p.ProductId));
            Assert.False(overlap, "Tenant data boundary violated: Products overlap between tenants!");
        }

        // Verify cross-database foreign keys are zero
        var crossFks = DatabaseSchemaInspector.CheckForCrossDatabaseForeignKeys(DatabaseSyncSafetyGuard.DomainDbTestConnection);
        Assert.Empty(crossFks);
    }

    [Fact]
    public async Task Test12_BootstrapSecondRun_IsIdempotentAndProducesNoDuplicates()
    {
        using (var domDb = DatabaseBootstrapRunner.CreateDomainDbContext())
        using (var secDb = DatabaseBootstrapRunner.CreateSecurityDbContext())
        using (var saasDb = DatabaseBootstrapRunner.CreateSaaSDbContext())
        {
            // 1. Snapshot counts before 2nd run
            var beforeRetail = await DatabaseBootstrapRunner.CountTenantMasterDataAsync(domDb, RetailTenantId, "retail");
            var beforeMfg = await DatabaseBootstrapRunner.CountTenantMasterDataAsync(domDb, ManufacturingTenantId, "manufacturing");
            var beforeUsers = await secDb.Users.CountAsync();
            var beforeRoles = await secDb.Roles.CountAsync();
            var beforePermissions = await secDb.Permissions.CountAsync();
            var beforePlans = await saasDb.SubscriptionPlans.CountAsync();

            // 2. Run Bootstrap a SECOND time
            DatabaseBootstrapRunner.RunSaaSBootstrap(saasDb);
            await DatabaseBootstrapRunner.RunSecurityBootstrapAsync(secDb);

            await DatabaseBootstrapRunner.RunIndustryProfileBootstrapAsync(
                domDb, RetailTenantId, "retail", "RET-01", "Retail Supermart Ltd", "RET-BR01", "Retail Central Store");
            await DatabaseBootstrapRunner.RunIndustryProfileBootstrapAsync(
                domDb, ManufacturingTenantId, "manufacturing", "MFG-01", "Apex Manufacturing Corp", "MFG-BR01", "Plant 1 Assembly");

            // 3. Snapshot counts after 2nd run
            var afterRetail = await DatabaseBootstrapRunner.CountTenantMasterDataAsync(domDb, RetailTenantId, "retail");
            var afterMfg = await DatabaseBootstrapRunner.CountTenantMasterDataAsync(domDb, ManufacturingTenantId, "manufacturing");
            var afterUsers = await secDb.Users.CountAsync();
            var afterRoles = await secDb.Roles.CountAsync();
            var afterPermissions = await secDb.Permissions.CountAsync();
            var afterPlans = await saasDb.SubscriptionPlans.CountAsync();

            // 4. Assert ZERO duplicate records created
            Assert.Equal(beforeRetail.TotalEntities, afterRetail.TotalEntities);
            Assert.Equal(beforeRetail.ProductCount, afterRetail.ProductCount);
            Assert.Equal(beforeRetail.CategoryCount, afterRetail.CategoryCount);
            Assert.Equal(beforeMfg.TotalEntities, afterMfg.TotalEntities);
            Assert.Equal(beforeUsers, afterUsers);
            Assert.Equal(beforeRoles, afterRoles);
            Assert.Equal(beforePermissions, afterPermissions);
            Assert.Equal(beforePlans, afterPlans);
        }
    }

    [Fact]
    public async Task Test13_DataSynchronisation_PreservesSystemAndUserRecords()
    {
        using (var domDb = DatabaseBootstrapRunner.CreateDomainDbContext())
        {
            // 1. Add a custom user category for Retail tenant
            string customCode = "CUSTOM-USER-CAT";
            var existingCat = await domDb.Categories.FirstOrDefaultAsync(c => c.TenantId == RetailTenantId && c.CategoryCode == customCode);
            if (existingCat == null)
            {
                var newCat = Category.Create(
                    RetailTenantId,
                    customCode,
                    "User Custom Specialty Category",
                    "Added by store manager");
                domDb.Categories.Add(newCat);
                await domDb.SaveChangesAsync();
            }

            // 2. Re-run bootstrap
            await DatabaseBootstrapRunner.RunIndustryProfileBootstrapAsync(
                domDb, RetailTenantId, "retail", "RET-01", "Retail Supermart Ltd", "RET-BR01", "Retail Central Store");

            // 3. Confirm user category still exists intact and active
            var userCatAfter = await domDb.Categories.FirstOrDefaultAsync(c => c.TenantId == RetailTenantId && c.CategoryCode == customCode);
            Assert.NotNull(userCatAfter);
            Assert.Equal("User Custom Specialty Category", userCatAfter.CategoryName);
            Assert.True(userCatAfter.IsActive);
        }
    }

    [Fact]
    public void Test14_SafeCleanup_GuaranteesDevelopmentDatabasesUntouched()
    {
        // Assert that all connection strings configured in tests point strictly to _Test databases
        var testConnections = new[]
        {
            DatabaseSyncSafetyGuard.PlatformDbTestConnection,
            DatabaseSyncSafetyGuard.SecurityDbTestConnection,
            DatabaseSyncSafetyGuard.DomainDbTestConnection,
            DatabaseSyncSafetyGuard.AIDbTestConnection
        };

        foreach (var conn in testConnections)
        {
            var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(conn);
            Assert.EndsWith("_Test", builder.InitialCatalog, StringComparison.OrdinalIgnoreCase);
        }

        // Assert development databases cannot be passed to safety guard without immediate exception
        var devDatabases = new[] { "PlatformDB", "SecurityDB", "DomainDB", "AIDatabase" };
        foreach (var devDb in devDatabases)
        {
            var ex = Assert.Throws<InvalidOperationException>(() =>
                DatabaseSyncSafetyGuard.AssertSafeTestDatabase($"Server=(localdb)\\MSSQLLocalDB;Database={devDb};Trusted_Connection=True;"));
            Assert.Contains("SAFETY VIOLATION", ex.Message);
        }
    }
}
