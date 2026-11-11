# ApexERP — User Role & Screen Permission Matrix

**Date:** 2026-11-11  
**Audit Type:** Exhaustive Role, Screen, Action & Endpoint Verification  
**Scope:** Master Data Screens, Procurement Screens, and User Management  

---

## 1. Executive Overview

This matrix documents the capability and permission mapping across standard user roles in ApexERP. It maps each business capability (View, Create, Edit, Delete, Submit, Approve, Release, Confirm, Activate/Deactivate) for every standard role to the authoritative permission identifiers, frontend UI controls, and backend API endpoints.

### Roles Covered
1. **Platform Admin** (Global platform administration)
2. **Tenant Admin** (Tenant-level business, user, and security administration)
3. **Procurement Manager** (Requisition, PO, and GRN lifecycle with approval)
4. **Procurement Officer** (Requisition, PO, and GRN operations without approval)
5. **Procurement Viewer** (Read-only procurement staff)
6. **Master Data Manager** (Full master data write/read administration)
7. **Master Data Viewer** (Read-only master data viewer)
8. **Specialized Master Data Roles** (`PRODUCT_CATEGORY_OFFICER`, `UOM_PACKAGING_OFFICER`, `SUPPLIER_CUSTOMER_OFFICER`, `PRICING_OFFICER`, `FINANCE_CONFIGURATION_OFFICER`, `WAREHOUSE_CONFIGURATION_OFFICER`)

---

## 2. Complete Screen & Action Matrix

### Legend
- **PASS**: Explicitly granted and strictly enforced.
- **DENIED**: Explicitly restricted by least privilege and enforced by backend.
- **N/A**: Action does not exist in business workflow for this entity.
- **GAP**: Inconsistency between frontend visibility and backend authorization.

---

### A. Master Data Screens

