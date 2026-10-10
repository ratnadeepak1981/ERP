using Security.Core.Models;
using Security.Interfaces;

namespace Security.Bootstrap.Permissions.Domain;

public static class WarehousePermissionSeed
{
    public static async Task SeedAsync(ISecurityBootstrapRepository repository)
    {
        await SeedPermissionAsync(repository, "WAREHOUSE.VIEW", "View Warehouse", "Warehouse", "Allows viewing warehouses and zones.");
        await SeedPermissionAsync(repository, "WAREHOUSE.CREATE", "Create Warehouse", "Warehouse", "Allows creating warehouses and zones.");
        await SeedPermissionAsync(repository, "WAREHOUSE.EDIT", "Edit Warehouse", "Warehouse", "Allows editing warehouses and zones.");
        await SeedPermissionAsync(repository, "WAREHOUSE.DELETE", "Delete Warehouse", "Warehouse", "Allows deleting warehouses and zones.");
        await SeedPermissionAsync(repository, "WAREHOUSE.SOFT_DELETE", "Soft Delete Warehouse", "Warehouse", "Allows soft deleting warehouses.");
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
