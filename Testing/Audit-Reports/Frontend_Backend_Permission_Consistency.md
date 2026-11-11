# ApexERP — Frontend & Backend Permission Consistency Audit

**Date:** 2026-11-11  
**Audit Scope:** Comparison of Angular Presentation Layer vs. .NET Core Application/API Security Layer  

---

## 1. Executive Summary

This audit evaluates the consistency between frontend UI presentation controls (menu items, route guards, button visibility, form disabled states) and backend API authorization enforcement (`[RequirePermission]`, `[Authorize]`, scope validators).

The backend enforcement is the authoritative security boundary. In all audited controllers, unauthorized operations are rejected with HTTP 401 Unauthorized or HTTP 403 Forbidden. However, several inconsistencies exist in the presentation layer where buttons remain visible or routes are directly accessible to users lacking write permissions.

---

## 2. Screen-by-Screen Consistency Assessment

### 2.1 Master Data Screens

| Screen | Frontend Component | Route Guard | UI Permission Checks | Backend Controller | Backend Authorization | Consistency Status | Finding / Mismatch Description |
| :--- | :--- | :--- | :--- | :--- | :--- | :---: | :--- |
| **Product Master** | `ProductMasterComponent`<br>`ProductPageComponent` | `workspaceGuard` on `/app` (no child guard) | None | `ProductController.cs` | Class: `PRODUCT.VIEW`<br>Post: `PRODUCT.CREATE`<br>Put: `PRODUCT.EDIT`<br>Delete: `PRODUCT.DELETE` | **MISMATCH (Medium)** | "Create Product" and "Edit" buttons are rendered regardless of user permissions. Direct URL access to `/app/master-data/products/new` is permitted by Angular router. Backend returns 403 Forbidden when submitted. |
| **Customer Master** | `CustomerMasterComponent`<br>`CustomerPageComponent` | `workspaceGuard` on `/app` (no child guard) | None | `CustomerController.cs` | Class: `CUSTOMER.VIEW`<br>Post: `CUSTOMER.CREATE`<br>Put: `CUSTOMER.EDIT`<br>Delete: `CUSTOMER.DELETE` | **MISMATCH (Medium)** | Action buttons and edit forms remain visible to read-only roles (`MASTER_DATA_VIEWER`). Backend rejects with 403 Forbidden. |
| **Supplier Master** | `SupplierMasterComponent`<br>`SupplierPageComponent` | `workspaceGuard` on `/app` (no child guard) | None | `SupplierController.cs` | Class: `SUPPLIER.VIEW`<br>Post: `SUPPLIER.CREATE`<br>Put: `SUPPLIER.EDIT`<br>Delete: `SUPPLIER.DELETE` | **MISMATCH (Medium)** | "New Supplier" and "Edit" buttons are visible to read-only users. Backend enforces 403 Forbidden. |
| **Categories & Brands** | `CategoriesBrandsComponent` | `workspaceGuard` on `/app` (no child guard) | None | `CategoryController.cs`<br>`BrandController.cs` | `CATEGORY.VIEW`, `CATEGORY.CREATE`, `CATEGORY.EDIT`<br>`BRAND.VIEW`, `BRAND.CREATE`, `BRAND.EDIT` | **MISMATCH (Medium)** | "Add Category" and "Add Brand" dialog buttons are visible to viewers. Backend rejects unauthorized creation. |
| **Payment Terms** | `PaymentTermsComponent` | `workspaceGuard` on `/app` (no child guard) | None | `PaymentTermController.cs` | Class: `PAYMENT_TERM.VIEW`<br>Post: `PAYMENT_TERM.CREATE`<br>Put: `PAYMENT_TERM.EDIT` | **MISMATCH (Low)** | Create and edit buttons visible without checking `PAYMENT_TERM.CREATE`. Backend enforces 403 Forbidden. |
| **Tax Codes** | `TaxCodesComponent` | `workspaceGuard` on `/app` (no child guard) | None | `TaxCodesController.cs` | Class: `TAX_CODE.VIEW`<br>Post: `TAX_CODE.CREATE`<br>Put: `TAX_CODE.EDIT` | **MISMATCH (Low)** | Create/Edit buttons visible without checking `TAX_CODE.CREATE`. Backend enforces 403 Forbidden. |
| **Currencies** | `CurrencyMasterComponent` | `workspaceGuard` on `/app` | Checked: `CURRENCY.CREATE`, `CURRENCY.EDIT`, `CURRENCY.DELETE` | `CurrencyController.cs` | Class: `CURRENCY.VIEW`<br>Post: `CURRENCY.CREATE`<br>Put: `CURRENCY.EDIT`<br>Delete: `CURRENCY.DELETE` | **CONSISTENT (Pass)** | Frontend accurately checks permissions via `AuthService` and disables/hides write buttons for viewers. |
| **Exchange Rates** | `ExchangeRateMasterComponent` | `workspaceGuard` on `/app` | Checked: `CURRENCY.CREATE`, `CURRENCY.EDIT` | `ExchangeRateController.cs` | Post: `CURRENCY.CREATE`<br>Put/Delete: `CURRENCY.EDIT` | **CONSISTENT (Pass)** | Add and Edit rate buttons are hidden when user lacks `CURRENCY.CREATE` / `CURRENCY.EDIT`. |
| **Price Cards** | `PriceCardMasterComponent`<br>`PriceCardPageComponent` | `workspaceGuard` on `/app` | Checked: `PRICE_CARD.CREATE`, `PRICE_CARD.EDIT`, `PRICE_CARD.MANAGE` | `PriceCardController.cs` | Class: `PRICE_CARD.VIEW`<br>Post: `PRICE_CARD.CREATE`<br>Put: `PRICE_CARD.EDIT`<br>Status: `PRICE_CARD.MANAGE` | **CONSISTENT (Pass)** | Frontend explicitly verifies permissions, sets form read-only mode, and hides action buttons for viewers. |
| **Unit of Measure (UOM)** | `UomManagementComponent` | `workspaceGuard` on `/app` (no child guard) | None | `UnitOfMeasureController.cs` | Class: `UOM.VIEW`<br>Post: `UOM.CREATE`<br>Put: `UOM.EDIT`<br>Convert: `UOM.CONVERT` | **MISMATCH (Medium)** | "Add Unit" and "Add Conversion" buttons visible to viewers. Backend rejects with 403 Forbidden. |
| **Warehouses & Bins** | `WarehouseMasterComponent`<br>`WarehousePageComponent` | `workspaceGuard` on `/app` | Checked: `WAREHOUSE.CREATE`, `WAREHOUSE.EDIT`, `WAREHOUSE.DELETE`, `LOCATION.*` | `WarehouseController.cs` | Class: `WAREHOUSE.VIEW`<br>Post: `WAREHOUSE.CREATE`<br>Put: `WAREHOUSE.EDIT`<br>Locations: `LOCATION.*` | **CONSISTENT (Pass)** | Frontend component implements comprehensive permission checks and hides mutating actions for viewers. |
| **Company & Branches** | `CompanySetupComponent` | Guarded by redirect to `/tenant-admin/company-setup` | `roleGuard(['Tenant Admin', 'Platform Admin'])` | `CompanyController.cs`<br>`BranchController.cs` | Get: `COMPANY.VIEW`, `BRANCH.VIEW`<br>Post: `COMPANY.CREATE`, `BRANCH.CREATE`<br>Put: `COMPANY.EDIT`, `BRANCH.EDIT` | **CONSISTENT (Pass)** | Strictly protected by role guard at router level and permission checks at API level. |

