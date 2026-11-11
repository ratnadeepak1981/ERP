# ApexERP — Batch Functionality Comprehensive Audit Report (Report H)

**Document ID:** Report_H_Batch_Functionality_Audit.md  
**Date:** 2026-11-11  
**Audit Scope:** 7 Core Inventory & Procurement Workflows  
**Mode:** AUDIT ONLY (Read-Only Inspection)

---

## 1. Executive Summary

A comprehensive implementation, integration, consistency, and security audit of the **Batch (Lot) Tracking** functionality was conducted across the seven core inventory and fulfillment workflows:
1. **Product Receipt (Goods Receipt Note / GRN)**
2. **Putaway**
3. **Stock Receipt (Ad-Hoc Inflow)**
4. **Stock Issue (Ad-Hoc Outflow)**
5. **Inventory Item Transfer (Inter-Location / Inter-Warehouse)**
6. **Stock Count (Physical Inventory & Variance Adjustment)**
7. **Dispatch Note (Sales Order Shipment)**

### Overall Audit Verdict
- **Backend Core Posting Engine:** **STRONG & MATURE (PASS)**. The backend `IInventoryPostingService`, `ProductBatch` master synchronization, FEFO allocation algorithm, transaction ledger, and valuation layers are well-engineered and rigorously tested at the domain level.
- **Backend Workflow API Coverage:** **MIXED (PARTIAL)**. Workflows 1, 2, 3, 4, 6, and 7 have fully functioning controllers and services. Workflow 5 (**Inventory Item Transfer**) has an internal posting engine method (`PostTransferAsync`) used by Putaway, but **completely lacks an API controller and public application service endpoint** for ad-hoc warehouse item transfers.
- **Frontend Web Implementation:** **SIGNIFICANT DISCONNECT (FAIL / PARTIAL)**. 
  - **Goods Receipt Note (`goods-receipt-page.component.ts`)**: The line model `EditableGRNLine` and table columns completely omit `batchNumber`, `manufacturingDate`, and `expiryDate`. Users cannot enter batch numbers when receiving batch-tracked goods. Attempting to confirm batch-tracked products received without batch numbers causes backend `400 Bad Request` exceptions.
  - **Store Putaway (`store-putaway-page.component.ts`)**: The line model contains `batchNumber`, but the detail table columns omit it. Operators putting away stock cannot see or verify which batch is being routed to which bin.
  - **Stock Transfers (`transfer-stock.component.ts`)**: A read-only transaction viewer with an "Issue Stock" button. No transfer creation form exists.
  - **Stock Count (`count-adjust.component.ts`)**: A placeholder product catalog list. Completely disconnected from `StockCountController` (`/api/stock-counts`); has no draft, physical count entry, variance calculation, or posting integration.
  - **Sales Shipment (`sales-shipment-page.component.ts`)**: Direct order dispatch works with backend FEFO auto-allocation, but manual shipment draft creation has no batch selection capability.

---

## 2. Existing Implementation Inventory

