using System.Collections.Generic;

namespace Domain.Common.ApplicationUrl;

/// <summary>
/// Configuration options for the Application URL Service.
/// Minimal settings for trusted-origin validation, multi-tenancy domains, and background fallbacks.
/// </summary>
public class ApplicationUrlOptions
{
    public const string SectionName = "Hosting";

    /// <summary>
    /// Default fallback URL when neither client origin nor active HTTP request is available
    /// (e.g. background jobs, CLI tools, automated timers). Defaults to "http://localhost:4200".
    /// </summary>
    public string FallbackBaseUrl { get; set; } = "http://localhost:4200";

    /// <summary>
    /// Tenant base domain for multi-tenant matching (e.g. "localhost", "apexerp.com", "example.com").
    /// </summary>
    public string TenantBaseDomain { get; set; } = "localhost";

    /// <summary>
    /// Explicit list of additional trusted origins or base domains allowed for workflow links.
    /// </summary>
    public List<string> AllowedOrigins { get; set; } = new();

    /// <summary>
    /// Whether private local area network (LAN) IP addresses and intranet hostnames are trusted.
    /// Enabled by default to support LAN and on-premise development/deployments.
    /// </summary>
    public bool AllowLocalNetworkOrigins { get; set; } = true;
}
