# ApexERP Retail Test Pack — Expected Stock Balances and Mathematical Reconciliation

## 1. Inventory Flow Balance Sheet

All balances are reconciled by item, warehouse, location, and batch across each transaction phase.

```
Total Inflow:  GRN (240) + Stock Receipts (35) = 275 units
Total Outflow: Stock Issues (15) + Transfers (40) + Dispatches (105) + Net Stock Count Shortage (-2 + 5 = +3 net)
```

---

## 2. Product-by-Product Ledger Reconciliation

### Product 1: `RET-001` (Samba Rice 5kg Pack)

| Stage / Transaction | Trans Qty | Batch `RICE-2026-001` | Batch `RICE-2026-002` | WH1 `LOC-STK-01` | WH2 `LOC-WH2-01` | Total Company |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: |
| **1. Opening Balance** | 0 | 0 | 0 | 0 | 0 | 0 |
| **2. GRN Inflow** | +100 | +60 (Dock) | +40 (Dock) | 0 | 0 | 100 |
| **3. Putaway to Storage** | 0 | 60 | 40 | 100 | 0 | 100 |
| **4. Stock Receipt (+)** | +15 | +15 | 0 | 115 | 0 | 115 |
| **5. Stock Issue (-)** | -10 | -10 | 0 | 105 | 0 | 105 |
| **6. Item Transfer (WH1->WH2)** | 20 | -20 | 0 | 85 | 20 | 105 |
| **7. Sales Dispatch SO1 (-)** | -20 | -20 | 0 | 65 | 20 | 85 |
| **8. Sales Dispatch SO2 (-)** | -10 | 0 | -10 | 55 | 20 | 75 |
| **9. Physical Count Adj** | 0 | 0 (Phys=25) | 0 (Phys=30) | 55 | 20 | 75 |
| **Final Verified Balance** | | **25.00** | **30.00** | **55.00** | **20.00** | **75.00** |

---

### Product 2: `RET-002` (Cooking Oil 1L)

| Stage / Transaction | Trans Qty | Batch `OIL-2026-001` | Batch `OIL-2026-002` | WH1 `LOC-STK-01` | WH2 `LOC-WH2-01` | Total Company |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: |
| **1. Opening Balance** | 0 | 0 | 0 | 0 | 0 | 0 |
| **2. GRN Inflow** | +80 | +50 (Dock) | +30 (Dock) | 0 | 0 | 80 |
| **3. Putaway to Storage** | 0 | 50 | 30 | 80 | 0 | 80 |
| **4. Stock Receipt (+)** | +10 | +10 | 0 | 90 | 0 | 90 |
| **5. Stock Issue (-)** | -5 | -5 | 0 | 85 | 0 | 85 |
| **6. Item Transfer (WH1->WH2)** | 10 | -10 | 0 | 75 | 10 | 85 |
| **7. Sales Dispatch SO1 (-)** | -15 | -15 | 0 | 60 | 10 | 70 |
| **8. Sales Dispatch SO2 (-)** | -10 | 0 | -10 | 50 | 10 | 60 |
| **9. Physical Count Adj (Shortage)** | -2 | -2 (Write-off) | 0 | 48 | 10 | 58 |
| **Final Verified Balance** | | **28.00** | **20.00** | **48.00** | **10.00** | **58.00** |

---

### Product 3: `RET-003` (White Sugar 1kg)

| Stage / Transaction | Trans Qty | Batch `SUGAR-2026-001` | Batch `SUGAR-2026-002` | WH1 `LOC-STK-01` | WH2 `LOC-WH2-01` | Total Company |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: |
| **1. Opening Balance** | 0 | 0 | 0 | 0 | 0 | 0 |
| **2. GRN Inflow** | +60 | +60 (Dock) | 0 | 0 | 0 | 60 |
| **3. Putaway to Storage** | 0 | 60 | 0 | 60 | 0 | 60 |
| **4. Stock Receipt (+)** | +10 | 0 | +10 | 70 | 0 | 70 |
| **5. Stock Issue (-)** | 0 | 0 | 0 | 70 | 0 | 70 |
| **6. Item Transfer (WH1->WH2)** | 10 | -10 | 0 | 60 | 10 | 70 |
| **7. Sales Dispatch SO1 (-)** | -10 | -10 | 0 | 50 | 10 | 60 |
| **8. Sales Dispatch SO2 (-)** | -10 | 0 | -10 | 40 | 10 | 50 |
| **9. Physical Count Adj (Overage)** | +5 | +5 (Write-in) | 0 | 45 | 10 | 55 |
| **Final Verified Balance** | | **45.00** | **0.00** | **45.00** | **10.00** | **55.00** |

---

## 3. Summary of Final Quantities by Location

| Location | Code | `RET-001` (Rice) | `RET-002` (Oil) | `RET-003` (Sugar) | Total Units |
| :--- | :--- | :---: | :---: | :---: | :---: |
| **WH1 Inbound Dock** | `LOC-REC-01` | 0.00 | 0.00 | 0.00 | 0.00 |
| **WH1 Main Storage** | `LOC-STK-01` | 55.00 | 48.00 | 45.00 | 148.00 |
| **WH2 Main Storage** | `LOC-WH2-01` | 20.00 | 10.00 | 10.00 | 40.00 |
| **Total Company Stock** | | **75.00** | **58.00** | **55.00** | **188.00** |
