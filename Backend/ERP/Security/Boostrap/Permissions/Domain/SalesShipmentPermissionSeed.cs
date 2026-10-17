using Security.Core.Models;
using Security.Interfaces;

namespace Security.Bootstrap.Permissions.Domain;

public static class SalesShipmentPermissionSeed
{
    public static async Task SeedAsync(
        ISecurityBootstrapRepository repository)
    {
        await SeedPermissionAsync(
            repository,
            "SALES_SHIPMENT.VIEW",
            "View Sales Shipment",
            "SalesShipment",
            "Allows viewing sales shipments and dispatch records.");

        await SeedPermissionAsync(
            repository,
            "SALES_SHIPMENT.CREATE",
            "Create Sales Shipment",
            "SalesShipment",
            "Allows creating shipments and dispatches against sales orders.");

        await SeedPermissionAsync(
            repository,
            "SALES_SHIPMENT.EDIT",
            "Edit Sales Shipment",
            "SalesShipment",
            "Allows editing draft shipments and logistics details.");

        await SeedPermissionAsync(
            repository,
            "SALES_SHIPMENT.DELETE",
            "Delete Sales Shipment",
            "SalesShipment",
            "Allows deleting draft sales shipments.");

        await SeedPermissionAsync(
            repository,
            "SALES_SHIPMENT.DISPATCH",
            "Dispatch Sales Shipment",
            "SalesShipment",
            "Allows executing shipment dispatch and posting inventory outflows.");

        await SeedPermissionAsync(
            repository,
            "SALES_SHIPMENT.DELIVER",
            "Deliver Sales Shipment",
            "SalesShipment",
            "Allows confirming delivery or customer pickup completion.");

        await SeedPermissionAsync(
            repository,
            "SALES_SHIPMENT.CANCEL",
            "Cancel Sales Shipment",
            "SalesShipment",
            "Allows cancelling draft sales shipments.");

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
