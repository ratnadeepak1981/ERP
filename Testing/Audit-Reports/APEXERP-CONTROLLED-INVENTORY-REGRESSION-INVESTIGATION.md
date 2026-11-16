# ApexERP Controlled Inventory Regression Investigation

**Investigation date:** 2026-11-15  
**Mode:** Read-only source review and exact-code tenant lookup; no inventory/API transaction queries, writes, tests, or configuration changes  
**Overall status:** **BLOCKED** — tenant-scoped transaction evidence is unavailable

## 1. Environment and evidence boundary

| Item | Finding |
|---|---|
| Intended tenant code / ID | PWTEST was explicitly authorized for read-only inspection, but an exact-code lookup in the configured PlatformDB returned no matching tenant row. Tenant ID is therefore unavailable. |
| Test environment | Repository API configuration points to SQL Server LocalDB (`PlatformDB`, `SecurityDB`, `DomainDB`, `AIDatabase`) and a `Development` launch profile. The LocalDB platform target is non-production configuration, but the intended PWTEST tenant is not present there and this does not establish the user's intended isolated test environment. |
| Active user membership | Not verified; without a matching tenant record and authenticated user context, membership cannot be established. |
| Application version | Not verified |
| Git branch / commit | `feature/stock-ledger-all-locations` / `d4c7c28df6cb771f298d9bcfe79dbf219d9fcc03` |
| Working tree | Already dirty before this report; includes inventory frontend and backend changes. Findings about current source, particularly frontend behavior, may not match the deployed application. |

The only tenant-data lookup performed was `SELECT Id, Code, Name, IsActive, Subdomain FROM dbo.Tenants WHERE Code = 'PWTEST'` against the configured `PlatformDB`; it returned zero rows. No other tenant records and no inventory data were queried. The tenant identity, login-to-tenant membership, and intended production/test isolation could not be verified, so inspecting receipt, ledger, balance, batch, or cost-layer records would violate the tenant-only restriction.

## 2. Confirmed observations

### User-reported, not independently verified

- A Batch Stock Issue initially failed.
- Changing the product to Non-Batch allowed a direct Stock Issue.
- Changing it back to Batch subsequently allowed a direct Stock Issue.
- Another Batch Stock Receipt was attempted, but its additional quantity did not appear in the expected transaction balance.

### Latest attempted receipt

| Required evidence | Result |
|---|---|
| Receipt reference / status | Not identified |
| Product and tenant | Not identified |
| Warehouse / location | Not identified |
| Batch number / posted quantity | Not identified |
| Transaction date / committed state | Not identified |

The supplied observations contain no document reference, quantity, timestamp, or tenant ID from which the latest receipt can be uniquely identified. No posted status or commit is asserted.

## 3. Balance and valuation reconciliation

| Source | Expected for a successfully committed receipt of quantity `Q` | Actual tenant evidence |
|---|---|---|
| Receipt document / inventory transaction | Positive movement for `Q`, with product, warehouse, location, batch and source reference | Not available |
| Inventory balance | Matching tenant/location/product/batch/serial balance increases by `Q` | Not available |
| Stock Ledger | Inbound movement for `Q`; subsequent issue/transfer movements reconcile to the balance | Not available |
| ProductBatch master | One product/batch identity, with authoritative dates | Not available |
| FIFO cost layers | Receipt layer(s) associated with the transaction and batch; remaining quantities reflect issues | Not available |

Thus the amount that appears or disappears, and whether the receipt transaction committed, cannot be determined. The report does not infer a data loss or a successful posting.

## 4. Posting and persistence path inspected

The active API registration maps `IInventoryService` and `IInventoryPostingService` to the `ERP.Application` implementations (`API\Confiquration\ServiceRegistration.cs:105-106`); the infrastructure repository is `ERP.Infrastructure.Repositories.Inventory.InventoryRepository` (`API\Confiquration\RepositoryRegistration.cs:66`).

1. `ERP.Application\Features\Inventory\Management\InventoryService.cs:373-556` validates receipt inputs and product/location ownership, constructs one inventory movement per line, calls posting, and commits the enclosing transaction after all lines complete. Exceptions roll back the transaction. Reuse of a supplied receipt ID/source reference is rejected when a prior transaction exists (`:421-426`).
2. `ERP.Application\Features\Inventory\InventoryPosting\InventoryPostingService.cs:34-208` validates the current product tracking mode, location and idempotency key; normalizes batch/serial identity; finds or creates the balance by tenant, location, product, batch and serial; updates quantity; processes valuation; adds the ledger transaction; saves and commits (or participates in the enclosing transaction).
3. Batch receipts create/reuse a product-scoped `ProductBatch`; conflicting established dates are rejected and missing dates may be backfilled/inherited (`InventoryPostingService.cs:671-761`).
4. `ERP.Infrastructure\Repositories\Inventory\InventoryRepository.cs:57-75` reads a balance by the exact normalized batch and serial key. `GetEligibleBatchesAsync` considers positive, non-empty batch balances at active, pickable locations, joins active batch masters, excludes expired batches, and sorts FEFO (`:121-233`).
5. FIFO inflows create cost layers; outflows consume active layers ordered by layer date and ID, restricted to the requested batch when supplied (`ERP.Application\Features\Inventory\Valuation\FifoValuationStrategy.cs:22-101`; repository query at `InventoryRepository.cs:290-328`).
6. Stock-by-location and Stock Ledger reports are separate projections over `InventoryBalances` and `InventoryTransactions` (`ERP.Infrastructure\Persistence\InventoryPersistenceCoordinator.cs:536`, `:895`). They are not independent persistence ledgers.

