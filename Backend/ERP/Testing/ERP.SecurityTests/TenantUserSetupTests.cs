using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Security.Core.Models;
using Security.Infrastructure.Persistence;
using Security.Interfaces;
using Security.Services;
using Xunit;

namespace ERP.SecurityTests;

public class TenantUserSetupTests : IClassFixture<TestSecurityFixture>
{
    private readonly TestSecurityFixture _fixture;

    public TenantUserSetupTests(TestSecurityFixture fixture)
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
    public async Task SetupTenantUsersAsync_CreatesCoreAccounts_WithCorrectRolesAndScopes()
    {
        using var db = CreateSecurityDb();
        var passwordService = new PasswordService();
        var service = new TenantUserSetupService(db, passwordService);

        var tenantId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var branchId = Guid.NewGuid();

        // 1. Seed tenant roles for this test tenant using standard templates
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

        var request = new TenantUserSetupRequest
        {
            TargetTenantId = tenantId,
            CompanyId = companyId,
            BranchId = branchId,
            TenantShortCode = "tst",
            EmailDomain = "test.local",
            DefaultInitialPassword = "TestPassword@2026!"
        };

        // Act
        var result = await service.SetupTenantUsersAsync(tenantId, request);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(11, result.Users.Count);
        Assert.All(result.Users, u => Assert.True(u.UserCreated));
        Assert.All(result.Users, u => Assert.True(u.RoleAssigned));
        Assert.All(result.Users, u => Assert.True(u.ScopeAssigned));
        Assert.All(result.Users, u => Assert.Equal(companyId, u.CompanyId));
        Assert.All(result.Users, u => Assert.Equal(branchId, u.BranchId));

        // Verify users exist in database
        var createdUsers = await db.Users
            .Where(u => u.TenantId == tenantId)
            .ToListAsync();
        Assert.Equal(11, createdUsers.Count);

        // Verify naming convention: {TENANT_CODE}{MODULE_CODE}{ROLE_CODE}
        var expectedUsernames = new[]
        {
            "TSTADM", "TSTPURMGR", "TSTPURSTF", "TSTINVMGR", "TSTINVSTF",
            "TSTSALMGR", "TSTSALSTF", "TSTWHMGR", "TSTFINMGR", "TSTMDMMGR", "TSTAUDVIEW"
        };
        foreach (var expectedUsername in expectedUsernames)
        {
            Assert.Contains(createdUsers, u => u.Username == expectedUsername);
        }

        // Verify passwords were properly hashed
        foreach (var user in createdUsers)
        {
            Assert.True(passwordService.VerifyPassword("TestPassword@2026!", user.PasswordHash));
        }
    }

    [Fact]
    public async Task SetupTenantUsersAsync_IsIdempotent_PreservesExistingAccountsAndZeroDuplicates()
    {
        using var db = CreateSecurityDb();
        var passwordService = new PasswordService();
        var service = new TenantUserSetupService(db, passwordService);

        var tenantId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var branchId = Guid.NewGuid();

        // Seed roles
        db.Roles.Add(new Role
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Procurement Manager",
            TemplateKey = "PROCUREMENT_MANAGER",
            IsActive = true
        });
        await db.SaveChangesAsync();

        var request = new TenantUserSetupRequest
        {
            TargetTenantId = tenantId,
            CompanyId = companyId,
            BranchId = branchId,
            TenantCode = "IDEM",
            EmailDomain = "idem.local",
            DefaultInitialPassword = "OriginalPassword@123"
        };

        // Pass 1: Creation
        var result1 = await service.SetupTenantUsersAsync(tenantId, request);
        var originalUser = await db.Users.FirstAsync(u => u.TenantId == tenantId && u.Username == "IDEMPURMGR");
        string originalHash = originalUser.PasswordHash;

        // Pass 2: Repeated execution with different password in request
        request.DefaultInitialPassword = "NewAttemptedPassword@456";
        var result2 = await service.SetupTenantUsersAsync(tenantId, request);

        // Assert Pass 2
        var purmgrResult2 = result2.Users.First(u => u.Username == "IDEMPURMGR");
        Assert.False(purmgrResult2.UserCreated);
        Assert.False(purmgrResult2.RoleAssigned);
        Assert.False(purmgrResult2.ScopeAssigned);
        Assert.Equal("Reused", purmgrResult2.Status);

        // Verify password hash was NOT altered
        var userAfter = await db.Users.FirstAsync(u => u.TenantId == tenantId && u.Username == "IDEMPURMGR");
        Assert.Equal(originalHash, userAfter.PasswordHash);
        Assert.True(passwordService.VerifyPassword("OriginalPassword@123", userAfter.PasswordHash));
        Assert.False(passwordService.VerifyPassword("NewAttemptedPassword@456", userAfter.PasswordHash));

        // Verify no duplicate users, roles, or scopes in database
        int userCount = await db.Users.CountAsync(u => u.TenantId == tenantId && u.Username == "IDEMPURMGR");
        Assert.Equal(1, userCount);

        int userRoleCount = await db.UserRoles.CountAsync(ur => ur.UserId == originalUser.Id);
        Assert.Equal(1, userRoleCount);

