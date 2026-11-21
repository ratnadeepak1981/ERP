# ApexERP — Sales Dispatch Note, Warehouse Picking, Replenishment & Fulfilment Specification

**Document Version:** 1.1.0  
**Status:** Architecture, Defect Analysis, Source-Code Audit & Operational Specification  
**Date:** 2026-11-21  
**Target Modules:**  
- Sales Domain: `ERP.Domain.Features.Sales.Shipment`, `ERP.Domain.Features.Sales.SalesOrder`
- Inventory & Warehouse Domain: `ERP.Domain.Features.Inventory`, `ERP.Domain.Features.MasterData.Warehouse`
- Application & API: `ERP.Application.Features.Sales.Shipment`, `API.Features.Domain.SalesShipmentController`, `API.Features.Domain.InventoryController`, `API.Features.Domain.GoodsReceiptNoteController`
- Presentation: `Frontend/Web/src/app/features/domain/workspace/sales/dispatches/`, `Frontend/Web/src/app/features/domain/workspace/procurement/goods-receipt-page/`
**Related Documents:**
- [Root Instructions](file:///z:/ERP/AGENTS.md)
- [Backend Instructions](file:///z:/ERP/Backend/ERP/AGENTS.md)
- [Frontend Instructions](file:///z:/ERP/Frontend/Web/AGENTS.md)
- [Sales Quotation Workflow Specification](file:///z:/ERP/Sales-Quotation-Workflow.md)
- [Customer Workspace & Registration Workflow](file:///z:/ERP/Customer-Workspace-and-Registration-Workflow.md)
- [Sales Backend Completion Report](file:///z:/ERP/ApexERP-Sales-Backend-Completion-Report.md)

---

## 1. Executive Summary & The Core Picking Principle

### 1.1 The Core Picking Requirement
The authoritative business requirement governing the ApexERP Sales Dispatch Note and Warehouse Picking workflow is:

> **Automatic stock availability discovery + default source selection + manual override + authorised dispatch posting.**
> 
> **Automatic picking does NOT mean automatic stock allocation, reservation or deduction.**

This principle governs all interactions across warehouse picking, logistics, sales channels, and inventory management:
1. **Automatic Discovery:** When a Draft Dispatch Note is created or reviewed, the system automatically discovers available, unreserved, non-expired, and non-quarantined stock across all authorized warehouse facilities, bins, and staging areas. It computes available balances and presents a sensible **default picking source suggestion**.
2. **Zero Involuntary Commitment:** Automatic discovery and default suggestion **never** reserve stock, **never** deduct stock, **never** create ledger transactions, **never** commit allocations, and **never** create artificial inventory receipts or transfers.
3. **Manual Override:** Authorized Sales Officers and Warehouse Officers retain complete operational freedom to override the suggested default source—changing warehouses, selecting alternative storage bins, choosing staging docks, adjusting quantities, and splitting a single Sales Order line across multiple bins or facilities.
4. **Authorised Posting:** Stock is deducted **strictly and exclusively** when an authorized user posts the completed dispatch note via `ISalesShipmentService.DispatchShipmentAsync` (requiring `SALES_SHIPMENT.DISPATCH`), creating immutable, sequenced inventory ledger movements in an atomic database transaction.

---

## 2. Reproduction and Analysis of the Original Picking-Source Defect

An exhaustive inspection of the existing source code in `SalesShipmentService.cs` (lines 866–1050), `SalesShipmentController.cs`, and `sales-shipment-page.component.ts` reveals the exact defects in the initial (Round 1) implementation:

```
┌────────────────────────────────────────────────────────────────────────┐
│               Original (Round 1) Picking-Source Defect                 │
├────────────────────────────────────────────────────────────────────────┤
│ 1. Header-Level Location Binding:                                      │
│    sales-shipment-page bound the entire order to a single location     │
│    picker: selectedLocationId & selectedWarehouseId in header.         │
│                                                                        │
│ 2. All-or-Nothing Location Constraint:                                 │
│    GetOrderDispatchAvailabilityAsync evaluated whether ONE single      │
│    location could fulfill 100% of all lines on the order.              │
│    If no single location had 100% of all items -> canFulfil = false.   │
│                                                                        │
│ 3. Dispatch Blocking:                                                  │
│    sales-shipment-page lines 524-526:                                 │
│    if (!this.availability()?.canFulfil) {                              │
│      this.message.set({ text: 'Cannot dispatch order with insufficient │
│                         stock.', severity: 'danger' });                │
│      return;                                                           │
│    }                                                                   │
│                                                                        │
│ 4. No Multi-Bin / Multi-Warehouse Allocation Grid:                     │
│    The UI lacked an editable grid (AppEditableDataTableComponent) to   │
│    let users split a line (e.g. 30 from Bin A, 20 from Bin B, 10 from │
│    Warehouse 2).                                                       │
│                                                                        │
│ 5. Conceptual Ambiguity:                                               │
│    "Automatic picking" was mistakenly feared to imply automatic stock  │
│    deduction or rigid locking, preventing flexible human warehouse     │
│    adjustments.                                                        │
└────────────────────────────────────────────────────────────────────────┘
```

### 2.1 Technical Root Causes in Existing Code

1. **Header-Level Monolithic Selection (`sales-shipment-page.component.ts`):**
   - Lines 109–110 declared:
     ```typescript
     readonly selectedLocationId = signal<string | null>(null);
     readonly selectedWarehouseId = signal<string | null>(null);
     ```
   - In lines 578–591, `dispatchOrder()` packaged `DispatchOrderRequest` with a single `warehouseId: whId` and `warehouseLocationId: locId`.
   - **Impact:** Forced all products on the Sales Order to be picked from the exact same storage bin, an assumption that violates real-world warehouse operations where products are distributed across different zones, aisles, and bins.

2. **All-or-Nothing Availability Evaluation (`SalesShipmentService.cs`):**
   - In `GetOrderDispatchAvailabilityAsync` (lines 1039–1050), the service iterated through active locations and checked `canLocFulfilAll`:
     ```csharp
     bool canLocFulfilAll = true;
     foreach (var (item, product, remaining) in remainingLines) { ... }
     ```
   - If a warehouse had 10 items in Bin 1 and 20 items in Bin 2 for a 30-item line, `canLocFulfilAll` evaluated to `false`.
   - The method returned `canFulfil = false`, and `sales-shipment-page` unconditionally aborted dispatch.

3. **Absence of Reusable Editable Table in Frontend:**
   - Unlike `goods-receipt-page.component.ts`, which utilizes `AppEditableDataTableComponent` with per-row dropdowns for `warehouseId` and `warehouseLocationId`, `sales-shipment-page` rendered read-only availability cards without row-level bin selection or row splitting.

4. **Resolution Mandate:**
   - The backend domain model `SalesShipmentItem` **already natively supports** multi-allocation rows per order line (`SalesOrderItemId`).
   - The picking engine must separate **availability discovery** from **allocation commitment**, and provide an interactive, editable picking grid matching the Partial Product Receipt convention.

---

## 3. Correct Picking Business Definition

### 3.1 Automatic Stock Availability Discovery
When a Draft Dispatch Note is created upon Sales Order confirmation, opened in the workspace, or refreshed by an operator:

1. **Multi-Warehouse Discovery:** The system scans all active warehouse facilities within the authorized tenant and operating company scope (scoped to user branch permissions).
2. **Location & Bin Breakdown:** Within each eligible warehouse, the system queries active locations where `LocationType.CanPick == true` (including bulk storage, picking racks, shelves, and staging docks).
3. **Staging Area Inclusion:** Staging areas (`LocationType.Code == "STAGING"`) are explicitly discovered and surfaced, identifying goods already staged for dispatch.
4. **Balance Calculation:** For each location, the system calculates **eligible unreserved stock**:
   $$\text{EligibleAvailable} = \text{OnHand} - \text{CommittedReservations} - \text{Quarantine} - \text{Expired}$$
5. **Default Source Proposal:** The system identifies the best default source per line and displays it clearly as a **suggestion**.
6. **Strict Invariant:**
   - **NO** stock is reserved.
   - **NO** stock is deducted.
   - **NO** inventory movements are generated.
   - **NO** draft receipts or transfers are committed.

### 3.2 Default Source Selection Strategy
The system selects the default picking source per line using deterministic inventory rules:
1. **Primary Warehouse Scope:** Priority is given to warehouses associated with the Sales Order's operating branch and company.
2. **Staging Stock Priority:** If stock for the product already resides in an outbound staging area (`LocationType.Code == "STAGING"`), that staging area is suggested first to expedite shipping and clear dock space.
3. **Picking Capability:** The location must have `LocationType.CanPick == true` and `LocationType.CanStoreInventory == true`.
4. **Quarantine Exclusion:** Locations or batches marked `IsQuarantine == true` are completely excluded.
5. **FEFO Prioritization for Batches:** For batch-tracked products, batches are ordered by `ExpiryDate ASC`. The non-expired batch closest to expiration is selected first.
6. **Single Bin Concentration:** When picking from storage bins, the system prefers the single bin holding sufficient stock to fulfill the line. If no single bin holds sufficient stock, it suggests the bin with the highest on-hand balance.

### 3.3 Manual Picking / Source Override
Manual picking empowers the Sales Officer or Warehouse Officer to modify the automatically suggested source:

An authorized user can:
- Change the selected warehouse facility per line.
- Change the storage location/bin per line.
- Select a staging area dock instead of a storage bin.
- Amend the allocated picking quantity.
- **Split an allocation row:** Add one or more secondary allocation rows for the same Sales Order line.
- Remove or amend draft allocation rows.
- Instantly review recalculated shortages and remaining unfulfilled balances.

#### Concrete Multi-Allocation Example:
Sales Order Line requires **60 units** of Industrial Pump (`PRD-PUMP`):

| Line Reference | Warehouse Facility | Storage Location / Bin | Available in Bin | User Allocation | Shortage | Tracking / Batch |
| :--- | :--- | :--- | :---: | :---: | :---: | :--- |
| **Line 1 (Primary)** | Main Warehouse | Bin A-01-01 (Storage) | 40 | **30** | 0 | Batch: `B-101` (Exp: 2027-06) |
| **Line 1 (Split 1)** | Main Warehouse | Bin A-02-04 (Storage) | 25 | **20** | 0 | Batch: `B-102` (Exp: 2027-08) |
| **Line 1 (Split 2)** | Branch Warehouse | Dock STG-01 (Staging) | 30 | **10** | 0 | Batch: `B-103` (Exp: 2027-09) |
| **Total Allocated** | | | | **60 / 60** | **0** | **100% Fulfilled** |

- The system validates that $\sum \text{AllocatedQuantity} (30 + 20 + 10 = 60) \le \text{OutstandingQuantity} (60)$.
- When the user changes any dropdown or quantity, availability and shortages are recalculated in real time.

### 3.4 Granular Picking Permissions
ApexERP enforces least-privilege, granular permissions rather than hardcoded role names:

- `SALES_SHIPMENT.VIEW`: View dispatch registers and draft details.
- `SALES_SHIPMENT.CREATE`: Create draft dispatch notes.
- `SALES_SHIPMENT.EDIT`: Amend draft allocations, override warehouses/bins, split rows, and update quantities.
- `SALES_SHIPMENT.DISPATCH`: Execute final dispatch posting, deducting stock from the inventory ledger.
- `SALES_SHIPMENT.DELIVER`: Confirm customer handover or carrier delivery.
- `SALES_SHIPMENT.CANCEL`: Cancel draft dispatch notes.
- `INVENTORY.VIEW`: View available stock, balances, and multi-warehouse shortages.

**Enforcement Rule:** Sales Officers and Warehouse Officers holding `SALES_SHIPMENT.EDIT` and `INVENTORY.VIEW` within their assigned company/branch scope are fully authorized to prepare, amend, and split draft dispatch allocations.

---

## 4. Unified Fulfillment Engine Across All Four Sales Channels

ApexERP operates four commercial sales channels that converge into a single, unified fulfillment engine:

```
┌─────────────────────────┐  ┌─────────────────────────┐  ┌─────────────────────────┐  ┌─────────────────────────┐
│ 1. Retail E-commerce    │  │ 2. Direct B2B Order     │  │ 3. Sales Quotation      │  │ 4. Internal ERP Order   │
│    (Public Cart/Store)  │  │    (Price Card / MOQ)   │  │    (Formal Proposal)    │  │    (Sales Desk / CRM)   │
└────────────┬────────────┘  └────────────┬────────────┘  └────────────┬────────────┘  └────────────┬────────────┘
             │                            │                            │                            │
             └────────────────────────────┴──────────────┬─────────────┴────────────────────────────┘
                                                         │
                                                         ▼ (Unified Contract Boundary)
                                          ┌─────────────────────────────┐
                                          │      SalesOrder Entity      │
                                          │  (Confirmed Status Lock)    │
                                          └──────────────┬──────────────┘
                                                         │
                                                         ▼ (Automated Draft Generation)
                                          ┌─────────────────────────────┐
                                          │     SalesShipment Entity    │
                                          │   (Draft Dispatch Note)     │
                                          └──────────────┬──────────────┘
                                                         │
                                    ┌────────────────────┴────────────────────┐
                                    ▼                                         ▼
                     ┌─────────────────────────────┐           ┌─────────────────────────────┐
                     │ Automatic Stock Discovery   │           │ Manual Source Override      │
                     │ • Multi-warehouse scan      │           │ • Split bins & warehouses   │
                     │ • Sensible default source   │           │ • Amend picking quantities  │
                     │ • ZERO stock deduction      │           │ • ZERO stock deduction      │
                     └─────────────────────────────┘           └─────────────────────────────┘
                                                         │
                                                         ▼ (Authorised Dispatch Posting)
                                          ┌─────────────────────────────┐
                                          │   IInventoryPostingService  │
                                          │ (Atomic Ledger Deduction)   │
                                          └─────────────────────────────┘
```

1. **No Duplicate Aggregates:** E-commerce, Direct B2B, Quotation conversions, and Internal Sales use the exact same `SalesOrder` and `SalesShipment` aggregates.
2. **Simplified Default for Retail / E-commerce:** For standard retail and E-commerce orders, the automated default source selection enables single-click dispatch for warehouse staff without manual bin hunting.
3. **Complete Override Capability:** For complex B2B or internal contracts, operators retain full power to split allocations, pick from staging areas, or pull from regional warehouses.

---

## 5. Shortage and Replenishment Workflow

When the requested quantity exceeds available stock across currently selected locations, the system initiates a controlled replenishment workflow.

```
[ Shortage Detected at Current Bin ]
  Required: 100 | Available at Bin: 40 | Deficit: 60
       │
       ▼
[ Automatic Multi-Warehouse Discovery Scan ]
  Is stock available in other bins in the same warehouse?
  ├── YES: Suggest intra-warehouse split (e.g. Bin 1: 40, Bin 2: 20).
  └── NO: Scan other authorized warehouses within company scope.
       │
       ▼
  Does stock exist in another authorized warehouse (e.g. Central DC)?
  ├── YES (Stock Exists Elsewhere):
  │    ├── Surface "Alternative Warehouse Available: Central DC (80 units)".
  │    ├── Option A: Authorized user overrides source to pick from Central DC directly.
  │    └── Option B: Initiate Inventory Transfer Replenishment Request.
  │         ├── Initiating user holds INVENTORY.TRANSFER permission -> Creates Draft InventoryTransfer.
  │         └── Initiating user lacks permission -> Creates Replenishment Request for Inventory Officer.
  │         └── Invariant: Destination stock DOES NOT increase until transfer is legitimately posted.
  │
  └── NO (Stock Exhausted Across All Facilities):
       ├── Display remaining shortage clearly.
       └── Surface Legitimate Stock Receipt Replenishment Options:
            ├── Option A: Identify open Purchase Orders and expedite Goods Receipt (GRN).
            ├── Option B: Create Purchase Requisition / PO for procurement.
            ├── Option C: Authorized Inventory user prepares Draft Stock Receipt against valid source document.
            └── STRICT PROHIBITION: Never create artificial stock, fake supplier receipts, or paper receipts.
```

### 5.1 Replenishment Path A: Inventory Transfer
- When stock exists in another authorized warehouse:
  - The UI displays alternative warehouses and available quantities.
  - An authorized user can initiate an **Inventory Transfer Draft**.
  - If the initiating user lacks transfer permissions, the system logs a **Replenishment Request** for the Inventory Manager.
  - **Ledger Invariant:** Draft transfers move zero stock. Stock increases at the destination warehouse **only** after the transfer is physically received and posted via `IInventoryPostingService.PostTransferAsync`.

### 5.2 Replenishment Path B: Stock Receipt (Procurement Goods Receipt)
- When stock is exhausted across all facilities:
  - The system highlights the shortage and checks for open Purchase Orders.
  - If an approved PO exists, an authorized user (Procurement Officer, Warehouse Officer, or Inventory Officer) prepares a **Goods Receipt Note (GRN)**.
  - **Strict Anti-Corruption Invariants:**
    - Fictitious supplier receipts, artificial inventory injections, and fake received quantities are **strictly prohibited**.
    - Creating a Draft GRN **never** increases inventory.
    - Only physical receipt, dock verification, and authorized posting (`GRN.CONFIRM`) increase inventory.

### 5.3 Granular Replenishment Permission Matrix

| Operation | Sales Officer | Warehouse Officer | Inventory Officer | Inventory Manager | Tenant Admin |
| :--- | :---: | :---: | :---: | :---: | :---: |
| **View Eligible Stock & Balances** | `INVENTORY.VIEW` (Scoped) | `INVENTORY.VIEW` (Scoped) | `INVENTORY.VIEW` (Scoped) | `INVENTORY.VIEW` (Scoped) | `INVENTORY.VIEW` |
| **View Shortages & Deficits** | Yes (`INVENTORY.VIEW`) | Yes (`INVENTORY.VIEW`) | Yes (`INVENTORY.VIEW`) | Yes (`INVENTORY.VIEW`) | Yes |
| **View Alternative Warehouses** | `INVENTORY.VIEW` (Scoped) | `INVENTORY.VIEW` (Scoped) | `INVENTORY.VIEW` (Scoped) | `INVENTORY.VIEW` (Scoped) | `INVENTORY.VIEW` |
| **Request Replenishment** | Yes | Yes | Yes | Yes | Yes |
| **Create Inventory Transfer Draft** | If authorized | If authorized | If authorized | If authorized | Yes |
| **Post Inventory Transfer** | If authorized | If authorized | If authorized | If authorized | Yes |
| **Create Draft Goods Receipt (GRN)** | If authorized | `GRN.CREATE` | `GRN.CREATE` | `GRN.CREATE` | `GRN.CREATE` |
| **Edit Draft Goods Receipt** | If authorized | `GRN.EDIT` | `GRN.EDIT` | `GRN.EDIT` | `GRN.EDIT` |
| **Verify Physical Dock Receipt** | If authorized | Yes | Yes | Yes | Yes |
| **Post Goods Receipt (`GRN.CONFIRM`)**| If authorized | `GRN.CONFIRM` | `GRN.CONFIRM` | `GRN.CONFIRM` | `GRN.CONFIRM` |
| **Post Direct Stock Receipt** | No | If authorized | `STOCK_RECEIPT.POST` | `STOCK_RECEIPT.POST` | `STOCK_RECEIPT.POST` |

*Note:* Role titles alone never bypass security checks. Operations execute strictly according to verified assigned permissions.

---

## 6. Product Receipt Flexibility (Alignment with GRN Workflow)

The Partial Product Receipt (GRN) workflow (`GoodsReceiptNote`) serves as the operational benchmark for warehouse consistency.

### 6.1 Multi-Role Operational Participation
Procurement Officers, Warehouse Officers, Inventory Officers, and Inventory Managers collaborate on goods receipts according to their granular permissions:
1. **Receipt Preparation:** Users with `GRN.CREATE` initialize a draft receipt from an approved Purchase Order (`POST /api/.../goods-receipt-notes/from-po/{poId}`).
2. **Dock Inspection & Location Selection:** Warehouse Officers inspect delivered goods, select receiving warehouses, and assign destination storage locations or quarantine zones.
3. **Partial Receipt Support:** Actual received quantities are entered per line. Outstanding quantities remain open on the PO for subsequent receipts.
4. **Batch & Serial Verification:** For tracked items, batch numbers, manufacturing dates, and expiry dates are captured via `BatchSerialTrackingDialogComponent` (`dialogMode: 'receipt'`).
5. **Physical Verification & Posting:** Authorized officers with `GRN.CONFIRM` post the receipt, executing `IInventoryPostingService.PostMovementAsync` with `MovementType = InventoryMovementType.GoodsReceipt` (inflow) to increase ledger balances.

---

## 7. Existing Inventory Invariants to Preserve

The following core inventory invariants are strictly preserved:

1. **Draft Dispatch Isolation:** Creating, updating, or deleting a Draft Dispatch Note **never deducts stock**.
2. **Draft Allocation Isolation:** Assigning warehouses, bins, batches, or serials to a draft **never deducts or reserves stock**.
3. **Draft Receipt Isolation:** Creating or editing a Draft GRN **never increases stock**.
4. **Draft Transfer Isolation:** Creating an Inventory Transfer draft **never moves stock**.
5. **Transactional Ledger Movements:** All physical inventory changes flow through `IInventoryPostingService.PostMovementAsync` with valid `SourceDocumentType` references.
6. **Double-Posting Protection:** Idempotency checks on `InventoryTransaction` prevent duplicate deductions.
7. **Posted Document Immutability:** Dispatched shipments and confirmed receipts cannot be edited or deleted. Reversals require formal return workflows.
8. **Batch & FEFO Rules:** Batches must be active and unexpired. FEFO ordering is strictly observed.
9. **Staging Area Model:** Staging areas are `WarehouseLocation` records with `LocationType.Code = "STAGING"`. Moving stock to staging via transfer and then dispatching from staging prevents double deduction.

---

## 8. Frontend Consistency: Picking Grid Specification

The Sales Dispatch Note Picking Grid is refactored to align with `AppEditableDataTableComponent` in `goods-receipt-page.component.ts`:

### 8.1 Grid Columns Breakdown

| Column Field | Header Label | Width | Type | Editable | Description |
| :--- | :--- | :---: | :---: | :---: | :--- |
| `productCode` | Item Code | `140px` | Text | No | Master product code (e.g. `PRD-00124`). |
| `productName` | Item Description | `220px` | Text | No | Master product name. |
| `unitOfMeasure` | UOM | `80px` | Text | No | Transaction or stocking UOM. |
| `orderedQuantity` | SO Ordered | `100px` | Number | No | Total quantity contracted on Sales Order. |
| `previouslyDispatchedQuantity` | Prev. Dispatched | `110px` | Number | No | Cumulative quantity shipped on prior dispatches. |
| `outstandingQuantity` | Outstanding | `110px` | Number | No | Remaining unfulfilled balance ($Ordered - PrevDispatched$). |
| `warehouseId` | Source Warehouse * | `210px` | Select | **Yes** | Dropdown of authorized warehouses. Default source pre-selected. |
| `warehouseLocationId` | Source Location / Bin * | `270px` | Select | **Yes** | Dropdown of pickable locations/bins/staging docks. Default bin pre-selected. |
| `availableQuantity` | Available in Bin | `110px` | Number | No | Real-time unreserved stock at selected `(WarehouseId, LocationId)`. |
| `quantityToDispatch` | Pick Qty Now * | `120px` | Number | **Yes** | Actual quantity to pick from this bin. Pre-populated with suggested quantity. |
| `shortageQuantity` | Shortage | `100px` | Badge | No | Red warning badge if $PickQty > Available$; green check if clear. |
| `trackingMode` | Tracking Details | `180px` | Custom | No | Interactive button opening `BatchSerialTrackingDialogComponent` (`dialogMode: 'issue'`). |
| `actions` | Actions | `100px` | Action | **Yes** | **"Split / Add Bin"** button to add an allocation row for the same order line. |

### 8.2 Visual Distinction Requirements
The user interface must clearly differentiate:
- **Available Stock:** Informational metric showing physical unreserved inventory.
- **Suggested Default Source:** Visual tag (e.g. `[Suggested]`) showing the system's recommended warehouse/bin.
- **User-Selected Source:** Explicitly confirmed selection once modified by the user.
- **Pick Qty Now:** The confirmed quantity to be physically picked.
- **Remaining Shortage:** Amber/red warning pill indicating unfulfilled demand.

An automatically suggested default source must **never** be presented as a confirmed physical pick until the user reviews and confirms it.

---

## 9. Comprehensive Test Matrix: Existing vs. Proposed Tests

### 9.1 Existing Passing Tests (Backend Security & Unit Suite)

| Test ID | Test Name | Location | Status | Verified Capability |
| :--- | :--- | :--- | :---: | :--- |
| **ET-01** | `CreateShipment_FromOrder_FullWorkflow_Success` | `SalesShipmentWorkflowAndScopeTests.cs:34` | **PASS** | Auto-draft creation on order confirm, draft status, zero deduction until dispatch. |
| **ET-02** | `PartialShipments_CumulativeCalculation_And_OverShipmentRejection` | `SalesShipmentWorkflowAndScopeTests.cs:99` | **PASS** | Cumulative partial dispatch calculation, 0% over-shipment tolerance rejection. |
| **ET-03** | `CustomerPickup_Or_StoreHandover_DirectDelivery_Succeeds` | `SalesShipmentWorkflowAndScopeTests.cs:234` | **PASS** | Direct draft to delivered transition, immediate stock deduction. |
| **ET-04** | `InsufficientInventory_Dispatch_FailsWithConflict` | `SalesShipmentWorkflowAndScopeTests.cs:273` | **PASS** | Rejection when on-hand stock is insufficient at posting time. |
| **ET-05** | `ServiceOnlyOrder_CannotCreatePhysicalShipment` | `SalesShipmentWorkflowAndScopeTests.cs:303` | **PASS** | Discrimination of service products; zero dispatch note created. |
| **ET-06** | `CancelDraftShipment_Succeeds_AndDoesNotMutateStock` | `SalesShipmentWorkflowAndScopeTests.cs:328` | **PASS** | Draft cancellation transitions to `Cancelled` with zero ledger movements. |
| **ET-07** | `ScopeEnforcement_CrossTenantAndBranch_Forbidden` | `SalesShipmentWorkflowAndScopeTests.cs:359` | **PASS** | Tenant and branch boundary enforcement (`403 Forbidden`). |
| **ET-08** | `PermissionEnforcement_WithoutDispatchPermission_ReturnsForbidden` | `SalesShipmentWorkflowAndScopeTests.cs:393` | **PASS** | Enforces `SALES_SHIPMENT.DISPATCH` permission. |
| **ET-09** | `SalesShipment_Create_SetsExpectedInitialValues` | `SalesShipmentDomainUnitTests.cs:13` | **PASS** | Domain initial state, Draft status, empty items. |
| **ET-10** | `SalesShipment_AddItem_ValidatesStateAndQuantities` | `SalesShipmentDomainUnitTests.cs:50` | **PASS** | Item quantity validation, negative rejection. |
| **ET-11** | `SalesShipment_LifecycleTransitions_EnforceDomainRules` | `SalesShipmentDomainUnitTests.cs:86` | **PASS** | State machine enforcement: Draft $\rightarrow$ Dispatched $\rightarrow$ Delivered. |
| **ET-12** | `SalesShipment_Idempotency_RepeatCallsDoNotThrow` | `SalesShipmentDomainUnitTests.cs:167` | **PASS** | Idempotency guard prevents duplicate dispatch execution. |
| **ET-13** | `DirectB2BOrder_ValidRequest_CreatesDraftSalesOrder...` | `SalesBackendCompletionTests.cs:40` | **PASS** | Direct B2B order channel creates Draft `SalesOrder`. |
| **ET-14** | `SalesOrder_Confirm_CreatesDraftDispatchNote_ForPhysicalItems` | `SalesBackendCompletionTests.cs:380` | **PASS** | Automatic Draft dispatch note generation on order confirmation. |

### 9.2 Proposed New Tests (To be Implemented in Phase 2 & 3)

| Test ID | Proposed Test Scenario | Target Layer | Expected Result |
| :--- | :--- | :--- | :--- |
| **PT-01** | Multi-Warehouse Stock Discovery Scan | Application / Unit | `GetOrderDispatchAvailabilityAsync` returns availability across all authorized warehouses for each line item. |
| **PT-02** | Staging Stock Prioritization in Default Source | Domain / Service | When stock is in staging (`STAGING`), default source selects the staging location first. |
| **PT-03** | Multi-Bin Split Allocation Persistence | Integration | `UpdateDraftAsync` accepts multiple allocation rows for one `SalesOrderItemId` and saves them in `DomainDbContext`. |
| **PT-04** | Cross-Warehouse Split Allocation Dispatch | Integration | Order line with 60 units split across Warehouse 1 (40) and Warehouse 2 (20); posting deducts 40 from WH1 and 20 from WH2. |
| **PT-05** | Dynamic Shortage Recalculation on Manual Override | Frontend Component | Changing location dropdown recalculates available quantity and updates shortage pill in real time. |
| **PT-06** | Zero Stock Impact on Draft Allocation Override | Integration | User amends warehouses, locations, and quantities on draft 5 times; stock ledger confirms zero movements. |
| **PT-07** | Inventory Transfer Replenishment Draft Creation | API / Integration | Authorized user initiates transfer from alternative warehouse; creates draft transfer without incrementing destination stock. |
| **PT-08** | Unauthorized Stock Receipt Rejection | Security | Sales Officer without `GRN.CREATE` or `STOCK_RECEIPT.POST` attempting to create stock receipt receives `403 Forbidden`. |
| **PT-09** | Two-Step Pick-to-Stage and Dispatch Test | Integration | Transfer from storage bin to staging dock, followed by dispatch from staging dock; asserts exact single ledger deduction. |
| **PT-10** | Reusable Batch/Serial Dialog Issue Mode FEFO Ordering | Frontend Component | Dialog displays eligible batches sorted by `ExpiryDate ASC`; caps available quantity per batch. |

---

## 10. Proposed Implementation Sequence

To maintain zero regressions and strict compliance with ApexERP Clean Architecture, the implementation will proceed in four sequential phases:

### Phase 2A: Backend Availability Discovery & Multi-Bin DTO Enhancements
1. Enhance `SalesOrderDispatchAvailabilityDto` and `SalesOrderDispatchLineAvailabilityDto`:
   - Include `AllWarehouseStock`: List of eligible warehouses and on-hand balances.
   - Include `EligibleLocations`: List of pickable bins and staging docks per warehouse.
   - Include `DefaultSuggestedSource`: Recommended warehouse, bin, and suggested quantity.
   - Include `SuggestedMultiBinSplits`: Multi-bin allocation split recommendation when a single bin cannot fulfill 100%.
2. Enhance `SalesShipmentService.UpdateDraftAsync` to accept and validate multi-allocation rows per `SalesOrderItemId`.
3. Add backend integration tests (`PT-01` through `PT-04`).

### Phase 2B: Contextual Replenishment Services
1. Add helper query in `IInventoryPlanningService` to retrieve open POs and transfer options for short items.
2. Wire permission checks for replenishment request generation.
3. Add replenishment security tests (`PT-07`, `PT-08`).

### Phase 3: Frontend Picking Grid Refactoring (Operational Alignment with GRN)
1. Refactor `sales-shipment-page.component.ts/html`:
   - Replace the Round 1 single-location header picker with `AppEditableDataTableComponent`.
   - Implement per-row `warehouseId` and `warehouseLocationId` dropdowns.
   - Implement **"Split / Add Bin"** row action allowing operators to allocate multiple bins for a single line.
   - Render real-time bin availability and dynamic shortage warning badges.
   - Connect `BatchSerialTrackingDialogComponent` in `dialogMode: 'issue'`.
2. Add contextual shortage banner with one-click navigation to create an inter-warehouse transfer or view open PO status.
3. Align action buttons: `Back`, `Save as Draft`, `Confirm & Post Dispatch`, `Cancel Draft`, `Print Packing Slip`.

### Phase 4: Full End-to-End Test & Real Browser Verification
1. Run backend security test suite (`dotnet test`).
2. Run Angular frontend build (`npm run build`).
3. Conduct real browser verification across all 4 sales channels:
   - Order confirmation $\rightarrow$ Auto Draft Dispatch Note.
   - Review default suggested source $\rightarrow$ Zero stock deducted.
   - Manual override $\rightarrow$ Split line across 2 bins.
   - Assign FEFO batches via dialog.
   - Post dispatch $\rightarrow$ Verify ledger deduction in Stock Ledger report.
   - Partial dispatch $\rightarrow$ Subsequent dispatch for remaining balance.
