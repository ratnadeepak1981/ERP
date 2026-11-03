# ApexERP — Global Agent Instructions

## 1. Purpose and Scope

This document defines the global architecture, development standards, coding practices, security requirements, data-management rules and development workflow for ApexERP.

These instructions apply to:

- Backend: .NET 8 / ASP.NET Core / C#
- Web: Angular / TypeScript
- Mobile: Flutter / Dart
- AI and supporting infrastructure projects, where applicable.

Before making changes:

1. Read this root `AGENTS.md`.
2. Read the applicable project-level `AGENTS.md`.
3. Inspect the actual repository and existing implementation.
4. Reuse established patterns wherever possible.
5. Make focused changes and test affected behaviour.

Never assume that a documented component, endpoint, model, folder or feature exists without verifying it.

## 2. Architecture and Design Principles

ApexERP follows a layered, Domain-Driven, Modular Monolith architecture.

The architecture must support independent business modules, multiple presentation clients, shared platform capabilities and future extensibility without unnecessary coupling.

### 2.1 Core architectural principles

- Separation of concerns.
- Domain-Driven Design (DDD), where appropriate.
- High cohesion and low coupling.
- Single Responsibility Principle.
- Dependency Inversion Principle.
- Explicit ownership of business capabilities and data.
- API-first integration.
- Reusable shared infrastructure.
- Configuration over unnecessary hardcoding.
- Backward compatibility.
- Evolutionary architecture.

Do not introduce architectural patterns merely for theoretical completeness. Apply them where they solve an actual business or technical requirement.

### 2.2 Layered architecture

Maintain clear responsibilities between:

1. Presentation Layer
2. API / Application Layer
3. Domain / Business Layer
4. Infrastructure Layer

The intended dependency direction must be respected.

- Presentation depends on backend APIs.
- API and application services coordinate business use cases.
- Domain owns business rules and invariants.
- Infrastructure implements persistence and external technical integrations.
- Domain must not unnecessarily depend on database frameworks, UI frameworks or external infrastructure.

### 2.3 Domain-Driven Design

Where appropriate:

- Model business entities around their identity and lifecycle.
- Use value objects for concepts defined by their values.
- Define aggregates around business invariants and transactional boundaries.
- Use domain services when business logic does not naturally belong to an entity or aggregate.
- Use application services to coordinate use cases.
- Keep domain events focused on meaningful business occurrences.
- Define clear ownership of domain data and business operations.
- Avoid unnecessary abstractions, generic repositories and speculative domain models.

Do not force DDD patterns onto simple functionality where ordinary application logic is sufficient.

## 3. Application and Layer Boundaries

ApexERP has separate Backend, Angular Web and Flutter Mobile applications.

### 3.1 Backend

The backend owns:

- Business rules and calculations.
- API contracts and request validation.
- Authentication and authorization.
- Tenant and organisational scope enforcement.
- Database persistence and transaction integrity.
- Workflow status transitions and posting.
- Concurrency and duplicate-operation protection.
- Audit and data-retention rules.

### 3.2 Angular Web

The Web application owns:

- Browser UI and navigation.
- Reusable Angular components.
- Client-side forms and presentation validation.
- API integration.
- Loading, error, success, empty and access-denied states.
- Presentation of backend results.

### 3.3 Flutter Mobile

The Mobile application owns:

- Mobile-specific UI and navigation.
- Reusable Flutter widgets.
- Mobile state management.
- API integration.
- Mobile-specific interaction and error handling.

### 3.4 Shared principles

- Web and Mobile must use backend APIs for business operations.
- Keep authoritative business rules in the backend.
- Do not independently implement conflicting business rules in different clients.
- Client-side validation never replaces backend validation.
- Maintain consistent business semantics across clients.
- Avoid unnecessary cross-project dependencies.

## 4. Business Module Ownership and Integration

ApexERP initially follows a Modular Monolith approach.

Current operational modules include:

- Procurement
- Inventory
- Sales

Future modules may include Manufacturing, Finance, HR and other business capabilities when approved.

### 4.1 Module ownership

