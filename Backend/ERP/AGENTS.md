# ApexERP Backend — Agent Instructions

## 1. Purpose and Scope

This file defines backend-specific development standards for ApexERP.

Technology:
- .NET 8
- ASP.NET Core Web API
- C#
- Entity Framework Core, where used
- Relational databases and existing database contexts
- Existing authentication, authorization, logging, and dependency-injection infrastructure

Read the root `Z:\ERP\AGENTS.md` and this file before making backend changes.

Inspect the actual repository, project files, dependencies, and existing implementations before assuming a feature or architectural pattern exists.

Preserve working functionality. Make focused, incremental changes and avoid unrelated refactoring.

## 2. Architecture and Layer Responsibilities

Follow ApexERP's existing layered architecture. Verify actual project and folder names before editing.

### API layer
- Define HTTP routes, request/response contracts, and status codes.
- Integrate authentication and authorization.
- Validate request structure and coordinate application operations.
- Return consistent error responses.
- Avoid embedding complex business logic in controllers.

### Application and service layer
- Coordinate business use cases and workflows.
- Apply permission-aware operation rules.
- Coordinate persistence and external services.
- Manage transaction boundaries where appropriate.

### Domain layer
- Own business rules, calculations, invariants, and valid state transitions.
- Avoid unnecessary dependencies on HTTP, UI, SaaS, Security, or AI infrastructure.
- Keep business rules independent of frontend implementation.

### Infrastructure and persistence
- Implement database access, EF Core configuration, external integrations, and infrastructure services.
- Follow existing dependency-injection and persistence patterns.
- Avoid introducing abstractions that duplicate existing functionality without a clear benefit.

Do not create new layers or projects merely for appearance. Follow the actual solution architecture.

## 3. Database Context Boundaries

Known database responsibilities include:
- `DomainDbContext`: operational and master data
- `SecurityDbContext`: authentication, users, roles, and permissions
- `SaaSDbContext`: tenants, plans, subscriptions, limits, and usage
- `AIDbContext`: AI-related data where implemented

Verify the actual context names, registrations, and ownership before editing.

Rules:
- Keep Domain independent of unnecessary SaaS, Security, and AI infrastructure dependencies.
- Do not introduce cross-database foreign keys where the architecture does not support them.
- Do not assume dedicated tenant databases are implemented.
- Enforce relationships across database boundaries through appropriate application-level validation.
- Do not make domain entities query other databases directly.
- Preserve existing migrations and persistent data.
- Do not reset databases, delete persistent data, or execute destructive schema operations without explicit approval.

## 4. Entity Framework Core and Persistence

- Inspect existing entities, configurations, migrations, relationships, and query patterns first.
- Follow established naming and mapping conventions.
- Use asynchronous database operations where appropriate.
- Pass cancellation tokens through supported asynchronous operations.
- Use filtered queries and projections when full entities are unnecessary.
- Avoid loading entire tables unnecessarily.
- Avoid N+1 queries and excessive database round trips.
- Apply pagination to potentially large result sets.
- Follow existing DTO and mapping conventions rather than exposing EF entities directly by default.
- Use appropriate transaction boundaries for operations that must succeed or fail together.
- Handle concurrent updates where relevant.
- Do not assume an operation is atomic merely because it occurs in one service method.
- Do not introduce generic repositories or extra abstraction layers without a clear benefit.

## 5. API Design and HTTP Behavior

- Inspect existing controllers, routes, DTOs, and response conventions before adding endpoints.
- Follow existing API route naming, versioning, authentication headers, and error formats.
- Keep API contracts stable unless a deliberate change is required.
- Validate request structure, required fields, ranges, and business constraints.
- Return appropriate HTTP status codes.
- Distinguish validation errors, authentication failures, authorization failures, missing resources, conflicts, and unexpected failures.
- Never expose stack traces, SQL details, credentials, secrets, or internal exception information to clients.
- Update relevant backend tests and frontend contracts when API changes require it.
- Do not invent endpoint names, request properties, or response fields without confirming the intended design.

