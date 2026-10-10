using Security.Core.Models;
using Security.Interfaces;

namespace Security.Bootstrap.Permissions.Domain;

public static class CountryPermissionSeed
{
    public static async Task SeedAsync(ISecurityBootstrapRepository repository)
    {
        await SeedPermissionAsync(repository, "COUNTRY.VIEW", "View Country", "Country", "Allows viewing countries.");
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
