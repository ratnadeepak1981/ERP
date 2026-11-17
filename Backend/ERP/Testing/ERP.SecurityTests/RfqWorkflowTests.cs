using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Domain.Features.MasterData.Currency;
using Domain.Features.MasterData.Product;
using Domain.Features.MasterData.UnitOfMeasure;
using Domain.Features.Procurement.RequestForQuotation;
using Domain.Infrastructure.Persistence;
using ERP.Domain.Features.MasterData.Supplier;
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
        var matrix = await quoteService.GetComparisonMatrixAsync(rfq.Id, tenantId);
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
        var quotes = await quoteService.GetQuotationsForRfqAsync(rfq.Id, tenantId);
        var quoteToApprove = quotes.First(q => q.SupplierId == supplierB.SupplierId);

        var approveRequest = new ApproveQuotationRequest { Remarks = "Awarded lowest evaluated price" };
        var selfApprovalEx = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            quoteService.ApproveQuotationAsync(rfq.Id, quoteToApprove.Id, approveRequest, creatorUserId, tenantId));
        Assert.Contains("separation of duties", selfApprovalEx.Message, StringComparison.OrdinalIgnoreCase);

        // 7. Legitimate Approval by independent approver (ApproverUserId != CreatorUserId)
        var approvedQuote = await quoteService.ApproveQuotationAsync(rfq.Id, quoteToApprove.Id, approveRequest, approverUserId, tenantId);
        Assert.Equal(SupplierQuotationStatus.Approved, approvedQuote.Status);

        // RFQ transitions to Awarded
        var awardedRfq = await rfqService.GetRfqByIdAsync(rfq.Id, tenantId);
        Assert.Equal(RequestForQuotationStatus.Awarded, awardedRfq!.Status);

        // 8. Multi-Award Support: Can approve another quotation on the same RFQ
        var quoteAEntity = quotes.First(q => q.SupplierId == supplierA.SupplierId);
        var approvedQuoteA = await quoteService.ApproveQuotationAsync(rfq.Id, quoteAEntity.Id, new ApproveQuotationRequest { Remarks = "Secondary award for backup supply" }, approverUserId, tenantId);
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
            quoteService.ApproveQuotationAsync(rfq.Id, expiredQuote.Id, new ApproveQuotationRequest(), approverUserId, tenantId));

        Assert.Contains("expired", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
