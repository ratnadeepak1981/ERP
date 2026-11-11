# Report D — Angular Screen and Permission Mapping

**Workspace:** `Z:\ERP`  
**Audit Mode:** READ-ONLY  
**Date:** 2026-11-11  
**Auditor:** Antigravity (DeepMind Automated Pair Programmer)

---

## 1. Executive Summary

This report maps all Angular routes, navigation menu items, and screen action buttons to their corresponding backend permissions.
Findings are categorized into **MATCH**, **MISMATCH**, or **MISSING**, and audited for security consistency.

---

## 2. Route Guards & Navigation Security Mapping

| Route | Guard Applied | Guard Implementation | Intended Allowed Roles / Perms | Match Status | Audit Result | Findings / Notes |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| `/*` (Public Shell) | None | Anonymous | Public | MATCH | **PASS** | Publicly accessible SaaS pages. |
| `/platform-admin/*` | `roleGuard` | `roleGuard(['Platform Admin', 'Security Manager'])` | Platform Admin, Security Manager | MATCH | **PASS** | Correctly isolates Platform Admin workspace shell. |
| `/platform-staff/*` | `roleGuard` | `roleGuard(['Platform Staff', 'Platform Admin', 'Security Manager'])` | Platform Staff, Platform Admin | MATCH | **PASS** | Correctly isolates Platform Staff shell. |
| `/tenant-admin/*` | `roleGuard` | `roleGuard(['Tenant Admin', 'Platform Admin'])` | Tenant Admin, Platform Admin | MATCH | **PASS** | Correctly isolates Tenant Admin workspace. |
| `/app/*` (ERP Workspace)| `workspaceGuard` | `authGuard` + blocks Customer + blocks unassigned Platform Staff | Tenant Staff, Tenant Admin | MISMATCH | **GAP FOUND** | `workspaceGuard` checks `isTenantStaff()` which searches for legacy role names `Tenant User`, `Branch User`, `Viewer` instead of the 19 standard Flow B role templates. |
| `/customer-portal/*`| `roleGuard` | `roleGuard(['Customer'])` | Customer | MATCH | **PASS** | Correctly restricts to Customer role. |
| `/storefront/*` | None | Anonymous | Public / Customer | MATCH | **PASS** | Public catalog browsing. |

---

## 3. Menu Navigation Permission Mapping

