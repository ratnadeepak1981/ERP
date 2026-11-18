# ApexERP — RFQ Architecture Inspection & Baseline Verification Report

**Document Reference:** `DOC-INSP-RFQ-2026-001`  
**Target Solution:** ApexERP Procurement & Supporting Architecture  
**Status:** Architecture Inspection & Baseline Verification Report  
**Author:** Antigravity AI (on behalf of ApexERP Architecture & Engineering Team)  
**Date:** November 17, 2026  
**Document File:** `z:\ERP\RFQ-Inspection-Report.md`  

---

## 1. Scope and Purpose of Inspection

This report documents the thorough inspection of the active ApexERP repository to assess its readiness for an upstream **Request for Quotation (RFQ)** capability. 

In strict adherence to project safety rules:
- All findings are derived directly from the active codebase.
- Verified existing behavior is explicitly separated from proposed functionality.
- Gaps, technical debt, and concurrency risks in baseline workflows are transparently identified.
- **Zero source code, database migrations, configuration files, seed scripts, or Git refs were modified.**

---

## 2. Solution Structure & Architectural Boundaries

ApexERP is structured as a **Domain-Driven Modular Monolith** across three independent layers and multiple database contexts:

### 2.1 Solution Directory Structure
- **Backend API & Entrypoint:**
  - `Backend/ERP/API/` — ASP.NET Core 8 Web API host, middleware, controllers, Swagger, and DI composition.
- **Domain & Core Capabilities:**
  - `Backend/ERP/Domain/` — Domain entities, business logic, aggregates, and EF Core persistence configurations (`Confiquarations/`).
  - `Backend/ERP/ERP.Application/` — Application orchestration services, DTOs, setup services, and external interfaces.
  - `Backend/ERP/ERP.Infrastructure/` — Infrastructure implementations (MailKit SMTP, BCL PDF engine, repository implementations).
- **Security & Multi-Tenancy:**
  - `Backend/ERP/Security/` — Identity, credentials, password hashing, JWT asymmetric RSA token engine, permissions catalogue, role templates, and bootstrap services.
  - `Backend/ERP/SaaS/` — Platform administration, tenant provisioning, custom domains, and subscription plans.
- **Frontend Presentation:**
  - `Frontend/Web/` — Angular 18 enterprise client utilizing PrimeNG, Signals, and Reactive Forms.
- **Automated Verification:**
  - `Backend/ERP/Testing/ERP.SecurityTests/` — Comprehensive xUnit integration and unit test suite covering multi-tenancy, permissions, procurement, inventory, and sales.

### 2.2 Database Context Segregation
The system strictly enforces segregation across four distinct database contexts:
1. `DomainDbContext`: Contains all operational master data (Products, Suppliers, UOMs, Currencies) and transactional records (`PurchaseOrders`, `PurchaseRequisitions`, `GoodsReceiptNotes`, `InventoryTransactions`).
2. `SecurityDbContext`: Contains user identities (`Users`), roles (`Roles`), permissions (`Permissions`), and role assignments (`RolePermissions`, `UserRoles`).
3. `SaaSDbContext`: Contains tenant definitions (`Tenants`), tenant domains (`TenantDomains`), and subscriptions (`Subscriptions`).
4. `AIDbContext`: Houses AI audit and prompt logging.

*Architectural Invariant Verified:* There are **zero cross-database foreign keys** in EF Core or SQL Server. Cross-context entities reference one another exclusively by stable GUID identifiers validated at the application service boundary.

---

## 3. Evaluation of Existing Procurement Workflows

### 3.1 Workflow A: Direct PO → GRN

#### 3.1.1 Implementation Mapping
- **Angular Presentation:**
  - Register: `Frontend/Web/src/app/features/domain/workspace/procurement/purchase-order.component.ts`
  - Workspace/Form: `Frontend/Web/src/app/features/domain/workspace/procurement/purchase-order-page.component.ts`
- **Backend Controller:**
  - `Backend/ERP/API/Features/Domain/PurchaseOrderController.cs`
- **Application & Domain Services:**
  - `Backend/ERP/Domain/Features/Procurement/PurchaseOrder/PurchaseOrderService.cs`
  - `Backend/ERP/Domain/Features/Procurement/Approval/ApprovalWorkflowService.cs`
  - `Backend/ERP/Domain/Features/Procurement/GoodsReceiptNote/GoodsReceiptNoteService.cs`
  - `Backend/ERP/Domain/Features/Inventory/InventoryPostingService.cs`
