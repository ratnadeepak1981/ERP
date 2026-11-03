using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SaaS.Application.DTOs;
using SaaS.Application.Services;
using SaaS.Core.Models;
using SaaS.Infrastructure.Persistence;
using SaaS.Infrastructure.Repositories;
using Xunit;

namespace ERP.SecurityTests;

public class TenantDomainServiceTests
{
    private SaaSDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<SaaSDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new SaaSDbContext(options);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ResolveTenantByHostnameAsync_EmptyOrWhitespace_ReturnsMalformed(string? hostname)
    {
        using var db = CreateInMemoryDb();
        var repo = new TenantDomainRepository(db);
        var service = new TenantDomainService(repo);

        var result = await service.ResolveTenantByHostnameAsync(hostname!);

        Assert.False(result.IsResolved);
        Assert.Contains("empty", result.FailureReason, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("invalid hostname with spaces")]
    [InlineData("http://retail.localhost")]
    [InlineData("retail:localhost")]
    [InlineData("inv@lid.domain")]
    public async Task ResolveTenantByHostnameAsync_MalformedHostname_ReturnsMalformed(string hostname)
    {
        using var db = CreateInMemoryDb();
        var repo = new TenantDomainRepository(db);
        var service = new TenantDomainService(repo);

        var result = await service.ResolveTenantByHostnameAsync(hostname);

        Assert.False(result.IsResolved);
        Assert.Contains("malformed", result.FailureReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ResolveTenantByHostnameAsync_UnknownHostname_ReturnsNotFound()
    {
        using var db = CreateInMemoryDb();
        var repo = new TenantDomainRepository(db);
        var service = new TenantDomainService(repo);

        var result = await service.ResolveTenantByHostnameAsync("unknown.localhost");

        Assert.False(result.IsResolved);
        Assert.Contains("not registered", result.FailureReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ResolveTenantByHostnameAsync_InactiveDomain_ReturnsInactiveDomain()
    {
        using var db = CreateInMemoryDb();
        var tenantId = Guid.NewGuid();
        var tenant = new Tenant
        {
            Id = tenantId,
            Code = "tenant-inactive-domain",
            Name = "Tenant Inactive Domain",
            IsActive = true
        };
        db.Tenants.Add(tenant);

        var domain = new TenantDomain
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Hostname = "inactive.localhost",
            Subdomain = "inactive",
            DomainType = TenantDomainType.Subdomain,
            IsPrimary = true,
            IsActive = false,
            IsVerified = true
        };
        db.TenantDomains.Add(domain);
        await db.SaveChangesAsync();

        var repo = new TenantDomainRepository(db);
        var service = new TenantDomainService(repo);

        var result = await service.ResolveTenantByHostnameAsync("inactive.localhost");

        Assert.False(result.IsResolved);
        Assert.False(result.IsActive);
        Assert.Equal(tenantId, result.TenantId);
        Assert.Contains("inactive", result.FailureReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ResolveTenantByHostnameAsync_UnverifiedDomain_ReturnsUnverifiedDomain()
    {
        using var db = CreateInMemoryDb();
        var tenantId = Guid.NewGuid();
        var tenant = new Tenant
        {
            Id = tenantId,
            Code = "tenant-unverified-domain",
            Name = "Tenant Unverified Domain",
            IsActive = true
        };
        db.Tenants.Add(tenant);

        var domain = new TenantDomain
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Hostname = "unverified.localhost",
            Subdomain = "unverified",
            DomainType = TenantDomainType.Subdomain,
            IsPrimary = true,
            IsActive = true,
            IsVerified = false
        };
        db.TenantDomains.Add(domain);
        await db.SaveChangesAsync();

        var repo = new TenantDomainRepository(db);
        var service = new TenantDomainService(repo);

        var result = await service.ResolveTenantByHostnameAsync("unverified.localhost");

        Assert.False(result.IsResolved);
        Assert.False(result.IsVerified);
        Assert.Equal(tenantId, result.TenantId);
        Assert.Contains("unverified", result.FailureReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ResolveTenantByHostnameAsync_InactiveTenant_ReturnsInactiveTenant()
    {
        using var db = CreateInMemoryDb();
        var tenantId = Guid.NewGuid();
        var tenant = new Tenant
        {
            Id = tenantId,
            Code = "inactive-tenant",
            Name = "Inactive Tenant",
            IsActive = false
        };
        db.Tenants.Add(tenant);

        var domain = new TenantDomain
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Hostname = "retail.localhost",
            Subdomain = "retail",
            DomainType = TenantDomainType.Subdomain,
            IsPrimary = true,
            IsActive = true,
            IsVerified = true
        };
        db.TenantDomains.Add(domain);
        await db.SaveChangesAsync();

        var repo = new TenantDomainRepository(db);
        var service = new TenantDomainService(repo);

        var result = await service.ResolveTenantByHostnameAsync("retail.localhost");

        Assert.False(result.IsResolved);
        Assert.False(result.IsTenantActive);
        Assert.Equal(tenantId, result.TenantId);
        Assert.Contains("tenant is inactive", result.FailureReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ResolveTenantByHostnameAsync_ActiveAndVerified_ResolvesSuccessfully()
    {
        using var db = CreateInMemoryDb();
        var tenantId = Guid.NewGuid();
        var tenant = new Tenant
        {
            Id = tenantId,
            Code = "retail-tenant",
            Name = "Retail Tenant",
            IsActive = true
        };
        db.Tenants.Add(tenant);

        var domain = new TenantDomain
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Hostname = "retail.localhost",
            Subdomain = "retail",
            DomainType = TenantDomainType.Subdomain,
            IsPrimary = true,
            IsActive = true,
            IsVerified = true
        };
        db.TenantDomains.Add(domain);
        await db.SaveChangesAsync();

        var repo = new TenantDomainRepository(db);
        var service = new TenantDomainService(repo);

        var result = await service.ResolveTenantByHostnameAsync("retail.localhost");

        Assert.True(result.IsResolved);
        Assert.Equal(tenantId, result.TenantId);
        Assert.Equal("retail.localhost", result.Hostname);
        Assert.Equal("retail", result.Subdomain);
        Assert.Equal(TenantDomainType.Subdomain, result.DomainType);
        Assert.True(result.IsPrimary);
        Assert.True(result.IsActive);
        Assert.True(result.IsVerified);
        Assert.True(result.IsTenantActive);
    }

    [Theory]
    [InlineData("RETAIL.LOCALHOST")]
    [InlineData("retail.localhost.")]
    [InlineData("  ReTaiL.LoCaLhOsT.  ")]
    public async Task ResolveTenantByHostnameAsync_Normalization_HandlesCaseAndTrailingDot(string inputHost)
    {
        using var db = CreateInMemoryDb();
        var tenantId = Guid.NewGuid();
        var tenant = new Tenant
        {
            Id = tenantId,
            Code = "retail-tenant",
            Name = "Retail Tenant",
            IsActive = true
        };
        db.Tenants.Add(tenant);

        var domain = new TenantDomain
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Hostname = "retail.localhost",
            Subdomain = "retail",
            DomainType = TenantDomainType.Subdomain,
            IsPrimary = true,
            IsActive = true,
            IsVerified = true
        };
        db.TenantDomains.Add(domain);
        await db.SaveChangesAsync();

        var repo = new TenantDomainRepository(db);
        var service = new TenantDomainService(repo);

        var result = await service.ResolveTenantByHostnameAsync(inputHost);

        Assert.True(result.IsResolved);
        Assert.Equal(tenantId, result.TenantId);
        Assert.Equal("retail.localhost", result.Hostname);
    }

    [Fact]
    public async Task ResolveTenantByHostnameAsync_CustomDomain_ResolvesSuccessfully()
    {
        using var db = CreateInMemoryDb();
        var tenantId = Guid.NewGuid();
        var tenant = new Tenant
        {
            Id = tenantId,
            Code = "custom-tenant",
            Name = "Custom Tenant",
            IsActive = true
        };
        db.Tenants.Add(tenant);

        var domain = new TenantDomain
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Hostname = "portal.acme-corp.com",
            Subdomain = null,
            DomainType = TenantDomainType.CustomDomain,
            IsPrimary = true,
            IsActive = true,
            IsVerified = true
        };
        db.TenantDomains.Add(domain);
        await db.SaveChangesAsync();

        var repo = new TenantDomainRepository(db);
        var service = new TenantDomainService(repo);

        var result = await service.ResolveTenantByHostnameAsync("portal.acme-corp.com");

        Assert.True(result.IsResolved);
        Assert.Equal(tenantId, result.TenantId);
        Assert.Equal("portal.acme-corp.com", result.Hostname);
        Assert.Null(result.Subdomain);
        Assert.Equal(TenantDomainType.CustomDomain, result.DomainType);
    }
}
