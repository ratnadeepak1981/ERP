# ApexERP Web — Angular Agent Instructions

## 1. Purpose and Scope

This document defines mandatory development standards for the ApexERP Angular Web application.

All AI coding agents and developers working in this frontend must follow these instructions.

### Technology Context

- Frontend framework: Angular.
- Language: TypeScript.
- UI framework: Existing shared ApexERP UI components and established frontend implementations.
- Existing PrimeNG: PrimeNG 22.1, subject to inspection of the actual installed version.
- Styling: Existing ApexERP theme, CSS conventions and design tokens.
- Backend: ASP.NET Core Web API.
- API communication: Existing Angular HTTP services and interceptors.

Always inspect the actual project configuration before assuming that a technology, library, component, service or implementation exists.

### Mandatory Initial Inspection

Before modifying any code:

1. Read the repository root `AGENTS.md`.
2. Read `Frontend/AGENTS.md`, if present.
3. Read this `Frontend/Web/AGENTS.md`.
4. Inspect the Angular workspace configuration and installed dependencies.
5. Inspect the affected feature, its components, services, models and routing.
6. Inspect existing shared components and established implementation patterns.
7. Inspect the relevant backend API contract when frontend changes depend on backend functionality.
8. Inspect the current Git branch, working tree and relevant changes before editing.

Do not assume that a feature, component, API endpoint, permission, service or shared implementation exists merely because it is mentioned in documentation.

If instructions conflict, identify the conflict and seek clarification before making consequential changes.

---

## 2. Frontend Architecture

ApexERP Web must follow established Angular and TypeScript architecture standards.

Apply Clean Architecture principles and DDD concepts pragmatically, adapting them to frontend responsibilities.

The frontend is a client application. It does not own or replace the backend ERP domain.

### 2.1 Frontend Architectural Responsibilities

| Area | Responsibility |
|---|---|
| Presentation | Pages, components, templates and user interaction |
| Application coordination | Frontend workflows, screen-level orchestration and UI state |
| Feature models | Frontend models, types, enums and API-facing representations |
| API integration | HTTP communication, API adapters and response mapping |
| Shared functionality | Reusable UI components and genuinely common frontend services |
| Authentication integration | Client-side authentication state and token lifecycle integration |
| Navigation | Routes, navigation menus, guards and navigation state |

Organise these responsibilities according to the existing Angular workspace structure.

Do not introduce unnecessary architectural layers simply to imitate backend architecture.

### 2.2 Frontend Clean Architecture Principles

- Keep presentation components focused on presentation and user interaction.
- Keep API communication outside presentation components where the existing architecture supports service-based communication.
- Use application services or facades when frontend workflows require coordination.
- Keep feature-specific models and business terminology organised within their owning feature.
- Isolate API communication and data mapping from UI presentation.
- Avoid circular dependencies.
- Avoid unnecessary coupling between independent features.
- Do not introduce abstractions without a clear maintainability or functional benefit.

### 2.3 DDD Principles in the Frontend

Apply DDD concepts where they improve frontend design:

- Organise functionality around business features.
- Use consistent business terminology with the backend.
- Keep feature ownership clear.
- Use appropriate typed models and interfaces.
- Represent business concepts consistently in forms, grids and transaction workflows.
- Use value-object-like types where they provide meaningful benefits.
- Keep feature-specific presentation rules within the owning feature.
- Avoid duplicating authoritative backend business rules.

Do not create frontend aggregates, repositories or domain services merely to imitate backend DDD.

### 2.4 Backend and Frontend Boundaries

The expected responsibility flow is:

**Angular Presentation → Frontend Application Coordination → API Integration → Backend API → Backend Application → Backend Domain**

Backend Infrastructure implements the relevant backend abstractions and handles persistence and external infrastructure.

