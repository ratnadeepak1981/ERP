using ERP.Domain.Bootstrap.Permissions;
using Security.Boostrap.Permissions.Platform;
using Security.Bootstrap.Permissions;
using Security.Bootstrap.Permissions.Domain;
using Security.Bootstrap.Roles;
using Security.Core.Models;
using Security.Interfaces;

namespace Security.Services;

public class SecurityBootstrapService
{
    private readonly ISecurityBootstrapRepository _repository;
    private readonly PasswordService _passwordService;

    public SecurityBootstrapService(
        ISecurityBootstrapRepository repository,
        PasswordService passwordService)
    {
        _repository = repository;
        _passwordService = passwordService;
    }

    public async Task EnsurePlatformRbacAsync()
    {
        await PermissionCatalogueSeeder.SeedAsync(_repository);
        await PlatformPermissionSeed.SeedAsync(_repository);
        await PlatformRoleSeed.SeedAsync(_repository);
        await PlatformRolePermissionSeed.SeedAsync(_repository);

        await EnsurePlatformAdminAsync();

        await EnsurePlatformUserAsync(
            "platformstaff",
            "platformstaff@platform.local",
            "123",
            "Platform Staff");

        await EnsurePlatformUserAsync(
            "securitymanager",
            "securitymanager@platform.local",
            "123",
            "Security Manager");

        await EnsurePlatformUserAsync(
            "audituser",
            "audituser@platform.local",
            "123",
            "Audit");
    }

    public async Task EnsureDomainPermissionsAsync()
    {
        // The central catalogue is the authoritative permission bootstrap. It
        // covers platform and tenant permissions and backfills metadata for
        // existing installations.
        await PermissionCatalogueSeeder.SeedAsync(_repository);

        // Keep the legacy global Tenant Admin role as a compatibility marker.
        // Tenant administrators are provisioned with a tenant-owned copy of the
        // TENANT_ADMIN template (see EnsureTenantStandardRolesAsync).
        await EnsureTenantAdminRoleAsync();

        await TenantAdminRolePermissionSeed.SeedAsync(_repository);
    }

