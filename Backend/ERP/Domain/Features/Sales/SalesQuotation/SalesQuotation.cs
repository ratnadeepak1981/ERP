using System;
using System.Collections.Generic;
using System.Linq;
using Domain.Features.MasterData.Currency;
using ERP.Domain.Common;
using ERP.Domain.Common.Scope;

namespace Domain.Features.Sales.SalesQuotation;

public class SalesQuotation : Auditable, ITenantScopedEntity, ICompanyScopedEntity, IBranchScopedEntity
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid CompanyId { get; set; }

    public Guid BranchId { get; set; }

    public Guid CustomerId { get; set; }

    public Guid? CustomerContactId { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public string QuotationNumber { get; set; } = string.Empty;

    public int RevisionNumber { get; set; } = 0;

    public SalesQuotationType DocumentType { get; set; } = SalesQuotationType.Standard;

    public DateTime QuotationDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Mandatory expiration boundary in UTC (expiring at 23:59:59.999 UTC on the calendar date).
    /// </summary>
    public DateTime ValidUntilDateUtc { get; set; }

    public DateTime? DeliveryDate { get; set; }

    public string Currency { get; set; } = "USD";

    public Guid? TransactionCurrencyId { get; private set; }

    public Guid? BaseCurrencyId { get; private set; }

    public string? BaseCurrencyCode { get; private set; }

    public int? BaseCurrencyDecimalPlaces { get; private set; }

    public decimal? AppliedExchangeRate { get; private set; }

    public ExchangeRateSource? ExchangeRateSource { get; private set; }

    public Guid? ExchangeRateId { get; private set; }

    public DateTime? ExchangeRateEffectiveDate { get; private set; }

    public Guid? PriceCardId { get; set; }

    public decimal? BaseSubTotal { get; private set; }

    public decimal? BaseDiscountAmount { get; private set; }

    public decimal? BaseTaxAmount { get; private set; }

    public decimal? BaseTotalAmount { get; private set; }

    public string? PaymentTerms { get; set; }

    public string? DeliveryTerms { get; set; }

    public SalesQuotationStatus Status { get; set; } = SalesQuotationStatus.Draft;

    public decimal SubTotal { get; set; }

    public decimal DiscountAmount { get; set; }

    public decimal TaxAmount { get; set; }

    public decimal TotalAmount { get; set; }

    public string? Notes { get; set; }

    public string? CustomerRemarks { get; set; }

    public string? InternalNotes { get; set; }

    public Guid? DeliveryAddressId { get; set; }

    public string? DeliveryAddressSnapshot { get; set; }

    public Guid? SubmittedByUserId { get; set; }

    public DateTime? SubmittedAtUtc { get; set; }

    public Guid? ApprovedByUserId { get; set; }

    public DateTime? ApprovedAtUtc { get; set; }

    public Guid? RejectedByUserId { get; set; }

    public DateTime? RejectedAtUtc { get; set; }

    public string? RejectionRemarks { get; set; }

    public DateTime? SentAtUtc { get; set; }

    public DateTime? AcceptedAtUtc { get; set; }

    public DateTime? DeclinedAtUtc { get; set; }

    public string? DeclinedRemarks { get; set; }

    public Guid? ConvertedSalesOrderId { get; set; }

    public DateTime? ConvertedAtUtc { get; set; }

    public Guid? SupersededByQuotationId { get; set; }

    public bool IsActive { get; set; } = true;

    public List<SalesQuotationItem> Items { get; set; } = new();

    public static SalesQuotation Create(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid customerId,
        string quotationNumber,
        DateTime validUntilDateUtc,
        SalesQuotationType documentType = SalesQuotationType.Standard,
        DateTime? quotationDate = null,
        DateTime? deliveryDate = null,
        string? currency = null,
        string? paymentTerms = null,
        string? deliveryTerms = null,
        string? notes = null,
        string? customerRemarks = null,
        string? internalNotes = null,
        Guid? createdByUserId = null,
        Guid? customerContactId = null,
        Guid? deliveryAddressId = null,
        string? deliveryAddressSnapshot = null,
        Guid? priceCardId = null)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (companyId == Guid.Empty) throw new ArgumentException("CompanyId is required.", nameof(companyId));
        if (branchId == Guid.Empty) throw new ArgumentException("BranchId is required.", nameof(branchId));
        if (customerId == Guid.Empty) throw new ArgumentException("CustomerId is required.", nameof(customerId));
        if (string.IsNullOrWhiteSpace(quotationNumber)) throw new ArgumentException("QuotationNumber is required.", nameof(quotationNumber));

        var quotation = new SalesQuotation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            BranchId = branchId,
            CustomerId = customerId,
            CustomerContactId = customerContactId,
            CreatedByUserId = createdByUserId,
            QuotationNumber = quotationNumber.Trim(),
            DocumentType = documentType,
            QuotationDate = quotationDate ?? DateTime.UtcNow,
            DeliveryDate = deliveryDate,
            Currency = string.IsNullOrWhiteSpace(currency) ? "USD" : currency.Trim().ToUpperInvariant(),
            PaymentTerms = paymentTerms,
            DeliveryTerms = deliveryTerms,
            Notes = notes,
            CustomerRemarks = customerRemarks,
            InternalNotes = internalNotes,
            DeliveryAddressId = deliveryAddressId,
            DeliveryAddressSnapshot = deliveryAddressSnapshot,
            PriceCardId = priceCardId,
            Status = SalesQuotationStatus.Draft
        };

        quotation.SetValidUntilDate(validUntilDateUtc);
        return quotation;
    }

    /// <summary>
    /// Creates a customer-initiated quotation request from Explore Products or Customer Portal.
    /// </summary>
    public static SalesQuotation CreateCustomerRequest(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid customerId,
        Guid customerContactId,
        string quotationNumber,
        DateTime validUntilDateUtc,
        string? customerRemarks = null,
        string? currency = null,
        Guid? createdByUserId = null)
    {
        return Create(
            tenantId,
            companyId,
            branchId,
            customerId,
            quotationNumber,
            validUntilDateUtc,
            documentType: SalesQuotationType.CustomerRequest,
            customerRemarks: customerRemarks,
            currency: currency,
            createdByUserId: createdByUserId,
            customerContactId: customerContactId);
    }

    /// <summary>
    /// Standardizes the mandatory ValidUntilDate to the end-of-calendar-day in UTC (23:59:59.999 UTC).
    /// </summary>
    public void SetValidUntilDate(DateTime dateUtc)
    {
        DateTime normalizedDate = dateUtc.Kind == DateTimeKind.Utc ? dateUtc.Date : DateTime.SpecifyKind(dateUtc.Date, DateTimeKind.Utc);
        ValidUntilDateUtc = normalizedDate.AddHours(23).AddMinutes(59).AddSeconds(59).AddMilliseconds(999);
    }

    public void SetCurrencySnapshot(
        Guid transactionCurrencyId,
        Guid baseCurrencyId,
        string baseCurrencyCode,
        int baseCurrencyDecimalPlaces,
        decimal appliedExchangeRate,
        ExchangeRateSource exchangeRateSource,
        Guid? exchangeRateId = null,
        DateTime? exchangeRateEffectiveDate = null)
    {
        if (Status != SalesQuotationStatus.Draft)
            throw new InvalidOperationException($"Cannot modify currency snapshot when quotation is in status '{Status}'.");

        TransactionCurrencyId = transactionCurrencyId;
        BaseCurrencyId = baseCurrencyId;
        BaseCurrencyCode = baseCurrencyCode;
        BaseCurrencyDecimalPlaces = baseCurrencyDecimalPlaces;
        AppliedExchangeRate = appliedExchangeRate;
        ExchangeRateSource = exchangeRateSource;
        ExchangeRateId = exchangeRateId;
        ExchangeRateEffectiveDate = exchangeRateEffectiveDate;

        RecalculateTotals();
    }

    public SalesQuotationItem AddItem(
        Guid productId,
        decimal quantity,
        decimal unitPrice,
        decimal discountRate = 0,
        Guid? taxCodeId = null,
        decimal taxRate = 0,
        Guid? unitOfMeasureId = null,
        bool isTaxInclusive = false,
        string? notes = null)
    {
        if (Status != SalesQuotationStatus.Draft)
            throw new InvalidOperationException($"Cannot add items when quotation is in status '{Status}'. Authoring is locked.");

        if (productId == Guid.Empty) throw new ArgumentException("ProductId is required.", nameof(productId));
        if (quantity <= 0) throw new ArgumentException("Quantity must be greater than zero.", nameof(quantity));
        if (unitPrice < 0) throw new ArgumentException("UnitPrice cannot be negative.", nameof(unitPrice));

        var item = new SalesQuotationItem
        {
            Id = Guid.NewGuid(),
            SalesQuotationId = Id,
            ProductId = productId,
            UnitOfMeasureId = unitOfMeasureId,
            Quantity = quantity,
            UnitPrice = unitPrice,
            DiscountRate = discountRate,
            TaxCodeId = taxCodeId,
            TaxRate = taxRate,
            IsTaxInclusive = isTaxInclusive,
            Notes = notes,
            IsActive = true
        };

        item.RecalculateLine();
        Items.Add(item);
        RecalculateTotals();
        return item;
    }

    public void UpdateItem(
        Guid itemId,
        decimal quantity,
        decimal unitPrice,
        decimal discountRate,
        Guid? taxCodeId,
        decimal taxRate,
        Guid? unitOfMeasureId,
        bool isTaxInclusive,
        string? notes)
    {
        if (Status != SalesQuotationStatus.Draft)
            throw new InvalidOperationException($"Cannot modify items when quotation is in status '{Status}'. Authoring is locked.");

        var item = Items.FirstOrDefault(i => i.Id == itemId && i.IsActive);
        if (item == null)
            throw new InvalidOperationException($"Quotation item '{itemId}' was not found.");

        if (quantity <= 0) throw new ArgumentException("Quantity must be greater than zero.", nameof(quantity));
        if (unitPrice < 0) throw new ArgumentException("UnitPrice cannot be negative.", nameof(unitPrice));

        item.Quantity = quantity;
        item.UnitPrice = unitPrice;
        item.DiscountRate = discountRate;
        item.TaxCodeId = taxCodeId;
        item.TaxRate = taxRate;
        item.UnitOfMeasureId = unitOfMeasureId;
        item.IsTaxInclusive = isTaxInclusive;
        item.Notes = notes;

        item.RecalculateLine();
        RecalculateTotals();
    }

    public void RemoveItem(Guid itemId)
    {
        if (Status != SalesQuotationStatus.Draft)
            throw new InvalidOperationException($"Cannot remove items when quotation is in status '{Status}'. Authoring is locked.");

        var item = Items.FirstOrDefault(i => i.Id == itemId && i.IsActive);
        if (item == null)
            throw new InvalidOperationException($"Quotation item '{itemId}' was not found.");

        item.IsActive = false;
        RecalculateTotals();
    }

    public void RecalculateTotals()
    {
        var activeItems = Items.Where(i => i.IsActive).ToList();

        SubTotal = Math.Round(activeItems.Sum(i => i.SubTotal), 2, MidpointRounding.AwayFromZero);
        DiscountAmount = Math.Round(activeItems.Sum(i => i.DiscountAmount), 2, MidpointRounding.AwayFromZero);
        TaxAmount = Math.Round(activeItems.Sum(i => i.TaxAmount), 2, MidpointRounding.AwayFromZero);
        TotalAmount = Math.Round(activeItems.Sum(i => i.LineTotal), 2, MidpointRounding.AwayFromZero);

        if (AppliedExchangeRate.HasValue && AppliedExchangeRate.Value > 0)
        {
            int decimals = BaseCurrencyDecimalPlaces ?? 2;
            BaseSubTotal = Math.Round(SubTotal * AppliedExchangeRate.Value, decimals, MidpointRounding.AwayFromZero);
            BaseDiscountAmount = Math.Round(DiscountAmount * AppliedExchangeRate.Value, decimals, MidpointRounding.AwayFromZero);
            BaseTaxAmount = Math.Round(TaxAmount * AppliedExchangeRate.Value, decimals, MidpointRounding.AwayFromZero);
            BaseTotalAmount = Math.Round(TotalAmount * AppliedExchangeRate.Value, decimals, MidpointRounding.AwayFromZero);
        }
        else
        {
            BaseSubTotal = SubTotal;
            BaseDiscountAmount = DiscountAmount;
            BaseTaxAmount = TaxAmount;
            BaseTotalAmount = TotalAmount;
        }
    }

    /// <summary>
    /// Submits the quotation for conditional internal managerial/admin approval.
    /// Locks authoring and transitions status from Draft -> Submitted.
    /// </summary>
    public void SubmitForApproval(Guid actorUserId)
    {
        if (Status != SalesQuotationStatus.Draft)
            throw new InvalidOperationException($"Cannot submit quotation with status '{Status}'. Only Draft quotations can be submitted for approval.");

        if (!Items.Any(i => i.IsActive))
            throw new InvalidOperationException("Cannot submit a quotation without at least one active item.");

        if (DateTime.UtcNow > ValidUntilDateUtc)
            throw new InvalidOperationException("Cannot submit an expired quotation for approval. Please update the ValidUntilDate.");

        Status = SalesQuotationStatus.Submitted;
        SubmittedByUserId = actorUserId;
        SubmittedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Admin/Managerial approval. Transitions status from Submitted -> Confirmed.
    /// Ready for formal customer issuance (Sent).
    /// </summary>
    public void Approve(Guid approverUserId)
    {
        if (Status != SalesQuotationStatus.Submitted)
            throw new InvalidOperationException($"Cannot approve quotation with status '{Status}'. Only Submitted quotations can be approved.");

        if (DateTime.UtcNow > ValidUntilDateUtc)
            throw new InvalidOperationException("Cannot approve an expired quotation. The validity period has elapsed.");

        Status = SalesQuotationStatus.Confirmed;
        ApprovedByUserId = approverUserId;
        ApprovedAtUtc = DateTime.UtcNow;
        RejectedByUserId = null;
        RejectedAtUtc = null;
        RejectionRemarks = null;
    }

    /// <summary>
    /// Admin/Managerial rejection with mandatory remarks. Transitions status from Submitted -> RejectedInternal.
    /// </summary>
    public void Reject(Guid approverUserId, string remarks)
    {
        if (Status != SalesQuotationStatus.Submitted)
            throw new InvalidOperationException($"Cannot reject quotation with status '{Status}'. Only Submitted quotations can be rejected.");

        if (string.IsNullOrWhiteSpace(remarks))
            throw new ArgumentException("Mandatory remarks are required when rejecting a quotation internally.", nameof(remarks));

        Status = SalesQuotationStatus.RejectedInternal;
        RejectedByUserId = approverUserId;
        RejectedAtUtc = DateTime.UtcNow;
        RejectionRemarks = remarks.Trim();
    }

    /// <summary>
    /// Cancels a quotation prior to customer acceptance.
    /// </summary>
    public void Cancel(Guid actorUserId)
    {
        if (Status == SalesQuotationStatus.Converted ||
            Status == SalesQuotationStatus.Accepted ||
            Status == SalesQuotationStatus.Cancelled)
        {
            throw new InvalidOperationException($"Cannot cancel quotation in terminal status '{Status}'.");
        }

        Status = SalesQuotationStatus.Cancelled;
    }

    /// <summary>
    /// Formally issues the confirmed quotation to the customer.
    /// Transitions status from Confirmed -> Sent.
    /// </summary>
    public void MarkSent(DateTime? sentAtUtc = null)
    {
        if (Status != SalesQuotationStatus.Confirmed && Status != SalesQuotationStatus.Draft)
            throw new InvalidOperationException($"Cannot issue quotation with status '{Status}'. Only Confirmed or Draft quotations can be sent to customers.");

        if (IsExpired())
            throw new InvalidOperationException("Cannot issue an expired quotation. Please update the ValidUntilDate before sending.");

        Status = SalesQuotationStatus.Sent;
        SentAtUtc = sentAtUtc ?? DateTime.UtcNow;
    }

    /// <summary>
    /// Customer acceptance of valid, formally issued proposal.
    /// Transitions status from Sent -> Accepted.
    /// </summary>
    public void Accept(Guid contactId, DateTime? acceptedAtUtc = null)
    {
        if (Status != SalesQuotationStatus.Sent)
            throw new InvalidOperationException($"Cannot accept quotation with status '{Status}'. Only formally issued (Sent) quotations can be accepted.");

        if (IsExpired())
            throw new InvalidOperationException("Cannot accept an expired quotation. The validity period has elapsed.");

        if (ConvertedSalesOrderId.HasValue)
            throw new InvalidOperationException("This quotation has already been converted into a Sales Order.");

        Status = SalesQuotationStatus.Accepted;
        CustomerContactId = contactId;
        AcceptedAtUtc = acceptedAtUtc ?? DateTime.UtcNow;
    }

    /// <summary>
    /// Atomically links the converted official Sales Order.
    /// Transitions status -> Converted.
    /// </summary>
    public void MarkConverted(Guid salesOrderId, DateTime? convertedAtUtc = null)
    {
        if (salesOrderId == Guid.Empty)
            throw new ArgumentException("SalesOrderId is required.", nameof(salesOrderId));

        if (Status != SalesQuotationStatus.Accepted && Status != SalesQuotationStatus.Sent)
            throw new InvalidOperationException($"Cannot convert quotation with status '{Status}'. Quotation must be Accepted or Sent.");

        Status = SalesQuotationStatus.Converted;
        ConvertedSalesOrderId = salesOrderId;
        ConvertedAtUtc = convertedAtUtc ?? DateTime.UtcNow;
    }

    /// <summary>
    /// Customer declines the formally issued proposal with mandatory remarks.
    /// Transitions status from Sent -> DeclinedByCustomer.
    /// </summary>
    public void Decline(Guid contactId, string remarks, DateTime? declinedAtUtc = null)
    {
        if (Status != SalesQuotationStatus.Sent)
            throw new InvalidOperationException($"Cannot decline quotation with status '{Status}'. Only formally issued (Sent) quotations can be declined.");

        if (string.IsNullOrWhiteSpace(remarks))
            throw new ArgumentException("Remarks are required when declining a quotation.", nameof(remarks));

        Status = SalesQuotationStatus.DeclinedByCustomer;
        CustomerContactId = contactId;
        DeclinedRemarks = remarks.Trim();
        DeclinedAtUtc = declinedAtUtc ?? DateTime.UtcNow;
    }

    /// <summary>
    /// Marks the issued quotation superseded by a newer formal revision.
    /// </summary>
    public void MarkSuperseded(Guid newRevisionQuotationId)
    {
        if (newRevisionQuotationId == Guid.Empty)
            throw new ArgumentException("New revision quotation ID is required.", nameof(newRevisionQuotationId));

        Status = SalesQuotationStatus.Superseded;
        SupersededByQuotationId = newRevisionQuotationId;
    }

    public bool IsExpired() => DateTime.UtcNow > ValidUntilDateUtc;
}
