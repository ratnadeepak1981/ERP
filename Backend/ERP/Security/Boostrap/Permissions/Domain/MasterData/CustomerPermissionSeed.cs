using Security.Core.Models;
using Security.Interfaces;

namespace Security.Bootstrap.Permissions.Domain;

public static class CustomerPermissionSeed
{
    public static async Task SeedAsync(
        ISecurityBootstrapRepository repository)
    {
        await SeedPermissionAsync(
            repository,
            "CUSTOMER.VIEW",
            "View Customer",
            "Customer",
            "Allows viewing customers.");

        await SeedPermissionAsync(
            repository,
            "CUSTOMER.CREATE",
            "Create Customer",
            "Customer",
            "Allows creating customers.");

        await SeedPermissionAsync(
            repository,
            "CUSTOMER.EDIT",
            "Edit Customer",
            "Customer",
            "Allows editing customers.");

        await SeedPermissionAsync(
            repository,
            "CUSTOMER.DELETE",
            "Delete Customer",
            "Customer",
            "Allows deleting customers.");

        await SeedPermissionAsync(
            repository,
            "CUSTOMER.SOFT_DELETE",
            "Soft Delete Customer",
            "Customer",
            "Allows soft deleting customers.");

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