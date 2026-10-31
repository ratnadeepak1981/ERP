# ApexERP Web — Angular Agent Instructions

## 1. Purpose and Scope

This file defines frontend-specific development standards for ApexERP Web.

Technology:
- Angular
- TypeScript
- PrimeNG, where appropriate
- Existing ApexERP theme and design tokens
- Existing API services, authentication, and shared components

Read the root `Z:\ERP\AGENTS.md` and this file before implementing Web changes.

Inspect the actual Angular workspace, installed versions, routes, shared components, services, models, and working screens before editing.

Do not assume a component or feature exists merely because it is described in documentation. Verify it in the repository.

Make small, targeted changes and preserve working functionality.

## 2. Frontend Architecture

Follow the existing Angular architecture and conventions.

Keep responsibilities separate:
- Components: presentation, user interaction, and screen coordination
- Services: API access and reusable application operations
- Models and interfaces: frontend data contracts
- Forms: input handling and client-side validation
- Routing: navigation and route configuration
- Shared components: reusable presentation and interaction patterns
- State management: use the existing project approach

Do not introduce a new state-management library or architectural pattern without a clear requirement.

Keep business rules authoritative on the backend. Frontend validation is for usability and must not replace backend validation.

Avoid putting large amounts of business logic directly into templates or generic presentation components.

## 3. Theme and Visual Consistency

- Preserve the existing ApexERP theme, design tokens, typography, colors, spacing, and component conventions.
- Inspect existing working screens before introducing a new layout.
- Reuse existing styles instead of creating competing styles.
- Follow the established Fluent-style design where already implemented.
- Use the existing spacing system and consistent alignment.
- Maintain responsive behavior and appropriate accessibility.
- Do not replace the existing theme or introduce another design system without explicit approval.
- Avoid screen-specific styling that duplicates a shared component's behavior.
- Do not introduce unrelated visual redesigns during a feature change.

## 4. Reusable Components First

Before creating a component, search the codebase for an existing reusable implementation.

Known shared component names may include the following; verify their existence and actual interfaces before use:
- `AppPageHeaderComponent`
- `AppReactiveFormComponent`
- `AppStatusBadgeComponent`
- `AppLoadingStateComponent`
- `AppErrorStateComponent`
- `AppEmptyStateComponent`
- `AppEditableDataTableComponent`
- `AppLookupDropdownComponent`

Rules:
- Reuse existing shared components when they meet the requirement.
- Extend a shared component only when the change is broadly useful and backward-compatible.
- Prefer configuration and inputs over creating near-duplicate components.
- Keep generic components independent of specific transaction business rules.
- Avoid creating one custom grid, header, footer, or button implementation per screen.
- Inspect component inputs, outputs, styling, accessibility, and existing usage before changing a shared component.
- Test existing screens that depend on a shared component after modifying it.

## 5. Page Headers, Forms, and Footer Actions

Use a consistent page structure wherever applicable.

### 5.1 Page header

Reuse the established page-header component or pattern for:
- Page title
- Breadcrumbs or navigation context, where supported
- Record number and status when relevant
- Primary actions
- Secondary actions

Avoid duplicating page-header markup and styling across screens.

### 5.2 Header forms

Reuse the established reactive-form component and field configuration patterns where appropriate.

- Follow existing Angular form conventions.
- Use typed models and appropriate validation.
- Show field-level validation messages clearly.
- Avoid duplicating form layouts unnecessarily.
- Keep screen-specific business rules outside generic form presentation components.
- Do not reset user-entered data unexpectedly.

### 5.3 Footer and transaction actions

Reuse existing footer or action-bar components where appropriate.

Keep actions consistent in labels, placement, spacing, icons, loading states, and disabled states.

Only display actions that apply to the screen, current record state, and effective permissions.

Do not introduce a separate footer component for each transaction when the existing shared pattern can support it.

## 6. Reusable Buttons and Action States

Reuse existing shared button components and established button styles.

Typical actions may include:
- New / Create
- Edit
- View
- Save
- Save Draft
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

These actions are examples. Do not display every action on every screen.

