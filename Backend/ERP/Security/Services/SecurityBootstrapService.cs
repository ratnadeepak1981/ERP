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

    public async Task EnsurePlatformAdminAsync()
    {
        var role = await _repository.GetPlatformAdminRoleAsync();

        if (role == null)
        {
            role = new Role
            {
                Id = Guid.NewGuid(),
                TenantId = null,
                Name = "Platform Admin",
                Description = "Platform-level administrator",
                IsSystemRole = true,
                IsActive = true
            };

            await _repository.AddRoleAsync(role);
        }

        var user = await _repository.GetPlatformAdminUserAsync();

        if (user == null)
        {
            user = new User
            {
                Id = Guid.NewGuid(),
                TenantId = null,
                Username = "admin",
                Email = "admin@platform.local",
                PasswordHash = _passwordService.HashPassword("123"),
                IsActive = true
            };

            await _repository.AddUserAsync(user);

            var userRole = new UserRole
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                RoleId = role.Id
            };

            await _repository.AddUserRoleAsync(userRole);
        }

        await _repository.SaveChangesAsync();
    }

    public async Task EnsureTenantAdminRoleAsync()
    {
        var role = await _repository.GetTenantAdminRoleAsync();

        if (role == null)
        {
            role = new Role
            {
                Id = Guid.NewGuid(),
                TenantId = null,
                Name = "Tenant Admin",
                Description = "Tenant-level administrator",
                IsSystemRole = true,
                IsActive = true
            };

            await _repository.AddRoleAsync(role);
            await _repository.SaveChangesAsync();
        }
    }

    public async Task EnsureTenantAdminAsync(
        Guid tenantId,
        string username,
        string email,
        string password)
    {
        var role = await _repository.GetTenantAdminRoleAsync();

        if (role == null)
        {
            throw new InvalidOperationException(
                "Tenant Admin role does not exist.");
        }

        var user = await _repository.GetTenantAdminUserAsync(
            tenantId,
            username);

        if (user == null)
        {
            user = new User
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Username = username,
                Email = email,
                PasswordHash = _passwordService.HashPassword(password),
                IsActive = true
            };

            await _repository.AddUserAsync(user);

            var userRole = new UserRole
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                RoleId = role.Id
            };

            await _repository.AddUserRoleAsync(userRole);

            await _repository.SaveChangesAsync();
        }
    }
}