# Report B — Complete ERP Screen and Action Inventory

**Workspace:** `Z:\ERP`  
**Audit Mode:** READ-ONLY  
**Date:** 2026-11-11  
**Auditor:** Antigravity (DeepMind Automated Pair Programmer)

---

## 1. Executive Summary

This inventory catalogues all **54 unique screens** discovered in the ApexERP Angular application, their routes, backing components, functional modules, and supported actions.

---

## 2. Platform Administration Module (`/platform-admin/*`)

| Screen Name | Route | Component | Supported Actions | Intended Roles |
| :--- | :--- | :--- | :--- | :--- |
| **Platform Dashboard** | `/platform-admin/dashboard` | `PlatformAdminDashboardComponent` | View KPI tiles, view tenant count, view ARR/MRR summaries, navigate to administrative sub-modules. | Platform Admin, Security Manager |
| **Tenants Directory** | `/platform-admin/tenants` | `TenantsComponent` | List all registered tenants, filter by status, search by name/code, view tenant metadata. | Platform Admin, Platform Staff |
| **Tenant Provisioning** | `/platform-admin/tenants/provisioning`<br>`/platform-admin/tenants/:tenantId/provisioning` | `TenantProvisioningComponent` | View pending provisioning queue, trigger Flow A tenant provisioning, view provisioning step results. | Platform Admin |
| **Platform Diagnostics** | `/platform-admin/diagnostics` | `DiagnosticsComponent` | Execute security diagnostics, test RSA key signing, test token issuance, test database connectivity. | Platform Admin, Security Manager |
| **Subscription Plans** | `/platform-admin/subscriptions` | *(Redirects to Dashboard)* | View plans catalog. | Platform Admin |
| **Audit & Compliance** | `/platform-admin/audit` | *(Redirects to Dashboard)* | View platform compliance records. | Platform Admin, Audit |

---

## 3. Platform Operational Staff Module (`/platform-staff/*`)

| Screen Name | Route | Component | Supported Actions | Intended Roles |
| :--- | :--- | :--- | :--- | :--- |
| **Platform Staff Portal** | `/platform-staff` | `PlatformStaffComponent` | Search tenants, view tenant subscription status, assist tenant administrators with onboarding. | Platform Staff, Platform Admin |

---

## 4. Public SaaS & Onboarding Module (`/*`)

| Screen Name | Route | Component | Supported Actions | Intended Roles |
| :--- | :--- | :--- | :--- | :--- |
| **Public Landing Page** | `/` | `HomeComponent` | View product overview, marketing hero, public navigation. | Anonymous / Public |
| **Product Overview** | `/overview` | `OverviewComponent` | View architecture, module capabilities, enterprise feature list. | Anonymous / Public |
| **Subscription Plans** | `/plans` | `PlansComponent` | View available subscription plans, compare limits, select plan for onboarding. | Anonymous / Public |
| **Workspace Directory** | `/directory` | `DirectoryComponent` | Search tenant organizations, resolve workspace subdomain, redirect to tenant portal. | Anonymous / Public |
| **User Sign-In** | `/login` | `LoginComponent` | Authenticate tenant staff, tenant admin, platform admin, or customer. Issue JWT & refresh token. | Anonymous / Public |
| **Tenant Registration** | `/register` | `RegisterComponent` | Self-service organization registration, select plan, choose subdomain, create tenant in `PlatformDB`. | Anonymous / Public |

---

## 5. B2B Customer Portal & Storefront Module

| Screen Name | Route | Component | Supported Actions | Intended Roles |
| :--- | :--- | :--- | :--- | :--- |
| **B2B Storefront** | `/storefront` | `StorefrontComponent` | Browse product catalog, view retail/wholesale pricing, search products by category. | Anonymous / Customer |
| **Customer Order Portal** | `/customer-portal` | `CustomerPortalComponent` | View customer order history, track order dispatch status, download sales invoices. | Customer |

