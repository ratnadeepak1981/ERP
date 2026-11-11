# ApexERP — Retail Test Pack: Users, Roles, Credentials and Scenario Matrix (Gate 6)

## 1. Test User Accounts and Canonical Identities

To ensure exact parity between manual browser testing, automated API integration testing, and security authorization checks, the following canonical test identities are established.

| Username | Login Email Alias | Intended Responsibility | Actual Role (`Role.Name`) | Template Key | Password Setup Status | Assigned Scope |
|---|---|---|---|---|---|---|
| `retail.admin` | `retail.admin@retail.local` | Tenant Administration, Master Data & Full Governance | `Tenant Admin` | `TENANT_ADMIN` | **`123`** (Verified Accepted) | Full Tenant Scope (All Companies & Branches) |
| `retail.po` | `retail.po@retail.local` | Procurement (PR & PO Creation, Line Editing, Submission) | `Procurement Officer` | `PROCUREMENT_OFFICER` | **`123`** (Verified Accepted) | Company: Lanka Retail Mart, Branch: Main Store |
| `retail.approver` | `retail.approver@retail.local` | Independent Approval (PR & PO Approval / Rejection, SoD) | `Procurement Manager` | `PROCUREMENT_MANAGER` | **`123`** (Verified Accepted) | Company: Lanka Retail Mart, Branch: Main Store |
| `retail.grn` | `retail.grn@retail.local` | Goods Receipt (GRN Creation from PO, Batch Splits, Confirmation) | `Procurement Officer` | `PROCUREMENT_OFFICER` | **`123`** (Verified Accepted) | Company: Lanka Retail Mart, Branch: Main Store |
| `retail.store` | `retail.store@retail.local` | Warehouse, Putaway, Receipts, Issues, Transfers, Counts, Reversals | `Inventory Manager` | `INVENTORY_MANAGER` | **`123`** (Verified Accepted) | Company: Lanka Retail Mart, Branch: Main Store |
| `retail.sales` | `retail.sales@retail.local` | Sales Orders, Order Confirmation, Shipments / Dispatch, Invoicing | `Sales Manager` | `SALES_MANAGER` | **`123`** (Verified Accepted) | Company: Lanka Retail Mart, Branch: Main Store |
| `retail.viewer` | `retail.viewer@retail.local` | Read-only Audit Verification, Historical Ledger & RBAC Enforcement | `Tenant Read-only Auditor` | `TENANT_AUDITOR` | **`123`** (Verified Accepted) | Company: Lanka Retail Mart, Branch: Main Store |

---

## 2. Scenario-by-Scenario User & Role Matrix (14-Step Workflow)

The table below defines for every scenario in the 14-step workflow:
- The actual username.
- Assigned role.
- Tenant, company, and branch scope.
- Required permissions.
- Operational capabilities (View, Create, Edit, Submit, Approve, Confirm, Post).
- The next responsible user for handover upon completion.