Rules:
- Use consistent labels, icons, spacing, and visual hierarchy.
- Provide loading feedback during long-running operations.
- Disable actions when an operation is in progress or is not valid for the current state.
- Prevent accidental duplicate submissions.
- Do not use browser-native `alert()`, `confirm()`, or `prompt()`.
- Prefer inline feedback and existing shared notification or confirmation components.
- Do not show actions that are unsupported by the backend workflow.
- Distinguish soft deletion from permanent deletion in labels and feedback.

A disabled or hidden button is not a security control. The backend must enforce authorization.

## 7. Transaction Draft Mode

Every applicable transaction screen must follow the actual backend Draft workflow.

Examples include Purchase Orders, Goods Receipts, Stock Receipts, Stock Issues, Putaway, Sales Orders, and Dispatch Notes.

### 7.1 Creating and editing drafts

- Start in Draft when that matches the established workflow.
- Allow incomplete drafts only where the workflow permits them.
- Use backend APIs to persist drafts where durable storage is required.
- Allow eligible drafts to be reopened and updated.
- Keep frontend state consistent with the backend response.
- Do not treat browser memory or local storage as the authoritative transaction record.
- Do not allow saving a draft to accidentally submit, approve, or post it.
- Use actual backend statuses and transitions. Do not invent new states.

### 7.2 Draft and posting buttons

Display buttons based on the current record state and permissions.

For example, a Draft may expose Save Draft, Update Draft, Submit, or Cancel if those actions are supported. A Posted transaction may be read-only and expose only the operations permitted by the backend.

- Validate fields before submission where appropriate.
- Show backend validation errors clearly.
- Disable repeated actions while a request is processing.
- Refresh the displayed status after a successful operation.
- Do not simulate posting by changing a frontend status property.
- Do not assume that saving a transaction creates inventory movements.

### 7.3 Unsaved changes

- Prevent accidental loss of unsaved changes.
- Use the established navigation and confirmation pattern.
- Prefer an inline confirmation or existing shared component instead of browser-native confirmation dialogs.
- Do not interrupt users with unnecessary confirmation prompts when no changes exist.

## 8. Editable and Read-Only Data Grids

Reuse the existing shared data-table or grid engine wherever possible.

Support the required modes through configuration where the shared implementation permits:

1. Editable Master
2. Read-only Master
3. Editable Transaction
4. Read-only Transaction

Common features, where supported and relevant:
- Search and filtering
- Sorting and paging
- Row selection
- Column visibility
- Table, list, or card presentation
- Single-row and bulk actions
- Loading, empty, and error states

### 8.1 Editable grids

- Follow existing cell-editing and row-editing conventions.
- Prevent accidental commits or loss of edits.
- Validate values before sending them to the backend.
- Preserve decimal precision according to the relevant field's requirements.
- Use reusable lookup components for product, customer, supplier, location, and other entity selection where appropriate.
- Do not overwrite earlier rows when multiple rows are selected or added.
- Keep row identity stable and handle API failures without silently discarding user edits.
- Respect current record state and permissions.

### 8.2 Read-only grids

- Do not expose editing controls for records that must be read-only.
- Do not rely on disabled visual controls to protect data.
- Use the same established display, search, paging, and status conventions where applicable.
- Keep read-only transaction history accessible where the user has permission.

### 8.3 Multiple grids and tabs

When one screen contains multiple logically distinct grids or detail sections:
- Use tabs when they improve navigation and organization.
- Keep each tab's data, selection, filtering, paging, loading, and error states appropriately independent.
- Avoid accidental state leakage between grids.
- Apply the correct permissions and editability to each tab.
- Reuse the shared grid implementation rather than building a new one for each tab.

## 9. PrimeNG Usage Rules

- Inspect the installed Angular and PrimeNG versions before implementation.
- Follow the project's existing PrimeNG and theme configuration.
- Prefer existing ApexERP shared components when they meet the requirement.
- Use PrimeNG selectively rather than introducing unnecessary widgets.
- Follow APIs supported by the installed version.
- Do not copy code from incompatible versions.
- Do not introduce a new PrimeNG dependency without a clear requirement.
- Do not replace the current theme or introduce a competing design system without approval.
- Keep library-specific implementation isolated where practical.
- Avoid unnecessary overlays, dialogs, and popup-based forms.
- Recheck existing screens if a shared PrimeNG wrapper or theme integration changes.

