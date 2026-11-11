# Report C — Backend Controller, API and Service Mapping

**Workspace:** `Z:\ERP`  
**Audit Mode:** READ-ONLY  
**Date:** 2026-11-11  
**Auditor:** Antigravity (DeepMind Automated Pair Programmer)

---

## 1. Executive Summary

An exhaustive scan was conducted across all **51 backend API controllers** and **345 HTTP endpoints** in `Backend/ERP/API`.
Each endpoint was evaluated for:
1. Controller-level and action-level authorization attributes (`[Authorize]`, `[RequirePermission]`, `[AllowAnonymous]`).
2. The permission identifier enforced.
3. The underlying Application Service and repository execution path.
4. Tenant, company, and branch data-scope enforcement.
5. Audit result: **PASS**, **GAP FOUND**, or **UNVERIFIED**.

---

## 2. Core Controller Audit Matrix

### A. Security & User Administration Module

| Controller | Method | Verb | Route | Auth Attribute | Permission Identifier | Application Service | Scope | Audit Result | Findings / Notes |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| `SecurityController` | `Login` | POST | `api/Security/login` | `[AllowAnonymous]` | None (Public) | `SecurityService.AuthenticateAsync` | Platform/Tenant | **PASS** | Authenticates user, issues JWT and refresh cookie. |
| `SecurityController` | `Refresh` | POST | `api/Security/refresh` | `[AllowAnonymous]` | None (Cookie) | `SecurityService.RefreshTokenAsync` | Platform/Tenant | **PASS** | Refreshes expired access tokens. |
| `SecurityController` | `Logout` | POST | `api/Security/logout` | `[Authorize]` | Authenticated User | `SecurityService.RevokeSessionAsync` | User Session | **PASS** | Revokes current active session. |
| `SecurityController` | `GetActiveSessions`| GET | `api/Security/sessions` | `[Authorize]` | Authenticated User | `SecurityService.GetActiveSessionsAsync` | User Session | **PASS** | Inspects user's active sessions. |
| `SecurityController` | `RevokeSession` | DELETE | `api/Security/sessions/{id}`| `[Authorize]` | Authenticated User | `SecurityService.RevokeSessionAsync` | User Session | **PASS** | Terminates specific session. |
| `UserController` | `GetUsers` | GET | `api/User` | `[Authorize]` | `None` *(Missing)* | `IUserService.GetUsersAsync` | Tenant | **GAP FOUND** | Missing `[RequirePermission("USER.VIEW")]`. Any authenticated tenant user can enumerate all users. |
| `UserController` | `GetUserById` | GET | `api/User/{id}` | `[Authorize]` | `None` *(Missing)* | `IUserService.GetUserByIdAsync` | Tenant | **GAP FOUND** | Missing `[RequirePermission("USER.VIEW")]`. |
| `UserController` | `CreateUser` | POST | `api/User` | `[Authorize]` | `None` *(Missing)* | `IUserService.CreateUserAsync` | Tenant | **GAP FOUND** | Missing `[RequirePermission("USER.CREATE")]`. Any authenticated user can POST new tenant users. |
| `UserController` | `UpdateUser` | PUT | `api/User/{id}` | `[Authorize]` | `None` *(Missing)* | `IUserService.UpdateUserAsync` | Tenant | **GAP FOUND** | Missing `[RequirePermission("USER.EDIT")]`. |
| `UserController` | `DeactivateUser` | DELETE | `api/User/{id}` | `[Authorize]` | `None` *(Missing)* | `IUserService.DeactivateUserAsync` | Tenant | **GAP FOUND** | Missing `[RequirePermission("USER.DELETE")]`. |
| `RoleController` | `GetRoles` | GET | `api/Role` | `[Authorize]` | `None` *(Missing)* | `IRoleService.GetRolesAsync` | Tenant | **GAP FOUND** | Missing `[RequirePermission("ROLE.VIEW")]`. |
| `RoleController` | `CreateRole` | POST | `api/Role` | `[Authorize]` | `None` *(Missing)* | `IRoleService.CreateRoleAsync` | Tenant | **GAP FOUND** | Missing `[RequirePermission("ROLE.CREATE")]`. Any authenticated user can create roles. |
| `RoleController` | `UpdateRole` | PUT | `api/Role/{id}` | `[Authorize]` | `None` *(Missing)* | `IRoleService.UpdateRoleAsync` | Tenant | **GAP FOUND** | Missing `[RequirePermission("ROLE.EDIT")]`. |
| `RoleController` | `DeleteRole` | DELETE | `api/Role/{id}` | `[Authorize]` | `None` *(Missing)* | `IRoleService.DeleteRoleAsync` | Tenant | **GAP FOUND** | Missing `[RequirePermission("ROLE.DELETE")]`. |
| `RolePermissionController` | `BatchAssign` | POST | `api/RolePermission/batch` | `[Authorize]` | `None` *(Missing)* | `IRolePermissionService.BatchAssignAsync` | Tenant | **GAP FOUND** | Missing `[RequirePermission("ROLE.EDIT")]`. Critical privilege escalation vector if unprotected. |
| `UserRoleController` | `AssignRole` | POST | `api/UserRole` | `[Authorize]` | `None` *(Missing)* | `IUserRoleService.AssignRoleAsync` | Tenant | **GAP FOUND** | Missing `[RequirePermission("USER.ASSIGN_ROLE")]`. Any user can grant arbitrary roles. |