| Step # | Workflow Scenario | Actual Username | Assigned Role | Tenant Scope | Company Scope | Branch Scope | Required Permissions | Allowed Actions | Restricted Actions | Next Responsible User |
| :---: | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **01** | **Purchase Requisition (PR)** | `retail.po` | `Procurement Officer` | `retail` (`1000...0001`) | Lanka Retail Mart (`30C4...A34A`) | Main Store (`5CFA...ECF1`) | `PURCHASE_REQUISITION.VIEW`, `PURCHASE_REQUISITION.CREATE`, `PURCHASE_REQUISITION.EDIT`, `PURCHASE_REQUISITION.SUBMIT` | View, Create, Edit, Submit, Cancel (Draft) | Approve, Reject (Returns HTTP 403) | `retail.approver` (Procurement Manager) |
| **01-A**| **PR Approval / Rejection** | `retail.approver` | `Procurement Manager` | `retail` (`1000...0001`) | Lanka Retail Mart (`30C4...A34A`) | Main Store (`5CFA...ECF1`) | `PURCHASE_REQUISITION.VIEW`, `PURCHASE_REQUISITION.APPROVE` | View, Approve, Reject | Edit Lines of Submitted PR | `retail.po` (Procurement Officer) |
| **02** | **Purchase Order (PO)** | `retail.po` | `Procurement Officer` | `retail` (`1000...0001`) | Lanka Retail Mart (`30C4...A34A`) | Main Store (`5CFA...ECF1`) | `PURCHASE_ORDER.VIEW`, `PURCHASE_ORDER.CREATE`, `PURCHASE_ORDER.EDIT`, `PURCHASE_ORDER.SUBMIT`, `SUPPLIER.VIEW`, `PRODUCT.VIEW` | View, Create (from PR), Edit, Submit | Approve (Returns HTTP 403) | `retail.approver` (Procurement Manager) |
| **02-A**| **PO Approval** | `retail.approver` | `Procurement Manager` | `retail` (`1000...0001`) | Lanka Retail Mart (`30C4...A34A`) | Main Store (`5CFA...ECF1`) | `PURCHASE_ORDER.VIEW`, `PURCHASE_ORDER.APPROVE` | View, Approve, Reject | Self-Approval if creator | `retail.grn` (Receiving Clerk) |
| **03** | **Product Receipt / GRN** | `retail.grn` | `Procurement Officer` | `retail` (`1000...0001`) | Lanka Retail Mart (`30C4...A34A`) | Main Store (`5CFA...ECF1`) | `GRN.VIEW`, `GRN.CREATE`, `GRN.EDIT`, `WAREHOUSE.VIEW` | View, Create Draft from PO, Edit Dock Lines | Confirm without Batch Data | Same user for Step 04 (`retail.grn`) |
| **04** | **Batch Allocation & Confirm**| `retail.grn` | `Procurement Officer` | `retail` (`1000...0001`) | Lanka Retail Mart (`30C4...A34A`) | Main Store (`5CFA...ECF1`) | `GRN.EDIT`, `GRN.CONFIRM`, `BATCH.CREATE`, `BATCH.VIEW` | View, Enter 5 Batches (240 units), Confirm Receipt | Post Putaway movements | `retail.store` (Inventory Manager) |
| **05** | **Warehouse Putaway** | `retail.store` | `Inventory Manager` | `retail` (`1000...0001`) | Lanka Retail Mart (`30C4...A34A`) | Main Store (`5CFA...ECF1`) | `PUTAWAY.POST`, `INVENTORY.VIEW`, `WAREHOUSE.VIEW`, `LOCATION.VIEW`, `BATCH.VIEW` | View, Create Putaway from GRN, Confirm / Post Movements | Modify source GRN | Same user for Step 06 (`retail.store`) |
| **06** | **Stock Receipt** | `retail.store` | `Inventory Manager` | `retail` (`1000...0001`) | Lanka Retail Mart (`30C4...A34A`) | Main Store (`5CFA...ECF1`) | `STOCK_RECEIPT.POST`, `INVENTORY.VIEW`, `BATCH.VIEW`, `BATCH.CREATE` | View, Create, Post Direct Inward (+30 units) | Approve POs / GRNs | Same user for Step 07 (`retail.store`) |
| **07** | **Stock Issue** | `retail.store` | `Inventory Manager` | `retail` (`1000...0001`) | Lanka Retail Mart (`30C4...A34A`) | Main Store (`5CFA...ECF1`) | `STOCK_ISSUE.POST`, `INVENTORY.VIEW`, `BATCH.VIEW` | View, Create, Post Direct Issue (-30 units, Net Zero) | Issue unallocated stock | Same user for Step 08 (`retail.store`) |
| **08** | **Inventory Item Transfer** | `retail.store` | `Inventory Manager` | `retail` (`1000...0001`) | Lanka Retail Mart (`30C4...A34A`) | Main Store (`5CFA...ECF1`) | `STOCK_TRANSFER.POST`, `INVENTORY.VIEW`, `WAREHOUSE.VIEW`, `LOCATION.VIEW` | View, Create Transfer Draft, Post Transfer (WH1 to WH2) | Transfer across unauthorized companies | Same user for Step 09 (`retail.store`) |
| **09** | **Stock Count** | `retail.store` | `Inventory Manager` | `retail` (`1000...0001`) | Lanka Retail Mart (`30C4...A34A`) | Main Store (`5CFA...ECF1`) | `STOCK_COUNT.VIEW`, `STOCK_COUNT.CREATE`, `STOCK_COUNT.SUBMIT`, `STOCK_COUNT.APPROVE`, `STOCK_COUNT.POST` | View, Create Count, Submit, Approve Variance, Post Adjustment | Post without approval | `retail.sales` (Sales Manager) |
| **10** | **Sales Order (SO1 & SO2)** | `retail.sales` | `Sales Manager` | `retail` (`1000...0001`) | Lanka Retail Mart (`30C4...A34A`) | Main Store (`5CFA...ECF1`) | `SALES_ORDER.VIEW`, `SALES_ORDER.CREATE`, `SALES_ORDER.EDIT`, `SALES_ORDER.SUBMIT`, `SALES_ORDER.CONFIRM`, `CUSTOMER.VIEW` | View, Create SO1 & SO2, Submit, Confirm | Post physical inventory without shipment | Same user for Step 11 (`retail.sales`) |
| **11** | **Dispatch Note (Shipment)**| `retail.sales` | `Sales Manager` | `retail` (`1000...0001`) | Lanka Retail Mart (`30C4...A34A`) | Main Store (`5CFA...ECF1`) | `SALES_SHIPMENT.VIEW`, `SALES_SHIPMENT.CREATE`, `SALES_SHIPMENT.DISPATCH`, `INVENTORY.VIEW`, `BATCH.VIEW` | View, Create Shipment from SO, Allocate Batches, Dispatch | Dispatch without batch allocation | Same user for Step 12 (`retail.sales`) |
| **12** | **Sales Invoice** | `retail.sales` | `Sales Manager` | `retail` (`1000...0001`) | Lanka Retail Mart (`30C4...A34A`) | Main Store (`5CFA...ECF1`) | `SALES_INVOICE.VIEW`, `SALES_INVOICE.CREATE`, `SALES_INVOICE.EDIT`, `SALES_INVOICE.CONFIRM` | View, Create Draft from Shipment, Update, Confirm Invoice | Modify confirmed invoice | `retail.store` (Inventory Manager) |
| **13** | **Inventory Reversals** | `retail.store` | `Inventory Manager` | `retail` (`1000...0001`) | Lanka Retail Mart (`30C4...A34A`) | Main Store (`5CFA...ECF1`) | `STOCK_TRANSFER.POST`, `INVENTORY.VIEW`, `WAREHOUSE.VIEW` | View, Create Transfer Return Draft (WH2 to WH1), Post Reversal | Reversal into invalid location | `retail.viewer` (Auditor) / `retail.admin` |
| **14** | **Reports & Audit Oversight**| `retail.viewer`<br>*(Oversight: `retail.admin`)* | `Tenant Read-only Auditor`<br>*(Admin: `Tenant Admin`)*| `retail` (`1000...0001`) | Lanka Retail Mart (`30C4...A34A`) | Main Store (`5CFA...ECF1`) | `*.VIEW` (Auditor)<br>All 89 permissions (Tenant Admin) | View all lists, stock balances, valuation layers, transaction ledgers | Any mutation, creation, deletion or posting (Auditor) | Test Suite Complete (Sign-off) |

