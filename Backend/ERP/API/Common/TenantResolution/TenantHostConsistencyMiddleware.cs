using System;
using System.Net.Mime;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace API.Common.TenantResolution;

public class TenantHostConsistencyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TenantHostConsistencyMiddleware> _logger;

    public TenantHostConsistencyMiddleware(
        RequestDelegate next,
        ILogger<TenantHostConsistencyMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(
        HttpContext context,
        ITenantContext tenantContext)
    {
        // Preflight CORS requests and static/doc assets are allowed without consistency check
        if (HttpMethods.IsOptions(context.Request.Method))
        {
            await _next(context);
            return;
        }

        var path = context.Request.Path.Value ?? string.Empty;
        if (path.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/hangfire", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        // On legacy shared host, preserve existing behavior:
        // Downstream controllers continue using JWT tenant claim.
        // Arbitrary tenant headers (like X-Tenant-Id) do not override the JWT claim.
        if (tenantContext.IsLegacySharedHost)
        {
            await _next(context);
            return;
        }

        // Tenant-specific domain request:
        if (tenantContext.IsTenantDomainRequest)
        {
            var endpoint = context.GetEndpoint();
            bool allowAnonymous = endpoint?.Metadata.GetMetadata<IAllowAnonymous>() != null;

            // If user is unauthenticated on tenant-specific hostname
            if (context.User.Identity?.IsAuthenticated != true)
            {
                if (allowAnonymous)
                {
                    await _next(context);
                    return;
                }

                _logger.LogWarning(
                    "Unauthenticated request to tenant domain '{Host}'",
                    tenantContext.Hostname);

                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = MediaTypeNames.Application.Json;

                var response = new
                {
                    status = StatusCodes.Status401Unauthorized,
                    title = "Unauthorized",
                    detail = "Authentication is required to access this tenant domain."
                };

                await context.Response.WriteAsync(
                    JsonSerializer.Serialize(response));
                return;
            }

            // User is authenticated: inspect tenant_id claim
            var jwtTenantClaim = context.User.FindFirst("tenant_id")?.Value;

            // Platform users do not have a tenant_id claim
            if (string.IsNullOrEmpty(jwtTenantClaim) || !Guid.TryParse(jwtTenantClaim, out var jwtTenantId))
            {
                _logger.LogWarning(
                    "Platform user or user without tenant claim attempted access to tenant domain '{Host}'",
                    tenantContext.Hostname);

                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = MediaTypeNames.Application.Json;

                var response = new
                {
                    status = StatusCodes.Status403Forbidden,
                    title = "Forbidden",
                    detail = "Platform users cannot access tenant-specific domains directly."
                };

                await context.Response.WriteAsync(
                    JsonSerializer.Serialize(response));
                return;
            }

            // Cross-tenant domain mismatch
            if (jwtTenantId != tenantContext.TenantId!.Value)
            {
                _logger.LogWarning(
                    "Cross-tenant mismatch: JWT tenant '{JwtTenant}' does not match host '{Host}' tenant '{HostTenant}'",
                    jwtTenantId,
                    tenantContext.Hostname,
                    tenantContext.TenantId);

                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = MediaTypeNames.Application.Json;

                var response = new
                {
                    status = StatusCodes.Status403Forbidden,
                    title = "Forbidden",
                    detail = "Cross-tenant domain mismatch. Authenticated tenant does not match the requested domain."
                };

                await context.Response.WriteAsync(
                    JsonSerializer.Serialize(response));
                return;
            }
        }

        await _next(context);
    }
}
