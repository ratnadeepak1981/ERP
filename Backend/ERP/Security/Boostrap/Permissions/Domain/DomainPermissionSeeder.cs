
using Security.Interfaces;
using ERP.Domain.Bootstrap.Permissions.MasterData;

namespace ERP.Domain.Bootstrap.Permissions;

public static class DomainPermissionSeeder
{
    public static async Task SeedAsync(
        ISecurityBootstrapRepository repository)
    {
        await CompanyBranchPermissionSeed.SeedAsync(repository);

        // Add the next Domain permission seeds here
        // await ProductCategoryPermissionSeed.SeedAsync(repository);
        // await CustomerPermissionSeed.SeedAsync(repository);
        // await SupplierPermissionSeed.SeedAsync(repository);
        // await WarehousePermissionSeed.SeedAsync(repository);
        // await PurchasePermissionSeed.SeedAsync(repository);
        // await SalesPermissionSeed.SeedAsync(repository);
    }
}
