# ApexERP Retail Test Pack — Defect Register

## 1. Defect Classification Scheme

- **Critical:** System crash, data loss, security bypass, or unauthorized transaction posting.
- **High:** Functional blockage preventing workflow completion; business logic or costing calculation discrepancy.
- **Medium:** UI validation or error message inaccuracy; recoverable workaround available.
- **Low:** Cosmetic styling, spacing, or minor layout flaw.

---

## 2. Identified Defects & Resolutions

| Defect ID | Severity | Module | Description | Root Cause | Resolution | Status |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| `RET-DEF-001` | **High** | Inventory / Putaway | Putaway line matching fails when multiple lines exist for the same Product ID. | In `PutawayService.cs`, matching evaluated `|| (e.ProductId == lineReq.ProductId)` before checking `SourceDocumentLineId`. | Updated matching logic to strictly check `SourceDocumentLineId` first, preventing cross-line over-quantity rejections. | **RESOLVED** |
| `RET-DEF-002` | **High** | Inventory / Stock Count | Stock Count posting fails with Conflict when both write-off (-qty) and write-in (+qty) exist in the same count. | Both the internal `CreateStockIssueRequest` and `CreateStockReceiptRequest` used the same `StockCountId` as their source document ID, triggering duplicate transaction detection in `InventoryService.CreateStockReceiptAsync`. | In `StockCountService.PostAsync`, generated unique IDs and differentiated reference keys (`SC-ISSUE-{countId}` and `SC-RECEIPT-{countId}`). | **RESOLVED** |
| `RET-DEF-003` | **Medium** | Inventory / Batches | Batch date conflict rejection on existing batches. | GRN line confirmation threw HTTP 409 Conflict when batch date didn't match `ProductBatches` database record. | Documented strict batch date inheritance in `Batch-Register.md` and aligned test data with master registered dates. | **RESOLVED** |
| `RET-DEF-004` | **Medium** | Inventory / Putaway | Putaway draft creation rejected with "Line item is not part of source receipt" when passing SourceDocumentType 1 for GRN. | `PutawaySourceDocumentType` defines `StockReceipt = 1` and `GoodsReceipt = 2`. Passing 1 routed lookup to `StockReceipts`. | Configured `SourceDocumentType = 2` (`GoodsReceipt`) for GRN putaways and documented enum mapping in manual test guide. | **RESOLVED** |