---

### B. Master Data Module

| Controller | Method | Verb | Route | Auth Attribute | Permission Identifier | Application Service | Scope | Audit Result | Findings / Notes |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| `CompanyController` | `GetCompanies` | GET | `api/companies` | `NONE` *(Missing)* | None | `ICompanyService.GetCompaniesAsync` | Tenant / Company | **GAP FOUND** | Class and method lack `[Authorize]` and `[RequirePermission("COMPANY.VIEW")]`. |
| `CompanyController` | `GetCompany` | GET | `api/companies/{id}` | `NONE` *(Missing)* | None | `ICompanyService.GetCompanyAsync` | Tenant / Company | **GAP FOUND** | Lacks `[Authorize]` and `[RequirePermission("COMPANY.VIEW")]`. |
| `CompanyController` | `CreateCompany` | POST | `api/companies` | `[RequirePermission]` | `COMPANY.CREATE` | `ICompanyService.CreateCompanyAsync` | Tenant | **PASS** | Correctly enforces `COMPANY.CREATE`. |
| `CompanyController` | `UpdateCompany` | PUT | `api/companies/{id}` | `[RequirePermission]` | `COMPANY.EDIT` | `ICompanyService.UpdateCompanyAsync` | Tenant / Company | **PASS** | Correctly enforces `COMPANY.EDIT`. |
| `BranchController` | `GetBranches` | GET | `api/companies/{cId}/branches` | `NONE` *(Missing)* | None | `IBranchService.GetBranchesAsync` | Tenant / Company | **GAP FOUND** | Class and method lack `[Authorize]` and `[RequirePermission("BRANCH.VIEW")]`. |
| `BranchController` | `GetBranch` | GET | `api/companies/{cId}/branches/{id}` | `NONE` *(Missing)* | None | `IBranchService.GetBranchAsync` | Tenant / Branch | **GAP FOUND** | Lacks `[Authorize]` and `[RequirePermission("BRANCH.VIEW")]`. |
| `ProductController` | `GetProducts` | GET | `api/products` | `[RequirePermission]` | `PRODUCT.VIEW` | `IProductService.GetProductsAsync` | Tenant | **PASS** | Properly enforces `PRODUCT.VIEW`. |
| `ProductController` | `CreateProduct` | POST | `api/products` | `[RequirePermission]` | `PRODUCT.CREATE` | `IProductService.CreateProductAsync` | Tenant | **PASS** | Properly enforces `PRODUCT.CREATE`. |
| `ProductController` | `UpdateProduct` | PUT | `api/products/{id}` | `[RequirePermission]` | `PRODUCT.EDIT` | `IProductService.UpdateProductAsync` | Tenant | **PASS** | Properly enforces `PRODUCT.EDIT`. |
| `ProductController` | `DeactivateProduct` | DELETE | `api/products/{id}` | `[RequirePermission]` | `PRODUCT.DELETE` | `IProductService.DeactivateProductAsync` | Tenant | **PASS** | Properly enforces `PRODUCT.DELETE`. |
| `CustomerController` | `GetCustomers` | GET | `api/customers` | `[RequirePermission]` | `CUSTOMER.VIEW` | `ICustomerService.GetCustomersAsync` | Tenant | **PASS** | Properly enforces `CUSTOMER.VIEW`. |
| `CustomerController` | `CreateCustomer` | POST | `api/customers` | `[RequirePermission]` | `CUSTOMER.CREATE` | `ICustomerService.CreateCustomerAsync` | Tenant | **PASS** | Properly enforces `CUSTOMER.CREATE`. |
| `CustomerController` | `UpdateCustomer` | PUT | `api/customers/{id}` | `[RequirePermission]` | `CUSTOMER.EDIT` | `ICustomerService.UpdateCustomerAsync` | Tenant | **PASS** | Properly enforces `CUSTOMER.EDIT`. |
| `SupplierController` | `GetSuppliers` | GET | `api/suppliers` | `[RequirePermission]` | `SUPPLIER.VIEW` | `ISupplierService.GetSuppliersAsync` | Tenant | **PASS** | Properly enforces `SUPPLIER.VIEW`. |
| `SupplierController` | `CreateSupplier` | POST | `api/suppliers` | `[RequirePermission]` | `SUPPLIER.CREATE` | `ISupplierService.CreateSupplierAsync` | Tenant | **PASS** | Properly enforces `SUPPLIER.CREATE`. |
| `SupplierController` | `UpdateSupplier` | PUT | `api/suppliers/{id}` | `[RequirePermission]` | `SUPPLIER.EDIT` | `ISupplierService.UpdateSupplierAsync` | Tenant | **PASS** | Properly enforces `SUPPLIER.EDIT`. |
| `WarehouseController`| `GetWarehouses` | GET | `api/warehouses` | `[RequirePermission]` | `WAREHOUSE.VIEW` | `IWarehouseService.GetWarehousesAsync` | Tenant / Branch | **PASS** | Properly enforces `WAREHOUSE.VIEW`. |
| `WarehouseController`| `CreateWarehouse` | POST | `api/warehouses` | `[RequirePermission]` | `WAREHOUSE.CREATE` | `IWarehouseService.CreateWarehouseAsync`| Tenant / Branch | **PASS** | Properly enforces `WAREHOUSE.CREATE`. |
| `LocationController` | `GetLocations` | GET | `api/locations` | `[RequirePermission]` | `LOCATION.VIEW` | `ILocationService.GetLocationsAsync` | Tenant / Warehouse | **PASS** | Properly enforces `LOCATION.VIEW`. |
| `LocationController` | `CreateLocation` | POST | `api/locations` | `[RequirePermission]` | `LOCATION.CREATE` | `ILocationService.CreateLocationAsync` | Tenant / Warehouse | **PASS** | Properly enforces `LOCATION.CREATE`. |
| `PriceCardController`| `GetPriceCards` | GET | `api/price-cards` | `[RequirePermission]` | `PRICE_CARD.VIEW` | `IPriceCardService.GetPriceCardsAsync` | Tenant | **PASS** | Properly enforces `PRICE_CARD.VIEW`. |
| `PriceCardController`| `CreatePriceCard`| POST | `api/price-cards` | `[RequirePermission]` | `PRICE_CARD.CREATE` | `IPriceCardService.CreatePriceCardAsync`| Tenant | **PASS** | Properly enforces `PRICE_CARD.CREATE`. |
| `PriceCardController`| `OverridePrice` | POST | `api/price-cards/override` | `[RequirePermission]` | `PRICE_CARD.OVERRIDE`| `IPriceCardService.ApplyOverrideAsync` | Tenant | **PASS** | Sensitive action correctly restricted to `PRICE_CARD.OVERRIDE`. |
| `CurrencyController` | `GetCurrencies` | GET | `api/currencies` | `[RequirePermission]` | `CURRENCY.VIEW` | `ICurrencyService.GetCurrenciesAsync` | Tenant | **PASS** | Properly enforces `CURRENCY.VIEW`. |

