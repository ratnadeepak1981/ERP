using System;
using ERP.Domain.Common;
using ERP.Domain.Common.Scope;

namespace Domain.Features.Procurement.RequestForQuotation;

public class SupplierQuotationLine : Auditable, ITenantScopedEntity, ICompanyScopedEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }

    public Guid SupplierQuotationId { get; set; }
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

    public SupplierQuotation? SupplierQuotation { get; set; }
}
