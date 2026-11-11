# ApexERP Retail Test Pack — Transaction Register

## 1. Register Index

This register catalogues all canonical transactions executed during the Retail verification run.

| Ref # | Workflow Type | Document Number | Initiator | Status | Financial Value | Stock Impact |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **TX-01** | Purchase Order | `PO-RET-2026-001` | `retail.po` | Approved | 249,440.00 LKR | None (Commitment) |
| **TX-02** | Goods Receipt Note | `GRN-RET-001` | `retail.grn` | Confirmed | 237,200.00 LKR | +240 units into `LOC-REC-01` |
| **TX-03** | Putaway Transaction | `PUT-RET-001` | `retail.store` | Confirmed | N/A (Internal) | -240 from `LOC-REC-01`, +240 into `LOC-STK-01` |
| **TX-04** | Stock Receipt | `SR-RET-001` | `retail.store` | Posted | 34,200.00 LKR | +35 units into `LOC-STK-01` |
| **TX-05** | Stock Issue | `SI-RET-001` | `retail.store` | Posted | 19,250.00 LKR | -15 units from `LOC-STK-01` |
| **TX-06** | Inventory Transfer | `TR-RET-001` | `retail.store` | Posted | 41,700.00 LKR | -40 from `LOC-STK-01`, +40 into `LOC-WH2-01` |
| **TX-07** | Sales Order 1 | `SO-RET-1-XXXX` | `retail.sales` | Confirmed | 56,750.00 LKR | None (Commitment) |
| **TX-08** | Sales Shipment 1 | `SH-RET-1-XXXX` | `retail.sales` | Dispatched | 56,750.00 LKR | -45 units from `LOC-STK-01` |
| **TX-09** | Sales Order 2 | `SO-RET-2-XXXX` | `retail.sales` | Confirmed | 33,000.00 LKR | None (Commitment) |
| **TX-10** | Sales Shipment 2 | `SH-RET-2-XXXX` | `retail.sales` | Dispatched | 33,000.00 LKR | -30 units from `LOC-STK-01` |
| **TX-11** | Stock Count & Adj | `SC-RET-001` | `retail.store` | Posted | +1,600.00 LKR (Net) | -2 `RET-002`, +5 `RET-003` in `LOC-STK-01` |

---

## 2. Inventory Transaction Identifiers

Each transactional stock posting produces permanent entries in `InventoryTransactions` tagged with:
- `TenantId`: `10000000-0000-0000-0000-000000000001`
- `MovementSequence`: Sequential integer preserving FIFO evaluation order.
- `TransactionDate`: UTC timestamp of posting.
- `SourceDocumentType`: `PurchaseOrder`, `GoodsReceipt`, `Putaway`, `StockTransfer`, `StockAdjustment`, `SalesOrder`, or `ManualPosting`.
- `SourceDocumentId`: Foreign key linking to the source document header.
- `SourceDocumentLineId`: Foreign key linking to the line item.
