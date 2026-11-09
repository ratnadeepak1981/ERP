namespace Security.Bootstrap.Roles;

/// <summary>
/// Outcome of provisioning the standard role templates for a tenant.
/// </summary>
public sealed record TenantRoleProvisioningResult(
    Guid TenantId,
    IReadOnlyList<string> CreatedTemplateKeys,
    IReadOnlyList<string> UpdatedTemplateKeys,
    IReadOnlyList<string> SkippedTemplateKeys,
    IReadOnlyList<string> Conflicts)
{
    public bool HasChanges =>
        CreatedTemplateKeys.Count > 0 || UpdatedTemplateKeys.Count > 0;
}