| Workflow | Backend Controller | Backend Service | Domain / Entity Models | Frontend Route | Frontend Component | Existing Integration Tests |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **1. Product Receipt** | `GoodsReceiptNoteController` | `GoodsReceiptNoteService` | `GoodsReceiptNote`, `GoodsReceiptNoteLine`, `ProductBatch` | `/app/procurement/goods-receipts/new` | `GoodsReceiptPageComponent` | `GoodsReceiptNoteTests.cs`, `ProductBatchMasterAndReceiptTests.cs` |
| **2. Putaway** | `PutawayController` | `PutawayService` | `Putaway`, `PutawayLine`, `PutawayLineAllocation` | `/app/inventory/store-putaway/new` | `StorePutawayPageComponent` | `PutawayPostingIntegrationTests.cs` |
| **3. Stock Receipt** | `InventoryController` | `InventoryService`, `InventoryPostingService` | `InventoryTransaction`, `InventoryBalance`, `ProductBatch`, `InventoryCostLayer` | `/app/inventory/stock-receipts/new` | `StockReceiptPageComponent`, `StockTransactionFormComponent` | `StockReceiptBatchIntegrationTests.cs`, `ProductBatchMasterAndReceiptTests.cs` |
| **4. Stock Issue** | `InventoryController` | `InventoryService`, `InventoryPostingService` | `InventoryTransaction`, `InventoryBalance`, `ProductBatch` | `/app/inventory/issue-stock/new` | `IssueStockPageComponent`, `StockTransactionFormComponent` | `StockIssueBatchIntegrationTests.cs`, `StockIssuePostingTests.cs` |
| **5. Item Transfer** | **None** (Missing Controller) | Internal `IInventoryPostingService.PostTransferAsync` | `InventoryTransaction`, `InventoryBalance` | `/app/inventory/transfers` | `TransferStockComponent` (Read-only list) | `InventoryTransferValuationIntegrationTests.cs` |
| **6. Stock Count** | `StockCountController` | `StockCountService` | `StockCount`, `StockCountLine`, `InventoryBatchIdentity` | `/app/inventory/count-adjust` | `CountAdjustComponent` (Catalog viewer only) | `StockCountSqlIntegrationTests.cs`, `StockCountOrchestrationTests.cs` |
| **7. Dispatch Note** | `SalesShipmentController` | `SalesShipmentService` | `SalesShipment`, `SalesShipmentItem`, `ProductBatch` | `/app/sales/dispatches/new` | `SalesShipmentPageComponent` | `SalesShipmentWorkflowAndScopeTests.cs`, `FEFOPickingAndExpiryDispatchTests.cs` |

---

## 3. Backend Completeness by Workflow

### 3.1 Product Receipt (Goods Receipt Note / GRN) — Status: `PASS`
- **Controller & Endpoints**:
  - `POST /api/companies/{companyId}/branches/{branchId}/goods-receipts/draft` (`GOODS_RECEIPT.CREATE`)
  - `PUT /api/companies/{companyId}/branches/{branchId}/goods-receipts/{id}` (`GOODS_RECEIPT.CREATE`)
  - `POST /api/companies/{companyId}/branches/{branchId}/goods-receipts/{id}/confirm` (`GOODS_RECEIPT.CONFIRM`)
  - `POST /api/companies/{companyId}/branches/{branchId}/goods-receipts/{id}/cancel` (`GOODS_RECEIPT.CONFIRM`)
- **Batch Capabilities**:
  - Line DTOs (`GoodsReceiptNoteLineDto`) and entity (`GoodsReceiptNoteLine.cs`) contain `BatchNumber`, `SerialNumber`, `ManufacturingDate`, `ExpiryDate`.
  - On confirmation, `GoodsReceiptNoteService.cs` invokes `_postingService.PostMovementAsync` with `MovementType = InventoryMovementType.GoodsReceipt`.
  - Automatically synchronizes with `ProductBatch` table. If batch exists, validates date consistency; if omitted, inherits master dates. If new, creates `ProductBatch`.
  - Atomically decrements purchase order on-order balances and increments warehouse location balance.

### 3.2 Putaway — Status: `PASS`
- **Controller & Endpoints**:
  - `GET .../putaways/eligible-receipts` (`INVENTORY.VIEW`)
  - `GET .../putaways/eligible-receipts/{sourceType}/{sourceId}/lines` (`INVENTORY.VIEW`)
  - `POST .../putaways` (Create Draft) (`PUTAWAY.POST`)
  - `PUT .../putaways/{id}` (Update Draft) (`PUTAWAY.POST`)
  - `POST .../putaways/{id}/confirm` (Confirm & Post) (`PUTAWAY.POST`)
  - `POST .../putaways/{id}/cancel` (`PUTAWAY.POST`)
- **Batch Capabilities**:
  - Filters eligible receipt lines by tracking identity (`BatchNumber`, `SerialNumber`).
  - Allows splitting a received batch across multiple destination storage bins (`PutawayLineAllocation`).
  - Calls `_inventoryPostingService.PostTransferAsync` to atomically move stock from the receiving staging location to destination bins while preserving batch identity and valuation.

### 3.3 Stock Receipt (Ad-Hoc Inflow) — Status: `PASS`
- **Controller & Endpoints**:
  - `POST /api/inventory/stock-receipts` (`STOCK_RECEIPT.POST`)