        int scopeCount = await db.UserRoleScopes.CountAsync(s => s.UserRole.UserId == originalUser.Id);
        Assert.Equal(1, scopeCount);
    }

    [Fact]
    public async Task SetupTenantUsersAsync_PreservesExistingTenantAdmin()
    {
        using var db = CreateSecurityDb();
        var passwordService = new PasswordService();
        var service = new TenantUserSetupService(db, passwordService);

        var tenantId = Guid.NewGuid();

        // Pre-create an administrator with a specific hash
        string adminPassword = "PreExistingAdminPass!99";
        var preExistingAdmin = new User
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Username = "existing.admin",
            Email = "existing.admin@domain.local",
            PasswordHash = passwordService.HashPassword(adminPassword),
            IsActive = true
        };
        db.Users.Add(preExistingAdmin);

        var adminRole = new Role
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Tenant Admin",
            TemplateKey = "TENANT_ADMIN",
            IsActive = true
        };
        db.Roles.Add(adminRole);
        await db.SaveChangesAsync();

        var request = new TenantUserSetupRequest
        {
            TargetTenantId = tenantId,
            TenantShortCode = "ext",
            EmailDomain = "ext.local",
            DefaultInitialPassword = "NewUsersPassword!1"
        };

        // Act
        var result = await service.SetupTenantUsersAsync(tenantId, request);

        // Assert
        Assert.Contains(result.Messages, m => m.Contains("existing.admin") && m.Contains("preserved"));

        var adminAfter = await db.Users.FirstAsync(u => u.Id == preExistingAdmin.Id);
        Assert.True(passwordService.VerifyPassword(adminPassword, adminAfter.PasswordHash));
        Assert.False(passwordService.VerifyPassword("NewUsersPassword!1", adminAfter.PasswordHash));
    }

    [Fact]
    public async Task TenantUser_SegregationOfDuties_ManagerVsStaffAndAuditor()
    {
        using var db = CreateSecurityDb();

        // Inspect permissions for Procurement Manager vs Procurement Officer in Test DB
        var mgrPermissions = await (from rp in db.RolePermissions
                                    join r in db.Roles on rp.RoleId equals r.Id
                                    join p in db.Permissions on rp.PermissionId equals p.Id
                                    where r.TemplateKey == "PROCUREMENT_MANAGER"
                                    select p.Code).Distinct().ToListAsync();

        var stfPermissions = await (from rp in db.RolePermissions
                                    join r in db.Roles on rp.RoleId equals r.Id
                                    join p in db.Permissions on rp.PermissionId equals p.Id
                                    where r.TemplateKey == "PROCUREMENT_OFFICER"
                                    select p.Code).Distinct().ToListAsync();

        var auditorPermissions = await (from rp in db.RolePermissions
                                        join r in db.Roles on rp.RoleId equals r.Id
                                        join p in db.Permissions on rp.PermissionId equals p.Id
                                        where r.TemplateKey == "TENANT_AUDITOR"
                                        select p.Code).Distinct().ToListAsync();

        // 1. Manager has approval authorities
        Assert.Contains("PURCHASE_ORDER.APPROVE", mgrPermissions);
        Assert.Contains("PURCHASE_REQUISITION.APPROVE", mgrPermissions);

        // 2. Staff does NOT have approval authorities
        Assert.DoesNotContain("PURCHASE_ORDER.APPROVE", stfPermissions);
        Assert.DoesNotContain("PURCHASE_REQUISITION.APPROVE", stfPermissions);

        // 3. Auditor has only VIEW / READ permissions (no CREATE, EDIT, DELETE, POST, APPROVE)
        Assert.All(auditorPermissions, p =>
        {
            Assert.DoesNotContain("APPROVE", p);
            Assert.DoesNotContain("POST", p);
            Assert.DoesNotContain("CREATE", p);
            Assert.DoesNotContain("EDIT", p);
            Assert.DoesNotContain("DELETE", p);
        });
    }

    [Fact]
    public async Task TenantUser_EnforcesTenantIsolation()
    {
        using var db = CreateSecurityDb();
        var passwordService = new PasswordService();
        var service = new TenantUserSetupService(db, passwordService);

        var tenant1 = Guid.NewGuid();
        var tenant2 = Guid.NewGuid();

        // Seed role for tenant 1 only
        db.Roles.Add(new Role
        {
            Id = Guid.NewGuid(),
            TenantId = tenant1,
            Name = "Procurement Manager",
            TemplateKey = "PROCUREMENT_MANAGER",
            IsActive = true
        });
        await db.SaveChangesAsync();

        var request1 = new TenantUserSetupRequest
        {
            TargetTenantId = tenant1,
            TenantCode = "TN1",
            EmailDomain = "tenant1.local",
            DefaultInitialPassword = "Tenant1Password@123"
        };

        // Act
        await service.SetupTenantUsersAsync(tenant1, request1);

        // Verify tenant 1 users cannot be seen when querying tenant 2
        var tenant2Users = await db.Users.Where(u => u.TenantId == tenant2).ToListAsync();
        Assert.Empty(tenant2Users);

        var tenant1Users = await db.Users.Where(u => u.TenantId == tenant1).ToListAsync();
        Assert.NotEmpty(tenant1Users);
        Assert.All(tenant1Users, u => Assert.Equal(tenant1, u.TenantId));
        Assert.All(tenant1Users, u => Assert.StartsWith("TN1", u.Username));
    }

    [Fact]
    public async Task MultiTenant_TwoTenants_HaveCompletelyIsolatedUsers_AndStandardNamingConvention()
    {
        using var db = CreateSecurityDb();
        var passwordService = new PasswordService();
        var service = new TenantUserSetupService(db, passwordService);

        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var companyA = Guid.NewGuid();
        var branchA = Guid.NewGuid();
        var companyB = Guid.NewGuid();
        var branchB = Guid.NewGuid();

        // Seed roles for Tenant A and Tenant B
        var standardRoleKeys = new[]
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

        foreach (var (key, name) in standardRoleKeys)
        {
            db.Roles.Add(new Role { Id = Guid.NewGuid(), TenantId = tenantA, TemplateKey = key, Name = name, IsActive = true });
            db.Roles.Add(new Role { Id = Guid.NewGuid(), TenantId = tenantB, TemplateKey = key, Name = name, IsActive = true });
        }
        await db.SaveChangesAsync();

        var requestA = new TenantUserSetupRequest
        {
            TargetTenantId = tenantA,
            CompanyId = companyA,
            BranchId = branchA,
            TenantCode = "APXQA",
            EmailDomain = "apxqa.local",
            DefaultInitialPassword = "PasswordA@123!"
        };

        var requestB = new TenantUserSetupRequest
        {
            TargetTenantId = tenantB,
            CompanyId = companyB,
            BranchId = branchB,
            TenantCode = "APXDEMO",
            EmailDomain = "apexdemo.local",
            DefaultInitialPassword = "PasswordB@456!"
        };

        // 1. Provision Tenant A
        var resultA = await service.SetupTenantUsersAsync(tenantA, requestA);
        Assert.True(resultA.Success);
        Assert.Equal(11, resultA.Users.Count);
        Assert.All(resultA.Users, u => Assert.StartsWith("APXQA", u.Username));
        Assert.All(resultA.Users, u => Assert.EndsWith("@apxqa.local", u.Email));
        Assert.Contains(resultA.Users, u => u.Username == "APXQAADM");
        Assert.Contains(resultA.Users, u => u.Username == "APXQAPURMGR");
        Assert.Contains(resultA.Users, u => u.Username == "APXQAMDMMGR");
        Assert.Contains(resultA.Users, u => u.Username == "APXQAAUDVIEW");

        // 2. Provision Tenant B
        var resultB = await service.SetupTenantUsersAsync(tenantB, requestB);
        Assert.True(resultB.Success);
        Assert.Equal(11, resultB.Users.Count);
        Assert.All(resultB.Users, u => Assert.StartsWith("APXDEMO", u.Username));
        Assert.All(resultB.Users, u => Assert.EndsWith("@apexdemo.local", u.Email));
        Assert.Contains(resultB.Users, u => u.Username == "APXDEMOADM");
        Assert.Contains(resultB.Users, u => u.Username == "APXDEMOPURMGR");
        Assert.Contains(resultB.Users, u => u.Username == "APXDEMOMDMMGR");
        Assert.Contains(resultB.Users, u => u.Username == "APXDEMOAUDVIEW");

        // 3. Verify distinct UserIds across tenants (NO shared user records)
        var userIdsA = resultA.Users.Select(u => u.UserId).ToHashSet();
        var userIdsB = resultB.Users.Select(u => u.UserId).ToHashSet();
        Assert.Empty(userIdsA.Intersect(userIdsB));

        // 4. Verify distinct roles across tenants (Tenant A role cannot be assigned to Tenant B user)
        var roleIdsA = resultA.Users.Select(u => u.RoleId).ToHashSet();
        var roleIdsB = resultB.Users.Select(u => u.RoleId).ToHashSet();
        Assert.Empty(roleIdsA.Intersect(roleIdsB));

        // 5. Verify database records
        var dbUsersA = await db.Users.Where(u => u.TenantId == tenantA).ToListAsync();
        var dbUsersB = await db.Users.Where(u => u.TenantId == tenantB).ToListAsync();
        Assert.Equal(11, dbUsersA.Count);
        Assert.Equal(11, dbUsersB.Count);

        // 6. Idempotency test for Tenant A: Re-run should mark as Reused and preserve password
        var rerunResultA = await service.SetupTenantUsersAsync(tenantA, requestA);
        Assert.True(rerunResultA.Success);
        Assert.All(rerunResultA.Users, u => Assert.Equal("Reused", u.Status));
        Assert.All(rerunResultA.Users, u => Assert.False(u.UserCreated));

        // 7. Verify Tenant B is completely unaffected by Tenant A rerun
        var countBAfterRerun = await db.Users.CountAsync(u => u.TenantId == tenantB);
        Assert.Equal(11, countBAfterRerun);
    }
}
