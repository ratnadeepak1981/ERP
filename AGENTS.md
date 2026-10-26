\# ApexERP — Global Agent Instructions



\## 1. Purpose and Scope



This file defines the global architecture, coding standards, UI conventions, security requirements, data-retention rules, and development workflow for ApexERP.



These rules apply to:

\- Backend: .NET 8 / ASP.NET Core / C#

\- Web: Angular / TypeScript

\- Mobile: Flutter / Dart

\- Supporting AI and infrastructure projects, where applicable



Before making changes:

1\. Read this root `AGENTS.md`.

2\. Read the applicable project-level `AGENTS.md`.

3\. Inspect the actual repository and existing implementation.

4\. Reuse established patterns wherever possible.

5\. Make focused changes and test the affected behavior.



Do not assume that a documented component, endpoint, model, folder, or feature exists without verifying it.



\## 2. Architecture Boundaries



ApexERP has separate Backend, Angular Web, and Flutter Mobile applications.



\### 2.1 Backend responsibilities



The backend owns:

\- Business rules and calculations

\- API contracts and request validation

\- Authentication and authorization

\- Tenant, company, branch, warehouse, and record-level access enforcement

\- Database persistence and transaction integrity

\- Workflow status transitions and transaction posting

\- Concurrency handling and duplicate-operation protection

\- Data-retention, deletion, restoration, and audit rules



\### 2.2 Angular Web responsibilities



The Web application owns:

\- Browser UI and navigation

\- Reusable Angular components

\- Client-side forms and presentation validation

\- API integration

\- Loading, error, success, empty, and access-denied states

\- Presentation of backend results



\### 2.3 Flutter Mobile responsibilities



The Flutter application owns:

\- Mobile-specific UI and navigation

\- Reusable Flutter widgets

\- Mobile state management

\- API integration

\- Mobile-specific interaction and error-handling patterns



\### 2.4 Shared architecture principles



\- Web and Flutter must use backend APIs for business operations.

\- Keep authoritative business rules in the backend.

\- Do not independently implement conflicting business rules in each frontend.

\- Client-side validation improves usability but never replaces backend validation.

\- Keep framework-specific implementations separate while maintaining consistent business semantics and API contracts.

\- Do not introduce unnecessary cross-project dependencies.

\- Do not assume a feature is implemented merely because it appears in the roadmap.



\## 3. Backend and Database Boundaries



Respect the existing separation between operational domain data, security, SaaS/platform management, and AI infrastructure.



Known architectural contexts include:

\- `DomainDbContext`: operational and master data

\- `SecurityDbContext`: authentication, users, roles, and permissions

\- `SaaSDbContext`: tenants, plans, subscriptions, limits, and usage

\- `AIDbContext`: AI-related data where implemented



Verify actual names and implementations before editing.



Rules:

\- Do not introduce cross-database foreign keys where the architecture does not support them.

\- Do not make the Domain layer unnecessarily dependent on SaaS, Security, or AI infrastructure.

\- Enforce tenant and organizational scope on the backend.

\- Do not assume dedicated tenant databases are implemented unless the repository confirms this.

\- Preserve existing migrations and persistent data.

\- Do not reset databases, delete persistent data, or perform destructive schema operations without explicit approval.

\- Follow established patterns for transactions, repositories, dependency injection, logging, and exception handling.



\## 4. Reusable Components and Code



\- Inspect existing shared components and working examples before creating new ones.

\- Prefer extending or configuring an existing component over creating a duplicate.

\- Keep reusable presentation components generic and configurable.

\- Keep business rules in the appropriate application or domain services.

\- Avoid large rewrites and unrelated refactoring.

\- Preserve working behavior unless a change is necessary and understood.

\- Use consistent naming, formatting, validation, logging, and error-handling conventions.

\- Do not create duplicate components simply to provide the same behavior under another name.



\## 5. Popup and Modal Restrictions



Avoid unnecessary popups, modal dialogs, and overlay-based forms.



Do not use browser-native:

\- `alert()`

\- `confirm()`

\- `prompt()`



Prefer:

\- Inline forms

\- Dedicated create/edit pages

\- Inline validation messages

\- Contextual confirmation sections