---

### C. Procurement Module

| Controller | Method | Verb | Route | Auth Attribute | Permission Identifier | Application Service | Scope | Audit Result | Findings / Notes |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| `PurchaseRequisitionController` | `GetRequisitions` | GET | `api/purchase-requisitions` | `[RequirePermission]` | `PURCHASE_REQUISITION.VIEW` | `IPurchaseRequisitionService.GetRequisitionsAsync` | Tenant / Company | **PASS** | Properly enforces `PURCHASE_REQUISITION.VIEW`. |
| `PurchaseRequisitionController` | `CreateRequisition` | POST | `api/purchase-requisitions` | `[RequirePermission]` | `PURCHASE_REQUISITION.CREATE` | `IPurchaseRequisitionService.CreateRequisitionAsync` | Tenant / Company | **PASS** | Properly enforces `PURCHASE_REQUISITION.CREATE`. |
| `PurchaseRequisitionController` | `SubmitRequisition` | POST | `.../{id}/submit` | `[RequirePermission]` | `PURCHASE_REQUISITION.SUBMIT` | `IPurchaseRequisitionService.SubmitRequisitionAsync` | Tenant / Company | **PASS** | Proper transition check. |
| `PurchaseRequisitionController` | `ApproveRequisition`| POST | `.../{id}/approve` | `[RequirePermission]` | `PURCHASE_REQUISITION.APPROVE` | `IPurchaseRequisitionService.ApproveRequisitionAsync` | Tenant / Company | **PASS** | Proper separation of duty (Manager only). |
| `PurchaseRequisitionController` | `CancelRequisition` | POST | `.../{id}/cancel` | `[RequirePermission]` | `PURCHASE_REQUISITION.CANCEL` | `IPurchaseRequisitionService.CancelRequisitionAsync` | Tenant / Company | **PASS** | Proper cancellation check. |
| `PurchaseOrderController` | `GetPurchaseOrders` | GET | `api/purchase-orders` | `[RequirePermission]` | `PO.VIEW,PURCHASE_ORDER.VIEW` | `IPurchaseOrderService.GetPurchaseOrdersAsync` | Tenant / Company / Branch | **PASS** | Uses alias-tolerant attribute. |
| `PurchaseOrderController` | `CreatePurchaseOrder`| POST | `api/purchase-orders` | `[RequirePermission]` | `PO.CREATE,PURCHASE_ORDER.CREATE` | `IPurchaseOrderService.CreatePurchaseOrderAsync` | Tenant / Company / Branch | **PASS** | Proper creation enforcement. |
| `PurchaseOrderController` | `SubmitPurchaseOrder`| POST | `.../{id}/submit` | `[RequirePermission]` | `PO.SUBMIT,PURCHASE_ORDER.SUBMIT` | `IPurchaseOrderService.SubmitPurchaseOrderAsync` | Tenant / Branch | **PASS** | Proper submit workflow check. |
| `PurchaseOrderController` | `ApprovePurchaseOrder`| POST | `.../{id}/approve` | `[RequirePermission]` | `PO.APPROVE,PURCHASE_ORDER.APPROVE` | `IPurchaseOrderService.ApprovePurchaseOrderAsync` | Tenant / Branch | **PASS** | Separation of duty: Officer cannot approve. |
| `PurchaseOrderController` | `CancelPurchaseOrder` | POST | `.../{id}/cancel` | `[RequirePermission]` | `PO.CANCEL,PURCHASE_ORDER.CANCEL` | `IPurchaseOrderService.CancelPurchaseOrderAsync` | Tenant / Branch | **PASS** | Proper cancel enforcement. |

