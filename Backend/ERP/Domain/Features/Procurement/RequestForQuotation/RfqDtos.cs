using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Domain.Features.Procurement.RequestForQuotation;

public class CreateRfqRequest
{
    public string Title { get; set; } = string.Empty;
    public DateTime ClosingDate { get; set; }
    public DateTime? DeliveryRequiredDate { get; set; }
    public string? DeliveryTerms { get; set; }
    public string? Instructions { get; set; }
    public List<CreateRfqLineRequest>? Lines { get; set; }
    public List<Guid>? SupplierIds { get; set; }
}

public class UpdateRfqRequest
{
    public string Title { get; set; } = string.Empty;
    public DateTime ClosingDate { get; set; }
    public DateTime? DeliveryRequiredDate { get; set; }
    public string? DeliveryTerms { get; set; }
    public string? Instructions { get; set; }
}

public class CreateRfqLineRequest
{
    public Guid ProductId { get; set; }
    public decimal Quantity { get; set; }
    public Guid UnitOfMeasureId { get; set; }
    public decimal? TargetUnitPrice { get; set; }
    public DateTime? RequiredDate { get; set; }
    public string? TechnicalSpecifications { get; set; }
    public string? Notes { get; set; }
}

public class AddRfqSupplierRequest
{
    public Guid SupplierId { get; set; }
}

public class SendRfqInvitationsRequest
{
    public List<Guid>? SupplierIds { get; set; }
    public string? PortalBaseUrl { get; set; }
    public string? TenantSubdomain { get; set; }
    public string? ExternalBaseUrl { get; set; }
    public string? CustomEmailNote { get; set; }
}

public class RfqInvitationEmailContext
{
    public string RecipientEmail { get; set; } = string.Empty;
    public string RecipientName { get; set; } = string.Empty;
    public string RfqNumber { get; set; } = string.Empty;
    public string RfqTitle { get; set; } = string.Empty;
    public string BuyingCompanyName { get; set; } = string.Empty;
    public DateTime ResponseDeadline { get; set; }
    public string? CustomNote { get; set; }

    // Option 1: Standard localhost
    public string StandardLocalhostUrl { get; set; } = string.Empty;

    // Option 2: Localhost tenant subdomain
    public string? TenantLocalhostUrl { get; set; }
    public string? TenantLocalhostUnavailableReason { get; set; }

    // Option 3: External tenant access
    public string? ExternalTenantUrl { get; set; }
    public string? ExternalTenantUnavailableReason { get; set; }
}

public class RecordManualQuotationRequest
{
    public Guid SupplierId { get; set; }
    public string QuotationReference { get; set; } = string.Empty;
    public DateTime QuotationDate { get; set; } = DateTime.UtcNow;
    public DateTime ValidUntil { get; set; }
    public Guid CurrencyId { get; set; }
    public decimal ExchangeRate { get; set; } = 1.0m;
    public string? Notes { get; set; }
    public List<RecordQuotationLineRequest> Lines { get; set; } = new();
}

public class RecordQuotationLineRequest
{
    public Guid? RFQLineId { get; set; }
    public Guid ProductId { get; set; }
    public decimal QuotedQuantity { get; set; }
    public Guid UnitOfMeasureId { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal DiscountPercentage { get; set; }
    public Guid? TaxCodeId { get; set; }
    public decimal TaxRate { get; set; }
    public int? LeadTimeDays { get; set; }
    public string? TechnicalRemarks { get; set; }
}

public class SubmitPublicQuotationRequest
{
    public string QuotationReference { get; set; } = string.Empty;
    public DateTime QuotationDate { get; set; } = DateTime.UtcNow;
    public DateTime ValidUntil { get; set; }

    /// <summary>
    /// Optional. When omitted or empty, the company base currency is used.
    /// </summary>
    public Guid? CurrencyId { get; set; }

