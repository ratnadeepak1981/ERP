using System.Text;
using System.Text.RegularExpressions;

namespace SaaS.Core.Rules;

/// <summary>
/// Authoritative domain rules for tenant subdomain slug generation, validation, and collision resolution.
/// RFC 1035 / DNS-compliant tenant slug format: lowercase letters, numbers, and single hyphens (3-63 characters).
/// </summary>
public static class TenantSlugRules
{
    public const int MinLength = 3;
    public const int MaxLength = 63;

    // Allowed: 3-63 chars, lowercase alphanumeric and hyphens, no leading/trailing hyphen, no consecutive hyphens
    private static readonly Regex SlugRegex =
        new(@"^[a-z0-9](?:[a-z0-9-]{1,61}[a-z0-9])?$", RegexOptions.Compiled);

    private static readonly Regex NonAlphanumericRegex =
        new(@"[^a-z0-9]+", RegexOptions.Compiled);

    private static readonly Regex ConsecutiveHyphensRegex =
        new(@"-{2,}", RegexOptions.Compiled);

    /// <summary>
    /// Reserved subdomain names that cannot be claimed by any tenant.
    /// </summary>
    public static readonly HashSet<string> ReservedSubdomains = new(StringComparer.OrdinalIgnoreCase)
    {
        "admin",
        "api",
        "app",
        "assets",
        "auth",
        "autodiscover",
        "billing",
        "cdn",
        "dashboard",
        "dev",
        "docs",
        "erp",
        "ftp",
        "graphql",
        "health",
        "help",
        "identity",
        "imap",
        "internal",
        "login",
        "mail",
        "mfa",
        "monitoring",
        "oauth",
        "platform",
        "pop",
        "portal",
        "preview",
        "prod",
        "register",
        "root",
        "saas",
        "security",
        "smtp",
        "staging",
        "static",
        "status",
        "storefront",
        "support",
        "system",
        "test",
        "webhook",
        "ws",
        "www"
    };

    /// <summary>
    /// Generates a standardized, DNS-compliant tenant subdomain slug from a tenant name.
    /// </summary>
    public static string GenerateSlug(string tenantName)
    {
        if (string.IsNullOrWhiteSpace(tenantName))
        {
            throw new ArgumentException("Tenant name cannot be empty when generating subdomain.", nameof(tenantName));
        }

        // 1. Lowercase & trim
        string cleaned = tenantName.Trim().ToLowerInvariant();

        // 2. Replace any non-alphanumeric character sequences with a single hyphen
        cleaned = NonAlphanumericRegex.Replace(cleaned, "-");

        // 3. Collapse consecutive hyphens
        cleaned = ConsecutiveHyphensRegex.Replace(cleaned, "-");

        // 4. Trim leading and trailing hyphens
        cleaned = cleaned.Trim('-');

        // 5. Length verification
        if (cleaned.Length < MinLength)
        {
            throw new ArgumentException(
                $"Tenant name '{tenantName}' must yield a valid subdomain slug of at least {MinLength} alphanumeric characters.",
                nameof(tenantName));
        }

        if (cleaned.Length > MaxLength)
        {
            cleaned = cleaned[..MaxLength].TrimEnd('-');
        }

        return cleaned;
    }

    /// <summary>
    /// Validates whether a slug matches all structural DNS label constraints.
    /// </summary>
    public static bool IsValidSlug(string? slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return false;
        }

        if (slug.Length < MinLength || slug.Length > MaxLength)
        {
            return false;
        }

        if (slug.StartsWith('-') || slug.EndsWith('-'))
        {
            return false;
        }

        if (slug.Contains("--"))
        {
            return false;
        }

        return SlugRegex.IsMatch(slug);
    }

    /// <summary>
    /// Checks whether the slug is a reserved system name.
    /// </summary>
    public static bool IsReserved(string? slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return false;
        }

        return ReservedSubdomains.Contains(slug.Trim());
    }

    /// <summary>
    /// Validates a slug against format rules and the reserved names blacklist.
    /// Throws descriptive ArgumentException on failure.
    /// </summary>
    public static void ValidateSubdomain(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            throw new ArgumentException("Subdomain slug cannot be empty.", nameof(slug));
        }

        if (slug.Length < MinLength || slug.Length > MaxLength)
        {
            throw new ArgumentException($"Subdomain length must be between {MinLength} and {MaxLength} characters.", nameof(slug));
        }

        if (slug.StartsWith('-') || slug.EndsWith('-'))
        {
            throw new ArgumentException("Subdomain cannot start or end with a hyphen.", nameof(slug));
        }

        if (slug.Contains("--"))
        {
            throw new ArgumentException("Subdomain cannot contain consecutive hyphens.", nameof(slug));
        }

        if (!SlugRegex.IsMatch(slug))
        {
            throw new ArgumentException("Subdomain can only contain lowercase letters, numbers, and hyphens.", nameof(slug));
        }

        if (IsReserved(slug))
        {
            throw new ArgumentException($"The subdomain '{slug}' is reserved for system use and cannot be assigned to a tenant.", nameof(slug));
        }
    }

    /// <summary>
    /// Safely resolves subdomain collisions by appending an incremental numeric suffix (e.g., abc-retail-2).
    /// Guarantees that the suffixed slug never exceeds MaxLength (63 characters).
    /// </summary>
    public static async Task<string> ResolveCollisionAsync(string baseSlug, Func<string, Task<bool>> existsAsync)
    {
        ValidateSubdomain(baseSlug);

        if (!await existsAsync(baseSlug))
        {
            return baseSlug;
        }

        int suffix = 2;
        while (suffix < 10000)
        {
            string suffixStr = $"-{suffix}";
            int allowedBaseLength = MaxLength - suffixStr.Length;
            string truncatedBase = baseSlug.Length > allowedBaseLength
                ? baseSlug[..allowedBaseLength].TrimEnd('-')
                : baseSlug;

            string candidate = $"{truncatedBase}{suffixStr}";

            if (!await existsAsync(candidate))
            {
                return candidate;
            }

            suffix++;
        }

        throw new InvalidOperationException($"Unable to resolve unique subdomain slug for '{baseSlug}'.");
    }
}
