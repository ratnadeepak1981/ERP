# ApexERP — Inventory Replenishment Engine: Draft PR Specification

## 1. Executive Summary and Architecture

This document defines the architecture, data contracts, shortage assessment rules, security controls, and lifecycle invariants for the **Unified Inventory Replenishment Engine** in ApexERP.

ApexERP consolidates all replenishment and shortage-resolution logic into **ONE shared replenishment engine** located in:
- Abstraction: `ERP.Application.Features.Inventory.Replenishment.IInventoryReplenishmentEngine`
- Implementation: `ERP.Application.Features.Inventory.Replenishment.InventoryReplenishmentEngine`
- Controller: `API.Features.Domain.ReplenishmentController`
- Frontend: `InventoryService` (`assessReplenishment` and `createReplenishmentPurchaseRequisition`)

The engine provides a single authoritative entry point for:
1. Evaluating stock availability, active reservations, quarantine holdings, expired batches, and open purchase order supply.
2. Generating genuine, persistent **Draft Purchase Requisitions (Draft PR)** within the existing Procurement module without duplicating procurement workflows, entities, or database tables.

```
+---------------------------------------------------------------------------------------+
|                                    DEMAND SOURCES                                     |
|  - Sales Shipment / Dispatch Shortages                                               |
|  - Sales Orders (Future Scenario A & B)                                               |
|  - Inventory Planning / Min-Max (Future Scenario C)                                  |
|  - Manual Replenishment Assessments                                                   |
+-------------------------------------------+-------------------------------------------+
                                            |
                                            v
+---------------------------------------------------------------------------------------+
|                     SHARED INVENTORY REPLENISHMENT ENGINE                             |
|                 (IInventoryReplenishmentEngine / ReplenishmentController)             |
|                                                                                       |
|  1. Scope Validation (TenantId, CompanyId, BranchId, WarehouseId)                    |
|  2. Stock Availability Assessment:                                                    |
|     * Physical On-Hand Stock                                                          |
|     * Deduct Expired Batches (ExpiryDate <= AsOfDate)                                 |
|     * Deduct Active Allocations (QuantityAllocated)                                   |
|     * Deduct Quarantine Holdings (QuantityQuarantine)                                 |
|     * Add Approved Open PO Supply (po.CompanyId == companyId && po.Status == Approved) |
|     * Anti-Double-Counting Reconciliation (max(balance.OnOrder, po.Outstanding))       |
|  3. Shortage Calculation:                                                             |
|     * Shortage = max(0, RequiredQty - NetAvailableStock)                              |
+-------------------------------------------+-------------------------------------------+
                                            |
                                            v
+---------------------------------------------------------------------------------------+
|                         EXISTING PROCUREMENT MODULE                                    |
|                      (IPurchaseRequisitionService)                                    |
|                                                                                       |
|  * Persistent Requisition Entity: PurchaseRequisitions & PurchaseRequisitionItems     |
|  * Status: Strictly Draft (PurchaseRequisitionStatus.Draft)                          |
|  * Zero Inventory Balance Deduction or Movement                                       |
|  * Zero Automatic PO Generation or Supplier Commitment                                |
|  * Preserves Existing Procurement Approval Matrix and Audit Trail                     |
+---------------------------------------------------------------------------------------+
```

---

## 2. Multi-Tenancy and Organizational Scope Boundaries

The engine enforces strict organizational isolation at every layer:

| Boundary | Enforcement Mechanism | Failure Behavior |
| :--- | :--- | :--- |
| **Tenant** | Filtered by `authResult.TenantId` from the caller's JWT claims. Master data and transactions from other tenants are unreachable. | Request rejected / 401 Unauthorized / 403 Forbidden |
| **Company** | Validated via `ValidateScopeAccess(companyId, branchId)`. PO supply queries strictly filter by `po.CompanyId == companyId`. Warehouses must belong to `companyId`. | 403 Forbidden / KeyNotFoundException |
| **Branch** | Validated via `ValidateScopeAccess(companyId, branchId)`. Requisitions are created under the requesting branch. | 403 Forbidden |
| **Warehouse** | Warehouse must belong to the authorized tenant and company. Inactive or deleted warehouses are rejected. | KeyNotFoundException / InvalidOperationException |