---

### 2.2 Procurement Screens

| Screen | Frontend Component | Route Guard | UI Permission Checks | Backend Controller | Backend Authorization | Consistency Status | Finding / Mismatch Description |
| :--- | :--- | :--- | :--- | :--- | :--- | :---: | :--- |
| **Purchase Requisition** | `PurchaseRequisitionComponent`<br>`PurchaseRequisitionPageComponent` | `workspaceGuard` on `/app` | Checked: `PURCHASE_REQUISITION.EDIT`, `SUBMIT`, `APPROVE`, `CANCEL`, `DELETE` | `PurchaseRequisitionController.cs` | `PURCHASE_REQUISITION.VIEW`, `CREATE`, `EDIT`, `DELETE`, `SUBMIT`, `APPROVE`, `CANCEL` | **CONSISTENT (Pass)** | Strict consistency. Requisition Officer can create and submit; Approve/Reject buttons are hidden for Officer and visible only to Manager/Admin. Backend strictly rejects unauthorized approval. |
| **Purchase Orders** | `PurchaseOrderComponent`<br>`PurchaseOrderPageComponent` | `workspaceGuard` on `/app` | Checked: `PURCHASE_ORDER.EDIT`, `SUBMIT`, `APPROVE`, `CANCEL`, `DELETE` | `PurchaseOrderController.cs` | `PURCHASE_ORDER.VIEW`, `CREATE`, `EDIT`, `DELETE`, `SUBMIT`, `APPROVE`, `CANCEL` | **CONSISTENT (Pass)** | Strict consistency. PO Officer can create and submit; Approve/Reject buttons are hidden for Officer and visible only to Manager/Admin. Backend rejects unauthorized approval. |
| **Goods Receipt (GRN)** | `GoodsReceiptRegisterComponent`<br>`GoodsReceiptPageComponent` | `workspaceGuard` on `/app` | None (Injected `AuthService` but unused in component logic) | `GoodsReceiptNoteController.cs` | `GRN.VIEW`, `GRN.CREATE`, `GRN.EDIT`, `GRN.DELETE`, `GRN.CONFIRM`, `GRN.CANCEL` | **MISMATCH (Low)** | Create and Confirm buttons remain visible on frontend even if user is a viewer. Backend rejects unauthorized calls with HTTP 403 Forbidden. |

