# ApexERP Retail Industry 1 — Comprehensive Manual UI Test Guide (Gate 6)

## 1. Prerequisites and Browser Preparation

1. **Web Client URL:** `http://localhost:4200`
2. **Backend API URL:** `http://localhost:5182`
3. **Tenant & Scope Context:** Log in using the Retail tenant context:
   - **Tenant ID:** `10000000-0000-0000-0000-000000000001` (`RETAIL` / `Lanka Retail Mart`)
   - **Company:** `Lanka Retail Mart` (`30C46526-3161-455C-B362-227E8BD0A34A`)
   - **Branch:** `Main Store` (`5CFABB17-CBE0-43C5-99AB-72769D8DECF1`)
4. **Standard Test Credentials:**
   - Default Password for all test users: **`123`**

### User Accounts and Operational Responsibilities

| User Role | Login Email Alias | Standard Password | Operational Gate 6 Responsibility |
| :--- | :--- | :---: | :--- |
| **Tenant Administrator** | `retail.admin@retail.local` | `123` | Master data setup, user security administration |
| **Procurement Officer** | `retail.po@retail.local` | `123` | Purchase Requisition & Purchase Order creation, submission |
| **Procurement Manager** | `retail.approver@retail.local` | `123` | Independent Purchase Requisition & Purchase Order approval (SoD) |
| **Receiving Clerk** | `retail.grn@retail.local` | `123` | Goods Receipt Note (GRN) creation, batch splits, confirmation |
| **Inventory Storekeeper** | `retail.store@retail.local` | `123` | Putaway, stock receipts, stock issues, transfers, stock counts |
| **Sales Representative** | `retail.sales@retail.local` | `123` | Sales orders, customer confirmation, shipment dispatch |
| **Audit Viewer** | `retail.viewer@retail.local` | `123` | Read-only audit verification and RBAC enforcement |

---

## 2. Step-by-Step Manual Execution Scenarios

### Step 0: Purchase Requisition (PR) Creation, Submission & Independent Approval (Pre-PO Requirement)

> [!IMPORTANT]
> **Mandatory Pre-PO Workflow:** The business process requires creating and independently approving a Purchase Requisition (PR) containing **at least 2 distinct items** before creating the Purchase Order. This verifies the complete end-to-end Procurement cycle (PR -> Approval -> PO from PR -> PO Approval -> GRN).

#### 0A: Create & Submit Purchase Requisition (Requester: Procurement Officer)
- **Login As:** `retail.po@retail.local` (Procurement Officer)
- **Navigate to:** `Procurement -> Purchase Requisitions`
- **Click:** `+ New Purchase Requisition`
- **Header Fields:**
  - **Company:** `Lanka Retail Mart`
  - **Branch:** `Main Store`
  - **Requisition Date:** Today's date
  - **Required Date:** 7 days from today
  - **Department:** `Retail Store Operations`
  - **Priority / Urgency:** `Medium`
  - **Justification:** "Quarterly inventory replenishment for retail staple food and cooking essentials."
- **Requisition Line Items (Add 3 Items — Meets 'At Least 2 Items' Requirement):**
  1. **Item 1:** Product: `RET-001` (Samba Rice 5kg) | Quantity: `100` | UOM: `BAG` | Est. Unit Price: `1,500.00`
  2. **Item 2:** Product: `RET-002` (Cooking Oil 1L) | Quantity: `80` | UOM: `BOTTLE` | Est. Unit Price: `850.00`
  3. **Item 3:** Product: `RET-003` (White Sugar 1kg) | Quantity: `60` | UOM: `BAG` | Est. Unit Price: `320.00`
- **Actions:**
  - Click `Save Draft` -> Verify toast message "Draft saved successfully". PR status displays `Draft`.
  - Click `Submit` -> PR status updates to `Submitted`.
  - **SoD Verification:** Notice the `Approve` button is **disabled or hidden** for `retail.po@retail.local` (requester cannot self-approve).

#### 0B: Independent PR Approval (Approver: Procurement Manager)
- **Logout** and **Login As:** `retail.approver@retail.local` (Procurement Manager)
- **Navigate to:** `Procurement -> Purchase Requisitions`
- **Open:** The submitted Purchase Requisition from Step 0A.
- **Inspect:** Verify all line items (at least 2 items, here 3 items: 100 BAG, 80 BOTTLE, 60 BAG) are present and correct.
- **Actions:**
  - Click `Approve` -> Enter approval remarks: "Approved for Purchase Order conversion".
  - Status updates to `Approved`.
  - Both submitter (`retail.po`) and approver (`retail.approver`) identities and timestamps are recorded in the PR audit trail.

---

### Step 1: Purchase Order Creation from Approved PR (At Least 2 Items Linked)

> [!NOTE]
> When creating the Purchase Order, the Procurement Officer can convert items from the approved PR. In this scenario, all 3 items (or at least 2 items) from the approved PR are ordered into the PO.