- **Persistence Configurations:**
  - `Backend/ERP/Domain/Infrastructure/Persistence/Confiquarations/PurchaseOrderConfiguration.cs`
  - `Backend/ERP/Domain/Infrastructure/Persistence/Confiquarations/GoodsReceiptNoteConfiguration.cs`

#### 3.1.2 Step-by-Step Functional Evaluation
1. **PO Draft Creation:**
   - Handled by `PurchaseOrderService.CreateOrderAsync()`.
   - Validates supplier existence in tenant scope via `ISupplierRepository`.
   - Resolves pricing and currency via `IPricingResolutionService` and `IPriceCardRepository`.
   - Enforces 4-decimal precision for quantities (`decimal(18,4)`) and 2-decimal precision for prices (`decimal(18,2)`).
   - Generates unique order number per tenant/company (`PO-FLOW-...`).
2. **Simple Approval Lifecycle & Self-Approval Guard:**
   - Handled by `ApprovalWorkflowService.cs`.
   - When submitted, checks tenant procurement settings (`/api/tenants/current/settings/procurement-approval`).
   - If approval is required, transitions to `Submitted`. An authorized user holding `PURCHASE_ORDER.APPROVE` must approve.
   - *Self-Approval Prevention:* If the approver is the original creator (`actorUserId == createdByUserId`), the service rejects approval with an `InvalidOperationException` resulting in `409 Conflict`.
3. **PDF Generation & Email Dispatch:**
   - `PurchaseOrderPdfService.cs` generates standards-compliant ISO 32000-1 (PDF 1.4) binaries using pure .NET BCL (zero external NuGet dependencies).
   - `MailKitSmtpEmailService.cs` constructs MIME messages, negotiates STARTTLS, and dispatches to the supplier's contact email.
   - Frontend provides modal preview with safe Blob URL sanitization and explicit dispatch confirmation.
4. **Goods Receipt Note (GRN) Processing:**
   - `GoodsReceiptNoteService.CreateDraftFromPoAsync()` creates draft GRN with line items locked to the PO unit price.
   - Receiving facility and destination storage bins are selected and cascaded to line items.
   - `ConfirmGrnAsync()` enforces **zero-tolerance over-receipt**: rejects any receipt where `receivedQuantity > outstandingQuantity`.
   - Updates `PurchaseOrderItem.ReceivedQuantity`. When all items are received, PO status transitions to `Fulfilled` (or remains `Approved` for partial receipts).
   - Invokes `InventoryPostingService.PostReceiptAsync()` to create FIFO valuation cost layers and update `InventoryBalance`.
   - Supports Batch and Serial tracking through `ProductBatch` records with manufacturing and expiry dates.

#### 3.1.3 Verification Status
- **Automated Tests:** Fully covered by:
  - `ProcurementWorkflowIntegrationTests.cs:51-140` (PO creation, submit, self-approval block, GM approval, audit history)
  - `GoodsReceiptNoteTests.cs:34-105` (GRN draft, confirmation, FIFO posting, PO fulfillment)
  - `PurchaseOrderPdfTests.cs` (PDF byte stream generation)
  - `SmtpLiveDeliveryTests.cs` (MailKit delivery and PDF attachment verification)
- **Documented User Testing:** Successfully demonstrated in the active demo environment (`PO-2026-6506` with supplier `SUP-RET-001`).

---

### 3.2 Workflow B: PR → PO → GRN

#### 3.2.1 Implementation Mapping
- **Angular Presentation:**
  - Requisition Register: `Frontend/Web/src/app/features/domain/workspace/procurement/purchase-requisition.component.ts`
  - Requisition Form: `Frontend/Web/src/app/features/domain/workspace/procurement/purchase-requisition-page.component.ts`
  - PO Conversion Flow: `purchase-order-page.component.ts:890-975`
- **Backend Controller:**
  - `Backend/ERP/API/Features/Domain/PurchaseRequisitionController.cs`
- **Application & Domain Services:**
  - `Backend/ERP/ERP.Application/Features/Procurement/PurchaseRequisition/PurchaseRequisitionService.cs`
- **Persistence Configurations:**
  - `Backend/ERP/Domain/Infrastructure/Persistence/Confiquarations/PurchaseRequisitionConfiguration.cs`

#### 3.2.2 Step-by-Step Functional Evaluation
1. **Requisition Creation & Approval:**
   - Handled by `PurchaseRequisitionService.CreateRequisitionAsync()`.
   - Records line items with estimated unit prices.
   - Validates products belong to tenant scope.
   - Follows approval lifecycle: `Draft` $\to$ `Submitted` $\to$ `Approved` (or `Rejected` with mandatory remarks).
