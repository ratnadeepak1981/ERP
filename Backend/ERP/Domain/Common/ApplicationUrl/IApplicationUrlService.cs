using System.Collections.Generic;

namespace Domain.Common.ApplicationUrl;

/// <summary>
/// Reusable Application URL Service that dynamically resolves the browser-accessible ApexERP
/// application URL and generates absolute workflow links (e.g. for invitations, quotations, registration).
/// </summary>
public interface IApplicationUrlService
{
    /// <summary>
    /// Dynamically resolves the trusted base URL (scheme://host[:port]) from an explicit client origin,
    /// an active HTTP request (Origin, Referer, or forwarded headers), or a configured fallback.
    /// </summary>
    /// <param name="clientOrigin">Optional browser origin supplied by the frontend (e.g. window.location.origin).</param>
    /// <returns>A validated, normalized absolute base URL without trailing slash (e.g. "https://erp.example.com:8443").</returns>
    string ResolveBaseUrl(string? clientOrigin = null);

    /// <summary>
    /// Generates a complete absolute application URL by combining the dynamically resolved base URL
    /// with a relative workflow path and optional query parameters.
    /// </summary>
    /// <param name="relativePath">The application route or path (e.g. "/public/rfq-submit").</param>
    /// <param name="queryParams">Optional dictionary of query parameters to append and encode.</param>
    /// <param name="clientOrigin">Optional browser origin supplied by the caller.</param>
    /// <returns>An absolute URL string with properly encoded query parameters.</returns>
    string BuildAbsoluteUrl(string relativePath, IDictionary<string, string?>? queryParams = null, string? clientOrigin = null);

    /// <summary>
    /// Validates whether a candidate origin string is a safe, well-formed HTTP/HTTPS origin
    /// that complies with trusted host rules and security policies.
    /// </summary>
    /// <param name="origin">The raw origin string to validate.</param>
    /// <param name="validatedOrigin">When valid, the canonical scheme://host[:port] string.</param>
    /// <param name="failureReason">When invalid, the description of why validation failed.</param>
    /// <returns>True if the origin is valid and trusted; otherwise false.</returns>
    bool IsValidOrigin(string? origin, out string? validatedOrigin, out string? failureReason);
}
