using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ERP.Application.Features.Procurement.PurchaseOrderFeature;

public class PurchaseOrderPdfModel
{
    public Guid OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public DateTime OrderDate { get; set; }
    public DateTime? ExpectedDeliveryDate { get; set; }
    public string Status { get; set; } = "Approved";
    public string Currency { get; set; } = "LKR";
    public string? PaymentTerms { get; set; }
    public string? Remarks { get; set; }

    // Company / Issuer
    public string CompanyName { get; set; } = string.Empty;
    public string? CompanyCode { get; set; }
    public string? BranchName { get; set; }
    public string? CompanyAddress { get; set; }
    public string? CompanyPhone { get; set; }
    public string? CompanyEmail { get; set; }

    // Supplier / Vendor
    public string SupplierName { get; set; } = string.Empty;
    public string? SupplierCode { get; set; }
    public string? SupplierContactPerson { get; set; }
    public string? SupplierEmail { get; set; }
    public string? SupplierPhone { get; set; }
    public string? SupplierAddress { get; set; }

    // Line items
    public List<PurchaseOrderPdfLineItem> Items { get; set; } = new();

    // Financial Totals
    public decimal SubTotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
}

public class PurchaseOrderPdfLineItem
{
    public int LineNumber { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string? UnitOfMeasure { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal LineTotal { get; set; }
    public string? Notes { get; set; }
}

public interface IPurchaseOrderPdfService
{
    /// <summary>
    /// Generates a standard ISO 32000-1 (PDF 1.4) document binary stream for the given Purchase Order model.
    /// Supports multi-page pagination, clean header/vendor blocks, itemized tables, and financial totals.
    /// </summary>
    byte[] GeneratePdf(PurchaseOrderPdfModel model);

    /// <summary>
    /// Asynchronously retrieves the required order, company, branch, supplier, and product data
    /// and generates the PDF bytes.
    /// </summary>
    Task<byte[]> GeneratePdfForOrderAsync(Guid tenantId, Guid companyId, Guid orderId, CancellationToken cancellationToken = default);
}