## 10. API Integration

Use the established API client, services, authentication mechanism, and request/response contracts.

- Inspect the corresponding backend endpoint and DTO before implementing integration.
- Do not invent endpoint routes or DTO fields.
- Follow the existing authentication-header and tenant-context mechanisms.
- Keep API calls out of reusable presentation components where the established architecture expects services.
- Reuse existing services instead of duplicating HTTP logic.
- Handle cancellation and component destruction appropriately for the existing Angular version and patterns.
- Avoid duplicate requests and stale updates.
- Refresh or reconcile data after successful mutations.
- Do not assume an HTTP request succeeded until the response confirms success.
- Do not implement business operations using mock data or local storage when the real backend API is required.

## 11. Role-Based UI Permissions

Use the existing authentication and permission services.

- Inspect how the project currently exposes effective permissions to Angular.
- Use established guards, directives, helpers, or permission services where available.
- Do not hard-code role names across components when permission-based checks are available.
- Do not assume a role has a permission without checking the existing configuration.
- Hide or disable actions that the user is not authorized to perform.
- Respect tenant, company, branch, warehouse, and record scope where the UI supports those contexts.
- Handle expired sessions and access-denied responses consistently.
- Do not derive permissions from a username or a user-selected role.
- Do not duplicate or invent permission rules independently of the backend.

Frontend permission checks improve usability only. Every protected API operation must enforce authorization on the backend.

## 12. Soft Delete, Deactivation, and Restoration

Follow the backend's actual deletion and retention model.

- Use accurate action labels such as Deactivate, Delete, Restore, or Reactivate.
- Do not describe a soft delete as permanent deletion.
- Use existing shared confirmation and feedback components where necessary.
- Avoid browser-native confirmation dialogs.
- Remove successfully deactivated records from active lists where appropriate.
- Refresh list data after successful deletion or restoration.
- Show backend validation, conflict, and permission errors clearly.
- Do not assume a deleted record can be restored without conflicts.
- Provide deleted-record views or restoration controls only where required and authorized.
- Do not implement frontend-only deletion that bypasses backend persistence.
- Do not allow ordinary UI actions to physically delete posted transactions or inventory history.

## 13. Loading, Empty, Error, and Success States

Use the existing shared state components wherever available.

Every relevant screen should handle:
- Initial loading
- Background refresh or processing
- Empty results
- Validation errors
- Successful operations
- Network failures
- Authentication failure or session expiry
- Access denied
- Not found
- Conflict
- Unexpected errors

Rules:
- Avoid blank screens without useful feedback.
- Avoid displaying raw stack traces or internal server details.
- Preserve user input when safely handling recoverable errors.
- Do not show success feedback before the backend confirms success.
- Avoid duplicate error notifications for the same failure.
- Make retry behavior safe and appropriate for the operation.
- Prevent repeated submissions while a request is in progress.

## 14. Forms, Lookups, and Data Integrity

- Reuse established form controls and lookup components.
- Follow existing typed-form and validation conventions.
- Distinguish display labels from stored identifiers.
- Ensure selected entities are correctly associated with their identifiers.
- Avoid hard-coded lookup values when the backend provides authoritative master data.
- Handle empty lookup results and loading failures.
- Avoid silently clearing dependent fields when a parent selection changes.
- Preserve valid user input when possible.
- Follow the backend's decimal precision, date, currency, and quantity conventions.
- Do not rely on frontend calculations as the final authority for financial or inventory values.

## 15. Navigation and Routing

- Follow existing route and navigation conventions.
- Reuse existing route guards and layout components.
- Do not expose a route as an authorization substitute.
- Preserve expected navigation and unsaved-change handling.
- Avoid duplicate routes and inconsistent menu entries.
- Display navigation items according to the existing permission model where appropriate.
- Ensure direct navigation to a protected route is handled correctly.
- Do not modify unrelated navigation or routing during a focused feature change.

## 16. Performance and Accessibility

