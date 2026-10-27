using Security.Core.Models;
using Security.Interfaces;

namespace Security.Bootstrap.Permissions.Domain;

public static class BatchPermissionSeed
{
    public static async Task SeedAsync(ISecurityBootstrapRepository repository)
    {
        var permissions = new[]
        {
            new Permission
            {
                Id = Guid.NewGuid(),
                Code = "BATCH.VIEW",
                Name = "View Batches",
                Description = "Allows viewing product batch details and expiry dates."
            },
            new Permission
            {
                Id = Guid.NewGuid(),
                Code = "BATCH.CREATE",
                Name = "Create Batches",
                Description = "Allows creating new product batches."
            },
            new Permission
            {
                Id = Guid.NewGuid(),
                Code = "BATCH.EDIT",
                Name = "Edit Batches",
                Description = "Allows editing product batch metadata."
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
