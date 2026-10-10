using Security.Core.Models;
using Security.Interfaces;

namespace Security.Bootstrap.Permissions.Domain;

public static class CategoryPermissionSeed
{
    public static async Task SeedAsync(ISecurityBootstrapRepository repository)
    {
        await SeedPermissionAsync(repository, "CATEGORY.VIEW", "View Category", "Category", "Allows viewing categories.");
        await SeedPermissionAsync(repository, "CATEGORY.CREATE", "Create Category", "Category", "Allows creating categories.");
        await SeedPermissionAsync(repository, "CATEGORY.EDIT", "Edit Category", "Category", "Allows editing categories.");
        await SeedPermissionAsync(repository, "CATEGORY.DELETE", "Delete Category", "Category", "Allows deleting categories.");
        await SeedPermissionAsync(repository, "CATEGORY.SOFT_DELETE", "Soft Delete Category", "Category", "Allows soft deleting categories.");
        await repository.SaveChangesAsync();
    }

    private static async Task SeedPermissionAsync(
        ISecurityBootstrapRepository repository,
        string code,
        string name,
        string module,
        string description)
    {
        var existing = await repository.GetPermissionByCodeAsync(code);
        if (existing != null) return;

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
