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

            "SALES_ORDER.VIEW",
            "SALES_ORDER.CREATE",
            "SALES_ORDER.EDIT",
            "SALES_ORDER.DELETE",
            "SALES_ORDER.SUBMIT",
            "SALES_ORDER.CONFIRM",
            "SALES_ORDER.CANCEL",

            "SALES_SHIPMENT.VIEW",
            "SALES_SHIPMENT.CREATE",
            "SALES_SHIPMENT.EDIT",
            "SALES_SHIPMENT.DELETE",
            "SALES_SHIPMENT.DISPATCH",
            "SALES_SHIPMENT.DELIVER",
            "SALES_SHIPMENT.CANCEL",

            "GRN.VIEW",
            "GRN.CREATE",
            "GRN.EDIT",
            "GRN.CANCEL",
            "GRN.CONFIRM",

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
            "UOM.CONVERT",

            "CURRENCY.VIEW",
            "CURRENCY.CREATE",
            "CURRENCY.EDIT",
            "CURRENCY.DELETE",

            "BRAND.VIEW",
            "BRAND.CREATE",
            "BRAND.EDIT",
            "BRAND.DELETE",

            "PAYMENT_TERM.VIEW",
            "PAYMENT_TERM.CREATE",
            "PAYMENT_TERM.EDIT",
            "PAYMENT_TERM.DELETE",

            "TAX_CODE.VIEW",
            "TAX_CODE.CREATE",
            "TAX_CODE.EDIT",
            "TAX_CODE.DELETE",

            "WAREHOUSE.VIEW",
            "WAREHOUSE.CREATE",
            "WAREHOUSE.EDIT",
            "WAREHOUSE.DELETE",
            "WAREHOUSE.SOFT_DELETE",
            "WAREHOUSE.ACTIVATE",
            "WAREHOUSE.DEACTIVATE",

            "LOCATION.VIEW",
            "LOCATION.CREATE",
            "LOCATION.EDIT",
            "LOCATION.DELETE",
            "LOCATION.ACTIVATE",
            "LOCATION.DEACTIVATE",

            "BATCH.VIEW",
            "BATCH.CREATE",
            "BATCH.EDIT",

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

            "COUNTRY.VIEW",

            "CRM.LEAD.VIEW",
            "CRM.LEAD.CREATE",
            "CRM.LEAD.EDIT",
            "CRM.LEAD.QUALIFY",
            "CRM.LEAD.CONVERT",
            "CRM.OPPORTUNITY.VIEW",
            "CRM.OPPORTUNITY.MANAGE",
            "CRM.ACTIVITY.VIEW",
            "CRM.ACTIVITY.MANAGE"
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