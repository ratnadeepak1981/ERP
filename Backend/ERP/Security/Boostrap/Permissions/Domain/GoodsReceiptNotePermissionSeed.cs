using Security.Core.Models;
using Security.Interfaces;

namespace Security.Bootstrap.Permissions.Domain;

public static class GoodsReceiptNotePermissionSeed
{
    public static async Task SeedAsync(ISecurityBootstrapRepository repository)
    {
        await SeedPermissionAsync(
            repository,
            "GRN.VIEW",
            "View Goods Receipt Note",
            "GoodsReceiptNote",
            "Allows viewing goods receipt notes.");

        await SeedPermissionAsync(
            repository,
            "GRN.CREATE",
            "Create Goods Receipt Note",
            "GoodsReceiptNote",
            "Allows creating draft goods receipt notes.");

        await SeedPermissionAsync(
            repository,
            "GRN.EDIT",
            "Edit Goods Receipt Note",
            "GoodsReceiptNote",
            "Allows editing draft goods receipt notes.");

        await SeedPermissionAsync(
            repository,
            "GRN.CANCEL",
            "Cancel Goods Receipt Note",
            "GoodsReceiptNote",
            "Allows cancelling draft goods receipt notes.");

        await SeedPermissionAsync(
            repository,
            "GRN.CONFIRM",
            "Confirm Goods Receipt Note",
            "GoodsReceiptNote",
            "Allows confirming goods receipt notes and posting to inventory.");

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
        if (existing != null) return;

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
