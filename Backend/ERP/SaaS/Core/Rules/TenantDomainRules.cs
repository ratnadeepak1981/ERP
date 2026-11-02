using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace SaaS.Core.Rules;

public static class TenantDomainRules
{
    private static readonly Regex SubdomainRegex = new(
        @"^[a-z0-9]([a-z0-9-]{1,61}[a-z0-9])?$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly HashSet<string> ReservedSubdomains = new(StringComparer.OrdinalIgnoreCase)
    {
        "admin",
        "api",
        "platform",
        "app",
        "www",
        "mail",
        "hangfire",
        "swagger",
        "auth",
        "support",
        "billing",
        "login",
        "root",
        "dev",
        "test"
    };

    public static string NormalizeSubdomain(string subdomain)
    {
        return subdomain?.Trim().ToLowerInvariant() ?? string.Empty;
    }

    public static string NormalizeHostname(string hostname)
    {
        if (string.IsNullOrWhiteSpace(hostname))
        {
            return string.Empty;
        }

        return hostname.Trim().TrimEnd('.').ToLowerInvariant();
    }

    public static bool IsValidSubdomain(string? subdomain)
    {
        if (string.IsNullOrWhiteSpace(subdomain))
        {
            return false;
        }

        string normalized = NormalizeSubdomain(subdomain);

        if (normalized.Length < 3 || normalized.Length > 63)
        {
            return false;
        }

        if (!SubdomainRegex.IsMatch(normalized))
        {
            return false;
        }

        if (IsReservedSubdomain(normalized))
        {
            return false;
        }

        return true;
    }

    public static bool IsReservedSubdomain(string? subdomain)
    {
        if (string.IsNullOrWhiteSpace(subdomain))
        {
            return false;
        }

        string normalized = NormalizeSubdomain(subdomain);
        return ReservedSubdomains.Contains(normalized);
    }

    public static string BuildLocalhostHostname(string subdomain)
    {
        if (!IsValidSubdomain(subdomain))
        {
            throw new ArgumentException(
                $"Subdomain '{subdomain}' is invalid or reserved.",
                nameof(subdomain));
        }

        return $"{NormalizeSubdomain(subdomain)}.localhost";
    }

    public static bool IsValidHostname(string? hostname)
    {
        if (string.IsNullOrWhiteSpace(hostname))
        {
            return false;
        }

        string normalized = NormalizeHostname(hostname);
        if (normalized.Length > 255)
        {
            return false;
        }

        return Uri.CheckHostName(normalized) != UriHostNameType.Unknown;
    }
}