- **Logout** and **Login As:** `retail.po@retail.local` (Procurement Officer)
- **Navigate to:** `Procurement -> Purchase Orders`
- **Click:** `+ New Purchase Order`
- **Link Source Requisition:**
  - Select the approved PR from Step 0B in the `Source Requisition` dropdown.
  - Verify badge: `Source Requisition: PR-XXXXXX (Approved)`
  - Verify lines are automatically pre-populated with PR items, quantities, and remaining allowed balance indicators.
- **Header Fields:**
  - **Supplier:** Select `Lanka Retail Distributors (Pvt) Ltd` (`SUP-RET-001`)
  - **Warehouse:** Select `Central Retail Warehouse` (`RETAIL-WH1`)
  - **Order Date:** Today's date
  - **Expected Delivery Date:** 7 days from today
  - **Payment Terms:** `Net 30 Days`
  - **Currency:** `LKR`
- **Line Items Grid Verification (At Least 2 PR Items Ordered):**
  1. **Line 1 (From PR Item 1):** Product: `RET-001` (Samba Rice 5kg) | Ordered Qty: `100` | Max Allowed: `100` | Unit Price: `1,500.00` (auto-resolved from Price Card) | Tax: `TAX-EXEMPT` (0%) | PR Link: Valid
  2. **Line 2 (From PR Item 2):** Product: `RET-002` (Cooking Oil 1L) | Ordered Qty: `80` | Max Allowed: `80` | Unit Price: `850.00` (auto-resolved from Price Card) | Tax: `VAT-18` (18%) | PR Link: Valid
  3. **Line 3 (From PR Item 3):** Product: `RET-003` (White Sugar 1kg) | Ordered Qty: `60` | Max Allowed: `60` | Unit Price: `320.00` (auto-resolved from Price Card) | Tax: `TAX-EXEMPT` (0%) | PR Link: Valid
- **Over-Order Protection Verification:**
  - Temporarily increase Ordered Qty on Line 1 (`RET-001`) to `105`.
  - Verify inline validation alert: "Quantity cannot exceed remaining PR quantity (100)".
  - Revert Qty back to `100`.
- **Header Totals Verification:**
  - SubTotal: `237,200.00 LKR`
  - Tax Amount: `12,240.00 LKR` (18% on Line 2 `RET-002`: `80 * 850 = 68,000 * 18% = 12,240`)
  - Total: `249,440.00 LKR`
- **Actions:**
  - Click `Save Draft` -> Verify toast message "Draft saved successfully".
  - Click `Submit` -> PO status updates to `Submitted`.
  - Notice the `Approve` button is **disabled or hidden** (SoD check).

---

### Step 2: SoD Check & Purchase Order Approval
- **Logout** and **Login As:** `retail.approver@retail.local` (Procurement Manager)
- **Navigate to:** `Procurement -> Purchase Orders`
- **Open:** The submitted Purchase Order from Step 1.
- **Inspect:** Verify line items and totals match `249,440.00 LKR`.
- **Actions:**
  - Click `Approve` -> Enter approval remarks: "Approved FY26 Retail Stock".
  - Status updates to `Approved`.
  - Verify PR status reflects `Ordered` or partial fulfillment tracking accordingly.

---

### Step 3: Goods Receipt Note (GRN) Inbound Split
- **Logout** and **Login As:** `retail.grn@retail.local` (Receiving Clerk)
- **Navigate to:** `Inventory -> Goods Receipt Notes`
- **Click:** `+ New GRN from PO`
- **Select PO:** The approved PO from Step 2.
- **Receiving Location:** `Receiving Dock 01` (`LOC-REC-01`).
- **Enter Batch Splits for Line Items (5 Total Batches, exactly 240 units):**
  - `RET-001` (Samba Rice 5kg) Line:
    - Batch `RICE-2026-001` | Qty: `60` | Mfg: `2026-01-15` | Exp: `2027-06-30`
    - Click `+ Add Batch Split`
    - Batch `RICE-2026-002` | Qty: `40` | Mfg: `2026-02-01` | Exp: `2027-08-31`
    *(Line 1 Subtotal: 100 units)*
  - `RET-002` (Cooking Oil 1L) Line:
    - Batch `OIL-2026-001` | Qty: `50` | Mfg: `2026-03-01` | Exp: `2027-09-30`
    - Click `+ Add Batch Split`
    - Batch `OIL-2026-002` | Qty: `30` | Mfg: `2026-03-15` | Exp: `2027-10-31`
    *(Line 2 Subtotal: 80 units)*
  - `RET-003` (White Sugar 1kg) Line:
    - Batch `SUGAR-2026-001` | Qty: `60` | Mfg: `2026-01-10` | Exp: `2027-07-31`
    *(Line 3 Subtotal: 60 units)*
- **Actions:**
  - Click `Confirm Receipt`.
  - Verify Status: `Confirmed`.

