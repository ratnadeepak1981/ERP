using Security.Core.Models;
using Security.Interfaces;

namespace Security.Boostrap.Permissions.Platform;

public static class PlatformPermissionSeed
{
    public static async Task SeedAsync(
        ISecurityBootstrapRepository repository)
    {
        await SeedPermissionAsync(
            repository,
            "TENANT.VIEW",
            "View Tenant",
            "Tenant",
            "Allows viewing tenants.");

        await SeedPermissionAsync(
            repository,
            "TENANT.CREATE",
            "Create Tenant",
            "Tenant",
            "Allows creating tenants.");

        await SeedPermissionAsync(
            repository,
            "TENANT.EDIT",
            "Edit Tenant",
            "Tenant",
            "Allows editing tenants.");

        await SeedPermissionAsync(
            repository,
            "TENANT.SUSPEND",
            "Suspend Tenant",
            "Tenant",
            "Allows suspending tenants.");

        await SeedPermissionAsync(
            repository,
            "SUBSCRIPTION.VIEW",
            "View Subscription",
            "Subscription",
            "Allows viewing subscriptions.");

        await SeedPermissionAsync(
            repository,
            "SUBSCRIPTION.CREATE",
            "Create Subscription",
            "Subscription",
            "Allows creating subscriptions.");

        await SeedPermissionAsync(
            repository,
            "SUBSCRIPTION.EDIT",
            "Edit Subscription",
            "Subscription",
            "Allows editing subscriptions.");

        await SeedPermissionAsync(
            repository,
            "AUDIT.VIEW",
            "View Audit",
            "Audit",
            "Allows viewing audit information.");

        await repository.SaveChangesAsync();
    }

    private static async Task SeedPermissionAsync(
        ISecurityBootstrapRepository repository,
        string code,
        string name,
        string module,
        string description)
    {
        var existing =
            await repository.GetPermissionByCodeAsync(code);

        if (existing != null)
            return;

        await repository.AddPermissionAsync(
            new Permission
            {
                Id = Guid.NewGuid(),
                Code = code,
                Name = name,
                Module = module,
                Description = description,
                IsActive = true
            });
    }
}