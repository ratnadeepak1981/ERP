# ApexERP Retail Test Pack — GRN and Putaway Test Cases (Gate 6)

## 1. Product Master Tracking Configuration

In ApexERP, batch and serial capture during Goods Receipt is strictly driven by the authoritative **Product Master** configuration:

| Product Code | Description | Base UOM | IsBatchTracked | IsSerialTracked | Tracking Mode | Tracking Requirement at Receipt |
| :--- | :--- | :---: | :---: | :---: | :--- | :--- |
| `RET-001` | Samba Rice 5kg Pack | BAG | `true` | `false` | Batch | Mandatory Batch Number, Mfg Date, Expiry Date |
| `RET-002` | Cooking Oil 1L | BTL | `true` | `false` | Batch | Mandatory Batch Number, Mfg Date, Expiry Date |
| `RET-003` | White Sugar 1kg | PKT | `true` | `false` | Batch | Mandatory Batch Number, Mfg Date, Expiry Date |

---

## 2. Step 3: Goods Receipt Note (GRN) Generation from PO

### TC-RET-GRN-01: GRN Draft Creation from Approved PO
- **Actor:** `retail.grn` (Receiving Clerk / Procurement Officer)
- **Role Code:** `PROCUREMENT_OFFICER`
- **Tenant Scope:** `retail` (`10000000-0000-0000-0000-000000000001`)
- **Company Scope:** `Lanka Retail Mart` (`30C46526-3161-455C-B362-227E8BD0A34A`)
- **Branch Scope:** `Main Store` (`5CFABB17-CBE0-43C5-99AB-72769D8DECF1`)
- **Permissions Verified:** `GRN.VIEW`, `GRN.CREATE`
- **Allowed Actions:** View PO, Create GRN Draft from PO, Save Draft
- **Restricted Actions:** Confirm without valid batch allocation (Denied with validation error)
- **Source Document:** Approved Purchase Order (`PO-RET-2026-001`)
- **Receiving Warehouse:** `RETAIL-WH1` (`Central Retail Warehouse`)
- **Receiving Location:** Receiving Dock 01 (`LOC-REC-01`)
- **Endpoint:** `POST /api/goodsreceiptnotes/from-po/{poId}`
- **Expected Outcome:** **HTTP 201 Created**. GRN created in `Draft` status with reference `GRN-RET-001`. Line items pre-populated from PO. Staging destination initialized to `LOC-REC-01`.
- **Next Step:** Batch allocation and confirmation (Step 4 / TC-RET-GRN-02).

---

## 3. Step 4: Batch Allocation & GRN Confirmation

### TC-RET-GRN-02: Multi-Batch Split Allocation (5 Batches, 240 Units) & Confirmation
- **Actor:** `retail.grn` (Receiving Clerk / Procurement Officer)
- **Role Code:** `PROCUREMENT_OFFICER`
- **Permissions Verified:** `GRN.EDIT`, `GRN.CONFIRM`, `BATCH.VIEW`, `BATCH.CREATE`
- **Endpoint:** `POST /api/goodsreceiptnotes/{grnId}/confirm`

#### Batch Allocation & Inbound Split Detail
To thoroughly verify multi-batch splitting per product line, the 240 ordered units are allocated across **5 distinct batches**:

| PO Line | Product | Total Qty | Batch Number | Split Qty | Mfg Date | Expiry Date | Unit Cost (LKR) | Staging Destination |
| :---: | :--- | :---: | :--- | :---: | :---: | :---: | :---: | :--- |
| 1 | `RET-001` (Rice) | 100 | `RICE-2026-001` | 60 | 2026-01-15 | 2027-06-30 | 1,500.00 | `LOC-REC-01` |
| 1 | `RET-001` (Rice) | 100 | `RICE-2026-002` | 40 | 2026-02-01 | 2027-08-31 | 1,500.00 | `LOC-REC-01` |
| 2 | `RET-002` (Oil) | 80 | `OIL-2026-001` | 50 | 2026-03-01 | 2027-09-30 | 850.00 | `LOC-REC-01` |
| 2 | `RET-002` (Oil) | 80 | `OIL-2026-002` | 30 | 2026-03-15 | 2027-10-31 | 850.00 | `LOC-REC-01` |
| 3 | `RET-003` (Sugar) | 60 | `SUGAR-2026-001` | 60 | 2026-01-10 | 2027-07-31 | 320.00 | `LOC-REC-01` |