#### Verification: Post-GRN Stock Report & Valuation Audit
- **Navigate to:** `Inventory -> Overview` (or `Inventory -> Current Stock`)
- **Run Standard Stock Report & Ledger Verification:**
  - Verify KPI Card **Total Inventory Value:** `237,200.00 LKR`
  - Verify KPI Card **Total On-Hand Quantity:** `240.0000` units
  - Verify KPI Card **Total Stock Items:** `3` items

| Transaction Source | Movement Type | Product Code & Name | Batch # | Warehouse | Staging / Storage Location | Inbound Qty | Valuation Rate (LKR) | Total Valuation (LKR) | Location Balance |
| :--- | :--- | :--- | :--- | :--- | :--- | :---: | :---: | :---: | :---: |
| `GRN-XXXXXX` | `GoodsReceipt` | `RET-001` Samba Rice 5kg | `RICE-2026-001` | `RETAIL-WH1` | **`LOC-REC-01` (Receiving Dock)** | `+60.0000` | `1,500.00` | `90,000.00` | `60.0000` |
| `GRN-XXXXXX` | `GoodsReceipt` | `RET-001` Samba Rice 5kg | `RICE-2026-002` | `RETAIL-WH1` | **`LOC-REC-01` (Receiving Dock)** | `+40.0000` | `1,500.00` | `60,000.00` | `40.0000` |
| `GRN-XXXXXX` | `GoodsReceipt` | `RET-002` Cooking Oil 1L | `OIL-2026-001` | `RETAIL-WH1` | **`LOC-REC-01` (Receiving Dock)** | `+50.0000` | `850.00` | `42,500.00` | `50.0000` |
| `GRN-XXXXXX` | `GoodsReceipt` | `RET-002` Cooking Oil 1L | `OIL-2026-002` | `RETAIL-WH1` | **`LOC-REC-01` (Receiving Dock)** | `+30.0000` | `850.00` | `25,500.00` | `30.0000` |
| `GRN-XXXXXX` | `GoodsReceipt` | `RET-003` White Sugar 1kg | `SUGAR-2026-001` | `RETAIL-WH1` | **`LOC-REC-01` (Receiving Dock)** | `+60.0000` | `320.00` | `19,200.00` | `60.0000` |
| **Total Inbound** | | | | | **Dock / Staging Total** | **`+240.0000`** | | **`237,200.00`** | **`240.0000`** |

> [!NOTE]
> All 240 units are currently quarantined/staged at `LOC-REC-01`. Main storage rack `LOC-STK-01` currently has `0.0000` units.

---

### Step 4: Warehouse Putaway to Main Racks
- **Logout** and **Login As:** `retail.store@retail.local` (Inventory Storekeeper)
- **Navigate to:** `Inventory -> Putaway`
- **Click:** `+ New Putaway`
- **Select Source:** `Goods Receipt Note` -> Pick confirmed GRN from Step 3.
- **Target Location:** Set all lines to `Main Rack A-01` (`LOC-STK-01`).
- **Confirm Quantities & Batches:**
  - `RET-001`: Batch `RICE-2026-001` (60), Batch `RICE-2026-002` (40)
  - `RET-002`: Batch `OIL-2026-001` (50), Batch `OIL-2026-002` (30)
  - `RET-003`: Batch `SUGAR-2026-001` (60)
- **Actions:**
  - Click `Confirm Putaway`.
  - Status updates to `Completed`.

#### Verification: Post-Putaway Stock Report & Location Audit
- **Navigate to:** `Inventory -> Overview` / `Current Stock`
- **Run Standard Stock Report & Ledger Verification:**
  - Verify **Staging Dock Cleared:** `LOC-REC-01` shows `0.0000` on hand for all items and batches.
  - Verify **Storage Rack Populated:** `LOC-STK-01` shows all `240.0000` units safely stored.
  - Verify KPI Card **Total Inventory Value:** Unchanged at `237,200.00 LKR` (internal transfer preserves valuation).

| Transaction Source | Movement Type | Product Code & Name | Batch # | From Location (Staging) | To Location (Storage) | Transferred Qty | Valuation Rate (LKR) | Line Valuation (LKR) | Storage Rack Balance |
| :--- | :--- | :--- | :--- | :--- | :--- | :---: | :---: | :---: | :---: |
| `PUT-XXXXXX` | `StoragePutaway` | `RET-001` Samba Rice 5kg | `RICE-2026-001` | `LOC-REC-01` (`0.0000`) | **`LOC-STK-01`** | `60.0000` | `1,500.00` | `90,000.00` | `60.0000` |
| `PUT-XXXXXX` | `StoragePutaway` | `RET-001` Samba Rice 5kg | `RICE-2026-002` | `LOC-REC-01` (`0.0000`) | **`LOC-STK-01`** | `40.0000` | `1,500.00` | `60,000.00` | `40.0000` |
| `PUT-XXXXXX` | `StoragePutaway` | `RET-002` Cooking Oil 1L | `OIL-2026-001` | `LOC-REC-01` (`0.0000`) | **`LOC-STK-01`** | `50.0000` | `850.00` | `42,500.00` | `50.0000` |
| `PUT-XXXXXX` | `StoragePutaway` | `RET-002` Cooking Oil 1L | `OIL-2026-002` | `LOC-REC-01` (`0.0000`) | **`LOC-STK-01`** | `30.0000` | `850.00` | `25,500.00` | `30.0000` |
| `PUT-XXXXXX` | `StoragePutaway` | `RET-003` White Sugar 1kg | `SUGAR-2026-001` | `LOC-REC-01` (`0.0000`) | **`LOC-STK-01`** | `60.0000` | `320.00` | `19,200.00` | `60.0000` |
| **Putaway Total** | | | | **Dock: `0.0000`** | **Rack: `240.0000`** | **`240.0000`** | | **`237,200.00`** | **`240.0000`** |

