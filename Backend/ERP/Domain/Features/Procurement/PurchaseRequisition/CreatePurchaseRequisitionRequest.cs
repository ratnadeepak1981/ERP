using System;
using System.Collections.Generic;

namespace Domain.Features.Procurement.PurchaseRequisition;

public class CreatePurchaseRequisitionItemRequest
{
    public Guid ProductId { get; set; }

    public Guid? UnitOfMeasureId { get; set; }

    public decimal Quantity { get; set; }

    public decimal? EstimatedUnitPrice { get; set; }

    public string? Remarks { get; set; }
}

public class CreatePurchaseRequisitionRequest
{
    public string RequisitionNumber { get; set; } = string.Empty;

    public DateTime? RequisitionDate { get; set; }

    public DateTime? RequiredDate { get; set; }

    public string? Notes { get; set; }

    public List<CreatePurchaseRequisitionItemRequest> Items { get; set; } = new();
}