- Avoid unnecessary change detection work, duplicate requests, and repeated subscriptions.
- Use the existing Angular patterns for subscription cleanup and reactive state.
- Apply pagination or server-side filtering where appropriate for large datasets.
- Avoid loading entire datasets when only a subset is required.
- Keep controls keyboard accessible.
- Provide appropriate labels and accessible names.
- Use clear focus behavior and validation feedback.
- Maintain responsive layouts for supported screen sizes.
- Avoid unnecessary animations or UI complexity.

Follow patterns supported by the installed Angular version rather than introducing APIs from newer versions without verification.

## 17. Testing and Browser Verification

For UI changes, test the actual workflow where the required environment is available.

### 17.1 Build and automated tests

Run the relevant existing commands for:
- Angular compilation and build
- Unit tests
- Component or integration tests
- Linting or static analysis, where configured

Do not invent scripts. Inspect `package.json` and workspace configuration first.

### 17.2 Real browser testing

Where available, verify:
- Screen loading and navigation
- Create, edit, view, and Draft workflows
- Grid editing and multi-row behavior
- Lookup selection
- Buttons and permitted actions
- Form validation and backend errors
- Role-based visibility and disabled states
- Soft delete and restoration
- Loading, empty, success, and failure states
- Responsive behavior and keyboard interaction
- API persistence and refresh consistency

Automated unit tests alone do not prove the complete browser workflow works.

Do not claim tests passed unless they were actually executed and passed. State which tests were not run.

## 18. Change Management and Git Safety

Before editing:
1. Inspect the working tree and existing changes.
2. Read root and project-level instructions.
3. Inspect the relevant screen, service, model, endpoint, and reusable components.
4. Identify the smallest safe implementation.

During editing:
- Preserve unrelated user changes.
- Avoid broad refactoring.
- Do not overwrite working implementations unnecessarily.
- Avoid adding dependencies without justification.
- Do not change backend contracts without verifying the backend requirement.

Do not commit, push, pull, reset, rebase, force-push, or switch branches unless explicitly instructed.

Do not discard user changes or perform destructive operations.

## 19. Web Completion Checklist

Before declaring a task complete, verify the applicable items:

- [ ] Existing components and working examples were inspected.
- [ ] Shared components were reused where appropriate.
- [ ] Theme and PrimeNG conventions were preserved.
- [ ] Buttons reflect actual workflow states and permissions.
- [ ] Draft behavior matches the backend workflow.
- [ ] Editable and read-only grids behave correctly.
- [ ] Multiple grids and tabs maintain independent state where needed.
- [ ] API requests use existing contracts and services.
- [ ] Soft-delete and restoration behavior follows the backend.
- [ ] Loading, empty, success, and error states are handled.
- [ ] Relevant build and tests were executed.
- [ ] Browser verification was performed where available.
- [ ] Existing user changes were preserved.
- [ ] Actual results and unresolved issues were documented.

## 20. Instructions for Angular Coding Agents

At the beginning of every Web task:

1. Read the root `AGENTS.md`.
2. Read this Web `AGENTS.md`.
3. Inspect the existing Angular implementation and working examples.
4. Identify reusable components, services, models, and patterns.
5. Confirm the relevant backend API contract.
6. Make only the changes needed for the requested task.
7. Run relevant tests and browser checks where available.
8. Summarize changed files, actual test results, and unresolved issues.

Do not invent missing APIs, components, permissions, or workflow states. Ask before making consequential architectural changes that are not clearly required.


## Angular File Separation and Naming Standards

### 1. Separate HTML, CSS, and TypeScript

Every Angular component must keep its responsibilities in separate files.

Use:
- `.component.ts` — component logic, properties, methods, inputs, outputs, and event handling.
- `.component.html` — template markup and bindings.
- `.component.css` — component-specific styles.

Example:

```text
sales-order/
├── sales-order.component.ts
├── sales-order.component.html
└── sales-order.component.css
```

Do not combine HTML templates or component CSS inside the TypeScript file.

Avoid:
- Inline templates using `template:`
- Inline component styles using `styles:`
- Large template strings in TypeScript
- Embedding HTML or CSS in TypeScript for convenience

Use `templateUrl` and `styleUrls` according to the installed Angular version and existing project conventions.

