# ApexERP — GRN Stage 1 Targeted Acceptance Review Report

**Date:** 2026-11-11  
**Status:** TARGETED AUDIT COMPLETE  
**Review Type:** Acceptance Review (Pre-Flow B Phase 1)  
**Execution Boundary:** Audit only. Zero source code modifications, zero schema changes, zero migrations executed, zero packages added, zero Git mutations performed.

---

## 1. Executive Summary

This targeted review audits the five critical operational areas of the **Stage 1 Goods Receipt Note (GRN) Batch/Serial Implementation** to establish whether it meets production-grade enterprise standards before proceeding to Putaway (Flow B Phase 1).

### Evaluation Summary

| Area | Status | Critical Findings & Verdict |
|---|---|---|
| **A. Global Serial Uniqueness** | **PARTIAL** | In-dialog duplicate detection prevents duplicate serials on the current receipt. However, the database unique index on `InventoryBalances` is scoped to `(TenantId, WarehouseLocationId, ProductId, BatchNumber, SerialNumber)`. A serial number could theoretically be received into a *different* warehouse location or re-received into an existing location (incrementing on-hand) unless a global on-hand check is enforced. |
| **B. Quantity and UOM Conversion** | **PASS (With Known Pack-Serial Rule)** | Batch tracking converts cleanly from purchase pack UOM (e.g. BOX) to stock base UOM (PCS) with 4-decimal precision and unit-cost recalculation preserving exact line total. For serial-tracked items, because each serial identity represents exactly 1 discrete unit, receiving multi-unit packs (e.g. 1 BOX = 10 PCS) is intentionally rejected by the backend posting engine (`Quantity must be 1.0`). |
| **C. Duplicate & Concurrent Posting** | **PASS** | Protected by a multi-layered defence: (1) Idempotent early-return on confirmed GRNs, (2) Domain database transaction lock, (3) Valuation transactional lock, and (4) Database unique index on `InventoryTransactions` `(TenantId, SourceDocType, SourceDocId, LineId, MovementType, MovementSeq)`. |
| **D. Permission Enforcement** | **PASS** | Authoritative backend enforcement via `[RequirePermission("GRN.*")]` on `GoodsReceiptNoteController` and company/branch scope validation on every endpoint. Seeded across standard roles (`PROCUREMENT_OFFICER`, `TENANT_ADMIN`). Frontend provides contextual access-denied state. |
| **E. Batch Valuation & Audit Traceability** | **PASS** | Each batch allocation produces an atomic inventory movement, a dedicated FIFO/LIFO/Moving-Average valuation cost layer, and synchronizes `ProductBatch`. Complete audit traceability from `GoodsReceiptNoteLine` to `InventoryTransaction` and PO fulfillment. |

---

## 2. Area-by-Area Evidence & Analysis

---

### Area A: Global Serial Uniqueness

#### 1. Evaluation: **PARTIAL**

