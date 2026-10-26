using Security.Core.Models;
using Security.Interfaces;

namespace Security.Bootstrap.Permissions.Domain;

public static class TaxCodePermissionSeed
{
    public static async Task SeedAsync(ISecurityBootstrapRepository repository)
    {
        var permissions = new[]
        {
            new Permission
            {
                Id = Guid.NewGuid(),
                Code = "TAX_CODE.VIEW",
                Name = "View Tax Codes",
                Description = "Can view tax codes and rates"
            },
            new Permission
            {
                Id = Guid.NewGuid(),
                Code = "TAX_CODE.CREATE",
                Name = "Create Tax Code",
                Description = "Can create new tax code records"
            },
            new Permission
            {
                Id = Guid.NewGuid(),
                Code = "TAX_CODE.EDIT",
                Name = "Edit Tax Code",
                Description = "Can edit or reactivate existing tax code records"
            },
            new Permission
            {
                Id = Guid.NewGuid(),
                Code = "TAX_CODE.DELETE",
                Name = "Deactivate Tax Code",
                Description = "Can deactivate tax code records"
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
