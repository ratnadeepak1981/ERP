using Security.Core.Models;
using Security.Interfaces;

namespace Security.Bootstrap.Permissions.Domain;

public static class RfqPermissionSeed
{
    public static async Task SeedAsync(
        ISecurityBootstrapRepository repository)
    {
        await SeedPermissionAsync(
            repository,
            "RFQ.VIEW",
            "View Request for Quotation",
            "Procurement",
            "Allows viewing RFQ registers, headers, and lines.");

        await SeedPermissionAsync(
            repository,
            "RFQ.CREATE",
            "Create Request for Quotation",
            "Procurement",
            "Allows creating RFQ drafts.");

        await SeedPermissionAsync(
            repository,
            "RFQ.EDIT",
            "Edit Request for Quotation",
            "Procurement",
            "Allows editing RFQ headers and lines.");

        await SeedPermissionAsync(
            repository,
            "RFQ.SUPPLIER.MANAGE",
            "Manage RFQ Suppliers",
            "Procurement",
            "Allows adding or removing suppliers from RFQs.");

        await SeedPermissionAsync(
            repository,
            "RFQ.SEND",
            "Send RFQ Invitations",
            "Procurement",
            "Allows sending official RFQ email invitations to suppliers.");

        await SeedPermissionAsync(
            repository,
            "RFQ.QUOTATION.RECORD",
            "Record Supplier Quotation",
            "Procurement",
            "Allows recording supplier quotation bids.");

        await SeedPermissionAsync(
            repository,
            "RFQ.QUOTATION.VIEW",
            "View Supplier Quotations",
            "Procurement",
            "Allows viewing submitted supplier quotations.");

        await SeedPermissionAsync(
            repository,
            "RFQ.COMPARE",
            "Compare Quotations",
            "Procurement",
            "Allows accessing and evaluating the side-by-side quotation comparison matrix.");

        await SeedPermissionAsync(
            repository,
            "RFQ.APPROVE",
            "Approve Quotation",
            "Procurement",
            "Allows approving/awarding supplier quotations.");

        await SeedPermissionAsync(
            repository,
            "RFQ.REJECT",
            "Reject Quotation",
            "Procurement",
            "Allows rejecting supplier quotations or cancelling RFQs.");

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
                Category = "Procurement",
                Operation = code.Split('.')[^1],
                IsActive = true
            });
    }
}