2. **Conversion from PR to PO:**
   - In `purchase-order-page.component.ts:890-975`, navigating with `?purchaseRequisitionId={id}` loads the approved PR.
   - **Critical Architecture Finding:** The `PurchaseOrder` entity and database table **do not have a `PurchaseRequisitionId` column**.
   - Instead, the relationship is tracked by parsing item notes:
     ```typescript
     notes: `[PR: ${reqNum} Line: ${itemId}]`
     ```
   - The frontend reads all active purchase orders in the branch, parses line notes matching `[PR: ... Line: ...]`, calculates the previously ordered quantity, and computes remaining quantity:
     $$\text{remaining} = \max(0, \text{requestedQuantity} - \text{orderedQuantity})$$
   - Line items are populated with the remaining quantity, and `maxAllowedQuantity` is enforced in the UI grid.
   - The source PR entity in `DomainDbContext` is **not modified**; it remains in `Approved` status permanently.

#### 3.2.3 Verification Status
- **Automated Tests:** Covered by `ProcurementWorkflowIntegrationTests.PurchaseRequisition_CreateSubmitRejectLifecycle_AndBranchScopeEnforcement` and `ProcurementDomainUnitTests.cs`.
- **Code Inspected but Runtime Behavior Not Verified:** PR-to-PO conversion via front-end note parsing.

---

### 3.3 Evaluation of Workflow 4: REQ → PO → GRN vs. PR → PO → GRN

The user requested special investigation into whether REQ-generated POs follow the same business logic as PR-generated POs.

#### Findings from Codebase Inspection:
1. **No Separate `MaterialRequisition` Entity:** Inspection of `Backend/ERP/Domain/` and `DomainDbContext.cs` confirms there is no separate `MaterialRequisition` or `StoreRequisition` database entity.
2. **Requisition Concept Unification:** `PurchaseRequisition` is the single authoritative demand aggregate in ApexERP Procurement.
3. **Operational Usage in Inventory:** In `Frontend/Web/src/app/features/domain/workspace/inventory/stock-transaction-form/`, stock issues support an arbitrary textual `sourceDocumentReference` (placeholder: `'e.g. SO-2026-004, WO-102, or MAINT-REQ-88'`).
4. **Conclusion on REQ vs. PR:** 
   - When a PO is created from a store or shopfloor requisition (REQ), it either uses the `PurchaseRequisition` workflow or enters as a Direct PO referencing the REQ code in its header/line notes.
   - **REQ-generated POs follow the EXACT SAME backend commercial validation as PR-generated POs:** both must pass through `PurchaseOrderService.CreateOrderAsync()` with full supplier validation, UOM checks, price card resolution, and approval policies.

---

### 3.4 Technical Defect & Risk in Existing PR-to-PO Conversion

#### Identified Risk: Concurrent Multi-Order Race Condition
Because remaining PR quantities are calculated client-side in Angular by aggregating notes from existing POs:
- If Buyer A and Buyer B simultaneously open the same approved PR (`Quantity = 100`) in separate browser sessions, both browsers will calculate `remaining = 100`.
- Both buyers can submit a PO for `100`, resulting in a duplicate order of `200` units against a `100`-unit requisition.
- **Architectural Lesson for RFQ:** The RFQ module must **not** rely on client-side text parsing for quotation or award limits. The link between an RFQ, its approved quotations, and generated POs must be recorded as an explicit database aggregate (`RFQPurchaseOrderLinks`) with transactional backend enforcement.

---

## 4. Inspection of Security Bootstrap, Permissions & Roles

### 4.1 Permission Catalogue Structure (`PermissionCatalogue.cs`)
- Located at: `Backend/ERP/Security/Boostrap/Permissions/PermissionCatalogue.cs`.
- Categorizes permissions into `Platform` and `Tenant`.
- **Naming Convention Verified:** All tenant domain transaction permissions use uppercase resource prefixes followed by operations:
  - `PURCHASE_REQUISITION.VIEW`, `PURCHASE_REQUISITION.CREATE`, `PURCHASE_REQUISITION.APPROVE`
  - `PURCHASE_ORDER.VIEW`, `PURCHASE_ORDER.CREATE`, `PURCHASE_ORDER.APPROVE`
  - `GRN.VIEW`, `GRN.CREATE`, `GRN.CONFIRM`