| Role | Screen | View | Create | Edit | Delete | Submit | Approve | Release | Confirm | Activate / Deactivate | Backend Enforcement | Frontend UI Controls |
| :--- | :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :--- | :--- |
| **Master Data Manager** | Product Master | PASS | PASS | PASS | PASS | N/A | N/A | N/A | N/A | PASS | `ProductController.cs`<br>`PRODUCT.VIEW`, `PRODUCT.CREATE`, `PRODUCT.EDIT`, `PRODUCT.DELETE` | Full form access, save button enabled, action buttons visible |
| **Master Data Viewer** | Product Master | PASS | DENIED | DENIED | DENIED | N/A | N/A | N/A | N/A | DENIED | `ProductController.cs`<br>Returns HTTP 403 Forbidden on write | **GAP**: Create/Edit buttons visible; route allows direct access |
| **Tenant Admin** | Product Master | PASS | PASS | PASS | PASS | N/A | N/A | N/A | N/A | PASS | `ProductController.cs` | Full access via `isTenantAdmin()` |
| **Procurement Manager** | Product Master | PASS | DENIED | DENIED | DENIED | N/A | N/A | N/A | N/A | DENIED | `ProductController.cs`<br>`PRODUCT.VIEW` | View-only access |
| **Procurement Officer** | Product Master | PASS | DENIED | DENIED | DENIED | N/A | N/A | N/A | N/A | DENIED | `ProductController.cs`<br>`PRODUCT.VIEW` | View-only access |
| **Master Data Manager** | Categories & Brands | PASS | PASS | PASS | PASS | N/A | N/A | N/A | N/A | PASS | `CategoryController.cs`, `BrandController.cs`<br>`CATEGORY.*`, `BRAND.*` | Inline add/edit dialogs enabled |
| **Master Data Viewer** | Categories & Brands | PASS | DENIED | DENIED | DENIED | N/A | N/A | N/A | N/A | DENIED | Returns HTTP 403 Forbidden on write | **GAP**: Action buttons visible in component |
| **Master Data Manager** | Customer Master | PASS | PASS | PASS | PASS | N/A | N/A | N/A | N/A | PASS | `CustomerController.cs`<br>`CUSTOMER.VIEW`, `CUSTOMER.CREATE`, `CUSTOMER.EDIT`, `CUSTOMER.DELETE` | Full customer form and address/contact management |
| **Master Data Viewer** | Customer Master | PASS | DENIED | DENIED | DENIED | N/A | N/A | N/A | N/A | DENIED | `CustomerController.cs`<br>Returns HTTP 403 Forbidden on write | **GAP**: Form and action buttons visible to viewer |
| **Master Data Manager** | Supplier Master | PASS | PASS | PASS | PASS | N/A | N/A | N/A | N/A | PASS | `SupplierController.cs`<br>`SUPPLIER.VIEW`, `SUPPLIER.CREATE`, `SUPPLIER.EDIT`, `SUPPLIER.DELETE` | Full supplier form and address/contact management |
| **Master Data Viewer** | Supplier Master | PASS | DENIED | DENIED | DENIED | N/A | N/A | N/A | N/A | DENIED | `SupplierController.cs`<br>Returns HTTP 403 Forbidden on write | **GAP**: Action buttons visible |
| **Procurement Manager** | Supplier Master | PASS | DENIED | DENIED | DENIED | N/A | N/A | N/A | N/A | DENIED | `SupplierController.cs`<br>`SUPPLIER.VIEW` | View directory |
| **Master Data Manager** | Unit of Measure (UOM) | PASS | PASS | PASS | PASS | N/A | N/A | N/A | N/A | PASS | `UnitOfMeasureController.cs`<br>`UOM.VIEW`, `UOM.CREATE`, `UOM.EDIT`, `UOM.DELETE`, `UOM.CONVERT` | Full UOM and conversion factor maintenance |
| **Master Data Viewer** | Unit of Measure (UOM) | PASS | DENIED | DENIED | DENIED | N/A | N/A | N/A | N/A | DENIED | Returns HTTP 403 Forbidden on write | **GAP**: Add/edit buttons visible |
| **UOM & Packaging Officer** | Unit of Measure (UOM) | PASS | PASS | PASS | PASS | N/A | N/A | N/A | N/A | PASS | `UnitOfMeasureController.cs`<br>`UOM.*` | Authorized for UOM and conversions |
| **Master Data Manager** | Currencies & FX Rates | PASS | PASS | PASS | PASS | N/A | N/A | N/A | N/A | PASS | `CurrencyController.cs`, `ExchangeRateController.cs`<br>`CURRENCY.VIEW`, `CURRENCY.CREATE`, `CURRENCY.EDIT`, `CURRENCY.DELETE` | Full currency and rate maintenance |
| **Master Data Viewer** | Currencies & FX Rates | PASS | DENIED | DENIED | DENIED | N/A | N/A | N/A | N/A | DENIED | Returns HTTP 403 Forbidden on write | PASS: `currency-master.component.ts` checks `CURRENCY.CREATE` and hides buttons |
| **Finance Config Officer** | Currencies & FX Rates | PASS | PASS | PASS | PASS | N/A | N/A | N/A | N/A | PASS | `CurrencyController.cs`, `ExchangeRateController.cs` | Authorized for currencies, tax codes, and payment terms |
| **Master Data Manager** | Payment Terms | PASS | PASS | PASS | PASS | N/A | N/A | N/A | N/A | PASS | `PaymentTermController.cs`<br>`PAYMENT_TERM.*` | Full CRUD |
| **Master Data Viewer** | Payment Terms | PASS | DENIED | DENIED | DENIED | N/A | N/A | N/A | N/A | DENIED | Returns HTTP 403 Forbidden on write | **GAP**: Action buttons visible |
| **Master Data Manager** | Tax Codes | PASS | PASS | PASS | PASS | N/A | N/A | N/A | N/A | PASS | `TaxCodesController.cs`<br>`TAX_CODE.*` | Full CRUD |
| **Master Data Viewer** | Tax Codes | PASS | DENIED | DENIED | DENIED | N/A | N/A | N/A | N/A | DENIED | Returns HTTP 403 Forbidden on write | **GAP**: Action buttons visible |
| **Master Data Manager** | Price Cards | PASS | PASS | PASS | N/A | N/A | N/A | N/A | N/A | PASS | `PriceCardController.cs`<br>`PRICE_CARD.VIEW`, `PRICE_CARD.CREATE`, `PRICE_CARD.EDIT`, `PRICE_CARD.MANAGE`, `PRICE_CARD.OVERRIDE` | PASS: `price-card-master.component.ts` checks permissions |
| **Master Data Viewer** | Price Cards | PASS | DENIED | DENIED | N/A | N/A | N/A | N/A | N/A | DENIED | `PriceCardController.cs`<br>Returns HTTP 403 Forbidden on write | PASS: Create/Edit buttons hidden by `price-card-page.component.ts` |
| **Pricing Officer** | Price Cards | PASS | PASS | PASS | N/A | N/A | N/A | N/A | N/A | PASS | `PriceCardController.cs`<br>`PRICE_CARD.*`, `SUPPLIER_PRICE.*` | Authorized for price cards and customer/supplier assignment |
| **Master Data Manager** | Warehouses & Bins | PASS | PASS | PASS | PASS | N/A | N/A | N/A | N/A | PASS | `WarehouseController.cs`<br>`WAREHOUSE.*`, `LOCATION.*` | PASS: `warehouse-master.component.ts` checks permissions |
| **Master Data Viewer** | Warehouses & Bins | PASS | DENIED | DENIED | DENIED | N/A | N/A | N/A | N/A | DENIED | Returns HTTP 403 Forbidden on write | PASS: Create/Edit buttons hidden by component |
| **Warehouse Config Officer** | Warehouses & Bins | PASS | PASS | PASS | PASS | N/A | N/A | N/A | N/A | PASS | `WarehouseController.cs`<br>`WAREHOUSE.*`, `LOCATION.*` | Authorized for warehouses, zones, and bins |
| **Tenant Admin** | Company & Branches | PASS | PASS | PASS | PASS | N/A | N/A | N/A | N/A | PASS | `CompanyController.cs`, `BranchController.cs`<br>`COMPANY.*`, `BRANCH.*` | Full company setup workspace |
| **Master Data Manager** | Company & Branches | PASS | PASS | PASS | PASS | N/A | N/A | N/A | N/A | PASS | `CompanyController.cs`, `BranchController.cs` | Backend permits; route redirects to `/tenant-admin` |
| **Master Data Viewer** | Company & Branches | PASS | DENIED | DENIED | DENIED | N/A | N/A | N/A | N/A | DENIED | `CompanyController.cs`, `BranchController.cs` | Backend enforces 403; frontend route requires Tenant Admin role |

