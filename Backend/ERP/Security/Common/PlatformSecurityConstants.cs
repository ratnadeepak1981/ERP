using System;
using System.Collections.Generic;
using Security.Core.Models;

namespace Security.Common;

public static class PlatformSecurityConstants
{
    public static readonly HashSet<string> PlatformRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "Platform Admin",
        "Platform Staff",
        "Security Manager",
        "Audit",
        "Executive",
        "Subscription Admin"
    };

    public static readonly HashSet<string> SystemTenantRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "Tenant Admin",
        "Tenant User",
        "Branch User",
        "Viewer"
    };

    public static bool IsPlatformRole(string? roleName)
    {
        return !string.IsNullOrWhiteSpace(roleName) && PlatformRoles.Contains(roleName);
    }

    public static bool IsPlatformOnlyPermission(string? module, string? code)
    {
        return IsPlatformOnlyPermission(null, module, code);
    }

    public static bool IsPlatformOnlyPermission(
        string? category,
        string? module,
        string? code)
    {
        // Category is the authoritative marker. Fall back to module and code
        // prefixes for legacy rows that predate categorisation.
        if (PermissionCategory.IsPlatform(category))
        {
            return true;
        }

        if (string.Equals(module, "Tenant", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(module, "Subscription", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(module, "AI", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!string.IsNullOrEmpty(code) &&
            (code.StartsWith("TENANT.", StringComparison.OrdinalIgnoreCase) ||
             code.StartsWith("SUBSCRIPTION.", StringComparison.OrdinalIgnoreCase) ||
             code.StartsWith("AI.", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return false;
    }
}
