# ApexERP Backend Inventory Regression Assessment

## Assessment scope and environment

- **Assessment date:** 2026-11-15
- **Repository:** `ratnadeepak1981/ERP`
- **Branch / commit:** `feature/stock-ledger-all-locations` / `d4c7c28df6cb771f298d9bcfe79dbf219d9fcc03`
- **Working tree:** Already contained numerous staged/unstaged/untracked changes. They were preserved. This report is the only file created for this assessment; the existing `APEXERP-CONTROLLED-INVENTORY-REGRESSION-INVESTIGATION.md` was not edited.
- **Runtime:** `global.json` selects .NET SDK `9.0.306`; backend projects target .NET 8.
- **Tenant/environment:** No tenant was selected or accessed for this backend-only assessment. No login, transaction, or database query was performed. Tenant-level results cannot be inferred from these source/build checks.
- **Application version:** Not independently established.
- **Restrictions observed:** No source, configuration, permission, or database changes; no migrations, resets, package installs, or Git operations that alter history.

## Active implementation

The API registrations select the following implementations, rather than the legacy inventory services in `Domain`:

| Responsibility | Active implementation |
|---|---|
| Inventory application operations | `ERP.Application.Features.Inventory.Management.InventoryService` |
| Posting and balance mutation | `ERP.Application.Features.Inventory.InventoryPosting.InventoryPostingService` |
| Putaway | `ERP.Application.Features.Inventory.Putaway.PutawayService` |
| Persistence coordination / reports | `ERP.Infrastructure.Persistence.InventoryPersistenceCoordinator` |
| Inventory and cost-layer repository | `ERP.Infrastructure.Repositories.Inventory.InventoryRepository` |
| FIFO valuation | `ERP.Application.Features.Inventory.Valuation.FifoValuationStrategy` |
| Product updates | `ERP.Application.Features.MasterData.Product.ProductService` and `Domain.Features.MasterData.Product.Product` |

DI registrations are in `Backend/ERP/API/Confiquration/ServiceRegistration.cs:100-105` and `RepositoryRegistration.cs:65-66`.

## Automated test and build results

| Check | Result | Details |
|---|---|---|
| `StockCountOrchestrationTests` | **PASS** | 11 passed, 0 failed, 0 skipped. This isolated in-memory test class does not exercise receipt, issue, batch, Putaway, or FIFO workflows. |
| API build with redirected output | **PASS** | API and referenced backend projects built with 0 warnings and 0 errors. Output was redirected to avoid the locked normal output directory. |
| Full solution build to normal output | **BLOCKED** | The initial normal-output build hit assembly-copy errors (`MSB3027`/`MSB3021`) because the running API and Visual Studio held files in `Backend/ERP/API/bin/Debug/net8.0`. |
| Full solution build to isolated output | **PASS** | Retried with `dotnet build .\Backend\ERP\ERP.sln --no-restore -p:BaseOutputPath="<session files>\backend-solution-build\"`; all solution projects built with 0 errors and 4 warnings. Output was kept outside the repository. The warnings are listed below. |
| Database-backed inventory/security tests | **BLOCKED** | Not run. `TestSecurityFixture` runs `Database.Migrate()` and `TestSeeder.SeedAll(...)` against LocalDB databases named `PlatformDB_Test`, `SecurityDB_Test`, `DomainDB_Test`, and `AIDatabase_Test` (`TestConstants.cs:8-11`; fixture lines 101-105). Running these would modify databases, contrary to the no-database-modification restriction. |
| Baseline comparison | **NOT TESTED** | No clean baseline was available. The solution build emitted warnings in test files; it is not possible to classify them as newly introduced or pre-existing from this run. |

The existing suite contains tests relevant to inventory posting, batch issue, transfers/valuation, and other inventory areas, but those database-backed tests were not executed. The inspected tracking-mode update path had no targeted update-with-existing-stock test identified. No package was installed.

The successful full solution build emitted these four warnings: `CurrencyTests.cs:213` (`CS1998`), `TenantSlugTests.cs:257` (`CS8604`), `RolePermissionControllerAuthorizationTests.cs:47` (`CS8602`), and `SubscriptionLimitsAndUsagePersistenceTests.cs:127` (`CS1998`).

