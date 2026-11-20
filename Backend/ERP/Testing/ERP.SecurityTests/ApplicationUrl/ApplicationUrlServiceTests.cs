using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using API.Services;
using Domain.Common.ApplicationUrl;
using Domain.Features.MasterData.Company;
using Domain.Features.MasterData.Product;
using Domain.Features.MasterData.UnitOfMeasure;
using Domain.Features.Procurement.RequestForQuotation;
using Domain.Infrastructure.Persistence;
using ERP.Application.Common.Email;
using ERP.Application.Features.Procurement.RFQ;
using ERP.Domain.Features.MasterData.Supplier;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace ERP.SecurityTests.ApplicationUrl;

public class ApplicationUrlServiceTests
{
    private sealed class MockHttpRequestOriginAccessor : IHttpRequestOriginAccessor
    {
        public string? RequestOrigin { get; set; }

        public string? GetRequestOrigin() => RequestOrigin;
    }

    private static ApplicationUrlService CreateService(
        IHttpRequestOriginAccessor? accessor = null,
        ApplicationUrlOptions? options = null)
    {
        var opt = options ?? new ApplicationUrlOptions
        {
            FallbackBaseUrl = "http://localhost:4200",
            TenantBaseDomain = "apexerp.com",
            AllowLocalNetworkOrigins = true,
            AllowedOrigins = new List<string> { "https://erp.example.com", "https://company.example.com" }
        };
        return new ApplicationUrlService(Options.Create(opt), accessor, NullLogger<ApplicationUrlService>.Instance);
    }

    // 1. Localhost URL generation
    [Fact]
    public void Test01_Localhost_Url_Generation()
    {
        var service = CreateService();
        var url = service.BuildAbsoluteUrl("/public/rfq-submit", new Dictionary<string, string?> { ["token"] = "abc123token" }, "http://localhost:4200");
        Assert.Equal("http://localhost:4200/public/rfq-submit?token=abc123token", url);
    }

    // 2. LAN IP URL generation
    [Fact]
    public void Test02_LanIp_Url_Generation()
    {
        var service = CreateService();
        var url = service.BuildAbsoluteUrl("/public/rfq-submit", new Dictionary<string, string?> { ["token"] = "lanToken" }, "http://192.168.1.10:4200");
        Assert.Equal("http://192.168.1.10:4200/public/rfq-submit?token=lanToken", url);
    }

    // 3. LAN hostname URL generation
    [Fact]
    public void Test03_LanHostname_Url_Generation()
    {
        var service = CreateService();
        var url = service.BuildAbsoluteUrl("/public/rfq-submit", new Dictionary<string, string?> { ["token"] = "hostToken" }, "http://erp-server:4200");
        Assert.Equal("http://erp-server:4200/public/rfq-submit?token=hostToken", url);
    }

    // 4. Hosted domain URL generation
    [Fact]
    public void Test04_HostedDomain_Url_Generation()
    {
        var service = CreateService();
        var url = service.BuildAbsoluteUrl("/public/rfq-submit", new Dictionary<string, string?> { ["token"] = "cloudToken" }, "https://erp.example.com");
        Assert.Equal("https://erp.example.com/public/rfq-submit?token=cloudToken", url);
    }

    // 5. Hosted subdomain URL generation
    [Fact]
    public void Test05_HostedSubdomain_Url_Generation()
    {
        var service = CreateService();
        var url = service.BuildAbsoluteUrl("/public/rfq-submit", new Dictionary<string, string?> { ["token"] = "subToken" }, "https://company.example.com");
        Assert.Equal("https://company.example.com/public/rfq-submit?token=subToken", url);
    }

    // 6. HTTP scheme preservation
    [Fact]
    public void Test06_HttpScheme_Preservation()
    {
        var service = CreateService();
        var url = service.BuildAbsoluteUrl("/public/rfq-submit", clientOrigin: "http://erp-internal.local:8080");
        Assert.StartsWith("http://", url);
        Assert.Contains("erp-internal.local:8080", url);
    }

