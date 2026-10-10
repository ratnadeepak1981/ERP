namespace ERP.Domain.Features.Inventory;

public enum InventoryMovementType
{
    GoodsReceipt = 1,        // Inflow from supplier / GRN (+)
    SupplierReturn = 2,      // Outflow back to supplier (-)
    InternalTransferIn = 3,  // Inflow to destination location (+)
    InternalTransferOut = 4, // Outflow from source location (-)
    StockAdjustmentIn = 5,   // Positive stock adjustment (+)
    StockAdjustmentOut = 6,  // Negative stock adjustment (-)
    ProductionReceipt = 7,   // Inflow of finished good (+)
    ProductionIssue = 8,     // Outflow of raw material (-)
    ShipmentDispatch = 9,    // Outflow for sales shipment (-)
    Reversal = 10            // Exact compensating movement
}
