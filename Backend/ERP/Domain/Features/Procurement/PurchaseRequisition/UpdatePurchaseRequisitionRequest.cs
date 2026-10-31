using System;

namespace Domain.Features.Procurement.PurchaseRequisition;

public class UpdatePurchaseRequisitionRequest
{
    public DateTime? RequisitionDate { get; set; }

    public DateTime? RequiredDate { get; set; }

    public string? Notes { get; set; }
}

public class UpdatePurchaseRequisitionItemRequest
{
    public Guid ProductId { get; set; }

    public Guid? UnitOfMeasureId { get; set; }

    public decimal Quantity { get; set; }

    public decimal? EstimatedUnitPrice { get; set; }

    public string? Remarks { get; set; }
}
