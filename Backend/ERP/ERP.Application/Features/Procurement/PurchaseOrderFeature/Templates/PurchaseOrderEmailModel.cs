using System;

namespace ERP.Application.Features.Procurement.PurchaseOrderFeature.Templates;

/// <summary>
/// Strongly typed template data model for Purchase Order supplier notification emails.
/// </summary>
public sealed class PurchaseOrderEmailModel
{
    public string CompanyName { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public string OrderNumber { get; set; } = string.Empty;
    public string OrderDate { get; set; } = string.Empty;
    public string? ExpectedDeliveryDate { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string TotalAmount { get; set; } = string.Empty;
    public string? Remarks { get; set; }
    public string? ContactPerson { get; set; }
    public string? CompanyEmail { get; set; }
    public string? CompanyPhone { get; set; }
    public string CurrentYear { get; set; } = DateTime.UtcNow.Year.ToString();
}