---

## 6. Tenant Administration Module (`/tenant-admin/*`)

| Screen Name | Route | Component | Supported Actions | Intended Roles |
| :--- | :--- | :--- | :--- | :--- |
| **Admin Console** | `/tenant-admin/console` | `TenantAdminComponent` | View organization health, onboarding completion steps, quick access to administrative tools. | Tenant Admin |
| **Company Setup Wizard** | `/tenant-admin/company-setup`<br>`/tenant-admin/companies`<br>`/tenant-admin/branches` | `CompanySetupComponent` | Multi-step company, branch, currency, warehouse, and product initialization wizard. | Tenant Admin |
| **Users Register** | `/tenant-admin/users` | `UserRegisterComponent` | List tenant users, filter by active/inactive, search by username/email, navigate to user form. | Tenant Admin |
| **User Page** | `/tenant-admin/users/new`<br>`/tenant-admin/users/:id`<br>`/tenant-admin/users/:id/edit` | `UserPageComponent` | Create user, view user profile, update email/names, toggle active status, assign/unassign roles. | Tenant Admin |
| **Roles Register** | `/tenant-admin/roles` | `RoleRegisterComponent` | List roles, inspect system vs custom roles, view assigned permission counts, create custom role. | Tenant Admin |
| **Role Page** | `/tenant-admin/roles/new`<br>`/tenant-admin/roles/:id`<br>`/tenant-admin/roles/:id/edit` | `RolePageComponent` | Create custom role, edit description, assign permissions from catalog, unassign permissions. | Tenant Admin |
| **User Scope Register** | `/tenant-admin/user-scope` | `UserScopeRegisterComponent` | List assigned user operating scopes (User $\leftrightarrow$ Role $\leftrightarrow$ Company $\leftrightarrow$ Branch). | Tenant Admin |
| **User Scope Page** | `/tenant-admin/user-scope/new` | `UserScopePageComponent` | Create user operating scope assignment, bind user role to specific company and branch. | Tenant Admin |
| **Permission Catalog** | `/tenant-admin/permissions` | `PermissionCatalogPageComponent` | Browse complete canonical permission catalogue, filter by module/operation, view assigned roles. | Tenant Admin |
| **Legacy User Security** | `/tenant-admin/legacy-security` | `UserSecurityComponent` | Legacy combined tabbed interface for users, roles, and scope maintenance. | Tenant Admin |

---

## 7. Master Data Module (`/app/master-data/*`)