| Responsibility | Owner |
|---|---|
| ERP business rules and invariants | Backend Domain |
| Transaction processing and posting | Backend Application/Domain |
| Database persistence | Backend Infrastructure |
| API endpoints and request handling | Backend API |
| Frontend screen workflows | Angular Application |
| User interaction and presentation | Angular Presentation |
| API communication | Angular API Integration |
| Frontend display models | Angular Feature Models |
| Final authorization enforcement | Backend |
| Menu and button visibility | Frontend, based on effective permissions |

Mandatory rules:

- Never access the backend database directly from Angular.
- Never bypass backend APIs to manipulate persisted ERP data.
- Never treat frontend validation as a replacement for backend validation.
- Never treat hidden buttons or disabled menus as security enforcement.
- Do not duplicate authoritative transaction calculations or posting rules.
- Backend API responses remain authoritative for persisted business state.

### 2.5 Feature-Based Organisation

The current operational ERP modules include:

- Procurement
- Inventory
- Sales

Keep feature-specific components, services, models, forms and state management within their appropriate feature boundaries where practical.

Shared functionality should be placed in shared areas only when it is genuinely reusable.

Do not allow one feature to manipulate another feature's internal state by bypassing its established workflow.

Before restructuring feature boundaries, inspect the existing project structure and dependencies.

---

## 3. Angular and TypeScript Standards

Follow official Angular and TypeScript standards compatible with the installed project versions.

- Use Angular-supported component and dependency-injection patterns.
- Follow existing Angular standalone component and module conventions.
- Follow the installed Angular routing and forms conventions.
- Use strict TypeScript typing.
- Avoid unnecessary `any`.
- Avoid unsafe type assertions.
- Use appropriate interfaces, types and enums.
- Use RxJS and Angular Signals according to their intended purposes.
- Avoid unnecessary subscriptions and memory leaks.
- Follow existing lifecycle and cleanup patterns.
- Use Angular-supported template control flow and binding conventions.
- Use dependency injection consistently.
- Avoid direct DOM manipulation unless there is a justified requirement and an Angular-compatible solution is unavailable.

Do not introduce a new framework, state-management library or architectural pattern without inspection and justification.

### 3.1 Architecture Flexibility

Frontend architecture is not frozen.

Agents may recommend improvements that follow established Angular and TypeScript standards and improve:

- Maintainability.
- Performance.
- Accessibility.
- Security.
- Testability.
- Feature isolation.
- Dependency management.
- Reusability.

Before consequential architectural changes, explain the proposed improvement, affected areas, compatibility risks and regression impact. Obtain approval where required.

**Mandatory principle:** Follow established frontend standards first. Apply Clean Architecture and DDD pragmatically. Prefer the simplest maintainable solution.

---

## 4. Angular File Separation and Naming

Keep HTML, CSS and TypeScript in separate files.

Use the established Angular file structure and naming conventions.

Typical structure:

- `feature-name.component.ts`
- `feature-name.component.html`
- `feature-name.component.css`
- `feature-name.service.ts`
- `feature-name.model.ts`
- `feature-name.interface.ts`

Rules:

- Do not introduce inline HTML templates.
- Do not introduce inline component styles.
- Use separate HTML and CSS files.
- Follow the installed Angular version's supported component metadata conventions.
- Follow the existing project convention for `styleUrl` or `styleUrls`.
- Keep services, models and interfaces in appropriate separate files.
- Avoid combining unrelated features into one large component or service.
- Keep files focused on one clear responsibility.
- Follow existing folder and naming conventions where they are already established.

Do not rename or move existing files unnecessarily.

---

## 5. Fluent-Inspired Clean Enterprise Theme

ApexERP Web must maintain a clean, professional and consistent enterprise user interface, following Fluent Design principles.

The visual direction should be modern, minimal, business-focused and suitable for daily ERP operations.

### 5.1 Design Principles

- Follow Fluent Design principles for visual consistency, clarity and usability.
- Maintain a clean enterprise appearance rather than a decorative or consumer-style interface.
- Prioritise information readability and efficient business workflows.
- Use consistent layouts, spacing, typography, colours and interaction patterns.
- Avoid unnecessary visual elements, excessive borders, shadows, gradients and animations.
- Keep information density appropriate for ERP forms, transaction screens and data grids.
- Ensure that important actions and transaction statuses are immediately understandable.