Typical status codes, where consistent with the existing API:
- `200 OK`: successful retrieval or update
- `201 Created`: successful resource creation
- `204 No Content`: successful operation without a response body
- `400 Bad Request`: invalid request
- `401 Unauthorized`: missing or invalid authentication
- `403 Forbidden`: caller lacks permission
- `404 Not Found`: resource not found or not accessible under the established disclosure policy
- `409 Conflict`: duplicate, concurrency, or workflow-state conflict
- `422 Unprocessable Content`: only if supported by the existing API contract
- `500 Internal Server Error`: unexpected server failure

Do not replace established API response conventions solely to match this illustrative list.

## 6. Authentication, Authorization, and Scope

The backend is the authoritative security boundary.

- Reuse the existing authentication middleware, services, policies, and permission model.
- Enforce authorization on every protected operation, including reads.
- Do not rely on frontend button visibility or client-supplied role names.
- Resolve tenant context through the established authenticated request mechanism.
- Validate company, branch, warehouse, and record scope where applicable.
- Do not trust client-supplied tenant or organizational identifiers without verifying access.
- Prevent cross-tenant data access.
- Apply equivalent protections to bulk operations.
- Do not grant permissions based solely on a username, email, or role label.
- Avoid leaking whether another tenant's private record exists.
- Follow existing rules for approval restrictions and segregation of duties.

Where supported by the existing permission system, authorize capabilities independently, such as:
- View
- Create
- Update
- Delete or deactivate
- Restore
- Submit
- Approve or reject
- Post
- Reverse or cancel

Reuse existing permission identifiers. Do not create parallel authorization mechanisms without a confirmed requirement.

## 7. Business Validation

All authoritative business validation must run on the backend, even when the frontend also validates input.

Validate where applicable:
- Required fields and valid formats
- Entity existence and active status
- Tenant and organizational scope
- Referential integrity
- Quantity, unit, and conversion rules
- Business dates and permitted ranges
- Duplicate identifiers and business keys
- Permitted transaction-state transitions
- Stock availability and allocation rules
- User permissions and approval requirements
- Concurrent modifications
- Restrictions on deleted or inactive master data

Use the existing validation framework and service patterns.

Keep validation errors understandable to API consumers without exposing internal implementation details. Avoid duplicating the same business rule inconsistently across controllers and services.

## 8. Soft Delete, Deactivation, and Restoration

### 8.1 Inspect before implementing

Before adding or modifying soft delete:
- Inspect existing deletion metadata and query filters.
- Check relationships, foreign keys, and historical references.
- Check unique indexes and business-key constraints.
- Check audit and retention requirements.
- Determine whether the entity requires deletion, deactivation, or a different workflow.
- Reuse existing infrastructure.

Do not automatically add `IsDeleted`, `DeletedAt`, or `DeletedBy` to every entity. Use the established model and add fields only when required.

### 8.2 Query behavior

- Exclude soft-deleted records from normal queries by default where appropriate.
- Provide an explicit, authorized path for administrative or audit queries that must include deleted records.
- Ensure required historical transaction details remain available.
- Verify the effects of filters on navigation properties, joins, and related queries.
- Do not disable global query filters indiscriminately.
- Make intentional inclusion of deleted records explicit in code.

### 8.3 Uniqueness and relationships

- Handle uniqueness constraints correctly for active and deleted records.
- Confirm database-provider support before implementing filtered or partial unique indexes.
- Do not assume a deleted record releases a unique key.
- Do not introduce duplicate active business records through restoration.
- Do not cascade soft deletion to related records without an explicitly established business rule.
- Preserve required historical relationships and audit information.

### 8.4 Delete and restore operations

- Enforce delete, deactivate, and restore permissions in the backend.
- Record the acting user and deletion time when required by the established audit design.
- Prevent unauthorized access to deleted records.
- Validate uniqueness, relationships, and business rules before restoration.
- Handle repeated delete and restore requests safely.
- Do not permanently delete records through ordinary CRUD endpoints unless physical deletion is explicitly authorized and permitted by retention rules.

