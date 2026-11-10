using System;
using System.Net;

namespace SaaS.Core.Rules;

/// <summary>
/// Authoritative domain helper for parsing and extracting tenant subdomain slugs
/// across localhost development, LAN IP wildcard domains (.nip.io / .sslip.io), and production hosting.
/// </summary>
public static class TenantHostResolver
{
    /// <summary>
    /// Extracts the tenant subdomain from an incoming HTTP host or forwarded host string.
    /// Returns null if the host corresponds to the root platform host, a bare IP address, or localhost root.
    /// </summary>
    /// <param name="hostHeader">Raw Host or X-Forwarded-Host value (e.g., 'retail.localhost:4200').</param>
    /// <param name="configuredBaseDomain">Configured base domain (e.g., 'localhost', 'apexerp.com').</param>
    /// <returns>Extracted tenant slug in lowercase (e.g., 'retail'), or null for platform root.</returns>
    public static string? ExtractSubdomain(string? hostHeader, string? configuredBaseDomain = null)
    {
        if (string.IsNullOrWhiteSpace(hostHeader))
        {
            return null;
        }

        // 1. Strip comma (if multiple proxies in X-Forwarded-Host) and port (e.g. 'retail.localhost:4200' -> 'retail.localhost')
        string host = hostHeader.Split(',')[0].Split(':')[0].Trim().ToLowerInvariant();

        // 2. Reject bare IP address or bare 'localhost' / '127.0.0.1' (Platform mode)
        if (host == "localhost" || host == "127.0.0.1" || IPAddress.TryParse(host, out _))
        {
            return null;
        }

        // 3. Check against explicit configured base domain if supplied
        if (!string.IsNullOrWhiteSpace(configuredBaseDomain))
        {
            string baseDomain = configuredBaseDomain.Split(':')[0].Trim().ToLowerInvariant();

            if (host.Equals(baseDomain, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (host.EndsWith("." + baseDomain, StringComparison.OrdinalIgnoreCase))
            {
                string prefix = host[..^(baseDomain.Length + 1)];
                return CleanSubdomainLabel(prefix);
            }
        }

        // 4. Wildcard LAN IP domain support (.nip.io / .sslip.io)
        // Format: <subdomain>.<ip-address>.nip.io (e.g., retail.192.168.1.5.nip.io)
        if (host.EndsWith(".nip.io", StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith(".sslip.io", StringComparison.OrdinalIgnoreCase))
        {
            string[] parts = host.Split('.');
            // Expected parts: [<subdomain>, <ip1>, <ip2>, <ip3>, <ip4>, "nip", "io"] -> length >= 7
            if (parts.Length >= 7)
            {
                // Verify the 4 segments before "nip" are numbers (valid IP)
                int ipStartIndex = parts.Length - 6;
                bool isIp = byte.TryParse(parts[ipStartIndex], out _) &&
                            byte.TryParse(parts[ipStartIndex + 1], out _) &&
                            byte.TryParse(parts[ipStartIndex + 2], out _) &&
                            byte.TryParse(parts[ipStartIndex + 3], out _);

                if (isIp && ipStartIndex > 0)
                {
                    // Subdomain is everything before the IP
                    return CleanSubdomainLabel(string.Join(".", parts[..ipStartIndex]));
                }
            }

            // Bare IP domain: e.g. 192.168.1.5.nip.io (Platform mode)
            return null;
        }

        // 5. Default localhost subdomain support (e.g., retail.localhost)
        if (host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase))
        {
            string prefix = host[..^".localhost".Length];
            return CleanSubdomainLabel(prefix);
        }

        return null;
    }

    private static string CleanSubdomainLabel(string prefix)
    {
        // If there are multiple dots (e.g. staging.retail), take the first or primary slug
        string[] labels = prefix.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (labels.Length == 0)
        {
            return prefix;
        }

        // The first label is the tenant slug (e.g., 'retail' in 'retail' or 'retail.app')
        return labels[0];
    }
}
