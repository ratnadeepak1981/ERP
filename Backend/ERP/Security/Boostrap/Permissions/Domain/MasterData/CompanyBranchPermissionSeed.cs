using Security.Core.Models;
using Security.Interfaces;

namespace ERP.Domain.Bootstrap.Permissions.MasterData;

public static class CompanyBranchPermissionSeed
{
    public static async Task SeedAsync(
        ISecurityBootstrapRepository repository)
    {
        await SeedPermissionAsync(
            repository,
            "COMPANY.VIEW",
            "View Company",
            "Company",
            "Allows viewing companies.");

        await SeedPermissionAsync(
            repository,
            "COMPANY.CREATE",
            "Create Company",
            "Company",
            "Allows creating companies.");

        await SeedPermissionAsync(
            repository,
            "COMPANY.EDIT",
            "Edit Company",
            "Company",
            "Allows editing companies.");

        await SeedPermissionAsync(
            repository,
            "COMPANY.DELETE",
            "Delete Company",
            "Company",
            "Allows deleting companies.");

        await SeedPermissionAsync(
            repository,
            "COMPANY.SOFT_DELETE",
            "Soft Delete Company",
            "Company",
            "Allows soft deleting companies.");

        await SeedPermissionAsync(
            repository,
            "BRANCH.VIEW",
            "View Branch",
            "Branch",
            "Allows viewing branches.");

        await SeedPermissionAsync(
            repository,
            "BRANCH.CREATE",
            "Create Branch",
            "Branch",
            "Allows creating branches.");

        await SeedPermissionAsync(
            repository,
            "BRANCH.EDIT",
            "Edit Branch",
            "Branch",
            "Allows editing branches.");

        await SeedPermissionAsync(
            repository,
            "BRANCH.DELETE",
            "Delete Branch",
            "Branch",
            "Allows deleting branches.");

        await SeedPermissionAsync(
            repository,
            "BRANCH.SOFT_DELETE",
            "Soft Delete Branch",
            "Branch",
            "Allows soft deleting branches.");

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

