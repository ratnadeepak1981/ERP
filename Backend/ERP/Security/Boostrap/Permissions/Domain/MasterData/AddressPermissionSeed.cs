using Security.Core.Models;
using Security.Interfaces;

namespace Security.Bootstrap.Permissions.Domain;

public static class AddressPermissionSeed
{
    public static async Task SeedAsync(ISecurityBootstrapRepository repository)
    {
        await SeedPermissionAsync(repository, "ADDRESS.VIEW", "View Address", "Address", "Allows viewing addresses and address types.");
        await SeedPermissionAsync(repository, "ADDRESS.CREATE", "Create Address", "Address", "Allows creating addresses and address types.");
        await SeedPermissionAsync(repository, "ADDRESS.EDIT", "Edit Address", "Address", "Allows editing addresses and address types.");
        await SeedPermissionAsync(repository, "ADDRESS.DELETE", "Delete Address", "Address", "Allows deleting addresses and address types.");
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
