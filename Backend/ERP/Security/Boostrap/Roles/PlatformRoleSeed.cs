using Security.Core.Models;
using Security.Interfaces;

namespace Security.Bootstrap.Roles;

public static class PlatformRoleSeed
{
    public static async Task SeedAsync(
        ISecurityBootstrapRepository repository)
    {
        await SeedRoleAsync(
            repository,
            "Platform Admin",
            "Platform-level administrator.");

        await SeedRoleAsync(
            repository,
            "Platform Staff",
            "Platform operational staff.");

        await SeedRoleAsync(
            repository,
            "Security Manager",
            "Platform security administrator.");

        await SeedRoleAsync(
            repository,
            "Audit",
            "Platform audit user.");

        await SeedRoleAsync(
            repository,
            "Executive",
            "Platform executive user.");

        await repository.SaveChangesAsync();
    }

    private static async Task SeedRoleAsync(
        ISecurityBootstrapRepository repository,
        string name,
        string description)
    {
        var existing =
            await repository.GetSystemRoleAsync(name);

        if (existing != null)
            return;

        var role = new Role
        {
            Id = Guid.NewGuid(),
            TenantId = null,
            Name = name,
            Description = description,
            IsSystemRole = true,
            IsActive = true
        };

        await repository.AddRoleAsync(role);
    }
}