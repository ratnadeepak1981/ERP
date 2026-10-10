using Security.Core.Models;
using Security.Interfaces;

namespace Security.Bootstrap.Permissions.Domain;

public static class LocationPermissionSeed
{
    public static async Task SeedAsync(ISecurityBootstrapRepository repository)
    {
        await SeedPermissionAsync(repository, "LOCATION.VIEW", "View Warehouse Location", "Location", "Allows viewing warehouse locations and types.");
        await SeedPermissionAsync(repository, "LOCATION.CREATE", "Create Warehouse Location", "Location", "Allows creating warehouse locations and types.");
        await SeedPermissionAsync(repository, "LOCATION.EDIT", "Edit Warehouse Location", "Location", "Allows editing warehouse locations and types.");
        await SeedPermissionAsync(repository, "LOCATION.DELETE", "Delete Warehouse Location", "Location", "Allows deleting warehouse locations and types.");
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
