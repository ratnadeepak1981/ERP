# ApexERP Retail Test Pack — Inventory Test Cases (Gate 6)

## 1. Step 6: Stock Receipt (Direct Inward Intake)

### TC-RET-INV-01: Direct Stock Receipt Posting (+30 Units)
- **Actor:** `retail.store` (Warehouse Storekeeper / Inventory Manager)
- **Role Code:** `INVENTORY_MANAGER`
- **Tenant Scope:** `retail` (`10000000-0000-0000-0000-000000000001`)
- **Company Scope:** `Lanka Retail Mart` (`30C46526-3161-455C-B362-227E8BD0A34A`)
- **Branch Scope:** `Main Store` (`5CFABB17-CBE0-43C5-99AB-72769D8DECF1`)
- **Permissions Verified:** `STOCK_RECEIPT.POST`, `INVENTORY.VIEW`, `BATCH.VIEW`, `BATCH.CREATE`
- **Allowed Actions:** View, Create Draft, Post Direct Stock Receipt
- **Restricted Actions:** Post without specifying location or batch
- **Endpoint:** `POST /api/inventory/stock-receipts`
- **Warehouse:** `RETAIL-WH1` (`Central Retail Warehouse`)
- **Location:** `LOC-STK-01` (`Main Storage Rack A-01`)
- **Reason:** `StockReceiptReason.Miscellaneous` (Sample / Promotional Intake)

#### Lines Received
| Product Code | Description | Batch Number | Received Qty | Valuation Rate (LKR) | Added Valuation (LKR) | New Rack Balance |
| :--- | :--- | :--- | :---: | :---: | :---: | :---: |
| `RET-001` | Samba Rice 5kg Pack | `RICE-2026-001` | `+10.0000` | 1,500.00 | +15,000.00 | `70.0000` |
| `RET-002` | Cooking Oil 1L | `OIL-2026-001` | `+10.0000` | 850.00 | +8,500.00 | `60.0000` |
| `RET-003` | White Sugar 1kg | `SUGAR-2026-001` | `+10.0000` | 320.00 | +3,200.00 | `70.0000` |

- **Expected Outcome:** **HTTP 200 OK**. Receipt posted with document `SR-RET-001`.
- **Inventory Impact:** Total on-hand quantity temporarily increases to `270.0000` units (+30 units); valuation increases to `263,900.00 LKR` (+26,700.00 LKR).
- **Next Step:** Direct Stock Issue (Step 7 / TC-RET-INV-02).

---

## 2. Step 7: Stock Issue (Operational / Sample Issue — Net Zero)

### TC-RET-INV-02: Direct Stock Issue Posting (-30 Units, Net Zero Offset)
- **Actor:** `retail.store` (Warehouse Storekeeper / Inventory Manager)
- **Role Code:** `INVENTORY_MANAGER`
- **Tenant Scope:** `retail` (`10000000-0000-0000-0000-000000000001`)
- **Company Scope:** `Lanka Retail Mart` (`30C46526-3161-455C-B362-227E8BD0A34A`)
- **Branch Scope:** `Main Store` (`5CFABB17-CBE0-43C5-99AB-72769D8DECF1`)
- **Permissions Verified:** `STOCK_ISSUE.POST`, `INVENTORY.VIEW`, `BATCH.VIEW`
- **Allowed Actions:** View, Create Draft, Post Direct Stock Issue
- **Restricted Actions:** Issue quantity exceeding location on-hand
- **Endpoint:** `POST /api/inventory/stock-issues`
- **Warehouse:** `RETAIL-WH1` (`Central Retail Warehouse`)
- **Location:** `LOC-STK-01` (`Main Storage Rack A-01`)
- **Purpose:** `StockIssuePurpose.InternalUse` (Quality Assurance / Pantry Sample Testing)

#### Lines Issued
| Product Code | Description | Batch Number | Issued Qty | Valuation Rate (LKR) | Deducted Valuation (LKR) | Restored Rack Balance |
| :--- | :--- | :--- | :---: | :---: | :---: | :---: |
| `RET-001` | Samba Rice 5kg Pack | `RICE-2026-001` | `-10.0000` | 1,500.00 | -15,000.00 | `60.0000` |
| `RET-002` | Cooking Oil 1L | `OIL-2026-001` | `-10.0000` | 850.00 | -8,500.00 | `50.0000` |
| `RET-003` | White Sugar 1kg | `SUGAR-2026-001` | `-10.0000` | 320.00 | -3,200.00 | `60.0000` |

