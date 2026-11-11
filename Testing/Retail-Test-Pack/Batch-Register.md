# ApexERP Retail Test Pack — Authoritative Batch Register

## 1. Master Batch Inventory

All batch dates are authoritative. In ApexERP, creating or confirming a transaction with a conflicting manufacturing date or expiration date for an existing batch number throws an HTTP 409 Conflict error.

| Product Code | Batch Number | Manufacturing Date | Expiration Date | Batch ID (DB) | Registered Purpose |
| :--- | :--- | :--- | :--- | :--- | :--- |
| `RET-001` (Rice) | `RICE-2026-001` | `2026-01-15` | `2027-06-30` | `E1A11111-0000-0000-0000-000000000001` | Inbound GRN (60), Receipt (+15), Issue (-10), Transfer (-20), Dispatch SO1 (-20) |
| `RET-001` (Rice) | `RICE-2026-002` | `2026-02-01` | `2027-08-31` | `E1A11111-0000-0000-0000-000000000002` | Inbound GRN (40), Dispatch SO2 (-10) |
| `RET-002` (Oil) | `OIL-2026-001` | `2026-03-01` | `2027-09-30` | `E2A22222-0000-0000-0000-000000000001` | Inbound GRN (50), Receipt (+10), Issue (-5), Transfer (-10), Dispatch SO1 (-15), Stock Count Adj (-2) |
| `RET-002` (Oil) | `OIL-2026-002` | `2026-03-15` | `2027-10-31` | `E2A22222-0000-0000-0000-000000000002` | Inbound GRN (30), Dispatch SO2 (-10) |
| `RET-003` (Sugar) | `SUGAR-2026-001` | `2026-01-10` | `2027-07-31` | `E3A33333-0000-0000-0000-000000000001` | Inbound GRN (60), Transfer (-10), Dispatch SO1 (-10), Stock Count Adj (+5) |
| `RET-003` (Sugar) | `SUGAR-2026-002` | `2026-02-15` | `2027-09-30` | `E3A33333-0000-0000-0000-000000000002` | Stock Receipt (+10), Dispatch SO2 (-10) |

---

## 2. Invariant Rules for Batch Integrity

1. **Date Immutability:** Once a batch is registered in `ProductBatches` for a tenant, any subsequent transaction referencing that `BatchNumber` MUST supply the exact same `ManufacturingDate` and `ExpiryDate`.
2. **First-In, First-Out (FIFO) Cost Layering:** Batches created earlier (e.g. `OIL-2026-001` vs `OIL-2026-002`) are allocated first when sales shipments or stock counts relieve stock.
3. **Traceability:** Every `InventoryTransaction` record stores the active `BatchNumber` and `ExpiryDate`.