In [`tenant-layout.component.ts`](file:///z:/ERP/Frontend/Web/src/app/layout/tenant-layout/tenant-layout.component.ts), sidebar navigation items are dynamically filtered using `canView*` helper functions:

| Menu Entry | Route | Frontend Permission Check | Expected Backend Permission | Match Status | Audit Result | Notes |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **Procurement Overview** | `/app/procurement` | `canViewProcurement()` | `PURCHASE_ORDER.VIEW` or `PURCHASE_REQUISITION.VIEW` | MATCH | **PASS** | Evaluates either requisition or PO view permission. |
| **Purchase Requisitions** | `/app/procurement/purchase-requisitions` | `canViewPurchaseRequisitions()` | `PURCHASE_REQUISITION.VIEW` | MATCH | **PASS** | Matches `PURCHASE_REQUISITION.VIEW`. |
| **Purchase Orders** | `/app/procurement/purchase-orders` | `canViewProcurement()` | `PURCHASE_ORDER.VIEW` | MATCH | **PASS** | Matches backend permission. |
| **Sales Overview** | `/app/sales` | `canViewSales()` | `SALES_ORDER.VIEW` | MATCH | **PASS** | Matches backend permission. |
| **Sales Orders** | `/app/sales/orders` | `canViewSales()` | `SALES_ORDER.VIEW` | MATCH | **PASS** | Matches backend permission. |
| **Dispatch Notes** | `/app/sales/dispatches` | `canViewSales()` | `SALES_SHIPMENT.VIEW` | MISMATCH | **GAP FOUND** | Menu displays based on `SALES_ORDER.VIEW` instead of `SALES_SHIPMENT.VIEW`. |
| **Sales Invoices** | `/app/sales/invoices` | `canViewSales()` | `SALES_INVOICE.VIEW` | MISMATCH | **GAP FOUND** | Menu displays based on `SALES_ORDER.VIEW` instead of `SALES_INVOICE.VIEW`. |
| **Master Data** | `/app/master-data` | None (Always visible in staff nav) | `PRODUCT.VIEW`, `CUSTOMER.VIEW`, etc. | MATCH | **PASS** | Master data overview acts as common reference container. |
| **Inventory** | `/app/inventory` | None (Always visible in staff nav) | `INVENTORY.VIEW` | MATCH | **PASS** | Container for inventory sub-items. |

---

## 4. Screen-Level Action & Button Authorization Mapping

| Screen / Component | UI Action / Button | Frontend Permission Check | Expected Backend Permission | Match Status | Audit Result | Notes |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| `PurchaseOrderPageComponent` | "Submit Order" | `hasPermission('PURCHASE_ORDER.SUBMIT')` | `PO.SUBMIT,PURCHASE_ORDER.SUBMIT` | MATCH | **PASS** | Handled correctly with alias tolerance in `AuthService`. |
| `PurchaseOrderPageComponent` | "Approve Order" | `hasPermission('PURCHASE_ORDER.APPROVE')` | `PO.APPROVE,PURCHASE_ORDER.APPROVE` | MATCH | **PASS** | Officer cannot see or click approve button. |
| `PurchaseOrderPageComponent` | "Cancel Order" | `hasPermission('PURCHASE_ORDER.CANCEL')` | `PO.CANCEL,PURCHASE_ORDER.CANCEL` | MATCH | **PASS** | Matches backend cancellation enforcement. |
| `PurchaseRequisitionPageComponent`| "Submit Requisition"| `hasPermission('PURCHASE_REQUISITION.SUBMIT')` | `PURCHASE_REQUISITION.SUBMIT` | MATCH | **PASS** | Matches backend endpoint. |
| `PurchaseRequisitionPageComponent`| "Approve Requisition"| `hasPermission('PURCHASE_REQUISITION.APPROVE')` | `PURCHASE_REQUISITION.APPROVE` | MATCH | **PASS** | Separation of duty enforced. |
| `PurchaseRequisitionPageComponent`| "Cancel Requisition"| `hasPermission('PURCHASE_REQUISITION.CANCEL')` | `PURCHASE_REQUISITION.CANCEL` | MATCH | **PASS** | Matches backend endpoint. |
| `SalesOrderPageComponent` | "Confirm Order" | `hasPermission('SALES_ORDER.CONFIRM')` | `SALES_ORDER.CONFIRM` | MATCH | **PASS** | Confirmation button restricted to Sales Manager. |
| `SalesOrderPageComponent` | "Submit Order" | `hasPermission('SALES_ORDER.SUBMIT')` | `SALES_ORDER.SUBMIT` | MATCH | **PASS** | Matches backend endpoint. |
| `SalesShipmentPageComponent` | "Dispatch Shipment"| `hasPermission('SALES_SHIPMENT.DISPATCH')` | `SALES_SHIPMENT.DISPATCH` | MATCH | **PASS** | Matches backend endpoint. |
| `SalesInvoicePageComponent` | "Confirm Invoice" | `hasPermission('SALES_INVOICE.CONFIRM')` | `SALES_INVOICE.CONFIRM` | MATCH | **PASS** | Matches backend posting permission. |
| `CountAdjustComponent` | "Approve Count" | `hasPermission('STOCK_COUNT.APPROVE')` | `STOCK_COUNT.APPROVE` | MATCH | **PASS** | Matches backend endpoint. |
| `CountAdjustComponent` | "Post Adjustments" | `hasPermission('STOCK_COUNT.POST')` | `STOCK_COUNT.POST` | MATCH | **PASS** | Matches backend ledger posting permission. |
| `PriceCardPageComponent` | "Override Price" | `hasPermission('PRICE_CARD.OVERRIDE')` | `PRICE_CARD.OVERRIDE` | MATCH | **PASS** | Explicitly blocked for Tenant Admin by template; restricted to Pricing Officer. |
| `CompanySetupComponent` | "Create Branch" | None | `BRANCH.CREATE` | MISSING | **GAP FOUND** | `createBranch` in `company.service.ts` writes to `localStorage` instead of calling backend! |
