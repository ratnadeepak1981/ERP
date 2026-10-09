using System;
using System.Linq;
using Domain.Features.MasterData.Branch;
using Domain.Features.MasterData.Company;
using Domain.Features.MasterData.Product;
using Domain.Infrastructure.Persistence;
using ERP.Domain.Features.MasterData.Category;
using SaaS.Core.Models;
using SaaS.Infrastructure.Persistence;
using Security.Core.Models;
using Security.Infrastructure.Persistence;

namespace ERP.SecurityTests;

public static class TestSeeder
{
    public static void SeedAll(
        SaaSDbContext platformDb,
        SecurityDbContext securityDb,
        DomainDbContext domainDb)
    {
        SeedPlatform(platformDb);
        SeedDomain(domainDb);
        SeedSecurity(securityDb);
    }

    private static void SeedPlatform(SaaSDbContext db)
    {
        if (!db.Tenants.Any(x => x.Id == TestConstants.TenantAlphaId))
        {
            db.Tenants.Add(new Tenant
            {
                Id = TestConstants.TenantAlphaId,
                Name = "Tenant Alpha",
                Code = "ALPHA",
                IsActive = true
            });
        }

        if (!db.Tenants.Any(x => x.Id == TestConstants.TenantBetaId))
        {
            db.Tenants.Add(new Tenant
            {
                Id = TestConstants.TenantBetaId,
                Name = "Tenant Beta",
                Code = "BETA",
                IsActive = true
            });
        }

        db.SaveChanges();
    }

    private static void SeedDomain(DomainDbContext db)
    {
        // Companies
        if (!db.Companies.Any(x => x.Id == TestConstants.CompanyAId))
        {
            db.Companies.Add(new Company
            {
                Id = TestConstants.CompanyAId,
                TenantId = TestConstants.TenantAlphaId,
                Name = "Company A",
                Code = "COMP_A",
                IsActive = true
            });
        }

        if (!db.Companies.Any(x => x.Id == TestConstants.CompanyBId))
        {
            db.Companies.Add(new Company
            {
                Id = TestConstants.CompanyBId,
                TenantId = TestConstants.TenantAlphaId,
                Name = "Company B",
                Code = "COMP_B",
                IsActive = true
            });
        }

        if (!db.Companies.Any(x => x.Id == TestConstants.CompanyCId))
        {
            db.Companies.Add(new Company
            {
                Id = TestConstants.CompanyCId,
                TenantId = TestConstants.TenantBetaId,
                Name = "Company C",
                Code = "COMP_C",
                IsActive = true
            });
        }

        db.SaveChanges();

        // Branches
        var branches = new[]
        {
            new Branch { Id = TestConstants.BranchA1Id, CompanyId = TestConstants.CompanyAId, Name = "Branch A1", Code = "BR_A1", IsActive = true },
            new Branch { Id = TestConstants.BranchA2Id, CompanyId = TestConstants.CompanyAId, Name = "Branch A2", Code = "BR_A2", IsActive = true },
            new Branch { Id = TestConstants.BranchB1Id, CompanyId = TestConstants.CompanyBId, Name = "Branch B1", Code = "BR_B1", IsActive = true },
            new Branch { Id = TestConstants.BranchB2Id, CompanyId = TestConstants.CompanyBId, Name = "Branch B2", Code = "BR_B2", IsActive = true },
            new Branch { Id = TestConstants.BranchC1Id, CompanyId = TestConstants.CompanyCId, Name = "Branch C1", Code = "BR_C1", IsActive = true }
        };

        foreach (var b in branches)
        {
            if (!db.Branches.Any(x => x.Id == b.Id))
            {
                db.Branches.Add(b);
            }
        }

        db.SaveChanges();

        // Categories & Products
        if (!db.Categories.Any(x => x.CategoryId == TestConstants.CategoryAlphaId))
        {
            db.Categories.Add(Category.Create(TestConstants.TenantAlphaId, "CAT_ALPHA", "Category Alpha", null, TestConstants.CategoryAlphaId));
            db.SaveChanges();
        }

        if (!db.Products.Any(x => x.ProductId == TestConstants.ProductAlphaId))
        {
            db.Products.Add(Product.Create(
                TestConstants.TenantAlphaId,
                "PROD_ALPHA",
                "Product Alpha",
                TestConstants.CategoryAlphaId,
                "PCS",
                null,
                ValuationMethod.FIFO,
                0, 0, 0, 0, false, true, true,
                TestConstants.ProductAlphaId));
        }

        if (!db.Categories.Any(x => x.CategoryId == TestConstants.CategoryBetaId))
        {
            db.Categories.Add(Category.Create(TestConstants.TenantBetaId, "CAT_BETA", "Category Beta", null, TestConstants.CategoryBetaId));
            db.SaveChanges();
        }

        if (!db.Products.Any(x => x.ProductId == TestConstants.ProductBetaId))
        {
            db.Products.Add(Product.Create(
                TestConstants.TenantBetaId,
                "PROD_BETA",
                "Product Beta",
                TestConstants.CategoryBetaId,
                "PCS",
                null,
                ValuationMethod.FIFO,
                0, 0, 0, 0, false, true, true,
                TestConstants.ProductBetaId));
        }

        db.SaveChanges();
    }

