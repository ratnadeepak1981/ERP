using Security.Core.Models;
using Security.Interfaces;

namespace Security.Bootstrap.Permissions.Domain;

public static class UnitOfMeasurePermissionSeed
{
    public static async Task SeedAsync(ISecurityBootstrapRepository repository)
    {
        await SeedPermissionAsync(repository, "UOM.VIEW", "View Unit of Measure", "UnitOfMeasure", "Allows viewing units of measure.");
        await SeedPermissionAsync(repository, "UOM.CREATE", "Create Unit of Measure", "UnitOfMeasure", "Allows creating units of measure.");
        await SeedPermissionAsync(repository, "UOM.EDIT", "Edit Unit of Measure", "UnitOfMeasure", "Allows editing units of measure.");
        await SeedPermissionAsync(repository, "UOM.DELETE", "Delete Unit of Measure", "UnitOfMeasure", "Allows deleting units of measure.");
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
