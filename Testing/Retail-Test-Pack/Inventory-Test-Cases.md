# ApexERP Retail Test Pack — Inventory Test Cases

## 1. Stock Receipt (Direct Inward Adjustment)

- **Test Identifier:** `TC-RET-INV-01`
- **Actor:** `retail.store` (Warehouse Storekeeper)
- **Role Code:** `InventoryStorekeeper`
- **Endpoint:** `POST /api/inventory/stock-receipts`
- **Warehouse:** `RETAIL-WH1` | **Location:** `LOC-STK-01`
- **Reason:** `StockReceiptReason.Miscellaneous`
- **Lines Received:**
  - `RET-001` (Rice) on batch `RICE-2026-001`: **+15 units** @ 1,500 LKR
  - `RET-002` (Oil) on batch `OIL-2026-001`: **+10 units** @ 850 LKR
  - `RET-003` (Sugar) on new batch `SUGAR-2026-002` (Mfg: 2026-02-15, Exp: 2027-09-30): **+10 units** @ 320 LKR
- **Outcome:** **HTTP 200 OK**. Stock balances updated immediately in `LOC-STK-01`.

---

## 2. Stock Issue (Internal Operational Usage)

- **Test Identifier:** `TC-RET-INV-02`
- **Actor:** `retail.store`
- **Role Code:** `InventoryStorekeeper`
- **Endpoint:** `POST /api/inventory/stock-issues`
- **Warehouse:** `RETAIL-WH1` | **Location:** `LOC-STK-01`
- **Purpose:** `StockIssuePurpose.InternalUse`
- **Remarks:** `Internal consumption / store pantry sample testing`
- **Lines Issued:**
  - `RET-001` (Rice) from batch `RICE-2026-001`: **10 units**
  - `RET-002` (Oil) from batch `OIL-2026-001`: **5 units**
- **Outcome:** **HTTP 200 OK**. Batch quantities accurately decremented without affecting unrelated batches.

---

## 3. Inventory Item Transfer (WH1 to WH2)

- **Test Identifier:** `TC-RET-INV-03`
- **Actor:** `retail.store`
- **Role Code:** `InventoryStorekeeper`
- **Endpoints:**
  1. `POST /api/inventory/item-transfers` (Create Draft)
  2. `POST /api/inventory/item-transfers/{transferId}/post` (Execute Transfer)
- **Source:** `RETAIL-WH1` (`LOC-STK-01`)
- **Destination:** `RETAIL-WH2` (`LOC-WH2-01`)
- **Lines Transferred:**
  - `RET-001`: **20 units** on batch `RICE-2026-001`
  - `RET-002`: **10 units** on batch `OIL-2026-001`
  - `RET-003`: **10 units** on batch `SUGAR-2026-001`
- **Outcome:** **HTTP 200 OK**.
- **Resulting Stock in WH2 (`LOC-WH2-01`):**
  - `RET-001`: 20
  - `RET-002`: 10
  - `RET-003`: 10

---

## 4. Stock Count & Discrepancy Adjustment

- **Test Identifier:** `TC-RET-INV-04`
- **Actor:** `retail.store`
- **Role Code:** `InventoryStorekeeper`
- **Endpoints:**
  1. `POST /api/stock-counts` (Create Physical Count Draft)
  2. `POST /api/stock-counts/{countId}/submit` (Submit Count)
  3. `POST /api/stock-counts/{countId}/approve` (Approve Variance)
  4. `POST /api/stock-counts/{countId}/post` (Post Variance Adjustments)
- **Scope:** `RETAIL-WH1` (`LOC-STK-01`)
- **Count Details & Variances:**
  | Product | Batch | Expected Qty | Counted Physical Qty | Variance Base | Valuation Effect |
  | :--- | :--- | :---: | :---: | :---: | :--- |
  | `RET-001` (Rice) | `RICE-2026-001` | 25 | 25 | **0** | No change |
  | `RET-002` (Oil) | `OIL-2026-001` | 30 | 28 | **-2** (Shortage) | Auto Stock Issue (Write-off) |
  | `RET-003` (Sugar) | `SUGAR-2026-001` | 40 | 45 | **+5** (Overage) | Auto Stock Receipt (Write-in) |
- **Outcome:** **HTTP 200 OK**. Posting handles both negative and positive variances atomically with separate deterministic idempotency keys (`SC-ISSUE-{countId}` and `SC-RECEIPT-{countId}`).
