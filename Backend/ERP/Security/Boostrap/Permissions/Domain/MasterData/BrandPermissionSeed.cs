using Security.Core.Models;
using Security.Interfaces;

namespace Security.Bootstrap.Permissions.Domain;

public static class BrandPermissionSeed
{
    public static async Task SeedAsync(ISecurityBootstrapRepository repository)
    {
        var permissions = new[]
        {
            new Permission
            {
                Id = Guid.NewGuid(),
                Code = "BRAND.VIEW",
                Name = "View Brands",
                Description = "Can view brand records"
            },
            new Permission
            {
                Id = Guid.NewGuid(),
                Code = "BRAND.CREATE",
                Name = "Create Brand",
                Description = "Can create new brand records"
            },
            new Permission
            {
                Id = Guid.NewGuid(),
                Code = "BRAND.EDIT",
                Name = "Edit Brand",
                Description = "Can edit or reactivate existing brand records"
            },
            new Permission
            {
                Id = Guid.NewGuid(),
                Code = "BRAND.DELETE",
                Name = "Deactivate Brand",
                Description = "Can deactivate brand records"
            }
        };

        foreach (var p in permissions)
        {
            var existing = await repository.GetPermissionByCodeAsync(p.Code);
            if (existing == null)
            {
                await repository.AddPermissionAsync(p);
            }
        }

        await repository.SaveChangesAsync();
    }
}