    private static void SeedSecurity(SecurityDbContext db)
    {
        // Ensure Roles
        var tenantAdminRole = db.Roles.FirstOrDefault(x => x.Name == "Tenant Admin")
            ?? new Role { Id = Guid.NewGuid(), Name = "Tenant Admin", Description = "Tenant Administrator", IsActive = true };

        var tenantUserRole = db.Roles.FirstOrDefault(x => x.Name == "Tenant User")
            ?? new Role { Id = Guid.NewGuid(), Name = "Tenant User", Description = "General Tenant User", IsActive = true };

        var branchUserRole = db.Roles.FirstOrDefault(x => x.Name == "Branch User")
            ?? new Role { Id = Guid.NewGuid(), Name = "Branch User", Description = "Branch User", IsActive = true };

        var viewerRole = db.Roles.FirstOrDefault(x => x.Name == "Viewer")
            ?? new Role { Id = Guid.NewGuid(), Name = "Viewer", Description = "Read-Only Viewer", IsActive = true };

        if (tenantAdminRole.Id != Guid.Empty && !db.Roles.Any(x => x.Id == tenantAdminRole.Id)) db.Roles.Add(tenantAdminRole);
        if (tenantUserRole.Id != Guid.Empty && !db.Roles.Any(x => x.Id == tenantUserRole.Id)) db.Roles.Add(tenantUserRole);
        if (branchUserRole.Id != Guid.Empty && !db.Roles.Any(x => x.Id == branchUserRole.Id)) db.Roles.Add(branchUserRole);
        if (viewerRole.Id != Guid.Empty && !db.Roles.Any(x => x.Id == viewerRole.Id)) db.Roles.Add(viewerRole);

        db.SaveChanges();

        // Seed Users
        SeedUserWithRole(db, TestConstants.PlatformAdminId, null, "platformadmin", "platform@platform.local");
        SeedUserWithRole(db, TestConstants.TenantAdminAlphaId, TestConstants.TenantAlphaId, "tenantadmin_a", "admin_a@tenant.local", tenantAdminRole);
        SeedUserWithRole(db, TestConstants.TenantAdminBetaId, TestConstants.TenantBetaId, "tenantadmin_b", "admin_b@tenant.local", tenantAdminRole);

        SeedUserWithRole(db, TestConstants.TenantUserAlphaId, TestConstants.TenantAlphaId, "tenantuser_a", "user_a@tenant.local", tenantUserRole);
        
        var compUserA = SeedUserWithRole(db, TestConstants.CompanyUserAId, TestConstants.TenantAlphaId, "compuser_a", "comp_a@tenant.local", branchUserRole);
        EnsureScope(db, compUserA, TestConstants.CompanyAId, null);

        var branchUserA1 = SeedUserWithRole(db, TestConstants.BranchUserA1Id, TestConstants.TenantAlphaId, "branchuser_a1", "branch_a1@tenant.local", branchUserRole);
        EnsureScope(db, branchUserA1, TestConstants.CompanyAId, TestConstants.BranchA1Id);

        var multiBranchUser = SeedUserWithRole(db, TestConstants.MultiBranchUserId, TestConstants.TenantAlphaId, "multibranch_user", "multi@tenant.local", branchUserRole);
        EnsureScope(db, multiBranchUser, TestConstants.CompanyAId, TestConstants.BranchA1Id);
        EnsureScope(db, multiBranchUser, TestConstants.CompanyAId, TestConstants.BranchA2Id);

        var mixedScopeUser = SeedUserWithRole(db, TestConstants.MixedScopeUserId, TestConstants.TenantAlphaId, "mixedscope_user", "mixed@tenant.local", branchUserRole);
        EnsureScope(db, mixedScopeUser, TestConstants.CompanyAId, TestConstants.BranchA1Id);
        EnsureScope(db, mixedScopeUser, TestConstants.CompanyBId, TestConstants.BranchB2Id);

        var readOnlyUser = SeedUserWithRole(db, TestConstants.ReadOnlyUserId, TestConstants.TenantAlphaId, "readonly_user", "readonly@tenant.local", viewerRole);
        EnsureScope(db, readOnlyUser, TestConstants.CompanyAId, TestConstants.BranchA1Id);

        var multiCompUser = SeedUserWithRole(db, TestConstants.MultiCompanyUserABId, TestConstants.TenantAlphaId, "multicomp_user_ab", "multicomp_ab@tenant.local", branchUserRole);
        EnsureScope(db, multiCompUser, TestConstants.CompanyAId, null);
        EnsureScope(db, multiCompUser, TestConstants.CompanyBId, null);

        db.SaveChanges();
    }

    private static UserRole SeedUserWithRole(
        SecurityDbContext db,
        Guid userId,
        Guid? tenantId,
        string username,
        string email,
        Role? role = null)
    {
        var user = db.Users.FirstOrDefault(x => x.Id == userId);
        if (user == null)
        {
            user = new User
            {
                Id = userId,
                TenantId = tenantId,
                Username = username,
                Email = email,
                PasswordHash = "hashed_123",
                IsActive = true
            };
            db.Users.Add(user);
            db.SaveChanges();
        }

        if (role == null) return null!;

        var userRole = db.UserRoles.FirstOrDefault(x => x.UserId == userId && x.RoleId == role.Id);
        if (userRole == null)
        {
            userRole = new UserRole
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                RoleId = role.Id,
                Role = role
            };
            db.UserRoles.Add(userRole);
            db.SaveChanges();
        }

        return userRole;
    }

    private static void EnsureScope(SecurityDbContext db, UserRole userRole, Guid? companyId, Guid? branchId)
    {
        if (userRole == null) return;

        bool exists = db.UserRoleScopes.Any(x =>
            x.UserRoleId == userRole.Id &&
            x.CompanyId == companyId &&
            x.BranchId == branchId);

        if (!exists)
        {
            db.UserRoleScopes.Add(new UserRoleScope
            {
                Id = Guid.NewGuid(),
                UserRoleId = userRole.Id,
                CompanyId = companyId,
                BranchId = branchId,
                IsActive = true
            });
            db.SaveChanges();
        }
    }
}