---

### D. Inventory Module

| Controller | Method | Verb | Route | Auth Attribute | Permission Identifier | Application Service | Scope | Audit Result | Findings / Notes |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| `GoodsReceiptNoteController` | `GetGrns` | GET | `api/inventory/grn` | `[RequirePermission]` | `GRN.VIEW` | `IGoodsReceiptNoteService.GetGrnsAsync` | Tenant / Warehouse | **PASS** | Proper `GRN.VIEW`. |
| `GoodsReceiptNoteController` | `CreateGrn` | POST | `api/inventory/grn` | `[RequirePermission]` | `GRN.CREATE` | `IGoodsReceiptNoteService.CreateGrnAsync` | Tenant / Warehouse | **PASS** | Proper `GRN.CREATE`. |
| `GoodsReceiptNoteController` | `ConfirmGrn` | POST | `.../{id}/confirm` | `[RequirePermission]` | `GRN.CONFIRM` | `IGoodsReceiptNoteService.ConfirmGrnAsync` | Tenant / Warehouse | **PASS** | Inventory posting event. |
| `StorePutawayController` | `GetPutawayTasks` | GET | `api/inventory/putaway` | `[RequirePermission]` | `PUTAWAY.VIEW` | `IStorePutawayService.GetTasksAsync` | Tenant / Warehouse | **PASS** | Properly decoupled from general inventory. |
| `StorePutawayController` | `ConfirmPutaway` | POST | `.../{id}/confirm` | `[RequirePermission]` | `PUTAWAY.EXECUTE` | `IStorePutawayService.ExecutePutawayAsync` | Tenant / Warehouse | **PASS** | Proper execute permission. |
| `StockTransferController` | `ExecuteTransfer` | POST | `api/inventory/transfers` | `[RequirePermission]` | `TRANSFER.EXECUTE` | `IStockTransferService.TransferStockAsync` | Tenant / Warehouse | **PASS** | Validates balances before movement. |
| `StockCountController` | `GetStockCounts` | GET | `api/inventory/stock-counts` | `[RequirePermission]` | `STOCK_COUNT.VIEW` | `IStockCountService.GetStockCountsAsync` | Tenant / Warehouse | **PASS** | Read-only count sheet view. |
| `StockCountController` | `CreateStockCount` | POST | `api/inventory/stock-counts` | `[RequirePermission]` | `STOCK_COUNT.CREATE`| `IStockCountService.CreateStockCountAsync`| Tenant / Warehouse | **PASS** | Initiates physical count. |
| `StockCountController` | `ApproveStockCount`| POST | `.../{id}/approve` | `[RequirePermission]` | `STOCK_COUNT.APPROVE`| `IStockCountService.ApproveStockCountAsync`| Tenant / Warehouse | **PASS** | Manager only. |
| `StockCountController` | `PostStockCount` | POST | `.../{id}/post` | `[RequirePermission]` | `STOCK_COUNT.POST` | `IStockCountService.PostStockCountAsync` | Tenant / Warehouse | **PASS** | Immutability: creates inventory ledger movements. |
| `StockIssueController` | `IssueStock` | POST | `api/inventory/stock-issues`| `[RequirePermission]` | `STOCK_ISSUE.CREATE`| `IStockIssueService.IssueStockAsync` | Tenant / Warehouse | **PASS** | Properly enforces `STOCK_ISSUE.CREATE`. |

