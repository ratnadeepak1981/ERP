# ApexERP — RFQ Stage 1: Requested Items Grid and Draft Editing Implementation Plan

**Document Reference:** `PLAN-RFQ-ITEMS-EDIT-001`  
**Target Branch:** `fix/rfq-stabilization`  
**Date:** November 17, 2026  
**Status:** Inspection Complete — Pending Explicit Approval to Implement  

---

## 1. Executive Summary & Root Cause Analysis

During manual testing under Tenant Admin (`flowadmin@apxflow.local`), two specific functional gaps were identified:

### Gap 1: Missing Requested Items Grid on New RFQ Page
- **Root Cause:** In `rfq-page.component.html`, the requested lines section was restricted behind `@if (!isNew() && activeTab() === 'lines' && rfq())`. When `isNew()` is true (`/app/procurement/rfqs/new`), only Tab 1 (General Details & Terms) was rendered, and the multi-tab navigation was hidden. As a result, the New RFQ page had no UI to search products, enter quantities, select UOMs, or define requested items. Furthermore, `saveRfq()` in `rfq-page.component.ts` only submitted header fields, omitting the `lines` array.
- **Business Impact:** An RFQ could be created as an empty header without requested items, leaving suppliers with nothing to quote.

### Gap 2: Missing Edit Action & Row Actions in RFQ Register
- **Root Cause:** 
  1. In `rfq.component.ts`, `tableColumns` defined columns for RFQ Number, Title, Status, Closing Date, Items, Invited Suppliers, Quotations, and Created On, but **omitted** `{ field: 'id', header: 'Actions', type: 'actions' }`. The shared `AppDataTableComponent` only renders the action buttons in table mode when a column with `type: 'actions'` is declared.
  2. The table actions only defined a generic `view-edit` action without a dedicated, prominent **"Edit Draft"** action for Draft RFQs (`RFQ-2026-0001` and `RFQ-2026-0002`).
  3. The `Status` column in `rfq.component.ts` had no badge mapping or enum formatter, displaying raw integer `1` instead of `Draft`.
- **Business Impact:** The user could not edit saved drafts from the register.

---

## 2. Existing Backend & Frontend Capabilities

### Existing Backend Capabilities (Fully Verified)
1. **Domain Entities:** `RequestForQuotation` and `RequestForQuotationLine` are already created with full EF Core configurations and decimal precision (`decimal(18,4)` for quantities and target prices).
2. **`RFQService.CreateRfqAsync`:** **Already supports lines!** It parses `CreateRfqRequest.Lines`, validates positive quantity, verifies product existence in the tenant scope, resolves UOM codes, assigns sequential line numbers, and persists header and lines atomically.
3. **Line Endpoints:** `POST {id}/lines` and `DELETE {id}/lines/{lineId}` already exist on `RequestForQuotationController`.
4. **Lifecycle Restrictions:** `RFQService.UpdateRfqAsync`, `AddLineAsync`, and `RemoveLineAsync` already enforce `rfq.Status == RequestForQuotationStatus.Draft`.

### Backend Capabilities to Enhance
1. **`UpdateRfqRequest` & `UpdateRfqAsync`:**
   Currently, `UpdateRfqRequest` only accepts header fields (`Title`, `ClosingDate`, `DeliveryRequiredDate`, `DeliveryTerms`, `Instructions`).  
   To allow atomic draft updating (updating header and requested items together in one transaction without multiple round-trips), `UpdateRfqRequest` will be extended with `public List<UpdateRfqLineDto>? Lines { get; set; }`.  
   `RFQService.UpdateRfqAsync` will reconcile lines: updating modified lines, adding new lines, and removing deleted lines within a single `SaveChangesAsync()` call.

### Existing Frontend Capabilities (Fully Verified)
1. **Master Data Cache:** `rfq-page.component.ts` already loads active products (`ProductItem[]`) and active UOMs (`UnitOfMeasureItem[]`) via `MasterDataService`.
2. **Tab 2 Template:** The HTML template already contains markup for product dropdown, quantity input, UOM dropdown, target price, required date, technical specs, and line table with delete action.

---

## 3. Detailed Proposed Solution

