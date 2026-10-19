using System;

namespace ERP.AI.Infrastructure.Configuration;

public class PythonServiceOptions
{
    public const string SectionName = "PythonService";

    /// <summary>
    /// Base URL for the Python FastAPI sidecar service.
    /// In production environments, this must use HTTPS and must not fall back to localhost.
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Timeout in seconds for readiness and health operations.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 10;

    /// <summary>
    /// Validates the configuration against environment requirements.
    /// </summary>
    /// <param name="isDevelopment">True if running under Development environment.</param>
    /// <exception cref="InvalidOperationException">Thrown when configuration is invalid.</exception>
    public void Validate(bool isDevelopment)
    {
        if (string.IsNullOrWhiteSpace(BaseUrl))
        {
            throw new InvalidOperationException(
                "PythonService:BaseUrl is required but was not configured. " +
                "Configure 'PythonService:BaseUrl' or environment variable 'PythonService__BaseUrl'.");
        }

        if (!Uri.TryCreate(BaseUrl, UriKind.Absolute, out var uri))
        {
            throw new InvalidOperationException(
                $"PythonService:BaseUrl '{BaseUrl}' is not a valid absolute URL.");
        }

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException(
                $"PythonService:BaseUrl '{BaseUrl}' must use HTTP or HTTPS scheme.");
        }

        if (isDevelopment)
        {
            // Development allows http://localhost or http://127.0.0.1 or https
            if (uri.Scheme == Uri.UriSchemeHttp && !uri.IsLoopback)
            {
                throw new InvalidOperationException(
                    $"In Development, HTTP is only permitted for loopback addresses (localhost/127.0.0.1). Configured: '{BaseUrl}'.");
            }
        }
        else
        {
            // Production / Staging must not use HTTP or loopback
            if (uri.Scheme != Uri.UriSchemeHttps)
            {
                throw new InvalidOperationException(
                    $"In Non-Development environments, PythonService:BaseUrl must strictly use HTTPS. Configured: '{BaseUrl}'.");
            }

            if (uri.IsLoopback)
            {
                throw new InvalidOperationException(
                    "In Non-Development environments, PythonService:BaseUrl cannot target localhost or loopback addresses.");
            }
        }

        if (TimeoutSeconds <= 0)
        {
            throw new InvalidOperationException(
                $"PythonService:TimeoutSeconds must be greater than zero. Configured: {TimeoutSeconds}.");
        }
    }
}
