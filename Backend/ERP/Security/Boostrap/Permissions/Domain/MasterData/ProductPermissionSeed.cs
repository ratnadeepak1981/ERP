using Security.Core.Models;
using Security.Interfaces;

namespace Security.Bootstrap.Permissions.Domain;

public static class ProductPermissionSeed
{
    public static async Task SeedAsync(
        ISecurityBootstrapRepository repository)
    {
        await SeedPermissionAsync(
            repository,
            "PRODUCT.VIEW",
            "View Product",
            "Product",
            "Allows viewing products.");

        await SeedPermissionAsync(
            repository,
            "PRODUCT.CREATE",
            "Create Product",
            "Product",
            "Allows creating products.");

        await SeedPermissionAsync(
            repository,
            "PRODUCT.EDIT",
            "Edit Product",
            "Product",
            "Allows editing products.");

        await SeedPermissionAsync(
            repository,
            "PRODUCT.DELETE",
            "Delete Product",
            "Product",
            "Allows deleting products.");

        await SeedPermissionAsync(
            repository,
            "PRODUCT.SOFT_DELETE",
            "Soft Delete Product",
            "Product",
            "Allows soft deleting products.");

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