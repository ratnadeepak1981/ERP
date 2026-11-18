using System;
using System.Collections.Generic;
using ERP.Domain.Common;
using ERP.Domain.Common.Scope;

namespace Domain.Features.Procurement.RequestForQuotation;

public enum SupplierQuotationStatus
{
    Draft = 1,
    Submitted = 2,
    UnderReview = 3,
    Approved = 4,
    Rejected = 5
}

public class SupplierQuotation : Auditable, ITenantScopedEntity, ICompanyScopedEntity, IBranchScopedEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid BranchId { get; set; }

    public Guid RFQId { get; set; }
    public Guid SupplierId { get; set; }
    public string SupplierCode { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;

    public string QuotationReference { get; set; } = string.Empty;
    public DateTime QuotationDate { get; set; } = DateTime.UtcNow;
    public DateTime ValidUntil { get; set; }

    public Guid CurrencyId { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public decimal ExchangeRate { get; set; } = 1.0m;

    public decimal SubTotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal BaseTotalAmount { get; set; }

    public SupplierQuotationStatus Status { get; set; } = SupplierQuotationStatus.Submitted;
    public string? Notes { get; set; }

    public bool IsManualEntry { get; set; }

    public DateTime? ApprovedAt { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public string? ApprovalRemarks { get; set; }

    public DateTime? RejectedAt { get; set; }
    public Guid? RejectedByUserId { get; set; }
    public string? RejectionRemarks { get; set; }

    public RequestForQuotation? RequestForQuotation { get; set; }
    public ICollection<SupplierQuotationLine> Lines { get; set; } = new List<SupplierQuotationLine>();
}