- **Batch Capabilities**:
  - Request DTO `CreateStockReceiptLineRequest` supports `BatchNumber`, `SerialNumber`, `ManufacturingDate`, `ExpiryDate`.
  - `InventoryService.cs` executes atomic transaction:
    - Creates `ProductBatch` if not present.
    - Creates `InventoryTransaction` with movement type `StockReceipt`.
    - Creates/updates `InventoryBalance` for (Location + Product + Batch).
    - Creates inbound `InventoryCostLayer` for inventory valuation (FIFO, LIFO, Weighted Average).

### 3.4 Stock Issue (Ad-Hoc Outflow) — Status: `PASS`
- **Controller & Endpoints**:
  - `POST /api/inventory/stock-issues` (`STOCK_ISSUE.POST`)
  - `GET /api/inventory/available-stock` (`INVENTORY.VIEW`)
- **Batch Capabilities**:
  - Request DTO `CreateStockIssueLineRequest` takes `BatchNumber`, `SerialNumber`.
  - `InventoryPostingService.cs` enforces:
    - Batch is required if product is batch-tracked.
    - Batch exists and is active (`ProductBatch.IsActive == true`).
    - Expiry check: rejects expired batches unless overridden by policy.
    - Decrements available and on-hand quantity for the exact batch.
    - Depletes cost layers according to company inventory valuation strategy.

### 3.5 Inventory Item Transfer — Status: `NOT IMPLEMENTED`
- **Controller & Endpoints**: **MISSING**. No endpoint exists under `InventoryController` (or any other controller) to execute ad-hoc warehouse or location transfers.
- **Underlying Engine**: `_inventoryPostingService.PostTransferAsync` exists in `ERP.Application/Features/Inventory/InventoryPosting/InventoryPostingService.cs` (lines 182-320) and supports transferring batch-tracked stock between locations or warehouses atomically, but has no API surface.

### 3.6 Stock Count (Physical Inventory) — Status: `PASS`
- **Controller & Endpoints**:
  - `GET /api/stock-counts`, `GET /api/stock-counts/{id}` (`STOCK_COUNT.VIEW`)
  - `POST /api/stock-counts` (`STOCK_COUNT.CREATE`)
  - `PUT /api/stock-counts/{id}` (`STOCK_COUNT.EDIT`)
  - `POST /api/stock-counts/{id}/submit` (`STOCK_COUNT.SUBMIT`)
  - `POST /api/stock-counts/{id}/approve` (`STOCK_COUNT.APPROVE`)
  - `POST /api/stock-counts/{id}/post` (`STOCK_COUNT.POST`)
- **Batch Capabilities**:
  - `StockCountLine.cs` tracks `BatchNumber`, `SerialNumber`, `ManufacturingDate`, `SystemQuantity`, and `PhysicalQuantity`.
  - Differentiates variance per batch identity (`InventoryBatchIdentity`).
  - Posting atomically posts positive variance as `StockAdjustmentPlus` (inflow) and negative variance as `StockAdjustmentMinus` (outflow) with batch traceability.

### 3.7 Dispatch Note (Sales Shipment) — Status: `PASS`
- **Controller & Endpoints**:
  - `GET .../sales-shipments`, `GET .../sales-shipments/{id}` (`SALES_SHIPMENT.VIEW`)
  - `GET .../from-order/{id}/availability` (`SALES_SHIPMENT.VIEW`)
  - `POST .../from-order/{id}/dispatch` (`SALES_SHIPMENT.DISPATCH`)
  - `POST .../from-order/{id}` (Create Draft) (`SALES_SHIPMENT.CREATE`)
  - `POST .../{id}/dispatch` (`SALES_SHIPMENT.DISPATCH`)
- **Batch Capabilities**:
  - Supports automated **FEFO (First-Expired, First-Out)** batch allocation: `SalesShipmentService.GetEligibleBatchesAsync` queries active, non-expired batches ordered by `ExpiryDate ASC`.
  - Validates batch availability, active status, and non-expiration at time of dispatch.
  - Automatically splits order lines into batch allocations if multiple batches are needed to fulfill the ordered quantity.
  - Posts `InventoryMovementType.SalesDispatch` and decrements stock balances.

