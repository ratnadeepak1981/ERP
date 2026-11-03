using Security.Core.Models;
using Security.Interfaces;

namespace Security.Bootstrap.Permissions.Domain;

public static class SalesInvoicePermissionSeed
{
    public static async Task SeedAsync(
        ISecurityBootstrapRepository repository)
    {
        await SeedPermissionAsync(
            repository,
            "SALES_INVOICE.VIEW",
            "View Sales Invoice",
            "SalesInvoice",
            "Allows viewing sales invoices.");

        await SeedPermissionAsync(
            repository,
            "SALES_INVOICE.CREATE",
            "Create Sales Invoice",
            "SalesInvoice",
            "Allows creating sales invoices from shipments.");

        await SeedPermissionAsync(
            repository,
            "SALES_INVOICE.EDIT",
            "Edit Sales Invoice",
            "SalesInvoice",
            "Allows editing draft sales invoices.");

        await SeedPermissionAsync(
            repository,
            "SALES_INVOICE.CONFIRM",
            "Confirm Sales Invoice",
            "SalesInvoice",
            "Allows confirming sales invoices.");

        await SeedPermissionAsync(
            repository,
            "SALES_INVOICE.CANCEL",
            "Cancel Sales Invoice",
            "SalesInvoice",
            "Allows cancelling sales invoices.");

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

        if (existing == null)
        {
            var permission = new Permission
            {
                Id = Guid.NewGuid(),
                Code = code,
                Name = name,
                Module = module,
                Description = description,
                IsActive = true
            };

            await repository.AddPermissionAsync(permission);
        }
    }
}