### 5.2 Theme and Design Tokens

Follow the existing ApexERP theme and design-token implementation.

Current known design direction:

| Element | Standard |
|---|---|
| Design philosophy | Fluent-inspired enterprise design |
| Primary colour | `#0F6CBD` |
| Typography | Segoe UI |
| Spacing | 4px grid |
| Component styling | Existing shared ApexERP components |
| Theme support | Existing light and dark theme implementation |
| Visual style | Clean, minimal and professional |

These values describe the current design direction. Inspect the actual theme configuration before changing or introducing tokens.

### 5.3 Layout and Visual Hierarchy

- Use clear page titles and section headings.
- Maintain consistent page margins and content alignment.
- Use appropriate spacing between forms, sections and data grids.
- Group related business information logically.
- Keep primary actions visually prominent without excessive emphasis.
- Use consistent positioning for page actions and transaction footer actions.
- Avoid unnecessary nested containers and card layouts.
- Use subtle separators and borders where they improve readability.
- Maintain consistent sizing and alignment across related screens.

### 5.4 Colours and Status Indicators

Use the existing theme tokens for:

- Primary actions.
- Secondary actions.
- Success states.
- Warning states.
- Error states.
- Information states.
- Disabled controls.
- Selected and focused elements.

Do not introduce arbitrary colours.

Transaction statuses must be visually distinguishable, including where applicable:

- Draft.
- Pending approval.
- Approved.
- Rejected.
- Posted.
- Cancelled.
- Reversed.
- Inactive.

Do not communicate status through colour alone. Include readable status text.

### 5.5 Forms and Data Grids

- Maintain consistent form labels, input heights and field spacing.
- Align related fields logically.
- Keep validation messages clear and concise.
- Use consistent column alignment and number formatting.
- Right-align numeric values where appropriate.
- Keep dates, quantities, currencies and totals readable.
- Avoid excessive grid borders and unnecessary visual decoration.
- Maintain clear differentiation between editable and read-only data.
- Support the existing responsive behaviour.

### 5.6 Light and Dark Themes

- Preserve the existing light and dark theme support.
- Use theme tokens rather than hardcoded background and text colours.
- Check contrast and readability in both themes.
- Ensure dialogs, dropdowns, forms, grids and notifications follow the active theme.
- Do not introduce feature-specific theme implementations.

### 5.7 Fluent Theme Implementation Rules

- Inspect the existing Fluent-inspired tokens and styling before making changes.
- Reuse existing CSS variables, theme classes and shared styling utilities.
- Do not introduce Fluent UI npm packages or another component library without explicit approval.
- Do not expand PrimeNG usage; follow the existing PrimeNG inspection-only policy.
- Do not replace existing shared components with a different UI framework.
- Avoid duplicating theme definitions across features.
- Keep shared theme changes centralised.
- Verify the impact of theme changes on existing screens before completion.

### 5.8 Visual Acceptance Standard

A screen should be accepted visually when it:

1. Looks consistent with the existing ApexERP enterprise interface.
2. Follows the Fluent-inspired design direction.
3. Uses existing theme tokens and shared components.
4. Has clear information hierarchy.
5. Supports efficient ERP data entry and review.
6. Maintains readability in light and dark modes.
7. Avoids unnecessary visual complexity.
8. Remains usable across supported screen sizes.
## 6. Shared UI Components and Reusability

Inspect existing shared UI components before creating new components.

Prefer existing ApexERP shared components and established reusable implementations.

Examples of reusable component responsibilities may include:

- Page headers.
- Form layouts.
- Action buttons.
- Confirmation dialogs.
- Lookup controls.
- Editable grids.
- Read-only grids.
- Search and filter controls.
- Pagination.
- Loading indicators.
- Empty states.
- Error messages.
- Notification messages.

These are examples, not confirmation that corresponding components already exist.

Mandatory rules:

1. Search for an existing implementation before creating a new component.
2. Inspect its inputs, outputs, events, styling and dependencies.
3. Reuse it when it meets the requirement.
4. Extend it carefully when appropriate.
5. Avoid duplicate shared components.
6. Keep shared components generic enough for their intended reuse.
7. Keep feature-specific business behaviour within the feature.

Do not introduce unnecessary shared components for one-time use cases.

### 6.1 Existing UI Technology Policy

- Inspect the existing shared UI component architecture before implementing UI changes.
- Prefer the existing ApexERP shared editable components and approved UI implementations.
- Preserve compatibility with existing screens.
- Allow existing UI technologies to coexist where required by the current implementation.
- Do not perform unrelated UI-library migrations.
- Do not introduce another UI component library.

### 6.2 PrimeNG Inspection-Only Policy

PrimeNG exists in the current project and may be present in existing screens and shared implementations.

However, **do not expand PrimeNG usage**.

- Inspect existing PrimeNG components only to understand existing implementation, compatibility and dependencies.
- Do not introduce additional PrimeNG components as a new implementation choice.
- Do not create new PrimeNG-based shared components.
- Do not replace existing ApexERP shared components with PrimeNG.
- Do not add or upgrade PrimeNG packages.
- Do not introduce new PrimeNG dependencies.
- Do not perform a PrimeNG migration or removal as part of an unrelated feature.
- Do not modify existing PrimeNG implementations unnecessarily.
- If an existing screen uses PrimeNG, inspect its implementation and preserve existing behaviour unless an approved change requires otherwise.

If the requirement cannot be implemented using existing approved shared UI components, explain the limitation and request approval before introducing a new UI dependency or expanding PrimeNG usage.

---

## 7. Page Headers, Forms and Footer Actions

Maintain a consistent page structure.

Inspect existing screens and shared components to identify the established layout.

Where appropriate, pages should contain:

- Page title and relevant context.
- Breadcrumbs or navigation context.
- Primary actions.
- Search and filter areas.
- Main content.
- Form validation feedback.
- Footer actions for transaction forms.

Rules:

- Do not create a different page layout for every feature.
- Keep primary and secondary actions visually distinguishable.
- Keep form labels and controls aligned.
- Provide clear validation messages.
- Avoid unnecessarily long forms.
- Group related fields logically.
- Preserve existing responsive behaviour.
- Avoid duplicated action buttons in headers and footers unless there is a clear usability reason.

---

## 8. Reusable Buttons and Action States

Use the established shared button components and styles.

Actions must have clear and predictable behaviour.

Examples:

- Create.
- Save.
- Update.
- Cancel.
- Delete.
- Submit.
- Approve.
- Reject.
- Post.
- Reverse.
- Print.
- Export.

Rules:

- Do not create duplicate button styles.
- Use appropriate confirmation for consequential operations.
- Disable actions when their required conditions are not satisfied.
- Show loading states during asynchronous operations.
- Prevent accidental repeated submission.
- Show meaningful success and failure feedback.
- Do not display an action that the user cannot perform under the effective UI permissions.
- Do not rely on button visibility or disabled state for backend security.

Transaction-specific actions must reflect the actual backend transaction lifecycle.

---

## 9. Transaction Draft Mode

Transaction screens must clearly distinguish between draft and posted states.

### Draft Behaviour

- Draft transactions may be edited according to backend rules.
- Draft transactions may be cancelled or deleted where permitted.
- Drafts must not affect posted inventory balances.
- Drafts must not create stock movements.
- Drafts must not update financial or inventory valuation balances.
- Draft status must be displayed clearly.

### Posting Behaviour

- Posting must be an explicit user action.
- Show confirmation where appropriate.
- Prevent repeated submission.
- Display the posting result and resulting transaction status.
- Refresh the screen using authoritative backend data.
- Do not assume that a successful HTTP request means the business transaction was correctly posted; handle the API response and final status.

### Posted Transaction Behaviour

Posted inventory transactions are immutable.