- Each module owns its business capabilities, workflows and validation.
- Define clear ownership of entities, aggregates and operational data.
- Avoid accessing another module's internal repositories or implementation directly.
- Communicate through established application interfaces or integration contracts.
- Avoid circular dependencies.
- Shared master data must have explicit ownership.
- Cross-module operations must preserve business and transaction integrity.

### 4.2 Cross-module integration

- Define the authoritative owner of every business operation.
- Maintain source-document references throughout transaction lifecycles.
- Validate cross-module operations through the responsible module.
- Use appropriate transaction boundaries.
- Apply idempotency and concurrency protection to important operations.
- Consider transactional outbox or reliable messaging patterns when asynchronous integration is genuinely required.

Do not introduce microservices, distributed messaging or event-driven infrastructure unnecessarily.

## 5. Database and Data Architecture

Respect the existing separation between operational domain data, security, SaaS/platform management and AI infrastructure.

Known contexts include:

- `DomainDbContext`: Operational and master data.
- `SecurityDbContext`: Authentication, users, roles and permissions.
- `SaaSDbContext`: Tenants, plans, subscriptions, limits and usage.
- `AIDbContext`: AI-related data where implemented.

Verify actual names and implementations before editing.

### 5.1 Database ownership

- Each database context owns its persistence responsibilities.
- Do not introduce unsupported cross-database foreign keys.
- Cross-context references must use stable identifiers and validated application-level relationships.
- Do not bypass context ownership by directly manipulating another context's tables.
- Keep Domain independent of SaaS, Security and AI infrastructure where appropriate.
- Do not assume dedicated tenant databases are already implemented.
- Design future database separation through appropriate abstractions where required.

### 5.2 Migration discipline

- Inspect current database schema and migration history before creating migrations.
- Follow established migration naming and execution conventions.
- Do not modify an already-applied migration to introduce new schema changes.
- Check migration dependencies and execution order.
- Verify the resulting database schema after applying migrations.
- Preserve existing migrations and persistent data.
- Never drop, recreate or reseed databases without explicit approval.
- Investigate migration failures before attempting corrective operations.

### 5.3 Data integrity

- Respect primary keys, foreign keys, uniqueness constraints and indexes.
- Validate relationships and business constraints.
- Avoid unnecessary duplicated data.
- Use historical snapshots where business requirements require original values to remain unchanged.
- Preserve transaction history and referential integrity.

## 6. Enterprise Features and Cross-Cutting Capabilities

Enterprise functionality must be implemented consistently across relevant modules.

### 6.1 Multi-tenancy and organisation

Support applicable boundaries:

- Tenant
- Company
- Branch
- Warehouse
- Location
- User and organisational assignments

Do not treat these boundaries as interchangeable.

Enforce isolation and scope on the backend.

### 6.2 Security

Support the existing authentication, authorization, role, permission and scope mechanisms.

### 6.3 Transaction management

Support applicable requirements:

- Draft persistence.
- Submission and approval.
- Posting and reversal.
- Concurrency handling.
- Duplicate-operation protection.
- Transaction traceability.

### 6.4 Financial and measurement consistency

Respect:

- Company base currency.
- Transaction currency.
- Exchange rates.
- Decimal precision.
- Rounding rules.
- Base and transaction units of measure.
- Conversion ratios.
- Historical price and tax snapshots where required.

Do not independently implement conflicting calculations across modules.

### 6.5 Auditability

Important business and security operations must be traceable.

Where applicable, record:

- Acting user.
- Operation.
- Timestamp.
- Source transaction.
- Previous and new state.
- Reversal or correction reference.

Keep permanent business audit history distinct from temporary application logs.

### 6.6 Reliability and performance

- Use appropriate database indexes.
- Implement pagination for large datasets.
- Avoid inefficient queries and unnecessary data loading.
- Use asynchronous processing where appropriate.
- Handle external service failures safely.
- Apply retry policies only to operations that support safe retries.
- Preserve transaction consistency during failures.

## 7. Reusable Components and Code Standards

