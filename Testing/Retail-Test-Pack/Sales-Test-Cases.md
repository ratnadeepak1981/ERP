# ApexERP Retail Test Pack — Sales Test Cases

## 1. Sales Order 1: Bulk Institutional Customer Order

- **Test Identifier:** `TC-RET-SALES-01`
- **Actor:** `retail.sales` (Sales Representative / Store Cashier)
- **Role Code:** `SalesExecutive`
- **Customer:** `Cargills Retail Supermarkets PLC` (`CUST-RET-001` / `0299BA16-6287-4348-8C64-D72B8FEFA3A7`)
- **Order Number:** `SO-RET-1-XXXX`
- **Currency:** `LKR`
- **Lines Ordered:**
  1. `RET-001` (Rice): 20 BAG @ 1,850.00 LKR = 37,000.00 LKR
  2. `RET-002` (Oil): 15 BTL @ 1,050.00 LKR = 15,750.00 LKR
  3. `RET-003` (Sugar): 10 PKT @ 400.00 LKR = 4,000.00 LKR
- **Workflow Steps:**
  1. `POST /api/companies/{companyId}/branches/{branchId}/sales-orders` (Draft)
  2. `POST /api/companies/{companyId}/branches/{branchId}/sales-orders/{id}/submit`
  3. `POST /api/companies/{companyId}/branches/{branchId}/sales-orders/{id}/confirm`
- **Shipment & Dispatch:**
  - Create Shipment from Order: `POST /api/companies/{companyId}/branches/{branchId}/sales-shipments/from-order/{id}`
  - Allocated Batches: `RICE-2026-001` (20), `OIL-2026-001` (15), `SUGAR-2026-001` (10)
  - Execute Dispatch: `POST /api/companies/{companyId}/branches/{branchId}/sales-shipments/{shipmentId}/dispatch`
- **Outcome:** **HTTP 200 OK**. Stock relieved from `LOC-STK-01`.

---

## 2. Sales Order 2: Walk-In Retail Distribution Order

- **Test Identifier:** `TC-RET-SALES-02`
- **Actor:** `retail.sales`
- **Role Code:** `SalesExecutive`
- **Customer:** `Cargills Retail Supermarkets PLC` (`CUST-RET-001`)
- **Order Number:** `SO-RET-2-XXXX`
- **Currency:** `LKR`
- **Lines Ordered:**
  1. `RET-001` (Rice): 10 BAG @ 1,850.00 LKR = 18,500.00 LKR
  2. `RET-002` (Oil): 10 BTL @ 1,050.00 LKR = 10,500.00 LKR
  3. `RET-003` (Sugar): 10 PKT @ 400.00 LKR = 4,000.00 LKR
- **Workflow Steps:**
  1. Create, submit, and confirm Sales Order 2.
  2. Create Shipment allocating from secondary batch sets:
     - `RET-001`: `RICE-2026-002` (10)
     - `RET-002`: `OIL-2026-002` (10)
     - `RET-003`: `SUGAR-2026-002` (10)
  3. Execute Dispatch via `/dispatch`.
- **Outcome:** **HTTP 200 OK**. Validates that secondary and later batches are correctly relieved when specified.