---

### B. Procurement Screens

| Role | Screen | View | Create | Edit | Delete | Submit | Approve | Release | Confirm | Cancel | Backend Enforcement | Frontend UI Controls |
| :--- | :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :--- | :--- |
| **Procurement Manager** | Purchase Requisition | PASS | PASS | PASS | PASS | PASS | PASS | N/A | N/A | PASS | `PurchaseRequisitionController.cs`<br>`PURCHASE_REQUISITION.*` | Full form, Submit, Approve, Reject, Cancel, Delete buttons enabled |
| **Procurement Officer** | Purchase Requisition | PASS | PASS | PASS | PASS | PASS | **DENIED** | N/A | N/A | PASS | `PurchaseRequisitionController.cs`<br>`PURCHASE_REQUISITION.APPROVE` omitted (HTTP 403) | PASS: Approve/Reject buttons hidden by `canApprove()` check |
| **Procurement Viewer** | Purchase Requisition | PASS | DENIED | DENIED | DENIED | DENIED | DENIED | N/A | N/A | DENIED | `PurchaseRequisitionController.cs`<br>Returns HTTP 403 on write actions | Read-only inspection; action buttons hidden |
| **Tenant Admin** | Purchase Requisition | PASS | PASS | PASS | PASS | PASS | PASS | N/A | N/A | PASS | `PurchaseRequisitionController.cs` | Full access via `isTenantAdmin()` |
| **Procurement Manager** | Purchase Orders | PASS | PASS | PASS | PASS | PASS | PASS | N/A | N/A | PASS | `PurchaseOrderController.cs`<br>`PURCHASE_ORDER.*` | Full PO form, Submit, Approve, Reject, Cancel buttons enabled |
| **Procurement Officer** | Purchase Orders | PASS | PASS | PASS | PASS | PASS | **DENIED** | N/A | N/A | PASS | `PurchaseOrderController.cs`<br>`PURCHASE_ORDER.APPROVE` omitted (HTTP 403) | PASS: Approve/Reject buttons hidden by `canApprove()` check |
| **Procurement Viewer** | Purchase Orders | PASS | DENIED | DENIED | DENIED | DENIED | DENIED | N/A | N/A | DENIED | `PurchaseOrderController.cs`<br>Returns HTTP 403 on write actions | Read-only inspection; all action buttons hidden |
| **Tenant Admin** | Purchase Orders | PASS | PASS | PASS | PASS | PASS | PASS | N/A | N/A | PASS | `PurchaseOrderController.cs` | Full access via `isTenantAdmin()` |
| **Procurement Manager** | Goods Receipt (GRN) | PASS | PASS | PASS | PASS | N/A | N/A | N/A | PASS | PASS | `GoodsReceiptNoteController.cs`<br>`GRN.*` | Full GRN form, batch/serial tracking, confirm receipt enabled |
| **Procurement Officer** | Goods Receipt (GRN) | PASS | PASS | PASS | PASS | N/A | N/A | N/A | PASS | PASS | `GoodsReceiptNoteController.cs`<br>`GRN.*` | Full GRN operations and posting enabled |
| **Procurement Viewer** | Goods Receipt (GRN) | PASS | DENIED | DENIED | DENIED | N/A | N/A | N/A | DENIED | DENIED | `GoodsReceiptNoteController.cs`<br>Returns HTTP 403 on write actions | **GAP**: Create/Confirm buttons visible on frontend |
| **Tenant Admin** | Goods Receipt (GRN) | PASS | PASS | PASS | PASS | N/A | N/A | N/A | PASS | PASS | `GoodsReceiptNoteController.cs` | Full access |

