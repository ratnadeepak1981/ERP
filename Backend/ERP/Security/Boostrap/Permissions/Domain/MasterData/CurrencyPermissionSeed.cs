using Security.Core.Models;
using Security.Interfaces;

namespace Security.Bootstrap.Permissions.Domain;

public static class CurrencyPermissionSeed
{
    public static async Task SeedAsync(ISecurityBootstrapRepository repository)
    {
        await SeedPermissionAsync(repository, "CURRENCY.VIEW", "View Currency", "Currency", "Allows viewing currencies.");
        await SeedPermissionAsync(repository, "CURRENCY.CREATE", "Create Currency", "Currency", "Allows creating currencies.");
        await SeedPermissionAsync(repository, "CURRENCY.EDIT", "Edit Currency", "Currency", "Allows editing currencies.");
        await SeedPermissionAsync(repository, "CURRENCY.DELETE", "Delete Currency", "Currency", "Allows deleting currencies.");
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