---

### Step 5: Direct Stock Receipt & Direct Stock Issue (Net-Zero Store Movements)
- **Continue As:** `retail.store@retail.local` (Inventory Storekeeper)
- **5A: Direct Stock Receipt:**
  - Navigate to `Inventory -> Stock Receipts` -> `+ New Stock Receipt`.
  - Warehouse: `RETAIL-WH1` | Location: `LOC-STK-01` | Reason: `Sample/Promotion Intake`.
  - Line 1: `RET-001` | Batch: `RICE-2026-001` | Qty: `10` | Cost: `1,500.00`
  - Line 2: `RET-002` | Batch: `OIL-2026-001` | Qty: `10` | Cost: `850.00`
  - Line 3: `RET-003` | Batch: `SUGAR-2026-001` | Qty: `10` | Cost: `320.00`
  - Click `Post Receipt` -> Verify toast "Stock receipt posted successfully".

#### Verification 5A: Post-Stock Receipt Report & Valuation Audit
- **Navigate to:** `Inventory -> Overview` (or `Inventory -> Current Stock`)
- **Run Standard Stock Report & Ledger Verification:**
  - Verify KPI Card **Total Inventory Value:** Increased to `263,900.00 LKR` (+26,700.00 LKR)
  - Verify KPI Card **Total On-Hand Quantity:** Increased to `270.0000` units (+30 units)

| Transaction Source | Movement Type | Product Code & Name | Batch # | Warehouse | Location Affected | Added Qty | Valuation Rate (LKR) | Added Valuation (LKR) | Storage Rack Balance |
| :--- | :--- | :--- | :--- | :--- | :--- | :---: | :---: | :---: | :---: |
| `SR-XXXXXX` | `StockReceipt` | `RET-001` Samba Rice 5kg | `RICE-2026-001` | `RETAIL-WH1` | `LOC-STK-01` | `+10.0000` | `1,500.00` | `+15,000.00` | `70.0000` |
| `SR-XXXXXX` | `StockReceipt` | `RET-002` Cooking Oil 1L | `OIL-2026-001` | `RETAIL-WH1` | `LOC-STK-01` | `+10.0000` | `850.00` | `+8,500.00` | `60.0000` |
| `SR-XXXXXX` | `StockReceipt` | `RET-003` White Sugar 1kg | `SUGAR-2026-001` | `RETAIL-WH1` | `LOC-STK-01` | `+10.0000` | `320.00` | `+3,200.00` | `70.0000` |
| **Receipt Total** | | | | | **`LOC-STK-01`** | **`+30.0000`** | | **`+26,700.00`** | **`270.0000`** |

- **5B: Direct Stock Issue (Internal Store Use / QA Testing):**
  - Navigate to `Inventory -> Stock Issues` -> `+ New Stock Issue`.
  - Warehouse: `RETAIL-WH1` | Location: `LOC-STK-01` | Purpose: `Quality Assurance / Tasting Sample`.
  - Line 1: `RET-001` | Batch: `RICE-2026-001` | Qty: `10`
  - Line 2: `RET-002` | Batch: `OIL-2026-001` | Qty: `10`
  - Line 3: `RET-003` | Batch: `SUGAR-2026-001` | Qty: `10`
  - Click `Post Issue` -> Verify toast "Stock issue posted successfully".

#### Verification 5B: Post-Stock Issue Report & Valuation Audit
- **Navigate to:** `Inventory -> Overview` (or `Inventory -> Current Stock`)
- **Run Standard Stock Report & Ledger Verification:**
  - Verify KPI Card **Total Inventory Value:** Restored to `237,200.00 LKR` (-26,700.00 LKR)
  - Verify KPI Card **Total On-Hand Quantity:** Restored to `240.0000` units (-30 units)

