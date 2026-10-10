using ERP.Domain.Bootstrap.Permissions;
using Security.Boostrap.Permissions.Platform;
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
        await EnsureTenantAdminRoleAsync();

        await DomainPermissionSeeder.SeedAsync(_repository);

        await ProductPermissionSeed.SeedAsync(_repository);

        await CustomerPermissionSeed.SeedAsync(_repository);

        await SupplierPermissionSeed.SeedAsync(_repository);

        await PurchaseOrderPermissionSeed.SeedAsync(_repository);

        await PurchaseRequisitionPermissionSeed.SeedAsync(_repository);

        await GoodsReceiptNotePermissionSeed.SeedAsync(_repository);

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

        var role = await _repository.GetTenantAdminRoleAsync();
        if (role == null)
        {
            throw new InvalidOperationException("Tenant Admin role does not exist.");
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