---

## 3. Password Requirements & Verification Analysis (`123`)

### Backend Authentication Policy Inspection
1. **Password Hashing:** `PasswordService.cs` uses standard PBKDF2 with HMAC-SHA256 (100,000 iterations, 16-byte cryptographically secure salt, 32-byte subkey).
2. **Validation Rules:** In `PasswordService.cs` and `UserService.cs`, the validation requirement is `!string.IsNullOrWhiteSpace(password)`. There are **no artificial minimum length or complexity restrictions** that reject password `123`.
3. **Password Setup Result:** **ACCEPTED**. The canonical password `123` generates a valid standard PBKDF2 hash and successfully authenticates against `VerifyPassword`.

### Frontend Angular Presentation Requirement
1. In Angular [`login.component.html`](file:///z:/ERP/Frontend/Web/src/app/features/saas/public/login/login.component.html), the login field is configured as:
   ```html
   <label for="email">Work Email</label>
   <input pInputText id="email" type="email" [(ngModel)]="email" name="email" placeholder="admin@platform.local" />
   ```
2. The browser HTML5 form validation and backend `UserService.LoginAsync` require a valid email format matching the registered user's email (`user.Email`).
3. Therefore:
   - For **Browser Login UI**: Enter the **Login Email Alias** (e.g., `retail.admin@retail.local`) and Password: **`123`**.
   - For **Backend API / Automated Tests**: Send `{ "email": "retail.admin@retail.local", "password": "123" }`.
   - The logical username `retail.admin` is retained as the audit trail identifier (`CreatedBy` / `UpdatedBy`).

---

## 4. Scope Isolation & Segregation of Duties (SoD)

- **Tenant Isolation:** All Retail test users belong strictly to Tenant ID `10000000-0000-0000-0000-000000000001` (`retail`).
- **Segregation of Duties (SoD) Invariants:**
  1. `retail.po` has permission to Create and Submit Purchase Requisitions and Purchase Orders, but **cannot approve** (`PURCHASE_REQUISITION.APPROVE` and `PURCHASE_ORDER.APPROVE` are denied with HTTP 403 Forbidden).
  2. `retail.approver` has permission to Approve and Reject Purchase Requisitions and Purchase Orders, ensuring distinct requester/creator and approver identities.
  3. `retail.grn` receives products into the Dock staging area (`LOC-REC-01`), but warehouse putaway to permanent racks (`LOC-STK-01`) is strictly performed by `retail.store`.
  4. `retail.sales` handles customer sales orders, shipments, and customer invoicing, but cannot modify warehouse master configurations or bypass warehouse authorization.
  5. `retail.viewer` possesses read-only viewing rights (`*.VIEW`) across all functional areas, with all creation, editing, deletion, and posting endpoints locked out (HTTP 403 Forbidden).