The repository includes focused regression tests for repeated receipts into an existing batch (`StockReceiptBatchIntegrationTests.cs:113-190`) and FIFO reconciliation (`StockIssueBatchIntegrationTests.cs:1095-1220`). They were inspected but **not run**: this continuation is read-only and the test fixture uses seeded test tenants, not the positively identified user tenant.

## 5. Tracking-mode history and likely discrepancy mechanism

The product update path passes the requested tracking mode into `Product.Update` (`ERP.Application\Features\MasterData\Product\ProductService.cs:126-206`, `:178`). The entity validates compatibility with stock-tracked/product type and then assigns `TrackingMode` (`ERP.Domain\Features\MasterData\Product\Product.cs:149-180`); the inspected update path does not reconcile existing balances, transactions, or cost layers before changing the mode.

When the active mode is `None`, posting accepts an empty batch number and persists identity under `BatchNumber = ""`. Changing the mode to Batch does not rewrite those historical movements. Later Batch issues require a non-empty batch and look up that exact batch balance; empty-batch stock is not part of that lookup or eligible-batch result. A new receipt under Batch mode is stored under its batch key rather than merged into the empty-batch balance.

**Assessment:** This is a confirmed code-level explanation for a mismatch between total product stock and batch-eligible stock if any of the user's receipts/movements were posted while tracking was `None`. It is **not confirmed as the cause in this tenant** because the relevant product history and transaction identities were not inspected.

There is a second possible presentation issue in the current, already-modified frontend checkout: `stock-transaction-form.component.ts:925-939` requests availability using the line's current batch and converts an API error into displayed zero. The available-stock endpoint reads only the exact requested batch key (`InventoryService.cs:57-75`). A blank/unselected batch or a failed request could therefore look like zero stock on that screen. This is a code-path possibility, not evidence that it caused the reported receipt discrepancy or that this checkout is deployed.

For a successful repeat receipt with the same product and batch, the code is intended to increment that batch/location balance and create another FIFO layer. The existing test asserts that behavior. If the attempted receipt is absent, source-reference duplication, validation/rollback, a different location/batch key, or a display/query mismatch remain possibilities; the available evidence cannot distinguish them.

## 6. Putaway path

`ERP.Application\Features\Inventory\Putaway\PutawayService.cs:558-613` confirms putaway through a transfer posting with source and destination locations and carries the line's batch/serial values into the transfer. The service constructs both warehouse IDs from the putaway warehouse. Source reduction and destination increase therefore depend on the transfer posting and shared transaction completing. No putaway transaction in the intended tenant was inspected or executed.

## 7. Four mandatory scenario assessment

| Scenario | Product type / path | Result | Evidence |
|---|---|---|---|
| 1 | Non-Batch, direct receipt → issue | **BLOCKED** | User reports an issue succeeded after switching to Non-Batch, but no receipt reference, posted quantity, tenant, ledger movement, or final balance was verified. |
| 2 | Batch, direct receipt → issue | **BLOCKED** | User reports an initial failure, later success after switching back to Batch, and an additional receipt whose quantity did not appear as expected. None of the underlying records or quantities were verified. |
| 3 | Non-Batch, receipt → putaway → issue | **NOT TESTED** | No execution or persisted evidence supplied or collected. |
| 4 | Batch, receipt → putaway → issue | **NOT TESTED** | No execution or persisted evidence supplied or collected. |

No scenario is accepted as PASS; the overall regression is **BLOCKED**.

## 8. Defect register and recommended action

| Finding | Status / impact | Recommended next action (not implemented) |
|---|---|---|
| Tracking mode can change without inventory-history reconciliation | Confirmed source-level risk; potentially strands prior unbatched stock outside batch availability and valuation identity. Live impact unknown. | Keep tracking mode immutable after stock activity, or design and approve a controlled, traceable reclassification workflow. Do not silently relabel posted history. |
| Availability display maps request errors to zero in current modified frontend source | Confirmed behavior in this checkout; whether it explains the report is unknown. | Surface the availability query error and keep it distinguishable from a genuine zero balance. Validate against the deployed version before any change. |
| Additional Batch receipt quantity not reflected | Unresolved; receipt, balance, ledger and cost-layer evidence unavailable. | After tenant confirmation and explicit authorization, perform a tenant-scoped read-only comparison by transaction ID/source reference, product, warehouse/location, batch and date. Do not post, reverse, adjust, or delete during that check. |

## 9. Final status and handoff

**BLOCKED.** The configured test platform has no `PWTEST` tenant row, so no tenant ID can be confirmed and the active user's membership cannot be verified. The newest Batch receipt and its ledger/balance/cost-layer reconciliation are not verifiable. No inventory/API transaction records were queried; no test, posting, configuration change, or implementation was performed. The four-scenario regression is not complete.

Before any further tenant-data investigation, identify the intended non-production environment in which `PWTEST` exists, confirm its tenant ID, and provide the attempted receipt reference (or a narrow date/time window). The user's read-only authorization for PWTEST does not authorize access to any other tenant or environment. Any implementation or additional transaction testing requires separate explicit approval.