## Batch versus Non-Batch implementation findings

### Posting, identity, and persistence

- `InventoryPostingService` validates tracking mode, normalizes missing batch/serial identifiers to empty strings for balance and valuation keys, applies the balance change, creates the inventory transaction, saves, and commits within a transaction. Failures roll back the transaction (`InventoryPostingService.cs:92-110, 206-214`).
- Balances are looked up by tenant, location, product, normalized batch, and serial (`InventoryRepository.cs:57-75`). Therefore, an unbatched balance is distinct from a balance for any named batch. The transaction model represents blank tracking identifiers as null; the balance key is normalized to an empty string.
- Batch eligibility is intentionally narrower than aggregate on-hand: it requires positive available quantity under a non-empty batch number, an active/pickable location, an active warehouse, an active batch master, and a non-expired batch. Results are ordered FEFO (`InventoryRepository.cs:121-232`). Exact batch/location balances are used for issue validation.
- `InventoryService.GetAvailableStockAsync` is an exact identity lookup. When no batch is supplied it looks up the empty batch key; it does not aggregate named batches. Callers needing batch availability must use the eligible-batch path. This is an API behavior to account for when assessing displays; no frontend behavior was tested here.
- FIFO inflows create cost layers carrying the transaction's batch and location. Outflows consume oldest active layers and filter by batch only when a non-empty batch number is supplied (`FifoValuationStrategy.cs:20-45, 50-91`; `InventoryRepository.cs:291-325`). For a named batch, issue valuation is batch-specific. For an empty batch, the repository query is not batch-filtered.
- The stock ledger report projects persisted inventory transactions, including their batch/serial and source/destination locations. Inventory balances are separately persisted and queried; the report code alone does not establish that any tenant's ledger and balances reconcile.

### Tracking-mode changes and FIFO risk

**Confirmed source-level risk; tenant impact unverified.** `ProductService.UpdateProductAsync` passes the request's `TrackingMode` to `Product.Update` and saves (`ProductService.cs:178-196`; `Product.cs:149-180`). This path does not check existing on-hand balances, inventory transactions, batches, or cost layers, and does not migrate/reclassify them.

Consequences supported by the active code paths:

1. Changing a product from `None` to `Batch` can leave existing empty-batch balances excluded from eligible-batch availability and inaccessible to a batch-specific issue.
2. Changing from `Batch` to `None`, then receiving untracked stock, makes an empty-batch issue query FIFO layers without a batch filter. If named-batch layers remain active, those layers can be consumed even though the issue reduces the empty-batch balance. This is a deterministic code-path risk when the described data state exists, not proof that it occurred in any tenant.

This tracking-mode transition risk is the strongest candidate mechanism for inconsistencies after mode changes, but the reported tenant observations cannot be attributed to it without transaction and balance evidence.

### Planning availability consistency

**Static inconsistency; business impact depends on the intended meaning of the planning metric.** `InventoryPlanningService` sums all warehouse balances into `totalOnHand` and, for batch-tracked products, subtracts expiry only for named batches. It does not exclude empty-batch balances in its `eligibleOnHand` calculation (`InventoryPlanningService.cs:76-96`). That differs from stock-issue batch eligibility, which excludes empty batch keys and requires active, pickable locations and active batch masters. If legacy unbatched balances exist after a tracking-mode change, planning can count quantities that batch issue cannot select. No planning data or UI output was examined.

## Receipt, issue, Putaway, and reporting consistency

- Receipt and issue operations validate line data and invoke the shared posting service. The posting service writes balance and transaction changes under a database transaction, with rollback on failure.
- Putaway confirmation validates outstanding receipt quantity and allocation totals, then posts paired source and destination transfer movements using the line's batch/serial identity and the same warehouse. It saves and commits the Putaway state in the same transaction scope (`PutawayService.cs`, `ConfirmPutawayAsync`). This is structurally consistent with preserving quantity and identity; runtime reconciliation was not performed.
- FIFO receipt and issue layers are created/consumed within posting. FEFO batch selection and FIFO cost-layer consumption are separate rules; FEFO ordering does not itself prove FIFO valuation correctness.
- No live records were queried. There are no receipt, issue, Putaway, ledger, balance, batch-master, or cost-layer references/quantities to report. No duplicate batch master creation or transaction persistence failure was confirmed.

