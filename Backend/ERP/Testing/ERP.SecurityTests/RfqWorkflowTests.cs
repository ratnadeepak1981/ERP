using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Domain.Features.MasterData.Company;
using Domain.Features.MasterData.Currency;
using Domain.Features.MasterData.Product;
using Domain.Features.MasterData.UnitOfMeasure;
using Domain.Features.Procurement.RequestForQuotation;
using Domain.Infrastructure.Persistence;
using ERP.Domain.Features.MasterData.Supplier;
using ERP.Infrastructure.Persistence.Auditing;
using Microsoft.EntityFrameworkCore;
using Security.Bootstrap.Permissions;
using Security.Bootstrap.Roles;
using Xunit;

namespace ERP.SecurityTests;

public class RfqWorkflowTests
{
    private sealed class StubRfqEmailNotificationService : IRfqEmailNotificationService
    {
        public RfqEmailDispatchResult Result { get; set; } = new(true);
        public int CallCount { get; private set; }
        public int AwardCallCount { get; private set; }
        public string? LastSubmissionUrl { get; private set; }
        public RfqInvitationEmailContext? LastContext { get; private set; }
        public RfqAwardEmailContext? LastAwardContext { get; private set; }

        public Task<RfqEmailDispatchResult> SendInvitationAsync(
            string recipientEmail,
            string recipientName,
            string rfqNumber,
            string rfqTitle,
            string submissionUrl,
            DateTime responseDeadline,
            CancellationToken ct = default)
        {
            CallCount++;
            LastSubmissionUrl = submissionUrl;
            return Task.FromResult(Result);
        }

        public Task<RfqEmailDispatchResult> SendInvitationAsync(
            RfqInvitationEmailContext context,
            CancellationToken ct = default)
        {
            CallCount++;
            LastContext = context;
            LastSubmissionUrl = context.StandardLocalhostUrl;
            return Task.FromResult(Result);
        }

        public Task<RfqEmailDispatchResult> SendAwardNotificationAsync(
            RfqAwardEmailContext context,
            CancellationToken ct = default)
        {
            AwardCallCount++;
            LastAwardContext = context;
            return Task.FromResult(Result);
        }
    }

    private static DomainDbContext CreateInMemoryDomainDb()
    {
        var options = new DbContextOptionsBuilder<DomainDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new DomainDbContext(options);
    }

    [Fact]
    public void PermissionCatalogue_ContainsAllTenCanonicalRfqPermissions()
    {
        var expectedRfqPermissions = new[]
        {
            "RFQ.VIEW",
            "RFQ.CREATE",
            "RFQ.EDIT",
            "RFQ.SUPPLIER.MANAGE",
            "RFQ.SEND",
            "RFQ.QUOTATION.RECORD",
            "RFQ.QUOTATION.VIEW",
            "RFQ.COMPARE",
            "RFQ.APPROVE",
            "RFQ.REJECT"
        };

        foreach (var perm in expectedRfqPermissions)
        {
            Assert.Contains(perm, PermissionCatalogue.TenantCodes);
        }
    }

    [Fact]
    public void StandardRoleTemplates_AssignsRfqPermissionsAppropriately()
    {
        var procManager = StandardRoleTemplates.Find("PROCUREMENT_MANAGER");
        var procOfficer = StandardRoleTemplates.Find("PROCUREMENT_OFFICER");
        var procViewer = StandardRoleTemplates.Find("PROCUREMENT_VIEWER");

        Assert.NotNull(procManager);
        Assert.NotNull(procOfficer);
        Assert.NotNull(procViewer);

        // Manager has all 10
        Assert.Contains("RFQ.VIEW", procManager.PermissionCodes);
        Assert.Contains("RFQ.CREATE", procManager.PermissionCodes);
        Assert.Contains("RFQ.EDIT", procManager.PermissionCodes);
        Assert.Contains("RFQ.SUPPLIER.MANAGE", procManager.PermissionCodes);
        Assert.Contains("RFQ.SEND", procManager.PermissionCodes);
        Assert.Contains("RFQ.QUOTATION.RECORD", procManager.PermissionCodes);
        Assert.Contains("RFQ.QUOTATION.VIEW", procManager.PermissionCodes);
        Assert.Contains("RFQ.COMPARE", procManager.PermissionCodes);
        Assert.Contains("RFQ.APPROVE", procManager.PermissionCodes);
        Assert.Contains("RFQ.REJECT", procManager.PermissionCodes);

        // Officer has operational
        Assert.Contains("RFQ.VIEW", procOfficer.PermissionCodes);
        Assert.Contains("RFQ.CREATE", procOfficer.PermissionCodes);
        Assert.Contains("RFQ.EDIT", procOfficer.PermissionCodes);
        Assert.Contains("RFQ.SUPPLIER.MANAGE", procOfficer.PermissionCodes);
        Assert.Contains("RFQ.SEND", procOfficer.PermissionCodes);
        Assert.Contains("RFQ.QUOTATION.RECORD", procOfficer.PermissionCodes);
        Assert.Contains("RFQ.QUOTATION.VIEW", procOfficer.PermissionCodes);
        Assert.Contains("RFQ.COMPARE", procOfficer.PermissionCodes);
        Assert.DoesNotContain("RFQ.APPROVE", procOfficer.PermissionCodes);
        Assert.DoesNotContain("RFQ.REJECT", procOfficer.PermissionCodes);

        // Viewer has view only
        Assert.Contains("RFQ.VIEW", procViewer.PermissionCodes);
        Assert.Contains("RFQ.QUOTATION.VIEW", procViewer.PermissionCodes);
        Assert.DoesNotContain("RFQ.COMPARE", procViewer.PermissionCodes);
        Assert.DoesNotContain("RFQ.CREATE", procViewer.PermissionCodes);
        Assert.DoesNotContain("RFQ.APPROVE", procViewer.PermissionCodes);
    }

    [Fact]
    public void RfqTokenService_GeneratesSecureToken_AndComputesConsistentHash()
    {
        using var db = CreateInMemoryDomainDb();
        var tokenService = new RfqTokenService(db);
        var (token, hash) = tokenService.GenerateToken();

        Assert.NotNull(token);
        Assert.True(token.Length >= 40);

        Assert.NotNull(hash);
        Assert.Equal(64, hash.Length); // 256-bit SHA-256 produces 64 hex characters

        var computedHash = tokenService.ComputeHash(token);
        Assert.Equal(hash, computedHash);
    }

    [Fact]
    public async Task AddSupplierAsync_AddsSupplierToDraftRfq_Successfully()
    {
        using var db = CreateInMemoryDomainDb();
        var tokenService = new RfqTokenService(db);
        var emailService = new StubRfqEmailNotificationService();
        var rfqService = new RFQService(db, tokenService, emailService);

        var tenantId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var creatorUserId = Guid.NewGuid();

        var uom = UnitOfMeasure.Create(tenantId, "KG", "Kilogram");
        await db.UnitsOfMeasure.AddAsync(uom);
        var product = Product.Create(tenantId, "RICE-01", "Samba Rice 5kg", uom.UnitOfMeasureId, "Food");
        await db.Products.AddAsync(product);

        var supplierA = Supplier.Create(tenantId, "SUP-01", "Supplier A (Pvt) Ltd", email: "suppliera@test.lk");
        var supplierB = Supplier.Create(tenantId, "SUP-02", "Supplier B PLC", email: "supplierb@test.lk");
        await db.Suppliers.AddRangeAsync(supplierA, supplierB);
        await db.SaveChangesAsync();

        var createRequest = new CreateRfqRequest
        {
            Title = "Test RFQ",
            ClosingDate = DateTime.UtcNow.AddDays(14),
            Lines = new List<CreateRfqLineRequest>
            {
                new()
                {
                    ProductId = product.ProductId,
                    Quantity = 100m,
                    UnitOfMeasureId = uom.UnitOfMeasureId
                }
            },
            SupplierIds = new List<Guid> { supplierA.SupplierId }
        };

        var rfq = await rfqService.CreateRfqAsync(createRequest, creatorUserId, tenantId, companyId, branchId);
        Assert.Single(rfq.InvitedSuppliers);

        var addRequest = new AddRfqSupplierRequest { SupplierId = supplierB.SupplierId };
        var updated = await rfqService.AddSupplierAsync(rfq.Id, addRequest, creatorUserId, tenantId);
        Assert.Equal(2, updated.InvitedSuppliers.Count);
    }

    [Fact]
    public async Task RfqLifecycle_Draft_Issued_Awarded_WithMultiSupplierAndSelfApprovalGuard()
    {
        using var db = CreateInMemoryDomainDb();
        var tokenService = new RfqTokenService(db);
        var emailService = new StubRfqEmailNotificationService();
        var rfqService = new RFQService(db, tokenService, emailService);
        var quoteService = new SupplierQuotationService(db, tokenService);

        var tenantId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var creatorUserId = Guid.NewGuid();
        var approverUserId = Guid.NewGuid();

        // Seed Master Data
        var uom = UnitOfMeasure.Create(tenantId, "KG", "Kilogram");
        await db.UnitsOfMeasure.AddAsync(uom);

        var currency = Currency.Create(tenantId, "LKR", "Sri Lankan Rupee", "Rs", 2);
        await db.Currencies.AddAsync(currency);

        var product = Product.Create(tenantId, "RICE-01", "Samba Rice 5kg", uom.UnitOfMeasureId, "Food");
        await db.Products.AddAsync(product);

        var supplierA = Supplier.Create(tenantId, "SUP-01", "Supplier A (Pvt) Ltd", email: "suppliera@test.lk");
        var supplierB = Supplier.Create(tenantId, "SUP-02", "Supplier B PLC", email: "supplierb@test.lk");
        await db.Suppliers.AddRangeAsync(supplierA, supplierB);
        await db.SaveChangesAsync();

        // 1. Create RFQ Draft
        var createRequest = new CreateRfqRequest
        {
            Title = "Annual Rice Procurement 2026",
            ClosingDate = DateTime.UtcNow.AddDays(14),
            DeliveryRequiredDate = DateTime.UtcNow.AddDays(30),
            Lines = new List<CreateRfqLineRequest>
            {
                new()
                {
                    ProductId = product.ProductId,
                    Quantity = 500m,
                    UnitOfMeasureId = uom.UnitOfMeasureId,
                    TargetUnitPrice = 250m,
                    TechnicalSpecifications = "Moisture < 12%"
                }
            },
            SupplierIds = new List<Guid> { supplierA.SupplierId, supplierB.SupplierId }
        };

        var rfq = await rfqService.CreateRfqAsync(createRequest, creatorUserId, tenantId, companyId, branchId);
        Assert.NotNull(rfq);
        Assert.Equal(RequestForQuotationStatus.Draft, rfq.Status);
        Assert.Single(rfq.Lines);
        Assert.Equal(2, rfq.InvitedSuppliers.Count);

        // 2. Send Invitations (Transitions to Issued)
        var inviteResult = await rfqService.SendInvitationsAsync(rfq.Id, new SendRfqInvitationsRequest(), creatorUserId, tenantId);
        Assert.Equal(2, inviteResult.SentCount);
        Assert.Equal(0, inviteResult.FailedCount);
        Assert.Equal(2, emailService.CallCount);

        var updatedRfq = await rfqService.GetRfqByIdAsync(rfq.Id, tenantId);
        Assert.Equal(RequestForQuotationStatus.Issued, updatedRfq!.Status);
        Assert.All(updatedRfq.InvitedSuppliers, supplier => Assert.Equal(RFQSupplierInvitationStatus.Sent, supplier.InvitationStatus));

        var repeatedInviteResult = await rfqService.SendInvitationsAsync(
            rfq.Id,
            new SendRfqInvitationsRequest(),
            creatorUserId,
            tenantId);
        Assert.Equal(0, repeatedInviteResult.SentCount);
        Assert.Equal(2, emailService.CallCount);

        // Verify tokens were generated
        var rfqEntity = await db.RequestForQuotations.Include(r => r.InvitedSuppliers).FirstAsync(r => r.Id == rfq.Id);
        var rfqSupA = rfqEntity.InvitedSuppliers.First(s => s.SupplierId == supplierA.SupplierId);
        var rfqSupB = rfqEntity.InvitedSuppliers.First(s => s.SupplierId == supplierB.SupplierId);
        Assert.NotNull(rfqSupA.TokenHash);
        Assert.NotNull(rfqSupB.TokenHash);
        Assert.NotEqual(rfqSupA.TokenHash, rfqSupB.TokenHash);

        // 3. Public Supplier Portal - Supplier A view & submission
        var (tokenA, tokenAHash) = tokenService.GenerateToken();
        rfqSupA.TokenHash = tokenAHash;
        await db.SaveChangesAsync();

        var publicViewA = await quoteService.GetPublicRfqDetailsByTokenAsync(tokenA);
        Assert.NotNull(publicViewA);
        Assert.Equal("Supplier A (Pvt) Ltd", publicViewA.SupplierName);
        Assert.Single(publicViewA.Lines);
        // Supplier confidentiality: target price property does not exist on PublicRfqLineDto
        Assert.Equal(500m, publicViewA.Lines[0].Quantity);

        // Submit Quotation A
        var submitA = new SubmitPublicQuotationRequest
        {
            QuotationReference = "QUO-SUPA-2026-001",
            QuotationDate = DateTime.UtcNow,
            ValidUntil = DateTime.UtcNow.AddDays(30),
            CurrencyId = currency.CurrencyId,
            Lines = new List<SubmitPublicQuotationLineRequest>
            {
                new()
                {
                    RFQLineId = publicViewA.Lines[0].RFQLineId,
                    QuotedQuantity = 500m,
                    UnitOfMeasureId = uom.UnitOfMeasureId,
                    UnitPrice = 240m,
                    DiscountPercentage = 2m,
                    LeadTimeDays = 7
                }
            }
        };
        var submitResultA = await quoteService.SubmitPublicQuotationAsync(tokenA, submitA);
        Assert.True(submitResultA.Success);

        // 4. Record Manual Quotation for Supplier B (Offline receipt)
        var manualB = new RecordManualQuotationRequest
        {
            SupplierId = supplierB.SupplierId,
            QuotationReference = "QUO-SUPB-OFFLINE-99",
            QuotationDate = DateTime.UtcNow,
            ValidUntil = DateTime.UtcNow.AddDays(30),
            CurrencyId = currency.CurrencyId,
            ExchangeRate = 1.0m,
            Lines = new List<RecordQuotationLineRequest>
            {
                new()
                {
                    RFQLineId = rfqEntity.Lines.First().Id,
                    ProductId = product.ProductId,
                    QuotedQuantity = 500m,
                    UnitOfMeasureId = uom.UnitOfMeasureId,
                    UnitPrice = 230m,
                    DiscountPercentage = 0m,
                    LeadTimeDays = 5
                }
            }
        };
        var quoteB = await quoteService.RecordManualQuotationAsync(rfq.Id, manualB, creatorUserId, tenantId, companyId, branchId);
        Assert.NotNull(quoteB);
        Assert.True(quoteB.IsManualEntry);

        // 5. Quotation Comparison Matrix
        var matrix = await quoteService.GetComparisonMatrixAsync(rfq.Id, tenantId, companyId, branchId);
        Assert.NotNull(matrix);
        Assert.Equal(2, matrix.Suppliers.Count);
        Assert.Single(matrix.Lines);
        Assert.Equal(2, matrix.Lines[0].Bids.Count);

        // Best bid detection (Supplier B has 230 vs Supplier A 240)
        var bestBid = matrix.Lines[0].Bids.OrderBy(b => b.UnitPrice).First();
        Assert.Equal(230m, bestBid.UnitPrice);
        Assert.Equal(supplierB.SupplierId, bestBid.SupplierId);

        // 6. Rule Enforcement: Self-Approval Guard
        // The creator of the RFQ cannot approve quotations
        var quotes = await quoteService.GetQuotationsForRfqAsync(rfq.Id, tenantId, companyId, branchId);
        var quoteToApprove = quotes.First(q => q.SupplierId == supplierB.SupplierId);

        var approveRequest = new ApproveQuotationRequest { Remarks = "Awarded lowest evaluated price" };
        var selfApprovalEx = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            quoteService.ApproveQuotationAsync(rfq.Id, quoteToApprove.Id, approveRequest, creatorUserId, tenantId, companyId, branchId));
        Assert.Contains("separation of duties", selfApprovalEx.Message, StringComparison.OrdinalIgnoreCase);

