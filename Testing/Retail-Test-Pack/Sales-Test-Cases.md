# ApexERP Retail Test Pack — Sales Test Cases (Gate 6)

## 1. Step 10: Sales Orders (SO1 & SO2)

### TC-RET-SO-01: Sales Order 1 (Primary Batches — 170 Units Total)
- **Actor:** `retail.sales` (Sales Representative / Sales Manager)
- **Role Code:** `SALES_MANAGER`
- **Tenant Scope:** `retail` (`10000000-0000-0000-0000-000000000001`)
- **Company Scope:** `Lanka Retail Mart` (`30C46526-3161-455C-B362-227E8BD0A34A`)
- **Branch Scope:** `Main Store` (`5CFABB17-CBE0-43C5-99AB-72769D8DECF1`)
- **Permissions Verified:** `SALES_ORDER.VIEW`, `SALES_ORDER.CREATE`, `SALES_ORDER.EDIT`, `SALES_ORDER.SUBMIT`, `SALES_ORDER.CONFIRM`, `CUSTOMER.VIEW`
- **Allowed Actions:** View, Create Draft, Submit, Confirm Sales Order
- **Restricted Actions:** Post physical inventory without shipment
- **Customer:** `Cargills Retail Supermarkets PLC` (`CUST-RET-001` / `0299BA16-6287-4348-8C64-D72B8FEFA3A7`)
- **Order Number:** `SO-RET-1-XXXX`
- **Currency:** `LKR`
- **Warehouse:** `RETAIL-WH1` (`Central Retail Warehouse`)
- **Endpoints:**
  1. `POST /api/companies/{companyId}/branches/{branchId}/sales-orders` (Draft)
  2. `POST /api/companies/{companyId}/branches/{branchId}/sales-orders/{id}/submit`
  3. `POST /api/companies/{companyId}/branches/{branchId}/sales-orders/{id}/confirm`

#### Lines Ordered (Primary Batches Target)
| Line | Product Code | Description | Ordered Qty | UOM | Unit Price (LKR) | Tax Code | Line Total (LKR) | Target Allocation Batch |
| :---: | :--- | :--- | :---: | :---: | :---: | :---: | :---: | :--- |
| 1 | `RET-001` | Samba Rice 5kg Pack | 60 | BAG | 1,800.00 | TAX-EXEMPT | 108,000.00 | `RICE-2026-001` (60 units) |
| 2 | `RET-002` | Cooking Oil 1L | 50 | BTL | 1,050.00 | VAT-18 | 61,950.00 | `OIL-2026-001` (50 units) |
| 3 | `RET-003` | White Sugar 1kg | 60 | PKT | 400.00 | TAX-EXEMPT | 24,000.00 | `SUGAR-2026-001` (60 units) |

- **Expected Outcome:** **HTTP 200 OK**. Order status: `Confirmed`. Line total = 170 units. Subtotal = `184,500.00 LKR` (Net total with tax = `193,950.00 LKR`).
- **Next Step:** Create Shipment 1 (Step 11 / TC-RET-SH-01).

---

### TC-RET-SO-02: Sales Order 2 (Secondary Batches — 70 Units Total)
- **Actor:** `retail.sales` (Sales Manager)
- **Role Code:** `SALES_MANAGER`
- **Tenant Scope:** `retail` (`10000000-0000-0000-0000-000000000001`)
- **Company Scope:** `Lanka Retail Mart` (`30C46526-3161-455C-B362-227E8BD0A34A`)
- **Branch Scope:** `Main Store` (`5CFABB17-CBE0-43C5-99AB-72769D8DECF1`)
- **Permissions Verified:** `SALES_ORDER.CREATE`, `SALES_ORDER.CONFIRM`
- **Customer:** `Keells Supermarkets Holdings` (`CUST-RET-002`)
- **Order Number:** `SO-RET-2-XXXX`
- **Currency:** `LKR`
- **Warehouse:** `RETAIL-WH1` (`Central Retail Warehouse`)
- **Endpoints:**
  1. `POST /api/companies/{companyId}/branches/{branchId}/sales-orders` (Draft)
  2. `POST /api/companies/{companyId}/branches/{branchId}/sales-orders/{id}/submit`
  3. `POST /api/companies/{companyId}/branches/{branchId}/sales-orders/{id}/confirm`