### 3.1 Requested Items Grid on New RFQ Page
- Embed the **Requested Items Grid** directly below the "Request for Quotation Information" card on `/app/procurement/rfqs/new` (and throughout Draft mode).
- Introduce a client-side reactive draft lines collection (`draftLines = signal<DraftRfqLineItem[]>([])`).
- **Required Grid Columns:**
  | Column | UI Control / Behaviour |
  |---|---|
  | `#` | Line number (1, 2, 3...) |
  | `Product` | Dropdown or search lookup from Product Master (`products()`) |
  | `Product Code` | Automatically populated from selected product |
  | `Product Name` | Automatically populated from selected product |
  | `Quantity` | Positive numeric input (`min="0.001"`, step="any"), required |
  | `UOM` | Dropdown from `uoms()`, defaulting to product's base UOM, required |
  | `Required Date` | Optional date picker, defaulting to RFQ delivery date |
  | `Target Unit Price` | Optional numeric input (internal procurement reference only) |
  | `Technical Specifications` | Optional text input for quality/ISO standards |
  | `Actions` | Edit / Remove line button |
- **Add Item Form/Bar:** Includes "Add Item" button, instant validation (product selected, quantity > 0, valid UOM).
- **Empty State:** When no lines are added, displays `<app-empty-state>` instructing the user to add product items.
- **Save Draft Validation:** Validates that at least one product line is present before saving draft RFQ.
- **Atomic Save:** Sends `CreateRfqRequest` with `lines: [...]`.

### 3.2 Edit Existing Draft RFQs (`RFQ-2026-0001`, `RFQ-2026-0002`)
- **Register Navigation:** In `rfq.component.ts`, clicking **"Edit Draft"** on any Draft row navigates to `/app/procurement/rfqs/{id}`.
- **Data Loading:** `loadRfq(id)` loads the existing RFQ. It populates the header fields and initializes `draftLines` from `rfq.lines`.
- **Edit Workflow:**
  - Header fields remain editable.
  - Existing lines are displayed in the Requested Items grid.
  - User can add new lines, edit existing line quantities/UOMs/prices, or remove lines.
  - Clicking **"Save Changes"** validates lines and sends `UpdateRfqRequest` with the complete updated `lines` array.
  - Backend `UpdateRfqAsync` updates header and synchronizes lines atomically.
  - RFQ number, TenantId, CompanyId, BranchId, CreatedAt, and CreatedBy remain completely unchanged.
  - If existing drafts currently have 0 lines, user can add lines and save them.

### 3.3 Register Display & Actions Fix
- In `rfq.component.ts`:
  - Add `{ field: 'id', header: 'Actions', width: '220px', type: 'actions' }` to `tableColumns`.
  - Add dedicated `edit-draft` action (`label: 'Edit Draft'`, `icon: 'pi pi-pencil'`, `severity: 'primary'`) visible strictly when `rfq.status === 'Draft'` (or `1`).
  - Add `view-rfq` action (`label: 'View'`, `icon: 'pi pi-eye'`, `severity: 'secondary'`) visible for non-Draft RFQs (`Issued`, `Closed`, `Awarded`, `Cancelled`).
  - Update `Status` column in `tableColumns` to `type: 'badge'`, mapping integer `1` / string `'Draft'` to `'Draft'` label with `'neutral'`/`'info'` severity.

---

## 4. Exact Files Expected to Change

| # | File Path | Scope of Change |
|---|---|---|
| 1 | `Backend/ERP/Domain/Features/Procurement/RequestForQuotation/RfqDtos.cs` | Add `UpdateRfqLineDto`; add `List<UpdateRfqLineDto>? Lines` to `UpdateRfqRequest`. |
| 2 | `Backend/ERP/Domain/Features/Procurement/RequestForQuotation/RFQService.cs` | In `UpdateRfqAsync`, implement atomic line synchronization (add, update, delete) while strictly preserving Draft lifecycle check and line numbering. |
| 3 | `Frontend/Web/src/app/core/models/rfq.models.ts` | Update `UpdateRfqRequest` and DTOs with `UpdateRfqLineDto`. |
| 4 | `Frontend/Web/src/app/features/domain/workspace/procurement/rfq.component.ts` | Add `actions` column to `tableColumns`; configure `edit-draft` vs `view-rfq` actions; add status badge formatting. |
| 5 | `Frontend/Web/src/app/features/domain/workspace/procurement/rfq-page.component.ts` | Implement `draftLines` signal, add/edit/remove line methods, populate lines on edit, update `saveRfq()` to include lines for both Create and Update. |
| 6 | `Frontend/Web/src/app/features/domain/workspace/procurement/rfq-page.component.html` | Embed Requested Items grid on New RFQ and Draft edit view with product lookup, inputs, table, and empty state. |