    public string? Notes { get; set; }
    public List<SubmitPublicQuotationLineRequest> Lines { get; set; } = new();
}

public class SubmitPublicQuotationLineRequest
{
    public Guid RFQLineId { get; set; }
    public decimal QuotedQuantity { get; set; }
    public Guid UnitOfMeasureId { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal DiscountPercentage { get; set; }
    public Guid? TaxCodeId { get; set; }
    public decimal TaxRate { get; set; }
    public int? LeadTimeDays { get; set; }
    public string? TechnicalRemarks { get; set; }
}

public class ApproveQuotationRequest
{
    public string? Remarks { get; set; }
}

public class RejectQuotationRequest
{
    public string? Remarks { get; set; }
}

public class CancelRfqRequest
{
    public string? Reason { get; set; }
}

public class RfqFilterRequest
{
    public string? SearchTerm { get; set; }
    public RequestForQuotationStatus? Status { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}

// ---------------------- DTOs ----------------------

public class RfqHeaderDto
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid BranchId { get; set; }
    public string RFQNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public RequestForQuotationStatus Status { get; set; }
    public DateTime ClosingDate { get; set; }
    public DateTime? DeliveryRequiredDate { get; set; }
    public string? DeliveryTerms { get; set; }
    public string? Instructions { get; set; }
    public int LineCount { get; set; }
    public int InvitedSupplierCount { get; set; }
    public int ReceivedQuotationCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid CreatedBy { get; set; }
}

public class RfqDetailsDto : RfqHeaderDto
{
    public List<RfqLineDto> Lines { get; set; } = new();
    public List<RfqSupplierDto> InvitedSuppliers { get; set; } = new();
    public List<SupplierQuotationDto> Quotations { get; set; } = new();
}

public class RfqLineDto
{
    public Guid Id { get; set; }
    public int LineNumber { get; set; }
    public Guid ProductId { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public Guid UnitOfMeasureId { get; set; }
    public string UnitOfMeasureCode { get; set; } = string.Empty;
    public decimal? TargetUnitPrice { get; set; }
    public DateTime? RequiredDate { get; set; }
    public string? TechnicalSpecifications { get; set; }
    public string? Notes { get; set; }
}

public class RfqSupplierDto
{
    public Guid Id { get; set; }
    public Guid SupplierId { get; set; }
    public string SupplierCode { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public string ContactEmail { get; set; } = string.Empty;
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public RFQSupplierInvitationStatus InvitationStatus { get; set; }
    public DateTime? InvitedAt { get; set; }
    public bool HasToken { get; set; }
    public DateTime? TokenExpiresAt { get; set; }
    public DateTime? ResponseReceivedAt { get; set; }
}

public class SupplierQuotationDto
{
    public Guid Id { get; set; }
    public Guid RFQId { get; set; }
    public Guid SupplierId { get; set; }
    public string SupplierCode { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public string QuotationReference { get; set; } = string.Empty;
    public DateTime QuotationDate { get; set; }
    public DateTime ValidUntil { get; set; }
    public Guid CurrencyId { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public decimal ExchangeRate { get; set; }
    public decimal SubTotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal BaseTotalAmount { get; set; }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public SupplierQuotationStatus Status { get; set; }
    public string? Notes { get; set; }
    public bool IsManualEntry { get; set; }
    public bool IsExpired => DateTime.UtcNow.Date > ValidUntil.Date;
    public DateTime? ApprovedAt { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public string? ApprovalRemarks { get; set; }
    public DateTime? RejectedAt { get; set; }
    public Guid? RejectedByUserId { get; set; }
    public string? RejectionRemarks { get; set; }
    public List<SupplierQuotationLineDto> Lines { get; set; } = new();
}

public class SupplierQuotationLineDto
{
    public Guid Id { get; set; }
    public Guid? RFQLineId { get; set; }
    public Guid ProductId { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public decimal QuotedQuantity { get; set; }
    public Guid UnitOfMeasureId { get; set; }
    public string UnitOfMeasureCode { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public decimal DiscountPercentage { get; set; }
    public Guid? TaxCodeId { get; set; }
    public decimal TaxRate { get; set; }
    public decimal LineTotal { get; set; }
    public int? LeadTimeDays { get; set; }
    public string? TechnicalRemarks { get; set; }
}

public class PublicRfqDetailsDto
{
    public Guid RFQId { get; set; }
    public string RFQNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public DateTime ClosingDate { get; set; }
    public DateTime? DeliveryRequiredDate { get; set; }
    public string? DeliveryTerms { get; set; }
    public string? Instructions { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public string SupplierCode { get; set; } = string.Empty;
    public bool AlreadySubmitted { get; set; }
    public Guid CurrencyId { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public List<PublicRfqLineDto> Lines { get; set; } = new();
}

public class PublicRfqLineDto
{
    public Guid RFQLineId { get; set; }
    public int LineNumber { get; set; }
    public Guid ProductId { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public Guid UnitOfMeasureId { get; set; }
    public string UnitOfMeasureCode { get; set; } = string.Empty;
    public DateTime? RequiredDate { get; set; }
    public string? TechnicalSpecifications { get; set; }
}

public class SubmitQuotationResultDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public Guid? QuotationId { get; set; }
    public string? QuotationReference { get; set; }
}

public class RfqInvitationResultDto
{
    public int SentCount { get; set; }
    public int FailedCount { get; set; }
    public List<string> Messages { get; set; } = new();
}

public class RfqComparisonMatrixDto
{
    public Guid RFQId { get; set; }
    public string RFQNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string BaseCurrencyCode { get; set; } = string.Empty;
    public List<RfqComparisonSupplierBidDto> Suppliers { get; set; } = new();
    public List<RfqComparisonLineDto> Lines { get; set; } = new();
}

public class RfqComparisonSupplierBidDto
{
    public Guid QuotationId { get; set; }
    public Guid SupplierId { get; set; }
    public string SupplierCode { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public string QuotationReference { get; set; } = string.Empty;
    public DateTime QuotationDate { get; set; }
    public DateTime? SubmissionDate { get; set; }
    public DateTime ValidUntil { get; set; }
    public bool IsExpired => DateTime.UtcNow.Date > ValidUntil.Date;
    public string CurrencyCode { get; set; } = string.Empty;
    public decimal ExchangeRate { get; set; }
    public bool HasValidExchangeRate { get; set; } = true;
    public string? CurrencyConversionError { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal BaseTotalAmount { get; set; }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public SupplierQuotationStatus Status { get; set; }
    public bool IsManualEntry { get; set; }
}

public class RfqComparisonLineDto
{
    public Guid RFQLineId { get; set; }
    public int LineNumber { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public decimal RequestedQuantity { get; set; }
    public string RequestedUomCode { get; set; } = string.Empty;
    public decimal? TargetUnitPrice { get; set; }
    public List<RfqComparisonLineBidDto> Bids { get; set; } = new();
}

public class RfqComparisonLineBidDto
{
    public Guid QuotationId { get; set; }
    public Guid SupplierId { get; set; }
    public decimal QuotedQuantity { get; set; }
    public string QuotedUomCode { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public decimal BaseUnitPrice { get; set; }
    public decimal DiscountPercentage { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal BaseDiscountAmount { get; set; }
    public decimal TaxRate { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal BaseTaxAmount { get; set; }
    public decimal LineTotal { get; set; }
    public decimal BaseLineTotal { get; set; }
    public int? LeadTimeDays { get; set; }
    public string? TechnicalRemarks { get; set; }
}
