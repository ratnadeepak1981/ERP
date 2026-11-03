using System;
using SaaS.Core.Models;
using SaaS.Core.Rules;
using Xunit;

namespace ERP.SecurityTests;

public class TenantDomainModelTests
{
    [Theory]
    [InlineData("retail")]
    [InlineData("wholesale")]
    [InlineData("hardware")]
    [InlineData("company-1")]
    [InlineData("my-store-123")]
    public void IsValidSubdomain_ValidSubdomains_ReturnsTrue(string subdomain)
    {
        bool isValid = TenantDomainRules.IsValidSubdomain(subdomain);
        Assert.True(isValid);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ab")] // Too short (< 3 chars)
    [InlineData("-invalid")] // Starts with hyphen
    [InlineData("invalid-")] // Ends with hyphen
    [InlineData("invalid.sub")] // Contains dot
    [InlineData("invalid_sub")] // Contains underscore
    [InlineData("invalid@sub")] // Special characters
    public void IsValidSubdomain_InvalidFormat_ReturnsFalse(string? subdomain)
    {
        bool isValid = TenantDomainRules.IsValidSubdomain(subdomain);
        Assert.False(isValid);
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("api")]
    [InlineData("platform")]
    [InlineData("app")]
    [InlineData("www")]
    [InlineData("mail")]
    [InlineData("hangfire")]
    [InlineData("swagger")]
    [InlineData("auth")]
    [InlineData("support")]
    [InlineData("billing")]
    public void IsValidSubdomain_ReservedSubdomains_ReturnsFalse(string reservedSubdomain)
    {
        bool isReserved = TenantDomainRules.IsReservedSubdomain(reservedSubdomain);
        bool isValid = TenantDomainRules.IsValidSubdomain(reservedSubdomain);

        Assert.True(isReserved);
        Assert.False(isValid);
    }

    [Fact]
    public void BuildLocalhostHostname_ValidSubdomain_ReturnsLocalhostHostname()
    {
        string hostname = TenantDomainRules.BuildLocalhostHostname("retail");
        Assert.Equal("retail.localhost", hostname);

        string caseInsensitive = TenantDomainRules.BuildLocalhostHostname("WHOLESALE");
        Assert.Equal("wholesale.localhost", caseInsensitive);
    }

    [Fact]
    public void BuildLocalhostHostname_ReservedOrInvalidSubdomain_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => TenantDomainRules.BuildLocalhostHostname("admin"));
        Assert.Throws<ArgumentException>(() => TenantDomainRules.BuildLocalhostHostname("ab"));
    }

    [Fact]
    public void TenantDomain_DefaultProperties_InitializedCorrectly()
    {
        var domain = new TenantDomain
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Hostname = "retail.localhost",
            Subdomain = "retail",
            DomainType = TenantDomainType.Subdomain,
            IsPrimary = true,
            IsActive = true,
            IsVerified = true
        };

        Assert.NotEqual(Guid.Empty, domain.Id);
        Assert.NotEqual(Guid.Empty, domain.TenantId);
        Assert.Equal("retail.localhost", domain.Hostname);
        Assert.Equal("retail", domain.Subdomain);
        Assert.Equal(TenantDomainType.Subdomain, domain.DomainType);
        Assert.True(domain.IsPrimary);
        Assert.True(domain.IsActive);
        Assert.True(domain.IsVerified);
    }
}