### 8.5 Transactions and inventory

- Do not physically delete posted transactions or inventory movement history through ordinary CRUD operations.
- Use supported cancellation, reversal, or correction workflows for posted transactions.
- Apply explicitly defined deletion rules to drafts.
- Preserve traceability for receipts, issues, putaway, dispatch, and other inventory movements.

## 9. Transaction Draft Mode and Workflow

Applicable transactions should follow their established Draft workflow. Examples may include Purchase Orders, Goods Receipts, Stock Receipts, Stock Issues, Putaway, Sales Orders, and Dispatch Notes.

### 9.1 Draft persistence

- Start a new transaction in Draft when required by the workflow.
- Allow incomplete drafts only where permitted.
- Persist drafts through the backend when durable storage is required.
- Allow eligible drafts to be reopened and updated.
- Do not use browser memory or local storage as the authoritative transaction record.
- Saving a draft must not accidentally submit, approve, or post it.
- Do not invent workflow statuses or transitions unsupported by the existing implementation.

### 9.2 State transitions

- Validate the current state before each transition.
- Validate required fields and business rules before submission or posting.
- Enforce permissions for each transition.
- Prevent edits when the current status prohibits them.
- Use concurrency protection when simultaneous updates could cause inconsistent results.
- Return clear validation or conflict responses for invalid transitions.

### 9.3 Posting and inventory

- Posting must execute the appropriate backend operation.
- Saving a draft must not create inventory movements unless explicitly required by the business workflow.
- Ensure stock movements occur at the correct business event.
- Prevent duplicate posting and duplicate inventory movements.
- Preserve transaction and inventory history.
- Use appropriate database transactions and idempotency or concurrency mechanisms for critical operations.
- Ensure related state changes and inventory movements remain consistent if an operation fails.

## 10. Inventory and Operational Data Integrity

For inventory-related operations:
- Validate the relevant product, unit, warehouse, location, and organizational scope.
- Validate stock availability and allocation rules where required.
- Use exact decimal types and appropriate precision for quantities and monetary values.
- Follow established rules for rounding and unit conversion.
- Prevent negative stock where prohibited by the configured business policy.
- Validate source and destination locations for transfers.
- Prevent invalid or repeated stock movements.
- Preserve the relationship between the originating transaction and its inventory movements.
- Use the established stock ledger, balance, and posting mechanisms.
- Do not independently update inventory balances through ad hoc logic if the existing architecture uses a controlled movement service.
- Keep inventory valuation behavior consistent with the configured costing method and existing implementation.

Do not assume every inventory rule applies identically to all transaction types. Verify the actual business requirements.

## 11. Concurrency, Idempotency, and Reliability

- Identify operations where simultaneous requests could corrupt data or create duplicates.
- Use existing concurrency tokens, database constraints, transactions, or locking mechanisms as appropriate.
- Use idempotency protection for critical operations when supported by the design.
- Do not assume frontend disabled buttons prevent duplicate requests.
- Make retries safe only where the operation is designed to support them.
- Handle database conflicts and concurrency exceptions consistently.
- Do not silently ignore failed persistence operations.
- Keep logging useful without recording sensitive data.

For critical operations such as posting, stock movements, and workflow transitions, verify that retries and repeated requests cannot produce unintended duplicate effects.

## 12. Master Data and Historical Integrity

- Respect the established tenant and organizational scope of each entity.
- Inspect relationships before changing deletion behavior or foreign keys.
- Prevent inactive or deleted master data from being used in new transactions where prohibited.
- Preserve historical transactions when master data changes.
- Use transaction snapshots where required to preserve historical details such as names, addresses, descriptions, or pricing.
- Do not introduce speculative tables or polymorphic relationships without a confirmed requirement.
- Apply database uniqueness constraints and backend validation consistently.
- Ensure restoration does not violate business-key uniqueness or relationship rules.