---

### E. Sales Module

| Controller | Method | Verb | Route | Auth Attribute | Permission Identifier | Application Service | Scope | Audit Result | Findings / Notes |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| `SalesOrderController` | `GetSalesOrders` | GET | `api/sales-orders` | `[RequirePermission]` | `SALES_ORDER.VIEW` | `ISalesOrderService.GetSalesOrdersAsync` | Tenant / Company / Branch | **PASS** | Scoped order query. |
| `SalesOrderController` | `CreateSalesOrder` | POST | `api/sales-orders` | `[RequirePermission]` | `SALES_ORDER.CREATE`| `ISalesOrderService.CreateSalesOrderAsync`| Tenant / Branch | **PASS** | Draft order creation. |
| `SalesOrderController` | `SubmitSalesOrder` | POST | `.../{id}/submit` | `[RequirePermission]` | `SALES_ORDER.SUBMIT`| `ISalesOrderService.SubmitSalesOrderAsync`| Tenant / Branch | **PASS** | Order submission. |
| `SalesOrderController` | `ConfirmSalesOrder`| POST | `.../{id}/confirm` | `[RequirePermission]` | `SALES_ORDER.CONFIRM`| `ISalesOrderService.ConfirmSalesOrderAsync`| Tenant / Branch | **PASS** | Separation of duty: Manager only. |
| `SalesShipmentController` | `CreateShipment` | POST | `api/sales-shipments` | `[RequirePermission]` | `SALES_SHIPMENT.CREATE`| `ISalesShipmentService.CreateShipmentAsync`| Tenant / Warehouse | **PASS** | Generates dispatch note. |
| `SalesShipmentController` | `DispatchShipment`| POST | `.../{id}/dispatch` | `[RequirePermission]` | `SALES_SHIPMENT.DISPATCH`| `ISalesShipmentService.DispatchShipmentAsync`| Tenant / Warehouse | **PASS** | Decrements inventory on dispatch. |
| `SalesInvoiceController` | `CreateInvoice` | POST | `api/sales-invoices` | `[RequirePermission]` | `SALES_INVOICE.CREATE`| `ISalesInvoiceService.CreateInvoiceAsync` | Tenant / Company | **PASS** | Invoice creation against shipment. |
| `SalesInvoiceController` | `ConfirmInvoice` | POST | `.../{id}/confirm` | `[RequirePermission]` | `SALES_INVOICE.CONFIRM`| `ISalesInvoiceService.ConfirmInvoiceAsync`| Tenant / Company | **PASS** | Commercial posting event. |

