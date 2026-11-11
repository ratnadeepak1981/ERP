# Report A — Standard Role and Permission Matrix

**Workspace:** `Z:\ERP`  
**Audit Mode:** READ-ONLY  
**Date:** 2026-11-11  
**Auditor:** Antigravity (DeepMind Automated Pair Programmer)

---

## 1. Overview & Architectural Principles

ApexERP enforces a strict boundary between **Platform Administration** (`TenantId = null`) and **Tenant-Scoped Operations** (`TenantId = <guid>`).
The canonical definitions for roles and permissions are established in **Flow B** under:
- `Backend/ERP/Security/Boostrap/Permissions/PermissionCatalogue.cs` (Authoritative Catalogue: 172+ permissions)
- `Backend/ERP/Security/Boostrap/Roles/StandardRoleTemplates.cs` (19 Tenant Standard Roles)
- `Backend/ERP/Security/Boostrap/Roles/PlatformRoleSeed.cs` (5 Platform Administrative Roles)
- `Backend/ERP/Security/Services/SecurityBootstrapService.cs` (Seeding engine for tenant-owned role instances)

---

## 2. Platform Roles (Platform Scope, `TenantId = null`)

Platform roles are seeded into `SecurityDB.Roles` with `TenantId = null` and `IsSystemRole = true`. They govern platform SaaS operations, infrastructure, subscription enforcement, and system auditing.

| Role Name | Scope | Intended Responsibilities | Core Assigned Permissions |
| :--- | :--- | :--- | :--- |
| **Platform Admin** | Platform | Global platform administrator. Manages tenants, provisioning, subscription plans, platform diagnostics, and system security. | `TENANT.VIEW`, `TENANT.CREATE`, `TENANT.EDIT`, `TENANT.DELETE`, `TENANT.MANAGE`, `SUBSCRIPTION.VIEW`, `SUBSCRIPTION.CREATE`, `SUBSCRIPTION.EDIT`, `SUBSCRIPTION.CANCEL`, `AUDIT.VIEW`, `AI.READINESS`, `PLATFORM.*` |
| **Platform Staff** | Platform | Operational platform staff. Handles tenant onboarding assistance, support triage, and read-only diagnostic review. | `TENANT.VIEW`, `SUBSCRIPTION.VIEW`, `AUDIT.VIEW` |
| **Security Manager** | Platform | Platform security and cryptographic officer. Governs RSA key rotations, permission catalogue verification, and token revocation. | `SECURITY.VIEW`, `SECURITY.MANAGE`, `AUDIT.VIEW`, `TENANT.VIEW` |
| **Audit** | Platform | Independent platform compliance officer. Read-only oversight of cross-tenant audit ledgers and security event histories. | `AUDIT.VIEW`, `TENANT.VIEW` |
| **Executive** | Platform | SaaS executive and business intelligence viewer. Accesses platform-wide billing analytics, MRR/ARR metrics, and tenant growth KPIs. | `TENANT.VIEW`, `SUBSCRIPTION.VIEW` |

---

## 3. Standard Tenant Roles (Tenant Scope, `TenantId = <tenantGuid>`)