| Transaction Source | Movement Type | Product Code & Name | Batch # | Warehouse | Location Affected | Issued Qty | Valuation Rate (LKR) | Deducted Valuation (LKR) | Storage Rack Balance |
| :--- | :--- | :--- | :--- | :--- | :--- | :---: | :---: | :---: | :---: |
| `SI-XXXXXX` | `StockIssue` | `RET-001` Samba Rice 5kg | `RICE-2026-001` | `RETAIL-WH1` | `LOC-STK-01` | `-10.0000` | `1,500.00` | `-15,000.00` | `60.0000` |
| `SI-XXXXXX` | `StockIssue` | `RET-002` Cooking Oil 1L | `OIL-2026-001` | `RETAIL-WH1` | `LOC-STK-01` | `-10.0000` | `850.00` | `-8,500.00` | `50.0000` |
| `SI-XXXXXX` | `StockIssue` | `RET-003` White Sugar 1kg | `SUGAR-2026-001` | `RETAIL-WH1` | `LOC-STK-01` | `-10.0000` | `320.00` | `-3,200.00` | `60.0000` |
| **Issue Total** | | | | | **`LOC-STK-01`** | **`-30.0000`** | | **`-26,700.00`** | **`240.0000`** |

> [!NOTE]
> The direct receipt (+30 units, +26,700 LKR) and direct issue (-30 units, -26,700 LKR) net out to zero. Storage rack `LOC-STK-01` returns exactly to the 240 units from the GRN.

---

### Step 6: Inter-Warehouse Transfer & Reversal/Return (WH1 to WH2 and Return)
- **Continue As:** `retail.store@retail.local` (Inventory Storekeeper)
- **6A: Transfer Out (WH1 -> WH2):**
  - Navigate to `Inventory -> Item Transfers` -> `+ New Transfer`.
  - **From Warehouse:** `Central Retail Warehouse` (`RETAIL-WH1`) | From Location: `LOC-STK-01`
  - **To Warehouse:** `Annex Retail Warehouse` (`RETAIL-WH2`) | To Location: `LOC-WH2-01`
  - Transfer Lines:
    - Line 1: `RET-001` | Batch: `RICE-2026-001` | Qty: `20`
    - Line 2: `RET-002` | Batch: `OIL-2026-001` | Qty: `10`
    - Line 3: `RET-003` | Batch: `SUGAR-2026-001` | Qty: `10`
  - Click `Save Draft` -> Then `Post Transfer`.

#### Verification 6A: Post-Transfer Out Stock Report & Valuation Audit
- **Navigate to:** `Inventory -> Overview` / `Current Stock`
- **Run Standard Stock Report & Ledger Verification:**
  - Verify **WH1 Stock:** Drops from 240 to `200.0000` units (Valuation: `195,500.00 LKR`).
  - Verify **WH2 Stock:** Increases from 0 to `40.0000` units (Valuation: `41,700.00 LKR`).
  - Verify **Total Company Valuation:** Unchanged at `237,200.00 LKR`.

| Transaction Source | Movement Type | Product Code & Name | Batch # | Origin (WH1) | Destination (WH2) | Qty Transferred | Valuation Rate (LKR) | Line Valuation (LKR) | WH1 Balance | WH2 Balance |
| :--- | :--- | :--- | :--- | :--- | :--- | :---: | :---: | :---: | :---: | :---: |
| `TR-XXXXXX` | `InternalTransfer` | `RET-001` Samba Rice 5kg | `RICE-2026-001` | `LOC-STK-01` | `LOC-WH2-01` | `20.0000` | `1,500.00` | `30,000.00` | `40.0000` | `20.0000` |
| `TR-XXXXXX` | `InternalTransfer` | `RET-002` Cooking Oil 1L | `OIL-2026-001` | `LOC-STK-01` | `LOC-WH2-01` | `10.0000` | `850.00` | `8,500.00` | `40.0000` | `10.0000` |
| `TR-XXXXXX` | `InternalTransfer` | `RET-003` White Sugar 1kg | `SUGAR-2026-001` | `LOC-STK-01` | `LOC-WH2-01` | `10.0000` | `320.00` | `3,200.00` | `50.0000` | `10.0000` |
| **Transfer Total** | | | | **WH1 Total** | **WH2 Total** | **`40.0000`** | | **`41,700.00`** | **`200.0000`** | **`40.0000`** |

- **6B: Transfer Return / Reversal (WH2 -> WH1):**
  - Navigate to `Inventory -> Item Transfers` -> `+ New Transfer`.
  - **From Warehouse:** `Annex Retail Warehouse` (`RETAIL-WH2`) | From Location: `LOC-WH2-01`
  - **To Warehouse:** `Central Retail Warehouse` (`RETAIL-WH1`) | To Location: `LOC-STK-01`
  - Transfer Lines:
    - Line 1: `RET-001` | Batch: `RICE-2026-001` | Qty: `20`
    - Line 2: `RET-002` | Batch: `OIL-2026-001` | Qty: `10`
    - Line 3: `RET-003` | Batch: `SUGAR-2026-001` | Qty: `10`
  - Click `Save Draft` -> Then `Post Transfer`.

