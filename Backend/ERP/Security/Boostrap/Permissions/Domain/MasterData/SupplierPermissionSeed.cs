using Security.Core.Models;
using Security.Interfaces;

namespace Security.Bootstrap.Permissions.Domain;

public static class SupplierPermissionSeed
{
    public static async Task SeedAsync(
        ISecurityBootstrapRepository repository)
    {
        await SeedPermissionAsync(
            repository,
            "SUPPLIER.VIEW",
            "View Supplier",
            "Supplier",
            "Allows viewing suppliers.");

        await SeedPermissionAsync(
            repository,
            "SUPPLIER.CREATE",
            "Create Supplier",
            "Supplier",
            "Allows creating suppliers.");

        await SeedPermissionAsync(
            repository,
            "SUPPLIER.EDIT",
            "Edit Supplier",
            "Supplier",
            "Allows editing suppliers.");

        await SeedPermissionAsync(
            repository,
            "SUPPLIER.DELETE",
            "Delete Supplier",
            "Supplier",
            "Allows deleting suppliers.");

        await SeedPermissionAsync(
            repository,
            "SUPPLIER.SOFT_DELETE",
            "Soft Delete Supplier",
            "Supplier",
            "Allows soft deleting suppliers.");

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