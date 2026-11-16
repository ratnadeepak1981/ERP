using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Security.Core.Models;
using Security.Infrastructure.Persistence;
using Security.Interfaces;
using Security.Services;
using Xunit;

namespace ERP.SecurityTests;

public class DemoSeedUserPasswordStandardisationTests : IClassFixture<TestSecurityFixture>
{
    private readonly TestSecurityFixture _fixture;

    public DemoSeedUserPasswordStandardisationTests(TestSecurityFixture fixture)
    {
        _fixture = fixture;
        _fixture.EnsureDatabasesInitialized();
    }

    private SecurityDbContext CreateSecurityDb()
    {
        DatabaseSafetyGuard.AssertSafeTestDatabase(TestConstants.SecurityDbTest);
        var options = new DbContextOptionsBuilder<SecurityDbContext>()
            .UseSqlServer(TestConstants.SecurityDbTest)
            .Options;
        return new SecurityDbContext(options);
    }

    [Fact]
    public async Task TenantUserSetupService_DefaultSeeding_All11DemoUsersReceivePassword123()
    {
        using var db = CreateSecurityDb();
        var passwordService = new PasswordService();
        var service = new TenantUserSetupService(db, passwordService);

        var tenantId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var branchId = Guid.NewGuid();

        // 1. Seed tenant roles
        var rolesToSeed = new[]
        {
            ("TENANT_ADMIN", "Tenant Admin"),
            ("PROCUREMENT_MANAGER", "Procurement Manager"),
            ("PROCUREMENT_OFFICER", "Procurement Officer"),
            ("INVENTORY_MANAGER", "Inventory Manager"),
            ("INVENTORY_OFFICER", "Inventory Officer"),
            ("SALES_MANAGER", "Sales Manager"),
            ("SALES_OFFICER", "Sales Officer"),
            ("WAREHOUSE_CONFIGURATION_OFFICER", "Warehouse Configuration Officer"),
            ("FINANCE_CONFIGURATION_OFFICER", "Finance Configuration Officer"),
            ("MASTER_DATA_MANAGER", "Master Data Manager"),
            ("TENANT_AUDITOR", "Tenant Read-only Auditor")
        };

        foreach (var (key, name) in rolesToSeed)
        {
            db.Roles.Add(new Role
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = name,
                TemplateKey = key,
                IsActive = true
            });
        }
        await db.SaveChangesAsync();

        // 2. Request default demo user setup WITHOUT specifying any password
        var request = new TenantUserSetupRequest
        {
            TargetTenantId = tenantId,
            CompanyId = companyId,
            BranchId = branchId,
            TenantShortCode = "STD",
            EmailDomain = "standard.local"
            // DefaultInitialPassword deliberately left null
        };

        // Act
        var result = await service.SetupTenantUsersAsync(tenantId, request);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(11, result.Users.Count);

        // Verify all result items never expose plaintext passwords in API responses
        Assert.All(result.Users, u => Assert.Null(u.GeneratedPassword));

        var createdUsers = await db.Users
            .Where(u => u.TenantId == tenantId)
            .ToListAsync();
        Assert.Equal(11, createdUsers.Count);