#### Verification 6B: Post-Transfer Return Stock Report & Valuation Audit
- **Navigate to:** `Inventory -> Overview` / `Current Stock`
- **Run Standard Stock Report & Ledger Verification:**
  - Verify **WH2 Stock Cleared:** `LOC-WH2-01` returns cleanly to **`0.0000`** units (`0.00 LKR`).
  - Verify **WH1 Stock Restored:** `LOC-STK-01` returns to **`240.0000`** units (`237,200.00 LKR`).

| Transaction Source | Movement Type | Product Code & Name | Batch # | Origin (WH2) | Destination (WH1) | Qty Returned | Valuation Rate (LKR) | Line Valuation (LKR) | WH2 Balance | WH1 Balance |
| :--- | :--- | :--- | :--- | :--- | :--- | :---: | :---: | :---: | :---: | :---: |
| `TR-YYYYYY` | `InternalTransfer` | `RET-001` Samba Rice 5kg | `RICE-2026-001` | `LOC-WH2-01` | `LOC-STK-01` | `20.0000` | `1,500.00` | `30,000.00` | `0.0000` | `60.0000` |
| `TR-YYYYYY` | `InternalTransfer` | `RET-002` Cooking Oil 1L | `OIL-2026-001` | `LOC-WH2-01` | `LOC-STK-01` | `10.0000` | `850.00` | `8,500.00` | `0.0000` | `50.0000` |
| `TR-YYYYYY` | `InternalTransfer` | `RET-003` White Sugar 1kg | `SUGAR-2026-001` | `LOC-WH2-01` | `LOC-STK-01` | `10.0000` | `320.00` | `3,200.00` | `0.0000` | `60.0000` |
| **Return Total** | | | | **WH2 Total** | **WH1 Total** | **`40.0000`** | | **`41,700.00`** | **`0.0000`** | **`240.0000`** |

---

### Step 7: Sales Orders, Shipment & Dispatch (Exhaust All Stock to Exactly Net Zero)
- **Logout** and **Login As:** `retail.sales@retail.local` (Sales Representative)
- **7A: Sales Order 1 (Primary Batches — 170 Units Total):**
  - Navigate to `Sales -> Sales Orders` -> `+ New Sales Order`.
  - Customer: `Cargills Retail Supermarkets PLC` (`CUST-RET-001`).
  - Warehouse: `Central Retail Warehouse` (`RETAIL-WH1`).
  - Add items (Primary Batches):
    1. `RET-001` (Samba Rice 5kg) | Qty: `60` | Unit Price: `1,800.00`
    2. `RET-002` (Cooking Oil 1L) | Qty: `50` | Unit Price: `1,050.00`
    3. `RET-003` (White Sugar 1kg) | Qty: `60` | Unit Price: `400.00`
  - Click `Submit` -> Then `Confirm Order`.
  - Navigate to `Sales -> Shipments` -> `+ Create Shipment from Order`.
  - Allocate from Primary Batches:
    - `RET-001`: Batch `RICE-2026-001` (Qty: `60`)
    - `RET-002`: Batch `OIL-2026-001` (Qty: `50`)
    - `RET-003`: Batch `SUGAR-2026-001` (Qty: `60`)
  - Click `Dispatch` -> Status updates to `Dispatched`.

#### Verification 7A: Post-SO1 Dispatch Stock Report & Valuation Audit
- **Navigate to:** `Inventory -> Overview` / `Current Stock`
- **Run Standard Stock Report & Ledger Verification:**
  - Verify **Primary Batches Depleted:** `RICE-2026-001`, `OIL-2026-001`, and `SUGAR-2026-001` show `0.0000` on hand.
  - Verify KPI Card **Total Inventory Value:** Reduced to `85,500.00 LKR` (-151,700.00 LKR cost reduction).
  - Verify KPI Card **Total On-Hand Quantity:** Reduced to `70.0000` units (-170 units).

| Transaction Source | Movement Type | Product Code & Name | Batch # | Warehouse | Location Affected | Dispatched Qty | Valuation Rate (LKR) | Cost Reduction (LKR) | Selling Rate (LKR) | Rack Balance |
| :--- | :--- | :--- | :--- | :--- | :--- | :---: | :---: | :---: | :---: | :---: |
| `SH-XXXXXX` | `ShipmentDispatch` | `RET-001` Samba Rice 5kg | `RICE-2026-001` | `RETAIL-WH1` | `LOC-STK-01` | `-60.0000` | `1,500.00` | `-90,000.00` | `1,800.00` | **`0.0000`** |
| `SH-XXXXXX` | `ShipmentDispatch` | `RET-002` Cooking Oil 1L | `OIL-2026-001` | `RETAIL-WH1` | `LOC-STK-01` | `-50.0000` | `850.00` | `-42,500.00` | `1,050.00` | **`0.0000`** |
| `SH-XXXXXX` | `ShipmentDispatch` | `RET-003` White Sugar 1kg | `SUGAR-2026-001` | `RETAIL-WH1` | `LOC-STK-01` | `-60.0000` | `320.00` | `-19,200.00` | `400.00` | **`0.0000`** |
| **SO1 Total** | | | | | **`LOC-STK-01`** | **`-170.0000`** | | **`-151,700.00`** | | **`70.0000`** |