| Screen Name | Route | Component | Supported Actions | Intended Roles |
| :--- | :--- | :--- | :--- | :--- |
| **Master Data Overview** | `/app/master-data` | `MasterDataOverviewComponent` | View entity counts (products, partners, locations), quick navigation cards. | Master Data Manager, Officers, Viewers |
| **Products Register** | `/app/master-data/products` | `ProductMasterComponent` | List products, search by SKU/name, filter by category/brand, view active status, export. | Master Data Manager, Product Category Officer, Viewer |
| **Product Page** | `/app/master-data/products/new`<br>`.../:id`<br>`.../:id/edit` | `ProductPageComponent` | Create product, edit details, assign UOM, assign tax code, assign price, deactivate. | Master Data Manager, Product Category Officer |
| **Customers Register** | `/app/master-data/customers` | `CustomerMasterComponent` | List customers, search by code/name, view credit limits, filter active status. | Master Data Manager, Supplier Customer Officer, Viewer |
| **Customer Page** | `/app/master-data/customers/new`<br>`.../:id`<br>`.../:id/edit` | `CustomerPageComponent` | Create customer, edit profile, configure billing/shipping addresses, configure payment terms. | Master Data Manager, Supplier Customer Officer |
| **Suppliers Register** | `/app/master-data/suppliers` | `SupplierMasterComponent` | List suppliers, search by name/code, view payment terms and contact details. | Master Data Manager, Supplier Customer Officer, Viewer |
| **Supplier Page** | `/app/master-data/suppliers/new`<br>`.../:id`<br>`.../:id/edit` | `SupplierPageComponent` | Create supplier, edit details, assign payment terms, configure trading currency. | Master Data Manager, Supplier Customer Officer |
| **Warehouses Register** | `/app/master-data/warehouses` | `WarehouseMasterComponent` | List warehouses, filter by branch/company, inspect physical address, view zone counts. | Master Data Manager, Warehouse Configuration Officer, Viewer |
| **Warehouse Page** | `/app/master-data/warehouses/new`<br>`.../:id`<br>`.../:id/edit` | `WarehousePageComponent` | Create warehouse, edit details, link to company branch, manage storage zones. | Master Data Manager, Warehouse Configuration Officer |
| **Warehouse Zone Page** | `.../warehouses/:wId/zones/new`<br>`.../zones/:zId/edit`<br>`.../zones/:zId` | `ZonePageComponent` | Create zone, edit zone name/type, manage bin locations within zone. | Master Data Manager, Warehouse Configuration Officer |
| **Location / Bin Page** | `.../locations/new`<br>`.../locations/:lId/edit`<br>`.../locations/:lId` | `LocationPageComponent` | Create bin location, edit aisle/rack/shelf identifiers, configure pick/putaway sequence. | Master Data Manager, Warehouse Configuration Officer |
| **Price Cards Page** | `/app/master-data/price-cards` | `PriceCardPageComponent` | List price cards, create price card, configure tier rules, configure customer overrides. | Pricing Officer, Master Data Manager |
| **Currencies Page** | `/app/master-data/currencies` | `CurrencyPageComponent` | List currencies, set base currency, configure exchange rates. | Finance Configuration Officer, Master Data Manager |

---

## 8. Procurement Module (`/app/procurement/*`)

| Screen Name | Route | Component | Supported Actions | Intended Roles |
| :--- | :--- | :--- | :--- | :--- |
| **Procurement Overview** | `/app/procurement` | `ProcurementOverviewComponent` | View procurement KPIs, monthly spend, pending POs, requisitions awaiting approval. | Procurement Manager, Procurement Officer, Viewer |
| **Purchase Requisitions** | `/app/procurement/purchase-requisitions` | `PurchaseRequisitionRegisterComponent` | List requisitions, filter by status (Draft, Submitted, Approved, Rejected, Cancelled). | Procurement Manager, Procurement Officer, Viewer |
| **Purchase Requisition Form** | `.../purchase-requisitions/new`<br>`.../:id`<br>`.../:id/edit` | `PurchaseRequisitionPageComponent` | Create draft, add item lines, submit for approval, approve, reject, cancel, convert to PO. | Procurement Manager (full + approve), Procurement Officer (create/submit) |
| **Purchase Orders** | `/app/procurement/purchase-orders` | `PurchaseOrderRegisterComponent` | List purchase orders, search by PO number/supplier, filter by status, export. | Procurement Manager, Procurement Officer, Viewer |
| **Purchase Order Form** | `.../purchase-orders/new`<br>`.../:id`<br>`.../:id/edit` | `PurchaseOrderPageComponent` | Create draft PO, select supplier, add lines, apply tax/currency, submit, approve, cancel, print. | Procurement Manager (full + approve), Procurement Officer (create/submit) |

---

## 9. Inventory Module (`/app/inventory/*`)

