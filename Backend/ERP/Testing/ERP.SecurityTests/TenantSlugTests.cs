using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SaaS.Application.DTOs;
using SaaS.Application.Interfaces;
using SaaS.Application.Services;
using SaaS.Core.Models;
using SaaS.Core.Rules;
using Xunit;

namespace ERP.SecurityTests;

public class TenantSlugTests
{
    [Theory]
    [InlineData("ABC Retail", "abc-retail")]
    [InlineData("Lanka Retail Mart", "lanka-retail-mart")]
    [InlineData("Keells Supermarkets", "keells-supermarkets")]
    [InlineData("Apex & Co. (Pvt) Ltd!", "apex-co-pvt-ltd")]
    [InlineData("AcMe InDuStRiEs", "acme-industries")]
    [InlineData("Warehouse 101", "warehouse-101")]
    [InlineData("  Leading And Trailing  ", "leading-and-trailing")]
    [InlineData("Special---Symbols___All---Over", "special-symbols-all-over")]
    public void GenerateSlug_ValidNames_GeneratesExpectedSlug(string input, string expected)
    {
        string result = TenantSlugRules.GenerateSlug(input);
        Assert.Equal(expected, result);
        Assert.True(TenantSlugRules.IsValidSlug(result));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void GenerateSlug_NullOrWhitespace_ThrowsArgumentException(string? input)
    {
        Assert.Throws<ArgumentException>(() => TenantSlugRules.GenerateSlug(input!));
    }

    [Theory]
    [InlineData("A")]
    [InlineData("AB")]
    [InlineData("!!")]
    [InlineData("---")]
    public void GenerateSlug_TooShort_ThrowsArgumentException(string input)
    {
        Assert.Throws<ArgumentException>(() => TenantSlugRules.GenerateSlug(input));
    }

    [Fact]
    public void GenerateSlug_ExceedsMaxLength_TruncatesGracefully()
    {
        string veryLongName = new string('a', 50) + " " + new string('b', 50);
        string slug = TenantSlugRules.GenerateSlug(veryLongName);

        Assert.True(slug.Length <= TenantSlugRules.MaxLength);
        Assert.False(slug.EndsWith('-'));
        Assert.True(TenantSlugRules.IsValidSlug(slug));
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("api")]
    [InlineData("platform")]
    [InlineData("app")]
    [InlineData("www")]
    [InlineData("mail")]
    [InlineData("login")]
    [InlineData("register")]
    [InlineData("status")]
    [InlineData("health")]
    [InlineData("billing")]
    [InlineData("storefront")]
    [InlineData("portal")]
    public void IsReserved_SystemNames_ReturnsTrue(string reserved)
    {
        Assert.True(TenantSlugRules.IsReserved(reserved));
        Assert.Throws<ArgumentException>(() => TenantSlugRules.ValidateSubdomain(reserved));
    }

    [Theory]
    [InlineData("abc-retail")]
    [InlineData("keells-supermarkets")]
    [InlineData("lanka-retail-mart")]
    [InlineData("company-123")]
    public void IsReserved_StandardTenants_ReturnsFalse(string nonReserved)
    {
        Assert.False(TenantSlugRules.IsReserved(nonReserved));
        TenantSlugRules.ValidateSubdomain(nonReserved); // Should not throw
    }

    [Theory]
    [InlineData("ab")] // Too short (< 3)
    [InlineData("-abc-retail")] // Leading hyphen
    [InlineData("abc-retail-")] // Trailing hyphen
    [InlineData("abc--retail")] // Consecutive hyphens
    [InlineData("abc_retail")] // Underscore not permitted
    [InlineData("ABC-RETAIL")] // Uppercase not permitted in slug
    [InlineData("abc retail")] // Whitespace not permitted
    [InlineData("abc.retail")] // Dot not permitted in slug
    public void IsValidSlug_InvalidInputs_ReturnsFalse(string invalidSlug)
    {
        Assert.False(TenantSlugRules.IsValidSlug(invalidSlug));
    }

    [Fact]
    public async Task ResolveCollisionAsync_NoExistingCollision_ReturnsBaseSlug()
    {
        var existing = new HashSet<string>();
        string resolved = await TenantSlugRules.ResolveCollisionAsync(
            "lanka-retail-mart",
            s => Task.FromResult(existing.Contains(s)));

        Assert.Equal("lanka-retail-mart", resolved);
    }

    [Fact]
    public async Task ResolveCollisionAsync_SingleCollision_AppendsSuffix2()
    {
        var existing = new HashSet<string> { "abc-retail" };
        string resolved = await TenantSlugRules.ResolveCollisionAsync(
            "abc-retail",
            s => Task.FromResult(existing.Contains(s)));

        Assert.Equal("abc-retail-2", resolved);
    }

    [Fact]
    public async Task ResolveCollisionAsync_MultipleCollisions_IncrementsSuffix()
    {
        var existing = new HashSet<string> { "abc-retail", "abc-retail-2", "abc-retail-3" };
        string resolved = await TenantSlugRules.ResolveCollisionAsync(
            "abc-retail",
            s => Task.FromResult(existing.Contains(s)));

        Assert.Equal("abc-retail-4", resolved);
    }

    [Fact]
    public async Task ResolveCollisionAsync_NearMaxLength_TruncatesBaseToFitSuffix()
    {
        // 63 character base slug
        string maxBase = new string('a', 63);
        var existing = new HashSet<string> { maxBase };

        string resolved = await TenantSlugRules.ResolveCollisionAsync(
            maxBase,
            s => Task.FromResult(existing.Contains(s)));

        Assert.True(resolved.Length <= TenantSlugRules.MaxLength);
        Assert.EndsWith("-2", resolved);
        Assert.True(TenantSlugRules.IsValidSlug(resolved));
    }

    [Fact]
    public async Task RegisterTenant_WithFullMetadata_PersistsAndGeneratesSlug()
    {
        // Mock / in-memory repositories setup
        var mockRepo = new MockTenantRepository();
        var mockDomainRepo = new MockTenantDomainRepository();
        var mockSubService = new MockSubscriptionService();
        var mockUsageService = new MockUsageService();
        var tenantService = new TenantService(mockSubService, mockRepo, mockUsageService, mockDomainRepo);

        var request = new CreateTenantRequest
        {
            Name = "Ceylon Tea Exports",
            SubscriptionPlanId = Guid.NewGuid(),
            IndustryType = "Manufacturing",
            CompanyStructure = "Single",
            ECommerceEnabled = true,
            AdminFullName = "Nimal Perera",
            AdminEmail = "nimal@ceylontea.lk",
            AdminPhone = "+94771234567"
        };

        var result = await tenantService.RegisterTenant(request);

        Assert.NotNull(result);
        Assert.NotNull(result.Tenant);
        Assert.Equal("ceylon-tea-exports", result.Tenant.Subdomain);
        Assert.Equal("ceylon-tea-exports", result.Subdomain);
        Assert.Equal("ceylon-tea-exports.localhost", result.Hostname);
    }

    [Fact]
    public async Task RegisterTenant_WithDuplicateName_AutoResolvesSubdomainCollision()
    {
        var mockRepo = new MockTenantRepository();
        mockRepo.ExistingSubdomains.Add("ceylon-tea-exports");
        var mockDomainRepo = new MockTenantDomainRepository();

        var mockSubService = new MockSubscriptionService();
        var mockUsageService = new MockUsageService();
        var tenantService = new TenantService(mockSubService, mockRepo, mockUsageService, mockDomainRepo);

        var request = new CreateTenantRequest
        {
            Name = "Ceylon Tea Exports",
            SubscriptionPlanId = Guid.NewGuid()
        };

        var result = await tenantService.RegisterTenant(request);

        Assert.Equal("ceylon-tea-exports-2", result.Tenant.Subdomain);
    }

    [Fact]
    public async Task RegisterTenant_WithReservedExplicitSubdomain_ThrowsArgumentException()
    {
        var mockRepo = new MockTenantRepository();
        var mockDomainRepo = new MockTenantDomainRepository();
        var mockSubService = new MockSubscriptionService();
        var mockUsageService = new MockUsageService();
        var tenantService = new TenantService(mockSubService, mockRepo, mockUsageService, mockDomainRepo);

        var request = new CreateTenantRequest
        {
            Name = "Global Admin Portal",
            Subdomain = "admin",
            SubscriptionPlanId = Guid.NewGuid()
        };

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => tenantService.RegisterTenant(request));
        Assert.Contains("reserved", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RegisterTenant_WithInvalidCompanyStructure_ThrowsArgumentException()
    {
        var mockRepo = new MockTenantRepository();
        var mockDomainRepo = new MockTenantDomainRepository();
        var mockSubService = new MockSubscriptionService();
        var mockUsageService = new MockUsageService();
        var tenantService = new TenantService(mockSubService, mockRepo, mockUsageService, mockDomainRepo);

        var request = new CreateTenantRequest
        {
            Name = "Valid Name Corp",
            CompanyStructure = "InvalidStructure",
            SubscriptionPlanId = Guid.NewGuid()
        };

        await Assert.ThrowsAsync<ArgumentException>(() => tenantService.RegisterTenant(request));
    }

    // Lightweight mock implementations for unit testing
    private class MockTenantRepository : ITenantRepository
    {
        public readonly List<Tenant> Tenants = new();
        public readonly HashSet<string> ExistingSubdomains = new(StringComparer.OrdinalIgnoreCase);

        public Task AddAsync(Tenant tenant)
        {
            Tenants.Add(tenant);
            ExistingSubdomains.Add(tenant.Subdomain);
            return Task.CompletedTask;
        }

        public Task<Tenant?> GetByIdAsync(Guid tenantId) =>
            Task.FromResult(Tenants.Find(t => t.Id == tenantId));

        public Task<bool> ExistsByCodeAsync(string code) =>
            Task.FromResult(Tenants.Exists(t => t.Code == code));

        public Task<bool> ExistsBySubdomainAsync(string subdomain) =>
            Task.FromResult(ExistingSubdomains.Contains(subdomain));

        public Task<Tenant?> GetBySubdomainAsync(string subdomain) =>
            Task.FromResult(Tenants.Find(t => string.Equals(t.Subdomain, subdomain, StringComparison.OrdinalIgnoreCase)));

        public Task SaveChangesAsync() => Task.CompletedTask;
        public Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> BeginTransactionAsync() =>
            Task.FromResult<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction>(new MockTransaction());
        public Task CommitTransactionAsync(Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction) => Task.CompletedTask;
        public Task RollbackTransactionAsync(Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction) => Task.CompletedTask;
        public Task<List<TenantQueueItemDto>> GetTenantListAsync() => Task.FromResult(new List<TenantQueueItemDto>());
        public Task<TenantDetailsDto?> GetTenantDetailsAsync(Guid tenantId) => Task.FromResult<TenantDetailsDto?>(null);
        public Task<TenantLifecycleStatusDto?> GetTenantStatusAsync(Guid tenantId) => Task.FromResult<TenantLifecycleStatusDto?>(null);
        public Task<TenantMetricsDto> GetTenantMetricsAsync() => Task.FromResult(new TenantMetricsDto());
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

    private class MockSubscriptionService : ISubscriptionService
    {
        public Subscription CreateSubscription(Guid tenantId, Guid subscriptionPlanId) =>
            new()
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                SubscriptionPlanId = subscriptionPlanId,
                SubscriptionName = "Enterprise Plan",
                SubscriptionType = "Paid",
                StorageMode = TenantStorageMode.Shared,
                Status = SubscriptionStatus.Active,
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddYears(1),
                IsActive = true
            };

        public bool IsSubscriptionActive(Guid tenantId) => true;

        public bool IsWithinLimit(Guid tenantId, string parameterKey, decimal currentValue) => true;
    }

    private class MockUsageService : ISubscriptionUsageService
    {
        public Task InitializeUsageAsync(Subscription subscription) => Task.CompletedTask;

        public Task<T> ExecuteWithUsageLimitAsync<T>(
            Guid tenantId,
            string parameterKey,
            decimal increment,
            Func<Task<T>> createEntityAction,
            System.Threading.CancellationToken cancellationToken = default)
        {
            return createEntityAction();
        }
    }

    private class MockTransaction : Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction
    {
        public Guid TransactionId => Guid.NewGuid();
        public void Commit() { }
        public Task CommitAsync(System.Threading.CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Rollback() { }
        public Task RollbackAsync(System.Threading.CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Dispose() { }
        public System.Threading.Tasks.ValueTask DisposeAsync() => System.Threading.Tasks.ValueTask.CompletedTask;
    }
}