### Critical Bug Resolution: Cross-Company PO Supply Isolation
In legacy planning routines, purchase order queries previously filtered by `po.TenantId == tenantId && po.Status == Approved` without enforcing `po.CompanyId == companyId`. This allowed Company B's purchase order supply to erroneously satisfy Company A's shortages within the same tenant. The shared engine strictly queries:
```csharp
po.TenantId == tenantId &&
po.CompanyId == companyId &&
po.Status == PurchaseOrderStatus.Approved
```
Company boundaries are now strictly defended.

---

## 3. Stock Availability and Shortage Calculation Formula

The engine calculates net availability and line shortages using the following mathematical model:

### Step 1: Eligible On-Hand Stock
Physical stock on hand from `InventoryBalance.QuantityOnHand`, adjusted for batch tracking:
$$\text{EligibleOnHand} = \max(0, \text{QuantityOnHand} - \text{ExpiredBatchQuantity})$$
Where `ExpiredBatchQuantity` is the sum of quantities in `Batches` where `ExpiryDate <= (AsOfDate ?? UtcNow)`.

### Step 2: Uncommitted Available Stock
$$\text{UncommittedStock} = \max(0, \text{EligibleOnHand} - \text{QuantityAllocated} - \text{QuantityQuarantine})$$

### Step 3: Approved Open PO Supply & Anti-Double-Counting
To prevent double counting existing open procurement pipeline:
- Balance on order: `InventoryBalance.QuantityOnOrder`.
- Outstanding approved PO lines:
  $$\text{PoLineOutstanding} = \sum (\text{item.Quantity} - \text{item.QuantityReceived})$$
  for all active line items in `PurchaseOrders` where `Status == Approved`, `CompanyId == companyId`, and `WarehouseId == warehouseId`.
- Reconciled PO supply:
  $$\text{EffectiveQuantityOnOrder} = \max(\text{QuantityOnOrder}, \text{PoLineOutstanding})$$

### Step 4: Net Available Stock
$$\text{NetAvailableStock} = \text{UncommittedStock} + \text{EffectiveQuantityOnOrder}$$

### Step 5: Line Shortage Quantity
For each demand line:
$$\text{ShortageQuantity} = \max(0, \text{RequiredQuantity} - \text{NetAvailableStock})$$
$$\text{HasShortage} = \text{ShortageQuantity} > 0$$

---

## 4. Draft Purchase Requisition Creation Lifecycle & Invariants

When shortages or replenishment needs are confirmed by an authorized user, the engine generates a genuine persistent Draft Purchase Requisition by delegating to `IPurchaseRequisitionService.CreateRequisitionAsync`.

### Invariants:
1. **Status is Strictly Draft**: The created requisition has `PurchaseRequisitionStatus.Draft`.
2. **No Automatic Approval**: The document is never automatically approved. It must traverse the standard procurement approval workflow (`IApprovalWorkflowService`).
3. **No Automatic Purchase Order**: No `PurchaseOrder` or supplier contract is generated.
4. **No Stock Deductions or Allocations**: Zero inventory balances are modified, zero stock is allocated, and zero stock movements (`InventoryTransaction`) are posted.
5. **Traceable Source Document**: The source entity and type (`SalesShipment`, `SalesOrder`, `InventoryPlanning`, or `Manual`) are recorded in the requisition notes and headers.
6. **Line Item Fidelity**: Every item captures `ProductId`, `Quantity`, `UnitOfMeasureId`, `EstimatedUnitPrice`, and `Remarks`.

---

## 4.1 Durable Idempotency, Concurrency, and Deduplication Architecture

To protect against duplicate purchase requisitions arising from network retries, double-clicks, or concurrent user submissions, the engine implements a multi-tier idempotency and atomic deduplication mechanism:

### 1. Client-Supplied Idempotency Key
- The API accepts an optional `IdempotencyKey` property in `CreateReplenishmentPrRequest` or via the standard HTTP request header `X-Idempotency-Key`.
- When supplied, this key uniquely identifies the replenishment attempt.

### 2. Multi-Scope Source Document Validation
- When `SourceType` is `SalesOrder` or `SalesShipment` and a `SourceEntityId` is provided, the engine queries `SalesOrders` to verify that the source document exists, is active, and strictly belongs to the authorized `(TenantId, CompanyId)`.
- Cross-company source manipulation is rejected with `InvalidOperationException`.