    // 7. HTTPS scheme preservation
    [Fact]
    public void Test07_HttpsScheme_Preservation()
    {
        var service = CreateService();
        var url = service.BuildAbsoluteUrl("/public/rfq-submit", clientOrigin: "https://secure.apexerp.com");
        Assert.StartsWith("https://", url);
        Assert.Contains("secure.apexerp.com", url);
    }

    // 8. Non-default port preservation (:4200, :8443, :8080)
    [Theory]
    [InlineData("http://localhost:4200", 4200)]
    [InlineData("https://company.example.com:8443", 8443)]
    [InlineData("http://10.0.0.50:8080", 8080)]
    public void Test08_NonDefaultPort_Preservation(string origin, int expectedPort)
    {
        var service = CreateService();
        var url = service.BuildAbsoluteUrl("/public/rfq-submit", clientOrigin: origin);
        var uri = new Uri(url);
        Assert.Equal(expectedPort, uri.Port);
    }

    // 9. Separate Angular frontend and API origins (explicit clientOrigin takes precedence over accessor origin)
    [Fact]
    public void Test09_SeparateAngularAndApiOrigins()
    {
        var accessor = new MockHttpRequestOriginAccessor
        {
            RequestOrigin = "http://localhost:5000" // Backend API origin
        };
        var service = CreateService(accessor);

        // When Angular passes window.location.origin
        var url = service.BuildAbsoluteUrl("/public/rfq-submit", clientOrigin: "http://localhost:4200");
        Assert.StartsWith("http://localhost:4200/public/rfq-submit", url);
    }

    // 10. Route and query-string handling (parameter escaping)
    [Fact]
    public void Test10_RouteAndQueryStringHandling_ParameterEscaping()
    {
        var service = CreateService();
        var queryParams = new Dictionary<string, string?>
        {
            ["token"] = "token with spaces & symbols=true",
            ["ref"] = "RFQ#2026/01"
        };
        var url = service.BuildAbsoluteUrl("/public/rfq-submit", queryParams, "http://localhost:4200");
        Assert.Contains("token=token%20with%20spaces%20%26%20symbols%3Dtrue", url);
        Assert.Contains("ref=RFQ%232026%2F01", url);
    }

