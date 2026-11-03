using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Microsoft.AspNetCore.Http;

namespace API.Common.TenantResolution;

public static class TrustedHostResolver
{
    private static readonly HashSet<string> LegacySharedHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "localhost",
        "127.0.0.1",
        "::1"
    };

    public static string ExtractHost(HttpContext context)
    {
        var remoteIp = context.Connection.RemoteIpAddress;
        bool isLoopback = remoteIp != null && IPAddress.IsLoopback(remoteIp);

        // Accept X-Forwarded-Host only from trusted loopback proxy (e.g., local Angular dev proxy)
        if (isLoopback && context.Request.Headers.TryGetValue("X-Forwarded-Host", out var forwardedHostValues))
        {
            var firstForwarded = forwardedHostValues.ToString()
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(firstForwarded))
            {
                return StripPort(firstForwarded);
            }
        }

        return context.Request.Host.Host;
    }

    public static bool IsLegacySharedHost(string host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return true;
        }

        string unbracketed = host.Trim().Trim('[', ']');

        if (LegacySharedHosts.Contains(unbracketed))
        {
            return true;
        }

        if (IPAddress.TryParse(unbracketed, out _))
        {
            return true;
        }

        return false;
    }

    private static string StripPort(string host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return string.Empty;
        }

        string trimmed = host.Trim();

        // Bracketed IPv6: "[::1]:5000" or "[::1]"
        if (trimmed.StartsWith('['))
        {
            int closingBracket = trimmed.IndexOf(']');
            if (closingBracket > 0)
            {
                return trimmed[1..closingBracket];
            }
        }

        // Standard host:port with exactly one colon
        int firstColon = trimmed.IndexOf(':');
        int lastColon = trimmed.LastIndexOf(':');
        if (firstColon > 0 && firstColon == lastColon)
        {
            return trimmed[..firstColon];
        }

        return trimmed;
    }
}
