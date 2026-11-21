using System;
using System.Collections.Generic;

namespace ERP.Application.Features.Sales.CustomerPortal;

public class CustomerOrderSummaryDto
{
    public Guid OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public DateTime OrderDate { get; set; }
    public DateTime? DeliveryDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Currency { get; set; } = "USD";
    public decimal SubTotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string? DeliveryAddressSnapshot { get; set; }
    public int ItemCount { get; set; }
    public string? LatestShipmentStatus { get; set; }
    public string? TrackingNumber { get; set; }
}

public class CustomerOrderDetailDto
{
    public Guid OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public DateTime OrderDate { get; set; }
    public DateTime? DeliveryDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Currency { get; set; } = "USD";
    public decimal SubTotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string? DeliveryAddressSnapshot { get; set; }
    public string? PaymentTerms { get; set; }
    public List<CustomerOrderItemDto> Items { get; set; } = new();
    public List<CustomerShipmentSummaryDto> Shipments { get; set; } = new();
}

public class CustomerOrderItemDto
{
    public Guid ItemId { get; set; }
    public Guid ProductId { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string UomCode { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public decimal DiscountRate { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxRate { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal LineTotal { get; set; }
}

public class CustomerShipmentSummaryDto
{
    public Guid ShipmentId { get; set; }
    public string ShipmentNumber { get; set; } = string.Empty;
    public DateTime ShipmentDate { get; set; }
    public DateTime? DispatchedAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public string? CarrierName { get; set; }
    public string? TrackingNumber { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
}

public class CustomerOrderTrackingDto
{
    public Guid OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string CurrentStage { get; set; } = string.Empty;
    public int CurrentStageIndex { get; set; }
    public string? CarrierName { get; set; }
    public string? TrackingNumber { get; set; }
    public DateTime? EstimatedDeliveryDate { get; set; }
    public List<CustomerTrackingStageDto> Stages { get; set; } = new();
}

public class CustomerTrackingStageDto
{
    public int StageIndex { get; set; }
    public string StageName { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
    public bool IsCurrent { get; set; }
    public DateTime? Timestamp { get; set; }
    public string Description { get; set; } = string.Empty;
}

public class CustomerInvoiceSummaryDto
{
    public Guid InvoiceId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTime InvoiceDate { get; set; }
    public DateTime? DueDate { get; set; }
    public Guid SalesOrderId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Currency { get; set; } = "USD";
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal OutstandingBalance { get; set; }
}

public class CustomerDashboardDto
{
    public int TotalOrdersCount { get; set; }
    public int ActiveOrdersCount { get; set; }
    public int PendingQuotesCount { get; set; }
    public int DispatchedShipmentsCount { get; set; }
    public List<CustomerOrderSummaryDto> RecentOrders { get; set; } = new();
    public List<string> Alerts { get; set; } = new();
}