\- Existing shared notification or confirmation components

\- Inline success and error feedback



Use a popup or modal only when the interaction genuinely benefits from it and is consistent with established project patterns.



Do not introduce a new popup framework or component without a clear requirement.



\## 6. Reusable Buttons and Actions



Reuse existing shared button components and established action styles throughout ApexERP.



Common actions may include:

\- New / Create

\- Edit

\- View

\- Save

\- Save Draft

\- Update Draft

\- Submit

\- Approve / Reject

\- Post

\- Cancel

\- Delete / Deactivate

\- Restore / Reactivate

\- Reverse

\- Refresh

\- Back / Close



These are examples, not a requirement to display every action on every screen.



Rules:

\- Inspect existing shared button components before creating new ones.

\- Keep labels, placement, icons, spacing, loading indicators, disabled states, and tooltips consistent.

\- Display actions relevant to the screen, current record state, and effective permissions.

\- Prevent repeated clicks from producing duplicate operations.

\- Show clear feedback for successful and failed operations.

\- Do not treat hiding or disabling a button as authorization.

\- Enforce every protected action in the backend.

\- Use accurate labels: for example, distinguish Deactivate from permanent Delete.



\## 7. Transaction Draft Mode and Workflow



Every applicable transaction must follow its implemented Draft workflow.



Examples include:

\- Purchase Orders

\- Goods Receipts

\- Stock Receipts

\- Stock Issues

\- Putaway transactions

\- Sales Orders

\- Dispatch Notes



The exact workflow depends on the business requirements and existing backend implementation.



\### 7.1 Draft rules



\- New transactions should begin in Draft when that matches the established workflow.

\- Allow incomplete drafts only where the business workflow permits them.

\- Persist drafts through the backend when durable storage is required.

\- Allow eligible drafts to be reopened and updated.

\- Do not treat browser memory or local storage as the authoritative transaction record.

\- Saving a draft must not accidentally submit, approve, or post a transaction.

\- Validate required data and business rules before submission or posting.

\- Restrict editing according to transaction status and permissions.

\- Use only statuses and transitions supported by the actual workflow.

\- Do not invent additional statuses without a requirement.



\### 7.2 Posting and inventory rules



\- Posting must invoke the appropriate backend operation.

\- Saving a draft must not create inventory movements unless explicitly defined by the business process.

\- Prevent duplicate posting and handle concurrency appropriately.

\- Preserve transaction history and inventory movement traceability.

\- Display the actual operation result and refresh related UI data consistently.

\- Do not allow a frontend-only status change to substitute for backend posting.



\## 8. Role-Based Access and Security



Reuse the existing authentication, role, permission, and scope mechanisms.



\- Do not hard-code role names throughout individual screens when the system supports permission-based checks.

\- Do not assume that a role has a permission without checking the actual configuration.

\- Do not grant permissions based on usernames, labels, frontend conditions, or user-selected values.

\- Apply permissions independently to each protected action.

\- Enforce tenant, company, branch, warehouse, and record-level restrictions on the backend where applicable.

\- Respect approval restrictions and segregation-of-duties rules where implemented.

\- Handle unauthenticated, expired-session, forbidden, and invalid-scope responses through standard application flows.

\- Never expose secrets, tokens, credentials, or sensitive information in logs or UI messages.



The backend remains the security authority, even when the frontend hides or disables an action.



\## 9. Master Data and Transaction Rules



Keep master-data maintenance distinct from operational transaction processing.



\- Inspect entity relationships, tenancy, uniqueness constraints, and deletion behavior before changing models.

\- Respect the established scope of each entity.

\- Do not add speculative tables, relationships, or abstractions without a confirmed requirement.

\- Preserve historical transaction integrity.

\- Use snapshots for historical business details when the design requires original values to remain unchanged after master-data edits.

\- Validate stock availability, quantities, units, and transaction state on the backend where relevant.

\- Create inventory movements at the correct business event, not merely because a user opened or saved a screen.

\- Do not couple invoice creation to stock movements unless the approved business workflow explicitly requires it.



\## 10. Soft Delete, Deactivation, Restoration, and Retention



\### 10.1 General rules



\- Prefer soft delete or deactivation for master data and business records when historical references, auditability, or business rules require records to be retained.

