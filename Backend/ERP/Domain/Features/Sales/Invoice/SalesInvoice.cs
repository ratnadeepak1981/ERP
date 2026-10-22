using System;
using System.Collections.Generic;
using System.Linq;
using ERP.Domain.Common;
using ERP.Domain.Common.Scope;

namespace Domain.Features.Sales.Invoice;

public class SalesInvoice : Auditable, ITenantScopedEntity, ICompanyScopedEntity, IBranchScopedEntity
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid CompanyId { get; set; }

    public Guid BranchId { get; set; }

    public Guid SalesOrderId { get; set; }

    public Guid? SalesShipmentId { get; set; }

    public Guid CustomerId { get; set; }

    public string InvoiceNumber { get; set; } = string.Empty;

    public DateTime InvoiceDate { get; set; } = DateTime.UtcNow;

    public DateTime? DueDate { get; set; }

    public SalesInvoiceStatus Status { get; set; } = SalesInvoiceStatus.Draft;

    public string Currency { get; set; } = "USD";

    public string? PaymentTerms { get; set; }

    public decimal Subtotal { get; set; }

    public decimal DiscountAmount { get; set; }

    public decimal TaxAmount { get; set; }

    public decimal TotalAmount { get; set; }

    public string? Notes { get; set; }

    public DateTime? ConfirmedAt { get; set; }

    public Guid? ConfirmedBy { get; set; }

    public bool IsActive { get; set; } = true;

    public List<SalesInvoiceItem> Items { get; set; } = new();

    public static SalesInvoice Create(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid salesOrderId,
        Guid customerId,
        string invoiceNumber,
        Guid? salesShipmentId = null,
        DateTime? invoiceDate = null,
        DateTime? dueDate = null,
        string currency = "USD",
        string? paymentTerms = null,
        string? notes = null)
    {
        if (string.IsNullOrWhiteSpace(invoiceNumber))
            throw new ArgumentException("Invoice number is required.", nameof(invoiceNumber));

        return new SalesInvoice
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            BranchId = branchId,
            SalesOrderId = salesOrderId,
            SalesShipmentId = salesShipmentId,
            CustomerId = customerId,
            InvoiceNumber = invoiceNumber.Trim(),
            Status = SalesInvoiceStatus.Draft,
            InvoiceDate = invoiceDate ?? DateTime.UtcNow,
            DueDate = dueDate,
            Currency = string.IsNullOrWhiteSpace(currency) ? "USD" : currency.Trim().ToUpperInvariant(),
            PaymentTerms = paymentTerms?.Trim(),
            Notes = notes?.Trim(),
            IsActive = true
        };
    }

    public SalesInvoiceItem AddItem(SalesInvoiceItem item)
    {
        if (item == null) throw new ArgumentNullException(nameof(item));
        if (Status != SalesInvoiceStatus.Draft)
            throw new InvalidOperationException($"Cannot add items to invoice in status '{Status}'. Only Draft invoices can be modified.");

        Items.Add(item);
        RecalculateTotals();
        return item;
    }

    public void RecalculateTotals()
    {
        var activeItems = Items.Where(i => i.IsActive).ToList();
        foreach (var it in activeItems)
        {
            it.RecalculateAmounts();
        }

        Subtotal = Math.Round(activeItems.Sum(i => i.Subtotal), 4, MidpointRounding.AwayFromZero);
        DiscountAmount = Math.Round(activeItems.Sum(i => i.DiscountAmount), 4, MidpointRounding.AwayFromZero);
        TaxAmount = Math.Round(activeItems.Sum(i => i.TaxAmount), 4, MidpointRounding.AwayFromZero);
        TotalAmount = Math.Round(activeItems.Sum(i => i.LineTotal), 4, MidpointRounding.AwayFromZero);
    }

    public void MarkConfirmed(Guid confirmedBy)
    {
        if (Status != SalesInvoiceStatus.Draft)
            throw new InvalidOperationException($"Cannot confirm invoice in status '{Status}'. Only Draft invoices can be confirmed.");

        var activeItems = Items.Where(i => i.IsActive).ToList();
        if (activeItems.Count == 0)
            throw new InvalidOperationException("Cannot confirm an invoice with no line items.");

        RecalculateTotals();
        Status = SalesInvoiceStatus.Confirmed;
        ConfirmedAt = DateTime.UtcNow;
        ConfirmedBy = confirmedBy;
    }

    public void MarkCancelled()
    {
        if (Status == SalesInvoiceStatus.Cancelled) return;
        if (Status == SalesInvoiceStatus.Paid)
            throw new InvalidOperationException("Cannot cancel an invoice that has been marked Paid.");

        Status = SalesInvoiceStatus.Cancelled;
    }
}
