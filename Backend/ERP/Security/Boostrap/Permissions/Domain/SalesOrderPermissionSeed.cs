using Security.Core.Models;
using Security.Interfaces;

namespace Security.Bootstrap.Permissions.Domain;

public static class SalesOrderPermissionSeed
{
    public static async Task SeedAsync(
        ISecurityBootstrapRepository repository)
    {
        await SeedPermissionAsync(
            repository,
            "SALES_ORDER.VIEW",
            "View Sales Order",
            "SalesOrder",
            "Allows viewing sales orders.");

        await SeedPermissionAsync(
            repository,
            "SALES_ORDER.CREATE",
            "Create Sales Order",
            "SalesOrder",
            "Allows creating sales orders.");

        await SeedPermissionAsync(
            repository,
            "SALES_ORDER.EDIT",
            "Edit Sales Order",
            "SalesOrder",
            "Allows editing sales orders.");

        await SeedPermissionAsync(
            repository,
            "SALES_ORDER.DELETE",
            "Delete Sales Order",
            "SalesOrder",
            "Allows deleting sales orders.");

        await SeedPermissionAsync(
            repository,
            "SALES_ORDER.SUBMIT",
            "Submit Sales Order",
            "SalesOrder",
            "Allows submitting sales orders.");

        await SeedPermissionAsync(
            repository,
            "SALES_ORDER.CONFIRM",
            "Confirm/Approve Sales Order",
            "SalesOrder",
            "Allows confirming or approving sales orders.");

        await SeedPermissionAsync(
            repository,
            "SALES_ORDER.CANCEL",
            "Cancel Sales Order",
            "SalesOrder",
            "Allows cancelling sales orders.");

        await repository.SaveChangesAsync();
    }

    private static async Task SeedPermissionAsync(
        ISecurityBootstrapRepository repository,
        string code,
        string name,
        string module,
        string description)
    {
        var existing =
            await repository.GetPermissionByCodeAsync(code);

        if (existing != null)
            return;

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