#### Post-GRN Expected Outcomes
- **GRN Status:** `Confirmed`
- **PO Status:** Fully Received (`Received`)
- **Inventory Posting:** 5 permanent `InventoryTransaction` records created (Source: `GoodsReceipt`).
- **Stock Location:** All 240 units reside in `LOC-REC-01` (`QuantityOnHand = 240.0000`).
- **Valuation:** `237,200.00 LKR` added to inventory asset valuation layers.
- **Main Rack Balance (`LOC-STK-01`):** `0.0000` units (stock remains safely quarantined in dock until putaway).
- **Next Responsible User:** `retail.store` (Inventory Manager).

---

## 4. Step 5: Warehouse Putaway to Main Storage Racks

### TC-RET-PUT-01: Putaway Execution (Dock to Permanent Storage)
- **Actor:** `retail.store` (Inventory Storekeeper / Warehouse Manager)
- **Role Code:** `INVENTORY_MANAGER`
- **Tenant Scope:** `retail` (`10000000-0000-0000-0000-000000000001`)
- **Company Scope:** `Lanka Retail Mart` (`30C46526-3161-455C-B362-227E8BD0A34A`)
- **Branch Scope:** `Main Store` (`5CFABB17-CBE0-43C5-99AB-72769D8DECF1`)
- **Permissions Verified:** `PUTAWAY.POST`, `INVENTORY.VIEW`, `WAREHOUSE.VIEW`, `LOCATION.VIEW`, `BATCH.VIEW`
- **Allowed Actions:** View GRN, Create Putaway Draft, Confirm / Post Putaway
- **Source Document:** Confirmed GRN `GRN-RET-001` (`PutawaySourceDocumentType.GoodsReceipt` / `2`)
- **Source Location:** Receiving Dock (`LOC-REC-01`)
- **Target Location:** Main Storage Rack (`LOC-STK-01`)
- **Endpoints:**
  1. `POST /api/inventory/putaway` (Create Putaway Draft)
  2. `POST /api/inventory/putaway/{putawayId}/confirm` (Execute Putaway)

#### Putaway Movements by Batch
Every received line item is transferred from dock quarantine into permanent racking:

| Line | Product | Batch Number | Putaway Qty | From Location (Dock) | To Location (Storage Rack) | Unit Cost (LKR) | Line Valuation (LKR) |
| :---: | :--- | :--- | :---: | :--- | :--- | :---: | :---: |
| 1 | `RET-001` | `RICE-2026-001` | 60 | `LOC-REC-01` | `LOC-STK-01` | 1,500.00 | 90,000.00 |
| 2 | `RET-001` | `RICE-2026-002` | 40 | `LOC-REC-01` | `LOC-STK-01` | 1,500.00 | 60,000.00 |
| 3 | `RET-002` | `OIL-2026-001` | 50 | `LOC-REC-01` | `LOC-STK-01` | 850.00 | 42,500.00 |
| 4 | `RET-002` | `OIL-2026-002` | 30 | `LOC-REC-01` | `LOC-STK-01` | 850.00 | 25,500.00 |
| 5 | `RET-003` | `SUGAR-2026-001` | 60 | `LOC-REC-01` | `LOC-STK-01` | 320.00 | 19,200.00 |

#### Post-Putaway Expected Status
- **Putaway Status:** `Completed` / `Confirmed`
- **`LOC-REC-01` Balance:** Cleanly drops to **`0.0000`** (Dock cleared).
- **`LOC-STK-01` Balance:** Holds all **`240.0000`** units:
  - `RET-001`: `100.0000` (B1: 60, B2: 40)
  - `RET-002`: `80.0000` (B1: 50, B2: 30)
  - `RET-003`: `60.0000` (B1: 60)
- **Total Valuation:** Unchanged at `237,200.00 LKR` (Internal warehouse transfer preserves asset cost).
- **Next Responsible User:** `retail.store` (Inventory Manager).