## Security and tenant isolation

- `InventoryController` requires `INVENTORY.VIEW` at controller level and `STOCK_ISSUE.POST` / `STOCK_RECEIPT.POST` for posting endpoints. The controller obtains tenant identity from `ICurrentUserContext` (`InventoryController.cs:16, 157-165, 230-238`).
- `PutawayController` requires `INVENTORY.VIEW`; posting operations require `PUTAWAY.POST`. It obtains tenant identity from the authenticated context and validates company and branch access before calling the service (`PutawayController.cs:15, 100-172, 181-208`).
- The inspected inventory repository queries use the supplied tenant ID for balances, batch masters, warehouses, and locations. This is source-level evidence only. Authorization tests and cross-tenant isolation tests were not run because their fixture migrates/seeds databases.
- No tenant data was accessed, and no frontend credentials were used. There is no runtime security or tenant-isolation pass result.

## Four mandatory scenarios

| Scenario | Product type / workflow | Result | Evidence |
|---|---|---|---|
| 1 | Non-Batch Receipt → Issue, direct | **NOT TESTED** | No tenant-level transaction was created or queried. |
| 2 | Batch Receipt → Issue, direct | **NOT TESTED** | No tenant-level transaction was created or queried. |
| 3 | Non-Batch Receipt → Putaway → Issue | **NOT TESTED** | No tenant-level transaction was created or queried. |
| 4 | Batch Receipt → Putaway → Issue | **NOT TESTED** | No tenant-level transaction was created or queried. |

The 11 passing Stock Count tests do not establish any of these workflows.

## Additional regression coverage

| Area | Result |
|---|---|
| Serial and BatchAndSerial posting | **NOT TESTED** |
| Item Transfer | **NOT TESTED** |
| Dispatch Note | **NOT TESTED** |
| Inventory Balance / Stock Ledger reconciliation | **NOT TESTED** |
| Warehouse/location eligibility | **NOT TESTED** |
| Inventory, receipt, issue, Putaway permissions | **NOT TESTED** at runtime; source attributes inspected |
| Cross-tenant isolation | **NOT TESTED** at runtime; tenant-scoped code inspected |
| Product TrackingMode update with existing stock | **NOT TESTED** |

## Defect and risk register

| Priority | Status | Finding | Business impact / recommended action |
|---|---|---|---|
| High | Confirmed source-level risk; tenant impact unverified | Product tracking mode can be changed without checking or reconciling inventory history, balances, or cost layers. Empty-batch stock can become unavailable after switching to Batch; later unbatched FIFO outflows can consume named-batch cost layers after switching to None. | Prevent incompatible mode changes while inventory activity exists, or require an explicit audited migration/reclassification workflow that reconciles balances, batches, ledger, and valuation layers. Add tests for both transition directions with existing stock and FIFO layers. |
| Medium | Static inconsistency; intended semantics need confirmation | Planning's `eligibleOnHand` may include empty-batch balances for Batch products, whereas issue availability excludes those balances. | Align planning with the authoritative issue-eligibility rules, or explicitly label the metric as aggregate physical on-hand and expose pickable/batch-eligible quantity separately. Add a test with an empty-batch balance and a named batch. |
| Medium | Verification gap | The required inventory regression tests use a fixture that migrates and seeds persistent LocalDB `_Test` databases. They could not safely run under the current restriction. | Provide an approved disposable/ephemeral database strategy or explicitly authorize test-fixture database writes before running these tests. |
| Low | Verification gap | Full solution build could not write to the normal API output directory because files were locked. | Rerun after the environment owner releases the locks; no process was stopped as part of this assessment. |

No confirmed tenant-specific defect, quantity discrepancy, or baseline test failure was established. No corrective action was implemented.

## Acceptance

**Overall backend inventory regression status: BLOCKED.** The full solution now builds successfully when output is isolated, but the core receipt/issue/Putaway/FIFO test suites and all tenant-level transaction reconciliation remain unverified. The available passing tests cover Stock Count only; they do not establish backend readiness for a Non-Batch demonstration or acceptance of any of the four mandatory scenarios.
