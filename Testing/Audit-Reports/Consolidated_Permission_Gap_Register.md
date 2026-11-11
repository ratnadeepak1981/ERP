# ApexERP — Consolidated Permission & Access Control Gap Register

**Date:** 2026-11-11  
**Audit Scope:** Comprehensive Security, Role, Screen, and Workflow Authorization Review  

---

## 1. Classification Methodology

- **CRITICAL:** Cross-tenant isolation breach, unauthenticated administrative escalation, or destructive security vulnerability.
- **HIGH:** Essential business operation blocked for authorized role, unauthorized workflow state transition (e.g., approval bypass), or complete role omission.
- **MEDIUM:** Incomplete role-to-action mapping, UI controls visible to unauthorized roles leading to 403 upon submission, or missing child route guards.
- **LOW:** Non-blocking usability defect, naming inconsistency, or missing visual indicator.

---

## 2. Consolidated Findings Register

### GAP-AUD-01: Legacy Tenant Admin Seeder Omits User and Role Management Permissions
- **Classification:** **HIGH**
- **Affected Role:** `Tenant Admin` (Legacy Global Seed)
- **Permission Identifiers:** `USER.VIEW`, `USER.CREATE`, `USER.EDIT`, `USER.DELETE`, `USER.ASSIGN_ROLE`, `ROLE.VIEW`, `ROLE.CREATE`, `ROLE.EDIT`, `ROLE.DELETE`
- **Location:**
  - Backend Seeder: `Backend/ERP/Security/Boostrap/Roles/TenantAdminRolePermissionSeed.cs` (lines 20-192)
  - Backend Service: `Backend/ERP/Security/Services/SecurityBootstrapService.cs` (lines 64, 173-194)
- **Root Cause:** When `USER.*` and `ROLE.*` permissions were added to `PermissionCatalogue.cs`, they were included in `StandardRoleTemplates.cs` via `tenantCodes`, but the hardcoded array in `TenantAdminRolePermissionSeed.cs` was not updated.
- **Business Impact:** Any installation relying on legacy fallback seeding leaves Tenant Admin unable to query `/api/User` or assign user roles via backend endpoints (returns HTTP 403 Forbidden).
- **Evidence:** `Select-String` search in `TenantAdminRolePermissionSeed.cs` shows zero occurrences of `USER.` or `ROLE.`.
- **Recommended Minimum Correction:** Add `USER.VIEW`, `USER.CREATE`, `USER.EDIT`, `USER.DELETE`, `USER.ASSIGN_ROLE`, `ROLE.VIEW`, `ROLE.CREATE`, `ROLE.EDIT`, and `ROLE.DELETE` to the `permissionCodes` array in `TenantAdminRolePermissionSeed.cs`.

---

### GAP-AUD-02: Master Data Child Routes Lack Angular Router Guards
- **Classification:** **MEDIUM**
- **Affected Roles:** `MASTER_DATA_VIEWER`, `PROCUREMENT_VIEWER`, `TENANT_AUDITOR`
- **Permission Identifiers:** `PRODUCT.CREATE`, `PRODUCT.EDIT`, `CUSTOMER.CREATE`, `CUSTOMER.EDIT`, `SUPPLIER.CREATE`, `SUPPLIER.EDIT`
- **Location:**
  - Frontend Routing: `Frontend/Web/src/app/features/domain/workspace/workspace.routes.ts` (lines 14-253)
- **Root Cause:** The child routes under `master-data` (`products/new`, `products/:id/edit`, `customers/new`, `customers/:id/edit`, `suppliers/new`, `suppliers/:id/edit`) lack `canActivate: [permissionGuard(...)]`. Only the parent route `/app` has `workspaceGuard`.
- **Business Impact:** A user with read-only permissions can manually type `/app/master-data/products/new` in the browser address bar and load the create/edit component. Although backend API calls are rejected with 403 Forbidden, the frontend does not prevent entry.
- **Evidence:** `workspace.routes.ts` contains 0 instances of `canActivate` on child master data routes.
- **Recommended Minimum Correction:** Attach `permissionGuard` with appropriate create/edit permissions to the `new` and `:id/edit` routes in `workspace.routes.ts`.

---

### GAP-AUD-03: Older Master Data UI Components Do Not Hide Action Buttons for Read-Only Roles
- **Classification:** **MEDIUM**
- **Affected Roles:** `MASTER_DATA_VIEWER`, `TENANT_AUDITOR`
- **Permission Identifiers:** `PRODUCT.CREATE`, `PRODUCT.DELETE`, `CUSTOMER.CREATE`, `CUSTOMER.DELETE`, `SUPPLIER.CREATE`, `SUPPLIER.DELETE`, `CATEGORY.CREATE`, `BRAND.CREATE`, `UOM.CREATE`
- **Location:**
  - Frontend Components:
    - `Frontend/Web/src/app/features/domain/workspace/master-data/products/product-master.component.ts`
    - `Frontend/Web/src/app/features/domain/workspace/master-data/customers/customer-master.component.ts`
    - `Frontend/Web/src/app/features/domain/workspace/master-data/suppliers/supplier-master.component.ts`
    - `Frontend/Web/src/app/features/domain/workspace/master-data/categories-brands/categories-brands.component.ts`
    - `Frontend/Web/src/app/features/domain/workspace/master-data/uom/uom-management.component.ts`