Flow B defines 19 immutable template specifications in [`StandardRoleTemplates.cs`](file:///z:/ERP/Backend/ERP/Security/Boostrap/Roles/StandardRoleTemplates.cs). During tenant provisioning (Flow A), a dedicated copy of each template is instantiated for the tenant with `TenantId = <tenantGuid>` and `TemplateKey = template.Key`.

| # | Role Template Key | Display Name | Functional Responsibilities | Intended Scope | Key Assigned Permissions |
| :--- | :--- | :--- | :--- | :--- | :--- |
| 1 | `TENANT_ADMIN` | Tenant Administrator | Full administrative authority over the tenant organization, companies, branches, users, roles, operating scopes, and operational master data. Strictly excluded from Platform-only permissions and `PRICE_CARD.OVERRIDE`. | Tenant | `COMPANY.*`, `BRANCH.*`, `USER.*`, `ROLE.*`, `USER_SCOPE.*`, `PRODUCT.*`, `CUSTOMER.*`, `SUPPLIER.*`, `PO.*`, `SO.*`, `GRN.*`, `STOCK_COUNT.*`, `PRICE_CARD.VIEW`, `PRICE_CARD.EDIT` (Excluded: `PRICE_CARD.OVERRIDE`, `TENANT.*`, `SUBSCRIPTION.*`) |
| 2 | `TENANT_AUDITOR` | Tenant Auditor | Read-only compliance and financial inspection role. May inspect all transactions, master data, users, and audit logs without modification authority. | Tenant | `COMPANY.VIEW`, `BRANCH.VIEW`, `PRODUCT.VIEW`, `CUSTOMER.VIEW`, `SUPPLIER.VIEW`, `PO.VIEW`, `SO.VIEW`, `GRN.VIEW`, `STOCK_COUNT.VIEW`, `AUDIT.VIEW` |
| 3 | `MASTER_DATA_MANAGER` | Master Data Manager | Comprehensive management of all operational master data (Products, Categories, Brands, UOMs, Suppliers, Customers, Warehouses, Locations, Price Cards). | Company / Tenant | `PRODUCT.*`, `CATEGORY.*`, `BRAND.*`, `UOM.*`, `CUSTOMER.*`, `SUPPLIER.*`, `WAREHOUSE.*`, `LOCATION.*`, `PRICE_CARD.VIEW`, `PRICE_CARD.EDIT`, `PRICE_CARD.CREATE` |
| 4 | `PRODUCT_CATEGORY_OFFICER` | Product & Category Officer | Maintains product catalog items, categories, brands, batches, and serial master records. | Company / Tenant | `PRODUCT.VIEW`, `PRODUCT.CREATE`, `PRODUCT.EDIT`, `CATEGORY.*`, `BRAND.*`, `BATCH.*` |
| 5 | `UOM_PACKAGING_OFFICER` | UOM & Packaging Officer | Maintains Units of Measure, standard unit conversion matrices, packaging specifications, and barcode/GS1 identifiers. | Tenant | `UOM.VIEW`, `UOM.CREATE`, `UOM.EDIT`, `UOM.CONVERSION.*`, `PACKAGING.*` |
| 6 | `SUPPLIER_CUSTOMER_OFFICER` | Supplier & Customer Officer | Maintains external business entity profiles, trading addresses, credit terms, and contact directories. | Company / Tenant | `CUSTOMER.VIEW`, `CUSTOMER.CREATE`, `CUSTOMER.EDIT`, `SUPPLIER.VIEW`, `SUPPLIER.CREATE`, `SUPPLIER.EDIT`, `ADDRESS.*`, `CONTACT.*` |
| 7 | `PRICING_OFFICER` | Pricing Officer | Maintains price lists, customer tier cards, supplier purchase pricelists, and possesses the sensitive price override capability. | Company / Tenant | `PRICE_CARD.VIEW`, `PRICE_CARD.CREATE`, `PRICE_CARD.EDIT`, `PRICE_CARD.OVERRIDE`, `SUPPLIER_PRICE.*` |
| 8 | `FINANCE_CONFIGURATION_OFFICER` | Finance Configuration Officer | Configures base and transaction currencies, exchange rates, tax codes, tax rate regimes, and payment terms. | Tenant | `CURRENCY.*`, `EXCHANGE_RATE.*`, `TAX_CODE.*`, `PAYMENT_TERMS.*` |
| 9 | `WAREHOUSE_CONFIGURATION_OFFICER` | Warehouse Configuration Officer | Manages physical and logical warehouse structures, zones, aisles, racks, and storage bins. | Branch / Warehouse | `WAREHOUSE.VIEW`, `WAREHOUSE.CREATE`, `WAREHOUSE.EDIT`, `LOCATION.VIEW`, `LOCATION.CREATE`, `LOCATION.EDIT` |
| 10 | `MASTER_DATA_VIEWER` | Master Data Viewer | Read-only lookup access to master data entities for operational reference during document entry. | Tenant | `PRODUCT.VIEW`, `CUSTOMER.VIEW`, `SUPPLIER.VIEW`, `WAREHOUSE.VIEW`, `LOCATION.VIEW`, `UOM.VIEW`, `PRICE_CARD.VIEW`, `CURRENCY.VIEW` |
| 11 | `PROCUREMENT_MANAGER` | Procurement Manager | Full procurement lifecycle management: Requisitions, Requests for Quotation, Purchase Orders, approvals, cancellations, and procurement policy settings. | Company / Branch | `PURCHASE_REQUISITION.*`, `PURCHASE_ORDER.*`, `GRN.*`, `PROCUREMENT.SETTINGS.*` |
| 12 | `PROCUREMENT_OFFICER` | Procurement Officer | Drafts and submits Purchase Requisitions, Purchase Orders, and inspects Goods Receipts. Explicitly blocked from approving POs or Requisitions. | Branch | `PURCHASE_REQUISITION.VIEW`, `PURCHASE_REQUISITION.CREATE`, `PURCHASE_REQUISITION.EDIT`, `PURCHASE_REQUISITION.SUBMIT`, `PURCHASE_ORDER.VIEW`, `PURCHASE_ORDER.CREATE`, `PURCHASE_ORDER.EDIT`, `PURCHASE_ORDER.SUBMIT`, `GRN.VIEW` |
| 13 | `PROCUREMENT_VIEWER` | Procurement Viewer | Read-only inquiry into purchase requisitions, orders, and delivery statuses. | Branch | `PURCHASE_REQUISITION.VIEW`, `PURCHASE_ORDER.VIEW`, `GRN.VIEW` |
| 14 | `INVENTORY_MANAGER` | Inventory Manager | Full inventory control: putaway execution, internal stock transfers, stock issue, and stock count adjustment approvals & posting. | Warehouse / Branch | `INVENTORY.VIEW`, `STOCK_RECEIPT.*`, `PUTAWAY.*`, `TRANSFER.*`, `STOCK_ISSUE.*`, `STOCK_COUNT.*` (including `APPROVE`, `POST`, `CANCEL`) |
| 15 | `INVENTORY_OFFICER` | Inventory Officer | Performs floor operations: creates putaway tasks, records stock receipts, performs stock issues, and records count sheets. Cannot approve or post adjustments. | Warehouse | `INVENTORY.VIEW`, `STOCK_RECEIPT.CREATE`, `PUTAWAY.CREATE`, `PUTAWAY.EXECUTE`, `STOCK_ISSUE.CREATE`, `STOCK_COUNT.VIEW`, `STOCK_COUNT.CREATE`, `STOCK_COUNT.SUBMIT` |
| 16 | `INVENTORY_VIEWER` | Inventory Viewer | Read-only stock balance inquiries, serial tracking, batch expiration inspection, and bin availability lookups. | Warehouse | `INVENTORY.VIEW`, `STOCK_BALANCE.VIEW`, `BATCH.VIEW`, `PUTAWAY.VIEW` |
| 17 | `SALES_MANAGER` | Sales Manager | Full sales order lifecycle management: order approval, confirmation, priority allocation, dispatch authorization, and invoice confirmation. | Company / Branch | `SALES_ORDER.*` (including `CONFIRM`, `APPROVE`), `SALES_SHIPMENT.*` (including `DISPATCH`, `DELIVER`), `SALES_INVOICE.*` (including `CONFIRM`, `CANCEL`) |
| 18 | `SALES_OFFICER` | Sales Officer | Creates and edits sales quotations and orders, and generates draft shipment dispatches. Cannot confirm orders or finalize invoices. | Branch | `SALES_ORDER.VIEW`, `SALES_ORDER.CREATE`, `SALES_ORDER.EDIT`, `SALES_ORDER.SUBMIT`, `SALES_SHIPMENT.VIEW`, `SALES_SHIPMENT.CREATE`, `SALES_INVOICE.VIEW` |
| 19 | `SALES_VIEWER` | Sales Viewer | Read-only order tracking, delivery status lookup, customer sales history, and invoice inquiry. | Branch | `SALES_ORDER.VIEW`, `SALES_SHIPMENT.VIEW`, `SALES_INVOICE.VIEW` |

---

## 4. Customer-Facing Roles

| Role Name | Scope | Intended Responsibilities | Assigned Permissions |
| :--- | :--- | :--- | :--- |
| **Customer** | Customer Portal / B2B Storefront | External customer representative. Can browse catalog prices, place B2B purchase orders, view personal order statuses, and view personal invoices. Strictly blocked from internal ERP workspaces (`workspaceGuard`). | `CUSTOMER_PORTAL.VIEW`, `STOREFRONT.VIEW`, `ORDER.CREATE` |

---

## 5. Scope Boundaries Evaluation

1. **Platform Admin vs. Tenant Admin Separation:**
   - **PASS**: Platform Admin operates exclusively on `PlatformDB` and `SecurityDB` where `TenantId = null`.
   - **PASS**: `TenantAdminRolePermissionSeed.cs` and `StandardRoleTemplates.cs` explicitly exclude platform permissions (`TENANT.CREATE`, `SUBSCRIPTION.MANAGE`, etc.) from Tenant Admin.
2. **Tenant Isolation:**
   - **PASS**: Every operational entity in `DomainDB` and tenant security record in `SecurityDB` enforces `TenantId` filtering via `ICurrentUserContext`.
3. **Company and Branch Scope:**
   - **PASS**: `IUserAccessService` verifies `CanAccessCompanyAsync` and `CanAccessBranchAsync` on scoped transaction endpoints.
