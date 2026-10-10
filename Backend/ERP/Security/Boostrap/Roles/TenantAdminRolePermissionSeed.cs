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
            "SUPPLIER.SOFT_DELETE",

            "PURCHASE_ORDER.VIEW",
            "PURCHASE_ORDER.CREATE",
            "PURCHASE_ORDER.EDIT",
            "PURCHASE_ORDER.DELETE",
            "PURCHASE_ORDER.SUBMIT",
            "PURCHASE_ORDER.APPROVE",
            "PURCHASE_ORDER.CANCEL",

            "PURCHASE_REQUISITION.VIEW",
            "PURCHASE_REQUISITION.CREATE",
            "PURCHASE_REQUISITION.EDIT",
            "PURCHASE_REQUISITION.SUBMIT",
            "PURCHASE_REQUISITION.APPROVE",
            "PURCHASE_REQUISITION.CANCEL",

            "PROCUREMENT.SETTINGS.VIEW",
            "PROCUREMENT.SETTINGS.EDIT",

            "CATEGORY.VIEW",
            "CATEGORY.CREATE",
            "CATEGORY.EDIT",
            "CATEGORY.DELETE",
            "CATEGORY.SOFT_DELETE",

            "UOM.VIEW",
            "UOM.CREATE",
            "UOM.EDIT",
            "UOM.DELETE",

            "WAREHOUSE.VIEW",
            "WAREHOUSE.CREATE",
            "WAREHOUSE.EDIT",
            "WAREHOUSE.DELETE",
            "WAREHOUSE.SOFT_DELETE",

            "LOCATION.VIEW",
            "LOCATION.CREATE",
            "LOCATION.EDIT",
            "LOCATION.DELETE",

            "ADDRESS.VIEW",
            "ADDRESS.CREATE",
            "ADDRESS.EDIT",
            "ADDRESS.DELETE",

            "CONTACT.VIEW",
            "CONTACT.CREATE",
            "CONTACT.EDIT",
            "CONTACT.DELETE",

            "SUPPLIER_PRICE.VIEW",
            "SUPPLIER_PRICE.CREATE",
            "SUPPLIER_PRICE.EDIT",
            "SUPPLIER_PRICE.DELETE",

            "COUNTRY.VIEW"
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