- Do not provide ordinary edit or delete actions for posted transactions.
- Do not allow a posted transaction to be reopened as a draft.
- Corrections must follow the backend's controlled reversal or adjustment workflow.
- Preserve references to the original transaction.
- Display original and reversal/adjustment relationships where supported by the API.
- Never simulate a reversal only in frontend state.

The backend remains responsible for enforcing all transaction lifecycle rules.

---

## 10. Data Grids and Transaction Lines

Inspect existing grid components and their supported editing behaviour before implementation.

### 10.1 Editable Grids

For editable transaction lines:

- Follow the existing shared editable-grid implementation.
- Support the required add, edit and remove operations.
- Provide appropriate validation.
- Display required fields clearly.
- Handle lookup selections correctly.
- Handle quantity, unit price, discounts, tax and totals using backend-compatible data types and precision.
- Prevent invalid line submissions.
- Preserve unsaved changes appropriately.
- Provide clear feedback when a line is invalid.

### 10.2 Read-Only Grids

Use read-only grids for:

- Posted transaction lines.
- Historical records.
- Audit information.
- Stock movement history.
- Other data that must not be edited through the current screen.

Do not make a grid editable merely because the underlying data is displayed in tabular form.

### 10.3 Multiple Grids and Tabs

When a page requires multiple grids or tabs:

- Group information according to its business purpose.
- Keep navigation between tabs clear.
- Preserve state appropriately when switching tabs.
- Avoid unnecessary API calls when existing data can safely be reused.
- Refresh data when required by the transaction lifecycle.
- Do not allow hidden tabs to bypass validation.
- Ensure each grid has a clear data owner and responsibility.

---

## 11. API Integration

All backend communication must use the established Angular API integration architecture.

Before implementing an API call:

1. Inspect existing API services.
2. Inspect the backend endpoint and HTTP method.
3. Inspect request and response contracts.
4. Check authentication requirements.
5. Check permission requirements.
6. Check tenant, company, branch and warehouse context.
7. Check error response conventions.
8. Reuse existing service patterns.

Rules:

- Do not call APIs directly from HTML templates.
- Avoid putting HTTP logic inside presentation components.
- Use appropriate typed request and response models.
- Handle API errors consistently.
- Avoid duplicated endpoint definitions.
- Avoid hardcoded API base URLs.
- Follow existing environment and configuration conventions.
- Do not silently change API request or response contracts.
- Do not assume that an endpoint exists because a frontend screen requires it.
- Do not create mock success responses as substitutes for missing backend functionality.

If backend support is missing, report it clearly rather than implementing a fake frontend workflow.

---

## 12. Role-Based UI Permissions

The frontend must respect the effective permissions returned or established by the backend authentication and authorization implementation.

Rules:

- Inspect the existing permission service and permission definitions.
- Inspect how permissions are loaded and assigned.
- Inspect existing route guards and menu filtering.
- Do not hardcode usernames, roles, tenant IDs or role-specific exceptions.
- Do not assume that every tenant has identical role assignments.
- Do not assume that defining a permission automatically assigns it to a role.
- Do not assume that Tenant Admin automatically has every newly introduced permission.
- Support custom roles and permission assignments.
- Separate permission definitions from role assignments.
- Hide or disable actions according to the effective permission and UI requirements.
- Refresh effective permissions when the existing authentication lifecycle requires it.

### Backend Authorization Is Mandatory

Frontend permission checks are for user experience only.

The backend must independently validate every protected operation.

Test permission behaviour separately at these levels:

1. Permission definition exists.
2. Permission is assigned to the intended role.
3. User receives the effective permission.
4. Menu and route visibility are correct.
5. Action visibility and enabled state are correct.
6. Backend authorization allows or rejects the operation correctly.

Do not treat a hidden menu as proof of security.

Company operational access and company master-data permissions must remain distinct according to the backend authorization rules.

---

## 13. Soft Delete, Deactivation and Restoration

Inspect the backend API and actual entity lifecycle before implementing delete-related UI.

Do not assume that delete means physical deletion.

Where the backend supports soft deletion or deactivation:

- Display the correct record status.
- Provide confirmation for consequential actions.
- Prevent inappropriate operations on inactive records.
- Provide restoration only where supported.
- Refresh the UI after status changes.
- Respect tenant and user permissions.
- Display meaningful API validation errors.

Do not implement restoration or permanent deletion without backend support.

For posted transactions, follow the transaction immutability and reversal rules rather than generic master-data deletion behaviour.

---

## 14. Loading, Empty, Error and Success States

Every data-driven screen must handle its asynchronous states appropriately.

### Loading

- Display a suitable loading indicator.
- Prevent repeated operations while requests are in progress.
- Avoid unnecessary layout shifts.

### Empty

- Display a meaningful empty state.
- Explain when no records are available.
- Provide an appropriate next action when permitted.

### Error

- Display understandable error messages.
- Distinguish validation errors from authorization, network and server failures.
- Do not hide backend error details that are necessary for resolving the issue.
- Avoid exposing sensitive technical information to end users.

### Success

- Display meaningful confirmation.
- Refresh or update the UI using authoritative API results.
- Avoid reporting success before the operation has completed.

Do not leave screens in indefinite loading states after failed requests.

---

## 15. Forms, Lookups and Data Integrity

Forms must follow Angular best practices and the existing application conventions.

Rules:

- Use the established reactive or template-driven form approach used by the affected feature.
- Prefer typed forms where appropriate.
- Keep validation rules clear and maintainable.
- Display field-level validation messages.
- Prevent invalid submission.
- Preserve user input when recoverable errors occur.
- Avoid duplicate API requests.
- Handle lookup loading and selection states correctly.
- Clear dependent values when their parent selection changes.
- Avoid stale lookup data.
- Use appropriate date, quantity, currency and identifier types.
- Respect backend precision and rounding behaviour.

### Data Integrity

Frontend validation improves usability but does not replace backend validation.

For transaction screens, consider:

- Required fields.
- Valid product and party selections.
- UOM selection.
- Quantity and price validation.
- Currency and exchange-rate context.
- Tax and discount presentation.
- Warehouse and location selection.
- Batch and serial selection.
- Transaction status.
- User permission.

Do not independently calculate authoritative inventory or financial values when the backend owns those calculations.

---

## 16. Routing, Page Navigation and Route Management

Use Angular Router and the established routing architecture.

### Route Organisation

- Keep route definitions in the established central or feature-routing structure.
- Follow existing lazy-loading conventions.
- Use descriptive, consistent kebab-case URL paths.
- Avoid duplicate route definitions.
- Keep route ownership clear.
- Use route guards where authentication or permission checks are required.
- Do not rely on route guards as backend security.

### Page Navigation

- Navigation menus must reflect the user's effective permissions.
- Use the established navigation configuration.
- Avoid hardcoded role-specific navigation.
- Keep breadcrumbs and navigation context consistent.
- Preserve required navigation state where appropriate.
- Avoid unnecessary full-page reloads.

### Browser Refresh and Direct Navigation

Every valid route must behave correctly when:

- Opened through the application menu.
- Refreshed in the browser.
- Opened directly using its URL.
- Accessed through a bookmark.
- Opened after authentication.
- Opened after a session expires.

Verify that the hosting/server configuration supports Angular route fallback for direct navigation and browser refresh.

### Unsaved Changes

- Identify screens containing unsaved changes.
- Warn users before navigating away when required.
- Use Angular-supported navigation protection patterns.
- Do not silently discard important user input.
- Do not block navigation unnecessarily when no unsaved changes exist.

---

## 17. Authentication and Token Management

Authentication must follow the actual backend security implementation.

Inspect the existing authentication service, interceptors, guards, API contracts and backend token lifecycle before making changes.

### Authentication Integration

- Centralise authentication state management.
- Use the established authentication service.
- Use HTTP interceptors for appropriate common request handling.
- Use route guards for authenticated navigation.
- Avoid implementing separate authentication logic in individual features.
- Keep tenant and company context consistent with the authenticated session.

### Access Token Management

