namespace Domain.Common.ApplicationUrl;

/// <summary>
/// Abstraction for retrieving the browser-facing origin from the ambient HTTP request,
/// decoupling the application and domain layers from direct ASP.NET Core HttpContext dependencies.
/// </summary>
public interface IHttpRequestOriginAccessor
{
    /// <summary>
    /// Attempts to extract the browser-facing origin (Origin header, Referer authority,
    /// or forwarded/host headers) from the current HTTP request.
    /// Returns null if not in the context of an HTTP request.
    /// </summary>
    string? GetRequestOrigin();
}
