using Security.Core.Models;
using Security.Interfaces;

namespace Security.Bootstrap.Permissions.Domain;

public static class PurchaseOrderPermissionSeed
{
    public static async Task SeedAsync(
        ISecurityBootstrapRepository repository)
    {
        await SeedPermissionAsync(
            repository,
            "PURCHASE_ORDER.VIEW",
            "View Purchase Order",
            "PurchaseOrder",
            "Allows viewing purchase orders.");

        await SeedPermissionAsync(
            repository,
            "PURCHASE_ORDER.CREATE",
            "Create Purchase Order",
            "PurchaseOrder",
            "Allows creating purchase orders.");

        await SeedPermissionAsync(
            repository,
            "PURCHASE_ORDER.EDIT",
            "Edit Purchase Order",
            "PurchaseOrder",
            "Allows editing purchase orders.");

        await SeedPermissionAsync(
            repository,
            "PURCHASE_ORDER.DELETE",
            "Delete Purchase Order",
            "PurchaseOrder",
            "Allows deleting purchase orders.");

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
