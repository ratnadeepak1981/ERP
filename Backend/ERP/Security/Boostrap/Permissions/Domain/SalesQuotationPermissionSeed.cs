using Security.Core.Models;
using Security.Interfaces;

namespace Security.Bootstrap.Permissions.Domain;

public static class SalesQuotationPermissionSeed
{
    public static async Task SeedAsync(
        ISecurityBootstrapRepository repository)
    {
        await SeedPermissionAsync(
            repository,
            "SALES_QUOTATION.VIEW",
            "View Sales Quotation",
            "SalesQuotation",
            "Allows viewing sales quotations.");

        await SeedPermissionAsync(
            repository,
            "SALES_QUOTATION.CREATE",
            "Create Sales Quotation",
            "SalesQuotation",
            "Allows creating sales quotations and quotation requests.");

        await SeedPermissionAsync(
            repository,
            "SALES_QUOTATION.EDIT",
            "Edit Sales Quotation",
            "SalesQuotation",
            "Allows editing sales quotation drafts.");

        await SeedPermissionAsync(
            repository,
            "SALES_QUOTATION.DELETE",
            "Delete Sales Quotation",
            "SalesQuotation",
            "Allows deleting sales quotation drafts.");

        await SeedPermissionAsync(
            repository,
            "SALES_QUOTATION.SUBMIT",
            "Submit Sales Quotation",
            "SalesQuotation",
            "Allows submitting sales quotations for managerial approval.");

        await SeedPermissionAsync(
            repository,
            "SALES_QUOTATION.CONFIRM",
            "Approve/Reject Sales Quotation",
            "SalesQuotation",
            "Allows approving or rejecting sales quotations (Manager/Admin).");

        await SeedPermissionAsync(
            repository,
            "SALES_QUOTATION.CANCEL",
            "Cancel Sales Quotation",
            "SalesQuotation",
            "Allows cancelling sales quotations.");

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
