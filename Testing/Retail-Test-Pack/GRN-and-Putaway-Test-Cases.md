# ApexERP Retail Test Pack — GRN and Putaway Test Cases

## 1. Goods Receipt Note (GRN) Generation

### Test Summary
- **Test Identifier:** `TC-RET-GRN-01`
- **Actor:** `retail.grn` (Receiving Clerk / Dock Operator)
- **Role Code:** `InventoryReceivingClerk`
- **Source Document:** Approved Purchase Order (`PO-RET-2026-001`)
- **Receiving Location:** Receiving Dock 01 (`LOC-REC-01`)
- **Endpoints:**
  1. `POST /api/goodsreceiptnotes/from-po/{poId}` (Draft creation)
  2. `POST /api/goodsreceiptnotes/{grnId}/confirm` (Confirmation & Stock Landing)

### Batch Allocation & Inbound Split
To thoroughly test multi-batch splitting per product line, the 240 ordered units are received across 5 distinct batches:

| PO Line | Product | Total Qty | Batch Number | Split Qty | Mfg Date | Expiry Date | Unit Cost (LKR) | Destination |
| :---: | :--- | :---: | :--- | :---: | :---: | :---: | :---: | :--- |
| 1 | `RET-001` (Rice) | 100 | `RICE-2026-001` | 60 | 2026-01-01 | 2026-12-31 | 1,500.00 | `LOC-REC-01` |
| 1 | `RET-001` (Rice) | 100 | `RICE-2026-002` | 40 | 2026-02-01 | 2027-01-31 | 1,500.00 | `LOC-REC-01` |
| 2 | `RET-002` (Oil) | 80 | `OIL-2026-001` | 50 | 2026-01-10 | 2026-10-31 | 850.00 | `LOC-REC-01` |
| 2 | `RET-002` (Oil) | 80 | `OIL-2026-002` | 30 | 2026-02-15 | 2026-11-30 | 850.00 | `LOC-REC-01` |
| 3 | `RET-003` (Sugar) | 60 | `SUGAR-2026-001` | 60 | 2026-01-05 | 2027-06-30 | 320.00 | `LOC-REC-01` |

### Expected Post-GRN Status
- **GRN Status:** `Confirmed`
- **PO Status:** Fully Received (`Received`)
- **Stock Location:** All 240 units reside in `LOC-REC-01` with `QuantityOnHand = 240`.

---

## 2. Warehouse Putaway

### Test Summary
- **Test Identifier:** `TC-RET-PUT-01`
- **Actor:** `retail.store` (Warehouse Storekeeper)
- **Role Code:** `InventoryStorekeeper`
- **Source Document:** Confirmed GRN (`PutawaySourceDocumentType.GoodsReceipt`)
- **Source Location:** Receiving Dock (`LOC-REC-01`)
- **Target Location:** Main Storage Rack (`LOC-STK-01`)
- **Endpoints:**
  1. `POST /api/inventory/putaway` (Create Putaway Draft)
  2. `POST /api/inventory/putaway/{putawayId}/confirm` (Execute Putaway)

### Putaway Movements
Each GRN line item is transferred from the inbound dock into permanent racking:

| Line | Product | Batch Number | Putaway Qty | From Location | To Location |
| :---: | :--- | :--- | :---: | :--- | :--- |
| 1 | `RET-001` | `RICE-2026-001` | 60 | `LOC-REC-01` | `LOC-STK-01` |
| 2 | `RET-001` | `RICE-2026-002` | 40 | `LOC-REC-01` | `LOC-STK-01` |
| 3 | `RET-002` | `OIL-2026-001` | 50 | `LOC-REC-01` | `LOC-STK-01` |
| 4 | `RET-002` | `OIL-2026-002` | 30 | `LOC-REC-01` | `LOC-STK-01` |
| 5 | `RET-003` | `SUGAR-2026-001` | 60 | `LOC-REC-01` | `LOC-STK-01` |

### Expected Post-Putaway Status
- **Putaway Status:** `Confirmed`
- **`LOC-REC-01` Balance:** `0.00` (Fully cleared dock)
- **`LOC-STK-01` Balance:**
  - `RET-001`: `100.00` (B1: 60, B2: 40)
  - `RET-002`: `80.00` (B1: 50, B2: 30)
  - `RET-003`: `60.00` (B1: 60)
