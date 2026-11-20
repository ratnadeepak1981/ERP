using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Domain.Features.Procurement.Route;

public class LinkedPoDto
{
    public Guid Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public DateTime OrderDate { get; set; }
}

public class LinkedRfqDto
{
    public Guid Id { get; set; }
    public string RFQNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime ClosingDate { get; set; }
    public int LineCount { get; set; }
}

public class ProcurementRouteStatusDto
{
    public Guid RequisitionId { get; set; }
    public string RequisitionNumber { get; set; } = string.Empty;
    public string RequisitionStatus { get; set; } = string.Empty;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ProcurementRouteType SelectedRoute { get; set; } = ProcurementRouteType.None;

    public bool CanCreateDirectPO { get; set; }
    public bool CanCreateRFQ { get; set; }
    public bool CanResetRoute { get; set; }
    public string? BlockReason { get; set; }

    public List<LinkedPoDto> LinkedPurchaseOrders { get; set; } = new();
    public LinkedRfqDto? LinkedRfq { get; set; }

    public decimal TotalRequestedQuantity { get; set; }
    public decimal TotalOrderedQuantity { get; set; }
    public decimal TotalRemainingQuantity { get; set; }
}

public class CreateRfqFromRequisitionRequest
{
    public string? Title { get; set; }
    public DateTime ClosingDate { get; set; }
    public DateTime? DeliveryRequiredDate { get; set; }
    public string? DeliveryTerms { get; set; }
    public string? Instructions { get; set; }
    public List<Guid>? SupplierIds { get; set; }
}

public class ResetProcurementRouteRequest
{
    public string Reason { get; set; } = string.Empty;
}
