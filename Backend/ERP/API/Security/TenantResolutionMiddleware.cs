using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using SaaS.Application.Interfaces;
using SaaS.Application.Services;
using SaaS.Core.Rules;

namespace API.Security;

/// <summary>
/// Early HTTP middleware responsible for:
/// 1. Dynamic tenant resolution from the incoming request hostname/subdomain across localhost, LAN (.nip.io), and production.
/// 2. Verification of tenant existence (404) and active status (403).
/// 3. JWT cross-tenant isolation enforcement (rejecting requests where authenticated token tenant != domain tenant).
/// </summary>
public class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IConfiguration _configuration;

    public TenantResolutionMiddleware(
        RequestDelegate next,
        IConfiguration configuration)
    {
        _next = next;
        _configuration = configuration;
    }

    public async Task InvokeAsync(
        HttpContext context,
        ITenantRepository tenantRepository,
        TenantContext tenantContext)
    {
        string? configuredBaseDomain = _configuration["Hosting:TenantBaseDomain"];

        // 1. Read host (respecting reverse proxy / dev-server forwarded host if present)
        string? host = context.Request.Headers["X-Forwarded-Host"].FirstOrDefault()
                       ?? context.Request.Headers["Host"].FirstOrDefault()
                       ?? context.Request.Host.Value;

        string? subdomain = TenantHostResolver.ExtractSubdomain(host, configuredBaseDomain);

        // 2. If a tenant subdomain is present and not reserved
        if (!string.IsNullOrWhiteSpace(subdomain) && !TenantSlugRules.IsReserved(subdomain))
        {
            var tenant = await tenantRepository.GetBySubdomainAsync(subdomain);

            if (tenant == null)
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(new
                {
                    succeeded = false,
                    message = $"Tenant with subdomain '{subdomain}' does not exist.",
                    statusCode = 404
                });
                return;
            }

            if (!tenant.IsActive)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(new
                {
                    succeeded = false,
                    message = $"Tenant '{subdomain}' is currently inactive or suspended.",
                    statusCode = 403
                });
                return;
            }

            tenantContext.SetTenant(tenant);
            context.Items["TenantContext"] = tenantContext;
        }
        else
        {
            tenantContext.Reset();
        }

        // 3. JWT Tenant Isolation Enforcement
        // If the user has authenticated and this request targets a tenant subdomain,
        // verify that the token's tenant_id matches the resolved domain tenant.
        if (context.User.Identity?.IsAuthenticated == true && tenantContext.IsTenantRequest)
        {
            string? userTenantClaim = context.User.FindFirst("tenant_id")?.Value
                                      ?? context.User.FindFirst("TenantId")?.Value;

            if (!string.IsNullOrWhiteSpace(userTenantClaim) &&
                Guid.TryParse(userTenantClaim, out var userTenantId))
            {
                if (userTenantId != tenantContext.TenantId)
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsJsonAsync(new
                    {
                        succeeded = false,
                        message = "Tenant isolation violation: Authenticated session does not match requested tenant domain.",
                        statusCode = 403
                    });
                    return;
                }
            }
        }

        await _next(context);
    }
}
