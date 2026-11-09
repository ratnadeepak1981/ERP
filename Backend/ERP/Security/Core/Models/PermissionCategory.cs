namespace Security.Core.Models;

/// <summary>
/// High-level ownership of a permission. Platform permissions belong to
/// ApexERP platform administration and must never be assignable by tenant
/// administrators. Tenant permissions belong to tenant business modules.
/// </summary>
public static class PermissionCategory
{
    public const string Platform = "Platform";

    public const string Tenant = "Tenant";

    public static bool IsPlatform(string? category)
    {
        return string.Equals(category, Platform, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsTenant(string? category)
    {
        return string.Equals(category, Tenant, StringComparison.OrdinalIgnoreCase);
    }
}