- Canonical RFQ permissions must follow this exact convention: `RFQ.VIEW`, `RFQ.CREATE`, `RFQ.EDIT`, `RFQ.SUPPLIER.MANAGE`, `RFQ.SEND`, `RFQ.QUOTATION.RECORD`, `RFQ.QUOTATION.VIEW`, `RFQ.COMPARE`, `RFQ.APPROVE`, `RFQ.REJECT`, `RFQ.PO.GENERATE`.

### 4.2 Standard Role Templates (`StandardRoleTemplates.cs`)
- Located at: `Backend/ERP/Security/Boostrap/Roles/StandardRoleTemplates.cs`.
- Currently on `CurrentVersion = 5`.
- Defines versioned templates for standard tenant roles:
  - `TENANT_ADMIN`
  - `PROCUREMENT_MANAGER`
  - `PROCUREMENT_OFFICER`
  - `INVENTORY_MANAGER`, `INVENTORY_OFFICER`, etc.
- **Tenant Admin Permission Assignment Logic (Lines 82-92):**
  ```csharp
  var tenantCodes = PermissionCatalogue.TenantCodes;
  var tenantAdminPermissions = tenantCodes
      .Where(code => !string.Equals(code, "PRICE_CARD.OVERRIDE", StringComparison.OrdinalIgnoreCase))
      .OrderBy(code => code)
      .ToArray();
  ```
  *Key Finding:* `TenantAdmin` permissions are dynamically derived from `PermissionCatalogue.TenantCodes`! Therefore, any new permission registered in `PermissionCatalogue.cs` via `AddTenant(...)` is automatically included in the `TENANT_ADMIN` template definition!

### 4.3 Tenant Upgrade Execution (`SecurityBootstrapService.cs`)
- Located at: `Backend/ERP/Security/Services/SecurityBootstrapService.cs`.
- Handled by `EnsureTenantStandardRolesAsync(Guid tenantId)` and `UpgradeExistingTenantsAsync()`.
- **Startup Execution Verified:** `API/Program.cs` (line 265) executes `await securityBootstrap.UpgradeExistingTenantsAsync()` on **every application startup**.
- **Additive Upgrade Mechanism:**
  1. Checks if `existingRole.TemplateVersion.Value < template.Version`.
  2. If behind, it reads the tenant role's assigned permissions and additively inserts missing permissions:
     ```csharp
     if (!assignedPermissionIds.Add(permission.Id)) continue;
     await _repository.AddRolePermissionAsync(new RolePermission { ... });
     ```
  3. Updates `existingRole.TemplateVersion = template.Version`.
  4. **Never deletes** existing permissions, preserving any bespoke permissions granted by tenant administrators.
- **Edge Case Verified:** If a tenant created a bespoke custom role with the name "Tenant Admin" without a `TemplateKey`, `EnsureTenantStandardRolesAsync` skips it and logs a conflict to protect user data from being overwritten.

---

## 5. Inspection of User and Supplier Seeding Architecture

### 5.1 Internal User Seeding (`TenantUserSetupService.cs`)
- Located at: `Backend/ERP/Security/Services/TenantUserSetupService.cs`.
- Method `BuildDefaultDemoUserSpecs()` generates standardized accounts using tenant short codes:
  - Tenant Admin: `{cleanCode}ADM` (e.g. `APXTADM`, email: `apxtadm@apxt.local`)
  - Procurement Manager: `{cleanCode}PURMGR` (e.g. `APXTPURMGR`, email: `apxtpurmgr@apxt.local`)
  - Procurement Officer: `{cleanCode}PURSTF` (e.g. `APXTPURSTF`, email: `apxtpurstf@apxt.local`)
- Default password for demo seeding is `"123"`.
- *Finding:* Internal users belong strictly to `SecurityDbContext` and are associated with a tenant and branch.

### 5.2 Supplier Master Data Seeding (`TenantMasterDataSetupService.cs`)
- Located at: `Backend/ERP/ERP.Application/Features/Setup/TenantMasterDataSetupService.cs`.
- Method `SeedSuppliersAsync()` parses industry JSON profiles (`retail.json`, `manufacturing.json`).
- Creates suppliers in `DomainDbContext.Suppliers` with `SupplierCode`, `Name`, `ContactPerson`, `Email`, `Phone`, `Address`, `PaymentTerms`, and `CurrencyId`.
- In the active demo environment, `SUP-RET-001` (`Lanka Retail Distributors (Pvt) Ltd`) has contact email configured as `ratnadeepak1981@gmail.com`.
- *Finding:* External suppliers belong strictly to `DomainDbContext.Suppliers`. They do not possess user accounts or passwords in `SecurityDbContext`.

