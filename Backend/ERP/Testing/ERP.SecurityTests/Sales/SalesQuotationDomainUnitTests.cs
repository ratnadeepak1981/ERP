using System;
using System.Linq;
using Domain.Features.Sales.SalesQuotation;
using Xunit;

namespace ERP.SecurityTests.Sales;

public class SalesQuotationDomainUnitTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _branchId = Guid.NewGuid();
    private readonly Guid _customerId = Guid.NewGuid();
    private readonly Guid _productId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _approverId = Guid.NewGuid();

    [Fact]
    public void SalesQuotation_Create_InitializesWithCorrectDefaults()
    {
        var validUntil = DateTime.UtcNow.AddDays(15);
        var quote = SalesQuotation.Create(
            _tenantId,
            _companyId,
            _branchId,
            _customerId,
            "SQ-2026-0001",
            validUntil,
            SalesQuotationType.Standard,
            notes: "Initial quote proposal",
            createdByUserId: _userId);

        Assert.Equal(_tenantId, quote.TenantId);
        Assert.Equal(_companyId, quote.CompanyId);
        Assert.Equal(_branchId, quote.BranchId);
        Assert.Equal(_customerId, quote.CustomerId);
        Assert.Equal("SQ-2026-0001", quote.QuotationNumber);
        Assert.Equal(SalesQuotationType.Standard, quote.DocumentType);
        Assert.Equal(SalesQuotationStatus.Draft, quote.Status);
        Assert.Equal("USD", quote.Currency);
        Assert.Equal(0m, quote.SubTotal);
        Assert.Equal(0m, quote.DiscountAmount);
        Assert.Equal(0m, quote.TaxAmount);
        Assert.Equal(0m, quote.TotalAmount);
        Assert.True(quote.IsActive);
        Assert.Empty(quote.Items);
        Assert.False(quote.IsExpired());

        // Verify end of calendar day in UTC: 23:59:59.999
        Assert.Equal(23, quote.ValidUntilDateUtc.Hour);
        Assert.Equal(59, quote.ValidUntilDateUtc.Minute);
        Assert.Equal(59, quote.ValidUntilDateUtc.Second);
        Assert.Equal(999, quote.ValidUntilDateUtc.Millisecond);
    }

    [Fact]
    public void SalesQuotation_CreateCustomerRequest_SetsCustomerRequestType()
    {
        var contactId = Guid.NewGuid();
        var validUntil = DateTime.UtcNow.AddDays(30);

        var request = SalesQuotation.CreateCustomerRequest(
            _tenantId,
            _companyId,
            _branchId,
            _customerId,
            contactId,
            "SQ-REQ-0001",
            validUntil,
            customerRemarks: "Urgent bulk quote needed",
            currency: "EUR",
            createdByUserId: _userId);

        Assert.Equal(SalesQuotationType.CustomerRequest, request.DocumentType);
        Assert.Equal(SalesQuotationStatus.Draft, request.Status);
        Assert.Equal("EUR", request.Currency);
        Assert.Equal("Urgent bulk quote needed", request.CustomerRemarks);
        Assert.Equal(contactId, request.CustomerContactId);
    }

    [Fact]
    public void SalesQuotation_AddItem_CalculatesLinesAndTotalsCorrectly()
    {
        var validUntil = DateTime.UtcNow.AddDays(10);
        var quote = SalesQuotation.Create(_tenantId, _companyId, _branchId, _customerId, "SQ-CALC", validUntil);

        // Line 1: Quantity: 10, UnitPrice: 100, Discount: 10%, Tax: 5% (Exclusive)
        // SubTotal: 1000 - 100 = 900
        // Discount: 100
        // Tax: 900 * 0.05 = 45
        // LineTotal: 945
        quote.AddItem(_productId, 10m, 100m, discountRate: 10m, taxRate: 5m, isTaxInclusive: false);

        Assert.Single(quote.Items);
        var item = quote.Items[0];
        Assert.Equal(900m, item.SubTotal);
        Assert.Equal(100m, item.DiscountAmount);
        Assert.Equal(45m, item.TaxAmount);
        Assert.Equal(945m, item.LineTotal);

        Assert.Equal(900m, quote.SubTotal);
        Assert.Equal(100m, quote.DiscountAmount);
        Assert.Equal(45m, quote.TaxAmount);
        Assert.Equal(945m, quote.TotalAmount);
    }

    [Fact]
    public void SalesQuotation_SubmitForApproval_LocksDraftAndTransitionsToSubmitted()
    {
        var quote = SalesQuotation.Create(
            _tenantId, _companyId, _branchId, _customerId, "SQ-SUBMIT", DateTime.UtcNow.AddDays(5), createdByUserId: _userId);
        quote.AddItem(_productId, 5m, 50m);

        Assert.Equal(SalesQuotationStatus.Draft, quote.Status);

        quote.SubmitForApproval(_userId);

        Assert.Equal(SalesQuotationStatus.Submitted, quote.Status);
        Assert.Equal(_userId, quote.SubmittedByUserId);
        Assert.NotNull(quote.SubmittedAtUtc);

        // Assert authoring is locked: Cannot add item once submitted
        var ex = Assert.Throws<InvalidOperationException>(() =>
            quote.AddItem(Guid.NewGuid(), 1m, 10m));
        Assert.Contains("Authoring is locked", ex.Message);
    }

    [Fact]
    public void SalesQuotation_SubmitWithoutItems_ThrowsInvalidOperationException()
    {
        var quote = SalesQuotation.Create(_tenantId, _companyId, _branchId, _customerId, "SQ-EMPTY", DateTime.UtcNow.AddDays(5));

        var ex = Assert.Throws<InvalidOperationException>(() => quote.SubmitForApproval(_userId));
        Assert.Contains("Cannot submit a quotation without at least one active item", ex.Message);
    }

    [Fact]
    public void SalesQuotation_SubmitExpiredQuotation_ThrowsInvalidOperationException()
    {
        var quote = SalesQuotation.Create(_tenantId, _companyId, _branchId, _customerId, "SQ-EXP", DateTime.UtcNow.AddDays(-1));
        quote.AddItem(_productId, 2m, 25m);

        var ex = Assert.Throws<InvalidOperationException>(() => quote.SubmitForApproval(_userId));
        Assert.Contains("Cannot submit an expired quotation", ex.Message);
    }

    [Fact]
    public void SalesQuotation_Approve_TransitionsToConfirmed()
    {
        var quote = SalesQuotation.Create(_tenantId, _companyId, _branchId, _customerId, "SQ-APP", DateTime.UtcNow.AddDays(10), createdByUserId: _userId);
        quote.AddItem(_productId, 4m, 100m);
        quote.SubmitForApproval(_userId);

        quote.Approve(_approverId);

        Assert.Equal(SalesQuotationStatus.Confirmed, quote.Status);
        Assert.Equal(_approverId, quote.ApprovedByUserId);
        Assert.NotNull(quote.ApprovedAtUtc);
    }

    [Fact]
    public void SalesQuotation_ApproveDirectlyFromDraft_ThrowsInvalidOperationException()
    {
        var quote = SalesQuotation.Create(_tenantId, _companyId, _branchId, _customerId, "SQ-INVALID-APP", DateTime.UtcNow.AddDays(10));
        quote.AddItem(_productId, 1m, 100m);

        var ex = Assert.Throws<InvalidOperationException>(() => quote.Approve(_approverId));
        Assert.Contains("Only Submitted quotations can be approved", ex.Message);
    }

    [Fact]
    public void SalesQuotation_Reject_RequiresRemarksAndTransitionsToRejectedInternal()
    {
        var quote = SalesQuotation.Create(_tenantId, _companyId, _branchId, _customerId, "SQ-REJ", DateTime.UtcNow.AddDays(10), createdByUserId: _userId);
        quote.AddItem(_productId, 4m, 100m);
        quote.SubmitForApproval(_userId);

        // Attempting reject with empty remarks fails
        Assert.Throws<ArgumentException>(() => quote.Reject(_approverId, "  "));

        quote.Reject(_approverId, "Margin is below target threshold of 20%. Please revise discounts.");

        Assert.Equal(SalesQuotationStatus.RejectedInternal, quote.Status);
        Assert.Equal(_approverId, quote.RejectedByUserId);
        Assert.NotNull(quote.RejectedAtUtc);
        Assert.Equal("Margin is below target threshold of 20%. Please revise discounts.", quote.RejectionRemarks);
    }

    [Fact]
    public void SalesQuotation_Cancel_TransitionsToCancelled()
    {
        var quote = SalesQuotation.Create(_tenantId, _companyId, _branchId, _customerId, "SQ-CANCEL", DateTime.UtcNow.AddDays(10));
        quote.AddItem(_productId, 1m, 100m);

        quote.Cancel(_userId);
        Assert.Equal(SalesQuotationStatus.Cancelled, quote.Status);
    }

    [Fact]
    public void SalesQuotation_Accept_WhenSent_TransitionsToAccepted()
    {
        var quote = SalesQuotation.Create(_tenantId, _companyId, _branchId, _customerId, "SQ-ACCEPT", DateTime.UtcNow.AddDays(10), createdByUserId: _userId);
        quote.AddItem(_productId, 2m, 50m);
        quote.SubmitForApproval(_userId);
        quote.Approve(_approverId);
        quote.MarkSent();

        Assert.Equal(SalesQuotationStatus.Sent, quote.Status);

        var contactId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        quote.Accept(contactId, now);

        Assert.Equal(SalesQuotationStatus.Accepted, quote.Status);
        Assert.Equal(contactId, quote.CustomerContactId);
        Assert.Equal(now, quote.AcceptedAtUtc);
    }

    [Fact]
    public void SalesQuotation_Accept_WhenNotSent_ThrowsInvalidOperationException()
    {
        var quote = SalesQuotation.Create(_tenantId, _companyId, _branchId, _customerId, "SQ-ACCEPT-DRAFT", DateTime.UtcNow.AddDays(10), createdByUserId: _userId);
        quote.AddItem(_productId, 2m, 50m);

        // Draft
        var exDraft = Assert.Throws<InvalidOperationException>(() => quote.Accept(Guid.NewGuid()));
        Assert.Contains("Only formally issued (Sent) quotations can be accepted", exDraft.Message);

        quote.SubmitForApproval(_userId);
        quote.Approve(_approverId);

        // Confirmed (Approved internally but not sent)
        var exConfirmed = Assert.Throws<InvalidOperationException>(() => quote.Accept(Guid.NewGuid()));
        Assert.Contains("Only formally issued (Sent) quotations can be accepted", exConfirmed.Message);
    }

    [Fact]
    public void SalesQuotation_Accept_WhenExpired_ThrowsInvalidOperationException()
    {
        var quote = SalesQuotation.Create(_tenantId, _companyId, _branchId, _customerId, "SQ-ACCEPT-EXP", DateTime.UtcNow.AddDays(1), createdByUserId: _userId);
        quote.AddItem(_productId, 1m, 50m);
        quote.SubmitForApproval(_userId);
        quote.Approve(_approverId);
        quote.MarkSent();

        // Set valid until in past via reflection to simulate expiration
        typeof(SalesQuotation).GetProperty(nameof(SalesQuotation.ValidUntilDateUtc))!
            .SetValue(quote, DateTime.UtcNow.AddDays(-1));

        Assert.True(quote.IsExpired());
        var ex = Assert.Throws<InvalidOperationException>(() => quote.Accept(Guid.NewGuid()));
        Assert.Contains("Cannot accept an expired quotation", ex.Message);
    }

    [Fact]
    public void SalesQuotation_Accept_WithEmptyContactId_ThrowsArgumentException()
    {
        var quote = SalesQuotation.Create(_tenantId, _companyId, _branchId, _customerId, "SQ-ACCEPT-NOCONTACT", DateTime.UtcNow.AddDays(10), createdByUserId: _userId);
        quote.AddItem(_productId, 1m, 50m);
        quote.SubmitForApproval(_userId);
        quote.Approve(_approverId);
        quote.MarkSent();

        var ex = Assert.Throws<ArgumentException>(() => quote.Accept(Guid.Empty));
        Assert.Contains("Customer contact ID is required", ex.Message);
    }

    [Fact]
    public void SalesQuotation_Decline_WhenSent_TransitionsToDeclinedByCustomer()
    {
        var quote = SalesQuotation.Create(_tenantId, _companyId, _branchId, _customerId, "SQ-DEC", DateTime.UtcNow.AddDays(10), createdByUserId: _userId);
        quote.AddItem(_productId, 1m, 50m);
        quote.SubmitForApproval(_userId);
        quote.Approve(_approverId);
        quote.MarkSent();

        var contactId = Guid.NewGuid();
        quote.Decline(contactId, "Price is higher than competing offer.");

        Assert.Equal(SalesQuotationStatus.DeclinedByCustomer, quote.Status);
        Assert.Equal(contactId, quote.CustomerContactId);
        Assert.Equal("Price is higher than competing offer.", quote.DeclinedRemarks);
        Assert.NotNull(quote.DeclinedAtUtc);
    }

    [Fact]
    public void SalesQuotation_Decline_WithoutRemarks_ThrowsArgumentException()
    {
        var quote = SalesQuotation.Create(_tenantId, _companyId, _branchId, _customerId, "SQ-DEC-NOREMARKS", DateTime.UtcNow.AddDays(10), createdByUserId: _userId);
        quote.AddItem(_productId, 1m, 50m);
        quote.SubmitForApproval(_userId);
        quote.Approve(_approverId);
        quote.MarkSent();

        Assert.Throws<ArgumentException>(() => quote.Decline(Guid.NewGuid(), "   "));
    }

    [Fact]
    public void SalesQuotation_MarkConverted_StrictlyRequiresAcceptedStatus()
    {
        var quote = SalesQuotation.Create(_tenantId, _companyId, _branchId, _customerId, "SQ-CONV-INVARIANT", DateTime.UtcNow.AddDays(10), createdByUserId: _userId);
        quote.AddItem(_productId, 1m, 50m);
        var orderId = Guid.NewGuid();

        // 1. Draft -> Convert must fail
        var exDraft = Assert.Throws<InvalidOperationException>(() => quote.MarkConverted(orderId));
        Assert.Contains("Quotation must be in Accepted status with verified customer acceptance", exDraft.Message);

        quote.SubmitForApproval(_userId);
        quote.Approve(_approverId);

        // 2. Confirmed (Approved internally) -> Convert must fail
        var exConfirmed = Assert.Throws<InvalidOperationException>(() => quote.MarkConverted(orderId));
        Assert.Contains("Quotation must be in Accepted status with verified customer acceptance", exConfirmed.Message);

        quote.MarkSent();

        // 3. Sent (Issued but unaccepted) -> Convert must fail
        var exSent = Assert.Throws<InvalidOperationException>(() => quote.MarkConverted(orderId));
        Assert.Contains("Quotation must be in Accepted status with verified customer acceptance", exSent.Message);

        // 4. Accepted -> Convert must succeed
        var contactId = Guid.NewGuid();
        quote.Accept(contactId);
        quote.MarkConverted(orderId);

        Assert.Equal(SalesQuotationStatus.Converted, quote.Status);
        Assert.Equal(orderId, quote.ConvertedSalesOrderId);
        Assert.NotNull(quote.ConvertedAtUtc);

        // 5. Already converted -> Convert again must fail
        var exSecond = Assert.Throws<InvalidOperationException>(() => quote.MarkConverted(Guid.NewGuid()));
        Assert.Contains("Quotation must be in Accepted status", exSecond.Message);
    }
}
