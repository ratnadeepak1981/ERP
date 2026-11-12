using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Security.Application.DTOs;
using Security.Bootstrap.Permissions;
using Security.Bootstrap.Roles;
using Security.Core.Models;
using Security.Infrastructure.Persistence;
using Security.Infrastructure.Persistence.Repositories;
using Security.Services;
using Xunit;

namespace ERP.SecurityTests;

/// <summary>
/// F1 verification: global permission catalogue, platform/tenant ownership,
/// central standard role templates and tenant-specific role copies.
/// </summary>
public class F1PermissionCatalogueAndStandardRolesTests : IClassFixture<TestSecurityFixture>
{
    private readonly TestSecurityFixture _fixture;
    private readonly JsonSerializerOptions _jsonOpts = new() { PropertyNameCaseInsensitive = true };

    public F1PermissionCatalogueAndStandardRolesTests(TestSecurityFixture fixture)
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

    private SecurityBootstrapService CreateBootstrap(SecurityDbContext db)
    {
        return new SecurityBootstrapService(
            new SecurityBootstrapRepository(db),
            new PasswordService());
    }

    private async Task<(HttpClient Client, LoginResponse Login)> LoginAsync(string email)
    {
        var anonymous = _fixture.CreateClient();
        var response = await anonymous.PostAsJsonAsync("/api/Security/login", new LoginRequest
        {
            Email = email,
            Password = "Password@123"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var login = await response.ReadApiResponseDataAsync<LoginResponse>(_jsonOpts);
        Assert.NotNull(login);

        var client = _fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login!.Token);

        return (client, login);
    }

    // =====================================================================
    // 1. Catalogue definitions and ownership
    // =====================================================================

    [Fact]
    public void Catalogue_CodesAreUnique_AndMetadataComplete()
    {
        var all = PermissionCatalogue.All;

        Assert.NotEmpty(all);
        Assert.Equal(
            all.Count,
            all.Select(x => x.Code).Distinct(StringComparer.OrdinalIgnoreCase).Count());

        foreach (var permission in all)
        {
            Assert.False(string.IsNullOrWhiteSpace(permission.Code));
            Assert.False(string.IsNullOrWhiteSpace(permission.Name));
            Assert.False(string.IsNullOrWhiteSpace(permission.Module));
            Assert.False(string.IsNullOrWhiteSpace(permission.Description));
            Assert.False(string.IsNullOrWhiteSpace(permission.Operation));
            Assert.Contains(permission.Category,
                new[] { PermissionCategory.Platform, PermissionCategory.Tenant });
            Assert.Equal(PermissionOperation.FromCode(permission.Code), permission.Operation);
        }
    }

    [Fact]
    public void Catalogue_PlatformOwnership_IsRestrictedToPlatformAdministration()
    {
        var platform = PermissionCatalogue.Platform;
        var tenant = PermissionCatalogue.Tenant;

        Assert.NotEmpty(platform);
        Assert.NotEmpty(tenant);
        Assert.Empty(platform.Select(x => x.Code).Intersect(tenant.Select(x => x.Code)));

        var expectedPlatformCodes = PermissionCatalogue.All
            .Where(x =>
                x.Code.StartsWith("TENANT.", StringComparison.OrdinalIgnoreCase) ||
                x.Code.StartsWith("SUBSCRIPTION.", StringComparison.OrdinalIgnoreCase) ||
                x.Code.StartsWith("AI.", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(x.Code, "AUDIT.VIEW", StringComparison.OrdinalIgnoreCase))
            .Select(x => x.Code)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Equal(
            expectedPlatformCodes.OrderBy(x => x),
            platform.Select(x => x.Code).OrderBy(x => x));

        // Platform permissions must never be present in the tenant catalogue.
        Assert.DoesNotContain(platform, p => PermissionCategory.IsTenant(p.Category));
    }

    [Fact]
    public void Catalogue_ContainsInventoryStockAndPutawayPermissions()
    {
        var codes = PermissionCatalogue.All
            .Select(x => x.Code)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var required = new[]
        {
            "INVENTORY.VIEW",
            "STOCK_RECEIPT.POST",
            "STOCK_ISSUE.POST",
            "PUTAWAY.POST",
            "STOCK_COUNT.VIEW",
            "STOCK_COUNT.CREATE",
            "STOCK_COUNT.EDIT",
            "STOCK_COUNT.SUBMIT",
            "STOCK_COUNT.APPROVE",
            "STOCK_COUNT.POST",
            "STOCK_COUNT.CANCEL"
        };

        foreach (var code in required)
        {
            Assert.True(codes.Contains(code), $"Catalogue is missing required permission '{code}'.");
        }

        // Inventory permissions are tenant-owned, never platform-owned.
        foreach (var code in required)
        {
            var definition = PermissionCatalogue.Find(code);
            Assert.NotNull(definition);
            Assert.Equal(PermissionCategory.Tenant, definition!.Category);
        }
    }

    // =====================================================================
    // 2. Standard role templates
    // =====================================================================

    [Fact]
    public void StandardRoleTemplates_ReferenceOnlyKnownTenantPermissions()
    {
        Assert.Equal(19, StandardRoleTemplates.All.Count);
        Assert.Equal(
            StandardRoleTemplates.All.Count,
            StandardRoleTemplates.All.Select(x => x.Key).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(
            StandardRoleTemplates.All.Count,
            StandardRoleTemplates.All.Select(x => x.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count());

        Assert.Empty(StandardRoleTemplates.GetUnknownPermissionCodes());

        foreach (var template in StandardRoleTemplates.All)
        {
            Assert.True(template.Version >= 1);
            Assert.NotEmpty(template.PermissionCodes);

            foreach (var code in template.PermissionCodes)
            {
                var permission = PermissionCatalogue.Find(code);
                Assert.NotNull(permission);
                Assert.Equal(PermissionCategory.Tenant, permission!.Category);
            }
        }
    }

    [Fact]
    public void StandardRoleTemplates_TenantAdminExcludesPlatformAndPriceOverride()
    {
        var template = StandardRoleTemplates.Find(StandardRoleTemplates.TenantAdminKey);
        Assert.NotNull(template);

        var codes = template!.PermissionCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.DoesNotContain("PRICE_CARD.OVERRIDE", codes);
        Assert.DoesNotContain(codes, code => code.StartsWith("TENANT."));
        Assert.DoesNotContain(codes, code => code.StartsWith("SUBSCRIPTION."));
        Assert.DoesNotContain(codes, code => code.StartsWith("AI."));
        Assert.DoesNotContain("AUDIT.VIEW", codes);

        // Tenant Admin must include the previously missing inventory permissions.
        Assert.Contains("INVENTORY.VIEW", codes);
        Assert.Contains("STOCK_RECEIPT.POST", codes);
        Assert.Contains("STOCK_ISSUE.POST", codes);
        Assert.Contains("PUTAWAY.POST", codes);
        Assert.Contains("STOCK_COUNT.POST", codes);
    }

    [Fact]
    public void StandardRoleTemplates_SeparateViewCreateApprovePostAndReverse()
    {
        var auditor = StandardRoleTemplates.Find(StandardRoleTemplates.TenantAuditorKey)!;
        Assert.All(auditor.PermissionCodes, code =>
            Assert.Equal(PermissionOperation.View, PermissionOperation.FromCode(code)));

        var inventoryOfficer = StandardRoleTemplates.Find("INVENTORY_OFFICER")!;
        Assert.Contains("INVENTORY.VIEW", inventoryOfficer.PermissionCodes);
        Assert.Contains("STOCK_COUNT.CREATE", inventoryOfficer.PermissionCodes);
        Assert.Contains("STOCK_COUNT.SUBMIT", inventoryOfficer.PermissionCodes);
        Assert.DoesNotContain("STOCK_COUNT.APPROVE", inventoryOfficer.PermissionCodes);
        Assert.DoesNotContain("STOCK_COUNT.POST", inventoryOfficer.PermissionCodes);

        var inventoryManager = StandardRoleTemplates.Find("INVENTORY_MANAGER")!;
        Assert.Contains("STOCK_COUNT.APPROVE", inventoryManager.PermissionCodes);
        Assert.Contains("STOCK_COUNT.POST", inventoryManager.PermissionCodes);

        var procurementOfficer = StandardRoleTemplates.Find("PROCUREMENT_OFFICER")!;
        Assert.DoesNotContain("PURCHASE_ORDER.APPROVE", procurementOfficer.PermissionCodes);
        Assert.DoesNotContain("PURCHASE_REQUISITION.APPROVE", procurementOfficer.PermissionCodes);

        var procurementManager = StandardRoleTemplates.Find("PROCUREMENT_MANAGER")!;
        Assert.Contains("PURCHASE_ORDER.APPROVE", procurementManager.PermissionCodes);
        Assert.Contains("PURCHASE_REQUISITION.APPROVE", procurementManager.PermissionCodes);

        var salesOfficer = StandardRoleTemplates.Find("SALES_OFFICER")!;
        Assert.DoesNotContain("SALES_ORDER.CONFIRM", salesOfficer.PermissionCodes);
        Assert.DoesNotContain("SALES_INVOICE.CONFIRM", salesOfficer.PermissionCodes);

        var salesManager = StandardRoleTemplates.Find("SALES_MANAGER")!;
        Assert.Contains("SALES_ORDER.CONFIRM", salesManager.PermissionCodes);
        Assert.Contains("SALES_INVOICE.CONFIRM", salesManager.PermissionCodes);
    }

    [Fact]
    public void StandardRoleTemplates_AllRoles_ReceiveNonSensitiveMasterDataReadBaseline()
    {
        var baseline = StandardRoleTemplates.MasterDataViewBaseline;

        Assert.NotEmpty(baseline);

        // The baseline is read-only by construction.
        Assert.All(baseline, code =>
            Assert.Equal(PermissionOperation.View, PermissionOperation.FromCode(code)));

        // Sensitive, administrative and commercial data stay explicitly controlled.
        foreach (var restricted in new[]
                 {
                     "USER.VIEW", "ROLE.VIEW", "CONTACT.VIEW",
                     "SUPPLIER_PRICE.VIEW", "PRICE_CARD.VIEW", "PRICE_CARD.OVERRIDE"
                 })
        {
            Assert.DoesNotContain(restricted, baseline);
        }

        foreach (var template in StandardRoleTemplates.All)
        {
            var codes = template.PermissionCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var code in baseline)
            {
                Assert.True(
                    codes.Contains(code),
                    $"Role '{template.Key}' is missing master data read baseline permission '{code}'.");
            }
        }

        // Lookup reads required by the procurement and sales transaction forms.
        foreach (var roleKey in new[] { "PROCUREMENT_OFFICER", "PROCUREMENT_MANAGER" })
        {
            var role = StandardRoleTemplates.Find(roleKey)!;
            foreach (var code in new[] { "CURRENCY.VIEW", "UOM.VIEW", "TAX_CODE.VIEW", "PRICE_CARD.VIEW" })
            {
                Assert.True(
                    role.PermissionCodes.Contains(code, StringComparer.OrdinalIgnoreCase),
                    $"Role '{roleKey}' is missing lookup permission '{code}'.");
            }
        }

        foreach (var roleKey in new[] { "SALES_OFFICER", "SALES_MANAGER" })
        {
            var role = StandardRoleTemplates.Find(roleKey)!;
            Assert.True(
                role.PermissionCodes.Contains("TAX_CODE.VIEW", StringComparer.OrdinalIgnoreCase),
                $"Role '{roleKey}' is missing lookup permission 'TAX_CODE.VIEW'.");
        }

        // The read baseline must never introduce mutation or approval capability.
        Assert.DoesNotContain(baseline, code =>
            !string.Equals(
                PermissionOperation.FromCode(code),
                PermissionOperation.View,
                StringComparison.OrdinalIgnoreCase));
    }

    // =====================================================================
    // 3. Tenant provisioning of role copies
    // =====================================================================

    [Fact]
    public async Task TenantRoleProvisioning_CreatesTenantScopedCopies_ForEachTenant()
    {
        using var db = CreateSecurityDb();

        foreach (var tenantId in new[] { TestConstants.TenantAlphaId, TestConstants.TenantBetaId })
        {
            // Upgrades never delete a tenant copy whose template was retired, so
            // only assert the copies for the current template catalogue.
            var currentTemplateKeys = StandardRoleTemplates.All
                .Select(x => x.Key)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var roles = (await db.Roles
                    .Where(x => x.TenantId == tenantId && x.TemplateKey != null)
                    .ToListAsync())
                .Where(x => currentTemplateKeys.Contains(x.TemplateKey!))
                .ToList();

            Assert.Equal(StandardRoleTemplates.All.Count, roles.Count);

            foreach (var template in StandardRoleTemplates.All)
            {
                var role = roles.Single(x =>
                    string.Equals(x.TemplateKey, template.Key, StringComparison.OrdinalIgnoreCase));

                Assert.Equal(template.Name, role.Name);
                Assert.Equal(tenantId, role.TenantId);
                Assert.False(role.IsSystemRole);
                Assert.True(role.IsActive);
                Assert.Equal(template.Version, role.TemplateVersion);
            }
        }

        // Tenant copies must have distinct role identities.
        var alphaAdmin = await db.Roles.SingleAsync(x =>
            x.TenantId == TestConstants.TenantAlphaId &&
            x.TemplateKey == StandardRoleTemplates.TenantAdminKey);
        var betaAdmin = await db.Roles.SingleAsync(x =>
            x.TenantId == TestConstants.TenantBetaId &&
            x.TemplateKey == StandardRoleTemplates.TenantAdminKey);

        Assert.NotEqual(alphaAdmin.Id, betaAdmin.Id);

        // Tenant Admin copy must not receive platform permissions.
        var alphaCodes = await db.RolePermissions
            .Where(x => x.RoleId == alphaAdmin.Id)
            .Select(x => x.Permission.Code)
            .ToListAsync();

        Assert.DoesNotContain("TENANT.VIEW", alphaCodes);
        Assert.DoesNotContain("SUBSCRIPTION.VIEW", alphaCodes);
        Assert.DoesNotContain("AUDIT.VIEW", alphaCodes);
        Assert.DoesNotContain("PRICE_CARD.OVERRIDE", alphaCodes);
    }

    [Fact]
    public async Task TenantRoleProvisioning_IsIdempotent_AndRetrySafe()
    {
        using var db = CreateSecurityDb();
        var bootstrap = CreateBootstrap(db);

        var rolesBefore = await db.Roles
            .Where(x => x.TenantId == TestConstants.TenantAlphaId)
            .CountAsync();

        var first = await bootstrap.EnsureTenantStandardRolesAsync(TestConstants.TenantAlphaId);
        var second = await bootstrap.EnsureTenantStandardRolesAsync(TestConstants.TenantAlphaId);

        Assert.Empty(first.CreatedTemplateKeys);
        Assert.Empty(first.UpdatedTemplateKeys);
        Assert.Empty(second.CreatedTemplateKeys);
        Assert.Empty(second.UpdatedTemplateKeys);

        using var verifyDb = CreateSecurityDb();
        var rolesAfter = await verifyDb.Roles
            .Where(x => x.TenantId == TestConstants.TenantAlphaId)
            .CountAsync();

        Assert.Equal(rolesBefore, rolesAfter);

        foreach (var template in StandardRoleTemplates.All)
        {
            var copies = await verifyDb.Roles.CountAsync(x =>
                x.TenantId == TestConstants.TenantAlphaId &&
                x.TemplateKey == template.Key);
            Assert.Equal(1, copies);
        }
    }

    [Fact]
    public async Task TenantRoleProvisioning_UpgradeAddsPermissions_WithoutRemovingCustomisations()
    {
        var customPermissionCode = "PRODUCT.CREATE";
        Guid roleId;

        using (var setupDb = CreateSecurityDb())
        {
            var role = await setupDb.Roles.SingleAsync(x =>
                x.TenantId == TestConstants.TenantAlphaId &&
                x.TemplateKey == "INVENTORY_VIEWER");
            roleId = role.Id;

            var permission = await setupDb.Permissions
                .SingleAsync(x => x.Code == customPermissionCode);

            var exists = await setupDb.RolePermissions.AnyAsync(x =>
                x.RoleId == roleId && x.PermissionId == permission.Id);

            if (!exists)
            {
                setupDb.RolePermissions.Add(new RolePermission
                {
                    Id = Guid.NewGuid(),
                    RoleId = roleId,
                    PermissionId = permission.Id
                });
            }

            // Simulate a tenant copy that is behind the current template version.
            role.TemplateVersion = 0;
            await setupDb.SaveChangesAsync();
        }

        try
        {
            using (var db = CreateSecurityDb())
            {
                var bootstrap = CreateBootstrap(db);
                var result = await bootstrap.EnsureTenantStandardRolesAsync(TestConstants.TenantAlphaId);

                Assert.Contains("INVENTORY_VIEWER", result.UpdatedTemplateKeys);
            }

            using var verifyDb = CreateSecurityDb();
            var upgraded = await verifyDb.Roles.SingleAsync(x => x.Id == roleId);

            Assert.Equal(StandardRoleTemplates.CurrentVersion, upgraded.TemplateVersion);

            // The customised permission must survive an additive template upgrade.
            var codes = await verifyDb.RolePermissions
                .Where(x => x.RoleId == roleId)
                .Select(x => x.Permission.Code)
                .ToListAsync();

            Assert.Contains(customPermissionCode, codes);
        }
        finally
        {
            // Restore the seeded state so other tests are unaffected.
            using var cleanupDb = CreateSecurityDb();
            var role = await cleanupDb.Roles.SingleAsync(x => x.Id == roleId);
            role.TemplateVersion = StandardRoleTemplates.CurrentVersion;

            var permission = await cleanupDb.Permissions
                .SingleAsync(x => x.Code == customPermissionCode);

            var extra = await cleanupDb.RolePermissions.FirstOrDefaultAsync(x =>
                x.RoleId == roleId && x.PermissionId == permission.Id);
            if (extra != null)
            {
                cleanupDb.RolePermissions.Remove(extra);
            }

            await cleanupDb.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task TenantRoleProvisioning_NameCollision_IsSkipped_AndNotOverwritten()
    {
        var tenantId = Guid.NewGuid();
        Guid customRoleId;

        try
        {
            using (var setupDb = CreateSecurityDb())
            {
                var customRole = new Role
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    Name = "Inventory Manager",
                    Description = "Tenant-authored role that collides with a template name.",
                    IsSystemRole = false,
                    IsActive = true
                };

                setupDb.Roles.Add(customRole);
                await setupDb.SaveChangesAsync();
                customRoleId = customRole.Id;
            }

            using (var db = CreateSecurityDb())
            {
                var bootstrap = CreateBootstrap(db);
                var result = await bootstrap.EnsureTenantStandardRolesAsync(tenantId);

                Assert.Contains("INVENTORY_MANAGER", result.SkippedTemplateKeys);
                Assert.NotEmpty(result.Conflicts);
            }

            using var verifyDb = CreateSecurityDb();
            var role = await verifyDb.Roles.SingleAsync(x => x.Id == customRoleId);

            // The tenant-authored role is untouched and was not converted into a template copy.
            Assert.Null(role.TemplateKey);
            Assert.Equal("Inventory Manager", role.Name);
            Assert.Equal("Tenant-authored role that collides with a template name.", role.Description);

            var templateCopies = await verifyDb.Roles.CountAsync(x =>
                x.TenantId == tenantId &&
                x.TemplateKey == "INVENTORY_MANAGER");
            Assert.Equal(0, templateCopies);
        }
        finally
        {
            using var cleanupDb = CreateSecurityDb();
            var roleIds = await cleanupDb.Roles
                .Where(x => x.TenantId == tenantId)
                .Select(x => x.Id)
                .ToListAsync();

            if (roleIds.Count > 0)
            {
                await cleanupDb.RolePermissions
                    .Where(x => roleIds.Contains(x.RoleId))
                    .ExecuteDeleteAsync();

                await cleanupDb.UserRoles
                    .Where(x => roleIds.Contains(x.RoleId))
                    .ExecuteDeleteAsync();

                await cleanupDb.Roles
                    .Where(x => roleIds.Contains(x.Id))
                    .ExecuteDeleteAsync();
            }
        }
    }

    [Fact]
    public async Task LegacyTenantAdminAssignment_IsMigratedToTenantCopy()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var username = $"legacy_{Guid.NewGuid():N}"[..14];

        try
        {
            Guid legacyRoleId;

            using (var setupDb = CreateSecurityDb())
            {
                var legacyRole = await setupDb.Roles.FirstOrDefaultAsync(x =>
                    x.TenantId == null &&
                    x.Name == "Tenant Admin");

                if (legacyRole == null)
                {
                    var bootstrap = CreateBootstrap(setupDb);
                    await bootstrap.EnsureTenantAdminRoleAsync();
                    legacyRole = await setupDb.Roles.FirstAsync(x =>
                        x.TenantId == null &&
                        x.Name == "Tenant Admin");
                }

                legacyRoleId = legacyRole!.Id;

                setupDb.Users.Add(new User
                {
                    Id = userId,
                    TenantId = tenantId,
                    Username = username,
                    Email = $"{username}@tenant.local",
                    PasswordHash = new PasswordService().HashPassword("Password@123"),
                    IsActive = true
                });

                setupDb.UserRoles.Add(new UserRole
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    RoleId = legacyRoleId
                });

                await setupDb.SaveChangesAsync();
            }

            using (var db = CreateSecurityDb())
            {
                var bootstrap = CreateBootstrap(db);
                await bootstrap.UpgradeExistingTenantsAsync();
            }

            using var verifyDb = CreateSecurityDb();
            var roles = await verifyDb.UserRoles
                .Where(x => x.UserId == userId)
                .Select(x => x.Role)
                .ToListAsync();

            Assert.DoesNotContain(roles, r => r.Id == legacyRoleId);
            Assert.Contains(roles, r =>
                r.TenantId == tenantId &&
                r.TemplateKey == StandardRoleTemplates.TenantAdminKey);
        }
        finally
        {
            using var cleanupDb = CreateSecurityDb();
            var userRoles = await cleanupDb.UserRoles
                .Where(x => x.UserId == userId)
                .ToListAsync();
            cleanupDb.UserRoles.RemoveRange(userRoles);
            await cleanupDb.SaveChangesAsync();

            var user = await cleanupDb.Users.FirstOrDefaultAsync(x => x.Id == userId);
            if (user != null)
            {
                cleanupDb.Users.Remove(user);
                await cleanupDb.SaveChangesAsync();
            }

            var roleIds = await cleanupDb.Roles
                .Where(x => x.TenantId == tenantId)
                .Select(x => x.Id)
                .ToListAsync();

            if (roleIds.Count > 0)
            {
                await cleanupDb.RolePermissions
                    .Where(x => roleIds.Contains(x.RoleId))
                    .ExecuteDeleteAsync();

                await cleanupDb.Roles
                    .Where(x => roleIds.Contains(x.Id))
                    .ExecuteDeleteAsync();
            }
        }
    }

    // =====================================================================
    // 4. Real login -> effective permissions -> endpoint authorization
    // =====================================================================

    [Fact]
    public async Task TenantAdminLogin_ReceivesTenantPermissions_AndCanAccessInventory()
    {
        var (client, login) = await LoginAsync("admin_a@tenant.local");

        Assert.Contains("Tenant Admin", login.Roles);
        Assert.Contains("INVENTORY.VIEW", login.Permissions);
        Assert.Contains("STOCK_RECEIPT.POST", login.Permissions);
        Assert.Contains("STOCK_ISSUE.POST", login.Permissions);
        Assert.Contains("PUTAWAY.POST", login.Permissions);
        Assert.Contains("STOCK_COUNT.POST", login.Permissions);

        Assert.DoesNotContain("TENANT.VIEW", login.Permissions);
        Assert.DoesNotContain("SUBSCRIPTION.VIEW", login.Permissions);
        Assert.DoesNotContain("AUDIT.VIEW", login.Permissions);
        Assert.DoesNotContain("PRICE_CARD.OVERRIDE", login.Permissions);

        var inventoryResponse = await client.GetAsync("/api/inventory/summary");
        Assert.NotEqual(HttpStatusCode.Forbidden, inventoryResponse.StatusCode);
        Assert.NotEqual(HttpStatusCode.Unauthorized, inventoryResponse.StatusCode);
    }

    [Fact]
    public async Task UserWithoutInventoryPermission_IsForbidden_FromInventoryEndpoints()
    {
        // readonly_user holds the tenant "Viewer" role, which has no permissions.
        var (client, login) = await LoginAsync("readonly@tenant.local");

        Assert.DoesNotContain("INVENTORY.VIEW", login.Permissions);

        var inventoryResponse = await client.GetAsync("/api/inventory/summary");
        Assert.Equal(HttpStatusCode.Forbidden, inventoryResponse.StatusCode);
    }

    [Fact]
    public async Task PermissionCatalog_ForTenantAdmin_ExcludesPlatformOwnership()
    {
        var (client, _) = await LoginAsync("admin_a@tenant.local");

        var response = await client.GetAsync("/api/permission");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var permissions = await response.ReadApiResponseDataAsync<List<JsonElement>>(_jsonOpts);
        Assert.NotNull(permissions);
        Assert.NotEmpty(permissions!);

        foreach (var permission in permissions!)
        {
            var code = permission.GetProperty("code").GetString() ?? string.Empty;
            var category = permission.GetProperty("category").GetString();
            var operation = permission.GetProperty("operation").GetString();

            Assert.NotEqual(PermissionCategory.Platform, category);
            Assert.DoesNotContain("TENANT.", code);
            Assert.DoesNotContain("SUBSCRIPTION.", code);
            Assert.DoesNotContain("AI.", code);
            Assert.False(string.IsNullOrWhiteSpace(operation));
        }
    }

    [Fact]
    public async Task TenantAdmin_CannotAssignPlatformPermission_ToTenantRole()
    {
        Guid roleId;
        Guid platformPermissionId;

        using (var db = CreateSecurityDb())
        {
            roleId = await db.Roles
                .Where(x =>
                    x.TenantId == TestConstants.TenantAlphaId &&
                    x.TemplateKey == "INVENTORY_VIEWER")
                .Select(x => x.Id)
                .SingleAsync();

            platformPermissionId = await db.Permissions
                .Where(x => x.Code == "TENANT.VIEW")
                .Select(x => x.Id)
                .SingleAsync();
        }

        var (client, _) = await LoginAsync("admin_a@tenant.local");

        var response = await client.PostAsJsonAsync("/api/RolePermission", new
        {
            roleId,
            permissionId = platformPermissionId
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        // The assignment must not have been created.
        using var verifyDb = CreateSecurityDb();
        Assert.False(await verifyDb.RolePermissions.AnyAsync(x =>
            x.RoleId == roleId && x.PermissionId == platformPermissionId));
    }

    [Fact]
    public async Task TenantRole_CanReceiveTenantPermission_ThroughApi()
    {
        Guid roleId;
        Guid permissionId;

        using (var db = CreateSecurityDb())
        {
            roleId = await db.Roles
                .Where(x =>
                    x.TenantId == TestConstants.TenantAlphaId &&
                    x.TemplateKey == "MASTER_DATA_VIEWER")
                .Select(x => x.Id)
                .SingleAsync();

            // Pick a tenant permission the role does not already own.
            permissionId = await db.Permissions
                .Where(x => x.Code == "CUSTOMER.CREATE")
                .Select(x => x.Id)
                .SingleAsync();
        }

        var (client, _) = await LoginAsync("admin_a@tenant.local");

        try
        {
            var response = await client.PostAsJsonAsync("/api/RolePermission", new
            {
                roleId,
                permissionId
            });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            using var verifyDb = CreateSecurityDb();
            Assert.True(await verifyDb.RolePermissions.AnyAsync(x =>
                x.RoleId == roleId && x.PermissionId == permissionId));
        }
        finally
        {
            using var cleanupDb = CreateSecurityDb();
            var assignment = await cleanupDb.RolePermissions.FirstOrDefaultAsync(x =>
                x.RoleId == roleId && x.PermissionId == permissionId);
            if (assignment != null)
            {
                cleanupDb.RolePermissions.Remove(assignment);
                await cleanupDb.SaveChangesAsync();
            }
        }
    }

    [Fact]
    public async Task RevokedPermission_IsNotIncludedInNewLoginToken()
    {
        // Use a dedicated user and role so the assertion is independent of any
        // other role assignments that may exist for shared seeded users.
        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var username = $"f1revoke{Guid.NewGuid():N}"[..16];
        Guid permissionId;

        using (var db = CreateSecurityDb())
        {
            permissionId = await db.Permissions
                .Where(x => x.Code == "PRODUCT.VIEW")
                .Select(x => x.Id)
                .SingleAsync();

            db.Roles.Add(new Role
            {
                Id = roleId,
                TenantId = TestConstants.TenantAlphaId,
                Name = $"F1 Revoke {Guid.NewGuid():N}"[..24],
                Description = "F1 revoked permission test role.",
                IsSystemRole = false,
                IsActive = true
            });

            db.Users.Add(new User
            {
                Id = userId,
                TenantId = TestConstants.TenantAlphaId,
                Username = username,
                Email = $"{username}@tenant.local",
                PasswordHash = new PasswordService().HashPassword("Password@123"),
                IsActive = true
            });

            db.UserRoles.Add(new UserRole
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                RoleId = roleId
            });

            db.RolePermissions.Add(new RolePermission
            {
                Id = Guid.NewGuid(),
                RoleId = roleId,
                PermissionId = permissionId
            });

            await db.SaveChangesAsync();
        }

        try
        {
            var (_, before) = await LoginAsync($"{username}@tenant.local");
            Assert.Contains("PRODUCT.VIEW", before.Permissions);

            using (var db = CreateSecurityDb())
            {
                var assignment = await db.RolePermissions.FirstAsync(x =>
                    x.RoleId == roleId && x.PermissionId == permissionId);
                db.RolePermissions.Remove(assignment);
                await db.SaveChangesAsync();
            }

            var (_, after) = await LoginAsync($"{username}@tenant.local");
            Assert.DoesNotContain("PRODUCT.VIEW", after.Permissions);
        }
        finally
        {
            using var cleanupDb = CreateSecurityDb();

            var assignments = await cleanupDb.UserRoles
                .Where(x => x.UserId == userId)
                .ToListAsync();
            cleanupDb.UserRoles.RemoveRange(assignments);

            var rolePermissions = await cleanupDb.RolePermissions
                .Where(x => x.RoleId == roleId)
                .ToListAsync();
            cleanupDb.RolePermissions.RemoveRange(rolePermissions);

            var user = await cleanupDb.Users.FirstOrDefaultAsync(x => x.Id == userId);
            if (user != null)
            {
                cleanupDb.Users.Remove(user);
            }

            var role = await cleanupDb.Roles.FirstOrDefaultAsync(x => x.Id == roleId);
            if (role != null)
            {
                cleanupDb.Roles.Remove(role);
            }

            await cleanupDb.SaveChangesAsync();
        }
    }

    // =====================================================================
    // 5. Per-role effective authorization and role composition
    // =====================================================================

    [Fact]
    public async Task EveryStandardTenantRole_HasExactlyItsTemplatePermissions()
    {
        using var db = CreateSecurityDb();

        foreach (var tenantId in new[] { TestConstants.TenantAlphaId, TestConstants.TenantBetaId })
        {
            foreach (var template in StandardRoleTemplates.All)
            {
                var role = await db.Roles.SingleAsync(x =>
                    x.TenantId == tenantId && x.TemplateKey == template.Key);

                var actual = (await db.RolePermissions
                        .Where(x => x.RoleId == role.Id)
                        .Select(x => x.Permission.Code)
                        .ToListAsync())
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                var expected = template.PermissionCodes
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                Assert.True(
                    expected.SetEquals(actual),
                    $"Tenant {tenantId} role '{template.Key}' permissions differ from the template. " +
                    $"Missing: [{string.Join(", ", expected.Except(actual))}]; " +
                    $"Extra: [{string.Join(", ", actual.Except(expected))}].");
            }
        }
    }

    [Fact]
    public async Task MultiRoleUser_ReceivesUnionOfAssignedRolePermissions()
    {
        var userId = Guid.NewGuid();
        var username = $"f1multi{Guid.NewGuid():N}"[..16];

        Guid inventoryViewerRoleId;
        Guid salesViewerRoleId;

        using (var db = CreateSecurityDb())
        {
            inventoryViewerRoleId = await db.Roles
                .Where(x => x.TenantId == TestConstants.TenantAlphaId && x.TemplateKey == "INVENTORY_VIEWER")
                .Select(x => x.Id)
                .SingleAsync();

            salesViewerRoleId = await db.Roles
                .Where(x => x.TenantId == TestConstants.TenantAlphaId && x.TemplateKey == "SALES_VIEWER")
                .Select(x => x.Id)
                .SingleAsync();

            db.Users.Add(new User
            {
                Id = userId,
                TenantId = TestConstants.TenantAlphaId,
                Username = username,
                Email = $"{username}@tenant.local",
                PasswordHash = new PasswordService().HashPassword("Password@123"),
                IsActive = true
            });

            db.UserRoles.Add(new UserRole
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                RoleId = inventoryViewerRoleId
            });

            db.UserRoles.Add(new UserRole
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                RoleId = salesViewerRoleId
            });

            await db.SaveChangesAsync();
        }

        try
        {
            var (client, login) = await LoginAsync($"{username}@tenant.local");

            var expected = StandardRoleTemplates.Find("INVENTORY_VIEWER")!.PermissionCodes
                .Concat(StandardRoleTemplates.Find("SALES_VIEWER")!.PermissionCodes)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var actual = login.Permissions.ToHashSet(StringComparer.OrdinalIgnoreCase);

            Assert.True(
                expected.SetEquals(actual),
                $"Multi-role effective permissions differ. " +
                $"Missing: [{string.Join(", ", expected.Except(actual))}]; " +
                $"Extra: [{string.Join(", ", actual.Except(expected))}].");

            // A permission held by either role allows access.
            var allowed = await client.GetAsync("/api/inventory/summary");
            Assert.NotEqual(HttpStatusCode.Forbidden, allowed.StatusCode);
            Assert.NotEqual(HttpStatusCode.Unauthorized, allowed.StatusCode);

            // A permission held by neither role is still denied. USER.VIEW is
            // deliberately excluded from the master data read baseline.
            var denied = await client.GetAsync("/api/User");
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        }
        finally
        {
            using var cleanupDb = CreateSecurityDb();
            var assignments = await cleanupDb.UserRoles
                .Where(x => x.UserId == userId)
                .ToListAsync();
            cleanupDb.UserRoles.RemoveRange(assignments);

            var user = await cleanupDb.Users.FirstOrDefaultAsync(x => x.Id == userId);
            if (user != null)
            {
                cleanupDb.Users.Remove(user);
            }

            await cleanupDb.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task CustomRole_GrantsOnlyExplicitlyAssignedPermissions()
    {
        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var username = $"f1custom{Guid.NewGuid():N}"[..16];

        using (var db = CreateSecurityDb())
        {
            var customerViewId = await db.Permissions
                .Where(x => x.Code == "CUSTOMER.VIEW")
                .Select(x => x.Id)
                .SingleAsync();

            db.Roles.Add(new Role
            {
                Id = roleId,
                TenantId = TestConstants.TenantAlphaId,
                Name = $"F1 Custom {Guid.NewGuid():N}"[..20],
                Description = "F1 custom role authorization test.",
                IsSystemRole = false,
                IsActive = true
            });

            db.Users.Add(new User
            {
                Id = userId,
                TenantId = TestConstants.TenantAlphaId,
                Username = username,
                Email = $"{username}@tenant.local",
                PasswordHash = new PasswordService().HashPassword("Password@123"),
                IsActive = true
            });

            db.UserRoles.Add(new UserRole
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                RoleId = roleId
            });

            db.RolePermissions.Add(new RolePermission
            {
                Id = Guid.NewGuid(),
                RoleId = roleId,
                PermissionId = customerViewId
            });

            await db.SaveChangesAsync();
        }

        try
        {
            var (client, login) = await LoginAsync($"{username}@tenant.local");

            Assert.True(
                new[] { "CUSTOMER.VIEW" }.ToHashSet(StringComparer.OrdinalIgnoreCase)
                    .SetEquals(login.Permissions),
                $"Custom role must grant only its explicit permission. Actual: " +
                $"[{string.Join(", ", login.Permissions)}].");

            var denied = await client.GetAsync("/api/inventory/summary");
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        }
        finally
        {
            using var cleanupDb = CreateSecurityDb();
            var assignments = await cleanupDb.UserRoles
                .Where(x => x.UserId == userId)
                .ToListAsync();
            cleanupDb.UserRoles.RemoveRange(assignments);

            var rolePermissions = await cleanupDb.RolePermissions
                .Where(x => x.RoleId == roleId)
                .ToListAsync();
            cleanupDb.RolePermissions.RemoveRange(rolePermissions);

            var user = await cleanupDb.Users.FirstOrDefaultAsync(x => x.Id == userId);
            if (user != null)
            {
                cleanupDb.Users.Remove(user);
            }

            var role = await cleanupDb.Roles.FirstOrDefaultAsync(x => x.Id == roleId);
            if (role != null)
            {
                cleanupDb.Roles.Remove(role);
            }

            await cleanupDb.SaveChangesAsync();
        }
    }
}