| Screen Name | Route | Component | Supported Actions | Intended Roles |
| :--- | :--- | :--- | :--- | :--- |
| **Inventory Overview** | `/app/inventory` | `InventoryOverviewComponent` | Stock valuation, total SKUs, low stock warnings, warehouse inventory breakdown. | Inventory Manager, Inventory Officer, Viewer |
| **Stock Receipts (GRN)** | `/app/inventory/stock-receipts` | `StockReceiptRegisterComponent` | List goods receipts, search by GRN number/supplier/PO reference, filter by status. | Inventory Manager, Inventory Officer, Viewer |
| **Stock Receipt Form** | `.../stock-receipts/new`<br>`.../:id` | `StockReceiptPageComponent` | Create GRN against PO, inspect quantities, record batch/serial, confirm receipt, post to stock. | Inventory Manager, Inventory Officer |
| **Store Putaway** | `/app/inventory/store-putaway` | `StorePutawayPageComponent` | View unallocated receipts, generate putaway tasks, assign target bins, confirm putaway. | Inventory Manager, Inventory Officer |
| **Stock Transfers** | `/app/inventory/transfers` | `TransferStockComponent` | Inter-warehouse transfers, inter-bin movements, validate stock balance, execute transfer. | Inventory Manager, Inventory Officer |
| **Stock Count & Adjust** | `/app/inventory/count-adjust` | `CountAdjustComponent` | Create stock count sheet, enter physical counts, calculate variances, submit, approve, post adjustments. | Inventory Manager (full + approve/post), Inventory Officer (create/submit) |
| **Stock Issue Register** | `/app/inventory/issue-stock` | `IssueStockRegisterComponent` | List stock issue documents, filter by department/purpose. | Inventory Manager, Inventory Officer, Viewer |
| **Stock Issue Form** | `.../issue-stock/new`<br>`.../:id` | `IssueStockPageComponent` | Create stock issue document, allocate from bins, confirm deduction from stock. | Inventory Manager, Inventory Officer |

---

## 10. Sales Module (`/app/sales/*`)

| Screen Name | Route | Component | Supported Actions | Intended Roles |
| :--- | :--- | :--- | :--- | :--- |
| **Sales Overview** | `/app/sales` | `SalesOverviewComponent` | View sales KPIs, monthly revenue, orders in progress, pending shipments. | Sales Manager, Sales Officer, Viewer |
| **Sales Orders Register** | `/app/sales/orders` | `SalesOrderRegisterComponent` | List sales orders, search by SO number/customer, filter by status, export. | Sales Manager, Sales Officer, Viewer |
| **Sales Order Form** | `.../sales/orders/new`<br>`.../:id`<br>`.../:id/edit`<br>`.../:id/confirm` | `SalesOrderPageComponent` | Create draft SO, select customer, add line items, apply pricing/discounts, submit, confirm, cancel. | Sales Manager (full + confirm), Sales Officer (create/submit) |
| **Dispatch Notes / Shipments** | `/app/sales/dispatches`<br>`.../dispatches/new`<br>`.../dispatches/:id` | `SalesShipmentRegisterComponent`<br>`SalesShipmentPageComponent` | Create shipment from confirmed SO, pick & pack items, dispatch shipment, record proof of delivery. | Sales Manager, Sales Officer |
| **Sales Invoices** | `/app/sales/invoices`<br>`.../invoices/:id`<br>`.../invoices/:id/edit` | `SalesInvoiceRegisterComponent`<br>`SalesInvoicePageComponent` | Generate sales invoice from shipment, confirm invoice, post to ledger, cancel invoice. | Sales Manager (full + confirm), Sales Officer (view/draft) |

---

## 11. System & Shared Screens

| Screen Name | Route | Component | Supported Actions | Intended Roles |
| :--- | :--- | :--- | :--- | :--- |
| **Tenant Dashboard** | `/app/dashboard` | `DashboardComponent` | Overview cards, recent activity, pending approvals, quick navigation shortcuts. | All Tenant Roles |
| **Component Showcase** | `/app/demonstration` | `DemonstrationComponent` | UI components sandbox, DataTable testing, StatusBadge testing, Form component review. | Tenant Staff, Tenant Admin |
| **Access Denied** | `/access-denied` | `AccessDeniedComponent` | Displays 403 Forbidden feedback, attempted route, missing permissions, back button. | Any Authenticated User |
