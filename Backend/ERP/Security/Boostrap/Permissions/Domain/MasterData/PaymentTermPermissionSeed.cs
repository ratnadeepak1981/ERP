using Security.Core.Models;
using Security.Interfaces;

namespace Security.Bootstrap.Permissions.Domain;

public static class PaymentTermPermissionSeed
{
    public static async Task SeedAsync(ISecurityBootstrapRepository repository)
    {
        var permissions = new[]
        {
            new Permission
            {
                Id = Guid.NewGuid(),
                Code = "PAYMENT_TERM.VIEW",
                Name = "View Payment Terms",
                Description = "Can view payment term records"
            },
            new Permission
            {
                Id = Guid.NewGuid(),
                Code = "PAYMENT_TERM.CREATE",
                Name = "Create Payment Term",
                Description = "Can create new payment term records"
            },
            new Permission
            {
                Id = Guid.NewGuid(),
                Code = "PAYMENT_TERM.EDIT",
                Name = "Edit Payment Term",
                Description = "Can edit or reactivate existing payment term records"
            },
            new Permission
            {
                Id = Guid.NewGuid(),
                Code = "PAYMENT_TERM.DELETE",
                Name = "Deactivate Payment Term",
                Description = "Can deactivate payment term records"
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