- Attach access tokens according to the backend authentication contract.
- Do not hardcode tokens.
- Avoid unnecessary token copies across components and services.
- Handle expired access tokens correctly.
- Do not assume that a token is valid merely because it exists in frontend state.

### Refresh Token Management

Implement refresh handling only when supported by the backend.

When refresh is supported:

- Follow the backend's refresh-token transport contract.
- Support the defined refresh-token rotation lifecycle.
- Coordinate concurrent failed requests so that multiple requests do not independently trigger conflicting refresh operations.
- Retry eligible requests only after successful refresh.
- Handle refresh failure consistently.
- Prevent infinite retry loops.
- Respect session expiration and revocation.
- Do not invent refresh endpoints or request formats.

### Token Storage and Cookie Security

- Inspect the actual backend cookie and token configuration.
- Prefer the backend-supported HttpOnly, Secure and appropriate SameSite cookie approach for browser refresh tokens.
- Do not attempt to read an HttpOnly cookie through JavaScript.
- Follow the backend's CSRF protection requirements.
- Follow the existing origin validation and required request-header conventions.
- Do not move tokens into a different storage mechanism without security review and approval.
- Do not store sensitive tokens in local storage unless the approved architecture explicitly requires it.

### HTTP Error Handling

Distinguish:

- `401 Unauthorized`: authentication missing, invalid or expired.
- `403 Forbidden`: authenticated user lacks permission.

Do not automatically log out a user for every `403` response.

### Logout and Session Expiration

- Use the established backend logout or session-revocation workflow.
- Clear frontend authentication state.
- Clear sensitive cached data.
- Handle session expiration gracefully.
- Redirect users appropriately.
- Prevent protected screens from remaining accessible through stale frontend state.

### Authentication Testing

Test:

- Successful login.
- Invalid credentials.
- Access-token expiration.
- Refresh-token success, if supported.
- Refresh-token expiration.
- Refresh-token replay or revocation, where applicable.
- Concurrent unauthorized requests.
- Logout.
- Session expiration.
- Tenant and company context.
- Unauthorized route access.
- Forbidden API operations.

Do not claim that authentication or refresh-token handling is secure without verifying the actual implementation and tests.

---

## 18. Performance, Accessibility and Responsive Design

Follow Angular performance and accessibility best practices.

### Performance

- Avoid unnecessary component re-rendering.
- Use appropriate change-detection and Signals/RxJS patterns.
- Avoid unnecessary API requests.
- Use lazy loading where appropriate.
- Use pagination or server-side filtering for large datasets where supported.
- Avoid loading excessive transaction history into the browser.
- Clean up subscriptions and event listeners.
- Avoid unnecessary computation in templates.
- Use track expressions or equivalent supported mechanisms for repeated collections.

### Accessibility

- Use semantic HTML.
- Provide accessible labels for controls.
- Support keyboard navigation.
- Maintain visible focus states.
- Provide meaningful validation feedback.
- Use appropriate contrast.
- Do not communicate status using colour alone.
- Support screen readers where practical.

### Responsive Design

- Follow the existing responsive layout conventions.
- Ensure forms, grids and action areas remain usable at supported screen sizes.
- Avoid fixed layouts that break on smaller displays.
- Inspect existing responsive behaviour before changing shared styles.

---

## 19. Testing and Browser Verification

Do not consider a frontend change complete merely because the code compiles.

Use the testing tools and conventions already available in the project.

### Component and Service Testing

Test relevant:

- Component behaviour.
- Form validation.
- Service/API interaction.
- Error handling.
- Permission visibility.
- Route guards.
- Authentication behaviour.
- Loading and empty states.
- State management.
- Shared component compatibility.

### Feature Testing

For transaction screens, verify:

- Initial page loading.
- Create and edit draft workflows.
- Lookup behaviour.
- Line-level validation.
- Save and cancel.
- Submit and posting.
- Read-only posted state.
- Reversal or adjustment presentation where supported.
- Permission-dependent actions.
- API failure handling.

### Browser Verification

Where the environment permits, verify:

- Page rendering.
- Navigation.
- Browser refresh.
- Direct route access.
- Form behaviour.
- Grid editing.
- Dialogs and confirmations.
- Loading and error states.
- Responsive layout.
- Browser console errors.
- API network failures.

### Regression Testing

Check that the change has not broken:

- Existing screens.
- Shared UI components.
- Navigation.
- Authentication.
- Permission handling.
- Existing API integrations.
- Related business features.

Report which tests were actually executed and their results. Do not claim that unexecuted tests passed.

---

## 20. Change Management and Git Safety

Follow the repository's Git and change-management instructions.

Before making changes:

1. Inspect the current branch.
2. Inspect the working tree.
3. Inspect relevant existing modifications.
4. Review the affected files.
5. Establish the baseline diff.

During implementation:

- Make small, focused changes.
- Avoid unrelated refactoring.
- Preserve existing user changes.
- Do not overwrite unrelated work.
- Review the diff after each meaningful change.
- Check for accidental file deletion or configuration changes.
- Keep changes within the requested scope.

Before completion:

- Review the complete diff.
- Check formatting and compilation where possible.
- Run relevant tests.
- Report modified files.
- Report tests executed and their results.
- Report known limitations and outstanding issues.

Never perform the following without explicit user approval:

- Commit.
- Push.
- Pull.
- Reset.
- Rebase.
- Force push.
- Switch branches.
- Delete branches.
- Discard user changes.
- Run destructive Git commands.
- Perform destructive database operations.

Do not install, remove or upgrade dependencies without explicit approval.

---

## 21. Angular Coding Agent Responsibilities

Every coding agent working on ApexERP Web must:

- Read and follow all applicable `AGENTS.md` instructions.
- Inspect existing code before proposing or implementing changes.
- Understand the affected feature and its dependencies.
- Follow Angular and TypeScript standards.
- Preserve Clean Architecture and appropriate DDD boundaries.
- Respect backend/frontend responsibility separation.
- Reuse existing shared UI components.
- Follow the PrimeNG inspection-only policy.
- Avoid introducing unnecessary dependencies.
- Preserve existing functionality.
- Make focused changes.
- Verify changes with appropriate tests.
- Report actual implementation status accurately.

### Mandatory Inspection Warning

If an agent cannot inspect the relevant source code, configuration, API contract, shared component or permission implementation, it must clearly report that limitation.

It must not invent an implementation based on assumptions.

If the requested change requires an architectural decision, dependency change, significant restructuring or a change to an existing security contract, stop and request approval before proceeding.

---

## 22. Web Development Completion Checklist

Before marking a frontend task complete, verify the applicable items:

- [ ] Root and frontend instructions were inspected.
- [ ] Existing Angular workspace and dependencies were inspected.
- [ ] Existing feature implementation was inspected.
- [ ] Existing shared UI components were inspected.
- [ ] No unnecessary PrimeNG usage was introduced.
- [ ] No unapproved UI library or dependency was introduced.
- [ ] Angular and TypeScript conventions were followed.
- [ ] HTML, CSS and TypeScript remain in separate files.
- [ ] Clean Architecture responsibilities were respected.
- [ ] DDD concepts were applied appropriately.
- [ ] Backend and frontend responsibilities remain separated.
- [ ] API contracts were verified.
- [ ] Authentication and permission behaviour were considered.
- [ ] Tenant and company context were preserved.
- [ ] Loading, empty, error and success states were handled.
- [ ] Forms and grids were validated.
- [ ] Routing and browser refresh behaviour were considered.
- [ ] Existing functionality was preserved.
- [ ] Relevant tests were executed.
- [ ] Browser verification was performed where possible.
- [ ] Git changes and final diff were inspected.
- [ ] Actual test results and outstanding limitations were reported.

**Final principle:** ApexERP Web must remain a maintainable, secure and consistent Angular application. Follow established frontend engineering standards, apply Clean Architecture and DDD where appropriate, reuse approved shared UI implementations, and keep the backend as the authoritative owner of ERP business behaviour.