        // 7. Legitimate Approval by independent approver (ApproverUserId != CreatorUserId)
        var approvedQuote = await quoteService.ApproveQuotationAsync(rfq.Id, quoteToApprove.Id, approveRequest, approverUserId, tenantId, companyId, branchId);
        Assert.Equal(SupplierQuotationStatus.Approved, approvedQuote.Status);

        // RFQ transitions to Awarded
        var awardedRfq = await rfqService.GetRfqByIdAsync(rfq.Id, tenantId);
        Assert.Equal(RequestForQuotationStatus.Awarded, awardedRfq!.Status);

        // 8. Multi-Award Support: Can approve another quotation on the same RFQ
        var quoteAEntity = quotes.First(q => q.SupplierId == supplierA.SupplierId);
        var approvedQuoteA = await quoteService.ApproveQuotationAsync(rfq.Id, quoteAEntity.Id, new ApproveQuotationRequest { Remarks = "Secondary award for backup supply" }, approverUserId, tenantId, companyId, branchId);
        Assert.Equal(SupplierQuotationStatus.Approved, approvedQuoteA.Status);
    }

    [Fact]
    public async Task InvitationFailureCanBeRetriedWithoutResendingSuccessfulInvitations()
    {
        using var db = CreateInMemoryDomainDb();
        var tokenService = new RfqTokenService(db);
        var emailService = new StubRfqEmailNotificationService
        {
            Result = new RfqEmailDispatchResult(false, "SMTP rejected the message.")
        };
        var rfqService = new RFQService(db, tokenService, emailService);

        var tenantId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var creatorUserId = Guid.NewGuid();
        var uom = UnitOfMeasure.Create(tenantId, "EA", "Each");
        var product = Product.Create(tenantId, "ITEM-01", "Test Item", uom.UnitOfMeasureId, "Test");
        var supplier = Supplier.Create(tenantId, "SUP-RETRY", "Retry Supplier", email: "retry@example.test");
        await db.UnitsOfMeasure.AddAsync(uom);
        await db.Products.AddAsync(product);
        await db.Suppliers.AddAsync(supplier);
        await db.SaveChangesAsync();

        var rfq = await rfqService.CreateRfqAsync(
            new CreateRfqRequest
            {
                Title = "Invitation Retry Test",
                ClosingDate = DateTime.UtcNow.AddDays(10),
                SupplierIds = new List<Guid> { supplier.SupplierId },
                Lines = new List<CreateRfqLineRequest>
                {
                    new()
                    {
                        ProductId = product.ProductId,
                        Quantity = 1,
                        UnitOfMeasureId = uom.UnitOfMeasureId
                    }
                }
            },
            creatorUserId,
            tenantId,
            companyId,
            branchId);

        var failedAttempt = await rfqService.SendInvitationsAsync(
            rfq.Id,
            new SendRfqInvitationsRequest { SupplierIds = new List<Guid> { supplier.SupplierId } },
            creatorUserId,
            tenantId);
        Assert.Equal(0, failedAttempt.SentCount);
        Assert.Equal(1, failedAttempt.FailedCount);
        Assert.Contains("Invitation could not be dispatched to 'Retry Supplier': SMTP rejected the message.", failedAttempt.Messages);

        var failedSupplier = (await rfqService.GetRfqByIdAsync(rfq.Id, tenantId))!.InvitedSuppliers.Single();
        Assert.Equal(RFQSupplierInvitationStatus.Failed, failedSupplier.InvitationStatus);
        Assert.False(failedSupplier.HasToken);

        emailService.Result = new RfqEmailDispatchResult(true);
        var retry = await rfqService.SendInvitationsAsync(
            rfq.Id,
            new SendRfqInvitationsRequest { SupplierIds = new List<Guid> { supplier.SupplierId } },
            creatorUserId,
            tenantId);
        Assert.Equal(1, retry.SentCount);
        Assert.Equal(0, retry.FailedCount);
        Assert.Equal(2, emailService.CallCount);

        var alreadySent = await rfqService.SendInvitationsAsync(
            rfq.Id,
            new SendRfqInvitationsRequest { SupplierIds = new List<Guid> { supplier.SupplierId } },
            creatorUserId,
            tenantId);
        Assert.Equal(0, alreadySent.SentCount);
        Assert.Equal(2, emailService.CallCount);
        Assert.Equal(
            RFQSupplierInvitationStatus.Sent,
            (await rfqService.GetRfqByIdAsync(rfq.Id, tenantId))!.InvitedSuppliers.Single().InvitationStatus);
    }

    [Fact]
    public async Task SendInvitations_PreservesActualTransportFailureReason_AndFallsBackWhenEmpty()
    {
        using var db = CreateInMemoryDomainDb();
        var tokenService = new RfqTokenService(db);
        var emailService = new StubRfqEmailNotificationService();
        var rfqService = new RFQService(db, tokenService, emailService);

        var tenantId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var creatorUserId = Guid.NewGuid();
        var uom = UnitOfMeasure.Create(tenantId, "EA", "Each");
        var product = Product.Create(tenantId, "ITEM-01", "Test Item", uom.UnitOfMeasureId, "Test");
        var supplierA = Supplier.Create(tenantId, "SUP-A", "Supplier A", email: "suppliera@example.test");
        var supplierB = Supplier.Create(tenantId, "SUP-B", "Supplier B", email: "supplierb@example.test");
        await db.UnitsOfMeasure.AddAsync(uom);
        await db.Products.AddAsync(product);
        await db.Suppliers.AddRangeAsync(supplierA, supplierB);
        await db.SaveChangesAsync();

        var rfq = await rfqService.CreateRfqAsync(
            new CreateRfqRequest
            {
                Title = "Error Message Preservation Test",
                ClosingDate = DateTime.UtcNow.AddDays(10),
                SupplierIds = new List<Guid> { supplierA.SupplierId, supplierB.SupplierId },
                Lines = new List<CreateRfqLineRequest>
                {
                    new()
                    {
                        ProductId = product.ProductId,
                        Quantity = 1,
                        UnitOfMeasureId = uom.UnitOfMeasureId
                    }
                }
            },
            creatorUserId,
            tenantId,
            companyId,
            branchId);

        // Case 1: Actual transport failure reason is provided -> should be preserved
        emailService.Result = new RfqEmailDispatchResult(false, "Network connection failure: No such host is known.");
        var resultWithReason = await rfqService.SendInvitationsAsync(
            rfq.Id,
            new SendRfqInvitationsRequest { SupplierIds = new List<Guid> { supplierA.SupplierId } },
            creatorUserId,
            tenantId);

        Assert.Equal(1, resultWithReason.FailedCount);
        Assert.Contains("Invitation could not be dispatched to 'Supplier A': Network connection failure: No such host is known.", resultWithReason.Messages);

        // Case 2: No failure reason provided -> fallback to default guidance
        emailService.Result = new RfqEmailDispatchResult(false, null);
        var resultWithFallback = await rfqService.SendInvitationsAsync(
            rfq.Id,
            new SendRfqInvitationsRequest { SupplierIds = new List<Guid> { supplierB.SupplierId } },
            creatorUserId,
            tenantId);

        Assert.Equal(1, resultWithFallback.FailedCount);
        Assert.Contains("Invitation could not be dispatched to 'Supplier B'. Correct the supplier email or mail configuration and retry.", resultWithFallback.Messages);
    }

    [Fact]
    public async Task SendInvitations_GeneratesThreeAccessOptions_WithSameValidToken()
    {
        using var db = CreateInMemoryDomainDb();
        var tokenService = new RfqTokenService(db);
        var emailService = new StubRfqEmailNotificationService();
        var rfqService = new RFQService(db, tokenService, emailService);

        var tenantId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var creatorUserId = Guid.NewGuid();

        var company = new Company
        {
            Id = companyId,
            TenantId = tenantId,
            Name = "Nova Retail Solutions (Pvt) Ltd",
            Code = "NOV"
        };
        await db.Companies.AddAsync(company);

        var uom = UnitOfMeasure.Create(tenantId, "EA", "Each");
        var product = Product.Create(tenantId, "ITEM-01", "Test Product", uom.UnitOfMeasureId, "General");
        var supplier = Supplier.Create(tenantId, "SUP-01", "Global Supplier Ltd", email: "supplier@global.com");
        await db.UnitsOfMeasure.AddAsync(uom);
        await db.Products.AddAsync(product);
        await db.Suppliers.AddAsync(supplier);
        await db.SaveChangesAsync();

        var rfq = await rfqService.CreateRfqAsync(new CreateRfqRequest
        {
            Title = "Equipment Purchase",
            ClosingDate = DateTime.UtcNow.AddDays(7),
            Lines = new List<CreateRfqLineRequest>
            {
                new() { ProductId = product.ProductId, Quantity = 5, UnitOfMeasureId = uom.UnitOfMeasureId }
            },
            SupplierIds = new List<Guid> { supplier.SupplierId }
        }, creatorUserId, tenantId, companyId, branchId);

        // Case A: With valid tenant subdomain and custom external domain
        var requestA = new SendRfqInvitationsRequest
        {
            SupplierIds = new List<Guid> { supplier.SupplierId },
            PortalBaseUrl = "http://localhost:4200",
            TenantSubdomain = "novaretail",
            ExternalBaseUrl = "apexerp.com"
        };

        var resultA = await rfqService.SendInvitationsAsync(rfq.Id, requestA, creatorUserId, tenantId);
        Assert.Equal(1, resultA.SentCount);
        Assert.NotNull(emailService.LastContext);

        var ctxA = emailService.LastContext!;
        Assert.Equal("Nova Retail Solutions (Pvt) Ltd", ctxA.BuyingCompanyName);
        Assert.StartsWith("http://localhost:4200/public/rfq-submit?token=", ctxA.StandardLocalhostUrl);
        Assert.NotNull(ctxA.TenantLocalhostUrl);
        Assert.StartsWith("http://novaretail.localhost:4200/public/rfq-submit?token=", ctxA.TenantLocalhostUrl);
        Assert.NotNull(ctxA.ExternalTenantUrl);
        Assert.StartsWith("https://novaretail.apexerp.com/public/rfq-submit?token=", ctxA.ExternalTenantUrl);

        // Verify all 3 URLs share the EXACT same plaintext token
        var tokenStandard = ctxA.StandardLocalhostUrl.Split("token=")[1];
        var tokenSubdomain = ctxA.TenantLocalhostUrl.Split("token=")[1];
        var tokenExternal = ctxA.ExternalTenantUrl.Split("token=")[1];
        Assert.Equal(tokenStandard, tokenSubdomain);
        Assert.Equal(tokenStandard, tokenExternal);

        // Verify that this token validates successfully in quotation service
        var quoteService = new SupplierQuotationService(db, tokenService);
        var publicDetails = await quoteService.GetPublicRfqDetailsByTokenAsync(tokenStandard);
        Assert.NotNull(publicDetails);
        Assert.Equal("Nova Retail Solutions (Pvt) Ltd", publicDetails!.CompanyName);
        Assert.Equal("Global Supplier Ltd", publicDetails.SupplierName);

        // Case B: Without tenant subdomain -> Option 2 & 3 clearly report unavailable
        var supplier2 = Supplier.Create(tenantId, "SUP-02", "Second Supplier Ltd", email: "supplier2@global.com");
        await db.Suppliers.AddAsync(supplier2);
        await db.SaveChangesAsync();
        await rfqService.AddSupplierAsync(rfq.Id, new AddRfqSupplierRequest { SupplierId = supplier2.SupplierId }, creatorUserId, tenantId);

        var requestB = new SendRfqInvitationsRequest
        {
            SupplierIds = new List<Guid> { supplier2.SupplierId },
            PortalBaseUrl = "http://localhost:4200",
            TenantSubdomain = null, // No subdomain configured
            ExternalBaseUrl = "apexerp.com"
        };

        var resultB = await rfqService.SendInvitationsAsync(rfq.Id, requestB, creatorUserId, tenantId);
        Assert.Equal(1, resultB.SentCount);
        var ctxB = emailService.LastContext!;
        Assert.Null(ctxB.TenantLocalhostUrl);
        Assert.NotNull(ctxB.TenantLocalhostUnavailableReason);
        Assert.Null(ctxB.ExternalTenantUrl);
        Assert.NotNull(ctxB.ExternalTenantUnavailableReason);

        // Case C: LAN IP Address configured as external host -> Does NOT prepend subdomain
        var supplier3 = Supplier.Create(tenantId, "SUP-03", "Third Supplier Ltd", email: "supplier3@global.com");
        await db.Suppliers.AddAsync(supplier3);
        await db.SaveChangesAsync();
        await rfqService.AddSupplierAsync(rfq.Id, new AddRfqSupplierRequest { SupplierId = supplier3.SupplierId }, creatorUserId, tenantId);

        var requestC = new SendRfqInvitationsRequest
        {
            SupplierIds = new List<Guid> { supplier3.SupplierId },
            PortalBaseUrl = "http://localhost:4200",
            TenantSubdomain = "novaretail",
            ExternalBaseUrl = "192.168.1.50" // IP address host
        };

        var resultC = await rfqService.SendInvitationsAsync(rfq.Id, requestC, creatorUserId, tenantId);
        Assert.Equal(1, resultC.SentCount);
        var ctxC = emailService.LastContext!;
        Assert.NotNull(ctxC.ExternalTenantUrl);
        Assert.StartsWith("http://192.168.1.50:4200/public/rfq-submit?token=", ctxC.ExternalTenantUrl);
        Assert.DoesNotContain("novaretail.192.168.1.50", ctxC.ExternalTenantUrl);
    }

    [Fact]
    public async Task QuotationApproval_RejectsExpiredQuotations()
    {
        using var db = CreateInMemoryDomainDb();
        var tokenService = new RfqTokenService(db);
        var quoteService = new SupplierQuotationService(db, tokenService);

        var tenantId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var creatorUserId = Guid.NewGuid();
        var approverUserId = Guid.NewGuid();

        var uom = UnitOfMeasure.Create(tenantId, "EA", "Each");
        await db.UnitsOfMeasure.AddAsync(uom);

        var currency = Currency.Create(tenantId, "USD", "US Dollar", "$", 2);
        await db.Currencies.AddAsync(currency);

        var product = Product.Create(tenantId, "PART-01", "Spare Part", uom.UnitOfMeasureId, "Spares");
        await db.Products.AddAsync(product);

        var supplier = Supplier.Create(tenantId, "SUP-EXP", "Expired Supplier Ltd");
        await db.Suppliers.AddAsync(supplier);

        var rfq = new RequestForQuotation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            BranchId = branchId,
            RFQNumber = "RFQ-2026-TEST",
            Title = "Expired Test RFQ",
            Status = RequestForQuotationStatus.Issued,
            ClosingDate = DateTime.UtcNow.AddDays(7),
            Lines = new List<RequestForQuotationLine>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    CompanyId = companyId,
                    LineNumber = 1,
                    ProductId = product.ProductId,
                    ProductCode = product.ProductCode,
                    ProductName = product.ProductName,
                    Quantity = 10m,
                    UnitOfMeasureId = uom.UnitOfMeasureId,
                    UnitOfMeasureCode = "EA"
                }
            }
        };
        // Seed quotation that expired yesterday
        var expiredQuote = new SupplierQuotation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            BranchId = branchId,
            RFQId = rfq.Id,
            SupplierId = supplier.SupplierId,
            SupplierCode = supplier.SupplierCode,
            SupplierName = supplier.SupplierName,
            QuotationReference = "EXP-001",
            QuotationDate = DateTime.UtcNow.AddDays(-10),
            ValidUntil = DateTime.UtcNow.AddDays(-1), // EXPIRED!
            CurrencyId = currency.CurrencyId,
            CurrencyCode = currency.CurrencyCode,
            ExchangeRate = 1.0m,
            Status = SupplierQuotationStatus.Submitted,
            TotalAmount = 1000m,
            BaseTotalAmount = 1000m
        };
        await db.RequestForQuotations.AddAsync(rfq);
        await db.SupplierQuotations.AddAsync(expiredQuote);
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            quoteService.ApproveQuotationAsync(rfq.Id, expiredQuote.Id, new ApproveQuotationRequest(), approverUserId, tenantId, companyId, branchId));

        Assert.Contains("expired", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<(
        RequestForQuotation Rfq,
        RFQSupplier Invitation,
        Currency Currency,
        Product Product,
        UnitOfMeasure Uom,
        Supplier Supplier,
        string Token)> SeedStep4Async(
        DomainDbContext db,
        RfqTokenService tokenService,
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        string suffix = "A",
        bool setCompanyBaseCurrency = true)
    {
        var uom = UnitOfMeasure.Create(tenantId, "EA", "Each");
        var currency = Currency.Create(tenantId, "LKR", "Sri Lankan Rupee", "Rs");
        var product = Product.Create(tenantId, $"ITEM-{suffix}", $"Test Item {suffix}", uom.UnitOfMeasureId, "Test");
        var supplier = Supplier.Create(tenantId, $"SUP-{suffix}", $"Supplier {suffix}", email: $"supplier{suffix}@test.lk");

        db.UnitsOfMeasure.Add(uom);
        db.Currencies.Add(currency);
        db.Products.Add(product);
        db.Suppliers.Add(supplier);

        if (setCompanyBaseCurrency)
        {
            db.Companies.Add(new Company
            {
                Id = companyId,
                TenantId = tenantId,
                Name = $"Company {suffix}",
                Code = $"CO-{suffix}",
                BaseCurrencyId = currency.CurrencyId
            });
        }

        var (token, tokenHash) = tokenService.GenerateToken();

        var rfq = new RequestForQuotation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            BranchId = branchId,
            RFQNumber = $"RFQ-T4-{suffix}",
            Title = $"Step 4 Test RFQ {suffix}",
            Status = RequestForQuotationStatus.Issued,
            ClosingDate = DateTime.UtcNow.AddDays(7),
            Lines = new List<RequestForQuotationLine>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    CompanyId = companyId,
                    LineNumber = 1,
                    ProductId = product.ProductId,
                    ProductCode = product.ProductCode,
                    ProductName = product.ProductName,
                    Quantity = 100m,
                    UnitOfMeasureId = uom.UnitOfMeasureId,
                    UnitOfMeasureCode = "EA",
                    TargetUnitPrice = 999m
                }
            },
            InvitedSuppliers = new List<RFQSupplier>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    CompanyId = companyId,
                    SupplierId = supplier.SupplierId,
                    SupplierCode = supplier.SupplierCode,
                    SupplierName = supplier.SupplierName,
                    ContactEmail = $"supplier{suffix}@test.lk",
                    InvitationStatus = RFQSupplierInvitationStatus.Sent,
                    InvitedAt = DateTime.UtcNow,
                    TokenHash = tokenHash,
                    TokenExpiresAt = DateTime.UtcNow.AddDays(2)
                }
            }
        };

        foreach (var invitation in rfq.InvitedSuppliers)
        {
            invitation.RFQId = rfq.Id;
        }

        db.RequestForQuotations.Add(rfq);
        await db.SaveChangesAsync();

        return (rfq, rfq.InvitedSuppliers.First(), currency, product, uom, supplier, token);
    }

    [Fact]
    public async Task PublicSubmission_UsesCompanyBaseCurrency_PersistsTax_AndBlocksRepeatedSubmission()
    {
        using var db = CreateInMemoryDomainDb();
        var tokenService = new RfqTokenService(db);
        var quoteService = new SupplierQuotationService(db, tokenService);

        var tenantId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var branchId = Guid.NewGuid();

        var seed = await SeedStep4Async(db, tokenService, tenantId, companyId, branchId);

        var publicView = await quoteService.GetPublicRfqDetailsByTokenAsync(seed.Token);
        Assert.NotNull(publicView);
        Assert.Equal(seed.Currency.CurrencyId, publicView!.CurrencyId);
        Assert.Equal("LKR", publicView.CurrencyCode);

        var invitation = await db.RFQSuppliers.FirstAsync(s => s.RFQId == seed.Rfq.Id);
        Assert.Equal(RFQSupplierInvitationStatus.Opened, invitation.InvitationStatus);

        var request = new SubmitPublicQuotationRequest
        {
            QuotationReference = "SUP-QUOTE-001",
            QuotationDate = DateTime.UtcNow,
            ValidUntil = DateTime.UtcNow.AddDays(20),
            CurrencyId = null,
            Lines = new List<SubmitPublicQuotationLineRequest>
            {
                new()
                {
                    RFQLineId = seed.Rfq.Lines.First().Id,
                    QuotedQuantity = 80m,
                    UnitOfMeasureId = seed.Uom.UnitOfMeasureId,
                    UnitPrice = 100m,
                    DiscountPercentage = 10m,
                    TaxRate = 15m,
                    LeadTimeDays = 5
                }
            }
        };

        var result = await quoteService.SubmitPublicQuotationAsync(seed.Token, request);
        Assert.True(result.Success);

        var quote = await db.SupplierQuotations
            .Include(q => q.Lines)
            .FirstAsync(q => q.RFQId == seed.Rfq.Id);

        Assert.Equal("LKR", quote.CurrencyCode);
        Assert.Equal(8000m, quote.SubTotal);
        Assert.Equal(800m, quote.DiscountAmount);
        Assert.Equal(1080m, quote.TaxAmount);
        Assert.Equal(8280m, quote.TotalAmount);
        Assert.Equal(8280m, quote.BaseTotalAmount);
        Assert.Equal(80m, quote.Lines.First().QuotedQuantity);
        Assert.Equal(15m, quote.Lines.First().TaxRate);
        Assert.Equal(8280m, quote.Lines.First().LineTotal);

        // The secure link is invalidated once the quotation is accepted.
        Assert.Null(await quoteService.GetPublicRfqDetailsByTokenAsync(seed.Token));

        // Even a re-issued link must not allow a second quotation for the same supplier.
        var (replayToken, replayHash) = tokenService.GenerateToken();
        invitation.TokenHash = replayHash;
        invitation.TokenExpiresAt = DateTime.UtcNow.AddDays(2);
        await db.SaveChangesAsync();

        var replay = await quoteService.SubmitPublicQuotationAsync(replayToken, request);
        Assert.False(replay.Success);
        Assert.Contains("already", replay.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, await db.SupplierQuotations.CountAsync(q => q.RFQId == seed.Rfq.Id));
    }

    [Fact]
    public async Task PublicSubmission_RejectsInvalidQuantitiesPricesTaxAndLineReferences()
    {
        using var db = CreateInMemoryDomainDb();
        var tokenService = new RfqTokenService(db);
        var quoteService = new SupplierQuotationService(db, tokenService);

        var seed = await SeedStep4Async(db, tokenService, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var rfqLineId = seed.Rfq.Lines.First().Id;

        SubmitPublicQuotationRequest Build(
            decimal quantity,
            decimal unitPrice,
            decimal discount,
            decimal tax,
            Guid? lineId = null)
        {
            return new SubmitPublicQuotationRequest
            {
                QuotationReference = "INVALID-TEST",
                QuotationDate = DateTime.UtcNow,
                ValidUntil = DateTime.UtcNow.AddDays(10),
                Lines = new List<SubmitPublicQuotationLineRequest>
                {
                    new()
                    {
                        RFQLineId = lineId ?? rfqLineId,
                        QuotedQuantity = quantity,
                        UnitOfMeasureId = seed.Uom.UnitOfMeasureId,
                        UnitPrice = unitPrice,
                        DiscountPercentage = discount,
                        TaxRate = tax,
                        LeadTimeDays = 5
                    }
                }
            };
        }

        var invalidRequests = new[]
        {
            Build(0m, 100m, 0m, 10m),        // zero quantity
            Build(101m, 100m, 0m, 10m),      // more than requested
            Build(10m, 0m, 0m, 10m),         // zero price
            Build(10m, -5m, 0m, 10m),        // negative price
            Build(10m, 100m, -1m, 10m),      // negative discount
            Build(10m, 100m, 101m, 10m),     // discount above 100
            Build(10m, 100m, 0m, -1m),       // negative tax
            Build(10m, 100m, 0m, 150m),      // tax above 100
            Build(10m, 100m, 0m, 10m, Guid.NewGuid()) // line not on this RFQ
        };

        foreach (var invalid in invalidRequests)
        {
            var result = await quoteService.SubmitPublicQuotationAsync(seed.Token, invalid);
            Assert.False(result.Success);
        }

        // Duplicate RFQ line in the same submission.
        var duplicateLines = Build(10m, 100m, 0m, 10m);
        duplicateLines.Lines.Add(new SubmitPublicQuotationLineRequest
        {
            RFQLineId = rfqLineId,
            QuotedQuantity = 10m,
            UnitOfMeasureId = seed.Uom.UnitOfMeasureId,
            UnitPrice = 100m,
            TaxRate = 10m
        });
        Assert.False((await quoteService.SubmitPublicQuotationAsync(seed.Token, duplicateLines)).Success);

        // Validity in the past.
        var pastValidity = Build(10m, 100m, 0m, 10m);
        pastValidity.ValidUntil = DateTime.UtcNow.AddDays(-1);
        Assert.False((await quoteService.SubmitPublicQuotationAsync(seed.Token, pastValidity)).Success);

        // No invalid submission was persisted.
        Assert.Equal(0, await db.SupplierQuotations.CountAsync(q => q.RFQId == seed.Rfq.Id));
    }

    [Fact]
    public async Task QuotationReads_EnforceCompanyBranchScope_AndPublicDtosHideTargetPricesAndCompetitors()
    {
        using var db = CreateInMemoryDomainDb();
        var tokenService = new RfqTokenService(db);
        var quoteService = new SupplierQuotationService(db, tokenService);

        var tenantId = Guid.NewGuid();
        var companyA = Guid.NewGuid();
        var branchA = Guid.NewGuid();
        var companyB = Guid.NewGuid();
        var branchB = Guid.NewGuid();

        var seedA = await SeedStep4Async(db, tokenService, tenantId, companyA, branchA, "A");
        var seedB = await SeedStep4Async(db, tokenService, tenantId, companyB, branchB, "B");

        // Both suppliers can submit independently.
        var requestA = new SubmitPublicQuotationRequest
        {
            QuotationReference = "A-QUOTE",
            QuotationDate = DateTime.UtcNow,
            ValidUntil = DateTime.UtcNow.AddDays(10),
            Lines = new List<SubmitPublicQuotationLineRequest>
            {
                new()
                {
                    RFQLineId = seedA.Rfq.Lines.First().Id,
                    QuotedQuantity = 50m,
                    UnitOfMeasureId = seedA.Uom.UnitOfMeasureId,
                    UnitPrice = 120m,
                    TaxRate = 8m
                }
            }
        };

        var requestB = new SubmitPublicQuotationRequest
        {
            QuotationReference = "B-QUOTE",
            QuotationDate = DateTime.UtcNow,
            ValidUntil = DateTime.UtcNow.AddDays(10),
            Lines = new List<SubmitPublicQuotationLineRequest>
            {
                new()
                {
                    RFQLineId = seedB.Rfq.Lines.First().Id,
                    QuotedQuantity = 70m,
                    UnitOfMeasureId = seedB.Uom.UnitOfMeasureId,
                    UnitPrice = 110m,
                    TaxRate = 8m
                }
            }
        };

        // Supplier-facing payloads contain no competitor data and no internal target price.
        var viewA = await quoteService.GetPublicRfqDetailsByTokenAsync(seedA.Token);
        Assert.NotNull(viewA);
        Assert.Equal("Supplier A", viewA!.SupplierName);
        Assert.Equal("Company A", viewA.CompanyName);
        Assert.Single(viewA.Lines);
        Assert.Equal(seedA.Rfq.Lines.First().Id, viewA.Lines[0].RFQLineId);
        Assert.Null(typeof(PublicRfqDetailsDto).GetProperty("Quotations"));
        Assert.Null(typeof(PublicRfqLineDto).GetProperty("TargetUnitPrice"));

        Assert.True((await quoteService.SubmitPublicQuotationAsync(seedA.Token, requestA)).Success);
        Assert.True((await quoteService.SubmitPublicQuotationAsync(seedB.Token, requestB)).Success);

        var quotesA = await quoteService.GetQuotationsForRfqAsync(seedA.Rfq.Id, tenantId, companyA, branchA);
        Assert.Single(quotesA);
        Assert.Equal(seedA.Supplier.SupplierId, quotesA[0].SupplierId);

        var quotesB = await quoteService.GetQuotationsForRfqAsync(seedB.Rfq.Id, tenantId, companyB, branchB);
        Assert.Single(quotesB);
        Assert.Equal(seedB.Supplier.SupplierId, quotesB[0].SupplierId);

        // A caller scoped to another company/branch cannot read the RFQ or its quotations.
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            quoteService.GetQuotationsForRfqAsync(seedA.Rfq.Id, tenantId, companyB, branchB));
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            quoteService.GetComparisonMatrixAsync(seedA.Rfq.Id, tenantId, companyB, branchB));

        var crossScopeQuote = await quoteService.GetQuotationByIdAsync(
            seedA.Rfq.Id, quotesA[0].Id, tenantId, companyB, branchB);
        Assert.Null(crossScopeQuote);

        // Another tenant cannot read the quotation either.
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            quoteService.GetQuotationsForRfqAsync(seedA.Rfq.Id, Guid.NewGuid(), companyA, branchA));
    }

    [Fact]
    public async Task ManualQuotation_DefaultsToCompanyBaseCurrency_AndRejectsInvalidAmounts()
    {
        using var db = CreateInMemoryDomainDb();
        var tokenService = new RfqTokenService(db);
        var quoteService = new SupplierQuotationService(db, tokenService);

        var tenantId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var seed = await SeedStep4Async(db, tokenService, tenantId, companyId, branchId);

        RecordManualQuotationRequest Build(decimal unitPrice)
        {
            return new RecordManualQuotationRequest
            {
                SupplierId = seed.Supplier.SupplierId,
                QuotationReference = "MANUAL-001",
                QuotationDate = DateTime.UtcNow,
                ValidUntil = DateTime.UtcNow.AddDays(10),
                CurrencyId = Guid.Empty,
                Lines = new List<RecordQuotationLineRequest>
                {
                    new()
                    {
                        RFQLineId = seed.Rfq.Lines.First().Id,
                        ProductId = seed.Product.ProductId,
                        QuotedQuantity = 25m,
                        UnitOfMeasureId = seed.Uom.UnitOfMeasureId,
                        UnitPrice = unitPrice,
                        DiscountPercentage = 0m,
                        TaxRate = 10m,
                        LeadTimeDays = 3
                    }
                }
            };
        }

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            quoteService.RecordManualQuotationAsync(seed.Rfq.Id, Build(0m), userId, tenantId, companyId, branchId));

        var manual = await quoteService.RecordManualQuotationAsync(
            seed.Rfq.Id, Build(200m), userId, tenantId, companyId, branchId);

        Assert.Equal("LKR", manual.CurrencyCode);
        Assert.Equal(1.0m, manual.ExchangeRate);
        Assert.Equal(25m * 200m, manual.SubTotal);
        Assert.Equal(500m, manual.TaxAmount);
        Assert.Equal(5500m, manual.TotalAmount);
    }

    [Fact]
    public async Task ComparisonMatrix_ExcludesDraftAndRejectedQuotations_AndPopulatesTaxAndBaseCalculations()
    {
        using var db = CreateInMemoryDomainDb();
        var tokenService = new RfqTokenService(db);
        var quoteService = new SupplierQuotationService(db, tokenService);

        var tenantId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var branchId = Guid.NewGuid();

        var seed = await SeedStep4Async(db, tokenService, tenantId, companyId, branchId);

        var supActive = seed.Supplier;
        var supRejected = Supplier.Create(tenantId, "SUP-REJ", "Rejected Supplier", email: "rej@test.lk");
        var supDraft = Supplier.Create(tenantId, "SUP-DFT", "Draft Supplier", email: "dft@test.lk");
        await db.Suppliers.AddRangeAsync(supRejected, supDraft);

        var rfqLine = seed.Rfq.Lines.First();

        // 1. Submitted quotation (eligible)
        var submittedQuote = new SupplierQuotation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            BranchId = branchId,
            RFQId = seed.Rfq.Id,
            SupplierId = supActive.SupplierId,
            SupplierCode = supActive.SupplierCode,
            SupplierName = supActive.SupplierName,
            QuotationReference = "QUOTE-ACTIVE",
            QuotationDate = DateTime.UtcNow,
            ValidUntil = DateTime.UtcNow.AddDays(15),
            CurrencyId = seed.Currency.CurrencyId,
            CurrencyCode = seed.Currency.CurrencyCode,
            ExchangeRate = 1.0m,
            Status = SupplierQuotationStatus.Submitted,
            SubTotal = 1000m,
            DiscountAmount = 100m,
            TaxAmount = 135m,
            TotalAmount = 1035m,
            BaseTotalAmount = 1035m,
            Lines = new List<SupplierQuotationLine>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    CompanyId = companyId,
                    RFQLineId = rfqLine.Id,
                    ProductId = rfqLine.ProductId,
                    ProductCode = rfqLine.ProductCode,
                    ProductName = rfqLine.ProductName,
                    QuotedQuantity = 10m,
                    UnitOfMeasureId = rfqLine.UnitOfMeasureId,
                    UnitOfMeasureCode = rfqLine.UnitOfMeasureCode,
                    UnitPrice = 100m,
                    DiscountPercentage = 10m,
                    TaxRate = 15m,
                    LineTotal = 1035m,
                    LeadTimeDays = 7,
                    TechnicalRemarks = "High grade batch"
                }
            }
        };

        // 2. Rejected quotation (excluded from active competing bids)
        var rejectedQuote = new SupplierQuotation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            BranchId = branchId,
            RFQId = seed.Rfq.Id,
            SupplierId = supRejected.SupplierId,
            SupplierCode = supRejected.SupplierCode,
            SupplierName = supRejected.SupplierName,
            QuotationReference = "QUOTE-REJECTED",
            QuotationDate = DateTime.UtcNow,
            ValidUntil = DateTime.UtcNow.AddDays(15),
            CurrencyId = seed.Currency.CurrencyId,
            CurrencyCode = seed.Currency.CurrencyCode,
            ExchangeRate = 1.0m,
            Status = SupplierQuotationStatus.Rejected,
            TotalAmount = 1500m,
            BaseTotalAmount = 1500m
        };

        // 3. Draft quotation (excluded from comparison matrix)
        var draftQuote = new SupplierQuotation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            BranchId = branchId,
            RFQId = seed.Rfq.Id,
            SupplierId = supDraft.SupplierId,
            SupplierCode = supDraft.SupplierCode,
            SupplierName = supDraft.SupplierName,
            QuotationReference = "QUOTE-DRAFT",
            QuotationDate = DateTime.UtcNow,
            ValidUntil = DateTime.UtcNow.AddDays(15),
            CurrencyId = seed.Currency.CurrencyId,
            CurrencyCode = seed.Currency.CurrencyCode,
            ExchangeRate = 1.0m,
            Status = SupplierQuotationStatus.Draft,
            TotalAmount = 900m,
            BaseTotalAmount = 900m
        };

        await db.SupplierQuotations.AddRangeAsync(submittedQuote, rejectedQuote, draftQuote);
        await db.SaveChangesAsync();

        // Query matrix
        var matrix = await quoteService.GetComparisonMatrixAsync(seed.Rfq.Id, tenantId, companyId, branchId);

        Assert.NotNull(matrix);
        // Only the submitted quotation is included in competing suppliers
        Assert.Single(matrix.Suppliers);
        Assert.Equal("QUOTE-ACTIVE", matrix.Suppliers[0].QuotationReference);
        Assert.Equal(SupplierQuotationStatus.Submitted, matrix.Suppliers[0].Status);
        Assert.NotNull(matrix.Suppliers[0].SubmissionDate);
        Assert.True(matrix.Suppliers[0].HasValidExchangeRate);

        // Verify line calculations
        Assert.Single(matrix.Lines);
        var lineBids = matrix.Lines[0].Bids;
        Assert.Single(lineBids);
        var bid = lineBids[0];
        Assert.Equal(100m, bid.UnitPrice);
        Assert.Equal(10m, bid.DiscountPercentage);
        Assert.Equal(100m, bid.DiscountAmount); // 1000 * 10%
        Assert.Equal(15m, bid.TaxRate);
        Assert.Equal(135m, bid.TaxAmount); // (1000 - 100) * 15%
        Assert.Equal(1035m, bid.LineTotal); // (1000 - 100) + 135
        Assert.Equal(1035m, bid.BaseLineTotal);
        Assert.Equal(135m, bid.BaseTaxAmount);
        Assert.Equal(100m, bid.BaseDiscountAmount);
        Assert.Equal(7, bid.LeadTimeDays);
    }

    [Fact]
    public async Task ComparisonMatrix_HandlesCurrencyNormalization_AndIdentifiesInvalidExchangeRates()
    {
        using var db = CreateInMemoryDomainDb();
        var tokenService = new RfqTokenService(db);
        var quoteService = new SupplierQuotationService(db, tokenService);

        var tenantId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var branchId = Guid.NewGuid();

        var seed = await SeedStep4Async(db, tokenService, tenantId, companyId, branchId);
        var usdCurrency = Currency.Create(tenantId, "USD", "US Dollar", "$");
        await db.Currencies.AddAsync(usdCurrency);

        var rfqLine = seed.Rfq.Lines.First();

        // Quotation in USD with a valid exchange rate of 300 LKR/USD
        var validUsdQuote = new SupplierQuotation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            BranchId = branchId,
            RFQId = seed.Rfq.Id,
            SupplierId = seed.Supplier.SupplierId,
            SupplierCode = seed.Supplier.SupplierCode,
            SupplierName = seed.Supplier.SupplierName,
            QuotationReference = "USD-VALID",
            QuotationDate = DateTime.UtcNow,
            ValidUntil = DateTime.UtcNow.AddDays(10),
            CurrencyId = usdCurrency.CurrencyId,
            CurrencyCode = "USD",
            ExchangeRate = 300m,
            Status = SupplierQuotationStatus.Submitted,
            TotalAmount = 50m,
            Lines = new List<SupplierQuotationLine>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    CompanyId = companyId,
                    RFQLineId = rfqLine.Id,
                    ProductId = rfqLine.ProductId,
                    ProductCode = rfqLine.ProductCode,
                    ProductName = rfqLine.ProductName,
                    QuotedQuantity = 10m,
                    UnitOfMeasureId = rfqLine.UnitOfMeasureId,
                    UnitOfMeasureCode = rfqLine.UnitOfMeasureCode,
                    UnitPrice = 5m,
                    DiscountPercentage = 0m,
                    TaxRate = 0m,
                    LineTotal = 50m
                }
            }
        };

        // Quotation in USD with missing/invalid exchange rate (0)
        var invalidUsdQuote = new SupplierQuotation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            BranchId = branchId,
            RFQId = seed.Rfq.Id,
            SupplierId = seed.Supplier.SupplierId,
            SupplierCode = seed.Supplier.SupplierCode,
            SupplierName = seed.Supplier.SupplierName,
            QuotationReference = "USD-INVALID",
            QuotationDate = DateTime.UtcNow,
            ValidUntil = DateTime.UtcNow.AddDays(10),
            CurrencyId = usdCurrency.CurrencyId,
            CurrencyCode = "USD",
            ExchangeRate = 0m, // Invalid / missing rate
            Status = SupplierQuotationStatus.Submitted,
            TotalAmount = 40m,
            Lines = new List<SupplierQuotationLine>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    CompanyId = companyId,
                    RFQLineId = rfqLine.Id,
                    ProductId = rfqLine.ProductId,
                    ProductCode = rfqLine.ProductCode,
                    ProductName = rfqLine.ProductName,
                    QuotedQuantity = 10m,
                    UnitOfMeasureId = rfqLine.UnitOfMeasureId,
                    UnitOfMeasureCode = rfqLine.UnitOfMeasureCode,
                    UnitPrice = 4m,
                    DiscountPercentage = 0m,
                    TaxRate = 0m,
                    LineTotal = 40m
                }
            }
        };

        await db.SupplierQuotations.AddRangeAsync(validUsdQuote, invalidUsdQuote);
        await db.SaveChangesAsync();

        var matrix = await quoteService.GetComparisonMatrixAsync(seed.Rfq.Id, tenantId, companyId, branchId);

        var validDto = matrix.Suppliers.First(s => s.QuotationReference == "USD-VALID");
        Assert.True(validDto.HasValidExchangeRate);
        Assert.Null(validDto.CurrencyConversionError);
        Assert.Equal(300m, validDto.ExchangeRate);
        Assert.Equal(15000m, validDto.BaseTotalAmount); // 50 * 300

        var invalidDto = matrix.Suppliers.First(s => s.QuotationReference == "USD-INVALID");
        Assert.False(invalidDto.HasValidExchangeRate);
        Assert.NotNull(invalidDto.CurrencyConversionError);
        Assert.Equal(0m, invalidDto.BaseTotalAmount); // Never silently substitute 1.0!
    }

    [Fact]
    public async Task QuotationApproval_PreventsAwardingCancelledOrClosedRfq()
    {
        using var db = CreateInMemoryDomainDb();
        var tokenService = new RfqTokenService(db);
        var quoteService = new SupplierQuotationService(db, tokenService);

        var tenantId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var creatorUserId = Guid.NewGuid();
        var approverUserId = Guid.NewGuid();

        var seed = await SeedStep4Async(db, tokenService, tenantId, companyId, branchId);
        db.Entry(seed.Rfq).Property(x => x.CreatedBy).CurrentValue = creatorUserId;

        var quote = new SupplierQuotation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            BranchId = branchId,
            RFQId = seed.Rfq.Id,
            SupplierId = seed.Supplier.SupplierId,
            SupplierCode = seed.Supplier.SupplierCode,
            SupplierName = seed.Supplier.SupplierName,
            QuotationReference = "QUOTE-TEST",
            QuotationDate = DateTime.UtcNow,
            ValidUntil = DateTime.UtcNow.AddDays(10),
            CurrencyId = seed.Currency.CurrencyId,
            CurrencyCode = seed.Currency.CurrencyCode,
            Status = SupplierQuotationStatus.Submitted,
            TotalAmount = 500m
        };
        await db.SupplierQuotations.AddAsync(quote);

        // 1. RFQ is Cancelled
        seed.Rfq.Status = RequestForQuotationStatus.Cancelled;
        await db.SaveChangesAsync();

        var cancelEx = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            quoteService.ApproveQuotationAsync(seed.Rfq.Id, quote.Id, new ApproveQuotationRequest(), approverUserId, tenantId, companyId, branchId));
        Assert.Contains("Cancelled", cancelEx.Message);

        // 2. RFQ is Closed
        seed.Rfq.Status = RequestForQuotationStatus.Closed;
        await db.SaveChangesAsync();

        var closeEx = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            quoteService.ApproveQuotationAsync(seed.Rfq.Id, quote.Id, new ApproveQuotationRequest(), approverUserId, tenantId, companyId, branchId));
        Assert.Contains("Closed", closeEx.Message);
    }

    [Fact]
    public async Task QuotationApproval_PreventsAwardingRejectedOrDraftQuotation()
    {
        using var db = CreateInMemoryDomainDb();
        var tokenService = new RfqTokenService(db);
        var quoteService = new SupplierQuotationService(db, tokenService);

        var tenantId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var creatorUserId = Guid.NewGuid();
        var approverUserId = Guid.NewGuid();

        var seed = await SeedStep4Async(db, tokenService, tenantId, companyId, branchId);
        db.Entry(seed.Rfq).Property(x => x.CreatedBy).CurrentValue = creatorUserId;

        var rejectedQuote = new SupplierQuotation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            BranchId = branchId,
            RFQId = seed.Rfq.Id,
            SupplierId = seed.Supplier.SupplierId,
            SupplierCode = seed.Supplier.SupplierCode,
            SupplierName = seed.Supplier.SupplierName,
            QuotationReference = "QUOTE-REJ-TEST",
            QuotationDate = DateTime.UtcNow,
            ValidUntil = DateTime.UtcNow.AddDays(10),
            CurrencyId = seed.Currency.CurrencyId,
            CurrencyCode = seed.Currency.CurrencyCode,
            Status = SupplierQuotationStatus.Rejected,
            TotalAmount = 500m
        };

        var draftQuote = new SupplierQuotation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            BranchId = branchId,
            RFQId = seed.Rfq.Id,
            SupplierId = seed.Supplier.SupplierId,
            SupplierCode = seed.Supplier.SupplierCode,
            SupplierName = seed.Supplier.SupplierName,
            QuotationReference = "QUOTE-DFT-TEST",
            QuotationDate = DateTime.UtcNow,
            ValidUntil = DateTime.UtcNow.AddDays(10),
            CurrencyId = seed.Currency.CurrencyId,
            CurrencyCode = seed.Currency.CurrencyCode,
            Status = SupplierQuotationStatus.Draft,
            TotalAmount = 400m
        };

        await db.SupplierQuotations.AddRangeAsync(rejectedQuote, draftQuote);
        await db.SaveChangesAsync();

        var rejEx = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            quoteService.ApproveQuotationAsync(seed.Rfq.Id, rejectedQuote.Id, new ApproveQuotationRequest(), approverUserId, tenantId, companyId, branchId));
        Assert.Contains("rejected", rejEx.Message, StringComparison.OrdinalIgnoreCase);

        var dftEx = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            quoteService.ApproveQuotationAsync(seed.Rfq.Id, draftQuote.Id, new ApproveQuotationRequest(), approverUserId, tenantId, companyId, branchId));
        Assert.Contains("Draft", dftEx.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task QuotationApproval_RepeatedAwardRequest_IsHandledSafelyAndIdempotently()
    {
        using var db = CreateInMemoryDomainDb();
        var tokenService = new RfqTokenService(db);
        var quoteService = new SupplierQuotationService(db, tokenService);

        var tenantId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var creatorUserId = Guid.NewGuid();
        var approverUserId = Guid.NewGuid();

        var seed = await SeedStep4Async(db, tokenService, tenantId, companyId, branchId);
        db.Entry(seed.Rfq).Property(x => x.CreatedBy).CurrentValue = creatorUserId;

        var quote = new SupplierQuotation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            BranchId = branchId,
            RFQId = seed.Rfq.Id,
            SupplierId = seed.Supplier.SupplierId,
            SupplierCode = seed.Supplier.SupplierCode,
            SupplierName = seed.Supplier.SupplierName,
            QuotationReference = "QUOTE-IDEM-TEST",
            QuotationDate = DateTime.UtcNow,
            ValidUntil = DateTime.UtcNow.AddDays(10),
            CurrencyId = seed.Currency.CurrencyId,
            CurrencyCode = seed.Currency.CurrencyCode,
            Status = SupplierQuotationStatus.Submitted,
            TotalAmount = 1000m
        };
        await db.SupplierQuotations.AddAsync(quote);
        await db.SaveChangesAsync();

        // 1st Award call: transitions to Approved and RFQ to Awarded
        var firstResult = await quoteService.ApproveQuotationAsync(
            seed.Rfq.Id, quote.Id, new ApproveQuotationRequest { Remarks = "First award" }, approverUserId, tenantId, companyId, branchId);

        Assert.Equal(SupplierQuotationStatus.Approved, firstResult.Status);
        Assert.Equal(RequestForQuotationStatus.Awarded, (await db.RequestForQuotations.FindAsync(seed.Rfq.Id))!.Status);
        var originalApprovedAt = firstResult.ApprovedAt;
        Assert.NotNull(originalApprovedAt);

        // 2nd Award call: Repeated call is safe and idempotent
        var repeatedResult = await quoteService.ApproveQuotationAsync(
            seed.Rfq.Id, quote.Id, new ApproveQuotationRequest { Remarks = "Repeated award attempt" }, approverUserId, tenantId, companyId, branchId);

        Assert.Equal(SupplierQuotationStatus.Approved, repeatedResult.Status);
        Assert.Equal(originalApprovedAt, repeatedResult.ApprovedAt); // Side-effects not repeated
        Assert.Equal("First award", repeatedResult.ApprovalRemarks);
    }

    [Fact]
    public async Task ComparisonMatrix_SuppliersAndLineBids_MaintainConsistentOrderingAndReconciledTotals()
    {
        using var db = CreateInMemoryDomainDb();
        var tokenService = new RfqTokenService(db);
        var quoteService = new SupplierQuotationService(db, tokenService);

        var tenantId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var branchId = Guid.NewGuid();

        var seed = await SeedStep4Async(db, tokenService, tenantId, companyId, branchId, "RECON");

        // Add a 2nd product & RFQ line
        var prod2 = Product.Create(tenantId, "ITEM-RECON-2", "Item Two", seed.Uom.UnitOfMeasureId, "Test");
        db.Products.Add(prod2);

        var rfqLine2 = new RequestForQuotationLine
        {
            Id = Guid.NewGuid(),
            RFQId = seed.Rfq.Id,
            TenantId = tenantId,
            CompanyId = companyId,
            LineNumber = 2,
            ProductId = prod2.ProductId,
            ProductCode = prod2.ProductCode,
            ProductName = prod2.ProductName,
            Quantity = 50m,
            UnitOfMeasureId = seed.Uom.UnitOfMeasureId,
            UnitOfMeasureCode = seed.Uom.Code,
            TargetUnitPrice = 2000m
        };
        db.RequestForQuotationLines.Add(rfqLine2);
        await db.SaveChangesAsync();

        var rfqLine1Id = seed.Rfq.Lines.First(l => l.LineNumber == 1).Id;
        var rfqLine2Id = rfqLine2.Id;

        // Three distinct suppliers
        var suppLanka = Supplier.Create(tenantId, "SUP-RET-001", "Lanka Retail Distributors (Pvt) Ltd");
        var suppTest = Supplier.Create(tenantId, "SUP-TEST-002", "Test Supplier");
        var suppPromime = Supplier.Create(tenantId, "SUP-PRO-003", "Promime mart");
        db.Suppliers.AddRange(suppLanka, suppTest, suppPromime);
        await db.SaveChangesAsync();

        // Quotations added in reverse/database order: Promime first (5.67B), Test second (2.65B), Lanka third (312M)
        var quotePromimeId = Guid.NewGuid();
        var quoteTestId = Guid.NewGuid();
        var quoteLankaId = Guid.NewGuid();

        var quotePromime = new SupplierQuotation
        {
            Id = quotePromimeId,
            TenantId = tenantId,
            CompanyId = companyId,
            BranchId = branchId,
            RFQId = seed.Rfq.Id,
            SupplierId = suppPromime.SupplierId,
            SupplierCode = suppPromime.SupplierCode,
            SupplierName = suppPromime.SupplierName,
            QuotationReference = "23232",
            QuotationDate = DateTime.UtcNow,
            ValidUntil = DateTime.UtcNow.AddDays(30),
            CurrencyId = seed.Currency.CurrencyId,
            CurrencyCode = seed.Currency.CurrencyCode,
            ExchangeRate = 1.0m,
            Status = SupplierQuotationStatus.Submitted,
            TotalAmount = 5670907730.00m,
            BaseTotalAmount = 5670907730.00m,
            Lines = new List<SupplierQuotationLine>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    CompanyId = companyId,
                    RFQLineId = rfqLine1Id,
                    ProductId = seed.Product.ProductId,
                    QuotedQuantity = 100m,
                    UnitOfMeasureCode = "EA",
                    UnitPrice = 30000000.00m,
                    LineTotal = 3000000000.00m
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    CompanyId = companyId,
                    RFQLineId = rfqLine2Id,
                    ProductId = prod2.ProductId,
                    QuotedQuantity = 50m,
                    UnitOfMeasureCode = "EA",
                    UnitPrice = 53418154.60m,
                    LineTotal = 2670907730.00m
                }
            }
        };

        var quoteTest = new SupplierQuotation
        {
            Id = quoteTestId,
            TenantId = tenantId,
            CompanyId = companyId,
            BranchId = branchId,
            RFQId = seed.Rfq.Id,
            SupplierId = suppTest.SupplierId,
            SupplierCode = suppTest.SupplierCode,
            SupplierName = suppTest.SupplierName,
            QuotationReference = "2323",
            QuotationDate = DateTime.UtcNow,
            ValidUntil = DateTime.UtcNow.AddDays(30),
            CurrencyId = seed.Currency.CurrencyId,
            CurrencyCode = seed.Currency.CurrencyCode,
            ExchangeRate = 1.0m,
            Status = SupplierQuotationStatus.Submitted,
            TotalAmount = 2654132100.00m,
            BaseTotalAmount = 2654132100.00m,
            Lines = new List<SupplierQuotationLine>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    CompanyId = companyId,
                    RFQLineId = rfqLine1Id,
                    ProductId = seed.Product.ProductId,
                    QuotedQuantity = 100m,
                    UnitOfMeasureCode = "EA",
                    UnitPrice = 15000000.00m,
                    LineTotal = 1500000000.00m
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    CompanyId = companyId,
                    RFQLineId = rfqLine2Id,
                    ProductId = prod2.ProductId,
                    QuotedQuantity = 50m,
                    UnitOfMeasureCode = "EA",
                    UnitPrice = 23082642.00m,
                    LineTotal = 1154132100.00m
                }
            }
        };

        var quoteLanka = new SupplierQuotation
        {
            Id = quoteLankaId,
            TenantId = tenantId,
            CompanyId = companyId,
            BranchId = branchId,
            RFQId = seed.Rfq.Id,
            SupplierId = suppLanka.SupplierId,
            SupplierCode = suppLanka.SupplierCode,
            SupplierName = suppLanka.SupplierName,
            QuotationReference = "23232323232",
            QuotationDate = DateTime.UtcNow,
            ValidUntil = DateTime.UtcNow.AddDays(30),
            CurrencyId = seed.Currency.CurrencyId,
            CurrencyCode = seed.Currency.CurrencyCode,
            ExchangeRate = 1.0m,
            Status = SupplierQuotationStatus.Submitted,
            TotalAmount = 312355930.00m,
            BaseTotalAmount = 312355930.00m,
            Lines = new List<SupplierQuotationLine>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    CompanyId = companyId,
                    RFQLineId = rfqLine1Id,
                    ProductId = seed.Product.ProductId,
                    QuotedQuantity = 100m,
                    UnitOfMeasureCode = "EA",
                    UnitPrice = 2000000.00m,
                    LineTotal = 200000000.00m
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    CompanyId = companyId,
                    RFQLineId = rfqLine2Id,
                    ProductId = prod2.ProductId,
                    QuotedQuantity = 50m,
                    UnitOfMeasureCode = "EA",
                    UnitPrice = 2247118.60m,
                    LineTotal = 112355930.00m
                }
            }
        };

        await db.SupplierQuotations.AddRangeAsync(quotePromime, quoteTest, quoteLanka);
        await db.SaveChangesAsync();

        // Act
        var matrix = await quoteService.GetComparisonMatrixAsync(seed.Rfq.Id, tenantId, companyId, branchId);

        // Assert
        Assert.NotNull(matrix);
        Assert.Equal(3, matrix.Suppliers.Count);

        // 1. Verify supplier summary totals are sorted ascending
        Assert.Equal("Lanka Retail Distributors (Pvt) Ltd", matrix.Suppliers[0].SupplierName);
        Assert.Equal(312355930.00m, matrix.Suppliers[0].TotalAmount);
        Assert.Equal(quoteLankaId, matrix.Suppliers[0].QuotationId);

        Assert.Equal("Test Supplier", matrix.Suppliers[1].SupplierName);
        Assert.Equal(2654132100.00m, matrix.Suppliers[1].TotalAmount);
        Assert.Equal(quoteTestId, matrix.Suppliers[1].QuotationId);

        Assert.Equal("Promime mart", matrix.Suppliers[2].SupplierName);
        Assert.Equal(5670907730.00m, matrix.Suppliers[2].TotalAmount);
        Assert.Equal(quotePromimeId, matrix.Suppliers[2].QuotationId);

        // 2. Verify compLine.Bids for every line strictly follow matrix.Suppliers order
        Assert.Equal(2, matrix.Lines.Count);
        foreach (var compLine in matrix.Lines)
        {
            Assert.Equal(3, compLine.Bids.Count);
            for (int i = 0; i < matrix.Suppliers.Count; i++)
            {
                Assert.Equal(matrix.Suppliers[i].QuotationId, compLine.Bids[i].QuotationId);
                Assert.Equal(matrix.Suppliers[i].SupplierId, compLine.Bids[i].SupplierId);
            }
        }

        // 3. Verify that for EVERY supplier, the sum of line totals equals the displayed supplier summary total
        foreach (var supplier in matrix.Suppliers)
        {
            var supplierLineTotalSum = matrix.Lines
                .SelectMany(l => l.Bids)
                .Where(b => b.QuotationId == supplier.QuotationId)
                .Sum(b => b.LineTotal);

            Assert.Equal(supplier.TotalAmount, supplierLineTotalSum);
        }

        // Specifically verify Lanka Retail: sum of lines is 312,355,930.00, matching supplier header
        var lankaLinesSum = matrix.Lines.SelectMany(l => l.Bids).Where(b => b.QuotationId == quoteLankaId).Sum(b => b.LineTotal);
        Assert.Equal(312355930.00m, lankaLinesSum);

        // Specifically verify Promime mart: sum of lines is 5,670,907,730.00, matching supplier header
        var promimeLinesSum = matrix.Lines.SelectMany(l => l.Bids).Where(b => b.QuotationId == quotePromimeId).Sum(b => b.LineTotal);
        Assert.Equal(5670907730.00m, promimeLinesSum);

        // Specifically verify Test Supplier: sum of lines is 2,654,132,100.00, matching supplier header
        var testLinesSum = matrix.Lines.SelectMany(l => l.Bids).Where(b => b.QuotationId == quoteTestId).Sum(b => b.LineTotal);
        Assert.Equal(2654132100.00m, testLinesSum);
    }

    [Fact]
    public async Task ApproveQuotation_SuccessfulAward_TriggersAwardNotificationEmailToWinningSupplier()
    {
        // Arrange
        using var db = CreateInMemoryDomainDb();
        var tokenService = new RfqTokenService(db);
        var emailService = new StubRfqEmailNotificationService();
        var quoteService = new SupplierQuotationService(db, tokenService, null, emailService);

        var tenantId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var creatorUserId = Guid.NewGuid();
        var approverUserId = Guid.NewGuid();

        var seed = await SeedStep4Async(db, tokenService, tenantId, companyId, branchId, "WIN1");
        db.Entry(seed.Rfq).Property(x => x.CreatedBy).CurrentValue = creatorUserId;

        var quote = new SupplierQuotation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            BranchId = branchId,
            RFQId = seed.Rfq.Id,
            SupplierId = seed.Supplier.SupplierId,
            SupplierCode = seed.Supplier.SupplierCode,
            SupplierName = seed.Supplier.SupplierName,
            QuotationReference = "QT-WIN-001",
            QuotationDate = DateTime.UtcNow,
            ValidUntil = DateTime.UtcNow.AddDays(14),
            CurrencyId = seed.Currency.CurrencyId,
            CurrencyCode = seed.Currency.CurrencyCode,
            TotalAmount = 250000m,
            BaseTotalAmount = 250000m,
            Status = SupplierQuotationStatus.Submitted
        };

        await db.SupplierQuotations.AddAsync(quote);
        await db.SaveChangesAsync();

        var approveReq = new ApproveQuotationRequest
        {
            Remarks = "Lowest compliant commercial proposal."
        };

        // Act
        var result = await quoteService.ApproveQuotationAsync(
            seed.Rfq.Id, quote.Id, approveReq, approverUserId, tenantId, companyId, branchId);

        // Assert
        Assert.Equal(SupplierQuotationStatus.Approved, result.Status);
        Assert.Equal(RequestForQuotationStatus.Awarded, (await db.RequestForQuotations.FindAsync(seed.Rfq.Id))!.Status);

        // 1. Verify email service was invoked exactly once for award notification
        Assert.Equal(1, emailService.AwardCallCount);
        Assert.NotNull(emailService.LastAwardContext);
        Assert.Equal(seed.Supplier.Email, emailService.LastAwardContext.RecipientEmail);
        Assert.Equal(seed.Supplier.SupplierName, emailService.LastAwardContext.WinningSupplierName);
        Assert.Equal(seed.Rfq.RFQNumber, emailService.LastAwardContext.RfqNumber);
        Assert.Equal("QT-WIN-001", emailService.LastAwardContext.QuotationReference);
        Assert.False(string.IsNullOrWhiteSpace(emailService.LastAwardContext.BuyerCompanyName));

        // 2. Verify DTO and audit record reflection
        Assert.Equal("Sent", result.AwardNotificationStatus);
        Assert.Null(result.AwardNotificationFailureReason);

        var audit = await db.DomainAuditRecords
            .Where(a => a.EntityName == "RfqAwardNotification" && a.EntityId == quote.Id.ToString())
            .OrderByDescending(a => a.Timestamp)
            .FirstOrDefaultAsync();
        Assert.NotNull(audit);
        Assert.Equal("AwardNotificationSent", audit.Action);
    }

    [Fact]
    public async Task ApproveQuotation_FailedEmailDelivery_DoesNotUndoAwardAndRecordsFailure()
    {
        // Arrange
        using var db = CreateInMemoryDomainDb();
        var tokenService = new RfqTokenService(db);
        var emailService = new StubRfqEmailNotificationService
        {
            Result = new RfqEmailDispatchResult(false, "SMTP server connection timed out.")
        };
        var quoteService = new SupplierQuotationService(db, tokenService, null, emailService);

        var tenantId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var creatorUserId = Guid.NewGuid();
        var approverUserId = Guid.NewGuid();

        var seed = await SeedStep4Async(db, tokenService, tenantId, companyId, branchId, "FAIL1");
        db.Entry(seed.Rfq).Property(x => x.CreatedBy).CurrentValue = creatorUserId;

        var quote = new SupplierQuotation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            BranchId = branchId,
            RFQId = seed.Rfq.Id,
            SupplierId = seed.Supplier.SupplierId,
            SupplierCode = seed.Supplier.SupplierCode,
            SupplierName = seed.Supplier.SupplierName,
            QuotationReference = "QT-FAIL-001",
            QuotationDate = DateTime.UtcNow,
            ValidUntil = DateTime.UtcNow.AddDays(14),
            CurrencyId = seed.Currency.CurrencyId,
            CurrencyCode = seed.Currency.CurrencyCode,
            TotalAmount = 150000m,
            BaseTotalAmount = 150000m,
            Status = SupplierQuotationStatus.Submitted
        };

        await db.SupplierQuotations.AddAsync(quote);
        await db.SaveChangesAsync();

        // Act
        var result = await quoteService.ApproveQuotationAsync(
            seed.Rfq.Id, quote.Id, new ApproveQuotationRequest { Remarks = "Approved with delivery failure" }, approverUserId, tenantId, companyId, branchId);

        // Assert: Award MUST be preserved despite email failure
        Assert.Equal(SupplierQuotationStatus.Approved, result.Status);
        var persistedQuote = await db.SupplierQuotations.FindAsync(quote.Id);
        Assert.NotNull(persistedQuote);
        Assert.Equal(SupplierQuotationStatus.Approved, persistedQuote.Status);

        var persistedRfq = await db.RequestForQuotations.FindAsync(seed.Rfq.Id);
        Assert.NotNull(persistedRfq);
        Assert.Equal(RequestForQuotationStatus.Awarded, persistedRfq.Status);

        // Failure recorded
        Assert.Equal("Failed", result.AwardNotificationStatus);
        Assert.Contains("SMTP server connection timed out", result.AwardNotificationFailureReason);

        var audit = await db.DomainAuditRecords
            .Where(a => a.EntityName == "RfqAwardNotification" && a.EntityId == quote.Id.ToString())
            .OrderByDescending(a => a.Timestamp)
            .FirstOrDefaultAsync();
        Assert.NotNull(audit);
        Assert.Equal("AwardNotificationFailed", audit.Action);
    }

    [Fact]
    public async Task RejectQuotation_DoesNotTriggerAwardNotification()
    {
        // Arrange
        using var db = CreateInMemoryDomainDb();
        var tokenService = new RfqTokenService(db);
        var emailService = new StubRfqEmailNotificationService();
        var quoteService = new SupplierQuotationService(db, tokenService, null, emailService);

        var tenantId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var seed = await SeedStep4Async(db, tokenService, tenantId, companyId, branchId, "REJ1");

        var quote = new SupplierQuotation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            BranchId = branchId,
            RFQId = seed.Rfq.Id,
            SupplierId = seed.Supplier.SupplierId,
            SupplierCode = seed.Supplier.SupplierCode,
            SupplierName = seed.Supplier.SupplierName,
            QuotationReference = "QT-REJ-001",
            QuotationDate = DateTime.UtcNow,
            ValidUntil = DateTime.UtcNow.AddDays(14),
            CurrencyId = seed.Currency.CurrencyId,
            CurrencyCode = seed.Currency.CurrencyCode,
            TotalAmount = 500000m,
            BaseTotalAmount = 500000m,
            Status = SupplierQuotationStatus.Submitted
        };

        await db.SupplierQuotations.AddAsync(quote);
        await db.SaveChangesAsync();

        // Act
        var result = await quoteService.RejectQuotationAsync(
            seed.Rfq.Id, quote.Id, new RejectQuotationRequest { Remarks = "Price exceeded target." }, userId, tenantId, companyId, branchId);

        // Assert
        Assert.Equal(SupplierQuotationStatus.Rejected, result.Status);
        Assert.Equal(0, emailService.AwardCallCount);
    }

    [Fact]
    public async Task AwardNotification_DuplicatePrevention_DoesNotResendIfAlreadySent()
    {
        // Arrange
        using var db = CreateInMemoryDomainDb();
        var tokenService = new RfqTokenService(db);
        var emailService = new StubRfqEmailNotificationService();
        var quoteService = new SupplierQuotationService(db, tokenService, null, emailService);

        var tenantId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var creatorUserId = Guid.NewGuid();
        var approverUserId = Guid.NewGuid();

        var seed = await SeedStep4Async(db, tokenService, tenantId, companyId, branchId, "DUP1");
        db.Entry(seed.Rfq).Property(x => x.CreatedBy).CurrentValue = creatorUserId;

        var quote = new SupplierQuotation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            BranchId = branchId,
            RFQId = seed.Rfq.Id,
            SupplierId = seed.Supplier.SupplierId,
            SupplierCode = seed.Supplier.SupplierCode,
            SupplierName = seed.Supplier.SupplierName,
            QuotationReference = "QT-DUP-001",
            QuotationDate = DateTime.UtcNow,
            ValidUntil = DateTime.UtcNow.AddDays(14),
            CurrencyId = seed.Currency.CurrencyId,
            CurrencyCode = seed.Currency.CurrencyCode,
            TotalAmount = 250000m,
            BaseTotalAmount = 250000m,
            Status = SupplierQuotationStatus.Submitted
        };

        await db.SupplierQuotations.AddAsync(quote);
        await db.SaveChangesAsync();

        // 1. Initial award (sends notification)
        await quoteService.ApproveQuotationAsync(
            seed.Rfq.Id, quote.Id, new ApproveQuotationRequest { Remarks = "Initial award" }, approverUserId, tenantId, companyId, branchId);
        Assert.Equal(1, emailService.AwardCallCount);

        // 2. Subsequent idempotent approve call does not resend
        await quoteService.ApproveQuotationAsync(
            seed.Rfq.Id, quote.Id, new ApproveQuotationRequest { Remarks = "Repeat call" }, approverUserId, tenantId, companyId, branchId);
        Assert.Equal(1, emailService.AwardCallCount);

        // 3. RetryAwardNotificationAsync explicitly throws InvalidOperationException when already sent
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            quoteService.RetryAwardNotificationAsync(seed.Rfq.Id, quote.Id, approverUserId, tenantId, companyId, branchId));
        Assert.Equal(1, emailService.AwardCallCount);
    }

    [Fact]
    public async Task SendInvitationsAsync_WhenRfqAwarded_RetriesOnlyAwardNotificationForWinningSupplier()
    {
        // Arrange
        using var db = CreateInMemoryDomainDb();
        var tokenService = new RfqTokenService(db);
        var emailService = new StubRfqEmailNotificationService();
        var rfqService = new RFQService(db, tokenService, emailService);

        var tenantId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var seed = await SeedStep4Async(db, tokenService, tenantId, companyId, branchId, "RETRY1");

        // Set RFQ status to Awarded
        seed.Rfq.Status = RequestForQuotationStatus.Awarded;

        var quote = new SupplierQuotation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            BranchId = branchId,
            RFQId = seed.Rfq.Id,
            SupplierId = seed.Supplier.SupplierId,
            SupplierCode = seed.Supplier.SupplierCode,
            SupplierName = seed.Supplier.SupplierName,
            QuotationReference = "QT-RETRY-001",
            QuotationDate = DateTime.UtcNow,
            ValidUntil = DateTime.UtcNow.AddDays(14),
            CurrencyId = seed.Currency.CurrencyId,
            CurrencyCode = seed.Currency.CurrencyCode,
            TotalAmount = 300000m,
            BaseTotalAmount = 300000m,
            Status = SupplierQuotationStatus.Approved,
            ApprovedAt = DateTime.UtcNow
        };

        // Record initial failure in audit
        var failedAudit = new DomainAuditRecord
        {
            AuditId = Guid.NewGuid(),
            TenantId = tenantId,
            EntityName = "RfqAwardNotification",
            EntityId = quote.Id.ToString(),
            Action = "AwardNotificationFailed",
            UserId = userId,
            Timestamp = DateTime.UtcNow.AddMinutes(-5),
            NewValues = "{\"Status\":\"Failed\",\"FailureReason\":\"Temporary network glitch\"}"
        };

        await db.SupplierQuotations.AddAsync(quote);
        await db.DomainAuditRecords.AddAsync(failedAudit);
        await db.SaveChangesAsync();

        var retryRequest = new SendRfqInvitationsRequest
        {
            SupplierIds = new List<Guid> { seed.Supplier.SupplierId }
        };

        // Act: Invoke existing SendInvitationsAsync action on Awarded RFQ
        var result = await rfqService.SendInvitationsAsync(seed.Rfq.Id, retryRequest, userId, tenantId);

        // Assert
        Assert.Equal(1, result.SentCount);
        Assert.Equal(0, result.FailedCount);

        // Verify award notification was called, NOT invitation
        Assert.Equal(1, emailService.AwardCallCount);
        Assert.Equal(0, emailService.CallCount); // SendInvitation was NOT called!
        Assert.NotNull(emailService.LastAwardContext);
        Assert.Equal(seed.Supplier.Email, emailService.LastAwardContext.RecipientEmail);

        // Verify new audit record is recorded as Sent
        var latestAudit = await db.DomainAuditRecords
            .Where(a => a.EntityName == "RfqAwardNotification" && a.EntityId == quote.Id.ToString())
            .OrderByDescending(a => a.Timestamp)
            .FirstAsync();
        Assert.Equal("AwardNotificationSent", latestAudit.Action);
    }

    [Fact]
    public async Task AwardNotification_OnlyWinningSupplierReceivesEmail_NeverCompetingSuppliers()
    {
        // Arrange
        using var db = CreateInMemoryDomainDb();
        var tokenService = new RfqTokenService(db);
        var emailService = new StubRfqEmailNotificationService();
        var quoteService = new SupplierQuotationService(db, tokenService, null, emailService);

        var tenantId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var creatorUserId = Guid.NewGuid();
        var approverUserId = Guid.NewGuid();

        var seed = await SeedStep4Async(db, tokenService, tenantId, companyId, branchId, "WIN");
        db.Entry(seed.Rfq).Property(x => x.CreatedBy).CurrentValue = creatorUserId;

        // Add second competing supplier
        var competitor = Supplier.Create(tenantId, "SUP-COMPETITOR", "Competitor Supplies Ltd", email: "competitor@test.lk");
        db.Suppliers.Add(competitor);
        await db.RFQSuppliers.AddAsync(new RFQSupplier
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            RFQId = seed.Rfq.Id,
            SupplierId = competitor.SupplierId,
            SupplierCode = competitor.SupplierCode,
            SupplierName = competitor.SupplierName,
            ContactEmail = competitor.Email!,
            InvitationStatus = RFQSupplierInvitationStatus.Submitted
        });

        var winningQuote = new SupplierQuotation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            BranchId = branchId,
            RFQId = seed.Rfq.Id,
            SupplierId = seed.Supplier.SupplierId,
            SupplierCode = seed.Supplier.SupplierCode,
            SupplierName = seed.Supplier.SupplierName,
            QuotationReference = "QT-WINNER-001",
            QuotationDate = DateTime.UtcNow,
            ValidUntil = DateTime.UtcNow.AddDays(14),
            CurrencyId = seed.Currency.CurrencyId,
            CurrencyCode = seed.Currency.CurrencyCode,
            TotalAmount = 100000m,
            BaseTotalAmount = 100000m,
            Status = SupplierQuotationStatus.Submitted
        };

        var competitorQuote = new SupplierQuotation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            BranchId = branchId,
            RFQId = seed.Rfq.Id,
            SupplierId = competitor.SupplierId,
            SupplierCode = competitor.SupplierCode,
            SupplierName = competitor.SupplierName,
            QuotationReference = "QT-LOSER-002",
            QuotationDate = DateTime.UtcNow,
            ValidUntil = DateTime.UtcNow.AddDays(14),
            CurrencyId = seed.Currency.CurrencyId,
            CurrencyCode = seed.Currency.CurrencyCode,
            TotalAmount = 180000m,
            BaseTotalAmount = 180000m,
            Status = SupplierQuotationStatus.Submitted
        };

        await db.SupplierQuotations.AddRangeAsync(winningQuote, competitorQuote);
        await db.SaveChangesAsync();

        // Act: Award winning quotation
        await quoteService.ApproveQuotationAsync(
            seed.Rfq.Id, winningQuote.Id, new ApproveQuotationRequest { Remarks = "Selected winning bid" }, approverUserId, tenantId, companyId, branchId);

        // Assert
        Assert.Equal(1, emailService.AwardCallCount);
        Assert.Equal(seed.Supplier.Email, emailService.LastAwardContext!.RecipientEmail);
        Assert.NotEqual(competitor.Email, emailService.LastAwardContext.RecipientEmail);

        // Context must not mention competitor quote or competitor name
        Assert.DoesNotContain("Competitor Supplies Ltd", emailService.LastAwardContext.QuotationReference);
        Assert.DoesNotContain("QT-LOSER-002", emailService.LastAwardContext.QuotationReference);
    }

    [Fact]
    public async Task ApproveQuotation_MissingSupplierEmail_RecordsFailureAndDoesNotSend()
    {
        // Arrange
        using var db = CreateInMemoryDomainDb();
        var tokenService = new RfqTokenService(db);
        var emailService = new StubRfqEmailNotificationService();
        var quoteService = new SupplierQuotationService(db, tokenService, null, emailService);

        var tenantId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var creatorUserId = Guid.NewGuid();
        var approverUserId = Guid.NewGuid();

        var seed = await SeedStep4Async(db, tokenService, tenantId, companyId, branchId, "NOEMAIL");
        db.Entry(seed.Rfq).Property(x => x.CreatedBy).CurrentValue = creatorUserId;

        // Clear email on both master record and RFQSupplier record
        db.Entry(seed.Supplier).Property(x => x.Email).CurrentValue = null;
        var rfqSup = await db.RFQSuppliers.FirstAsync(s => s.SupplierId == seed.Supplier.SupplierId && s.RFQId == seed.Rfq.Id);
        rfqSup.ContactEmail = string.Empty;
        await db.SaveChangesAsync();

        var quote = new SupplierQuotation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            BranchId = branchId,
            RFQId = seed.Rfq.Id,
            SupplierId = seed.Supplier.SupplierId,
            SupplierCode = seed.Supplier.SupplierCode,
            SupplierName = seed.Supplier.SupplierName,
            QuotationReference = "QT-NOEMAIL-001",
            QuotationDate = DateTime.UtcNow,
            ValidUntil = DateTime.UtcNow.AddDays(14),
            CurrencyId = seed.Currency.CurrencyId,
            CurrencyCode = seed.Currency.CurrencyCode,
            TotalAmount = 250000m,
            BaseTotalAmount = 250000m,
            Status = SupplierQuotationStatus.Submitted
        };

        await db.SupplierQuotations.AddAsync(quote);
        await db.SaveChangesAsync();

        // Act
        var result = await quoteService.ApproveQuotationAsync(
            seed.Rfq.Id, quote.Id, new ApproveQuotationRequest { Remarks = "Awarded without email" }, approverUserId, tenantId, companyId, branchId);

        // Assert: Quotation and RFQ are awarded, but email is NOT sent and failure is recorded
        Assert.Equal(SupplierQuotationStatus.Approved, result.Status);
        Assert.Equal(RequestForQuotationStatus.Awarded, (await db.RequestForQuotations.FindAsync(seed.Rfq.Id))!.Status);
        Assert.Equal(0, emailService.AwardCallCount);
        Assert.Equal("Failed", result.AwardNotificationStatus);
        Assert.NotNull(result.AwardNotificationFailureReason);
        Assert.True(result.AwardNotificationFailureReason.Contains("missing", StringComparison.OrdinalIgnoreCase) || result.AwardNotificationFailureReason.Contains("no registered contact email", StringComparison.OrdinalIgnoreCase));

        var audit = await db.DomainAuditRecords
            .Where(a => a.EntityName == "RfqAwardNotification" && a.EntityId == quote.Id.ToString())
            .OrderByDescending(a => a.Timestamp)
            .FirstOrDefaultAsync();
        Assert.NotNull(audit);
        Assert.Equal("AwardNotificationFailed", audit.Action);
    }

    [Fact]
    public async Task ApproveQuotation_InvalidSupplierEmail_RecordsFailureAndDoesNotSend()
    {
        // Arrange
        using var db = CreateInMemoryDomainDb();
        var tokenService = new RfqTokenService(db);
        var emailService = new StubRfqEmailNotificationService();
        var quoteService = new SupplierQuotationService(db, tokenService, null, emailService);

        var tenantId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var creatorUserId = Guid.NewGuid();
        var approverUserId = Guid.NewGuid();

        var seed = await SeedStep4Async(db, tokenService, tenantId, companyId, branchId, "BADEMAIL");
        db.Entry(seed.Rfq).Property(x => x.CreatedBy).CurrentValue = creatorUserId;

        // Set invalid email format
        db.Entry(seed.Supplier).Property(x => x.Email).CurrentValue = "not-an-email";
        var rfqSup = await db.RFQSuppliers.FirstAsync(s => s.SupplierId == seed.Supplier.SupplierId && s.RFQId == seed.Rfq.Id);
        rfqSup.ContactEmail = "not-an-email";
        await db.SaveChangesAsync();

        var quote = new SupplierQuotation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            BranchId = branchId,
            RFQId = seed.Rfq.Id,
            SupplierId = seed.Supplier.SupplierId,
            SupplierCode = seed.Supplier.SupplierCode,
            SupplierName = seed.Supplier.SupplierName,
            QuotationReference = "QT-BADEMAIL-001",
            QuotationDate = DateTime.UtcNow,
            ValidUntil = DateTime.UtcNow.AddDays(14),
            CurrencyId = seed.Currency.CurrencyId,
            CurrencyCode = seed.Currency.CurrencyCode,
            TotalAmount = 150000m,
            BaseTotalAmount = 150000m,
            Status = SupplierQuotationStatus.Submitted
        };

        await db.SupplierQuotations.AddAsync(quote);
        await db.SaveChangesAsync();

        // Act
        var result = await quoteService.ApproveQuotationAsync(
            seed.Rfq.Id, quote.Id, new ApproveQuotationRequest { Remarks = "Awarded with invalid email" }, approverUserId, tenantId, companyId, branchId);

        // Assert: Email is not dispatched due to invalid format
        Assert.Equal(SupplierQuotationStatus.Approved, result.Status);
        Assert.Equal(0, emailService.AwardCallCount);
        Assert.Equal("Failed", result.AwardNotificationStatus);
        Assert.NotNull(result.AwardNotificationFailureReason);
        Assert.Contains("invalid", result.AwardNotificationFailureReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AwardNotification_ConcurrentRetries_DoNotSendDuplicateEmails()
    {
        // Arrange
        using var db = CreateInMemoryDomainDb();
        var tokenService = new RfqTokenService(db);
        var emailService = new StubRfqEmailNotificationService();
        var quoteService = new SupplierQuotationService(db, tokenService, null, emailService);

        var tenantId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var creatorUserId = Guid.NewGuid();
        var approverUserId = Guid.NewGuid();

        var seed = await SeedStep4Async(db, tokenService, tenantId, companyId, branchId, "CONCUR");
        db.Entry(seed.Rfq).Property(x => x.CreatedBy).CurrentValue = creatorUserId;

        var quote = new SupplierQuotation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            BranchId = branchId,
            RFQId = seed.Rfq.Id,
            SupplierId = seed.Supplier.SupplierId,
            SupplierCode = seed.Supplier.SupplierCode,
            SupplierName = seed.Supplier.SupplierName,
            QuotationReference = "QT-CONCUR-001",
            QuotationDate = DateTime.UtcNow,
            ValidUntil = DateTime.UtcNow.AddDays(14),
            CurrencyId = seed.Currency.CurrencyId,
            CurrencyCode = seed.Currency.CurrencyCode,
            TotalAmount = 250000m,
            BaseTotalAmount = 250000m,
            Status = SupplierQuotationStatus.Submitted
        };

        await db.SupplierQuotations.AddAsync(quote);
        await db.SaveChangesAsync();

        // 1. Initial award (sends notification)
        await quoteService.ApproveQuotationAsync(
            seed.Rfq.Id, quote.Id, new ApproveQuotationRequest { Remarks = "Approved for concurrency test" }, approverUserId, tenantId, companyId, branchId);
        Assert.Equal(1, emailService.AwardCallCount);

        // 2. Simulate 5 concurrent retry tasks
        var tasks = Enumerable.Range(0, 5).Select(_ =>
            Task.Run(async () =>
            {
                try
                {
                    await quoteService.RetryAwardNotificationAsync(
                        seed.Rfq.Id, quote.Id, approverUserId, tenantId, companyId, branchId);
                }
                catch (InvalidOperationException)
                {
                    // Expected when already sent or lock rejected
                }
            })).ToArray();

        await Task.WhenAll(tasks);

        // Assert: Email was sent exactly once, never duplicated
        Assert.Equal(1, emailService.AwardCallCount);
    }

    [Fact]
    public async Task AwardNotification_TenantCompanyBranchIsolation_Enforced()
    {
        // Arrange
        using var db = CreateInMemoryDomainDb();
        var tokenService = new RfqTokenService(db);
        var emailService = new StubRfqEmailNotificationService();
        var quoteService = new SupplierQuotationService(db, tokenService, null, emailService);

        var tenantId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var creatorUserId = Guid.NewGuid();
        var approverUserId = Guid.NewGuid();

        var seed = await SeedStep4Async(db, tokenService, tenantId, companyId, branchId, "ISOLATE");
        db.Entry(seed.Rfq).Property(x => x.CreatedBy).CurrentValue = creatorUserId;

        var quote = new SupplierQuotation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            BranchId = branchId,
            RFQId = seed.Rfq.Id,
            SupplierId = seed.Supplier.SupplierId,
            SupplierCode = seed.Supplier.SupplierCode,
            SupplierName = seed.Supplier.SupplierName,
            QuotationReference = "QT-ISOLATE-001",
            QuotationDate = DateTime.UtcNow,
            ValidUntil = DateTime.UtcNow.AddDays(14),
            CurrencyId = seed.Currency.CurrencyId,
            CurrencyCode = seed.Currency.CurrencyCode,
            TotalAmount = 250000m,
            BaseTotalAmount = 250000m,
            Status = SupplierQuotationStatus.Submitted
        };

        await db.SupplierQuotations.AddAsync(quote);
        await db.SaveChangesAsync();

        var wrongTenantId = Guid.NewGuid();
        var wrongCompanyId = Guid.NewGuid();
        var wrongBranchId = Guid.NewGuid();

        // Act & Assert: Cross-tenant approval must fail
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            quoteService.ApproveQuotationAsync(seed.Rfq.Id, quote.Id, new ApproveQuotationRequest(), approverUserId, wrongTenantId, companyId, branchId));

        // Cross-company approval must fail
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            quoteService.ApproveQuotationAsync(seed.Rfq.Id, quote.Id, new ApproveQuotationRequest(), approverUserId, tenantId, wrongCompanyId, branchId));

        // Cross-branch approval must fail
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            quoteService.ApproveQuotationAsync(seed.Rfq.Id, quote.Id, new ApproveQuotationRequest(), approverUserId, tenantId, companyId, wrongBranchId));

        // Ensure 0 emails were dispatched
        Assert.Equal(0, emailService.AwardCallCount);
    }
}
