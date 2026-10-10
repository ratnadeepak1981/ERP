
using Security.Interfaces;
using ERP.Domain.Bootstrap.Permissions.MasterData;

namespace ERP.Domain.Bootstrap.Permissions;

public static class DomainPermissionSeeder
{
    public static async Task SeedAsync(
        ISecurityBootstrapRepository repository)
    {
        await CompanyBranchPermissionSeed.SeedAsync(repository);
        await Security.Bootstrap.Permissions.Domain.CategoryPermissionSeed.SeedAsync(repository);
        await Security.Bootstrap.Permissions.Domain.UnitOfMeasurePermissionSeed.SeedAsync(repository);
        await Security.Bootstrap.Permissions.Domain.WarehousePermissionSeed.SeedAsync(repository);
        await Security.Bootstrap.Permissions.Domain.LocationPermissionSeed.SeedAsync(repository);
        await Security.Bootstrap.Permissions.Domain.AddressPermissionSeed.SeedAsync(repository);
        await Security.Bootstrap.Permissions.Domain.ContactPermissionSeed.SeedAsync(repository);
        await Security.Bootstrap.Permissions.Domain.SupplierProductPricePermissionSeed.SeedAsync(repository);
        await Security.Bootstrap.Permissions.Domain.CountryPermissionSeed.SeedAsync(repository);
    }
}
