using System;
using System.Collections.Generic;

namespace Domain.Features.Procurement.GoodsReceiptNote;

public class CreateGoodsReceiptNoteLineRequest
{
    public Guid PurchaseOrderItemId { get; set; }

    public Guid ProductId { get; set; }

    public Guid WarehouseId { get; set; }

    public Guid WarehouseLocationId { get; set; }

    public decimal QuantityReceived { get; set; }

    public decimal UnitCost { get; set; }

    public Guid? UnitOfMeasureId { get; set; }

    public string? BatchNumber { get; set; }

    public string? SerialNumber { get; set; }

    public DateTime? ExpiryDate { get; set; }

    public bool IsQuarantine { get; set; }

    public string? Remarks { get; set; }
}

public class CreateGoodsReceiptNoteRequest
{
    public Guid PurchaseOrderId { get; set; }

    public string GrnNumber { get; set; } = string.Empty;

    public DateTime? ReceiptDate { get; set; }

    public string? DeliveryNoteNumber { get; set; }

    public string? Remarks { get; set; }

    public List<CreateGoodsReceiptNoteLineRequest> Lines { get; set; } = new();
}

public class UpdateGoodsReceiptNoteRequest
{
    public DateTime? ReceiptDate { get; set; }

    public string? DeliveryNoteNumber { get; set; }

    public string? Remarks { get; set; }

    public List<CreateGoodsReceiptNoteLineRequest> Lines { get; set; } = new();
}

public class GoodsReceiptNoteLineDto
{
    public Guid GoodsReceiptNoteLineId { get; set; }

    public Guid GoodsReceiptNoteId { get; set; }

    public Guid PurchaseOrderItemId { get; set; }

    public Guid ProductId { get; set; }

    public Guid WarehouseId { get; set; }

    public Guid WarehouseLocationId { get; set; }

    public decimal QuantityReceived { get; set; }

    public decimal UnitCost { get; set; }

    public decimal LineTotal { get; set; }

    public Guid? UnitOfMeasureId { get; set; }

    public string? BatchNumber { get; set; }

    public string? SerialNumber { get; set; }

    public DateTime? ExpiryDate { get; set; }

    public bool IsQuarantine { get; set; }

    public string? Remarks { get; set; }
}

public class GoodsReceiptNoteDto
{
    public Guid GoodsReceiptNoteId { get; set; }

    public Guid TenantId { get; set; }

    public Guid CompanyId { get; set; }

    public Guid BranchId { get; set; }

    public Guid PurchaseOrderId { get; set; }

    public Guid? SupplierId { get; set; }

    public string GrnNumber { get; set; } = string.Empty;

    public DateTime ReceiptDate { get; set; }

    public GoodsReceiptNoteStatus Status { get; set; }

    public string? DeliveryNoteNumber { get; set; }

    public string? Remarks { get; set; }

    public decimal TotalAmount { get; set; }

    public List<GoodsReceiptNoteLineDto> Lines { get; set; } = new();
}