    // 11. Invalid/malicious origin rejection
    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("ftp://malicious.com")]
    [InlineData("file:///etc/passwd")]
    [InlineData("data:text/html,hack")]
    [InlineData("https://untrusted-hacker-site.ru")]
    public void Test11_InvalidMaliciousOrigin_Rejection(string maliciousOrigin)
    {
        var service = CreateService();
        // Should fall back to configured fallback instead of accepting malicious origin
        var resolved = service.ResolveBaseUrl(maliciousOrigin);
        Assert.Equal("http://localhost:4200", resolved);
    }

    // 12. Host-header injection prevention (CRLF, line breaks)
    [Theory]
    [InlineData("http://localhost:4200\r\nInjected-Header: evil")]
    [InlineData("http://localhost:4200%0d%0aInjected: evil")]
    [InlineData("http://localhost:4200\nevil.com")]
    public void Test12_HostHeaderInjection_Prevention(string injectionAttempt)
    {
        var service = CreateService();
        var resolved = service.ResolveBaseUrl(injectionAttempt);
        Assert.DoesNotContain("\r", resolved);
        Assert.DoesNotContain("\n", resolved);
        Assert.DoesNotContain("%0d", resolved, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("%0a", resolved, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("http://localhost:4200", resolved);
    }

    // 13. Trusted reverse-proxy forwarded-header handling
    [Fact]
    public void Test13_ReverseProxyForwardedHeader_Handling()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Forwarded-Proto"] = "https";
        httpContext.Request.Headers["X-Forwarded-Host"] = "company.example.com";
        httpContext.Request.Headers["X-Forwarded-Port"] = "8443";

        var contextAccessor = new HttpContextAccessor { HttpContext = httpContext };
        var originAccessor = new HttpRequestOriginAccessor(contextAccessor);

        var forwardedOrigin = originAccessor.GetRequestOrigin();
        Assert.Equal("https://company.example.com:8443", forwardedOrigin);

        var service = CreateService(originAccessor);
        var resolved = service.ResolveBaseUrl();
        Assert.Equal("https://company.example.com:8443", resolved);
    }

    // 14. Configured fallback when no browser request exists
    [Fact]
    public void Test14_ConfiguredFallback_WhenNoBrowserRequest()
    {
        var emptyAccessor = new MockHttpRequestOriginAccessor();
        var options = new ApplicationUrlOptions
        {
            FallbackBaseUrl = "https://erp.example.com:8443",
            AllowedOrigins = new List<string> { "https://erp.example.com:8443" }
        };
        var service = CreateService(emptyAccessor, options);

        var resolved = service.ResolveBaseUrl();
        Assert.Equal("https://erp.example.com:8443", resolved);
    }

    // 15. Supplier Invitation email contains the single correct generated URL
    [Fact]
    public async Task Test15_SupplierInvitationEmail_ContainsSingleUrl()
    {
        using var db = CreateInMemoryDomainDb();
        var tenantId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var company = new Company
        {
            Id = companyId,
            TenantId = tenantId,
            Name = "Test Buyer Corp",
            Code = "TBC"
        };
        await db.Companies.AddAsync(company);

        var supplier = Supplier.Create(tenantId, "SUP-01", "Acme Supplies", email: "acme@example.com");
        await db.Suppliers.AddAsync(supplier);

        var uom = UnitOfMeasure.Create(tenantId, "PCS", "Pieces");
        await db.UnitsOfMeasure.AddAsync(uom);

        var product = Product.Create(tenantId, "PROD-01", "Industrial Widget", uom.UnitOfMeasureId, "General");
        await db.Products.AddAsync(product);
        await db.SaveChangesAsync();

        var tokenService = new RfqTokenService(db);
        var stubEmail = new StubEmailService();
        var emailNotificationService = new RfqEmailNotificationService(stubEmail);

        var urlService = CreateService();
        var rfqService = new RFQService(db, tokenService, emailNotificationService, null, urlService);

        var rfq = await rfqService.CreateRfqAsync(new CreateRfqRequest
        {
            Title = "Materials RFQ",
            ClosingDate = DateTime.UtcNow.AddDays(7),
            Lines = new List<CreateRfqLineRequest>
            {
                new() { ProductId = product.ProductId, Quantity = 100, UnitOfMeasureId = uom.UnitOfMeasureId }
            },
            SupplierIds = new List<Guid> { supplier.SupplierId }
        }, userId, tenantId, companyId, branchId);

        // Send invitation with custom LAN IP browser origin
        var sendRequest = new SendRfqInvitationsRequest
        {
            SupplierIds = new List<Guid> { supplier.SupplierId },
            PortalBaseUrl = "http://192.168.1.100:4200"
        };

        var result = await rfqService.SendInvitationsAsync(rfq.Id, sendRequest, userId, tenantId);
        Assert.Equal(1, result.SentCount);
        Assert.NotNull(result.InvitationUrl);
        Assert.StartsWith("http://192.168.1.100:4200/public/rfq-submit?token=", result.InvitationUrl);

        // Verify email HTML contains ONE submission link and no confusing "Option 1 / Option 2 / Option 3"
        Assert.NotNull(stubEmail.LastMessage);
        var capturedEmailHtml = stubEmail.LastMessage!.HtmlBody;
        Assert.NotEmpty(capturedEmailHtml);
        Assert.Contains("Quotation Submission Portal", capturedEmailHtml);
        Assert.Contains("http://192.168.1.100:4200/public/rfq-submit?token=", capturedEmailHtml);
        Assert.DoesNotContain("Option 1: Standard Localhost", capturedEmailHtml);
        Assert.DoesNotContain("Option 2: Localhost Tenant Subdomain", capturedEmailHtml);
        Assert.DoesNotContain("Option 3: External / Hosted Tenant Portal", capturedEmailHtml);
    }

    // 16. Token security, hashing, expiry, and validation remain unchanged
    [Fact]
    public async Task Test16_TokenSecurity_Hashing_Expiry_Unchanged()
    {
        using var db = CreateInMemoryDomainDb();
        var tenantId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var company = new Company
        {
            Id = companyId,
            TenantId = tenantId,
            Name = "Secure Buyer Co",
            Code = "SBC"
        };
        await db.Companies.AddAsync(company);

        var supplier = Supplier.Create(tenantId, "SUP-SEC", "Secure Supplier", email: "sec@example.com");
        await db.Suppliers.AddAsync(supplier);

        var uom = UnitOfMeasure.Create(tenantId, "EA", "Each");
        await db.UnitsOfMeasure.AddAsync(uom);

        var product = Product.Create(tenantId, "SEC-01", "Secure Item", uom.UnitOfMeasureId, "General");
        await db.Products.AddAsync(product);
        await db.SaveChangesAsync();

        var tokenService = new RfqTokenService(db);
        var urlService = CreateService();
        var rfqService = new RFQService(db, tokenService, null, null, urlService);

        var rfq = await rfqService.CreateRfqAsync(new CreateRfqRequest
        {
            Title = "Security Audit RFQ",
            ClosingDate = DateTime.UtcNow.AddDays(5),
            Lines = new List<CreateRfqLineRequest>
            {
                new() { ProductId = product.ProductId, Quantity = 10, UnitOfMeasureId = uom.UnitOfMeasureId }
            },
            SupplierIds = new List<Guid> { supplier.SupplierId }
        }, userId, tenantId, companyId, branchId);

        // Generate token
        var (plaintextToken, tokenHash) = tokenService.GenerateToken();
        Assert.False(string.IsNullOrWhiteSpace(plaintextToken));
        Assert.False(string.IsNullOrWhiteSpace(tokenHash));
        Assert.NotEqual(plaintextToken, tokenHash);

        // Verify SHA-256 hash matches
        var recomputedHash = tokenService.ComputeHash(plaintextToken);
        Assert.Equal(tokenHash, recomputedHash);

        // Assign to supplier record and verify quotation retrieval
        var rfqSupplier = await db.RFQSuppliers.FirstAsync(s => s.RFQId == rfq.Id && s.SupplierId == supplier.SupplierId);
        rfqSupplier.TokenHash = tokenHash;
        rfqSupplier.TokenExpiresAt = rfq.ClosingDate.AddDays(2);
        await db.SaveChangesAsync();

        var quoteService = new SupplierQuotationService(db, tokenService);
        var publicDetails = await quoteService.GetPublicRfqDetailsByTokenAsync(plaintextToken);
        Assert.NotNull(publicDetails);
        Assert.Equal("Secure Buyer Co", publicDetails!.CompanyName);
        Assert.Equal("Secure Supplier", publicDetails.SupplierName);
        Assert.Equal(rfq.RFQNumber, publicDetails.RFQNumber);

        // Invalid token rejection
        var invalidDetails = await quoteService.GetPublicRfqDetailsByTokenAsync("totally-invalid-token");
        Assert.Null(invalidDetails);
    }

    private static DomainDbContext CreateInMemoryDomainDb()
    {
        var options = new DbContextOptionsBuilder<DomainDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new DomainDbContext(options);
    }

    private sealed class StubEmailService : IEmailService
    {
        public EmailMessage? LastMessage { get; private set; }

        public Task<EmailResult> SendEmailAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            LastMessage = message;
            return Task.FromResult(EmailResult.Success());
        }

        public Task<EmailResult> SendEmailAsync(string to, string subject, string body, bool isHtml = true, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(EmailResult.Success());
        }

        public Task<EmailResult> SendEmailWithAttachmentAsync(
            string to,
            string subject,
            string body,
            byte[] attachmentBytes,
            string attachmentFilename,
            string attachmentContentType = "application/pdf",
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(EmailResult.Success());
        }
    }
}
