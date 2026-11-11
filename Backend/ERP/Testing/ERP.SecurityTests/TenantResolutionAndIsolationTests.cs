using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using API.Common.TenantResolution;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using SaaS.Application.DTOs;
using SaaS.Application.Interfaces;
using SaaS.Application.Services;
using SaaS.Core.Models;
using SaaS.Core.Rules;
using Xunit;

namespace ERP.SecurityTests;

public class TenantResolutionAndIsolationTests
{
    private readonly Guid _retailTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly Guid _wholesaleTenantId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Theory]
    // 1. Localhost development
    [InlineData("retail.localhost:4200", null, "retail")]
    [InlineData("wholesale.localhost:4200", null, "wholesale")]
    [InlineData("retail.localhost", null, "retail")]
    [InlineData("localhost:4200", null, null)]
    [InlineData("localhost", null, null)]
    [InlineData("127.0.0.1:4200", null, null)]
    // 2. LAN demonstration via wildcard IP DNS (.nip.io / .sslip.io)
    [InlineData("retail.192.168.1.5.nip.io:4200", null, "retail")]
    [InlineData("wholesale.192.168.1.5.nip.io:4200", null, "wholesale")]
    [InlineData("192.168.1.5.nip.io:4200", null, null)]
    [InlineData("192.168.1.5:4200", null, null)]
    // 3. Configured base domain (e.g. apexerp.com)
    [InlineData("retail.apexerp.com", "apexerp.com", "retail")]
    [InlineData("wholesale.apexerp.com", "apexerp.com", "wholesale")]
    [InlineData("apexerp.com", "apexerp.com", null)]
    public void TenantHostResolver_ExtractSubdomain_CorrectlyExtracts(string host, string? baseDomain, string? expectedSubdomain)
    {
        string? result = TenantHostResolver.ExtractSubdomain(host, baseDomain);
        Assert.Equal(expectedSubdomain, result);
    }

    [Fact]
    public async Task TenantDomainService_ValidSubdomain_ResolvesSuccessfully()
    {
        var domainService = CreateDomainService();
        var result = await domainService.ResolveTenantByHostnameAsync("retail.localhost:4200");

        Assert.True(result.IsResolved);
        Assert.Equal(_retailTenantId, result.TenantId);
        Assert.Equal("retail", result.Subdomain);
        Assert.Equal("Retail Tenant Mart", result.TenantName);
    }

    [Fact]
    public async Task TenantDomainService_LanSubdomain_ResolvesSuccessfully()
    {
        var domainService = CreateDomainService();
        var result = await domainService.ResolveTenantByHostnameAsync("wholesale.192.168.1.5.nip.io:4200");

        Assert.True(result.IsResolved);
        Assert.Equal(_wholesaleTenantId, result.TenantId);
        Assert.Equal("wholesale", result.Subdomain);
        Assert.Equal("Wholesale Distribution Corp", result.TenantName);
    }

    [Fact]
    public async Task TenantDomainService_UnknownSubdomain_ReturnsNotFound()
    {
        var domainService = CreateDomainService();
        var result = await domainService.ResolveTenantByHostnameAsync("unknown.localhost:4200");

        Assert.False(result.IsResolved);
        Assert.Contains("not registered", result.FailureReason);
    }

    [Fact]
    public async Task Middleware_ValidActiveTenant_ResolvesTenantContext()
    {
        var domainService = CreateDomainService();
        var tenantContext = new TenantContext();

        bool nextCalled = false;
        var middleware = new TenantResolutionMiddleware(
            next: (ctx) =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            logger: NullLogger<TenantResolutionMiddleware>.Instance);

        var context = new DefaultHttpContext();
        context.Request.Headers["Host"] = "retail.localhost:4200";

        await middleware.InvokeAsync(context, tenantContext, domainService);

        Assert.True(nextCalled);
        Assert.True(tenantContext.IsTenantDomainRequest);
        Assert.False(tenantContext.IsLegacySharedHost);
        Assert.Equal(_retailTenantId, tenantContext.TenantId);
        Assert.Equal("retail", tenantContext.Subdomain);
        Assert.Equal("Retail Tenant Mart", tenantContext.TenantName);
    }

    [Fact]
    public async Task Middleware_UnknownTenant_Returns404NotFound()
    {
        var domainService = CreateDomainService();
        var tenantContext = new TenantContext();

        bool nextCalled = false;
        var middleware = new TenantResolutionMiddleware(
            next: (ctx) =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            logger: NullLogger<TenantResolutionMiddleware>.Instance);

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Request.Headers["Host"] = "nonexistent.localhost:4200";

        await middleware.InvokeAsync(context, tenantContext, domainService);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
        Assert.False(tenantContext.IsTenantDomainRequest);
    }

    [Fact]
    public async Task Middleware_InactiveTenant_Returns404NotFound()
    {
        var domainService = CreateDomainService();
        var tenantContext = new TenantContext();

        bool nextCalled = false;
        var middleware = new TenantResolutionMiddleware(
            next: (ctx) =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            logger: NullLogger<TenantResolutionMiddleware>.Instance);

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Request.Headers["Host"] = "suspended.localhost:4200";

        await middleware.InvokeAsync(context, tenantContext, domainService);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
        Assert.False(tenantContext.IsTenantDomainRequest);
    }

    [Fact]
    public async Task Middleware_LegacySharedHost_PassesThroughAsLegacy()
    {
        var domainService = CreateDomainService();
        var tenantContext = new TenantContext();

        bool nextCalled = false;
        var middleware = new TenantResolutionMiddleware(
            next: (ctx) =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            logger: NullLogger<TenantResolutionMiddleware>.Instance);

        var context = new DefaultHttpContext();
        context.Request.Headers["Host"] = "localhost:4200";

        await middleware.InvokeAsync(context, tenantContext, domainService);

        Assert.True(nextCalled);
        Assert.False(tenantContext.IsTenantDomainRequest);
        Assert.True(tenantContext.IsLegacySharedHost);
    }

    [Fact]
    public async Task Middleware_ForwardedHostHeader_TakesPrecedenceOverDirectHost()
    {
        var domainService = CreateDomainService();
        var tenantContext = new TenantContext();

        bool nextCalled = false;
        var middleware = new TenantResolutionMiddleware(
            next: (ctx) =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            logger: NullLogger<TenantResolutionMiddleware>.Instance);

        var context = new DefaultHttpContext();
        context.Request.Host = new HostString("localhost:5182");
        context.Request.Headers["X-Forwarded-Host"] = "retail.localhost:4200";

        await middleware.InvokeAsync(context, tenantContext, domainService);

        Assert.True(nextCalled);
        Assert.True(tenantContext.IsTenantDomainRequest);
        Assert.Equal("retail", tenantContext.Subdomain);
        Assert.Equal(_retailTenantId, tenantContext.TenantId);
    }

    [Fact]
    public async Task ConsistencyMiddleware_TenantAHostWithTenantAJwt_Succeeds()
    {
        var tenantContext = new TenantContext();
        tenantContext.SetResolvedTenant(_retailTenantId, "retail.localhost:4200", "retail", "Retail Tenant Mart");

        bool nextCalled = false;
        var consistencyMiddleware = new TenantHostConsistencyMiddleware(
            next: (ctx) =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            logger: NullLogger<TenantHostConsistencyMiddleware>.Instance);

        var context = new DefaultHttpContext();
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim("tenant_id", _retailTenantId.ToString())
        };
        context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"));

        await consistencyMiddleware.InvokeAsync(context, tenantContext);

        Assert.True(nextCalled);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    [Fact]
    public async Task ConsistencyMiddleware_TenantAHostWithTenantBJwt_RejectsWith403CrossTenantMismatch()
    {
        var tenantContext = new TenantContext();
        tenantContext.SetResolvedTenant(_retailTenantId, "retail.localhost:4200", "retail", "Retail Tenant Mart");

        bool nextCalled = false;
        var consistencyMiddleware = new TenantHostConsistencyMiddleware(
            next: (ctx) =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            logger: NullLogger<TenantHostConsistencyMiddleware>.Instance);

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        // Authenticated user belonging to WHOLESALE tenant attempting to access RETAIL tenant
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim("tenant_id", _wholesaleTenantId.ToString())
        };
        context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"));

        await consistencyMiddleware.InvokeAsync(context, tenantContext);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        string responseText = await reader.ReadToEndAsync();
        Assert.Contains("Cross-tenant domain mismatch", responseText);
    }

    private ITenantDomainService CreateDomainService()
    {
        var repo = new MockTenantDomainRepository();

        var retailTenant = new Tenant
        {
            Id = _retailTenantId,
            Name = "Retail Tenant Mart",
            Code = "RETAIL",
            Subdomain = "retail",
            IsActive = true
        };

        var wholesaleTenant = new Tenant
        {
            Id = _wholesaleTenantId,
            Name = "Wholesale Distribution Corp",
            Code = "WHOLESALE",
            Subdomain = "wholesale",
            IsActive = true
        };

        var suspendedTenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Suspended Tenant",
            Code = "SUSPENDED",
            Subdomain = "suspended",
            IsActive = false
        };

        repo.Domains.Add(new TenantDomain
        {
            Id = Guid.NewGuid(),
            TenantId = _retailTenantId,
            Tenant = retailTenant,
            Hostname = "retail.localhost",
            Subdomain = "retail",
            DomainType = TenantDomainType.Subdomain,
            IsPrimary = true,
            IsActive = true,
            IsVerified = true
        });

        repo.Domains.Add(new TenantDomain
        {
            Id = Guid.NewGuid(),
            TenantId = _wholesaleTenantId,
            Tenant = wholesaleTenant,
            Hostname = "wholesale.localhost",
            Subdomain = "wholesale",
            DomainType = TenantDomainType.Subdomain,
            IsPrimary = true,
            IsActive = true,
            IsVerified = true
        });

        repo.Domains.Add(new TenantDomain
        {
            Id = Guid.NewGuid(),
            TenantId = suspendedTenant.Id,
            Tenant = suspendedTenant,
            Hostname = "suspended.localhost",
            Subdomain = "suspended",
            DomainType = TenantDomainType.Subdomain,
            IsPrimary = true,
            IsActive = true,
            IsVerified = true
        });

        return new TenantDomainService(repo);
    }

    private class MockTenantDomainRepository : ITenantDomainRepository
    {
        public readonly List<TenantDomain> Domains = new();

        public Task<TenantDomain?> GetByHostnameAsync(string hostname) =>
            Task.FromResult(Domains.Find(d => string.Equals(d.Hostname, hostname, StringComparison.OrdinalIgnoreCase)));

        public Task<TenantDomain?> GetBySubdomainAsync(string subdomain) =>
            Task.FromResult(Domains.Find(d => string.Equals(d.Subdomain, subdomain, StringComparison.OrdinalIgnoreCase)));

        public Task<List<TenantDomain>> GetByTenantIdAsync(Guid tenantId) =>
            Task.FromResult(Domains.FindAll(d => d.TenantId == tenantId));

        public Task<bool> ExistsHostnameAsync(string hostname) =>
            Task.FromResult(Domains.Exists(d => string.Equals(d.Hostname, hostname, StringComparison.OrdinalIgnoreCase)));

        public Task<bool> ExistsSubdomainAsync(string subdomain) =>
            Task.FromResult(Domains.Exists(d => string.Equals(d.Subdomain, subdomain, StringComparison.OrdinalIgnoreCase)));

        public Task AddAsync(TenantDomain tenantDomain)
        {
            Domains.Add(tenantDomain);
            return Task.CompletedTask;
        }

        public Task DeleteAsync(TenantDomain tenantDomain)
        {
            Domains.Remove(tenantDomain);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync() => Task.CompletedTask;
    }
}
