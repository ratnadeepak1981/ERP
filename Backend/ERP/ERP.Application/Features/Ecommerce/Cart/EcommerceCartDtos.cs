using System;
using System.Collections.Generic;
using Domain.Features.Ecommerce;
using Domain.Features.Sales.SalesOrder;

namespace ERP.Application.Features.Ecommerce.Cart;

public class AddToCartRequest
{
    public Guid ProductId { get; set; }

    public decimal Quantity { get; set; }

    public Guid? UnitOfMeasureId { get; set; }

    public Guid? ProductPackagingId { get; set; }

    public string? SessionId { get; set; }
}

public class UpdateCartLineRequest
{
    public decimal Quantity { get; set; }

    public string? SessionId { get; set; }
}

public class CartItemDto
{
    public Guid LineId { get; set; }

    public Guid ProductId { get; set; }

    public string ProductCode { get; set; } = string.Empty;

    public string ProductName { get; set; } = string.Empty;

    public Guid? UnitOfMeasureId { get; set; }

    public string? UnitOfMeasureCode { get; set; }

    public string? UnitOfMeasureName { get; set; }

    public Guid? ProductPackagingId { get; set; }

    public string? PackagingName { get; set; }

    public decimal Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal SubTotal { get; set; }

    public decimal DiscountRate { get; set; }

    public decimal DiscountAmount { get; set; }

    public Guid? TaxCodeId { get; set; }

    public string? TaxCode { get; set; }

    public decimal TaxRate { get; set; }

    public decimal TaxAmount { get; set; }

    public decimal LineTotal { get; set; }

    public bool IsTaxInclusive { get; set; }

    public string Currency { get; set; } = "USD";

    public bool IsAvailable { get; set; } = true;

    public string? ValidationMessage { get; set; }
}

public class CartDto
{
    public Guid CartId { get; set; }

    public Guid TenantId { get; set; }

    public Guid CompanyId { get; set; }

    public string CompanyName { get; set; } = string.Empty;

    public Guid? CustomerId { get; set; }

    public string? CustomerName { get; set; }

    public string? SessionId { get; set; }

    public EcommerceCartStatus Status { get; set; }

    public string Currency { get; set; } = "USD";

    public int TotalItems { get; set; }

    public decimal SubTotal { get; set; }

    public decimal TotalDiscount { get; set; }

    public decimal TotalTax { get; set; }

    public decimal GrandTotal { get; set; }

    public List<CartItemDto> Lines { get; set; } = new();
}

public class CheckoutRequest
{
    public string? SessionId { get; set; }

    public string? IdempotencyKey { get; set; }

    public Guid? CustomerId { get; set; }

    public Guid? DeliveryAddressId { get; set; }

    public string? CustomerNotes { get; set; }

    public DateTime? RequestedDeliveryDate { get; set; }
}

public class CheckoutResultDto
{
    public bool Success { get; set; }

    public Guid SalesOrderId { get; set; }

    public string OrderNumber { get; set; } = string.Empty;

    public SalesOrderStatus OrderStatus { get; set; }

    public Guid CartId { get; set; }

    public decimal TotalAmount { get; set; }

    public string Currency { get; set; } = "USD";

    public string? Message { get; set; }
}