### 3. Active Draft PR Detection & Safe Replay
- The engine formats structured tags in `Notes`: `[IdempotencyKey:{key}]` and `[Source:{type}:{id}]`.
- Prior to creating a requisition, it queries `PurchaseRequisitions` within the same `(TenantId, CompanyId)` where `Status != Cancelled`.
- If an existing active PR matches the idempotency key or source entity, the engine aborts duplicate creation and returns the existing requisition result with `IsReused = true`.

### 4. Atomic Database-Level Concurrency Protection
- To guard against simultaneous race conditions, the engine derives a deterministic `RequisitionNumber`:
  - From `IdempotencyKey`: `$"PR-IDEM-{SHA256(key)[..12]}"`
  - From `SourceEntityId`: `$"PR-REPL-{sourceShortId}-A{attempt}"`
- The database enforces uniqueness via the pre-existing unique index:
  ```
  IX_PurchaseRequisitions_TenantId_CompanyId_RequisitionNumber (TenantId, CompanyId, RequisitionNumber)
  ```
- If two parallel requests race, one succeeds at the storage layer while the second encounters a unique constraint violation in `PurchaseRequisitionService`. The replenishment engine intercepts the constraint collision, queries the winning requisition, and safely returns it to the second caller with `IsReused = true`.
- This mechanism is 100% atomic and introduces **zero schema migrations**.

### 5. Cancelled PR Recovery Lifecycle
- A cancelled purchase requisition does **not** permanently lock or block a subsequent replenishment request.
- The active PR lookup filters out `PurchaseRequisitionStatus.Cancelled`.
- When an operation is retried following a cancellation, the attempt counter increments (e.g. `PR-IDEM-...-A2`), generating a distinct requisition number that avoids collision with historical cancelled records.

---

## 4.2 Existing Draft PR Visibility, Progress Indicators, Error Handling, and Navigation (Phase 2)

Phase 2 enhances the user and system lifecycle by exposing active Draft PR status, safe retry semantics, clear progress indicators, and dedicated inventory navigation:

### 1. Existing Draft PR Visibility in Dispatch & Orders
- **Availability Query Enrichment**: `ISalesShipmentService.GetOrderDispatchAvailabilityAsync` inspects `PurchaseRequisitions` in the same `(TenantId, CompanyId)` where `Status != Cancelled` matching the sales order reference tags.
- **DTO Metadata**:
  - `SalesOrderDispatchAvailabilityDto`: surfaces `HasPendingPurchaseRequisitions` (boolean) and `TotalPendingPurchaseRequisitions` (integer count).
  - `SalesOrderDispatchLineAvailabilityDto`: surfaces `HasPendingPurchaseRequisition` (boolean), `PendingPurchaseRequisitionId` (Guid?), `PendingPurchaseRequisitionNumber` (string?), and `PendingPurchaseRequisitionStatus` (string?).
- **User Interface State**:
  - Shortage analysis displays an informative pending banner when open requisitions exist for the order.
  - Line items display a distinct `PR Pending: {number}` warning badge, disable the redundant `Requisition Stock` button, and present an `Inspect PR` button navigating directly to `/app/procurement/purchase-requisitions/{id}`.
- **Strict Accounting Invariant**: Unapproved Draft PRs NEVER decrement shortage quantities or increment available stock. Shortage quantities remain truthful until actual Purchase Orders are approved, received, and posted.
- **Cancelled PR Recovery**: When a requisition is cancelled, subsequent calls to `GetOrderDispatchAvailabilityAsync` automatically clear the pending status, allowing authorized users to generate fresh replenishment requests without permanent lockouts.

### 2. Indeterminate Progress Feedback
- **Measured Progress**: Stock assessment and batch submission display indeterminate progress bars (`.erp-progress-bar-indeterminate`) accompanied by informative status messages.
- **No Fabricated Metrics**: Percentage completion is never arbitrarily faked or simulated client-side.
- **Duplicate Click Prevention**: Action buttons (`Assess Stock`, `Create Draft PR`, modal submission) are disabled while asynchronous operations are in-flight.

