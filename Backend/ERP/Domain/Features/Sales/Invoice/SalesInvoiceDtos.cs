using System;
using System.Collections.Generic;

namespace Domain.Features.Sales.Invoice;

public class CreateSalesInvoiceRequest
{
    public Guid SalesOrderId { get; set; }

    public Guid? SalesShipmentId { get; set; }

    public string? InvoiceNumber { get; set; }

    public DateTime? InvoiceDate { get; set; }

    public DateTime? DueDate { get; set; }

    public string? Currency { get; set; }

    public string? PaymentTerms { get; set; }

    public string? Notes { get; set; }

    public List<CreateSalesInvoiceItemRequest> Items { get; set; } = new();
}

public class CreateSalesInvoiceItemRequest
{
    public Guid ProductId { get; set; }

    public decimal Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal DiscountRate { get; set; }

    public decimal TaxRate { get; set; }

    public Guid? SalesOrderItemId { get; set; }

    public Guid? SalesShipmentItemId { get; set; }

    public Guid? UnitOfMeasureId { get; set; }

    public string? Notes { get; set; }
}

public class UpdateSalesInvoiceRequest
{
    public DateTime? InvoiceDate { get; set; }

    public DateTime? DueDate { get; set; }

    public string? PaymentTerms { get; set; }

    public string? Notes { get; set; }

    public List<CreateSalesInvoiceItemRequest>? Items { get; set; }
}

public class ConfirmSalesInvoiceRequest
{
    public string? Notes { get; set; }
}

public class SalesInvoiceDto
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid CompanyId { get; set; }

    public Guid BranchId { get; set; }

    public Guid SalesOrderId { get; set; }

    public string OrderNumber { get; set; } = string.Empty;

    public Guid? SalesShipmentId { get; set; }

    public string? ShipmentNumber { get; set; }

    public Guid CustomerId { get; set; }

    public string CustomerName { get; set; } = string.Empty;

    public string InvoiceNumber { get; set; } = string.Empty;

    public DateTime InvoiceDate { get; set; }

    public DateTime? DueDate { get; set; }

    public SalesInvoiceStatus Status { get; set; }

    public string Currency { get; set; } = "USD";

    public string? PaymentTerms { get; set; }

    public decimal Subtotal { get; set; }

    public decimal DiscountAmount { get; set; }

    public decimal TaxAmount { get; set; }

    public decimal TotalAmount { get; set; }

    public string? Notes { get; set; }

    public DateTime? ConfirmedAt { get; set; }

    public Guid? ConfirmedBy { get; set; }

    public List<SalesInvoiceItemDto> Items { get; set; } = new();
}

public class SalesInvoiceItemDto
{
    public Guid Id { get; set; }

    public Guid SalesInvoiceId { get; set; }

    public Guid? SalesOrderItemId { get; set; }

    public Guid? SalesShipmentItemId { get; set; }

    public Guid ProductId { get; set; }

    public string ProductCode { get; set; } = string.Empty;

    public string ProductName { get; set; } = string.Empty;

    public decimal Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal DiscountRate { get; set; }

    public decimal DiscountAmount { get; set; }

    public decimal TaxRate { get; set; }

    public decimal TaxAmount { get; set; }

    public decimal Subtotal { get; set; }

    public decimal LineTotal { get; set; }

    public Guid? UnitOfMeasureId { get; set; }

    public string? Notes { get; set; }
}