---

### 2.3 User Management Screens (Tenant Admin Workspace)

| Screen | Frontend Component | Route Guard | UI Permission Checks | Backend Controller | Backend Authorization | Consistency Status | Finding / Mismatch Description |
| :--- | :--- | :--- | :--- | :--- | :--- | :---: | :--- |
| **User Accounts** | `UserRegisterComponent`<br>`UserPageComponent` | `roleGuard(['Tenant Admin', 'Platform Admin'])` on `/tenant-admin` | Full user administration (Create, Edit, Toggle, Role Assign, Scope Assign) | `UserController.cs` | `USER.VIEW`, `USER.CREATE`, `USER.EDIT`, `USER.DELETE`, `USER.ASSIGN_ROLE` | **CONSISTENT (Pass)** | Route is restricted to Tenant Admin and Platform Admin. All operations are authorized by backend with tenant isolation. |
| **Role Management** | `RoleRegisterComponent`<br>`RolePageComponent` | `roleGuard(['Tenant Admin', 'Platform Admin'])` on `/tenant-admin` | Role administration and permission assignment | `RoleController.cs`<br>`RolePermissionController.cs` | `ROLE.VIEW`, `ROLE.CREATE`, `ROLE.EDIT`, `ROLE.DELETE` | **CONSISTENT (Pass)** | Frontend allows creating custom roles and editing permissions. Backend validates tenant ownership and prevents assigning platform permissions. |
| **User Scope Governance** | `UserScopeRegisterComponent`<br>`UserScopePageComponent` | `roleGuard(['Tenant Admin', 'Platform Admin'])` on `/tenant-admin` | Cascading company and branch scope assignment | `UserController.cs` (`/scopes`, `/scope`, `/scope/{id}`) | `USER.VIEW`, `USER.EDIT` | **CONSISTENT (Pass)** | Frontend cascades company-to-branch selection; backend validates company and branch tenant boundaries before persisting `UserRoleScope`. |

---

## 3. Root Cause Analysis of Frontend Mismatches

1. **Child Route Protection Absence:**
   - In `workspace.routes.ts`, routes such as `/master-data/products/new` and `/master-data/products/:id/edit` are not guarded by `canActivate: [permissionGuard(['PRODUCT.CREATE'])]`.
   - Any user authenticated into `/app` can enter the URL directly into the browser.
2. **Component Button Guarding Inconsistency:**
   - Newer components (`warehouse-master.component.ts`, `price-card-page.component.ts`, `currency-master.component.ts`, `purchase-requisition-page.component.ts`, `purchase-order-page.component.ts`) inject `AuthService` and conditionally hide action buttons using `authService.hasPermission(...)`.
   - Older master data components (`product-master.component.ts`, `customer-master.component.ts`, `supplier-master.component.ts`, `categories-brands.component.ts`, `uom-management.component.ts`) rely solely on backend rejection and do not check permissions in presentation markup.