- **7B: Sales Order 2 (Secondary Batches — 70 Units Total):**
  - Navigate to `Sales -> Sales Orders` -> `+ New Sales Order`.
  - Customer: `Keells Supermarkets Holdings` (`CUST-RET-002`).
  - Warehouse: `Central Retail Warehouse` (`RETAIL-WH1`).
  - Add items (Secondary Batches):
    1. `RET-001` (Samba Rice 5kg) | Qty: `40` | Unit Price: `1,800.00`
    2. `RET-002` (Cooking Oil 1L) | Qty: `30` | Unit Price: `1,050.00`
  - Click `Submit` -> Then `Confirm Order`.
  - Navigate to `Sales -> Shipments` -> `+ Create Shipment from Order`.
  - Allocate from Secondary Batches:
    - `RET-001`: Batch `RICE-2026-002` (Qty: `40`)
    - `RET-002`: Batch `OIL-2026-002` (Qty: `30`)
  - Click `Dispatch` -> Status updates to `Dispatched`.

#### Verification 7B: Post-SO2 Dispatch Stock Report & Valuation Audit
- **Navigate to:** `Inventory -> Overview` / `Current Stock`
- **Run Standard Stock Report & Ledger Verification:**
  - Verify **Secondary Batches Depleted:** `RICE-2026-002` and `OIL-2026-002` show `0.0000` on hand.
  - Verify KPI Card **Total Inventory Value:** Cleanly drops to **`0.00 LKR`** (-85,500.00 LKR).
  - Verify KPI Card **Total On-Hand Quantity:** Cleanly drops to **`0.0000`** units.
  - Verify **Warehouse Empty:** Both `RETAIL-WH1` and `RETAIL-WH2` have **`0.0000`** inventory.

| Transaction Source | Movement Type | Product Code & Name | Batch # | Warehouse | Location Affected | Dispatched Qty | Valuation Rate (LKR) | Cost Reduction (LKR) | Selling Rate (LKR) | Final Balance |
| :--- | :--- | :--- | :--- | :--- | :--- | :---: | :---: | :---: | :---: | :---: |
| `SH-YYYYYY` | `ShipmentDispatch` | `RET-001` Samba Rice 5kg | `RICE-2026-002` | `RETAIL-WH1` | `LOC-STK-01` | `-40.0000` | `1,500.00` | `-60,000.00` | `1,800.00` | **`0.0000`** |
| `SH-YYYYYY` | `ShipmentDispatch` | `RET-002` Cooking Oil 1L | `OIL-2026-002` | `RETAIL-WH1` | `LOC-STK-01` | `-30.0000` | `850.00` | `-25,500.00` | `1,050.00` | **`0.0000`** |
| **SO2 Total** | | | | | **`LOC-STK-01`** | **`-70.0000`** | | **`-85,500.00`** | | **`0.0000`** |

---

### Step 8: Physical Stock Count & Zero-Stock Reconciliation Verification
- **Logout** and **Login As:** `retail.store@retail.local` (Inventory Storekeeper)
- **Navigate to:** `Inventory -> Stock Counts`
- **Click:** `+ New Stock Count`
- **Warehouse:** `Central Retail Warehouse` (`RETAIL-WH1`) | Location: `LOC-STK-01`.
- **Verify System Stock & Enter Physical Counts (All Zero):**
  - `RET-001` (`RICE-2026-001`): System Qty `0.0000` | Counted `0.0000` -> Variance: `0.0000`
  - `RET-001` (`RICE-2026-002`): System Qty `0.0000` | Counted `0.0000` -> Variance: `0.0000`
  - `RET-002` (`OIL-2026-001`): System Qty `0.0000` | Counted `0.0000` -> Variance: `0.0000`
  - `RET-002` (`OIL-2026-002`): System Qty `0.0000` | Counted `0.0000` -> Variance: `0.0000`
  - `RET-003` (`SUGAR-2026-001`): System Qty `0.0000` | Counted `0.0000` -> Variance: `0.0000`
- **Workflow:**
  - Click `Submit` -> Then `Approve`.
  - Click `Post Count` -> Verify zero-variance stock count posts cleanly without errors.

#### Verification 8: Final Stock Count & System Valuation Audit Report
- **Navigate to:** `Inventory -> Overview`
- **Run Final Stock Valuation Report Verification:**

