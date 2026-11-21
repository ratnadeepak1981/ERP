using System;
using System.Linq;
using Domain.Common.ApplicationUrl;
using Microsoft.AspNetCore.Http;

namespace API.Services;

/// <summary>
/// Ambient HTTP request implementation of <see cref="IHttpRequestOriginAccessor"/>.
/// Extracts the browser-facing origin by prioritizing the browser's Origin header, Referer authority,
/// and reverse-proxy forwarded headers (X-Forwarded-Proto, X-Forwarded-Host, X-Forwarded-Port).
/// </summary>
public class HttpRequestOriginAccessor : IHttpRequestOriginAccessor
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpRequestOriginAccessor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
    }

    public string? GetRequestOrigin()
    {
        var context = _httpContextAccessor.HttpContext;
        if (context == null)
        {
            return null;
        }

        var request = context.Request;

        // 1. Direct browser Origin header (sent by browsers on POST / CORS requests)
        if (request.Headers.TryGetValue("Origin", out var originValues))
        {
            var origin = originValues.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
            if (!string.IsNullOrWhiteSpace(origin))
            {
                return origin.Trim();
            }
        }

        // 2. Referer header authority
        if (request.Headers.TryGetValue("Referer", out var refererValues))
        {
            var referer = refererValues.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
            if (!string.IsNullOrWhiteSpace(referer) && Uri.TryCreate(referer.Trim(), UriKind.Absolute, out var refererUri))
            {
                return $"{refererUri.Scheme}://{refererUri.Authority}";
            }
        }

        // 3. Reverse-proxy forwarded headers (X-Forwarded-Proto, X-Forwarded-Host, X-Forwarded-Port)
        string scheme = request.Scheme;
        if (request.Headers.TryGetValue("X-Forwarded-Proto", out var protoValues))
        {
            var proto = protoValues.ToString().Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(proto))
            {
                scheme = proto.Trim().ToLowerInvariant();
            }
        }

        string? host = null;
        if (request.Headers.TryGetValue("X-Forwarded-Host", out var forwardedHostValues))
        {
            var fHost = forwardedHostValues.ToString().Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(fHost))
            {
                host = fHost.Trim();
            }
        }

        if (string.IsNullOrWhiteSpace(host) && request.Host.HasValue)
        {
            host = request.Host.Value;
        }

        if (!string.IsNullOrWhiteSpace(host))
        {
            // If host doesn't include port, but X-Forwarded-Port is explicitly provided
            if (!host.Contains(':') && request.Headers.TryGetValue("X-Forwarded-Port", out var portValues))
            {
                var portStr = portValues.ToString().Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                if (int.TryParse(portStr, out var port))
                {
                    bool isDefaultPort = (scheme == "http" && port == 80) || (scheme == "https" && port == 443);
                    if (!isDefaultPort)
                    {
                        host = $"{host}:{port}";
                    }
                }
            }

            return $"{scheme}://{host}";
        }

        return null;
    }
}