- **Expected Outcome:** **HTTP 200 OK**. Issue posted with document `SI-RET-001`.
- **Net-Zero Reconciliation:** Direct receipt (+30 units, +26,700 LKR) and direct issue (-30 units, -26,700 LKR) cleanly offset each other to net zero. Rack `LOC-STK-01` returns exactly to the baseline **`240.0000`** units and **`237,200.00 LKR`**.
- **Next Step:** Inter-warehouse Transfer (Step 8 / TC-RET-INV-03).

---

## 3. Step 8: Inventory Item Transfer (WH1 to WH2)

### TC-RET-INV-03: Inter-Warehouse Transfer Out (40 Units)
- **Actor:** `retail.store` (Warehouse Storekeeper / Inventory Manager)
- **Role Code:** `INVENTORY_MANAGER`
- **Tenant Scope:** `retail` (`10000000-0000-0000-0000-000000000001`)
- **Company Scope:** `Lanka Retail Mart` (`30C46526-3161-455C-B362-227E8BD0A34A`)
- **Branch Scope:** `Main Store` (`5CFABB17-CBE0-43C5-99AB-72769D8DECF1`)
- **Permissions Verified:** `STOCK_TRANSFER.POST`, `INVENTORY.VIEW`, `WAREHOUSE.VIEW`, `LOCATION.VIEW`
- **Allowed Actions:** View, Create Transfer Draft, Post Transfer
- **Restricted Actions:** Transfer between unauthorized companies
- **Origin Warehouse:** `RETAIL-WH1` (`Central Retail Warehouse`) | Origin Location: `LOC-STK-01`
- **Destination Warehouse:** `RETAIL-WH2` (`Annex Retail Warehouse`) | Destination Location: `LOC-WH2-01`
- **Endpoints:**
  1. `POST /api/inventory/item-transfers` (Create Draft)
  2. `POST /api/inventory/item-transfers/{transferId}/post` (Execute Transfer)

#### Lines Transferred
| Product Code | Description | Batch Number | Transferred Qty | Valuation Rate (LKR) | Line Valuation (LKR) | WH1 Balance | WH2 Balance |
| :--- | :--- | :--- | :---: | :---: | :---: | :---: | :---: |
| `RET-001` | Samba Rice 5kg Pack | `RICE-2026-001` | `20.0000` | 1,500.00 | 30,000.00 | `40.0000` | `20.0000` |
| `RET-002` | Cooking Oil 1L | `OIL-2026-001` | `10.0000` | 850.00 | 8,500.00 | `40.0000` | `10.0000` |
| `RET-003` | White Sugar 1kg | `SUGAR-2026-001` | `10.0000` | 320.00 | 3,200.00 | `50.0000` | `10.0000` |

- **Expected Outcome:** **HTTP 200 OK**. Transfer posted with reference `TR-RET-001`.
- **Resulting Stock:** WH1 (`LOC-STK-01`) decrements to `200.0000` units (`195,500.00 LKR`); WH2 (`LOC-WH2-01`) increments to `40.0000` units (`41,700.00 LKR`). Total company valuation is unchanged at `237,200.00 LKR`.
- **Next Step:** Stock Count (Step 9 / TC-RET-INV-04).

---

## 4. Step 9: Stock Count & Discrepancy Adjustment

### TC-RET-INV-04: Physical Count, Variance Approval & Posting
- **Actor:** `retail.store` (Warehouse Storekeeper / Inventory Manager)
- **Role Code:** `INVENTORY_MANAGER`
- **Tenant Scope:** `retail` (`10000000-0000-0000-0000-000000000001`)
- **Company Scope:** `Lanka Retail Mart` (`30C46526-3161-455C-B362-227E8BD0A34A`)
- **Branch Scope:** `Main Store` (`5CFABB17-CBE0-43C5-99AB-72769D8DECF1`)
- **Permissions Verified:** `STOCK_COUNT.VIEW`, `STOCK_COUNT.CREATE`, `STOCK_COUNT.EDIT`, `STOCK_COUNT.SUBMIT`, `STOCK_COUNT.APPROVE`, `STOCK_COUNT.POST`
- **Allowed Actions:** View, Create Count Draft, Enter Physical Counts, Submit, Approve Variance, Post Adjustment
- **Restricted Actions:** Post count without approval
- **Count Location:** `RETAIL-WH1` (`LOC-STK-01`)
- **Endpoints:**
  1. `POST /api/stock-counts` (Create Draft)
  2. `POST /api/stock-counts/{countId}/submit` (Submit)
  3. `POST /api/stock-counts/{countId}/approve` (Approve Variances)
  4. `POST /api/stock-counts/{countId}/post` (Post Variance Adjustments)

