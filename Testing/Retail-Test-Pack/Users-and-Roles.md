# ApexERP — Retail Test Pack: Users, Roles and Credentials (Gate 1 Verification)

## 1. Test User Accounts and Identities

To ensure exact parity between manual browser testing and automated API/integration testing, the following canonical test identities are established.

| Username | Login Email Alias | Intended Responsibility | Actual Role (`Role.Name`) | Template Key | Password Setup Status | Assigned Scope |
|---|---|---|---|---|---|---|
| `retail.admin` | `retail.admin@retail.local` | Tenant Administration & Master Data | `Tenant Admin` | `TENANT_ADMIN` | **`123`** (Verified Accepted) | Full Tenant Scope (All Companies & Branches) |
| `retail.po` | `retail.po@retail.local` | Procurement (PO Creation & Submission) | `Procurement Officer` | `PROCUREMENT_OFFICER` | **`123`** (Verified Accepted) | Company: Lanka Retail Mart, Branch: Main Store |
| `retail.approver` | `retail.approver@retail.local` | Independent PO Approval | `Procurement Manager` | `PROCUREMENT_MANAGER` | **`123`** (Verified Accepted) | Company: Lanka Retail Mart, Branch: Main Store |
| `retail.grn` | `retail.grn@retail.local` | Goods Receipt (GRN Creation & Confirmation) | `Procurement Officer` | `PROCUREMENT_OFFICER` | **`123`** (Verified Accepted) | Company: Lanka Retail Mart, Branch: Main Store |
| `retail.store` | `retail.store@retail.local` | Warehouse, Putaway & Inventory (Stock Issue/Receipt/Transfer/Count) | `Inventory Manager` | `INVENTORY_MANAGER` | **`123`** (Verified Accepted) | Company: Lanka Retail Mart, Branch: Main Store |
| `retail.sales` | `retail.sales@retail.local` | Sales Order Management, Confirmation & Shipment Dispatch | `Sales Manager` | `SALES_MANAGER` | **`123`** (Verified Accepted) | Company: Lanka Retail Mart, Branch: Main Store |

## 2. Password Requirement & Verification Analysis (`123`)

### Backend Authentication Policy Inspection
1. **Password Hashing:** `PasswordService.cs` uses standard PBKDF2 with HMAC-SHA256 (100,000 iterations, 16-byte cryptographically secure salt, 32-byte subkey).
2. **Validation Rules:** In `PasswordService.cs` and `UserService.cs`, the validation requirement is `!string.IsNullOrWhiteSpace(password)`. There are **no artificial minimum length or complexity restrictions** that reject password `123`.
3. **Password Setup Result:** **ACCEPTED**. The requested password `123` generates a valid standard PBKDF2 hash and successfully authenticates against `VerifyPassword`.

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

## 3. Scope Isolation & Segregation of Duties

- **Tenant Isolation:** All Retail test users belong strictly to Tenant ID `10000000-0000-0000-0000-000000000001` (`retail`).
- **Segregation of Duties (SOD):**
  - `retail.po` has permission to Create and Submit Purchase Orders, but **cannot approve** (`PURCHASE_ORDER.APPROVE` is denied).
  - `retail.approver` has permission to Approve Purchase Orders, ensuring distinct submitter and approver identities.
  - Self-approval attempts by `retail.po` are blocked at both UI and backend controller levels.