### 3. Error Handling, Input Preservation, and Safe Retry
- **Categorized Errors**: Failures are translated into user-friendly error categories (`Validation`, `Authorization`, `Network`, `Conflict`, `Server`) with actionable remediation guidance.
- **State Preservation**: On transient network failures, user selections, checkbox states, and edited replenishment quantities are strictly preserved in client memory.
- **Client Idempotency Key Retention**: The client-generated `idempotencyKey` is retained across retry attempts (`retrySubmission()`), guaranteeing that network retries safely replay against the backend atomic deduplication mechanism without creating duplicate documents.

### 4. Dedicated Inventory Navigation & Standalone Batch Replenishment
- **Route**: Dedicated route `/app/inventory/replenishment`, guarded by `permissionGuard(['INVENTORY.VIEW'])`.
- **Navigation Shell**: Integrated under Inventory navigation in `tenant-layout.component.ts` with label `Stock Replenishment` (`pi pi-sync`) visible to `Inventory Manager`, `Inventory Officer`, and `Tenant Administrator`.
- **Overview Stage**: Added `Stock Replenishment` step in `inventory-overview.component.ts` process flow.
- **Batch Processing Workflow**:
  1. Select target warehouse facility.
  2. Execute multi-item shortage assessment.
  3. Interactive grid displaying net availability, allocations, and shortages with selectable line checkboxes.
  4. Batch confirmation modal summarizing line count and estimated requisition spend.
  5. Single atomic Draft PR generation containing all selected lines with zero inventory movement.

---

## 5. Security and Permission Model

### Permission Catalogue Addition
Added to `Security.Bootstrap.Permissions.PermissionCatalogue`:
- **Code**: `INVENTORY.REPLENISH.CREATE`
- **Name**: `Create Inventory Replenishment Draft PR`
- **Module**: `Inventory`
- **Category**: `Tenant`
- **Description**: `Allows assessing inventory shortages and generating draft purchase requisitions.`

### Standard Role Template Alignment (`StandardRoleTemplates.cs`):
- **`TENANT_ADMIN`**: Automatically inherits `INVENTORY.REPLENISH.CREATE` as a tenant permission.
- **`INVENTORY_MANAGER`**: Explicitly granted `INVENTORY.REPLENISH.CREATE`.
- **`INVENTORY_OFFICER`**: Explicitly granted `INVENTORY.REPLENISH.CREATE`.
- **`PROCUREMENT_MANAGER`**: Granted `INVENTORY.REPLENISH.CREATE` alongside existing PR management permissions.
- **`PROCUREMENT_OFFICER`**: Granted `INVENTORY.REPLENISH.CREATE` alongside existing PR creation permissions.
- **`INVENTORY_VIEWER`**: Denied `INVENTORY.REPLENISH.CREATE` (read-only access to `INVENTORY.VIEW` preserved).

---

## 6. API and Integration Contracts

### Controller: `API.Features.Domain.ReplenishmentController`
Route: `api/companies/{companyId:guid}/branches/{branchId:guid}/inventory/replenishment`

#### Endpoint 1: Stock Shortage Assessment
- **Method**: `POST /assess`
- **Permission**: `INVENTORY.VIEW`
- **Request Body**:
  ```json
  {
    "warehouseId": "9a9a9a9a-9a9a-9a9a-9a9a-9a9a9a9a9a9a",
    "asOfDate": "2026-11-27T00:00:00Z",
    "items": [
      {
        "productId": "44444444-4444-4444-4444-444444444444",
        "requiredQuantity": 50.0,
        "unitOfMeasureId": "33333333-3333-3333-3333-333333333333",
        "remarks": "Dispatch shortage assessment"
      }
    ]
  }
  ```
- **Response**: `ApiResponse<ReplenishmentAssessmentResult>` containing breakdown of `quantityOnHand`, `expiredQuantity`, `eligibleOnHandQuantity`, `quantityAllocated`, `quantityQuarantine`, `quantityOnOrder`, `netAvailableStock`, and `shortageQuantity`.

#### Endpoint 2: Draft Purchase Requisition Creation
- **Method**: `POST /draft-purchase-requisition`
- **Permission**: `INVENTORY.REPLENISH.CREATE`
- **Request Body**:
  ```json
  {
    "warehouseId": "9a9a9a9a-9a9a-9a9a-9a9a-9a9a9a9a9a9a",
    "sourceType": 2,
    "sourceEntityId": "77777777-7777-7777-7777-777777777777",
    "sourceReference": "SO-2026-0001",
    "requiredDate": "2026-12-05T00:00:00Z",
    "notes": "Sales Order SO-2026-0001 replenishment requisition.",
    "items": [
      {
        "productId": "44444444-4444-4444-4444-444444444444",
        "requestedQuantity": 35.0,
        "unitOfMeasureId": "33333333-3333-3333-3333-333333333333",
        "estimatedUnitPrice": 150.0,
        "remarks": "Replenishment for SO-2026-0001"
      }
    ]
  }
  ```
