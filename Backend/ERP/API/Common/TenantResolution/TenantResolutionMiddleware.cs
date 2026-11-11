using System.Net.Mime;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using SaaS.Application.Interfaces;

namespace API.Common.TenantResolution;

public class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TenantResolutionMiddleware> _logger;

    public TenantResolutionMiddleware(
        RequestDelegate next,
        ILogger<TenantResolutionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(
        HttpContext context,
        ITenantContext tenantContext,
        ITenantDomainService tenantDomainService)
    {
        string host = TrustedHostResolver.ExtractHost(context);

        if (TrustedHostResolver.IsLegacySharedHost(host))
        {
            tenantContext.SetLegacySharedHost(host);
            await _next(context);
            return;
        }

        // Specific tenant hostname resolution
        var result = await tenantDomainService.ResolveTenantByHostnameAsync(
            host,
            context.RequestAborted);

        if (!result.IsResolved)
        {
            _logger.LogWarning(
                "Tenant domain resolution failed for host '{Host}'. Reason: {Reason}",
                host,
                result.FailureReason);

            context.Response.StatusCode = StatusCodes.Status404NotFound;
            context.Response.ContentType = MediaTypeNames.Application.Json;

            var errorResponse = new
            {
                status = StatusCodes.Status404NotFound,
                title = "Tenant Domain Not Found",
                detail = result.FailureReason ?? $"Tenant domain '{host}' was not found or is inactive."
            };

            await context.Response.WriteAsync(
                JsonSerializer.Serialize(errorResponse));
            return;
        }

        tenantContext.SetResolvedTenant(
            result.TenantId!.Value,
            result.Hostname!,
            result.Subdomain,
            result.TenantName);

        context.Items["CurrentTenantId"] = result.TenantId.Value;
        context.Items["TenantContext"] = tenantContext;

        await _next(context);
    }
}