#### Lines Ordered (Secondary Batches Target)
| Line | Product Code | Description | Ordered Qty | UOM | Unit Price (LKR) | Tax Code | Line Total (LKR) | Target Allocation Batch |
| :---: | :--- | :--- | :---: | :---: | :---: | :---: | :---: | :--- |
| 1 | `RET-001` | Samba Rice 5kg Pack | 40 | BAG | 1,800.00 | TAX-EXEMPT | 72,000.00 | `RICE-2026-002` (40 units) |
| 2 | `RET-002` | Cooking Oil 1L | 30 | BTL | 1,050.00 | VAT-18 | 37,170.00 | `OIL-2026-002` (30 units) |

- **Expected Outcome:** **HTTP 200 OK**. Order status: `Confirmed`. Line total = 70 units. Subtotal = `97,000.00 LKR` (Net total with tax = `102,670.00 LKR`).
- **Next Step:** Create Shipment 2 (Step 11 / TC-RET-SH-02).

---

## 2. Step 11: Dispatch Notes / Sales Shipments (SH1 & SH2)

### TC-RET-SH-01: Shipment 1 Dispatch (Relieve Primary Batches — 170 Units)
- **Actor:** `retail.sales` (Sales Manager)
- **Role Code:** `SALES_MANAGER`
- **Permissions Verified:** `SALES_SHIPMENT.CREATE`, `SALES_SHIPMENT.DISPATCH`, `INVENTORY.VIEW`, `BATCH.VIEW`
- **Source Document:** Confirmed Sales Order 1 (`SO-RET-1-XXXX`)
- **Endpoints:**
  1. `POST /api/companies/{companyId}/branches/{branchId}/sales-shipments/from-order/{orderId}` (Create Draft)
  2. `POST /api/companies/{companyId}/branches/{branchId}/sales-shipments/{shipmentId}/dispatch` (Execute Dispatch)

#### Batch Allocation & Physical Dispatch
| Line | Product Code | Description | Batch Number | Dispatch Qty | Storage Location | Valuation Cost Relieved (LKR) | Resulting Batch Balance |
| :---: | :--- | :--- | :--- | :---: | :--- | :---: | :---: |
| 1 | `RET-001` | Samba Rice 5kg Pack | `RICE-2026-001` | `60.0000` | `LOC-STK-01` | -90,000.00 | **`0.0000`** |
| 2 | `RET-002` | Cooking Oil 1L | `OIL-2026-001` | `50.0000` | `LOC-STK-01` | -42,500.00 | **`0.0000`** |
| 3 | `RET-003` | White Sugar 1kg | `SUGAR-2026-001` | `60.0000` | `LOC-STK-01` | -19,200.00 | **`0.0000`** |

- **Expected Outcome:** **HTTP 200 OK**. Shipment status: `Dispatched`.
- **Inventory Impact:** 170 units relieved from `LOC-STK-01`. Inventory valuation decreases by `151,700.00 LKR`. Primary batches `RICE-2026-001`, `OIL-2026-001`, and `SUGAR-2026-001` are completely depleted to **`0.0000`**.
- **Next Step:** Create Sales Invoice 1 (Step 12 / TC-RET-INV-01).

---

### TC-RET-SH-02: Shipment 2 Dispatch (Relieve Secondary Batches — 70 Units)
- **Actor:** `retail.sales` (Sales Manager)
- **Role Code:** `SALES_MANAGER`
- **Permissions Verified:** `SALES_SHIPMENT.CREATE`, `SALES_SHIPMENT.DISPATCH`, `INVENTORY.VIEW`, `BATCH.VIEW`
- **Source Document:** Confirmed Sales Order 2 (`SO-RET-2-XXXX`)
- **Endpoints:**
  1. `POST /api/companies/{companyId}/branches/{branchId}/sales-shipments/from-order/{orderId}`
  2. `POST /api/companies/{companyId}/branches/{branchId}/sales-shipments/{shipmentId}/dispatch`

