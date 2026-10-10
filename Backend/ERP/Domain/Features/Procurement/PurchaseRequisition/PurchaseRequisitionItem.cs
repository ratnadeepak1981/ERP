using System;

namespace Domain.Features.Procurement.PurchaseRequisition;

public class PurchaseRequisitionItem
{
    public Guid Id { get; set; }

    public Guid PurchaseRequisitionId { get; set; }

    public Guid ProductId { get; set; }

    public Guid? UnitOfMeasureId { get; set; }

    public decimal Quantity { get; set; }

    public decimal EstimatedUnitPrice { get; set; }

    public decimal EstimatedLineTotal { get; set; }

    public string? Remarks { get; set; }

    public bool IsActive { get; set; } = true;
}