## 13. Error Handling, Logging, and Observability

- Follow established exception-handling and logging middleware.
- Return consistent API error responses.
- Log actionable diagnostic information without exposing secrets or personal data unnecessarily.
- Do not swallow exceptions without an intentional recovery strategy.
- Avoid returning internal exception messages to API consumers.
- Distinguish expected validation and conflict conditions from unexpected failures.
- Include correlation or trace identifiers where the existing infrastructure supports them.
- Do not log access tokens, passwords, encryption keys, or connection strings.
- Preserve sufficient diagnostic information for authorized operational troubleshooting.

## 14. Configuration and Secrets

- Use the established configuration and dependency-injection mechanisms.
- Do not hard-code credentials, connection strings, tokens, or encryption keys.
- Do not commit secrets or sensitive environment-specific values.
- Validate required configuration at startup where appropriate.
- Keep development and production configuration behavior distinct.
- Do not weaken authentication, encryption, or security settings merely to simplify local development.
- Follow the existing cryptography and key-management design.

## 15. Testing Standards

Test the affected behavior using the repository's existing test frameworks and conventions.

### 15.1 Unit tests

Test:
- Business rules and calculations
- Validation
- Permitted and prohibited state transitions
- Permission-related decision logic
- Soft-delete and restoration rules
- Inventory allocation and movement rules

### 15.2 Integration tests

Where appropriate, test:
- API contracts and HTTP status codes
- Database persistence
- Entity relationships and constraints
- Tenant and organizational isolation
- Authorization enforcement
- Transaction boundaries and rollback behavior
- Concurrency and duplicate-operation protection
- Soft-delete query behavior and restoration
- Posting and inventory movement consistency

### 15.3 Regression tests

Verify that changes do not break existing workflows or API contracts.

Do not remove or weaken existing tests merely to make a change pass.

Do not claim tests passed unless they were actually executed and passed. Identify tests not run and any remaining limitations.

## 16. Development and Git Safety

Before making changes:
1. Inspect the repository structure and current working state.
2. Read relevant project instructions.
3. Inspect existing implementations, models, migrations, services, APIs, and tests.
4. Identify dependencies and potential regressions.
5. Make a focused implementation plan.

During development:
- Preserve existing user changes.
- Avoid unrelated refactoring.
- Do not replace working implementations unnecessarily.
- Do not overwrite configuration or data without approval.
- Explain significant architectural changes before proceeding.

Do not perform Git operations that change repository state unless explicitly instructed, including:
- Commit
- Push
- Pull
- Reset
- Rebase
- Force-push
- Branch switching

Do not discard user changes or execute destructive Git commands.

## 17. Backend Completion Checklist

Before declaring a backend task complete, verify the applicable items:

- [ ] Existing architecture and project conventions were inspected.
- [ ] API contracts follow established patterns.
- [ ] Business validation runs on the backend.
- [ ] Authentication, authorization, and scope checks are enforced.
- [ ] Database relationships and transaction boundaries are correct.
- [ ] Soft-delete, restoration, and retention behavior is correct where applicable.
- [ ] Draft and workflow transitions are valid.
- [ ] Inventory movements are correct and cannot be duplicated.
- [ ] Errors and conflicts are handled appropriately.
- [ ] Relevant tests were executed.
- [ ] No unrelated user changes were overwritten.
- [ ] Actual results and unresolved issues are documented.

## 18. Instructions for Backend Coding Agents

At the beginning of every backend task:

1. Read the root `AGENTS.md`.
2. Read this backend `AGENTS.md`.
3. Inspect the existing implementation before proposing or making changes.
4. Reuse established services, entities, repositories, validators, authorization policies, and persistence patterns.
5. Make only the changes needed for the requested task.
6. Test the affected behavior.
7. Report changed files, actual test results, and unresolved issues.

Do not invent missing architecture, permissions, database relationships, API contracts, or workflow states. Ask for clarification before making consequential architectural changes that are not clearly required.