- **Root Cause:** Unlike `warehouse-master.component.ts` and `currency-master.component.ts` which check `authService.hasPermission(...)`, these components render "New", "Edit", and "Delete" buttons unconditionally.
- **Business Impact:** Viewers see clickable create and delete buttons. Clicking them opens forms or triggers requests that fail at the backend with 403 Forbidden, degrading usability.
- **Evidence:** None of the listed components inject `AuthService` or bind button visibility to user permissions.
- **Recommended Minimum Correction:** Inject `AuthService` and bind button visibility (`*ngIf="canCreate()"`) using `authService.hasPermission(...)`.

---

### GAP-AUD-04: Goods Receipt Note (GRN) Component Has Unused `AuthService`
- **Classification:** **LOW**
- **Affected Roles:** `PROCUREMENT_VIEWER`, `TENANT_AUDITOR`
- **Permission Identifiers:** `GRN.CREATE`, `GRN.CONFIRM`
- **Location:**
  - Frontend Component: `Frontend/Web/src/app/features/domain/workspace/procurement/goods-receipt-page.component.ts` (line 115)
  - Frontend Register: `Frontend/Web/src/app/features/domain/workspace/procurement/goods-receipt-register.component.ts`
- **Root Cause:** `AuthService` is injected in `goods-receipt-page.component.ts`, but its permissions are not queried to disable or hide the "Confirm Receipt" action button for view-only users.
- **Business Impact:** A viewer can click "Confirm Receipt", which fails with backend 403 Forbidden.
- **Evidence:** `authService` is injected on line 115 but not referenced in template expressions or action button guards.
- **Recommended Minimum Correction:** Bind `canConfirm()` to `authService.hasPermission('GRN.CONFIRM') || authService.isTenantAdmin()`.

---

### GAP-AUD-05: Missing Sidebar Links for Secondary Master Data Screens in Main Navigation
- **Classification:** **LOW**
- **Affected Roles:** All Tenant Staff Roles
- **Permission Identifiers:** `CATEGORY.VIEW`, `BRAND.VIEW`, `UOM.VIEW`, `PAYMENT_TERM.VIEW`, `TAX_CODE.VIEW`, `EXCHANGE_RATE.VIEW`
- **Location:**
  - Frontend Layout: `Frontend/Web/src/app/layout/tenant-layout/tenant-layout.component.ts` (lines 178-193)
- **Root Cause:** The `getSupplyChainNavItems()` sidebar menu only includes Products, Customers, Suppliers, Warehouses, Price Cards, and Currencies. Categories & Brands, UOM, Payment Terms, Tax Codes, and Exchange Rates are accessible only by navigating through the `Supply Chain Masters` overview card grid (`/app/master-data`).
- **Business Impact:** Users cannot navigate directly to secondary master data screens from the sidebar without first opening the Master Data Overview hub.
- **Evidence:** `tenant-layout.component.ts` lines 184-191 omit routes for `/app/master-data/uom`, `/app/master-data/categories-brands`, `/app/master-data/payment-terms`, and `/app/master-data/tax-codes`.
- **Recommended Minimum Correction:** Add nested sub-items or quick-access links under the Master Data accordion in `tenant-layout.component.ts`.

---

### GAP-AUD-06: Tenant Configuration SaaSDbContext Integration Error in Test Environment
- **Classification:** **MEDIUM**
- **Affected Roles:** `Tenant Admin`
- **Permission Identifiers:** `PROCUREMENT.SETTINGS.VIEW`, `PROCUREMENT.SETTINGS.EDIT`
- **Location:**
  - Backend Controller: `Backend/ERP/API/Features/Saas/TenantProcurementSettingsController.cs` (lines 48, 85)
  - Test Suite: `Backend/ERP/Testing/ERP.SecurityTests/Procurement/ProcurementWorkflowIntegrationTests.cs` (lines 37, 154)
- **Root Cause:** `TenantProcurementSettingsController` queries `_configRepository.GetByTenantIdAsync(tenantId.Value)` which targets `SaaSDbContext`. In the integration test environment, cross-context resolution or missing tenant configuration seed results in HTTP 500 InternalServerError rather than default configuration fallback.
- **Business Impact:** Tenant administrators querying procurement settings in unseeded or test environments encounter 500 errors.
- **Evidence:** `ProcurementWorkflowIntegrationTests` fails on line 37 with `Expected: OK, Actual: InternalServerError`.
- **Recommended Minimum Correction:** Ensure `TenantConfigurationRepository` handles missing records gracefully and verify that `SaaSDbContext` is initialized with default tenant configurations.