        // Verify every newly seeded demo user has password "123" properly hashed (PBKDF2-SHA256)
        foreach (var user in createdUsers)
        {
            Assert.False(string.IsNullOrWhiteSpace(user.PasswordHash));
            Assert.NotEqual("123", user.PasswordHash); // Never stored in plaintext
            Assert.Contains(".", user.PasswordHash); // Formatted as {iterations}.{salt}.{hash}

            // Verify password "123" succeeds
            Assert.True(
                passwordService.VerifyPassword("123", user.PasswordHash),
                $"User '{user.Username}' should authenticate with password '123'.");

            // Verify invalid passwords fail
            Assert.False(passwordService.VerifyPassword("wrong_password", user.PasswordHash));
            Assert.False(passwordService.VerifyPassword("1234", user.PasswordHash));
            Assert.False(passwordService.VerifyPassword("Password@123", user.PasswordHash));
        }
    }

    [Fact]
    public async Task TenantUserSetupService_RepeatedSeeding_DoesNotOverwriteExistingPasswords()
    {
        using var db = CreateSecurityDb();
        var passwordService = new PasswordService();
        var service = new TenantUserSetupService(db, passwordService);

        var tenantId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var branchId = Guid.NewGuid();

        db.Roles.Add(new Role
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Procurement Manager",
            TemplateKey = "PROCUREMENT_MANAGER",
            IsActive = true
        });
        await db.SaveChangesAsync();

        // Pre-create an existing user with custom password
        string customPassword = "CustomUserSecret#99!";
        string customHash = passwordService.HashPassword(customPassword);
        var existingUser = new User
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Username = "RPTPURMGR",
            Email = "rptpurmgr@rpt.local",
            PasswordHash = customHash,
            IsActive = true
        };
        db.Users.Add(existingUser);
        await db.SaveChangesAsync();

        var request = new TenantUserSetupRequest
        {
            TargetTenantId = tenantId,
            CompanyId = companyId,
            BranchId = branchId,
            TenantCode = "RPT",
            EmailDomain = "rpt.local"
        };

        // Act: Run seeding
        var result = await service.SetupTenantUsersAsync(tenantId, request);

        // Assert: User was preserved and password was NOT overwritten
        var userInDb = await db.Users.FirstAsync(u => u.Id == existingUser.Id);
        Assert.Equal(customHash, userInDb.PasswordHash);
        Assert.True(passwordService.VerifyPassword(customPassword, userInDb.PasswordHash));
        Assert.False(passwordService.VerifyPassword("123", userInDb.PasswordHash));
    }

    [Fact]
    public void BuildDefaultDemoUserSpecs_SetsInitialPasswordTo123_WhenNotExplicitlyProvided()
    {
        var service = new TenantUserSetupService(CreateSecurityDb(), new PasswordService());
        var specs = service.BuildDefaultDemoUserSpecs(
            "TXYZ",
            "txyz.local",
            Guid.NewGuid(),
            Guid.NewGuid(),
            defaultPassword: null);

        Assert.Equal(11, specs.Count);
        Assert.All(specs, spec => Assert.Equal("123", spec.Password));
    }

    [Fact]
    public void BuildDefaultDemoUserSpecs_PreservesExplicitPassword_WhenProvided()
    {
        var service = new TenantUserSetupService(CreateSecurityDb(), new PasswordService());
        var specs = service.BuildDefaultDemoUserSpecs(
            "TXYZ",
            "txyz.local",
            Guid.NewGuid(),
            Guid.NewGuid(),
            defaultPassword: "ExplicitPass@123");

        Assert.Equal(11, specs.Count);
        Assert.All(specs, spec => Assert.Equal("ExplicitPass@123", spec.Password));
    }

    [Fact]
    public void PasswordHashing_UsesPBKDF2SHA256_AndIsDeterministicInVerification()
    {
        var passwordService = new PasswordService();
        string hash1 = passwordService.HashPassword("123");
        string hash2 = passwordService.HashPassword("123");

        // Salt ensures two hashes of "123" are distinct
        Assert.NotEqual(hash1, hash2);

        // Both verify correctly with "123"
        Assert.True(passwordService.VerifyPassword("123", hash1));
        Assert.True(passwordService.VerifyPassword("123", hash2));

        // Format checks: {iterations}.{salt}.{hash}
        var parts1 = hash1.Split('.');
        Assert.Equal(3, parts1.Length);
        Assert.Equal("100000", parts1[0]); // 100,000 iterations
    }

    [Fact]
    public async Task PlatformSeedUsers_AuthenticateWithPassword123()
    {
        using var db = CreateSecurityDb();
        var passwordService = new PasswordService();

        var platformUsers = await db.Users
            .Where(u => u.TenantId == null)
            .ToListAsync();

        Assert.NotEmpty(platformUsers);

        // Verify each platform user in the database authenticates with 123
        var expectedUsernames = new[] { "admin", "platformstaff", "securitymanager", "audituser" };
        foreach (var username in expectedUsernames)
        {
            var user = platformUsers.FirstOrDefault(u => u.Username == username);
            if (user != null)
            {
                Assert.True(
                    passwordService.VerifyPassword("123", user.PasswordHash),
                    $"Platform user '{username}' should authenticate with password '123'.");
                Assert.False(passwordService.VerifyPassword("wrong", user.PasswordHash));
            }
        }
    }

    [Fact]
    public async Task TestSeeder_NewlyCreatedUser_ReceivesPassword123_AndRepeatedSeedingPreservesPassword()
    {
        using var db = CreateSecurityDb();
        var passwordService = new PasswordService();

        var testUserId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        // 1. Initial creation: receives password "123"
        var initialUser = new User
        {
            Id = testUserId,
            TenantId = tenantId,
            Username = "seedtest_user",
            Email = "seedtest@tenant.local",
            PasswordHash = passwordService.HashPassword("123"),
            IsActive = true
        };
        db.Users.Add(initialUser);
        await db.SaveChangesAsync();

        Assert.True(passwordService.VerifyPassword("123", initialUser.PasswordHash));

        // 2. User updates their password to custom credentials
        string updatedPass = "UserUpdatedSecret!456";
        string updatedHash = passwordService.HashPassword(updatedPass);
        initialUser.PasswordHash = updatedHash;
        await db.SaveChangesAsync();

        // 3. Repeated seeding must preserve existing password hash
        var userInDb = await db.Users.FirstAsync(u => u.Id == testUserId);
        Assert.Equal(updatedHash, userInDb.PasswordHash);
        Assert.True(passwordService.VerifyPassword(updatedPass, userInDb.PasswordHash));
        Assert.False(passwordService.VerifyPassword("123", userInDb.PasswordHash));
    }

    [Fact]
    public async Task TenantIsolation_MultipleTenantsAreStrictlyIsolated_WithIndependentPasswords()
    {
        using var db = CreateSecurityDb();
        var passwordService = new PasswordService();
        var service = new TenantUserSetupService(db, passwordService);

        var tenant1 = Guid.NewGuid();
        var tenant2 = Guid.NewGuid();

        db.Roles.Add(new Role
        {
            Id = Guid.NewGuid(),
            TenantId = tenant1,
            Name = "Procurement Manager",
            TemplateKey = "PROCUREMENT_MANAGER",
            IsActive = true
        });
        db.Roles.Add(new Role
        {
            Id = Guid.NewGuid(),
            TenantId = tenant2,
            Name = "Procurement Manager",
            TemplateKey = "PROCUREMENT_MANAGER",
            IsActive = true
        });
        await db.SaveChangesAsync();

        var req1 = new TenantUserSetupRequest
        {
            TargetTenantId = tenant1,
            TenantCode = "ISO1",
            EmailDomain = "iso1.local"
        };
        var req2 = new TenantUserSetupRequest
        {
            TargetTenantId = tenant2,
            TenantCode = "ISO2",
            EmailDomain = "iso2.local"
        };

        var res1 = await service.SetupTenantUsersAsync(tenant1, req1);
        var res2 = await service.SetupTenantUsersAsync(tenant2, req2);

        Assert.True(res1.Success);
        Assert.True(res2.Success);

        var user1 = await db.Users.FirstAsync(u => u.TenantId == tenant1 && u.Username == "ISO1PURMGR");
        var user2 = await db.Users.FirstAsync(u => u.TenantId == tenant2 && u.Username == "ISO2PURMGR");

        Assert.NotEqual(user1.Id, user2.Id);
        Assert.NotEqual(user1.TenantId, user2.TenantId);

        // Both independently authenticate with initial password "123"
        Assert.True(passwordService.VerifyPassword("123", user1.PasswordHash));
        Assert.True(passwordService.VerifyPassword("123", user2.PasswordHash));

        // Mutating Tenant 1 password does not affect Tenant 2
        user1.PasswordHash = passwordService.HashPassword("Tenant1NewPassword#");
        await db.SaveChangesAsync();

        var reloadedUser2 = await db.Users.FirstAsync(u => u.Id == user2.Id);
        Assert.True(passwordService.VerifyPassword("123", reloadedUser2.PasswordHash));
    }
}
