# ApexERP Retail Test Pack — Transaction Register (Gate 6 Full Lifecycle)

## 1. Register Index

This register catalogues all canonical transactions executed during the Retail verification run across the 14-step workflow sequence.

| Ref # | Workflow Step | Document Type | Document Number | Initiator (Username) | Assigned Role | Status | Financial Value | Stock Impact |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **TX-01** | Step 01 | Purchase Requisition | `PR-RET-2026-001` | `retail.po` / `retail.approver` | `Procurement Officer` / `Procurement Manager` | Approved | 237,200.00 LKR (Est.) | None (Requisition Demand) |
| **TX-02** | Step 02 | Purchase Order | `PO-RET-2026-001` | `retail.po` / `retail.approver` | `Procurement Officer` / `Procurement Manager` | Approved | 249,440.00 LKR | None (Commitment) |
| **TX-03** | Steps 03 & 04 | Goods Receipt Note | `GRN-RET-001` | `retail.grn` | `Procurement Officer` | Confirmed | 237,200.00 LKR | +240.0000 units into `LOC-REC-01` (5 Batches) |
| **TX-04** | Step 05 | Putaway Transaction | `PUT-RET-001` | `retail.store` | `Inventory Manager` | Completed | N/A (Internal Transfer) | -240 from `LOC-REC-01`, +240 into `LOC-STK-01` |
| **TX-05** | Step 06 | Stock Receipt | `SR-RET-001` | `retail.store` | `Inventory Manager` | Posted | 26,700.00 LKR | +30.0000 units into `LOC-STK-01` |
| **TX-06** | Step 07 | Stock Issue | `SI-RET-001` | `retail.store` | `Inventory Manager` | Posted | 26,700.00 LKR | -30.0000 units from `LOC-STK-01` (Net Zero Offset) |
| **TX-07** | Step 08 | Inventory Transfer Out | `TR-RET-001` | `retail.store` | `Inventory Manager` | Posted | 41,700.00 LKR | -40.0000 from `LOC-STK-01`, +40.0000 into `LOC-WH2-01` |
| **TX-08** | Step 09 | Stock Count & Adj | `SC-RET-001` | `retail.store` | `Inventory Manager` | Posted | -100.00 LKR (Net) | Shortage (-2 `RET-002`), Overage (+5 `RET-003`) in `LOC-STK-01` |
| **TX-09** | Step 10 | Sales Order 1 | `SO-RET-1-XXXX` | `retail.sales` | `Sales Manager` | Confirmed | 193,950.00 LKR | None (Commitment) |
| **TX-10** | Step 11 | Sales Shipment 1 | `SH-RET-1-XXXX` | `retail.sales` | `Sales Manager` | Dispatched | 151,700.00 LKR (Cost) | -170.0000 units from `LOC-STK-01` (Primary Batches) |
| **TX-11** | Step 12 | Sales Invoice 1 | `INV-RET-1-XXXX` | `retail.sales` | `Sales Manager` | Confirmed | 193,950.00 LKR (Gross) | None (Billing from Shipment 1) |
| **TX-12** | Step 10 | Sales Order 2 | `SO-RET-2-XXXX` | `retail.sales` | `Sales Manager` | Confirmed | 102,670.00 LKR | None (Commitment) |
| **TX-13** | Step 11 | Sales Shipment 2 | `SH-RET-2-XXXX` | `retail.sales` | `Sales Manager` | Dispatched | 85,500.00 LKR (Cost) | -70.0000 units from `LOC-STK-01` (Secondary Batches) |
| **TX-14** | Step 12 | Sales Invoice 2 | `INV-RET-2-XXXX` | `retail.sales` | `Sales Manager` | Confirmed | 102,670.00 LKR (Gross) | None (Billing from Shipment 2) |
| **TX-15** | Step 13 | Inventory Transfer Return | `TR-RET-002` | `retail.store` | `Inventory Manager` | Posted | 41,700.00 LKR | -40.0000 from `LOC-WH2-01`, +40.0000 into `LOC-STK-01` (Reversal) |

---

## 2. Permanent Inventory Transaction Architecture

Each transactional stock posting produces permanent entries in `InventoryTransactions` tagged with:
- `TenantId`: `10000000-0000-0000-0000-000000000001`
- `MovementSequence`: Sequential integer preserving FIFO evaluation order.
- `TransactionDate`: UTC timestamp of posting.
- `SourceDocumentType`: `PurchaseOrder`, `GoodsReceipt`, `Putaway`, `StockTransfer`, `StockAdjustment`, `SalesOrder`, `SalesShipment`, or `ManualPosting`.
- `SourceDocumentId`: Foreign key linking to the source document header.
- `SourceDocumentLineId`: Foreign key linking to the line item.
- `BatchNumber`, `ExpiryDate`: Explicit batch tracking tags.
- `ReversalOfTransactionId`: Linked foreign key when a transaction reverses a previous movement.
