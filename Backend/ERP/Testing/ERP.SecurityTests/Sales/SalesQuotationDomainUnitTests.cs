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
}