| Transaction Source | Movement Type | Product Code & Name | Warehouse | Location | System Qty | Counted Qty | Variance Qty | Valuation Rate (LKR) | Valuation Variance (LKR) | Final Confirmed Stock |
| :--- | :--- | :--- | :--- | :--- | :---: | :---: | :---: | :---: | :---: | :---: |
| `SC-XXXXXX` | `StockCountAudit` | `RET-001` Samba Rice 5kg | `RETAIL-WH1` | `LOC-STK-01` | `0.0000` | `0.0000` | `0.0000` | `1,500.00` | `0.00` | **`0.0000`** |
| `SC-XXXXXX` | `StockCountAudit` | `RET-002` Cooking Oil 1L | `RETAIL-WH1` | `LOC-STK-01` | `0.0000` | `0.0000` | `0.0000` | `850.00` | `0.00` | **`0.0000`** |
| `SC-XXXXXX` | `StockCountAudit` | `RET-003` White Sugar 1kg | `RETAIL-WH1` | `LOC-STK-01` | `0.0000` | `0.0000` | `0.0000` | `320.00` | `0.00` | **`0.0000`** |
| **All Locations** | | **All 3 Retail SKUs** | **All WHs** | **All Locs** | **`0.0000`** | **`0.0000`** | **`0.0000`** | | **`0.00`** | **`0.0000`** |

- **Confirmed Final Metrics:**
  - Total Stock Items: `3`
  - Total On-Hand Quantity: **`0.0000`**
  - Total Inventory Valuation: **`0.00 LKR`**
  - Confirms complete lifecycle from PR -> PO -> GRN -> Putaway -> Receipt -> Issue -> Transfer -> Transfer Return -> Sales Dispatches -> Stock Count reconciled at exact net zero!

---

### Step 9: Read-Only Audit & RBAC Verification
- **Logout** and **Login As:** `retail.viewer@retail.local` (Audit Viewer)
- **Navigate to:** `Procurement -> Purchase Requisitions`
  - Verify PR list is visible.
  - Verify `+ New Purchase Requisition` button is hidden or disabled.
- **Navigate to:** `Procurement -> Purchase Orders`
  - Verify purchase orders are viewable.
  - Verify `+ New Purchase Order` button is hidden or disabled.
- **Navigate to:** `Inventory -> Recent Transactions`
  - Verify full end-to-end movement audit history (GRN -> Putaway -> Receipt -> Issue -> Transfer -> Dispatch -> Stock Count).

---

## 3. End-State Stock Balance Reconciliation (Net-Zero Verification)

All inbound stock from the Goods Receipt Note (240 units) is completely sold and dispatched across Sales Orders 1 and 2, while intermediate direct receipts and issues offset each other (+10 vs -10), and inter-warehouse transfers are returned to WH1. Consequently, the final stock across every product, batch, warehouse, and location is cleanly **`0.0000`**, making testing and verification effortless and unambiguous.

| Product Code | Product Description | Batch Number | Warehouse Code | Location Code | Expected Final Quantity | Verification Status |
| :--- | :--- | :--- | :--- | :--- | :---: | :---: |
| `RET-001` | Samba Rice 5kg | `RICE-2026-001` | `RETAIL-WH1` | `LOC-STK-01` | **0.0000** | Pending Manual Run |
| `RET-001` | Samba Rice 5kg | `RICE-2026-002` | `RETAIL-WH1` | `LOC-STK-01` | **0.0000** | Pending Manual Run |
| `RET-001` | Samba Rice 5kg | `RICE-2026-001` | `RETAIL-WH2` | `LOC-WH2-01` | **0.0000** | Pending Manual Run |
| `RET-002` | Cooking Oil 1L | `OIL-2026-001` | `RETAIL-WH1` | `LOC-STK-01` | **0.0000** | Pending Manual Run |
| `RET-002` | Cooking Oil 1L | `OIL-2026-002` | `RETAIL-WH1` | `LOC-STK-01` | **0.0000** | Pending Manual Run |
| `RET-002` | Cooking Oil 1L | `OIL-2026-001` | `RETAIL-WH2` | `LOC-WH2-01` | **0.0000** | Pending Manual Run |
| `RET-003` | White Sugar 1kg | `SUGAR-2026-001` | `RETAIL-WH1` | `LOC-STK-01` | **0.0000** | Pending Manual Run |
| `RET-003` | White Sugar 1kg | `SUGAR-2026-001` | `RETAIL-WH2` | `LOC-WH2-01` | **0.0000** | Pending Manual Run |
| *(All)* | *(All Inbound)* | *(All)* | `RETAIL-WH1` | `LOC-REC-01` | **0.0000** | Receiving Dock Clear |

---

## 4. Defect and Anomaly Prevention Notes

1. **Multi-Batch GRN Putaway (`RET-DEF-001`):** Ensure putaway maps line items by `SourceDocumentLineId` to handle multiple batches on the same product cleanly.
2. **Stock Count Idempotency Key (`RET-DEF-002`):** When posting count adjustments with both shortages and overages, distinct transaction IDs are generated automatically.
3. **Batch Expiry Dates (`RET-DEF-003`):** Enter explicit manufacturing and expiration dates during GRN creation matching the registered test dates above.
4. **GRN Putaway Enum (`RET-DEF-004`):** Select source type `Goods Receipt Note` (`SourceDocumentType = 2`) when executing putaway from confirmed GRNs.
