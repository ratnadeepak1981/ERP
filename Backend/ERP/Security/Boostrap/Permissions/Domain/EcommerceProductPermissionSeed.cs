using System;
using System.Threading.Tasks;
using Security.Core.Models;
using Security.Interfaces;

namespace Security.Bootstrap.Permissions.Domain;

public static class EcommerceProductPermissionSeed
{
    public static async Task SeedAsync(
        ISecurityBootstrapRepository repository)
    {
        await SeedPermissionAsync(
            repository,
            "ECOMMERCE_PRODUCT.VIEW",
            "View E-commerce Publications",
            "Ecommerce",
            "Allows viewing company e-commerce product publications.");

        await SeedPermissionAsync(
            repository,
            "ECOMMERCE_PRODUCT.CREATE",
            "Create E-commerce Publication",
            "Ecommerce",
            "Allows selecting an ERP product to create an e-commerce publication.");

        await SeedPermissionAsync(
            repository,
            "ECOMMERCE_PRODUCT.EDIT",
            "Edit E-commerce Publication",
            "Ecommerce",
            "Allows updating online description and display order for an e-commerce publication.");

        await SeedPermissionAsync(
            repository,
            "ECOMMERCE_PRODUCT.PUBLISH",
            "Publish E-commerce Product",
            "Ecommerce",
            "Allows publishing an eligible ERP product to the public e-commerce catalogue.");

        await SeedPermissionAsync(
            repository,
            "ECOMMERCE_PRODUCT.UNPUBLISH",
            "Unpublish E-commerce Product",
            "Ecommerce",
            "Allows unpublishing a product to remove it from public visibility.");

        await SeedPermissionAsync(
            repository,
            "ECOMMERCE_PRODUCT.DELETE",
            "Delete E-commerce Publication",
            "Ecommerce",
            "Allows deleting an e-commerce publication record.");

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
