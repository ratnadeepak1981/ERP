using System;
using System.Collections.Generic;

namespace Domain.Features.Procurement.PurchaseOrder;

public class CreatePurchaseOrderItemRequest
{
    public Guid ProductId { get; set; }

    public Guid? UnitOfMeasureId { get; set; }

    public decimal Quantity { get; set; }

    public decimal? UnitPrice { get; set; }

    public decimal DiscountRate { get; set; } = 0;

    public Guid? TaxCodeId { get; set; }

    public decimal TaxRate { get; set; } = 0;

    public bool IsTaxInclusive { get; set; } = false;

    public string? Notes { get; set; }
}

public class CreatePurchaseOrderRequest
{
    public string OrderNumber { get; set; } = string.Empty;

    public Guid? SupplierId { get; set; }

    public DateTime? OrderDate { get; set; }

    /// <summary>
    /// Optional currency override. When omitted the supplier's default currency is used.
    /// The value must match an existing active Currency.CurrencyCode for the tenant.
    /// </summary>
    public string? Currency { get; set; }

    /// <summary>
    /// Optional purchase Price Card selection. Its currency is authoritative for card prices.
    /// </summary>
    public Guid? PriceCardId { get; set; }

    /// <summary>
    /// Optional payment-term override. When omitted the supplier's default PaymentTermId is used.
    /// Must reference an active PaymentTerm for the tenant.
    /// </summary>
    public Guid? PaymentTermId { get; set; }

    /// <summary>
    /// Optional explicit expected delivery date.
    /// When omitted it is calculated from OrderDate + supplier price-card lead time (if available).
    /// </summary>
    public DateTime? ExpectedDeliveryDate { get; set; }

    public List<CreatePurchaseOrderItemRequest> Items { get; set; } = new();
}

public class RefreshPurchaseOrderCurrencyRequest
{
    public DateTime? OrderDate { get; set; }
    public Guid? TransactionCurrencyId { get; set; }
}
