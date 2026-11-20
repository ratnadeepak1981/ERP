using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Domain.Common.ApplicationUrl;

/// <summary>
/// Thread-safe, reusable implementation of <see cref="IApplicationUrlService"/>.
/// Dynamically resolves browser-facing origins (preserving scheme, host, subdomain, and non-default port)
/// and generates safe, absolute application URLs with strict security validation.
/// </summary>
public class ApplicationUrlService : IApplicationUrlService
{
    private readonly ApplicationUrlOptions _options;
    private readonly IHttpRequestOriginAccessor? _httpOriginAccessor;
    private readonly ILogger<ApplicationUrlService>? _logger;

    private static readonly HashSet<string> LocalhostHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "localhost",
        "127.0.0.1",
        "::1"
    };

    public ApplicationUrlService(
        IOptions<ApplicationUrlOptions>? options = null,
        IHttpRequestOriginAccessor? httpOriginAccessor = null,
        ILogger<ApplicationUrlService>? logger = null)
    {
        _options = options?.Value ?? new ApplicationUrlOptions();
        _httpOriginAccessor = httpOriginAccessor;
        _logger = logger;
    }

    public string ResolveBaseUrl(string? clientOrigin = null)
    {
        // 1. Preferred source: Validated browser client origin (e.g. window.location.origin)
        if (!string.IsNullOrWhiteSpace(clientOrigin))
        {
            if (IsValidOrigin(clientOrigin, out var validatedClientOrigin, out var failureReason))
            {
                return validatedClientOrigin!;
            }

            _logger?.LogWarning("Client-supplied origin '{Origin}' was rejected: {Reason}. Falling back to request or configured base URL.",
                clientOrigin, failureReason);
        }

        // 2. Secondary source: Ambient HTTP request (Origin, Referer, or Forwarded headers)
        if (_httpOriginAccessor != null)
        {
            var requestOrigin = _httpOriginAccessor.GetRequestOrigin();
            if (!string.IsNullOrWhiteSpace(requestOrigin) &&
                IsValidOrigin(requestOrigin, out var validatedRequestOrigin, out _))
            {
                return validatedRequestOrigin!;
            }
        }

        // 3. Fallback: Configured fallback base URL
        if (!string.IsNullOrWhiteSpace(_options.FallbackBaseUrl) &&
            IsValidOrigin(_options.FallbackBaseUrl, out var validatedFallback, out _))
        {
            return validatedFallback!;
        }

        return "http://localhost:4200";
    }

    public string BuildAbsoluteUrl(string relativePath, IDictionary<string, string?>? queryParams = null, string? clientOrigin = null)
    {
        var baseUrl = ResolveBaseUrl(clientOrigin);

        var path = relativePath ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(path) && !path.StartsWith('/'))
        {
            path = "/" + path;
        }

        if (queryParams == null || queryParams.Count == 0)
        {
            return $"{baseUrl}{path}";
        }

        var sb = new StringBuilder();
        sb.Append(baseUrl);
        sb.Append(path);

        bool hasExistingQuery = path.Contains('?');
        bool isFirstParam = !hasExistingQuery;

        foreach (var kvp in queryParams)
        {
            if (string.IsNullOrWhiteSpace(kvp.Key)) continue;

            sb.Append(isFirstParam ? '?' : '&');
            isFirstParam = false;

            sb.Append(Uri.EscapeDataString(kvp.Key));
            sb.Append('=');
            if (!string.IsNullOrEmpty(kvp.Value))
            {
                sb.Append(Uri.EscapeDataString(kvp.Value));
            }
        }

        return sb.ToString();
    }

    public bool IsValidOrigin(string? origin, out string? validatedOrigin, out string? failureReason)
    {
        validatedOrigin = null;

        if (string.IsNullOrWhiteSpace(origin))
        {
            failureReason = "Origin cannot be null or empty.";
            return false;
        }

        string trimmed = origin.Trim();

        if (trimmed.Length > 2048)
        {
            failureReason = "Origin exceeds maximum permitted length of 2048 characters.";
            return false;
        }

        // Check for control characters or header injection tokens (CR, LF, null byte, tab)
        if (trimmed.Any(c => c is '\r' or '\n' or '\0' or '\t'))
        {
            failureReason = "Origin contains invalid control characters or line breaks (injection attempt detected).";
            return false;
        }

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            failureReason = "Origin is not a valid absolute URI.";
            return false;
        }

        // Scheme validation: Must be HTTP or HTTPS
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            failureReason = $"Unsupported scheme '{uri.Scheme}'. Only HTTP and HTTPS are permitted.";
            return false;
        }

        // Path validation: Origins must not contain subpaths
        if (uri.AbsolutePath != "/" && !string.IsNullOrEmpty(uri.AbsolutePath))
        {
            failureReason = $"Origin must not contain path components ('{uri.AbsolutePath}'). Subpaths belong in workflow route paths.";
            return false;
        }

        // Query and fragment validation
        if (!string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
        {
            failureReason = "Origin must not contain query strings or URL fragments.";
            return false;
        }

        string host = uri.Host.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(host))
        {
            failureReason = "Origin host is missing.";
            return false;
        }

        if (uri.Port is < 1 or > 65535)
        {
            failureReason = $"Origin port '{uri.Port}' is outside the valid TCP port range (1-65535).";
            return false;
        }

        // Validate host against trusted origin rules
        if (!IsHostTrusted(host, uri))
        {
            failureReason = $"Host '{uri.Host}' is not recognized as a trusted development, LAN, or configured hosted domain.";
            return false;
        }

        failureReason = null;
        validatedOrigin = $"{uri.Scheme}://{uri.Host}{(uri.IsDefaultPort ? string.Empty : $":{uri.Port}")}";
        return true;
    }

    private bool IsHostTrusted(string host, Uri uri)
    {
        string unbracketed = host.Trim('[', ']');

        // 1. Localhost and Loopback addresses
        if (LocalhostHosts.Contains(unbracketed) || unbracketed.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // 2. IP Address verification
        if (IPAddress.TryParse(unbracketed, out var ip))
        {
            if (IPAddress.IsLoopback(ip))
            {
                return true;
            }

            if (_options.AllowLocalNetworkOrigins)
            {
                // IPv4 Private Networks (RFC 1918)
                if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                {
                    byte[] bytes = ip.GetAddressBytes();
                    // 10.0.0.0/8
                    if (bytes[0] == 10) return true;
                    // 172.16.0.0/12 (172.16.0.0 - 172.31.255.255)
                    if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) return true;
                    // 192.168.0.0/16
                    if (bytes[0] == 192 && bytes[1] == 168) return true;
                    // 169.254.0.0/16 (Link-local)
                    if (bytes[0] == 169 && bytes[1] == 254) return true;
                }
                // IPv6 Link-Local / Unique-Local
                else if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
                {
                    if (ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal) return true;
                    byte[] bytes = ip.GetAddressBytes();
                    // fc00::/7 (Unique local)
                    if ((bytes[0] & 0xfe) == 0xfc) return true;
                }
            }
        }

        // 3. Local Wildcard DNS services (e.g. nip.io, sslip.io)
        if (unbracketed.EndsWith(".nip.io", StringComparison.OrdinalIgnoreCase) ||
            unbracketed.EndsWith(".sslip.io", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // 4. LAN / Intranet Single-label hostnames (e.g. "erp-server", "appserver", or ".local"/".lan"/".internal")
        if (_options.AllowLocalNetworkOrigins)
        {
            if (!unbracketed.Contains('.') ||
                unbracketed.EndsWith(".local", StringComparison.OrdinalIgnoreCase) ||
                unbracketed.EndsWith(".lan", StringComparison.OrdinalIgnoreCase) ||
                unbracketed.EndsWith(".internal", StringComparison.OrdinalIgnoreCase) ||
                unbracketed.EndsWith(".corp", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        // 5. Configured TenantBaseDomain (e.g. "localhost", "apexerp.com", "example.com")
        string baseDomain = (_options.TenantBaseDomain ?? string.Empty).Trim().ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(baseDomain))
        {
            if (string.Equals(unbracketed, baseDomain, StringComparison.OrdinalIgnoreCase) ||
                unbracketed.EndsWith("." + baseDomain, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        // 6. Configured explicit AllowedOrigins or AllowedBaseDomains
        if (_options.AllowedOrigins != null && _options.AllowedOrigins.Count > 0)
        {
            foreach (var allowed in _options.AllowedOrigins)
            {
                if (string.IsNullOrWhiteSpace(allowed)) continue;
                string cleanAllowed = allowed.Trim().ToLowerInvariant();

                // If allowed contains scheme (full origin)
                if (cleanAllowed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    cleanAllowed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    if (Uri.TryCreate(cleanAllowed, UriKind.Absolute, out var allowedUri))
                    {
                        if (string.Equals(uri.Scheme, allowedUri.Scheme, StringComparison.OrdinalIgnoreCase) &&
                            (string.Equals(unbracketed, allowedUri.Host, StringComparison.OrdinalIgnoreCase) ||
                             unbracketed.EndsWith("." + allowedUri.Host, StringComparison.OrdinalIgnoreCase)))
                        {
                            // If allowedUri explicitly specifies a non-default port, require exact port match; otherwise allow any port
                            if (allowedUri.IsDefaultPort || uri.Port == allowedUri.Port)
                            {
                                return true;
                            }
                        }
                    }
                }
                // Host or base domain match
                else
                {
                    if (string.Equals(unbracketed, cleanAllowed, StringComparison.OrdinalIgnoreCase) ||
                        unbracketed.EndsWith("." + cleanAllowed, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }
}