---

### C. User Management Screens (Tenant Admin Workspace)

| Role | Screen | View | Create | Edit | Delete / Deactivate | Assign Role | Assign Scope | Revoke Scope | Backend Enforcement | Frontend UI Controls |
| :--- | :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :--- | :--- |
| **Tenant Admin** | User Accounts (`/tenant-admin/users`) | PASS | PASS | PASS | PASS | PASS | PASS | PASS | `UserController.cs`<br>`USER.VIEW`, `USER.CREATE`, `USER.EDIT`, `USER.DELETE`, `USER.ASSIGN_ROLE` | Full user register, user creation, status toggle, role change, scope navigation |
| **Tenant Admin** | Role Administration (`/tenant-admin/roles`) | PASS | PASS | PASS | PASS | N/A | N/A | N/A | `RoleController.cs`, `RolePermissionController.cs`<br>`ROLE.VIEW`, `ROLE.CREATE`, `ROLE.EDIT`, `ROLE.DELETE` | Custom role register, role editor, permission sync |
| **Tenant Admin** | User Operating Scope (`/tenant-admin/user-scope`) | PASS | PASS | N/A | PASS | N/A | PASS | PASS | `UserController.cs`<br>`USER.VIEW`, `USER.EDIT` (`AssignScope`, `RevokeScope`) | Scope register, cascading company/branch assignment |
| **Platform Admin** | User Accounts (Platform) | PASS | PASS | PASS | PASS | PASS | N/A | N/A | `UserController.cs` (Unscoped) | Platform staff administration |
| **Procurement Roles** | User Accounts | **DENIED** | **DENIED** | **DENIED** | **DENIED** | **DENIED** | **DENIED** | **DENIED** | `UserController.cs`<br>Returns HTTP 403 Forbidden | Blocked by `roleGuard` on `/tenant-admin` |
| **Master Data Roles** | User Accounts | **DENIED** | **DENIED** | **DENIED** | **DENIED** | **DENIED** | **DENIED** | **DENIED** | `UserController.cs`<br>Returns HTTP 403 Forbidden | Blocked by `roleGuard` on `/tenant-admin` |

---

## 3. Scope Enforcement Summary

| Entity | Tenant Isolation Enforcement | Company Scope Enforcement | Branch Scope Enforcement | Evidence |
| :--- | :---: | :---: | :---: | :--- |
| **Product Master** | Global / Tenant | Shared across Companies | Multi-branch applicable | `ProductConfiguration.cs`: TenantId isolated |
| **Customer Master** | Tenant-scoped | Shared across Companies | Multi-branch applicable | `Customer.cs`: TenantId isolated |
| **Supplier Master** | Tenant-scoped | Shared across Companies | Multi-branch applicable | `Supplier.cs`: TenantId isolated |
| **Warehouses** | Tenant-scoped | Scoped to Company | Assigned to Branch | `Warehouse.CompanyId`, `Warehouse.BranchId` verified |
| **Purchase Requisitions** | Tenant-scoped | Validated against acting User Scope | Validated against acting User Scope | `PurchaseRequisitionController.cs`: `ICompanyBranchScopeValidator` |
| **Purchase Orders** | Tenant-scoped | Validated against acting User Scope | Validated against acting User Scope | `PurchaseOrderController.cs`: `ICompanyBranchScopeValidator` |
| **Goods Receipts (GRN)** | Tenant-scoped | Validated against PO Company | Validated against Destination Warehouse Branch | `GoodsReceiptNoteController.cs` |
| **User & Role Accounts** | Tenant-scoped | Bound via `UserRoleScope.CompanyId` | Bound via `UserRoleScope.BranchId` | `UserController.cs`: Tenant validation |