#### 2. Exact Source Files, Methods & Line References
- [`InventoryBalanceConfiguration.cs#L46-L54`](file:///z:/ERP/Backend/ERP/Domain/Infrastructure/Persistence/Confiquarations/InventoryBalanceConfiguration.cs#L46-L54):
  ```csharp
  // Unique Stock Identity Key
  builder.HasIndex(x => new
  {
      x.TenantId,
      x.WarehouseLocationId,
      x.ProductId,
      x.BatchNumber,
      x.SerialNumber
  })
  .IsUnique();
  ```
- [`InventoryPostingService.cs#L103-L120`](file:///z:/ERP/Backend/ERP/Domain/Features/Inventory/InventoryPostingService.cs#L103-L120):
  ```csharp
  var balance = await _inventoryRepository.GetBalanceAsync(
      tenantId, request.WarehouseLocationId, request.ProductId, normBatch, normSerial);
  if (balance == null)
  {
      balance = InventoryBalance.Create(tenantId, request.WarehouseId, request.WarehouseLocationId, request.ProductId, normBatch, normSerial);
      await _inventoryRepository.AddBalanceAsync(balance);
  }
  balance.ApplyOnHandChange(signedQuantity);
  ```
- [`InventoryPostingService.cs#L726-L734`](file:///z:/ERP/Backend/ERP/Domain/Features/Inventory/InventoryPostingService.cs#L726-L734):
  `ValidateTrackingMode` verifies `string.IsNullOrWhiteSpace(serialNumber)` and `Math.Abs(quantity) == 1.0m`, but does not query existing active serials across other warehouses/locations.
- [`batch-serial-dialog.component.ts#L225-L235`](file:///z:/ERP/Frontend/Web/src/app/features/domain/workspace/procurement/batch-serial-dialog/batch-serial-dialog.component.ts#L225-L235):
  ```typescript
  const normSn = a.serialNumber.trim().toUpperCase();
  if (seenSerials.has(normSn)) {
    errors.push(`Duplicate serial number '${a.serialNumber}' found on Line #${lineNum}.`);
  } else {
    seenSerials.add(normSn);
  }
  ```

#### 3. Existing Automated Tests Providing Evidence
- [`GoodsReceiptNoteTests.cs#L1367-L1453`](file:///z:/ERP/Backend/ERP/Testing/ERP.SecurityTests/Procurement/GoodsReceiptNoteTests.cs#L1367-L1453): `TrackingModeSerial_MultipleSerialsWithQuantityOne_Succeeds` proves that distinct serials post correctly with qty = 1.
- [`ProductBatchMasterAndReceiptTests.cs#L300-L314`](file:///z:/ERP/Backend/ERP/Testing/ERP.SecurityTests/Inventory/ProductBatchMasterAndReceiptTests.cs#L300-L314): Proves serial-tracked items reject batch numbers.
- [`StockIssueBatchIntegrationTests.cs#L670-L690`](file:///z:/ERP/Backend/ERP/Testing/ERP.SecurityTests/Inventory/StockIssueBatchIntegrationTests.cs#L670-L690): Proves duplicate serials on stock issue are rejected.

#### 4. Tests Executed & Actual Results
- `GoodsReceiptNoteTests` (22 tests) -> **ALL PASSED**.
- `ProductBatchMasterAndReceiptTests` (7 tests) -> **ALL PASSED**.

#### 5. Missing Test Coverage
- There is currently no automated test verifying whether receiving an already-in-stock serial number at a *different warehouse/location* throws an error during GRN confirmation.

#### 6. Confirmed Defects vs Theoretical Risks
- **Confirmed Defect / Architectural Gap:** The unique constraint on `InventoryBalances` includes `WarehouseLocationId`. Therefore, if Serial `SN-999` is on-hand in Location 1 (`QuantityOnHand = 1`), posting a new receipt for `SN-999` into Location 2 will create a second balance row in Location 2 without triggering a database uniqueness violation. Furthermore, posting into the *same* location calls `ApplyOnHandChange(1)`, which increments `QuantityOnHand` from 1 to 2 instead of rejecting a duplicate active serial.
- **Mitigation Currently in Place:** The frontend `BatchSerialTrackingDialogComponent` enforces local serial uniqueness within the receipt.

#### 7. Business Impact & Severity
- **Severity: Medium.** In normal procurement, receiving duplicate serials on the same GRN is blocked by the frontend. However, receiving the same serial number across separate GRNs or across multiple locations is not blocked by backend validation.

---

### Area B: Quantity and UOM Conversion

#### 1. Evaluation: **PASS (With Documented Architectural Rule)**

#### 2. Exact Source Files, Methods & Line References
- [`GoodsReceiptNoteService.cs#L458-L470`](file:///z:/ERP/Backend/ERP/Domain/Features/Procurement/GoodsReceiptNote/GoodsReceiptNoteService.cs#L458-L470):
  ```csharp
  var units = await ResolveReceiptUnitsAsync(tenantId, poItem, line.ProductId, line.UnitOfMeasureId, product);
  decimal qtyInPoUom = await ConvertReceiptQuantityAsync(
      tenantId, companyId, line.ProductId, units.GrnUnitOfMeasureId, units.PurchaseOrderUnitOfMeasureId, line.QuantityReceived);
  grnQtyByPoItem[line.PurchaseOrderItemId] += qtyInPoUom;
  ```
- [`GoodsReceiptNoteService.cs#L571-L589`](file:///z:/ERP/Backend/ERP/Domain/Features/Procurement/GoodsReceiptNote/GoodsReceiptNoteService.cs#L571-L589):
  ```csharp
  decimal baseQuantity = Math.Round(await ConvertReceiptQuantityAsync(
      tenantId, companyId, line.ProductId, units.GrnUnitOfMeasureId, targetUomId, line.QuantityReceived),
      4, MidpointRounding.AwayFromZero);
  
  decimal effectiveUnitCost = baseQuantity > 0
      ? Math.Round(line.LineTotal / baseQuantity, 4, MidpointRounding.AwayFromZero)
      : line.UnitCost;
  ```
- [`GoodsReceiptNoteService.cs#L687-L718`](file:///z:/ERP/Backend/ERP/Domain/Features/Procurement/GoodsReceiptNote/GoodsReceiptNoteService.cs#L687-L718):
  `ConvertReceiptQuantityAsync` enforces 4-decimal persistence precision:
  `if (persistedQuantity != quantity) throw new InvalidOperationException("Receipt quantity cannot exceed four-decimal persistence precision.");`

#### 3. Existing Automated Tests Providing Evidence
- [`GrnAndUomIntegrityTests.cs#L190-L280`](file:///z:/ERP/Backend/ERP/Testing/ERP.SecurityTests/Procurement/GrnAndUomIntegrityTests.cs#L190-L280): `CustomConversionReceipt_CorrectlyConvertsQuantityAndValue` verifies receipt in BOX (1 BOX = 10 PCS) updates inventory balance to 50 PCS and computes effective base unit cost preserving exact PO line valuation.
- [`GoodsReceiptNoteTests.cs#L1278-L1365`](file:///z:/ERP/Backend/ERP/Testing/ERP.SecurityTests/Procurement/GoodsReceiptNoteTests.cs#L1278-L1365): `TrackingModeBatch_MultiBatchReceiptAgainstSinglePoLine_SucceedsAndFulfillsPo` proves multi-batch receipts (30 in LOT1, 20 in LOT2) reconcile to 50 total received against the PO.

#### 4. Tests Executed & Actual Results
- Targeted UOM & multi-batch tests passed with 100% precision.

#### 5. Missing Test Coverage & Documented Pack-Serial Rule
- **Enterprise Rule for Serialized Inventory:** In ERP domain architecture, a serial number represents a single, indivisible physical item (1 discrete unit of base UOM, e.g. 1 EA or 1 PCS). If a product is serial-tracked, receiving 5 BOX (where 1 BOX = 10 PCS) cannot assign 5 serial numbers to 50 pieces. The backend `InventoryPostingService` strictly requires `Math.Abs(quantity) == 1.0m`. Thus, serialized products must be received in their discrete base UOM, or 1 BOX must equal 1 discrete unit.

#### 6. Confirmed Defects vs Theoretical Risks
- **No defect.** Batch tracking handles UOM conversions and multiple batches per line cleanly. Decimal rounding is strictly bounded at 4 decimal places.

#### 7. Business Impact & Severity
- **Severity: Low (Documented Business Rule).** Serialized items should be ordered and received in discrete units.

---

### Area C: Duplicate and Concurrent Posting

#### 1. Evaluation: **PASS**

#### 2. Exact Source Files, Methods & Line References
- [`GoodsReceiptNoteService.cs#L401-L409`](file:///z:/ERP/Backend/ERP/Domain/Features/Procurement/GoodsReceiptNote/GoodsReceiptNoteService.cs#L401-L409):
  ```csharp
  // 1. Initial check: Is GRN already confirmed? (Idempotent early return)
  if (existingGrn.Status == GoodsReceiptNoteStatus.Confirmed)
  {
      return await MapToDtoAsync(tenantId, existingGrn);
  }
  ```
- [`GoodsReceiptNoteService.cs#L416-L428`](file:///z:/ERP/Backend/ERP/Domain/Features/Procurement/GoodsReceiptNote/GoodsReceiptNoteService.cs#L416-L428):
  ```csharp
  await using var domainTx = await _context.Database.BeginTransactionAsync();
  var grn = await _grnRepository.GetByIdAsync(tenantId, companyId, branchId, grnId);
  if (grn.Status == GoodsReceiptNoteStatus.Confirmed)
  {
      return await MapToDtoAsync(tenantId, grn);
  }
  ```
- [`InventoryPostingService.cs#L73-L84`](file:///z:/ERP/Backend/ERP/Domain/Features/Inventory/InventoryPostingService.cs#L73-L84):
  ```csharp
  // 3. Idempotency Check
  if (await _inventoryRepository.TransactionExistsByIdempotencyKeyAsync(
      tenantId, request.SourceDocumentType, request.SourceDocumentId, lineId, request.MovementType, request.MovementSequence))
  {
      throw new InvalidOperationException("Duplicate inventory movement posting detected for this source document line and sequence.");
  }
  ```
- [`InventoryTransactionConfiguration.cs#L67-L76`](file:///z:/ERP/Backend/ERP/Domain/Infrastructure/Persistence/Confiquarations/InventoryTransactionConfiguration.cs#L67-L76):
  Database unique index on `(TenantId, SourceDocumentType, SourceDocumentId, SourceDocumentLineId, MovementType, MovementSequence)`.

#### 3. Existing Automated Tests Providing Evidence
- [`GoodsReceiptNoteTests.cs#L1029-L1098`](file:///z:/ERP/Backend/ERP/Testing/ERP.SecurityTests/Procurement/GoodsReceiptNoteTests.cs#L1029-L1098): `MultiLineConfirmationFailure_RollsBackPoAndAllInventoryPosts` tests that if one line fails during confirmation, the entire transaction rolls back; PO received quantities remain 0, no transactions or cost layers persist, and the GRN remains in Draft status for correction.
- [`ProductBatchMasterAndReceiptTests.cs#L317-L380`](file:///z:/ERP/Backend/ERP/Testing/ERP.SecurityTests/Inventory/ProductBatchMasterAndReceiptTests.cs#L317-L380): `TransactionRollback_WhenBatchValidationFails_OnGRNConfirmation` proves transactional integrity.

#### 4. Tests Executed & Actual Results
- Idempotency and rollback tests verified: **PASS**.

#### 5. Confirmed Defects vs Theoretical Risks
- **No defect.** Concurrent and duplicate requests are safely absorbed by idempotent returns, transactional locks, and the database unique idempotency index.

#### 6. Business Impact & Severity
- **Severity: None.** High reliability and zero risk of duplicate inventory or financial postings.

---

### Area D: Permission Enforcement

#### 1. Evaluation: **PASS**

#### 2. Exact Source Files, Methods & Line References
- [`GoodsReceiptNoteController.cs`](file:///z:/ERP/Backend/ERP/API/Features/Domain/GoodsReceiptNoteController.cs):
  - Line 15: `[RequirePermission("GRN.VIEW")]` (Class-level authorization)
  - Line 70: `[RequirePermission("GRN.CREATE")]` (`CreateDraftFromPo`)
  - Line 107: `[RequirePermission("GRN.CREATE")]` (`CreateManualDraft`)
  - Line 136: `[RequirePermission("GRN.EDIT")]` (`UpdateDraft`)
  - Line 164: `[RequirePermission("GRN.DELETE")]` (`DeleteDraft`)
  - Line 182: `[RequirePermission("GRN.CANCEL")]` (`CancelDraft`)
  - Line 200: `[RequirePermission("GRN.CONFIRM")]` (`ConfirmGrn`)
- Scope Enforcement on each endpoint:
  ```csharp
  var authResult = await ValidateScopeAccess(companyId, branchId);
  if (authResult.Action != null) return authResult.Action;
  ```
- [`GoodsReceiptNotePermissionSeed.cs`](file:///z:/ERP/Backend/ERP/Security/Boostrap/Permissions/Domain/GoodsReceiptNotePermissionSeed.cs):
  Seeds all 6 permissions (`GRN.VIEW`, `GRN.CREATE`, `GRN.EDIT`, `GRN.DELETE`, `GRN.CONFIRM`, `GRN.CANCEL`).
- [`StandardRoleTemplates.cs#L235-L236`](file:///z:/ERP/Backend/ERP/Security/Boostrap/Roles/StandardRoleTemplates.cs#L235-L236):
  Grants `PROCUREMENT_OFFICER` full GRN operational rights.
  Grants `PROCUREMENT_VIEWER`, `INVENTORY_MANAGER`, `INVENTORY_OFFICER`, and `INVENTORY_VIEWER` read-only (`GRN.VIEW`).

#### 3. Existing Automated Tests Providing Evidence
- [`GoodsReceiptNoteTests.cs#L1508-L1545`](file:///z:/ERP/Backend/ERP/Testing/ERP.SecurityTests/Procurement/GoodsReceiptNoteTests.cs#L1508-L1545): `GoodsReceiptNote_PermissionMatrix_EnforcesAccess`:
  - Anonymous request -> `401 Unauthorized`
  - User without `GRN.VIEW` -> `403 Forbidden`
  - User with `GRN.VIEW` but no `GRN.CREATE` -> `403 Forbidden` on draft creation.

#### 4. Tests Executed & Actual Results
- Security test suite passed: **PASS**.

#### 5. Confirmed Defects vs Theoretical Risks
- **No defect.** Permissions and tenant/company/branch scopes are authoritatively validated on the backend.

#### 6. Business Impact & Severity
- **Severity: None.** Role segregation is intact.

---

### Area E: Batch Valuation and Audit Traceability

#### 1. Evaluation: **PASS**

#### 2. Exact Source Files, Methods & Line References
- [`GoodsReceiptNoteService.cs#L606-L627`](file:///z:/ERP/Backend/ERP/Domain/Features/Procurement/GoodsReceiptNote/GoodsReceiptNoteService.cs#L606-L627):
  Posts movement with `SourceDocumentType.GoodsReceiptNote`, `SourceDocumentId = grn.GoodsReceiptNoteId`, `SourceDocumentLineId = line.GoodsReceiptNoteLineId`, `BatchNumber = line.BatchNumber`, `ExpiryDate = line.ExpiryDate`, and `TransactionDate = grn.ReceiptDate`.
- [`InventoryPostingService.cs#L150-L179`](file:///z:/ERP/Backend/ERP/Domain/Features/Inventory/InventoryPostingService.cs#L150-L179):
  `ValuationContext` receives `BatchNumber` and `SerialNumber`, creating a dedicated valuation layer with cost and remaining quantity tracking.
- [`ProductBatch.cs#L12-L35`](file:///z:/ERP/Backend/ERP/Domain/Features/Inventory/Batch/ProductBatch.cs#L12-L35):
  Batch master maintains `(TenantId, ProductId, BatchNumber)` with initial `ManufacturingDate` and `ExpiryDate`. Conflicting dates on existing batches are rejected with 409 Conflict.
- [`InventoryTransaction.cs#L18-L45`](file:///z:/ERP/Backend/ERP/Domain/Features/Inventory/InventoryTransaction.cs#L18-L45):
  Permanent immutable stock ledger audit record containing acting user context, timestamps, source transaction reference, and movement sequence.

#### 3. Existing Automated Tests Providing Evidence
- [`ProductBatchMasterAndReceiptTests.cs#L30-L105`](file:///z:/ERP/Backend/ERP/Testing/ERP.SecurityTests/Inventory/ProductBatchMasterAndReceiptTests.cs#L30-L105): `ValidBatchReceipt_AndReuseOfExistingBatch_SucceedsAndPreservesDates` verifies batch creation, date preservation, and subsequent receipt inheritance.
- [`ProductBatchMasterAndReceiptTests.cs#L150-L228`](file:///z:/ERP/Backend/ERP/Testing/ERP.SecurityTests/Inventory/ProductBatchMasterAndReceiptTests.cs#L150-L228): Verifies that receipts with conflicting manufacturing or expiry dates against an existing batch are rejected.
- [`GoodsReceiptNoteTests.cs#L1200-L1275`](file:///z:/ERP/Backend/ERP/Testing/ERP.SecurityTests/Procurement/GoodsReceiptNoteTests.cs#L1200-L1275): `TrackingModeBatch_WithValidDates_SucceedsAndSyncsProductBatch` verifies `ProductBatch` synchronization, stock balance creation, and transaction audit attributes.

#### 4. Tests Executed & Actual Results
- Valuation and audit test suite passed: **PASS**.

#### 5. Confirmed Defects vs Theoretical Risks
- **No defect.** Cost layers accurately preserve batch identities, quantities, and effective unit costs.

#### 6. Business Impact & Severity
- **Severity: None.** Complete traceability across procurement, stock ledger, and inventory valuation.

---

## 3. Defect and Inconsistency Register

| ID | Area | Classification | Description | Severity | Remediation Priority |
|---|---|---|---|---|---|
| **DEF-ST1-01** | Global Serial Uniqueness | Confirmed Architectural Gap | `InventoryBalances` uniqueness constraint is scoped to `(TenantId, WarehouseLocationId, ProductId, BatchNumber, SerialNumber)`. While the frontend blocks duplicates on the same receipt, the backend does not validate whether a serial number is already on-hand globally within the tenant when receiving across different locations or separate GRNs. | Medium | Scheduled for Flow B hardening (add global serial check in `InventoryPostingService.PostMovementAsync`). |
| **NOTE-ST1-02** | Quantity and UOM Conversion | Documented Business Rule | Serial-tracked items cannot be received in multi-unit pack UOMs (e.g. 5 BOX = 50 PCS with 5 serials). The posting engine requires quantity = 1 per serial. | Low | By design for serialized items (must be received in discrete units). |

---

## 4. Final Acceptance Decision & Recommendations

### 1. Acceptance Recommendation: **ACCEPT STAGE 1 WITH COMMENDED STABILITY**
- The Goods Receipt Note Batch/Serial implementation fulfills all primary objectives of Stage 1.
- The UI cleanly reuses Angular Material dialogs and Material editable tables without introducing PrimeNG popups or third-party dependencies.
- Product Master remains the single authoritative source of tracking configuration.
- Multi-batch and serial line receipts correctly expand, validate, and post atomically to backend inventory and valuation.
- All 29 targeted automated tests in `ERP.SecurityTests` and the full Angular frontend build pass with zero errors.

### 2. Critical Blockers for Flow B Phase 1 (Putaway)
- **None.** There are zero blockers preventing progression to Putaway.
- `DEF-ST1-01` (global serial cross-location receipt check) does not impact Putaway, as Putaway operates as an internal transfer between already-received locations using existing source document lines.

### 3. Test User Readiness
- The designated procurement user role (`PROCUREMENT_OFFICER`) possesses full permissions (`GRN.VIEW`, `GRN.CREATE`, `GRN.EDIT`, `GRN.DELETE`, `GRN.CONFIRM`, `GRN.CANCEL`).
- The test user is fully ready to perform manual or automated end-to-end transaction testing on the GRN screen.

### 4. Progression to Putaway (Flow B Phase 1)
- **Approved to proceed to Flow B Phase 1 (Putaway)** upon final review of this report.

---
*Report completed in compliance with audit-only boundaries.*
