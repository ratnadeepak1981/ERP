using Security.Core.Models;
using Security.Interfaces;

namespace Security.Bootstrap.Permissions.Domain;

public static class ContactPermissionSeed
{
    public static async Task SeedAsync(ISecurityBootstrapRepository repository)
    {
        await SeedPermissionAsync(repository, "CONTACT.VIEW", "View Contact", "Contact", "Allows viewing contacts and contact types.");
        await SeedPermissionAsync(repository, "CONTACT.CREATE", "Create Contact", "Contact", "Allows creating contacts and contact types.");
        await SeedPermissionAsync(repository, "CONTACT.EDIT", "Edit Contact", "Contact", "Allows editing contacts and contact types.");
        await SeedPermissionAsync(repository, "CONTACT.DELETE", "Delete Contact", "Contact", "Allows deleting contacts and contact types.");
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
