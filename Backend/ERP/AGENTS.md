# ApexERP Backend — Agent Instructions

## 1. Purpose and Scope

These instructions apply to all backend development within ApexERP.

### Technology stack

- .NET 8
- ASP.NET Core Web API
- C#
- Entity Framework Core
- Relational databases
- Existing authentication, authorization, logging, dependency injection and configuration infrastructure.

The backend must support a maintainable, secure, extensible enterprise ERP platform.

### Mandatory inspection warning

Before making any changes:

1. Read the repository-root `AGENTS.md` and this Backend `AGENTS.md`.
2. Inspect the actual solution, project structure, existing implementations, database contexts, migrations, dependency registrations and relevant tests.
3. Inspect any additional project-level or feature-level `AGENTS.md` instructions.
4. Identify the existing architectural pattern and follow it unless a change is explicitly required.
5. Do not assume that a service, interface, entity, feature, validation rule, permission or architectural pattern exists merely because it would be desirable.
6. Do not create duplicate implementations when equivalent functionality already exists.
7. Preserve existing working functionality and user changes.
8. Identify potential impacts on other modules, database contexts, tenants, permissions, APIs and existing transactions before changing shared components.
9. Explain significant risks and dependencies before implementation.
10. If a requested change requires a consequential architectural decision that is not clearly established, stop and ask the user before proceeding.

**Inspection must come before implementation. Do not guess, redesign or refactor unnecessarily.**
### 2.3 Mandatory Clean Architecture Boundary Enforcement

These boundaries are mandatory and must be preserved during every backend implementation, modification and refactoring.

| Project / Layer | May depend on | Must not depend on |
|---|---|---|
| Domain | Domain framework-independent code | Application, Infrastructure, API, SaaS, Security, EF Core or external infrastructure implementations |
| Application | Domain and application-level abstractions | Infrastructure implementations, API controllers, presentation concerns |
| Infrastructure | Domain and Application abstractions, approved technical dependencies | Presentation/API implementation details |
| Presentation / API | Application contracts and approved API-level shared abstractions | Infrastructure implementations or direct business-rule execution |

**Dependency direction:**

- Domain remains independent.
- Application references Domain.
- Infrastructure references the abstractions it implements.
- Presentation/API invokes Application use cases.
- The application composition root configures dependency injection.

#### Boundary protection rules

- Do not move business rules into API controllers.
- Do not place EF Core or database-specific implementation code in Domain.
- Do not reference Infrastructure projects from Domain or Application.
- Do not access `DbContext` directly from controllers.
- Do not bypass Application workflows to manipulate domain data from API endpoints.
- Do not make Domain entities depend on HTTP, authentication-provider, SaaS or external API implementations.
- Do not make the Domain depend on a shared service merely because that service is convenient.
- Do not introduce circular project references.
- Do not solve dependency problems by moving business logic into an inappropriate layer.
- Keep external integrations behind appropriate abstractions.
- Keep module-specific business rules inside their owning module.
- Shared services must not become a back door for bypassing module boundaries.

#### Inspection and change control

Before introducing or changing a project reference:

1. Inspect the existing project-reference graph.
2. Identify which layer owns the functionality.
3. Identify the direction of the proposed dependency.
4. Check whether an existing abstraction already solves the requirement.
5. Explain any boundary violation or architectural risk.
6. Obtain user approval before a consequential architectural restructuring.

Do not reorganise existing projects simply to make them match an ideal folder structure. Preserve working implementations while progressively maintaining the intended architectural boundaries.

**Architecture acceptance condition:** A feature is not considered architecturally complete merely because it builds and passes tests. Its project references, dependency direction, business-rule ownership and infrastructure isolation must also be correct.

### 2.4 Transitional Clean Architecture Implementation Status