    public async Task EnsurePlatformAdminAsync()
    {
        var role =
            await _repository.GetPlatformAdminRoleAsync();

        if (role == null)
        {
            throw new InvalidOperationException(
                "Platform Admin role does not exist.");
        }

        var user =
            await _repository.GetPlatformAdminUserAsync();

        if (user == null)
        {
            user = new User
            {
                Id = Guid.NewGuid(),
                TenantId = null,
                Username = "admin",
                Email = "admin@platform.local",
                PasswordHash =
                    _passwordService.HashPassword("123"),
                IsActive = true
            };

            await _repository.AddUserAsync(user);

            await _repository.SaveChangesAsync();
        }

        var userRole =
            await _repository.GetUserRoleAsync(
                user.Id,
                role.Id);

        if (userRole == null)
        {
            await _repository.AddUserRoleAsync(
                new UserRole
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    RoleId = role.Id
                });

            await _repository.SaveChangesAsync();
        }
    }

    private async Task EnsurePlatformUserAsync(
        string username,
        string email,
        string password,
        string roleName)
    {
        var role =
            await _repository.GetSystemRoleAsync(roleName);

        if (role == null)
        {
            throw new InvalidOperationException(
                $"Platform role '{roleName}' does not exist.");
        }

        var user =
            await _repository.GetPlatformUserAsync(username);

        if (user == null)
        {
            user = new User
            {
                Id = Guid.NewGuid(),
                TenantId = null,
                Username = username,
                Email = email,
                PasswordHash =
                    _passwordService.HashPassword(password),
                IsActive = true
            };

            await _repository.AddUserAsync(user);

            await _repository.SaveChangesAsync();
        }

        var userRole =
            await _repository.GetUserRoleAsync(
                user.Id,
                role.Id);

        if (userRole == null)
        {
            await _repository.AddUserRoleAsync(
                new UserRole
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    RoleId = role.Id
                });

            await _repository.SaveChangesAsync();
        }
    }

    public async Task EnsureTenantAdminRoleAsync()
    {
        var existingRole =
            await _repository.GetTenantAdminRoleAsync();

        if (existingRole != null)
            return;

        var role = new Role
        {
            Id = Guid.NewGuid(),
            TenantId = null,
            Name = "Tenant Admin",
            Description = "Tenant administrator role",
            IsSystemRole = true,
            IsActive = true
        };

        await _repository.AddRoleAsync(role);

        await _repository.SaveChangesAsync();
    }

    /// <summary>
    /// Creates or updates the tenant-owned copies of every standard role
    /// template. The operation is idempotent and retry-safe:
    /// - a role is only created when no template copy and no name collision exists;
    /// - existing template copies are upgraded additively when their template
    ///   version is behind the current version;
    /// - tenant customisations (extra permissions, removed permissions, renamed
    ///   roles) are never removed or overwritten.
    /// </summary>
    public async Task<TenantRoleProvisioningResult> EnsureTenantStandardRolesAsync(
        Guid tenantId)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("Tenant ID cannot be empty.", nameof(tenantId));
        }

        var tenantRoles = await _repository.GetTenantRolesAsync(tenantId);

        var rolesByTemplateKey = tenantRoles
            .Where(x => !string.IsNullOrWhiteSpace(x.TemplateKey))
            .GroupBy(x => x.TemplateKey!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.First(),
                StringComparer.OrdinalIgnoreCase);

        var rolesByName = tenantRoles
            .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.First(),
                StringComparer.OrdinalIgnoreCase);

        var permissionsByCode = (await _repository.GetPermissionsAsync())
            .ToDictionary(x => x.Code, StringComparer.OrdinalIgnoreCase);

        var created = new List<string>();
        var updated = new List<string>();
        var skipped = new List<string>();
        var conflicts = new List<string>();
        var assignedByRoleId = new Dictionary<Guid, HashSet<Guid>>();

        foreach (var template in StandardRoleTemplates.All)
        {
            if (rolesByTemplateKey.TryGetValue(template.Key, out var existingRole))
            {
                // Track the tenant role under its template key for later lookups.
                rolesByName[existingRole.Name] = existingRole;
            }
            else if (rolesByName.TryGetValue(template.Name, out _))
            {
                // A tenant-authored role already uses this name. Never overwrite
                // it; report the conflict instead of creating a duplicate.
                skipped.Add(template.Key);
                conflicts.Add(
                    $"Role '{template.Name}' already exists for tenant {tenantId} " +
                    "without template metadata. Template was not applied.");
                continue;
            }
            else
            {
                existingRole = new Role
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    Name = template.Name,
                    Description = template.Description,
                    IsSystemRole = false,
                    IsActive = true,
                    TemplateKey = template.Key,
                    TemplateVersion = template.Version
                };

                await _repository.AddRoleAsync(existingRole);
                rolesByTemplateKey[template.Key] = existingRole;
                rolesByName[template.Name] = existingRole;
                assignedByRoleId[existingRole.Id] = new HashSet<Guid>();
                created.Add(template.Key);
            }

            var isNew = created.Contains(template.Key, StringComparer.OrdinalIgnoreCase);

            // Only synchronise when the copy was just created or is behind the
            // template version. This preserves tenant customisations.
            var shouldSync = isNew ||
                !existingRole.TemplateVersion.HasValue ||
                existingRole.TemplateVersion.Value < template.Version;

            if (!shouldSync)
            {
                continue;
            }

            if (!assignedByRoleId.TryGetValue(existingRole.Id, out var assignedPermissionIds))
            {
                assignedPermissionIds = (await _repository
                        .GetRolePermissionsAsync(existingRole.Id))
                    .Select(x => x.PermissionId)
                    .ToHashSet();

                assignedByRoleId[existingRole.Id] = assignedPermissionIds;
            }

            foreach (var permissionCode in template.PermissionCodes)
            {
                if (!permissionsByCode.TryGetValue(permissionCode, out var permission))
                {
                    // Unknown codes are ignored at runtime; tests assert that the
                    // template matrix only references catalogue permissions.
                    continue;
                }

                if (!assignedPermissionIds.Add(permission.Id))
                {
                    continue;
                }

                await _repository.AddRolePermissionAsync(new RolePermission
                {
                    Id = Guid.NewGuid(),
                    RoleId = existingRole.Id,
                    PermissionId = permission.Id
                });
            }

            existingRole.TemplateVersion = template.Version;

            if (!isNew)
            {
                updated.Add(template.Key);
            }
        }

        await _repository.SaveChangesAsync();

        return new TenantRoleProvisioningResult(
            tenantId,
            created,
            updated,
            skipped,
            conflicts);
    }

    /// <summary>
    /// Upgrades existing tenants to the standard role templates and migrates
    /// legacy global Tenant Admin assignments to the tenant-owned copies.
    /// Safe to run on every startup.
    /// </summary>
    public async Task UpgradeExistingTenantsAsync()
    {
        var tenantIds = await _repository.GetTenantIdsAsync();

        foreach (var tenantId in tenantIds)
        {
            await EnsureTenantStandardRolesAsync(tenantId);
        }

        var legacyTenantAdminRole = await _repository.GetTenantAdminRoleAsync();
        if (legacyTenantAdminRole == null || !tenantIds.Any())
        {
            return;
        }

        var legacyAssignments = await _repository
            .GetUserRolesByRoleIdAsync(legacyTenantAdminRole.Id);

        foreach (var legacyAssignment in legacyAssignments)
        {
            var tenantId = legacyAssignment.User?.TenantId;
            if (!tenantId.HasValue)
            {
                continue;
            }

            var tenantAdminRole = await _repository
                .GetTenantRoleByTemplateKeyAsync(
                    tenantId.Value,
                    StandardRoleTemplates.TenantAdminKey);

            if (tenantAdminRole == null)
            {
                continue;
            }

            var tenantAssignment = await _repository.GetUserRoleAsync(
                legacyAssignment.UserId,
                tenantAdminRole.Id);

            if (tenantAssignment == null)
            {
                tenantAssignment = new UserRole
                {
                    Id = Guid.NewGuid(),
                    UserId = legacyAssignment.UserId,
                    RoleId = tenantAdminRole.Id
                };

                await _repository.AddUserRoleAsync(tenantAssignment);

                var legacyScopes = await _repository
                    .GetUserRoleScopesAsync(legacyAssignment.Id);

                foreach (var legacyScope in legacyScopes)
                {
                    await _repository.AddUserRoleScopeAsync(new UserRoleScope
                    {
                        Id = Guid.NewGuid(),
                        UserRoleId = tenantAssignment.Id,
                        CompanyId = legacyScope.CompanyId,
                        BranchId = legacyScope.BranchId,
                        IsActive = legacyScope.IsActive
                    });
                }
            }

            await _repository.RemoveUserRoleAsync(legacyAssignment);
        }

        await _repository.SaveChangesAsync();
    }

    public async Task EnsureTenantAdminAsync(
        Guid tenantId,
        string username,
        string email,
        string password)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("Tenant ID cannot be empty.", nameof(tenantId));
        }

        if (string.IsNullOrWhiteSpace(username))
        {
            throw new ArgumentException("Username cannot be empty.", nameof(username));
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("Email cannot be empty.", nameof(email));
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            throw new ArgumentException("Password cannot be empty.", nameof(password));
        }

        var role = await _repository.GetTenantRoleByTemplateKeyAsync(
            tenantId,
            StandardRoleTemplates.TenantAdminKey);

        if (role == null)
        {
            // Provision the tenant's standard roles (including its own
            // Tenant Admin copy) on demand. This keeps retries idempotent.
            await EnsureTenantStandardRolesAsync(tenantId);

            role = await _repository.GetTenantRoleByTemplateKeyAsync(
                tenantId,
                StandardRoleTemplates.TenantAdminKey);
        }

        if (role == null)
        {
            throw new InvalidOperationException(
                "Tenant Admin role could not be provisioned for the tenant. " +
                "A conflicting custom role with the same name may exist.");
        }

        var user = await _repository.GetTenantAdminUserAsync(tenantId, username);
        if (user == null)
        {
            user = new User
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Username = username.Trim(),
                Email = email.Trim(),
                PasswordHash = _passwordService.HashPassword(password),
                IsActive = true
            };

            await _repository.AddUserAsync(user);
            await _repository.SaveChangesAsync();
        }

        var userRole = await _repository.GetUserRoleAsync(user.Id, role.Id);
        if (userRole == null)
        {
            await _repository.AddUserRoleAsync(
                new UserRole
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    RoleId = role.Id
                });

            await _repository.SaveChangesAsync();
        }
    }
}