---

### F. SaaS & Platform Module

| Controller | Method | Verb | Route | Auth Attribute | Permission Identifier | Application Service | Scope | Audit Result | Findings / Notes |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| `TenantController` | `GetCurrentTenantContext`| GET | `api/Tenant/context` | `[AllowAnonymous]` | None | `ITenantContext` | Host resolution | **PASS** | Resolves subdomain/host for public shell. |
| `TenantController` | `TenantRegistrationTest` | POST | `api/Tenant/tenant-registration-test` | `[AllowAnonymous]` | None | `ITenantService.RegisterTenant` | PlatformDB | **PASS** | Self-service registration onboarding endpoint. |
| `TenantController` | `Provision` | POST | `api/Tenant/provision` | `[RequirePermission]` | `TENANT.CREATE` | `ITenantProvisioningService.ProvisionTenant` | Platform Admin | **PASS** | Restricted to `IsPlatformUser` and `TENANT.CREATE`. |
| `TenantController` | `GetAll` | GET | `api/Tenant` | `[RequirePermission]` | `TENANT.VIEW` | `ITenantService.GetTenantListAsync` | Platform Admin | **PASS** | Blocked for non-platform users. |
| `SubscriptionPlanController`| `GetPlans` | GET | `api/SubscriptionPlan` | `[AllowAnonymous]` | None | `ISubscriptionPlanService.GetPlansAsync` | Public | **PASS** | Public pricing tiers. |