The following modules have been transitioned to Clean Architecture (`ERP.Application` and `ERP.Infrastructure`) while retaining authoritative entities and `DomainDbContext` in `ERP.Domain`:
- **Procurement:** `PurchaseOrder`, `GoodsReceiptNote`, `PurchaseRequisition`, `ApprovalWorkflow` (Commit `8d63a34`).
- **Inventory:** `InventoryPostingService`, `InventoryService`, `BatchService`, `PutawayService`, `InventoryPlanningService`, Valuation strategies (Commit `d99cede`).
- **Sales:** `SalesOrderService`, `SalesShipmentService`, `SalesInvoiceService` (Commit `0660093`).
- **Master Data (Group 1):** `PaymentTerm` and `TaxCode` application services in `ERP.Application` and repository implementations in `ERP.Infrastructure`.
- **Master Data (Step 2):** `Brand` and `Category` application services in `ERP.Application` and repository implementations in `ERP.Infrastructure`.
- **Master Data (Step 3):** `Currency` and `ExchangeRate` application services in `ERP.Application` and repository implementations in `ERP.Infrastructure`.
- **Master Data (Step 4):** `Customer` and `Supplier` application services in `ERP.Application`, repository implementations in `ERP.Infrastructure`, and dedicated persistence coordinators (`CustomerPersistenceCoordinator`, `SupplierPersistenceCoordinator`).

## 3. Database Context Boundaries

Known database responsibilities include:

- `DomainDbContext`: ERP operational and master data.
- `SecurityDbContext`: Authentication, users, sessions, refresh tokens and security-related data.
- `SaaSDbContext`: SaaS platform, subscription and tenant-platform data.
- `AIDbContext`: AI-related persistence where currently implemented.

**Important:** Verify the actual context names, ownership and project locations before making changes.

### Mandatory rules

- Keep database ownership boundaries clear.
- Do not introduce cross-database foreign keys.
- Do not make the Domain layer depend on SaaS, Security or AI database implementations.
- Cross-context relationships must be handled through appropriate application-level identifiers and services.
- Do not assume all contexts are hosted in separate physical databases.
- Do not assume the current deployment already uses dedicated databases per tenant.
- Maintain tenant isolation regardless of the physical database arrangement.
- Design database access so future database separation is possible without introducing unnecessary complexity today.
- Do not perform database drops, truncations or destructive data operations without explicit user approval.

## 4. Entity Framework Core and Persistence

Before modifying persistence:

- Inspect existing entities, configurations, relationships, indexes and migrations.
- Follow established EF Core conventions.
- Check whether an entity or relationship already exists.
- Preserve existing database compatibility unless a migration is required.
- Use asynchronous operations and cancellation tokens where supported.
- Use projections and filtered queries to retrieve only necessary data.
- Avoid N+1 queries and unnecessary database round trips.
- Apply pagination to potentially large result sets.
- Use appropriate decimal precision for quantities, prices, costs, exchange rates and tax values.
- Use appropriate concurrency controls for transactions and shared records.
- Ensure transaction boundaries cover operations that must succeed or fail together.
- Avoid unnecessary repository abstractions when the existing architecture already provides suitable persistence abstractions.
- Avoid exposing EF entities directly through API contracts where established DTOs are used.
- Keep persistence concerns out of the Domain layer.

### Migration discipline

- Inspect the current migration history and database state before creating migrations.
- Do not modify an already-applied migration casually.
- Do not create duplicate migrations for an existing schema change.
- Check migration ordering and dependencies.
- Consider existing tenant data and existing records.
- Review generated migration operations before applying them.
- Identify destructive operations, data-loss risks and default-value implications.
- Do not automatically drop or recreate databases to resolve migration problems.
- Do not claim that a migration is applied without verification.
- Report the affected database context and migration status.

## 5. API Design and HTTP Behaviour

Before adding or changing endpoints, inspect existing controllers, routes, DTOs, response conventions and API versioning practices.

Requirements:

- Maintain consistent REST and HTTP behaviour.
- Use clear request and response DTOs.
- Validate incoming requests.
- Keep controller logic thin.
- Do not expose internal exception details or sensitive information.
- Preserve API compatibility where possible.
- Consider Angular, Flutter and future external API consumers when changing contracts.
- Avoid unnecessary breaking changes.
- API endpoints should invoke Application use cases rather than implementing business workflows directly.

Use appropriate HTTP status codes according to existing API conventions, including:

- `200` — successful request.
- `201` — resource created.
- `204` — successful operation with no response body.
- `400` — invalid request.
- `401` — unauthenticated.
- `403` — unauthorized operation.
- `404` — resource not found or appropriately concealed.
- `409` — conflict, duplicate or concurrency issue.
- `422` — business validation failure, if supported by existing conventions.
- `500` — unexpected server error.

Do not return success responses when the underlying operation has failed.

## 6. Authentication, Authorization and Scope

Backend authorization is the final authority. Frontend menu visibility and route guards are not security controls.

### Authentication

- Reuse the existing authentication implementation.
- Do not introduce a second authentication mechanism without approval.
- Never trust user IDs, tenant IDs, roles or permissions supplied by the client without validation.
- Derive authenticated identity and tenant context from the trusted authentication context.
- Follow existing access-token, refresh-token, session and revocation rules.
- Do not expose tokens, credentials or security secrets in responses or logs.

### Authorization

- Protect every relevant endpoint, including read operations.
- Enforce permissions in the backend.
- Validate access for single-record and bulk operations.
- Do not rely on usernames, email addresses, tenant-role labels or hard-coded user allowlists.
- Support custom roles through the existing permission model.
- Do not create parallel authorization systems for individual modules.

### Permission Definition versus Role Assignment

These are separate responsibilities and must be tested separately.

1. **Permission definition:** Establishes that a permission exists and identifies the operation it protects.
2. **Role assignment:** Determines which roles are granted that permission.
3. **User access:** Determines the effective permissions of the authenticated user through the applicable role and scope assignments.

Creating a permission definition does not automatically grant it to Tenant Admin or any other role.

When adding a new permission:

- Inspect permission definitions.
- Inspect existing role assignments.
- Inspect tenant provisioning and seed logic.
- Determine whether existing tenants need an assignment update.
- Do not assume that a new permission is automatically assigned to existing roles.
- Test permission existence and role assignment independently.
- Verify backend endpoint authorization as well as frontend menu, route and action visibility.

### Tenant, Company and Operational Scope

- Enforce tenant isolation on every applicable operation.
- Validate company, branch, warehouse, location and record ownership.
- Do not allow one tenant to access another tenant's data.
- Do not assume that company access and company master-data permissions are the same capability.
- Apply assigned company and operational scope according to the existing security model.
- Do not introduce a hard-coded company or tenant restriction.

### Segregation of Duties

Where workflows require it, enforce appropriate separation between creation, submission, approval, rejection and posting.

Do not assume that a user who can create a transaction can also approve or post it.

## 7. Business Validation

Backend validation is mandatory even when equivalent frontend validation exists.

Validate, where applicable:

- Required fields and formats.
- Entity existence and active status.
- Tenant, company, branch and warehouse ownership.
- Referential integrity.
- Quantity and unit-of-measure compatibility.
- Unit conversion availability and validity.
- Decimal precision and rounding.
- Business dates and effective dates.
- Duplicate records and duplicate transaction references.
- Transaction state and permitted state transitions.
- Stock availability and allocation.
- Batch and serial-number requirements.
- Currency and exchange-rate availability.
- Tax configuration and calculated values.
- User permissions and operational scope.
- Concurrency and stale data.
- Inactive, deleted or otherwise restricted master data.

Use the existing validation framework where available.

Do not duplicate business validation across multiple layers unnecessarily. The domain must still protect its essential business invariants.

## 8. Soft Delete, Deactivation and Restoration

Before implementing deletion or deactivation, inspect the existing entity model, audit fields, relationships, indexes and query filters.

- Reuse existing deletion and deactivation patterns.
- Do not add new audit or deletion fields without checking existing conventions.
- Ensure normal queries exclude soft-deleted records where required.
- Use explicit inclusion mechanisms for administrative or historical queries.
- Consider unique constraints and database-provider behaviour.
- Do not cascade-delete related business records without an established business rule.
- Protect historical transactions and audit records.
- Require appropriate permissions.
- Make repeated operations safe and predictable.
- Define restoration behaviour where restoration is supported.
- Ensure restored records do not violate uniqueness or referential integrity.

