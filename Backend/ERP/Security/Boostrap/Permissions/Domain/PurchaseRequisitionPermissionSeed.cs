using Security.Core.Models;
using Security.Interfaces;

namespace Security.Bootstrap.Permissions.Domain;

public static class PurchaseRequisitionPermissionSeed
{
    public static async Task SeedAsync(ISecurityBootstrapRepository repository)
    {
        await SeedPermissionAsync(repository, "PURCHASE_REQUISITION.VIEW", "View Purchase Requisition", "Procurement", "Allows viewing purchase requisitions.");
        await SeedPermissionAsync(repository, "PURCHASE_REQUISITION.CREATE", "Create Purchase Requisition", "Procurement", "Allows creating purchase requisitions.");
        await SeedPermissionAsync(repository, "PURCHASE_REQUISITION.EDIT", "Edit Purchase Requisition", "Procurement", "Allows editing purchase requisitions.");
        await SeedPermissionAsync(repository, "PURCHASE_REQUISITION.SUBMIT", "Submit Purchase Requisition", "Procurement", "Allows submitting purchase requisitions.");
        await SeedPermissionAsync(repository, "PURCHASE_REQUISITION.APPROVE", "Approve/Reject Purchase Requisition", "Procurement", "Allows approving or rejecting purchase requisitions.");
        await SeedPermissionAsync(repository, "PURCHASE_REQUISITION.CANCEL", "Cancel Purchase Requisition", "Procurement", "Allows cancelling purchase requisitions.");

        await SeedPermissionAsync(repository, "PROCUREMENT.SETTINGS.VIEW", "View Procurement Approval Settings", "Procurement", "Allows viewing procurement approval settings.");
        await SeedPermissionAsync(repository, "PROCUREMENT.SETTINGS.EDIT", "Edit Procurement Approval Settings", "Procurement", "Allows configuring procurement approval settings.");

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