---

## 6. Inspection of External Access & Supplier Invitation Patterns

### 6.1 Authentication Middleware (`TenantHostConsistencyMiddleware.cs`)
- Located at: `Backend/ERP/API/Common/TenantResolution/TenantHostConsistencyMiddleware.cs`.
- Checks endpoint metadata:
  ```csharp
  bool allowAnonymous = endpoint?.Metadata.GetMetadata<IAllowAnonymous>() != null;
  ```
- If an endpoint is marked with `[AllowAnonymous]`, it bypasses the JWT bearer requirement, allowing external callers to access public endpoints.

### 6.2 Comparison of Supplier Invitation Approaches
1. **Portal Account Approach (Rejected):**
   - Provisioning login credentials in `SecurityDbContext` for external suppliers introduces heavy user management overhead, password resets, account lifecycles, and risk of external users probing internal endpoints.
2. **Cryptographic Secure-Link Token Approach (Recommended):**
   - Each supplier invited to an RFQ receives a unique 256-bit cryptographically secure token.
   - The token hash (`SHA-256`) is stored on `RFQSuppliers.TokenHash` with an expiration date equal to `RFQ.ClosingDate`.
   - The supplier accesses a dedicated external submission page (`[AllowAnonymous]`) using the link:
     `https://tenant.domain.com/public/rfq-submit?rfqId={id}&supplierId={sid}&token={secret}`
   - The backend validates the token hash and closing date before serving RFQ lines or accepting bids.
   - Completely isolates suppliers from internal ERP data and other bidders.

---

## 7. Baseline Verification Status Matrix

| Component / Workflow | Actual Code Location | Automated Test Evidence | Operational Verification | Findings / Notes |
| :--- | :--- | :--- | :--- | :--- |
| **Direct PO Draft** | `PurchaseOrderService.cs` | `ProcurementWorkflowIntegrationTests.cs` | Verified in Demo (`PO-2026-6506`) | Fully functional. |
| **PO Simple Approval** | `ApprovalWorkflowService.cs` | `ProcurementWorkflowIntegrationTests.cs` | Verified in Demo | Self-approval blocked with 409 Conflict. |
| **PO PDF Engine** | `PurchaseOrderPdfService.cs` | `PurchaseOrderPdfTests.cs` | Verified in Demo Preview | Pure .NET BCL implementation. |
| **PO SMTP Email** | `MailKitSmtpEmailService.cs` | `SmtpLiveDeliveryTests.cs` | Verified live email dispatch | STARTTLS on port 587. |
| **PO → GRN Receiving** | `GoodsReceiptNoteService.cs` | `GoodsReceiptNoteTests.cs` | Verified in Demo | Zero over-receipt enforced. |
| **GRN Inventory Posting**| `InventoryPostingService.cs` | `GoodsReceiptNoteTests.cs` | Verified in Demo | FIFO layers created. |
| **GRN Batch Tracking** | `goods-receipt-page.component.ts`| `GoodsReceiptNoteTests.cs` | Verified in Demo | ProductBatch records created. |
| **PR Lifecycle** | `PurchaseRequisitionService.cs`| `ProcurementWorkflowIntegrationTests.cs` | Inspected | Approved PR remains immutable. |
| **PR-to-PO Conversion** | `purchase-order-page.component.ts`| Inspected | Inspected | Notes-based parsing risk under concurrency. |
| **Security Bootstrap** | `SecurityBootstrapService.cs` | `F1PermissionCatalogueAndStandardRolesTests.cs`| Verified in startup | Additive role upgrades working. |
| **User Seeding** | `TenantUserSetupService.cs` | `TenantUserSetupTests.cs` | Verified | Standardized naming convention. |
| **Supplier Seeding** | `TenantMasterDataSetupService.cs`| `TenantMasterDataSetupTests.cs` | Verified | Master data profile JSON seeding. |

---

## 8. Conclusion of Inspection

The inspection confirms that ApexERP possesses mature, high-quality domain and infrastructure services suitable for supporting an RFQ capability. The proposed RFQ module will strictly reuse existing PO generation, UOM conversion, currency resolution, email dispatch, and approval services without destabilizing working baseline workflows.

---
*End of RFQ Architecture Inspection & Baseline Verification Report.*