### 2. Naming Convention

Use traditional Angular filenames with the file type suffix at the end.

Examples:

| Purpose | Filename |
|---|---|
| Component logic | `sales-order.component.ts` |
| Component template | `sales-order.component.html` |
| Component styles | `sales-order.component.css` |
| Service | `sales-order.service.ts` |
| Model | `sales-order.model.ts` |
| Interface | `sales-order.interface.ts` |
| Routing module, where used | `sales-order-routing.module.ts` |
| Module, where used | `sales-order.module.ts` |
| Pipe | `currency-format.pipe.ts` |
| Directive | `permission.directive.ts` |
| Guard | `auth.guard.ts` |
| Validator | `quantity.validator.ts` |
| Unit test | `sales-order.component.spec.ts` |

Use lowercase kebab-case for filenames. Keep names descriptive and consistent with existing project conventions.

### 3. Component Declaration

A component should reference its separate files explicitly.

Example:

```typescript
@Component({
  selector: 'app-sales-order',
  templateUrl: './sales-order.component.html',
  styleUrls: ['./sales-order.component.css']
})
```

Verify the installed Angular version and existing project configuration before choosing component metadata options. If the project uses a newer supported equivalent, follow the project's established convention while keeping the files separate.

### 4. CSS Rules

- Keep component-specific styles in the corresponding `.component.css` file.
- Keep global styles and design tokens in the established global stylesheet or theme files.
- Reuse existing design tokens and shared styles.
- Avoid duplicating CSS across components.
- Do not use inline styles in HTML as a substitute for reusable CSS, except for narrowly justified dynamic styling.
- Avoid broad global selectors that unintentionally affect unrelated screens.
- Do not introduce another styling framework without approval.

### 5. HTML Rules

- Keep component markup in the `.component.html` file.
- Keep templates readable and focused on presentation.
- Avoid complex business logic in template expressions.
- Use existing reusable form, button, grid, status, loading, error, and empty-state components.
- Follow accessible markup and established Angular binding conventions.

### 6. TypeScript Rules

- Keep component logic in `.component.ts`.
- Move reusable API calls into the established service layer.
- Keep interfaces and models in their appropriate files when they are shared or sufficiently substantial.
- Avoid putting large blocks of HTML or CSS into TypeScript.
- Avoid unnecessary `any` types and duplicated business logic.
- Follow the project's existing Angular, TypeScript, and state-management conventions.

### 7. Existing Files and Refactoring

- Inspect the current component structure before modifying it.
- For new components, create separate TypeScript, HTML, and CSS files unless an existing project-wide rule explicitly requires otherwise.
- When modifying an existing component, preserve its behavior and make the smallest necessary change.
- Do not rename or restructure existing files across the application as part of an unrelated feature.
- If an existing component uses inline HTML or CSS, separate it when requested or when it is part of the approved task scope.
- Update component metadata and imports correctly when moving templates or styles.
- Run relevant builds and tests after structural changes.

### 8. Required Review

Before completing an Angular task, check:
- [ ] HTML is in a separate `.component.html` file.
- [ ] CSS is in a separate `.component.css` file.
- [ ] Component logic is in a separate `.component.ts` file.
- [ ] Filenames follow the established Angular naming convention.
- [ ] No unnecessary inline template or inline component styles were introduced.
- [ ] Existing theme and shared CSS conventions are preserved.
- [ ] The application builds and relevant tests pass.


## Angular Routing, Page Navigation and Route Management

### 1. Routing Architecture

- Use Angular Router for application navigation.
- Keep route configuration in the established routing files; do not scatter route definitions across unrelated components.
- Follow the existing project structure. Use feature-level routing files where appropriate.
- Keep page components, route definitions, navigation menus, guards, and API services separated by responsibility.
- Reuse existing routing and layout components rather than creating duplicate navigation infrastructure.
- Use descriptive, consistent, lowercase kebab-case route paths.

Example:

```text
/tenant-admin
/tenant-admin/users
/tenant-admin/roles
/procurement/purchase-orders
/inventory/putaway
/sales/orders
/sales/dispatch
```

These are illustrative paths. Inspect the actual route configuration before adding or changing routes.