- Inspect existing shared components and working examples before creating new ones.
- Prefer extending or configuring existing components over creating duplicates.
- Keep reusable presentation components generic and configurable.
- Keep business rules in the appropriate application or domain services.
- Avoid large rewrites and unrelated refactoring.
- Preserve working behaviour unless a change is necessary and understood.
- Follow established naming, formatting, validation, logging and exception-handling conventions.
- Avoid unnecessary dependencies and competing frameworks.

## 8. Popup, Modal and Interaction Restrictions

Avoid unnecessary popups, modal dialogs and overlay-based forms.

Do not use browser-native:

- `alert()`
- `confirm()`
- `prompt()`

Prefer:

- Inline forms.
- Dedicated create/edit pages.
- Inline validation.
- Contextual confirmation sections.
- Existing shared notification and confirmation components.
- Inline success and error feedback.

Use popups only where the interaction genuinely benefits from them and follows existing project patterns.

Do not introduce a new popup framework without a clear requirement.

## 9. Reusable Buttons and Actions

Reuse existing shared button components and established action styles.

Common actions include:

- New / Create
- Edit / View
- Save / Save Draft
- Update Draft
- Submit
- Approve / Reject
- Post
- Cancel
- Delete / Deactivate
- Restore / Reactivate
- Reverse
- Refresh
- Back / Close

Rules:

- Display actions relevant to the screen, transaction state and effective permissions.
- Keep labels, icons, placement, spacing, loading indicators and disabled states consistent.
- Prevent repeated clicks from producing duplicate operations.
- Provide clear success and failure feedback.
- Hiding or disabling a button is not authorization.
- Enforce protected operations in the backend.
- Use accurate action labels.

## 10. Transaction Draft Mode and Workflow

Every applicable transaction must follow its established Draft workflow.

Examples:

- Purchase Orders
- Goods Receipts
- Stock Receipts
- Stock Issues
- Putaway transactions
- Sales Orders
- Dispatch Notes

### 10.1 Draft rules

- New transactions should begin in Draft where the established workflow requires it.
- Allow incomplete drafts only where business requirements permit.
- Persist drafts through the backend when durable storage is required.
- Allow eligible drafts to be reopened and updated.
- Do not treat browser memory or local storage as the authoritative transaction record.
- Saving a draft must not accidentally submit, approve or post it.
- Validate required data and business rules before submission or posting.
- Restrict editing according to transaction status and permissions.
- Use only statuses and transitions supported by the actual workflow.

### 10.2 Posting and inventory rules

- Posting must invoke the appropriate backend operation.
- Saving a draft must not create inventory movements unless explicitly defined by the business process.
- Prevent duplicate posting and handle concurrency.
- Preserve transaction history and stock movement traceability.
- Refresh related UI data after successful operations.
- Frontend-only status changes must never substitute for backend posting.

### 10.3 Posted transaction immutability

- Posted transactions and their resulting inventory movements must not be physically deleted through ordinary CRUD operations.
- Do not silently modify historical stock movements.
- Use supported reversal, adjustment or correction workflows.
- Preserve original transaction references and reversal history.
- Maintain batch, serial, warehouse and source-document traceability.

## 11. Master Data and Transaction Rules

Keep master-data maintenance distinct from operational transaction processing.

- Inspect entity relationships, tenancy, uniqueness constraints and deletion behaviour before changing models.
- Respect the established scope of each entity.
- Do not introduce speculative tables, relationships or abstractions without confirmed requirements.
- Preserve historical transaction integrity.
- Use snapshots when original business values must remain unchanged.
- Validate stock availability, quantities, units and transaction state on the backend.
- Create inventory movements at the correct business event.
- Do not couple invoice creation to stock movements unless the approved workflow explicitly requires it.
- Prevent inactive or prohibited master records from being selected in new transactions.

## 12. Soft Delete, Deactivation, Restoration and Retention

### 12.1 General rules

- Prefer soft delete or deactivation when historical references, auditability or business requirements require retention.
- Do not physically delete records by default.
- Do not assume every entity must use soft delete.
- Inspect existing models, relationships, foreign keys and deletion behaviour first.
- Physical deletion is permitted only when explicitly required, authorized and safe.