#### Count Details & Variances
| Product | Batch | System Expected Qty | Counted Physical Qty | Variance Qty | Valuation Rate (LKR) | Valuation Effect (LKR) | Auto Adjustment Mechanism |
| :--- | :--- | :---: | :---: | :---: | :---: | :---: | :--- |
| `RET-001` | `RICE-2026-001` | `40.0000` | `40.0000` | `0.0000` | 1,500.00 | `0.00` | No Adjustment |
| `RET-002` | `OIL-2026-001` | `40.0000` | `38.0000` | `-2.0000` | 850.00 | `-1,700.00` | Auto Stock Issue (Shortage Write-off) |
| `RET-003` | `SUGAR-2026-001` | `50.0000` | `55.0000` | `+5.0000` | 320.00 | `+1,600.00` | Auto Stock Receipt (Overage Write-in) |

- **Expected Outcome:** **HTTP 200 OK**. Posting handles negative and positive variances atomically using distinct idempotency keys (`SC-ISSUE-{countId}` and `SC-RECEIPT-{countId}`).
- **Next Responsible User:** `retail.sales` (Sales Manager) for Sales Orders (Step 10).

---

## 5. Step 13: Inventory Reversals (Transfer Return WH2 to WH1)

### TC-RET-INV-05: Inter-Warehouse Transfer Return / Reversal
- **Actor:** `retail.store` (Warehouse Storekeeper / Inventory Manager)
- **Role Code:** `INVENTORY_MANAGER`
- **Tenant Scope:** `retail` (`10000000-0000-0000-0000-000000000001`)
- **Company Scope:** `Lanka Retail Mart` (`30C46526-3161-455C-B362-227E8BD0A34A`)
- **Branch Scope:** `Main Store` (`5CFABB17-CBE0-43C5-99AB-72769D8DECF1`)
- **Permissions Verified:** `STOCK_TRANSFER.POST`, `INVENTORY.VIEW`, `WAREHOUSE.VIEW`, `LOCATION.VIEW`
- **Allowed Actions:** View, Create Return Transfer Draft, Post Reversal
- **Origin Warehouse:** `RETAIL-WH2` (`Annex Retail Warehouse`) | Origin Location: `LOC-WH2-01`
- **Destination Warehouse:** `RETAIL-WH1` (`Central Retail Warehouse`) | Destination Location: `LOC-STK-01`
- **Endpoints:**
  1. `POST /api/inventory/item-transfers` (Create Draft)
  2. `POST /api/inventory/item-transfers/{transferId}/post` (Execute Reversal)

#### Lines Returned / Reversed
| Product Code | Description | Batch Number | Returned Qty | Valuation Rate (LKR) | Line Valuation (LKR) | WH2 Balance | WH1 Balance Restored |
| :--- | :--- | :--- | :---: | :---: | :---: | :---: | :---: |
| `RET-001` | Samba Rice 5kg Pack | `RICE-2026-001` | `20.0000` | 1,500.00 | 30,000.00 | **`0.0000`** | `60.0000` |
| `RET-002` | Cooking Oil 1L | `OIL-2026-001` | `10.0000` | 850.00 | 8,500.00 | **`0.0000`** | `48.0000` (net of count adj) |
| `RET-003` | White Sugar 1kg | `SUGAR-2026-001` | `10.0000` | 320.00 | 3,200.00 | **`0.0000`** | `65.0000` (net of count adj) |

- **Expected Outcome:** **HTTP 200 OK**. Reversal posted with reference `TR-RET-002`.
- **Reconciliation Impact:** WH2 (`LOC-WH2-01`) returns cleanly to **`0.0000`** units (`0.00 LKR`). All stock is restored to WH1 ready for final dispatch and zero-balance reconciliation.
- **Next Responsible User:** `retail.viewer` (Auditor) / `retail.admin` (Tenant Admin) for final reports (Step 14).