---

## 5. Database & Migration Assessment

- **Database Schema Changes Required:** **NONE**.
- **EF Core Migrations Required:** **NONE**.
- **Explanation:** Tables `RequestForQuotations` and `RequestForQuotationLines` already exist in `DomainDbContext` with all necessary columns, constraints, foreign keys, and indexes. No columns or tables are being added, altered, or deleted.

---

## 6. Security, Permissions & Lifecycle Boundaries

1. **Permission Enforcement:**
   - Creating RFQ requires `RFQ.CREATE`.
   - Editing Draft RFQ requires `RFQ.EDIT`.
   - Tenant Admin retains all RFQ permissions.
2. **Scope Isolation:**
   - Multi-tenant query filters (`TenantId`, `CompanyId`, `BranchId`) are strictly enforced on all queries and mutations in `RequestForQuotationController` and `RFQService`.
3. **Lifecycle Rules:**
   - Only `Draft` RFQs can be edited or have lines added/modified/deleted.
   - Once an RFQ is `Issued`, `Closed`, or `Cancelled`, backend throws `InvalidOperationException`.
4. **Internal Price Privacy:**
   - `TargetUnitPrice` and internal notes remain strictly on internal endpoints and are never mapped to `PublicRfqDetailsDto`.

---

## 7. Risks & Mitigation Plan

| Risk | Mitigation |
|---|---|
| Existing draft RFQs (`RFQ-2026-0001`, `RFQ-2026-0002`) corrupted or overwritten | Reopening existing drafts updates records in place using their existing IDs; IDs and RFQ numbers are never regenerated. |
| Line item orphaned or desynchronized on update | `RFQService.UpdateRfqAsync` handles line synchronization in a single EF Core transaction; if any line validation fails, the entire transaction rolls back. |
| Missing UOM or Product references | Products and UOMs are validated against `TenantId` in `_dbContext.Products` and `_dbContext.UnitsOfMeasure`. |
| Angular compilation errors | Strict adherence to existing `AppDataTableComponent` patterns, signals, and TypeScript DTOs. |

---

## 8. Verification & Test Plan

1. **Test 1: New RFQ with Multiple Items**
   - Navigate to `/app/procurement/rfqs/new`.
   - Fill Title: `Laboratory Equipment FY2026`.
   - Add Product Line 1: Samba Rice (100 BAG, Target Price: 50.00).
   - Add Product Line 2: Basmati Rice (50 BAG, Target Price: 80.00).
   - Add Product Line 3: Sugar (200 KG, Target Price: 15.00).
   - Click "Save Draft". Confirm RFQ is created with a generated number (e.g. `RFQ-2026-0003`) and 3 lines.
2. **Test 2: Reopen and Edit Draft**
   - In RFQ Register, click "Edit Draft" on `RFQ-2026-0003`.
   - Verify header and all 3 lines load correctly.
   - Modify title to `Laboratory Equipment FY2026 - Revised`.
   - Modify Line 1 quantity from 100 to 120.
   - Remove Line 3.
   - Add Line 4 with a new product.
   - Click "Save Changes".
   - Reload from database and confirm: title updated, Line 1 quantity is 120, Line 3 removed, Line 4 present, RFQ ID and RFQ number unchanged.
3. **Test 3: Existing Drafts (`RFQ-2026-0001`, `RFQ-2026-0002`)**
   - In RFQ Register, click "Edit Draft" on `RFQ-2026-0001`.
   - Add 2 requested product lines and save.
   - Confirm lines are successfully persisted and displayed.
4. **Test 4: Lifecycle Restrictions**
   - Attempt to call `PUT /rfqs/{id}` on an `Issued`, `Closed`, or `Cancelled` RFQ.
   - Verify backend returns `400 Bad Request` / `InvalidOperationException`.
5. **Test 5: Build & Compilation Verification**
   - Run `dotnet build Backend/ERP/API/API.csproj`.
   - Run Angular TypeScript check `tsc --noEmit`.

---
*End of Inspection and Implementation Plan. Awaiting explicit user approval before beginning implementation.*