#### Batch Allocation & Physical Dispatch
| Line | Product Code | Description | Batch Number | Dispatch Qty | Storage Location | Valuation Cost Relieved (LKR) | Resulting Batch Balance |
| :---: | :--- | :--- | :--- | :---: | :--- | :---: | :---: |
| 1 | `RET-001` | Samba Rice 5kg Pack | `RICE-2026-002` | `40.0000` | `LOC-STK-01` | -60,000.00 | **`0.0000`** |
| 2 | `RET-002` | Cooking Oil 1L | `OIL-2026-002` | `30.0000` | `LOC-STK-01` | -25,500.00 | **`0.0000`** |

- **Expected Outcome:** **HTTP 200 OK**. Shipment status: `Dispatched`.
- **Inventory Impact:** 70 units relieved from `LOC-STK-01`. Inventory valuation decreases by `85,500.00 LKR`. Secondary batches `RICE-2026-002` and `OIL-2026-002` are completely depleted to **`0.0000`**.
- **Net Balance Check:** All warehouse storage and dock locations reach exactly **`0.0000`** units and **`0.00 LKR`** valuation!
- **Next Step:** Create Sales Invoice 2 (Step 12 / TC-RET-INV-02).

---

## 3. Step 12: Sales Invoicing (INV1 & INV2)

### TC-RET-INV-01: Sales Invoice 1 Creation from Shipment 1 & Confirmation
- **Actor:** `retail.sales` (Sales Manager)
- **Role Code:** `SALES_MANAGER`
- **Tenant Scope:** `retail` (`10000000-0000-0000-0000-000000000001`)
- **Company Scope:** `Lanka Retail Mart` (`30C46526-3161-455C-B362-227E8BD0A34A`)
- **Branch Scope:** `Main Store` (`5CFABB17-CBE0-43C5-99AB-72769D8DECF1`)
- **Permissions Verified:** `SALES_INVOICE.VIEW`, `SALES_INVOICE.CREATE`, `SALES_INVOICE.EDIT`, `SALES_INVOICE.CONFIRM`
- **Allowed Actions:** View, Create Draft from Shipment, Update, Confirm Invoice
- **Restricted Actions:** Modify confirmed invoice
- **Source Document:** Dispatched Shipment 1 (`SH-RET-1-XXXX`)
- **Endpoints:**
  1. `POST /api/companies/{companyId}/branches/{branchId}/sales-invoices/from-shipment/{shipmentId}` (Create Draft)
  2. `POST /api/companies/{companyId}/branches/{branchId}/sales-invoices/{id}/confirm` (Confirm Invoice)
- **Invoice Financials:**
  - SubTotal: `184,500.00 LKR`
  - Tax Amount: `9,450.00 LKR` (18% on Line 2 `RET-002`: `50 * 1,050 = 52,500 * 18% = 9,450.00`)
  - Total Invoice Amount: `193,950.00 LKR`
- **Expected Outcome:** **HTTP 200 OK**. Invoice status: `Confirmed`. Invoice reference generated: `INV-RET-1-XXXX`.

---

### TC-RET-INV-02: Sales Invoice 2 Creation from Shipment 2 & Confirmation
- **Actor:** `retail.sales` (Sales Manager)
- **Role Code:** `SALES_MANAGER`
- **Permissions Verified:** `SALES_INVOICE.CREATE`, `SALES_INVOICE.CONFIRM`
- **Source Document:** Dispatched Shipment 2 (`SH-RET-2-XXXX`)
- **Endpoints:**
  1. `POST /api/companies/{companyId}/branches/{branchId}/sales-invoices/from-shipment/{shipmentId}`
  2. `POST /api/companies/{companyId}/branches/{branchId}/sales-invoices/{id}/confirm`
- **Invoice Financials:**
  - SubTotal: `97,000.00 LKR`
  - Tax Amount: `5,670.00 LKR` (18% on Line 2 `RET-002`: `30 * 1,050 = 31,500 * 18% = 5,670.00`)
  - Total Invoice Amount: `102,670.00 LKR`
- **Expected Outcome:** **HTTP 200 OK**. Invoice status: `Confirmed`. Invoice reference generated: `INV-RET-2-XXXX`.
- **Next Responsible User:** `retail.store` (Inventory Manager) for Inventory Reversals (Step 13) or `retail.viewer` (Auditor) for final reports (Step 14).
