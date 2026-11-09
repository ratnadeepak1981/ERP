using Security.Core.Models;
using Security.Interfaces;

namespace Security.Bootstrap.Permissions.Domain;

public static class StockCountPermissionSeed
{
    public static async Task SeedAsync(ISecurityBootstrapRepository repository)
    {
        var permissions = new[]
        {
            ("STOCK_COUNT.VIEW", "View Stock Counts", "Allows viewing stock counts."),
            ("STOCK_COUNT.CREATE", "Create Stock Counts", "Allows creating stock count drafts."),
            ("STOCK_COUNT.EDIT", "Edit Stock Counts", "Allows editing stock count drafts."),
            ("STOCK_COUNT.SUBMIT", "Submit Stock Counts", "Allows submitting stock counts for approval."),
            ("STOCK_COUNT.APPROVE", "Approve Stock Counts", "Allows approving submitted stock counts."),
            ("STOCK_COUNT.POST", "Post Stock Counts", "Allows posting approved stock count adjustments."),
            ("STOCK_COUNT.CANCEL", "Cancel Stock Counts", "Allows cancelling unposted stock counts.")
        };

        foreach (var (code, name, description) in permissions)
        {
            if (await repository.GetPermissionByCodeAsync(code) != null)
                continue;

            await repository.AddPermissionAsync(new Permission
            {
                Id = Guid.NewGuid(),
                Code = code,
                Name = name,
                Module = "StockCount",
                Description = description,
                IsActive = true
            });
        }

        await repository.SaveChangesAsync();
    }
}
