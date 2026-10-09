using Security.Core.Models;
using Security.Interfaces;

namespace Security.Boostrap.Permissions.Platform;

public static class PlatformRolePermissionSeed
{
    public static async Task SeedAsync(
        ISecurityBootstrapRepository repository)
    {
        await AddPermissionsAsync(
            repository,
            "Platform Admin",
            new[]
            {
                "TENANT.VIEW",
                "TENANT.CREATE",
                "TENANT.EDIT",
                "TENANT.SUSPEND",
                "SUBSCRIPTION.VIEW",
                "SUBSCRIPTION.CREATE",
                "SUBSCRIPTION.EDIT",
                "AUDIT.VIEW"
            });

        await AddPermissionsAsync(
            repository,
            "Platform Staff",
            new[]
            {
                "TENANT.VIEW",
                "SUBSCRIPTION.VIEW"
            });

        await AddPermissionsAsync(
            repository,
            "Security Manager",
            new[]
            {
                "TENANT.VIEW",
                "AUDIT.VIEW"
            });

        await AddPermissionsAsync(
            repository,
            "Audit",
            new[]
            {
                "AUDIT.VIEW"
            });

        await AddPermissionsAsync(
            repository,
            "Executive",
            new[]
            {
                "TENANT.VIEW",
                "SUBSCRIPTION.VIEW",
                "AUDIT.VIEW"
            });
    }

    private static async Task AddPermissionsAsync(
        ISecurityBootstrapRepository repository,
        string roleName,
        string[] permissionCodes)
    {
        var role =
            await repository.GetSystemRoleAsync(roleName);

        if (role == null)
            throw new InvalidOperationException(
                $"Role '{roleName}' not found.");

        foreach (var code in permissionCodes)
        {
            var permission =
                await repository.GetPermissionByCodeAsync(code);

            if (permission == null)
                throw new InvalidOperationException(
                    $"Permission '{code}' not found.");

            var existing =
                await repository.GetRolePermissionAsync(
                    role.Id,
                    permission.Id);

            if (existing != null)
                continue;

            await repository.AddRolePermissionAsync(
                new RolePermission
                {
                    Id = Guid.NewGuid(),
                    RoleId = role.Id,
                    PermissionId = permission.Id
                });

            await repository.SaveChangesAsync();
        }
    }
}