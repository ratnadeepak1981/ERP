using System;
using System.Collections.Generic;

namespace Domain.Features.Sales.SalesQuotation;

public class SalesQuotationDto
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid CompanyId { get; set; }

    public Guid BranchId { get; set; }

    public Guid CustomerId { get; set; }

    public string? CustomerName { get; set; }

    public string? CustomerCode { get; set; }

    public Guid? CustomerContactId { get; set; }

    public string? ContactPersonName { get; set; }

    public string? ContactEmail { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public string QuotationNumber { get; set; } = string.Empty;

    public int RevisionNumber { get; set; }

    public SalesQuotationType DocumentType { get; set; }

    public DateTime QuotationDate { get; set; }

    public DateTime ValidUntilDateUtc { get; set; }

    public int DaysUntilExpiry => (int)Math.Max(0, Math.Ceiling((ValidUntilDateUtc - DateTime.UtcNow).TotalDays));

    public bool IsExpired => DateTime.UtcNow > ValidUntilDateUtc;

    public DateTime? DeliveryDate { get; set; }

    public string Currency { get; set; } = "USD";

    public Guid? TransactionCurrencyId { get; set; }

    public Guid? BaseCurrencyId { get; set; }

    public string? BaseCurrencyCode { get; set; }

    public int? BaseCurrencyDecimalPlaces { get; set; }

    public decimal? AppliedExchangeRate { get; set; }

    public Guid? PriceCardId { get; set; }

    public string? PaymentTerms { get; set; }

    public string? DeliveryTerms { get; set; }

    public SalesQuotationStatus Status { get; set; }

    public decimal SubTotal { get; set; }

    public decimal DiscountAmount { get; set; }

    public decimal TaxAmount { get; set; }

    public decimal TotalAmount { get; set; }

    public decimal? BaseSubTotal { get; set; }

    public decimal? BaseDiscountAmount { get; set; }

    public decimal? BaseTaxAmount { get; set; }

    public decimal? BaseTotalAmount { get; set; }

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

    public List<SalesQuotationItemDto> Items { get; set; } = new();
}

public class SalesQuotationItemDto
{
    public Guid Id { get; set; }

    public Guid SalesQuotationId { get; set; }

    public Guid ProductId { get; set; }

    public string? ProductCode { get; set; }

    public string? ProductName { get; set; }

    public Guid? UnitOfMeasureId { get; set; }

    public string? UnitOfMeasureCode { get; set; }

    public decimal Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal DiscountRate { get; set; }

    public decimal DiscountAmount { get; set; }

    public Guid? TaxCodeId { get; set; }

    public string? TaxCodeName { get; set; }

    public decimal TaxRate { get; set; }

    public decimal TaxAmount { get; set; }

    public decimal SubTotal { get; set; }

    public decimal LineTotal { get; set; }

    public bool IsTaxInclusive { get; set; }

    public string? Notes { get; set; }

    public bool IsActive { get; set; }
}

public class CreateSalesQuotationDraftRequest
{
    public string QuotationNumber { get; set; } = string.Empty;

    public Guid CustomerId { get; set; }

    public Guid? CustomerContactId { get; set; }

    public DateTime? QuotationDate { get; set; }

    public DateTime ValidUntilDateUtc { get; set; }

    public DateTime? DeliveryDate { get; set; }

    public string Currency { get; set; } = "USD";

    public Guid? PriceCardId { get; set; }

    public string? PaymentTerms { get; set; }

    public string? DeliveryTerms { get; set; }

    public string? Notes { get; set; }

    public string? InternalNotes { get; set; }

    public Guid? DeliveryAddressId { get; set; }

    public string? DeliveryAddressSnapshot { get; set; }

    public List<CreateSalesQuotationItemRequest> Items { get; set; } = new();
}

/// <summary>
/// Customer-facing request submitted from public Explore Products or Customer Portal.
/// </summary>
public class CreateCustomerQuotationRequest
{
    public Guid CustomerId { get; set; }

    public Guid? CustomerContactId { get; set; }

    public string? CustomerRemarks { get; set; }

    public DateTime? RequiredDeliveryDate { get; set; }

    public string Currency { get; set; } = "USD";

    public List<CreateSalesQuotationItemRequest> Items { get; set; } = new();
}

public class UpdateSalesQuotationDraftRequest
{
    public DateTime? QuotationDate { get; set; }

    public DateTime? ValidUntilDateUtc { get; set; }

    public DateTime? DeliveryDate { get; set; }

    public string? Currency { get; set; }

    public string? PaymentTerms { get; set; }

    public string? DeliveryTerms { get; set; }

    public string? Notes { get; set; }

    public string? InternalNotes { get; set; }

    public Guid? DeliveryAddressId { get; set; }

    public string? DeliveryAddressSnapshot { get; set; }
}

public class CreateSalesQuotationItemRequest
{
    public Guid ProductId { get; set; }

    public Guid? UnitOfMeasureId { get; set; }

    public decimal Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal DiscountRate { get; set; } = 0;

    public Guid? TaxCodeId { get; set; }

    public decimal TaxRate { get; set; } = 0;

    public bool IsTaxInclusive { get; set; } = false;

    public string? Notes { get; set; }
}

public class UpdateSalesQuotationItemRequest
{
    public decimal Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal DiscountRate { get; set; } = 0;

    public Guid? TaxCodeId { get; set; }

    public decimal TaxRate { get; set; } = 0;

    public Guid? UnitOfMeasureId { get; set; }

    public bool IsTaxInclusive { get; set; } = false;

    public string? Notes { get; set; }
}

public class SalesQuotationDecisionRequest
{
    public string? Remarks { get; set; }
}