\- Do not physically delete records by default.

\- Do not assume every entity must use soft delete. Choose the appropriate behavior based on the entity's business purpose and retention requirements.

\- Inspect existing models, relationships, foreign keys, transaction history, audit requirements, and delete behavior before implementing deletion.

\- Physical deletion is permitted only when explicitly required, authorized, and safe under established retention rules.



\### 10.2 Backend requirements



\- Enforce deletion, deactivation, and restoration rules in the backend.

\- Inspect and reuse existing deletion metadata and infrastructure, such as `IsDeleted`, `DeletedAt`, `DeletedBy`, or equivalent fields, if already supported.

\- Do not add duplicate deletion fields or a new soft-delete framework without first inspecting the current implementation.

\- Exclude soft-deleted records from normal queries by default where appropriate.

\- Provide authorized access to deleted records for administration, auditing, or recovery when required.

\- Prevent deleted or inactive records from being selected for new transactions when prohibited by business rules.

\- Preserve historical transactions, audit records, and required relationships.

\- Handle uniqueness constraints correctly so deleted records do not cause unexpected conflicts or permit invalid duplicate active records.

\- Do not cascade soft deletion to related records without confirming the intended business behavior.

\- Record the acting user and deletion time when required by the established audit design.

\- Authorize restoration and validate uniqueness, relationships, and business rules before restoring a record.

\- Ensure repeated deletion and restoration requests are handled safely.



\### 10.3 Frontend requirements



\- Use accurate action labels such as Deactivate, Delete, Restore, or Reactivate.

\- Do not label a soft delete as permanent deletion.

\- Hide or disable actions when the user lacks permission or the record state prohibits the operation.

\- Prefer inline confirmation or an existing shared confirmation component when confirmation is required.

\- Remove successfully deactivated records from active lists where appropriate.

\- Display clear success and failure feedback.

\- Provide deleted-record views or restoration actions only where required and authorized.



\### 10.4 Transactions and inventory



\- Do not physically delete posted transactions or inventory movement history through ordinary CRUD operations.

\- Use supported cancellation, reversal, or correction workflows for posted transactions.

\- Draft transactions may have different deletion rules, which must be explicitly defined and enforced by the backend.

\- Preserve traceability for receipts, issues, putaway, dispatch, and other inventory movements.



\### 10.5 Soft-delete testing



Verify that:

\- Deleted records are excluded from normal queries where appropriate.

\- Historical references remain accessible.

\- Deleted records cannot be used in prohibited new transactions.

\- Uniqueness constraints behave correctly.

\- Deletion and restoration require appropriate permissions.

\- Repeated deletion requests are safe.

\- Posted transactions and inventory history cannot be silently removed through ordinary CRUD operations.



\## 11. Web and Mobile UI Standards



\- Follow each project's existing theme, typography, spacing, navigation, and component conventions.

\- Keep Web and Flutter consistent in business behavior, not necessarily identical in layout.

\- Prefer reusable components over duplicated screen-specific implementations.

\- Avoid unnecessary dependencies and competing design systems.

\- Provide meaningful loading, empty, success, validation, network-error, conflict, and access-denied states.

\- Preserve user input when safely handling recoverable errors.

\- Prevent accidental data loss and duplicate submissions.

\- Make disabled and read-only states clear.

\- Consider accessibility, keyboard interaction, responsive layout, and mobile usability.



\## 12. Reusable Forms, Grids, and Tabs



Use the established reusable form and grid architecture.



\### 12.1 Grid modes



Support the appropriate existing patterns for:

\- Editable Master

\- Read-only Master

\- Editable Transaction

\- Read-only Transaction



Reuse common features where implemented:

\- Search and filtering

\- Sorting and paging

\- Row selection

\- Column visibility

\- Table, list, or card views

\- Appropriate single-row and bulk actions



\### 12.2 Transaction screen structure



Where applicable, reuse the established:

1\. Page header with title, status, and permitted actions

2\. Transaction header form

3\. Detail grid

4\. Footer with totals, validation summary, and action buttons

5\. Tabs for logically distinct detail grids or sections



\### 12.3 Multiple grids and tabs



