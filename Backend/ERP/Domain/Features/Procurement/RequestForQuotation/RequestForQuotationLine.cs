using System;
using ERP.Domain.Common;
using ERP.Domain.Common.Scope;

namespace Domain.Features.Procurement.RequestForQuotation;

public class RequestForQuotationLine : Auditable, ITenantScopedEntity, ICompanyScopedEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }

    public Guid RFQId { get; set; }
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

    public RequestForQuotation? RequestForQuotation { get; set; }
}