### 12.2 Backend requirements

- Enforce deletion, deactivation and restoration rules in the backend.
- Reuse existing deletion metadata such as `IsDeleted`, `DeletedAt` and `DeletedBy`, where implemented.
- Do not add duplicate deletion fields or frameworks.
- Exclude deleted records from normal queries where appropriate.
- Provide authorized access to deleted records where required.
- Prevent inactive or deleted records from being selected in prohibited new transactions.
- Handle uniqueness constraints correctly.
- Do not cascade soft deletion without confirming the intended behaviour.
- Record the acting user and deletion time where required.
- Authorize restoration and validate uniqueness and relationships.
- Ensure repeated deletion and restoration requests are safe.

### 12.3 Frontend requirements

- Use accurate labels such as Deactivate, Delete, Restore and Reactivate.
- Hide or disable actions when permissions or record states prohibit them.
- Prefer inline confirmation or established shared confirmation components.
- Remove successfully deactivated records from active lists where appropriate.
- Display clear success and failure feedback.
- Provide deleted-record views and restoration actions only where required and authorized.

### 12.4 Transaction retention

- Never physically delete posted transactions or inventory movement history through ordinary CRUD operations.
- Use supported cancellation, reversal or correction workflows.
- Apply explicitly defined deletion rules to Draft transactions.
- Preserve traceability across receipts, issues, putaway, dispatch and other inventory movements.

## 13. Web and Mobile UI Standards

- Follow each project's existing theme, typography, spacing, navigation and component conventions.
- Keep Web and Flutter consistent in business behaviour, not necessarily identical in layout.
- Prefer reusable components over duplicated screen-specific implementations.
- Avoid unnecessary dependencies and competing design systems.
- Provide meaningful loading, empty, success, validation, network-error, conflict and access-denied states.
- Preserve user input when safely handling recoverable errors.
- Prevent accidental data loss and duplicate submissions.
- Make disabled and read-only states clear.
- Consider accessibility, keyboard interaction, responsiveness and mobile usability.

## 14. Reusable Forms, Grids and Tabs

Use the established reusable form and grid architecture.

### 14.1 Grid modes

Support the appropriate existing patterns for:

- Editable Master
- Read-only Master
- Editable Transaction
- Read-only Transaction

Reuse common features where implemented:

- Search and filtering.
- Sorting and paging.
- Row selection.
- Column visibility.
- Table, list or card views.
- Appropriate single-row and bulk actions.

### 14.2 Transaction screen structure

Where applicable, reuse:

1. Page header with title, status and permitted actions.
2. Transaction header form.
3. Detail grid.
4. Footer with totals, validation summary and action buttons.
5. Tabs for logically distinct detail sections.

### 14.3 Multiple grids and tabs

- Use tabs when multiple logically distinct grids or sections belong to the same screen.
- Keep each tab's state, data, permissions, loading and error handling appropriately independent.
- Avoid duplicating grid implementations.
- Keep transaction header data and detail rows consistent.
- Validate the complete transaction on the backend before submission or posting.

Do not create a new component for every screen when existing shared components can support the requirement.

## 15. PrimeNG and UI Library Rules

For Angular:

- Inspect installed Angular and PrimeNG versions before implementation.
- Follow the existing theme and design-token configuration.
- Prefer established ApexERP shared components.
- Use PrimeNG selectively.
- Do not copy APIs from incompatible PrimeNG versions.
- Do not replace the existing theme or introduce a competing design system without approval.
- Isolate library-specific details where practical.
- Do not add dependencies without a clear requirement.
- Avoid unnecessary overlay-based forms and dialogs.

Detailed Angular conventions belong in the Web project's own `AGENTS.md`.

## 16. API and External Integration Standards