\- Use tabs when multiple logically distinct grids or detail sections belong to the same screen.

\- Keep each tab's state, data, permissions, loading, and error handling appropriately independent.

\- Avoid duplicating grid implementations when the shared grid can support the requirement through configuration.

\- Keep transaction header data and detail rows consistent.

\- Validate the complete transaction on the backend before submission or posting.



Do not create a new component for every screen when existing shared components can be configured appropriately.



\## 13. PrimeNG and UI Library Rules



For Angular:

\- Inspect the installed Angular and PrimeNG versions before implementation.

\- Follow the existing theme and design-token configuration.

\- Prefer established ApexERP shared components when they meet the requirement.

\- Use PrimeNG selectively rather than introducing unnecessary library-specific widgets.

\- Do not copy APIs from an incompatible PrimeNG version.

\- Do not replace the existing theme or introduce a competing design system without approval.

\- Isolate library-specific details where practical.

\- Do not add dependencies without a clear requirement.

\- Avoid unnecessary overlay-based forms, dialogs, and confirmation popups.



Detailed Angular-specific rules belong in the Web project's own `AGENTS.md`.



\## 14. API Integration and Error Handling



\- Follow existing API routes, request/response models, authentication headers, and error conventions.

\- Do not invent endpoints or DTO properties without inspecting the backend.

\- Treat backend responses as authoritative.

\- Handle validation failures, unauthorized requests, forbidden requests, not-found responses, conflicts, and network failures appropriately.

\- Use the established API client and authentication services.

\- Avoid duplicating API integration logic across screens.

\- Refresh or reconcile displayed data after successful mutations.

\- Use safe retry behavior only for operations that support it.

\- Prevent accidental repeated submission of non-idempotent operations.

\- Do not expose internal exceptions or sensitive infrastructure details to end users.



\## 15. Development Workflow and Git Safety



\### 15.1 Before editing



1\. Inspect the repository structure and relevant project instructions.

2\. Check the current implementation and working examples.

3\. Review relevant models, services, APIs, components, tests, and configuration.

4\. Identify existing user changes and avoid overwriting them.

5\. Define the smallest appropriate implementation plan for substantial tasks.



\### 15.2 During implementation



\- Make focused, incremental changes.

\- Preserve unrelated work.

\- Avoid speculative features and broad refactoring.

\- Do not replace working code wholesale when a targeted change is sufficient.

\- Do not modify unrelated configuration or dependencies.

\- Explain consequential architectural changes before proceeding.



\### 15.3 Git safety



Do not perform the following unless explicitly instructed:

\- Commit

\- Push

\- Pull

\- Reset

\- Rebase

\- Force-push

\- Switch or change branches

\- Discard or overwrite user changes



Additional rules:

\- Do not assume a remote, branch, or repository path without checking it.

\- Do not execute destructive Git commands.

\- Do not claim Git changes were completed unless they were actually performed with authorization.

\- Report repository state honestly when relevant.



\## 16. Testing and Completion Criteria



Test the affected layers wherever the required environment is available.



Depending on the task, verify:

\- Backend build and relevant tests

\- API validation and authorization

\- Database persistence and transaction behavior

\- Angular build and relevant tests

\- Flutter analysis and relevant tests

\- Real browser workflows for Web UI changes

\- End-to-end business workflows and regression behavior



For transaction workflows, verify Draft creation, persistence, reopening, updating, submission, approval, posting, and cancellation where supported.



For soft delete, verify filtering, retention, uniqueness, restoration, permissions, and historical references.



For security changes, verify both frontend behavior and direct backend API enforcement.



Do not claim tests passed unless they were actually executed and passed. Identify tests not run and unresolved limitations.



\## 17. Instructions for Coding Agents



At the beginning of every task:



1\. Read this root `AGENTS.md`.

2\. Read the relevant project-level `AGENTS.md`.

3\. Inspect the existing implementation before proposing or making changes.

4\. Identify reusable components, services, and established patterns.

5\. Make only the changes needed for the requested task.

6\. Test the affected behavior.

7\. Summarize changed files, actual test results, and unresolved issues.



These instructions do not authorize redesigning the application or expanding task scope. Ask before making consequential architectural changes that are not clearly required.