---

## 4. Frontend Completeness by Workflow

### 4.1 Product Receipt (GRN) — Status: `FAIL`
- **Component**: [`goods-receipt-page.component.ts`](file:///z:/ERP/Frontend/Web/src/app/features/domain/workspace/procurement/goods-receipt-page.component.ts)
- **Defect**: The UI line model `EditableGRNLine` and table configuration `itemColumns` (lines 233-319) do not include `batchNumber`, `manufacturingDate`, or `expiryDate`.
- **Impact**: Warehouse staff cannot record batch numbers or expiry dates during goods receiving.

### 4.2 Putaway — Status: `PARTIAL`
- **Component**: [`store-putaway-page.component.ts`](file:///z:/ERP/Frontend/Web/src/app/features/domain/workspace/inventory/store-putaway/store-putaway-page.component.ts)
- **Defect**: `EditablePutawayLine` defines `batchNumber?: string | null`, but `detailColumns` (lines 176-253) does not render a `batchNumber` column.
- **Impact**: If a receipt contains multiple batches of the same product, the operator cannot see which batch is being assigned to which rack/bin.

### 4.3 Stock Receipt (Ad-Hoc) — Status: `PASS`
- **Component**: [`stock-receipt-page.component.ts`](file:///z:/ERP/Frontend/Web/src/app/features/domain/workspace/inventory/stock-receipt/stock-receipt-page.component.ts) & [`stock-transaction-form.component.ts`](file:///z:/ERP/Frontend/Web/src/app/features/domain/workspace/inventory/stock-transaction-form/stock-transaction-form.component.ts)
- **Features**: Has editable columns for `batchNumber`, `manufacturingDate`, and `expiryDate`. Maps directly to `CreateStockReceiptRequest`.

### 4.4 Stock Issue (Ad-Hoc) — Status: `PARTIAL`
- **Component**: [`issue-stock-page.component.ts`](file:///z:/ERP/Frontend/Web/src/app/features/domain/workspace/inventory/issue-stock/issue-stock-page.component.ts) & [`stock-transaction-form.component.ts`](file:///z:/ERP/Frontend/Web/src/app/features/domain/workspace/inventory/stock-transaction-form/stock-transaction-form.component.ts)
- **Defect**: Batch number input is a free-text input field. Changing it triggers `refreshAvailableStock(line)` to display available quantity, but it lacks a dropdown/picker to select from existing available batches at that warehouse location (`GET /api/inventory/batches/eligible`).

### 4.5 Inventory Item Transfer — Status: `NOT IMPLEMENTED`
- **Component**: [`transfer-stock.component.ts`](file:///z:/ERP/Frontend/Web/src/app/features/domain/workspace/inventory/transfers/transfer-stock.component.ts)
- **Defect**: Only displays recent transactions. Has no "New Transfer" action, form, or source/destination bin selection.

### 4.6 Stock Count (Physical Inventory) — Status: `NOT IMPLEMENTED`
- **Component**: [`count-adjust.component.ts`](file:///z:/ERP/Frontend/Web/src/app/features/domain/workspace/inventory/count-adjust/count-adjust.component.ts)
- **Defect**: Displays product catalog items from `masterDataService.getProducts()`. Does not call `StockCountController` (`/api/stock-counts`), has no draft creation, physical count recording table, variance display, or approval/posting workflow.

### 4.7 Dispatch Note (Sales Shipment) — Status: `PARTIAL`
- **Component**: [`sales-shipment-page.component.ts`](file:///z:/ERP/Frontend/Web/src/app/features/domain/workspace/sales/dispatches/sales-shipment-page.component.ts)
- **Features**: Displays `batchNumber` in read-only view table. Automated dispatch from order works via backend FEFO.
- **Defect**: Manual shipment draft line entry lacks interactive batch picker or manual batch allocation override.

---

## 5. Component Reuse and PrimeNG Assessment

### 5.1 Reusable Angular Components Evaluated
- **[`AppPageHeaderComponent`](file:///z:/ERP/Frontend/Web/src/app/shared/components/page-header/page-header.component.ts)**: Standard across all 7 workflows. Conforms to UI guidelines.
- **[`AppEditableDataTableComponent`](file:///z:/ERP/Frontend/Web/src/app/shared/components/data-table/editable-data-table.component.ts)**:
  - Extensively used in GRN, Putaway, Stock Receipt, Stock Issue.
  - Supports `editType: 'text'`, `'number'`, `'date'`, `'select'`.
  - Capable of rendering batch inputs, manufacturing dates, expiry dates, and destination location dropdowns without adding third-party libraries.
- **[`AppStatusBadgeComponent`](file:///z:/ERP/Frontend/Web/src/app/shared/components/status-badge/status-badge.component.ts)**: Used consistently for Draft, Confirmed, Posted, Dispatched statuses.
- **[`AccessDeniedComponent`](file:///z:/ERP/Frontend/Web/src/app/shared/components/access-denied/access-denied.component.ts)**: Correctly embedded across inventory and procurement screens.

### 5.2 PrimeNG Assessment
- The project uses PrimeNG selectively: `ButtonModule`, `InputTextModule`, `DialogModule`, `TooltipModule`.
- **Policy Compliance**: Meets project instruction Rule 8 & 15. No third-party popup or competing UI grid frameworks are introduced.
- **Recommendation**: For batch selection in Stock Issue, Putaway, and Dispatch, reuse `AppEditableDataTableComponent` with `editType: 'select'` or `editType: 'text'`, populated from `BatchController.GetEligibleBatches`. Do not create overlay popups.

---

## 6. Backend / Frontend Integration Findings

| Finding ID | Workflow | Severity | Description | Evidence |
| :--- | :--- | :--- | :--- | :--- |
| **INT-01** | Product Receipt (GRN) | **High** | GRN UI cannot capture batch numbers or dates. Confirmation of batch-tracked goods received via UI fails in backend validation. | `EditableGRNLine` in `goods-receipt-page.component.ts` (lines 43-62) vs `GoodsReceiptNoteLine.cs` (lines 28-35). |
| **INT-02** | Putaway | **Medium** | Batch number not visible in Putaway line table. Operators cannot identify which batch is placed in which storage location. | `EditablePutawayLine.batchNumber` defined but omitted from `detailColumns` in `store-putaway-page.component.ts` (lines 176-253). |
| **INT-03** | Stock Issue | **Low** | Free-text batch number input requires operators to know the exact batch string rather than selecting from available inventory batches. | `stock-transaction-form.component.ts` line 468 (`editType: 'text'`). |
| **INT-04** | Inventory Item Transfer | **Critical** | Frontend `/app/inventory/transfers` has no transfer form or endpoint connection; backend has no transfer controller. | `transfer-stock.component.ts` (lines 172-197) and `InventoryController.cs`. |
| **INT-05** | Stock Count | **Critical** | Frontend `/app/inventory/count-adjust` is completely detached from backend `StockCountController`. | `count-adjust.component.ts` calls `getProducts()` instead of `/api/stock-counts`. |

---

## 7. Security and Permission Findings

### 7.1 Endpoint Authorization Matrix

| Endpoint | Controller | Class Level | Action Level | Status |
| :--- | :--- | :--- | :--- | :--- |
| `POST .../goods-receipts/draft` | `GoodsReceiptNoteController` | `[RequirePermission("GOODS_RECEIPT.VIEW")]` | `[RequirePermission("GOODS_RECEIPT.CREATE")]` | **PASS** |
| `POST .../goods-receipts/{id}/confirm` | `GoodsReceiptNoteController` | `[RequirePermission("GOODS_RECEIPT.VIEW")]` | `[RequirePermission("GOODS_RECEIPT.CONFIRM")]` | **PASS** |
| `POST .../putaways` | `PutawayController` | `[RequirePermission("INVENTORY.VIEW")]` | `[RequirePermission("PUTAWAY.POST")]` | **PASS** |
| `POST .../putaways/{id}/confirm` | `PutawayController` | `[RequirePermission("INVENTORY.VIEW")]` | `[RequirePermission("PUTAWAY.POST")]` | **PASS** |
| `POST /api/inventory/stock-receipts` | `InventoryController` | `[RequirePermission("INVENTORY.VIEW")]` | `[RequirePermission("STOCK_RECEIPT.POST")]` | **PASS** |
| `POST /api/inventory/stock-issues` | `InventoryController` | `[RequirePermission("INVENTORY.VIEW")]` | `[RequirePermission("STOCK_ISSUE.POST")]` | **PASS** |
| `POST /api/stock-counts` | `StockCountController` | *(Inherits BaseApiController)* | `[RequirePermission("STOCK_COUNT.CREATE")]` | **PASS** |
| `POST /api/stock-counts/{id}/post` | `StockCountController` | *(Inherits BaseApiController)* | `[RequirePermission("STOCK_COUNT.POST")]` | **PASS** |
| `POST .../sales-shipments/from-order/{id}/dispatch` | `SalesShipmentController` | `[RequirePermission("SALES_SHIPMENT.VIEW")]` | `[RequirePermission("SALES_SHIPMENT.DISPATCH")]` | **PASS** |
| `GET /api/inventory/batches/eligible` | `BatchController` | `[RequirePermission("BATCH.VIEW,PRODUCT.VIEW")]` | *(Class Level)* | **PASS** |

### 7.2 Multi-Tenant & Scope Enforcement
- All controllers enforce tenant isolation via `_currentUserContext.TenantId`.
- Nested company/branch routes (`/api/companies/{companyId}/branches/{branchId}/...`) invoke `ValidateScopeAccess` using `_userAccessService.CanAccessCompanyAsync` and `CanAccessBranchAsync`.
- Cross-tenant data leakage is prevented at the database query level (`b.TenantId == tenantId`).

---

## 8. Cross-Workflow Business Consistency Findings

### 8.1 Inventory Lifecycle Traceability

```
[Purchase Order]
       │
       ▼ (Workflow 1: Product Receipt / GRN)
[Goods Receipt Note Line] ────► Inflow Posting: Creates ProductBatch + Location Balance (Receiving Dock)
       │
       ▼ (Workflow 2: Putaway)
[Putaway Transfer] ───────────► Transfer Posting: Moves Batch from Receiving Dock to Bin
       │
       ├──► (Workflow 4: Stock Issue) ───► Outflow Posting: Reduces Bin Batch Balance
       │
       ├──► (Workflow 5: Item Transfer) ─► [GAP: Missing Controller] Internal PostTransferAsync
       │
       ├──► (Workflow 6: Stock Count) ───► Variance Posting: StockAdjustmentPlus / StockAdjustmentMinus
       │
       ▼ (Workflow 7: Dispatch Note)
[Sales Shipment] ─────────────► FEFO Allocation ──► Outflow Posting: Depletes Batch Balance
```

### 8.2 Inconsistencies Identified
1. **Broken Chain from GRN to Putaway in UI**: Because the GRN UI does not capture batch numbers, received lines enter the staging location without batch identification unless posted via API/test scripts.
2. **Missing Inter-Location Transfer**: Moving a batch between two storage locations (e.g. from Bulk Storage to Pick Face) cannot be done through an ad-hoc transfer screen; operators must awkwardly do a manual Stock Issue followed by a manual Stock Receipt, losing continuous transfer transaction audit history.
3. **FEFO Enforcement Asymmetry**: FEFO is enforced automatically during Sales Order Dispatch, but manual Stock Issues rely on manual batch entry without FEFO warnings for older expiring batches.

---

## 9. Existing Test Coverage and Execution Results

### 9.1 Existing Automated Test Inventory
- **`ProductBatchMasterAndReceiptTests.cs`**:
  - Covers GRN receipt, batch creation, batch date preservation, conflicting expiry date rejection.
- **`StockReceiptBatchIntegrationTests.cs`**:
  - Covers ad-hoc stock receipts with batches, cost layer creation, date inheritance.
- **`StockIssueBatchIntegrationTests.cs`**:
  - Covers ad-hoc stock issues, insufficient stock rejection, inactive batch rejection, expired batch rejection.
- **`FEFOPickingAndExpiryDispatchTests.cs`**:
  - Covers FEFO sorting, expiry dispatch gating.
- **`PutawayPostingIntegrationTests.cs`**:
  - Covers putaway posting from staging to destination with batch preservation.
- **`StockCountSqlIntegrationTests.cs`**:
  - Covers physical inventory variance posting with batch tracking.

### 9.2 Test Execution Status
- All dedicated batch integration tests pass when executed against the domain database.
- Angular frontend build compiles cleanly (`ng build`: 0 errors).

---

## 10. Defect and Inconsistency Register with Severity

| Defect ID | Workflow | Severity | Description | Recommended Resolution |
| :--- | :--- | :--- | :--- | :--- |
| **DEF-BATCH-01** | Product Receipt | **High** | `goods-receipt-page.component.ts` lacks batch number, manufacturing date, and expiry date fields on receipt lines. | Add batch and date columns to `EditableGRNLine` and `itemColumns` in `GoodsReceiptPageComponent`. |
| **DEF-BATCH-02** | Putaway | **Medium** | `store-putaway-page.component.ts` does not display batch numbers in the putaway lines table. | Add `batchNumber` read-only column to `detailColumns` in `StorePutawayPageComponent`. |
| **DEF-BATCH-03** | Item Transfer | **High** | No API controller or service endpoint for inventory item transfers; frontend `transfer-stock.component.ts` is only a read-only list. | Add `TransferStock` endpoint in `InventoryController.cs` and implement transfer creation form in `TransferStockComponent`. |
| **DEF-BATCH-04** | Stock Count | **High** | `count-adjust.component.ts` is a stub loading products catalog; completely disconnected from backend `StockCountController`. | Implement physical count recording, variance grid, and submit/approve/post actions wired to `/api/stock-counts`. |
| **DEF-BATCH-05** | Stock Issue | **Low** | Free-text batch input in `stock-transaction-form.component.ts` lacks dropdown of available batches at the selected location. | Integrate with `GET /api/inventory/batches/eligible` to provide selectable batch options. |
| **DEF-BATCH-06** | Dispatch Note | **Low** | Manual shipment draft line entry lacks interactive batch picker. | Add batch selector to manual shipment line items or default to order-based dispatch. |

---

## 11. Recommended Implementation Sequence and Dependencies

```
Step 1: Fix Product Receipt Batch Entry (DEF-BATCH-01)
  └── Enables capturing batch numbers & expiry dates at receiving dock.

Step 2: Surface Batch Numbers in Putaway (DEF-BATCH-02)
  └── Allows operators to see and verify batches being put away into bins.

Step 3: Implement Item Transfer API & UI (DEF-BATCH-03)
  └── Exposes backend PostTransferAsync via InventoryController and adds transfer form in UI.

Step 4: Connect Stock Count UI to Backend (DEF-BATCH-04)
  └── Replaces catalog stub with functional stock count draft, count recording, and variance posting.

Step 5: Enhance Batch Selection in Stock Issue & Dispatch (DEF-BATCH-05, DEF-BATCH-06)
  └── Replaces raw text inputs with eligible batch selectors.
```

---

## 12. Acceptance Criteria for Outstanding Items

1. **Product Receipt (GRN)**:
   - When receiving a product configured with `TrackingMode.Batch`, the GRN editable table requires a non-empty `BatchNumber`.
   - Optional or mandatory `ManufacturingDate` and `ExpiryDate` can be entered in the line item.
   - On confirmation, the batch number and dates persist in `ProductBatch` and `InventoryTransaction`.
2. **Putaway**:
   - The putaway line table displays the `BatchNumber` associated with each incoming staging line.
   - When splitting a line across multiple destination bins, the batch number is preserved across all allocations.
3. **Item Transfer**:
   - `POST /api/inventory/transfers` accepts source warehouse/location, destination warehouse/location, product, quantity, and batch number.
   - Outflow and inflow transactions are created atomically with matching batch number and valuation.
4. **Stock Count**:
   - Warehouse operators can create a count draft for a warehouse/location, record physical counts per batch, review calculated system variance, submit, approve, and post adjustments.
   - Posted adjustments create traceable `StockAdjustmentPlus` or `StockAdjustmentMinus` movements with batch numbers.
5. **Stock Issue**:
   - The batch input provides a list of batches with positive available stock at the selected location, showing expiry dates to prevent picking expired lots.

---
*End of Audit Report.*
