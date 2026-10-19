using Security.Core.Models;
using Security.Interfaces;

namespace Security.Bootstrap.Permissions.Domain;

public static class CrmPermissionSeed
{
    public static async Task SeedAsync(ISecurityBootstrapRepository repository)
    {
        await SeedPermissionAsync(
            repository,
            "CRM.LEAD.VIEW",
            "View CRM Leads",
            "CRM",
            "Allows viewing CRM leads.");

        await SeedPermissionAsync(
            repository,
            "CRM.LEAD.CREATE",
            "Create CRM Lead",
            "CRM",
            "Allows creating new CRM leads.");

        await SeedPermissionAsync(
            repository,
            "CRM.LEAD.EDIT",
            "Edit CRM Lead",
            "CRM",
            "Allows editing existing CRM leads.");

        await SeedPermissionAsync(
            repository,
            "CRM.LEAD.QUALIFY",
            "Qualify/Disqualify CRM Lead",
            "CRM",
            "Allows qualifying or disqualifying CRM leads.");

        await SeedPermissionAsync(
            repository,
            "CRM.LEAD.CONVERT",
            "Convert CRM Lead",
            "CRM",
            "Allows converting qualified CRM leads to customers.");

        await SeedPermissionAsync(
            repository,
            "CRM.OPPORTUNITY.VIEW",
            "View CRM Opportunities",
            "CRM",
            "Allows viewing CRM opportunities.");

        await SeedPermissionAsync(
            repository,
            "CRM.OPPORTUNITY.MANAGE",
            "Manage CRM Opportunities",
            "CRM",
            "Allows creating, editing, and advancing CRM opportunities.");

        await SeedPermissionAsync(
            repository,
            "CRM.ACTIVITY.VIEW",
            "View CRM Activities",
            "CRM",
            "Allows viewing CRM activities.");

        await SeedPermissionAsync(
            repository,
            "CRM.ACTIVITY.MANAGE",
            "Manage CRM Activities",
            "CRM",
            "Allows creating, updating, and completing CRM activities.");

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
