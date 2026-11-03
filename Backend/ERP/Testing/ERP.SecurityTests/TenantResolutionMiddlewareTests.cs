using System;
using System.IO;
using System.Net;
using System.Security.Claims;
using System.Threading.Tasks;
using API.Common.TenantResolution;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SaaS.Application.DTOs;
using SaaS.Application.Interfaces;
using SaaS.Application.Services;
using SaaS.Core.Models;
using SaaS.Infrastructure.Persistence;
using SaaS.Infrastructure.Repositories;
using Xunit;

namespace ERP.SecurityTests;

public class TenantResolutionMiddlewareTests
{
    private SaaSDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<SaaSDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new SaaSDbContext(options);
    }

    private (TenantResolutionMiddleware resolutionMiddleware, TenantHostConsistencyMiddleware consistencyMiddleware, ITenantDomainService domainService, SaaSDbContext db)
        CreateMiddlewarePipeline(RequestDelegate endpointDelegate)
    {
        var db = CreateInMemoryDb();
        var repo = new TenantDomainRepository(db);
        var domainService = new TenantDomainService(repo);

        var consistency = new TenantHostConsistencyMiddleware(
            endpointDelegate,
            NullLogger<TenantHostConsistencyMiddleware>.Instance);

        var resolution = new TenantResolutionMiddleware(
            async ctx => await consistency.InvokeAsync(ctx, ctx.RequestServices?.GetService(typeof(ITenantContext)) as ITenantContext ?? (ITenantContext)ctx.Items["TenantContext"]!),
            NullLogger<TenantResolutionMiddleware>.Instance);

        return (resolution, consistency, domainService, db);
    }

    [Theory]
    [InlineData("localhost")]
    [InlineData("127.0.0.1")]
    [InlineData("::1")]
    public async Task LegacyHost_Localhost_PreservesExistingBehavior_AllowsWithoutTenantLookup(string legacyHost)
    {
        bool endpointReached = false;
        var (resolution, _, domainService, db) = CreateMiddlewarePipeline(ctx =>
        {
            endpointReached = true;
            ctx.Response.StatusCode = StatusCodes.Status200OK;
            return Task.CompletedTask;
        });

        using (db)
        {
            var context = new DefaultHttpContext();
            context.Request.Host = new HostString(legacyHost);
            context.Response.Body = new MemoryStream();

            var tenantContext = new TenantContext();
            context.Items["TenantContext"] = tenantContext;

            await resolution.InvokeAsync(context, tenantContext, domainService);

            Assert.True(endpointReached);
            Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
            Assert.True(tenantContext.IsLegacySharedHost);
            Assert.False(tenantContext.IsTenantDomainRequest);
            Assert.Null(tenantContext.TenantId);
        }
    }

    [Fact]
    public async Task ArbitraryTenantHeader_OnSharedHost_DoesNotOverrideJwtTenant()
    {
        bool endpointReached = false;
        var (resolution, _, domainService, db) = CreateMiddlewarePipeline(ctx =>
        {
            endpointReached = true;
            ctx.Response.StatusCode = StatusCodes.Status200OK;
            return Task.CompletedTask;
        });

        using (db)
        {
            var context = new DefaultHttpContext();
            context.Request.Host = new HostString("localhost");
            context.Request.Headers["X-Tenant-Id"] = Guid.NewGuid().ToString();
            context.Response.Body = new MemoryStream();

            var realTenantId = Guid.NewGuid();
            var identity = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                new Claim("tenant_id", realTenantId.ToString())
            }, "TestAuth");
            context.User = new ClaimsPrincipal(identity);

            var tenantContext = new TenantContext();
            context.Items["TenantContext"] = tenantContext;

            await resolution.InvokeAsync(context, tenantContext, domainService);

            Assert.True(endpointReached);
            Assert.True(tenantContext.IsLegacySharedHost);
            Assert.Null(tenantContext.TenantId);
        }
    }

    [Fact]
    public async Task UnknownTenantHostname_Returns404NotFound()
    {
        bool endpointReached = false;
        var (resolution, _, domainService, db) = CreateMiddlewarePipeline(ctx =>
        {
            endpointReached = true;
            return Task.CompletedTask;
        });

        using (db)
        {
            var context = new DefaultHttpContext();
            context.Request.Host = new HostString("unknown.localhost");
            context.Response.Body = new MemoryStream();

            var tenantContext = new TenantContext();
            context.Items["TenantContext"] = tenantContext;

            await resolution.InvokeAsync(context, tenantContext, domainService);

            Assert.False(endpointReached);
            Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
        }
    }

    [Fact]
    public async Task InactiveTenantDomain_Returns404NotFound()
    {
        var tenantId = Guid.NewGuid();
        bool endpointReached = false;
        var (resolution, _, domainService, db) = CreateMiddlewarePipeline(ctx =>
        {
            endpointReached = true;
            return Task.CompletedTask;
        });

        using (db)
        {
            db.Tenants.Add(new Tenant
            {
                Id = tenantId,
                Code = "inactive-domain-tenant",
                Name = "Tenant with Inactive Domain",
                IsActive = true
            });
            db.TenantDomains.Add(new TenantDomain
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Hostname = "inactive.localhost",
                Subdomain = "inactive",
                DomainType = TenantDomainType.Subdomain,
                IsPrimary = true,
                IsActive = false,
                IsVerified = true
            });
            await db.SaveChangesAsync();

            var context = new DefaultHttpContext();
            context.Request.Host = new HostString("inactive.localhost");
            context.Response.Body = new MemoryStream();

            var tenantContext = new TenantContext();
            context.Items["TenantContext"] = tenantContext;

            await resolution.InvokeAsync(context, tenantContext, domainService);

            Assert.False(endpointReached);
            Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
        }
    }

    [Fact]
    public async Task UnverifiedTenantDomain_Returns404NotFound()
    {
        var tenantId = Guid.NewGuid();
        bool endpointReached = false;
        var (resolution, _, domainService, db) = CreateMiddlewarePipeline(ctx =>
        {
            endpointReached = true;
            return Task.CompletedTask;
        });

        using (db)
        {
            db.Tenants.Add(new Tenant
            {
                Id = tenantId,
                Code = "unverified-tenant",
                Name = "Tenant Unverified",
                IsActive = true
            });
            db.TenantDomains.Add(new TenantDomain
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Hostname = "unverified.localhost",
                Subdomain = "unverified",
                DomainType = TenantDomainType.Subdomain,
                IsPrimary = true,
                IsActive = true,
                IsVerified = false
            });
            await db.SaveChangesAsync();

            var context = new DefaultHttpContext();
            context.Request.Host = new HostString("unverified.localhost");
            context.Response.Body = new MemoryStream();

            var tenantContext = new TenantContext();
            context.Items["TenantContext"] = tenantContext;

            await resolution.InvokeAsync(context, tenantContext, domainService);

            Assert.False(endpointReached);
            Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
        }
    }

    [Fact]
    public async Task InactiveAssociatedTenant_Returns404NotFound()
    {
        var tenantId = Guid.NewGuid();
        bool endpointReached = false;
        var (resolution, _, domainService, db) = CreateMiddlewarePipeline(ctx =>
        {
            endpointReached = true;
            return Task.CompletedTask;
        });

        using (db)
        {
            db.Tenants.Add(new Tenant
            {
                Id = tenantId,
                Code = "inactive-tenant",
                Name = "Inactive Tenant",
                IsActive = false
            });
            db.TenantDomains.Add(new TenantDomain
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Hostname = "retail.localhost",
                Subdomain = "retail",
                DomainType = TenantDomainType.Subdomain,
                IsPrimary = true,
                IsActive = true,
                IsVerified = true
            });
            await db.SaveChangesAsync();

            var context = new DefaultHttpContext();
            context.Request.Host = new HostString("retail.localhost");
            context.Response.Body = new MemoryStream();

            var tenantContext = new TenantContext();
            context.Items["TenantContext"] = tenantContext;

            await resolution.InvokeAsync(context, tenantContext, domainService);

            Assert.False(endpointReached);
            Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
        }
    }

    [Fact]
    public async Task TenantHostname_Unauthenticated_Returns401Unauthorized()
    {
        var tenantId = Guid.NewGuid();
        bool endpointReached = false;
        var (resolution, _, domainService, db) = CreateMiddlewarePipeline(ctx =>
        {
            endpointReached = true;
            return Task.CompletedTask;
        });

        using (db)
        {
            db.Tenants.Add(new Tenant
            {
                Id = tenantId,
                Code = "retail-tenant",
                Name = "Retail Tenant",
                IsActive = true
            });
            db.TenantDomains.Add(new TenantDomain
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Hostname = "retail.localhost",
                Subdomain = "retail",
                DomainType = TenantDomainType.Subdomain,
                IsPrimary = true,
                IsActive = true,
                IsVerified = true
            });
            await db.SaveChangesAsync();

            var context = new DefaultHttpContext();
            context.Request.Host = new HostString("retail.localhost");
            context.Response.Body = new MemoryStream();

            var tenantContext = new TenantContext();
            context.Items["TenantContext"] = tenantContext;

            await resolution.InvokeAsync(context, tenantContext, domainService);

            Assert.False(endpointReached);
            Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
        }
    }

    [Fact]
    public async Task TenantHostname_AuthenticatedWithMatchingJwt_Allowed200()
    {
        var tenantId = Guid.NewGuid();
        bool endpointReached = false;
        var (resolution, _, domainService, db) = CreateMiddlewarePipeline(ctx =>
        {
            endpointReached = true;
            ctx.Response.StatusCode = StatusCodes.Status200OK;
            return Task.CompletedTask;
        });

        using (db)
        {
            db.Tenants.Add(new Tenant
            {
                Id = tenantId,
                Code = "retail-tenant",
                Name = "Retail Tenant",
                IsActive = true
            });
            db.TenantDomains.Add(new TenantDomain
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Hostname = "retail.localhost",
                Subdomain = "retail",
                DomainType = TenantDomainType.Subdomain,
                IsPrimary = true,
                IsActive = true,
                IsVerified = true
            });
            await db.SaveChangesAsync();

            var context = new DefaultHttpContext();
            context.Request.Host = new HostString("retail.localhost");
            context.Response.Body = new MemoryStream();

            var identity = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                new Claim("tenant_id", tenantId.ToString())
            }, "TestAuth");
            context.User = new ClaimsPrincipal(identity);

            var tenantContext = new TenantContext();
            context.Items["TenantContext"] = tenantContext;

            await resolution.InvokeAsync(context, tenantContext, domainService);

            Assert.True(endpointReached);
            Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
            Assert.True(tenantContext.IsTenantDomainRequest);
            Assert.Equal(tenantId, tenantContext.TenantId);
            Assert.Equal("retail.localhost", tenantContext.Hostname);
            Assert.Equal("retail", tenantContext.Subdomain);
        }
    }

    [Fact]
    public async Task TenantHostname_AuthenticatedWithMismatchingJwt_Returns403Forbidden()
    {
        var tenantId = Guid.NewGuid();
        var anotherTenantId = Guid.NewGuid();
        bool endpointReached = false;
        var (resolution, _, domainService, db) = CreateMiddlewarePipeline(ctx =>
        {
            endpointReached = true;
            return Task.CompletedTask;
        });

        using (db)
        {
            db.Tenants.Add(new Tenant
            {
                Id = tenantId,
                Code = "retail-tenant",
                Name = "Retail Tenant",
                IsActive = true
            });
            db.TenantDomains.Add(new TenantDomain
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Hostname = "retail.localhost",
                Subdomain = "retail",
                DomainType = TenantDomainType.Subdomain,
                IsPrimary = true,
                IsActive = true,
                IsVerified = true
            });
            await db.SaveChangesAsync();

            var context = new DefaultHttpContext();
            context.Request.Host = new HostString("retail.localhost");
            context.Response.Body = new MemoryStream();

            // JWT has another tenant's ID
            var identity = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                new Claim("tenant_id", anotherTenantId.ToString())
            }, "TestAuth");
            context.User = new ClaimsPrincipal(identity);

            var tenantContext = new TenantContext();
            context.Items["TenantContext"] = tenantContext;

            await resolution.InvokeAsync(context, tenantContext, domainService);

            Assert.False(endpointReached);
            Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        }
    }

    [Fact]
    public async Task TenantHostname_PlatformUserWithoutTenantClaim_Returns403Forbidden()
    {
        var tenantId = Guid.NewGuid();
        bool endpointReached = false;
        var (resolution, _, domainService, db) = CreateMiddlewarePipeline(ctx =>
        {
            endpointReached = true;
            return Task.CompletedTask;
        });

        using (db)
        {
            db.Tenants.Add(new Tenant
            {
                Id = tenantId,
                Code = "retail-tenant",
                Name = "Retail Tenant",
                IsActive = true
            });
            db.TenantDomains.Add(new TenantDomain
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Hostname = "retail.localhost",
                Subdomain = "retail",
                DomainType = TenantDomainType.Subdomain,
                IsPrimary = true,
                IsActive = true,
                IsVerified = true
            });
            await db.SaveChangesAsync();

            var context = new DefaultHttpContext();
            context.Request.Host = new HostString("retail.localhost");
            context.Response.Body = new MemoryStream();

            // Platform user without tenant_id claim
            var identity = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.Role, "Platform Admin")
            }, "TestAuth");
            context.User = new ClaimsPrincipal(identity);

            var tenantContext = new TenantContext();
            context.Items["TenantContext"] = tenantContext;

            await resolution.InvokeAsync(context, tenantContext, domainService);

            Assert.False(endpointReached);
            Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        }
    }

    [Fact]
    public void TrustedHostResolver_UntrustedForwardedHost_SpoofingIgnored()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("198.51.100.25"); // External non-loopback IP
        context.Request.Host = new HostString("localhost:5182");
        context.Request.Headers["X-Forwarded-Host"] = "retail.localhost";

        string extracted = TrustedHostResolver.ExtractHost(context);

        Assert.Equal("localhost", extracted);
    }

    [Fact]
    public void TrustedHostResolver_TrustedForwardedHost_FromLoopback_Accepted()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Loopback; // 127.0.0.1
        context.Request.Host = new HostString("localhost:5182");
        context.Request.Headers["X-Forwarded-Host"] = "retail.localhost:4200";

        string extracted = TrustedHostResolver.ExtractHost(context);

        Assert.Equal("retail.localhost", extracted);
    }
}