- **Response**: `ApiResponse<DraftPurchaseRequisitionResult>`
  ```json
  {
    "succeeded": true,
    "data": {
      "requisitionId": "f1f1f1f1-f1f1-f1f1-f1f1-f1f1f1f1f1f1",
      "requisitionNumber": "PR-20261127-001",
      "status": "Draft",
      "lineCount": 1,
      "message": "Draft purchase requisition 'PR-20261127-001' created successfully with 1 line item(s). Status is Draft. Zero stock allocated."
    }
  }
  ```

### Sales Dispatch Integration:
`SalesShipmentService.CreateReplenishmentPurchaseRequisitionAsync` was refactored to delegate directly to `IInventoryReplenishmentEngine.CreateDraftPurchaseRequisitionAsync`, eliminating duplicated PR generation logic and ensuring unified audit and status semantics.

---

## 7. Extensibility Provisions for Future Scenarios

The shared replenishment engine is explicitly architected to support future scenarios without requiring schema changes or duplicate engines:

### Scenario A — Sales Order Draft Assessment
- **Workflow**: When a draft corporate Sales Order is created (or converted from quotation), the frontend can call `AssessStockAsync` to verify inventory availability.
- **Extensibility**: Uses `ReplenishmentSourceType.SalesOrder` with `sourceEntityId = salesOrderId`. If shortages exist, user can invoke `CreateDraftPurchaseRequisitionAsync`.

### Scenario B — Direct Corporate Sales Order Replenishment
- **Workflow**: Direct Sales Orders created without quotations follow the exact same assessment and Draft PR pipeline as Scenario A.

### Scenario C — Proactive Replenishment Based on Planning Rules
- **Workflow**: Authorised inventory users evaluate Min/Max, Reorder Level, and Safety Stock rules across products.
- **Extensibility**: Uses `ReplenishmentSourceType.InventoryPlanning`. The calculated deficit (`MaxStock - NetAvailable`) feeds into `CreateDraftPurchaseRequisitionAsync`.

### Scenario D — Inter-Warehouse Stock Transfer
- **Workflow**: Searching alternative warehouses within the same authorized company to recommend stock transfers (`TransferableQuantity`).
- **Extensibility**: The engine contract can be extended with `AssessTransferAvailabilityAsync` and `CreateDraftInventoryTransferAsync`. Transfer receipt posting remains separate from transfer initiation.

### Scenario E — Combined Replenishment
- **Workflow**: Partial stock transfer from sister warehouse + Draft PR for remaining deficit.
- **Extensibility**: Combines Scenario D (transfer available portion) and Scenario A/C (Draft PR for remaining balance).

### Scenario F — Sales Dispatch Shortages
- **Status**: Implemented and live via `SalesShipmentService` delegating to `IInventoryReplenishmentEngine`.

---

## 8. Verification and Quality Checklist

- [x] Shared engine created in `Backend/ERP/ERP.Application/Features/Inventory/Replenishment/`
- [x] Scoped DI registered in `ServiceRegistration.cs`
- [x] Cross-company PO supply isolation enforced (`po.CompanyId == companyId`)
- [x] Expired batch and quarantine exclusion implemented in stock availability calculation
- [x] Anti-double-counting reconciled between `InventoryBalance.QuantityOnOrder` and open approved PO lines
- [x] Delegation to `IPurchaseRequisitionService.CreateRequisitionAsync` preserves genuine `PurchaseRequisition` entity and Draft lifecycle
- [x] Zero stock deduction or allocation invariant enforced
- [x] Permission `INVENTORY.REPLENISH.CREATE` registered in catalogue and role templates
- [x] Controller `ReplenishmentController` created with company/branch scope validation
- [x] Frontend `InventoryService` and `inventory.models.ts` updated with TypeScript DTOs
- [x] Integration tests created in `InventoryReplenishmentEngineTests.cs`
- [x] Architecture specification completed