### 2. Page and Route Mapping

Every navigable page must have an intentional route and appropriate navigation behavior.

- Register new pages in the correct routing configuration.
- Connect navigation menu items to their corresponding routes.
- Ensure buttons, breadcrumbs, page links, and transaction navigation use Angular Router appropriately.
- Use route parameters for record identifiers when needed.
- Use query parameters for appropriate filters, searches, pagination, or other shareable page state.
- Validate route parameters before loading or updating records.
- Handle invalid identifiers, missing records, and unmatched routes appropriately.
- Provide a not-found page or reuse the existing one.
- Avoid duplicate route definitions and conflicting wildcard routes.
- Place wildcard routes after valid application routes.
- Use lazy loading where it fits the existing architecture and improves application loading.

### 3. Route Guards and Permissions

- Reuse existing authentication and authorization guards.
- Protect routes that require an authenticated session.
- Apply role and permission checks to restricted pages and operations.
- Keep route guards aligned with the backend's authoritative permissions.
- Do not treat hiding a menu item or blocking a route in Angular as sufficient security.
- Never trust role, permission, tenant, company, branch, or warehouse values supplied by the browser without backend validation.
- Preserve tenant and organizational scope when navigating between pages.
- Redirect unauthenticated users through the established authentication flow.
- Show an appropriate access-denied page when the user lacks permission.
- Prevent redirect loops between authentication, authorization, and error routes.

### 4. Browser Refresh and Direct URL Access

Every valid application route must behave correctly when the user:

- Refreshes the browser.
- Opens a page URL directly.
- Opens a bookmarked route.
- Opens a link in a new tab.
- Navigates backward or forward using browser history.

Requirements:

- Restore the intended route after a page refresh when the session is still valid.
- Re-fetch authoritative record data from the API when required.
- Do not rely exclusively on component memory or transient in-memory state to identify the current page or record.
- Preserve valid route parameters and query parameters.
- Handle expired authentication, missing records, insufficient permissions, and network failures safely.
- Configure the web server or hosting environment to serve the Angular application entry point for valid client-side routes.
- Do not redirect every refreshed URL to the home page.
- Do not bypass authentication or authorization to make refresh work.

### 5. Navigation State and Unsaved Changes

- Use Angular Router navigation rather than manually assigning browser locations for ordinary in-app navigation.
- Preserve relevant filters and search state in route or query parameters when appropriate.
- Warn users before leaving a page with unsaved changes using the application's established confirmation pattern.
- Avoid browser `alert()`, `confirm()`, and `prompt()`.
- Use an existing reusable confirmation component or navigation guard where appropriate.
- Do not block navigation unnecessarily when no unsaved changes exist.
- Prevent duplicate navigation actions from causing duplicate submissions or repeated transactions.

## Authentication and Token Management

### 6. Centralized Authentication Architecture

- Inspect the existing authentication service, HTTP interceptors, guards, API configuration, and backend authentication endpoints before making changes.
- Use a centralized authentication service for login, logout, session state, and token lifecycle management.
- Use a centralized HTTP interceptor or the established equivalent to attach authentication credentials to applicable API requests.
- Avoid implementing separate token-handling logic in individual pages or business services.
- Follow the backend's actual access-token, refresh-token, expiry, rotation, revocation, and logout contracts.
- Do not invent endpoints, token formats, or backend capabilities.

### 7. Access-Token Management

- Attach the valid access token to protected API requests using the authentication scheme required by the backend, normally `Authorization: Bearer <token>`.
- Do not attach credentials indiscriminately to unrelated external URLs.
- Detect authentication failures through centralized HTTP handling.
- Distinguish HTTP 401 (unauthenticated or expired session) from HTTP 403 (authenticated but not authorized).
- Do not treat every HTTP 403 response as a reason to refresh tokens or log the user out.
- Do not repeatedly retry requests with an invalid access token.
- Keep token expiry handling consistent across application pages and tabs where applicable.

### 8. Refresh-Token Lifecycle

Implement refresh-token behavior only in accordance with the backend's supported refresh-token mechanism.

If the backend supports refresh tokens:

