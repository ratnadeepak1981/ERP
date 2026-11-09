using Security.Core.Models;
using Security.Interfaces;

namespace Security.Bootstrap.Permissions;

/// <summary>
/// Upserts the central permission catalogue into the Security database.
/// This seeder is idempotent and is the authoritative bootstrap for permission
/// definitions. Existing rows are never deleted and their display names are
/// preserved; missing metadata (category, operation, module) is backfilled.
/// </summary>
public static class PermissionCatalogueSeeder
{
    public static async Task SeedAsync(
        ISecurityBootstrapRepository repository)
    {
        var existing = (await repository.GetPermissionsAsync())
            .ToDictionary(x => x.Code, StringComparer.OrdinalIgnoreCase);

        var hasChanges = false;

        foreach (var definition in PermissionCatalogue.All)
        {
            if (!existing.TryGetValue(definition.Code, out var permission))
            {
                await repository.AddPermissionAsync(new Permission
                {
                    Id = Guid.NewGuid(),
                    Code = definition.Code,
                    Name = definition.Name,
                    Module = definition.Module,
                    Description = definition.Description,
                    Category = definition.Category,
                    Operation = definition.Operation,
                    IsActive = true
                });

                hasChanges = true;
                continue;
            }

            if (!string.Equals(permission.Category, definition.Category, StringComparison.Ordinal))
            {
                permission.Category = definition.Category;
                hasChanges = true;
            }

            if (string.IsNullOrWhiteSpace(permission.Operation))
            {
                permission.Operation = definition.Operation;
                hasChanges = true;
            }

            if (string.IsNullOrWhiteSpace(permission.Module))
            {
                permission.Module = definition.Module;
                hasChanges = true;
            }

            if (!permission.IsActive)
            {
                permission.IsActive = true;
                hasChanges = true;
            }
        }

        if (hasChanges)
        {
            await repository.SaveChangesAsync();
        }
    }
}
