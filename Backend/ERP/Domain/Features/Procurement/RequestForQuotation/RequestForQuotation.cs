using System;
using System.Collections.Generic;
using ERP.Domain.Common;
using ERP.Domain.Common.Scope;

namespace Domain.Features.Procurement.RequestForQuotation;

public enum RequestForQuotationStatus
{
    Draft = 1,
    Issued = 2,
    Closed = 3,
    Awarded = 4,
    Cancelled = 5
}

public class RequestForQuotation : Auditable, ITenantScopedEntity, ICompanyScopedEntity, IBranchScopedEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid BranchId { get; set; }

    public string RFQNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public RequestForQuotationStatus Status { get; set; } = RequestForQuotationStatus.Draft;

    public DateTime ClosingDate { get; set; }
    public DateTime? DeliveryRequiredDate { get; set; }
    public string? DeliveryTerms { get; set; }
    public string? Instructions { get; set; }

    public ICollection<RequestForQuotationLine> Lines { get; set; } = new List<RequestForQuotationLine>();
    public ICollection<RFQSupplier> InvitedSuppliers { get; set; } = new List<RFQSupplier>();
    public ICollection<SupplierQuotation> Quotations { get; set; } = new List<SupplierQuotation>();
}