- Refresh the access token when required by the agreed authentication design.
- Avoid refreshing on every API request.
- Prevent multiple simultaneous 401 responses from initiating independent refresh operations.
- Coordinate concurrent requests so they can wait for one refresh operation and then continue safely.
- Retry an eligible original request at most once after a successful refresh.
- Avoid retrying non-authentication failures or requests that are not safe to replay.
- Handle refresh failure, token expiry, revocation, and invalid refresh tokens consistently.
- If refresh-token rotation is supported, use the newest returned token and never continue using a superseded token.
- Prevent infinite refresh loops if the refresh endpoint itself returns an authentication failure.
- Ensure pending requests fail cleanly if the refresh operation fails.

If the backend does not yet support refresh tokens, do not simulate refresh behavior in Angular. Use the existing access-token expiry and re-authentication behavior until backend support is implemented.

### 9. Token Storage and Security

- Inspect the current token-storage approach before changing it.
- Do not store credentials in source code, configuration files committed to version control, URLs, route parameters, or query strings.
- Do not log access tokens, refresh tokens, passwords, or authentication headers.
- Prefer appropriately configured `HttpOnly`, `Secure`, and `SameSite` cookies for refresh-token storage when the backend architecture supports them.
- If cookies are used for authentication, follow the backend's CSRF protection requirements.
- If the existing architecture uses browser-accessible storage, assess the XSS risk and avoid expanding token exposure.
- Do not move tokens between storage mechanisms without reviewing the backend contract, deployment configuration, and existing authentication flow.
- Do not assume an Angular-only change can make an insecure token-storage design secure.

### 10. Tenant and Session Context

- Follow the existing mechanism for sending tenant context, such as `X-Tenant-Id`, when required by the backend.
- Do not treat tenant context supplied by the frontend as authorization.
- Ensure login, refresh, logout, and session restoration follow the backend's tenant and identity rules.
- Do not silently reuse a previous user's tenant context after logout or account switching.
- Clear user-specific cached data, in-memory session state, and relevant persisted application state when logging out.
- Do not clear shared application configuration unnecessarily.
- Prevent requests from a previous session from repopulating the new user's state.

### 11. Logout and Expired Sessions

- Provide a centralized logout operation.
- Use the backend logout or refresh-token revocation endpoint when one exists and is required by the authentication design.
- Clear the local authentication state even when the logout request fails.
- Prevent protected pages from remaining usable after the session has expired.
- Redirect to the configured login route and preserve a safe return URL when appropriate.
- Validate the return URL to prevent open redirects.
- Do not redirect to login repeatedly when already on the login page.
- Handle a failed refresh operation once, then transition to the established unauthenticated state.
- Do not expose sensitive server errors or credentials in the user interface.

### 12. Testing Requirements

Verify the following where applicable:

- [ ] Every registered page route opens correctly.
- [ ] Browser refresh preserves valid routes and reloads required data.
- [ ] Direct URL access and browser history navigation work.
- [ ] Unauthenticated users cannot access protected pages.
- [ ] Unauthorized users receive the appropriate access-denied behavior.
- [ ] Expired access tokens follow the configured refresh or re-login flow.
- [ ] Concurrent 401 responses do not trigger multiple refresh operations.
- [ ] Successful refresh retries eligible requests only once.
- [ ] Failed refresh operations do not cause infinite loops.
- [ ] Logout clears the previous user's session and relevant state.
- [ ] Tenant context does not leak across user sessions.
- [ ] Invalid routes and missing records are handled appropriately.
- [ ] Existing routes, authentication behavior, and API integrations remain functional.
- [ ] Relevant automated tests, build checks, and browser tests pass.

### 13. Implementation Rules

Before modifying routing or authentication:

1. Inspect the existing Angular route configuration, guards, interceptors, authentication services, and storage mechanism.
2. Inspect the backend authentication and refresh-token implementation to confirm supported behavior.
3. Identify reusable services and existing conventions.
4. Make the smallest changes needed for the requested task.
5. Do not replace the authentication architecture or rewrite routing globally without explicit approval.
6. Test both successful and failure paths.
7. Report which behaviors were verified and which depend on backend functionality that is not implemented.