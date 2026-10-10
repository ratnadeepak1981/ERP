using Security.Core.Models;
using Security.Interfaces;

namespace Security.Bootstrap.Permissions.Domain;

public static class SupplierProductPricePermissionSeed
{
    public static async Task SeedAsync(ISecurityBootstrapRepository repository)
    {
        await SeedPermissionAsync(repository, "SUPPLIER_PRICE.VIEW", "View Supplier Product Price", "SupplierPrice", "Allows viewing supplier product prices.");
        await SeedPermissionAsync(repository, "SUPPLIER_PRICE.CREATE", "Create Supplier Product Price", "SupplierPrice", "Allows creating supplier product prices.");
        await SeedPermissionAsync(repository, "SUPPLIER_PRICE.EDIT", "Edit Supplier Product Price", "SupplierPrice", "Allows editing supplier product prices.");
        await SeedPermissionAsync(repository, "SUPPLIER_PRICE.DELETE", "Delete Supplier Product Price", "SupplierPrice", "Allows deleting supplier product prices.");
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
