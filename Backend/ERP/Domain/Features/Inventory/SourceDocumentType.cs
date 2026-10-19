namespace ERP.Domain.Features.Inventory;

public enum SourceDocumentType
{
    GoodsReceiptNote = 1,
    SupplierReturnNote = 2,
    StockTransferOrder = 3,
    StockAdjustment = 4,
    ProductionOrder = 5,
    SalesShipment = 6,
    ManualPosting = 7
}
