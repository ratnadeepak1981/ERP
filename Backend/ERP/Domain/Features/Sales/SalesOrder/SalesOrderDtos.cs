using System;
using System.Collections.Generic;

namespace Domain.Features.Sales.SalesOrder;

public class CreateSalesOrderItemRequest
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

public class CreateSalesOrderRequest
{
    public string OrderNumber { get; set; } = string.Empty;

    public SalesOrderType DocumentType { get; set; } = SalesOrderType.Standard;

    public Guid CustomerId { get; set; }

    public DateTime? OrderDate { get; set; }

    public DateTime? DeliveryDate { get; set; }

    public string? Currency { get; set; }

    public string? PaymentTerms { get; set; }

    public string? Notes { get; set; }

    public Guid? OpportunityId { get; set; }

    public Guid? DeliveryAddressId { get; set; }

    public List<CreateSalesOrderItemRequest> Items { get; set; } = new();
}

public class SalesOrderDecisionRequest
{
    public string? Remarks { get; set; }
}

public class SalesOrderItemDto
{
    public Guid Id { get; set; }

    public Guid SalesOrderId { get; set; }

    public Guid ProductId { get; set; }

    public Guid? UnitOfMeasureId { get; set; }

    public decimal Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal DiscountRate { get; set; }

    public decimal DiscountAmount { get; set; }

    public decimal TaxRate { get; set; }

    public decimal TaxAmount { get; set; }

    public decimal SubTotal { get; set; }

    public Guid? TaxCodeId { get; set; }

    public decimal LineTotal { get; set; }

    public bool IsTaxInclusive { get; set; }

    public string? Notes { get; set; }

    public bool IsActive { get; set; }
}

public class SalesOrderDto
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid CompanyId { get; set; }

    public Guid BranchId { get; set; }

    public Guid CustomerId { get; set; }

    public string OrderNumber { get; set; } = string.Empty;

    public SalesOrderType DocumentType { get; set; }

    public DateTime OrderDate { get; set; }

    public DateTime? DeliveryDate { get; set; }

    public string Currency { get; set; } = "USD";

    public string? PaymentTerms { get; set; }

    public SalesOrderStatus Status { get; set; }

    public decimal SubTotal { get; set; }

    public decimal DiscountAmount { get; set; }

    public decimal TaxAmount { get; set; }

    public decimal TotalAmount { get; set; }

    public string? Notes { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public Guid? OpportunityId { get; set; }

    public Guid? DeliveryAddressId { get; set; }

    public string? DeliveryAddressSnapshot { get; set; }

    public bool IsActive { get; set; }

    public List<SalesOrderItemDto> Items { get; set; } = new();
}