- Follow existing API routes, request/response models, authentication headers and error conventions.
- Do not invent endpoints or DTO properties without inspecting the backend.
- Avoid exposing internal database entities directly as API contracts.
- Treat backend responses as authoritative.
- Maintain stable API contracts for Web, Mobile and external integrations.
- Introduce API versioning when breaking changes become necessary.
- Handle validation failures, unauthorized requests, forbidden requests, not-found responses, conflicts and network failures appropriately.
- Use established API clients and authentication services.
- Avoid duplicating API integration logic.
- Refresh or reconcile displayed data after successful mutations.
- Use safe retry behaviour only for operations that support it.
- Prevent accidental repeated submission of non-idempotent operations.
- Do not expose internal exceptions or sensitive infrastructure details.

## 17. AI and External Service Architecture

AI and external services must remain isolated from authoritative operational business logic.

- AI must consume approved APIs and application services.
- AI must not directly manipulate operational database tables.
- AI-generated suggestions must not bypass backend validation or authorization.
- Transaction creation, approval and posting must follow deterministic backend business rules.
- Support interchangeable AI providers through appropriate abstractions where required.
- Protect tenant data and prevent cross-tenant information leakage.
- Do not expose secrets or API keys in frontend code or logs.
- Handle external provider failures without corrupting operational transactions.
- Users must retain control over consequential business operations.

Do not introduce unnecessary AI infrastructure before an approved business requirement exists.

## 18. Audit, Logging and Observability

- Maintain audit records for important business and security operations.
- Preserve transaction lifecycle and correction history.
- Use structured logging.
- Use correlation IDs for tracing related operations where supported.
- Keep operational logs separate from permanent business audit history.
- Never log passwords, access tokens, refresh tokens, API keys or sensitive credentials.
- Do not expose internal exceptions or infrastructure details to end users.
- Use established health-check and monitoring patterns where implemented.

## 19. Development Workflow and Git Safety

### 19.1 Before editing

1. Read root and applicable project-level instructions.
2. Inspect repository structure and relevant implementation.
3. Review relevant models, services, APIs, components, tests and configuration.
4. Identify existing user changes and avoid overwriting them.
5. Define the smallest appropriate implementation plan for substantial tasks.

### 19.2 During implementation

- Make focused, incremental changes.
- Preserve unrelated work.
- Avoid speculative features and broad refactoring.
- Do not replace working code wholesale when targeted changes are sufficient.
- Do not modify unrelated configuration or dependencies.
- Explain consequential architectural changes before proceeding.

### 19.3 Git safety

Do not perform the following unless explicitly instructed:

- Commit
- Push
- Pull
- Reset
- Rebase
- Force-push
- Switch or change branches
- Discard or overwrite user changes

Additional rules:

- Do not assume a remote, branch or repository path without checking.
- Do not execute destructive Git commands.
- Do not claim Git changes were completed unless actually performed with authorization.
- Inspect the working tree before editing.
- Review `git diff` after focused changes.
- Report repository state honestly when relevant.

## 20. Testing and Completion Criteria

Test affected layers wherever the required environment is available.

Depending on the task, verify:

- Backend build and relevant tests.
- API validation and authorization.
- Database persistence and transaction behaviour.
- Migration execution and resulting schema.
- Angular build and relevant tests.
- Flutter analysis and relevant tests.
- Real browser workflows for Web UI changes.
- End-to-end business workflows and regression behaviour.

For transaction workflows, verify Draft creation, persistence, reopening, updating, submission, approval, posting and cancellation where supported.

For inventory workflows, verify stock balances, UOM conversion, batch and serial traceability, warehouse scope and reversal behaviour where applicable.

For soft delete, verify filtering, retention, uniqueness, restoration, permissions and historical references.

For security changes, verify frontend behaviour and direct backend API enforcement.

Do not claim tests passed unless they were actually executed and passed. Identify tests not run and unresolved limitations.

## 21. Instructions for Coding Agents

At the beginning of every task:

1. Read this root `AGENTS.md`.
2. Read the relevant project-level `AGENTS.md`.
3. Inspect the existing implementation before proposing or making changes.
4. Identify reusable components, services and established patterns.
5. Make only the changes needed for the requested task.
6. Test the affected behaviour.
7. Summarize changed files, actual test results and unresolved issues.

These instructions do not authorize redesigning the application or expanding task scope.

Ask before making consequential architectural changes that are not clearly required.