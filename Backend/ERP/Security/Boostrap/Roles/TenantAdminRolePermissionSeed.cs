using Security.Core.Models;
using Security.Interfaces;

namespace Security.Bootstrap.Roles;

public static class TenantAdminRolePermissionSeed
{
    public static async Task SeedAsync(
        ISecurityBootstrapRepository repository)
    {
        var role =
            await repository.GetTenantAdminRoleAsync();

        if (role == null)
        {
            throw new InvalidOperationException(
                "Tenant Admin role does not exist.");
        }

        var permissionCodes = new[]
        {
            "COMPANY.VIEW",
            "COMPANY.CREATE",
            "COMPANY.EDIT",
            "COMPANY.DELETE",
            "COMPANY.SOFT_DELETE",

            "BRANCH.VIEW",
            "BRANCH.CREATE",
            "BRANCH.EDIT",
            "BRANCH.DELETE",
            "BRANCH.SOFT_DELETE",

            "PRODUCT.VIEW",
            "PRODUCT.CREATE",
            "PRODUCT.EDIT",
            "PRODUCT.DELETE",
            "PRODUCT.SOFT_DELETE",

            "CUSTOMER.VIEW",
            "CUSTOMER.CREATE",
            "CUSTOMER.EDIT",
            "CUSTOMER.DELETE",
            "CUSTOMER.SOFT_DELETE",

            "SUPPLIER.VIEW",
            "SUPPLIER.CREATE",
            "SUPPLIER.EDIT",
            "SUPPLIER.DELETE",
            "SUPPLIER.SOFT_DELETE"
        };

        foreach (var code in permissionCodes)
        {
            var permission =
                await repository.GetPermissionByCodeAsync(code);

            if (permission == null)
            {
                throw new InvalidOperationException(
                    $"Required permission '{code}' does not exist.");
            }

            var existing =
                await repository.GetRolePermissionAsync(
                    role.Id,
                    permission.Id);

            if (existing != null)
                continue;

            await repository.AddRolePermissionAsync(
                new RolePermission
                {
                    Id = Guid.NewGuid(),
                    RoleId = role.Id,
                    PermissionId = permission.Id
                });
        }

        await repository.SaveChangesAsync();
    }
}