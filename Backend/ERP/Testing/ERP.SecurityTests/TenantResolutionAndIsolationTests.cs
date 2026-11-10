using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using API.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
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
    public async Task Middleware_ValidActiveTenant_ResolvesTenantContext()
    {
        var mockRepo = CreateMockRepo();
        var tenantContext = new TenantContext();
        var config = CreateConfig("localhost");

        bool nextCalled = false;
        var middleware = new TenantResolutionMiddleware(
            next: (ctx) =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            configuration: config);

        var context = new DefaultHttpContext();
        context.Request.Headers["Host"] = "retail.localhost:4200";

        await middleware.InvokeAsync(context, mockRepo, tenantContext);

        Assert.True(nextCalled);
        Assert.True(tenantContext.IsTenantRequest);
        Assert.Equal(_retailTenantId, tenantContext.TenantId);
        Assert.Equal("retail", tenantContext.Subdomain);
        Assert.Equal("Retail Tenant Mart", tenantContext.TenantName);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    [Fact]
    public async Task Middleware_UnknownTenant_Returns404NotFound()
    {
        var mockRepo = CreateMockRepo();
        var tenantContext = new TenantContext();
        var config = CreateConfig("localhost");

        bool nextCalled = false;
        var middleware = new TenantResolutionMiddleware(
            next: (ctx) =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            configuration: config);

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Request.Headers["Host"] = "nonexistent.localhost:4200";

        await middleware.InvokeAsync(context, mockRepo, tenantContext);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
        Assert.False(tenantContext.IsTenantRequest);
    }

    [Fact]
    public async Task Middleware_InactiveTenant_Returns403Forbidden()
    {
        var mockRepo = CreateMockRepo();
        var tenantContext = new TenantContext();
        var config = CreateConfig("localhost");

        bool nextCalled = false;
        var middleware = new TenantResolutionMiddleware(
            next: (ctx) =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            configuration: config);

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Request.Headers["Host"] = "suspended.localhost:4200";

        await middleware.InvokeAsync(context, mockRepo, tenantContext);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Fact]
    public async Task Middleware_PlatformRoot_PassesThroughWithoutTenantContext()
    {
        var mockRepo = CreateMockRepo();
        var tenantContext = new TenantContext();
        var config = CreateConfig("localhost");

        bool nextCalled = false;
        var middleware = new TenantResolutionMiddleware(
            next: (ctx) =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            configuration: config);

        var context = new DefaultHttpContext();
        context.Request.Headers["Host"] = "localhost:4200";

        await middleware.InvokeAsync(context, mockRepo, tenantContext);

        Assert.True(nextCalled);
        Assert.False(tenantContext.IsTenantRequest);
    }

    [Fact]
    public async Task Middleware_ForwardedHostHeader_TakesPrecedenceOverDirectHost()
    {
        var mockRepo = CreateMockRepo();
        var tenantContext = new TenantContext();
        var config = CreateConfig("localhost");

        bool nextCalled = false;
        var middleware = new TenantResolutionMiddleware(
            next: (ctx) =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            configuration: config);

        var context = new DefaultHttpContext();
        // Reverse proxy target is localhost:5182, but client sent retail.localhost:4200
        context.Request.Host = new HostString("localhost:5182");
        context.Request.Headers["X-Forwarded-Host"] = "retail.localhost:4200";

        await middleware.InvokeAsync(context, mockRepo, tenantContext);

        Assert.True(nextCalled);
        Assert.True(tenantContext.IsTenantRequest);
        Assert.Equal("retail", tenantContext.Subdomain);
    }

    [Fact]
    public async Task Middleware_TenantAHostWithTenantAJwt_Succeeds()
    {
        var mockRepo = CreateMockRepo();
        var tenantContext = new TenantContext();
        var config = CreateConfig("localhost");

        bool nextCalled = false;
        var middleware = new TenantResolutionMiddleware(
            next: (ctx) =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            configuration: config);

        var context = new DefaultHttpContext();
        context.Request.Headers["Host"] = "retail.localhost:4200";

        // Authenticated user belonging to retail tenant
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim("tenant_id", _retailTenantId.ToString())
        };
        context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"));

        await middleware.InvokeAsync(context, mockRepo, tenantContext);

        Assert.True(nextCalled);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    [Fact]
    public async Task Middleware_TenantAHostWithTenantBJwt_RejectsWith403TenantIsolationViolation()
    {
        var mockRepo = CreateMockRepo();
        var tenantContext = new TenantContext();
        var config = CreateConfig("localhost");

        bool nextCalled = false;
        var middleware = new TenantResolutionMiddleware(
            next: (ctx) =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            configuration: config);

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Request.Headers["Host"] = "retail.localhost:4200";

        // Authenticated user belonging to WHOLESALE tenant attempting to access RETAIL tenant
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim("tenant_id", _wholesaleTenantId.ToString())
        };
        context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"));

        await middleware.InvokeAsync(context, mockRepo, tenantContext);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        string responseText = await reader.ReadToEndAsync();
        Assert.Contains("Tenant isolation violation", responseText);
    }

    private static IConfiguration CreateConfig(string baseDomain)
    {
        var myConfiguration = new Dictionary<string, string?>
        {
            { "Hosting:TenantBaseDomain", baseDomain }
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(myConfiguration)
            .Build();
    }

    private MockTenantRepository CreateMockRepo()
    {
        var repo = new MockTenantRepository();

        repo.Tenants.Add(new Tenant
        {
            Id = _retailTenantId,
            Name = "Retail Tenant Mart",
            Code = "RETAIL",
            Subdomain = "retail",
            IndustryType = "Retail",
            CompanyStructure = "Single",
            ECommerceEnabled = true,
            IsActive = true
        });

        repo.Tenants.Add(new Tenant
        {
            Id = _wholesaleTenantId,
            Name = "Wholesale Distribution Corp",
            Code = "WHOLESALE",
            Subdomain = "wholesale",
            IndustryType = "Wholesale",
            CompanyStructure = "Multi",
            ECommerceEnabled = false,
            IsActive = true
        });

        repo.Tenants.Add(new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Suspended Tenant",
            Code = "SUSPENDED",
            Subdomain = "suspended",
            IsActive = false
        });

        return repo;
    }

    private class MockTenantRepository : ITenantRepository
    {
        public readonly List<Tenant> Tenants = new();

        public Task AddAsync(Tenant tenant)
        {
            Tenants.Add(tenant);
            return Task.CompletedTask;
        }

        public Task<Tenant?> GetByIdAsync(Guid tenantId) =>
            Task.FromResult(Tenants.Find(t => t.Id == tenantId));

        public Task<bool> ExistsByCodeAsync(string code) =>
            Task.FromResult(Tenants.Exists(t => t.Code == code));

        public Task<bool> ExistsBySubdomainAsync(string subdomain) =>
            Task.FromResult(Tenants.Exists(t => string.Equals(t.Subdomain, subdomain, StringComparison.OrdinalIgnoreCase)));

        public Task<Tenant?> GetBySubdomainAsync(string subdomain) =>
            Task.FromResult(Tenants.Find(t => string.Equals(t.Subdomain, subdomain, StringComparison.OrdinalIgnoreCase)));

        public Task SaveChangesAsync() => Task.CompletedTask;
        public Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> BeginTransactionAsync() =>
            throw new NotImplementedException();
        public Task CommitTransactionAsync(Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction) => Task.CompletedTask;
        public Task RollbackTransactionAsync(Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction) => Task.CompletedTask;
        public Task<List<TenantQueueItemDto>> GetTenantListAsync() => Task.FromResult(new List<TenantQueueItemDto>());
        public Task<TenantDetailsDto?> GetTenantDetailsAsync(Guid tenantId) => Task.FromResult<TenantDetailsDto?>(null);
        public Task<TenantLifecycleStatusDto?> GetTenantStatusAsync(Guid tenantId) => Task.FromResult<TenantLifecycleStatusDto?>(null);
        public Task<TenantMetricsDto> GetTenantMetricsAsync() => Task.FromResult(new TenantMetricsDto());
    }
}
