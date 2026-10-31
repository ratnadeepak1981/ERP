using System;
using System.Threading.Tasks;
using Security.Core.Models;
using Security.Interfaces;

namespace Security.Bootstrap.Permissions.Domain;

public static class PriceCardPermissionSeed
{
    public static async Task SeedAsync(ISecurityBootstrapRepository repository)
    {
        await SeedPermissionAsync(repository, "PRICE_CARD.VIEW", "View Price Cards", "PriceCard", "Allows viewing shared price cards.");
        await SeedPermissionAsync(repository, "PRICE_CARD.CREATE", "Create Price Card", "PriceCard", "Allows creating shared price cards.");
        await SeedPermissionAsync(repository, "PRICE_CARD.EDIT", "Edit Price Card", "PriceCard", "Allows editing shared price cards and lines.");
        await SeedPermissionAsync(repository, "PRICE_CARD.MANAGE", "Manage Price Cards", "PriceCard", "Allows managing, deactivating and reactivating price cards.");
        await SeedPermissionAsync(repository, "PRICE_CARD.OVERRIDE", "Override Transaction Price Card", "PriceCard", "Allows overriding default price card at transaction level.");
        await SeedPermissionAsync(repository, "SUPPLIER.PRICE_CARD.ASSIGN", "Assign Supplier Price Card", "Supplier", "Allows assigning default purchase price card to suppliers.");
        await SeedPermissionAsync(repository, "CUSTOMER.PRICE_CARD.ASSIGN", "Assign Customer Price Card", "Customer", "Allows assigning default sales price card to customers.");
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