### Inventory and Posted Transaction Restriction

Posted inventory transactions must never be physically deleted through ordinary CRUD operations.

They must be corrected through controlled reversal or adjustment workflows.

## 9. Transaction Draft Mode and Workflow

Support the established transaction lifecycle without inventing unnecessary states.

Typical lifecycle stages may include:

- Draft
- Submitted
- Approved
- Rejected
- Posted
- Cancelled
- Reversed

Use only the states supported by the actual feature and business workflow.

### Draft transactions

- Drafts may be edited or deleted according to applicable permissions and workflow.
- Saving a draft must not create final stock movements or financial postings.
- Draft validation must distinguish incomplete information from posting requirements.
- Preserve draft ownership and audit history as required.

### Submitted and approved transactions

- Follow the established workflow.
- Enforce permitted transitions on the backend.
- Prevent unauthorized modifications.
- Respect approval and segregation-of-duties requirements.
- Prevent duplicate submission, approval and posting.

### Posting

- Posting must be an authoritative backend operation.
- Validate all business requirements again at posting time.
- Execute related changes within appropriate transaction boundaries.
- Ensure the transaction cannot be posted twice.
- Ensure failure does not leave partial or inconsistent operational data.
- Preserve posting references and audit history.

### Cross-Module Transaction Consistency

For workflows spanning Procurement, Inventory and Sales:

- Maintain consistent references between source and resulting transactions.
- Prevent duplicate downstream transactions.
- Preserve source-document quantities and remaining quantities.
- Enforce partial and complete processing rules.
- Ensure posting and stock movements are committed consistently.
- Handle failures without leaving misleading document statuses.
- Do not bypass the owning module's business rules.

## 10. Inventory and Operational Data Integrity

Inventory is a critical enterprise data area. Changes must preserve stock accuracy, traceability and valuation.

### General inventory rules

- Validate product, stock status, warehouse and location.
- Validate company, branch, tenant and operational scope.
- Validate quantity, base UOM and transaction UOM.
- Apply established UOM conversion rules.
- Use decimal types and appropriate precision.
- Prevent invalid negative stock unless explicitly permitted by business policy.
- Validate stock allocation and availability.
- Validate source and destination warehouses for transfers.
- Prevent duplicate stock movements.
- Use the established stock ledger or stock movement service.
- Preserve cost and valuation calculations.
- Avoid maintaining inconsistent independent stock balances.

### Batch and Serial Number Management

Batch and serial-number handling must preserve traceability throughout the inventory lifecycle.

- Inspect existing batch and serial-number models before extending them.
- Follow the product's configured batch/serial tracking requirements.
- Create batch records at the appropriate receipt stage, normally during goods receipt.
- Do not create a new batch merely because stock is transferred between warehouses or locations.
- Preserve the original batch identity during transfers.
- Preserve serial-number identity throughout receipt, storage, transfer, issue and dispatch.
- Prevent duplicate serial-number registration or use.
- Validate batch and serial availability before issue or dispatch.
- Respect expiry, status, warehouse and other applicable restrictions.
- Ensure stock movements retain the correct batch and serial references.
- Ensure reversals restore or correct the appropriate batch/serial quantities and status.
- Do not silently discard batch or serial history.

### Mandatory Posted Inventory Transaction Immutability

**Once an inventory transaction has been successfully posted, it is immutable.**

The backend must enforce all of the following:

- Do not allow posted inventory transactions to be edited.
- Do not allow posted inventory transactions to be updated through ordinary CRUD endpoints.
- Do not allow posted transactions to be reopened as drafts.
- Do not allow posted transactions to be physically deleted.
- Do not permit changing posted quantities, products, warehouses, batches, serial numbers, costs or other inventory-impacting values.
- Do not rely only on frontend read-only controls.
- Reject unauthorized modification attempts at the backend service/domain level.
- Preserve the original posted transaction and its audit history.

If a posted transaction is incorrect, use an authorized reversal or adjustment process.

The correction must:

1. Reference the original posted transaction.
2. Record the reason and responsible user.
3. Preserve the original transaction.
4. Generate the necessary compensating stock movements.
5. Maintain batch and serial traceability.
6. Maintain stock valuation and costing integrity.
7. Preserve a complete audit trail.

Do not implement a generic update or delete operation that bypasses these rules.

## 11. Concurrency, Idempotency and Reliability

ERP operations may be submitted concurrently or retried.

- Consider concurrent updates to stock, transactions and master data.
- Use appropriate concurrency tokens, database constraints, transactions or locking mechanisms.
- Prevent duplicate posting and duplicate stock movements.
- Make retry-sensitive operations idempotent where appropriate.
- Do not rely on frontend button disabling to prevent duplicate requests.
- Handle concurrency failures predictably.
- Ensure a failed operation does not leave partial data.
- Use retry strategies only where they are safe.
- Do not automatically retry non-idempotent operations without protection.

## 12. Master Data and Historical Integrity

Master data supports operational transactions and must be handled carefully.

- Validate tenant and company scope.
- Inspect existing relationships before changing master-data models.
- Prevent inactive or deleted master records from being used in new transactions where prohibited.
- Preserve historical transaction snapshots where the business requires them.
- Do not let later master-data changes silently alter historical transaction values.
- Maintain the distinction between current master data and historical transaction data.
- Avoid speculative tables, attributes or relationships without a defined requirement.
- Do not introduce industry-specific assumptions into shared business logic without checking the existing configuration model.

## 13. Error Handling, Logging and Observability

- Reuse existing exception-handling middleware and response conventions.
- Return meaningful business errors without exposing internal implementation details.
- Log unexpected failures with sufficient diagnostic context.
- Do not log passwords, tokens, secrets or sensitive personal information.
- Use correlation or trace identifiers where supported.
- Distinguish technical logs from business audit records.
- Record important business actions through the established audit mechanism.
- Include relevant transaction references when diagnosing workflow failures.
- Do not swallow exceptions or return false success responses.

## 14. Configuration, Secrets and External Dependencies

### Configuration and Secrets

- Use existing configuration and dependency-injection patterns.
- Do not hard-code connection strings, credentials, API keys or environment-specific URLs.
- Keep secrets outside source control.
- Follow established cryptography and key-management mechanisms.
- Validate required configuration during application startup where appropriate.
- Preserve environment separation.
- Never weaken security to make local development or testing easier.

### External NuGet Package and Dependency Management

**WARNING: Do not introduce a new external NuGet package without first obtaining explicit user approval.**

Before proposing a package:

1. Inspect whether the required functionality already exists in .NET 8.
2. Inspect existing solution dependencies and shared services.
3. Check whether the functionality can be implemented using existing approved dependencies.
4. Explain why an additional package is necessary.
5. Identify the package name, purpose and proposed version.
6. Review licence, maintenance status and security considerations.
7. Check .NET 8 compatibility.
8. Identify transitive dependency and version-conflict risks.

Additional rules:

- Prefer built-in .NET functionality and existing approved dependencies.
- Do not silently add, upgrade, replace or remove packages.
- Do not introduce duplicate packages with overlapping functionality.
- Do not add dependencies solely for convenience when a simple maintainable implementation using existing capabilities is sufficient.
- Do not modify central package management or shared dependency versions without understanding the solution-wide impact.
- After an approved package is added, verify package restore, build, relevant tests and compatibility.
- Report every newly introduced or changed NuGet dependency.

If an existing external package is already in use, inspect its actual usage and version before modifying it. Do not replace it simply because another package appears more convenient.

## 15. Testing Standards

Testing must demonstrate actual behaviour, not merely successful compilation.

Use existing test projects and frameworks.

### Required test categories

- Unit tests for domain rules and application behaviour.
- Integration tests for persistence and transaction boundaries.
- API tests for endpoints and response behaviour.
- Authorization and permission tests.
- Tenant and company isolation tests.
- Validation and negative-path tests.
- Migration and database compatibility checks where applicable.
- Regression tests for existing functionality.
- Concurrency and idempotency tests for critical operations.

### ERP-specific testing

Where relevant, test:

- Procurement-to-inventory workflows.
- Partial and complete goods receipt.
- Purchase-order remaining quantities.
- Supplier and product relationships.
- Currency and exchange-rate calculations.
- UOM conversions and rounding.
- Batch creation and batch traceability.
- Serial-number uniqueness and lifecycle.
- Warehouse transfers without creating new batch identities.
- Stock issue and dispatch.
- Inventory reversal and adjustment.
- Posted transaction immutability.
- Draft editing and deletion rules.
- Sales transaction and inventory integration.
- Tax snapshots and historical values.
- Permission definition and role assignment separately.
- Existing tenant behaviour after permission or migration changes.

Test both successful and unsuccessful scenarios.

Do not claim that a test passed unless it was actually executed and its result verified. Report tests that were not run.

## 16. Development and Git Safety

Before changing code:

- Inspect the current Git branch and working tree.
- Inspect existing user changes.
- Review relevant recent commits where necessary.
- Identify the smallest safe change.
- Check the applicable instructions and existing implementation.
- Establish a baseline using `git diff` and other relevant status checks.

During implementation:

- Make small, focused changes.
- Avoid unrelated refactoring.
- Preserve existing user work.
- Do not overwrite files or discard changes without approval.
- Review the diff after each meaningful change.
- Build and test the affected projects.

### Git restrictions

Do not perform any of the following unless explicitly instructed by the user:

- Commit.
- Push.
- Pull.
- Reset.
- Rebase.
- Force push.
- Switch branches.
- Delete branches.
- Discard user changes.
- Execute destructive Git commands.

Do not assume that a clean build authorizes a commit or push.

Report the branch, modified files, build results, test results and remaining issues.

## 17. Backend Completion Checklist

Before declaring a backend task complete, verify:

- [ ] Root and applicable project instructions were inspected.
- [ ] Existing implementation and architectural boundaries were inspected.
- [ ] No unnecessary architecture or feature duplication was introduced.
- [ ] Clean Architecture dependency rules were preserved.
- [ ] Domain independence was preserved.
- [ ] Module and feature ownership were respected.
- [ ] Database context boundaries were respected.
- [ ] Tenant, company and operational scope were validated.
- [ ] Authentication and backend authorization were enforced.
- [ ] Permission definitions and role assignments were checked separately where applicable.
- [ ] Business validation was enforced on the backend.
- [ ] API contracts and response conventions were preserved.
- [ ] Transaction and concurrency integrity were considered.
- [ ] Inventory, batch and serial traceability were preserved where applicable.
- [ ] Posted inventory transactions cannot be edited, reopened or deleted.
- [ ] Reversal or adjustment behaviour preserves audit and stock integrity.
- [ ] Migrations were reviewed and their status verified where applicable.
- [ ] No unapproved external NuGet package was introduced.
- [ ] Relevant builds and tests were executed.
- [ ] The final diff was inspected.
- [ ] No user changes were discarded.
- [ ] No unauthorized Git operations were performed.

## 18. Instructions for Backend Coding Agents

All backend coding agents must follow these principles:

1. Inspect before implementing.
2. Read and obey all applicable `AGENTS.md` instructions.
3. Treat the existing repository implementation as the source of truth.
4. Follow Clean Architecture, DDD and feature-based modular boundaries without introducing unnecessary abstractions.
5. Keep business rules in the appropriate domain or application boundary.
6. Preserve database, security and module ownership boundaries.
7. Treat tenant isolation and backend authorization as mandatory.
8. Treat inventory posting and historical transaction integrity as critical business requirements.
9. Never modify posted inventory transactions through ordinary CRUD operations.
10. Never introduce external NuGet dependencies without explicit user approval.
11. Never make destructive database or Git changes without explicit user approval.
12. Do not silently redesign established architecture.
13. Do not claim implementation, migration, build or test success without verification.
14. Report assumptions, risks, dependencies, modified files and test evidence.
15. If an inspection reveals a significant architectural conflict or an unclear business rule, stop and ask before making a consequential change.

**Priority rule:** Correctness, data integrity, security, traceability and preservation of existing working functionality take priority over implementation speed.