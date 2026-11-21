# ApexERP — Customer Registration, Workspace & E-commerce Publishing Specification

**Document Version:** 1.4.0  
**Status:** Architecture & Requirements Specification — E-commerce Product Publishing Backend Implemented  
**Date:** 2026-11-20  
**Target Repository:** `ApexERP` (`Backend/ERP`, `Frontend/Web`)  
**Related Documents:**
- [Sales-Quotation-Workflow.md](file:///z:/ERP/Sales-Quotation-Workflow.md) (v1.3.2)
- [Shared-Email-Retry-Architecture.md](file:///z:/ERP/Shared-Email-Retry-Architecture.md) (v1.2.0)
- [ApexERP-Frontend-UI-Standards.md](file:///z:/ERP/ApexERP-Frontend-UI-Standards.md) (v1.2.0)
- [Root Instructions](file:///z:/ERP/AGENTS.md)
- [Backend Instructions](file:///z:/ERP/Backend/ERP/AGENTS.md)
- [Frontend Instructions](file:///z:/ERP/Frontend/Web/AGENTS.md)

---

## Document Taxonomy & Legend

Throughout this document, every architectural component, workflow, schema, and API specification is strictly classified using the following normative tags:

| Tag | Definition |
|---|---|
| `[Verified Existing Capability]` | Confirmed present in the codebase via source-code inspection of entities, services, controllers, or configurations. |
| `[Agreed Business Rule]` | Binding business logic confirmed across project specifications and governance instructions. |
| `[Proposed Requirement]` | Architectural design proposed to satisfy prompt requirements while adhering to system invariants. |
| `[Pending Decision]` | Architectural or business alternative requiring explicit stakeholder sign-off before implementation. |
| `[Future Implementation]` | Formal capability documented in architecture but deliberately deferred to subsequent development phases. |
| `[Source-Code Verification Required]` | Component where initial inspection was completed, but detailed edge-case parameters require runtime or schema confirmation. |

---

## 1. Purpose and Strategic Scope

### 1.1 Objective
This specification defines the functional, architectural, security, and integration design for the **ApexERP Customer Registration** and **Customer Workspace**. It establishes:
1. **One Unified Customer Workspace:** A self-service portal shell sharing the proven layout, navigation ergonomics, and design system of the internal ERP Tenant Workspace, while maintaining strict isolation of business logic, permissions, and customer data.
2. **Two Account Types on One Shared Registration Screen:** Individual Customer and Company Customer account types supported through a single reusable registration journey and backend registration pipeline.
3. **Phased Implementation Priority:** **Company Customer Registration and Workspace capabilities represent Phase 1 (Immediate Priority)**. Individual Customer capabilities are architecturally specified as a future phase (`[Future Implementation]`).
4. **Registration Simplification (Phase 1):** Email verification is **deferred** to a future enhancement. Password complexity rules are **not required** (keeping simple password hashing via existing infrastructure). Newly registered customers activate immediately upon successful account creation.
5. **Multi-Selling-Company Switching Within a Tenant:** One customer registration supports transacting with multiple authorized selling companies operating under the same tenant. The customer can switch active company context on the fly without re-authenticating.
6. **Separation of Four Core Identities:** Strict operational distinction between **Tenant**, **Selling Company**, **Customer Company**, and **Customer Contact**.
7. **Three Customer Quotation Request Entry Paths:** Facilitating new quotation requests, requests cloned from historical Sales Orders, and requests cloned from previous Sales Quotations without mutating original records.
8. **Atomic Commercial Acceptance:** Converting accepted, valid Sales Quotations into official `SalesOrder` (`Draft`) records while atomically updating assigned customer `PriceCard` contract lines.
9. **Direct Contract Purchasing:** Authorised Company Customers can purchase directly from their assigned `PriceCard` without requiring quotation bidding.
10. **Customer Order, Delivery, and Invoice Visibility:** Comprehensive post-order visibility into fulfillment progression, dispatch tracking, and financial invoice status without exposing internal operational records, employee notes, or audit trails.

### 1.2 Enterprise Invariants
- `[Agreed Business Rule]` **No Duplicate Business Logic:** Customer transactions MUST NOT introduce separate backend database entities for customer orders, shipments, or invoices. All transactions converge downstream into authoritative `SalesOrder`, `SalesShipment`, and `SalesInvoice` aggregate roots in `DomainDbContext`.
- `[Agreed Business Rule]` **Strict Privilege Isolation:** Customer users must NEVER be assigned internal employee permissions (such as `SALES_QUOTATION.CREATE` or `SALES_ORDER.CONFIRM`) to access customer-facing endpoints. Customer authorization relies exclusively on distinct customer roles and server-derived customer identity contexts.
- `[Agreed Business Rule]` **Tenant & Company Scoping:** No client-supplied `CustomerId`, `CustomerContactId`, `CompanyId`, or `TenantId` will be trusted blindly. All identifiers must be validated against the authenticated user's cryptographically signed security token and database membership records.
- `[Agreed Business Rule]` **Information Security & DTO Separation:** Internal staff notes (`InternalNotes`), reviewer identities (`ApprovedByUserId`), and managerial rejection remarks must never be leaked across customer-facing APIs.

---

## 2. The Four Distinct Business Identities in Multi-Company Multi-Tenancy

To prevent architectural and data-isolation defects, ApexERP strictly distinguishes four separate organizational identities across all customer interactions:

```
┌──────────────────────────────────────────────────────────────────────────────────────────────────┐
│                                       TENANT (ApexERP Instance)                                  │
│                          Example: "Apex Global Industrial Group" (TenantId)                      │
├─────────────────────────────────────────────────┬────────────────────────────────────────────────┤
│               SELLING COMPANIES                 │                CUSTOMER COMPANIES              │
│    (Operating Entities Selling Goods)           │       (External Legal Purchasing Entities)     │
│                                                 │                                                │
│  ┌───────────────────────────────────────────┐  │  ┌──────────────────────────────────────────┐  │
│  │ Selling Company A: "Apex Hardware Ltd"    │◄─┼──┤ Customer Company 1: "Acme Manufacturing"  │  │
│  │ CompanyId: 1111-AAAA                      │  │  │ CustomerId: 8888-CCCC                    │  │
│  └───────────────────────────────────────────┘  │  └────────────────────┬─────────────────────┘  │
│                         ▲                       │                       │                        │
│                         │ (Authorized           │                       ▼                        │
│                         │  Relationships)       │          CUSTOMER CONTACTS             │
│                         ▼                       │    (Individual Human Representatives)  │
│  ┌───────────────────────────────────────────┐  │  ┌──────────────────────────────────────────┐  │
│  │ Selling Company B: "Apex Chemicals Corp"  │◄─┼──┤ Contact: "Johnathan Doe"                 │  │
│  │ CompanyId: 2222-BBBB                      │  │  │ Email: j.doe@acme.com (UserId)           │  │
│  └───────────────────────────────────────────┘  │  │ Designation: Procurement Director        │  │
│                                                 │  └──────────────────────────────────────────┘  │
└─────────────────────────────────────────────────┴────────────────────────────────────────────────┘
```

### 2.1 Rigorous Identity Definitions `[Agreed Business Rule]`

| Identity | Architectural Definition | Database Aggregate & Scope | Role in Customer Transactions |
|---|---|---|---|
| **1. Tenant** | The top-level cloud subscriber or enterprise organization operating the ApexERP instance. | `Tenant` in `SaaSDbContext`; `ITenantScopedEntity` on all domain entities (`TenantId`). | Enforces multi-tenant data boundary. Customers cannot see data from other tenants under any circumstances. |
| **2. Selling Company** | A distinct legal business entity or operational subsidiary operating *within* the Tenant that manufactures, stocks, and sells products to customers. | `Company` in `DomainDbContext` (`CompanyId`). Owns warehouses, branches, price cards, and tax codes. | The seller entity issuing quotations and fulfilling orders. A customer selects an active Selling Company to explore products and place orders. |
| **3. Customer Company** | The external commercial enterprise, corporate client, or institutional buyer purchasing goods from Selling Companies. | `Customer` in `DomainDbContext` (`CustomerId`). Stores legal name, registration number, tax ID, and address. | The contracting buyer party. Holds negotiated B2B Price Cards and master transaction histories. |
| **4. Customer Contact** | The individual human professional who registers, authenticates, and acts on behalf of the Customer Company. | `User` in `SecurityDbContext` (login identity); `CustomerContact` & `Contact` in `DomainDbContext`. | The human actor initiating requests. Must have authorized, verified permissions for the Customer Company. |

### 2.2 Relational Invariants `[Agreed Business Rule]`
1. **Never Confuse Tenant with Selling Company:** A Tenant may operate multiple independent Selling Companies (e.g., Hardware division vs. Chemical division). Products, catalogs, pricing, inventory, and invoices belong to specific Selling Companies, not to the Tenant as a flat monolith.
2. **Never Confuse Customer Company with Customer Contact:** External companies do not have a single shared login. Every human contact has an individual login identity (`User.Email`). One Customer Company can have multiple authorized contacts (e.g., buyers, financial controllers, viewers).
3. **Multi-Selling-Company Relationships:** A Customer Company may maintain commercial accounts with multiple Selling Companies within the same Tenant. For example, Acme Manufacturing purchases fasteners from *Selling Company A* and lubricants from *Selling Company B*.
4. **Contact Permission Scoping:** A Customer Contact's authority is validated independently for each Selling Company relationship. Being authorized to buy from Selling Company A does not automatically grant purchasing authority for Selling Company B without verified membership.

---

## 3. Customer Account Types & Identity Models

ApexERP defines two mutually exclusive customer account models operating within the multi-tenant architecture:

```
                            ┌─────────────────────────────────────────┐
                            │        ApexERP Customer Accounts        │
                            └────────────────────┬────────────────────┘
                                                 │
                    ┌────────────────────────────┴────────────────────────────┐
                    ▼                                                         ▼
    ┌───────────────────────────────┐                         ┌───────────────────────────────┐
    │      Individual Customer      │                         │       Company Customer        │
    ├───────────────────────────────┤                         ├───────────────────────────────┤
    │ • Natural human consumer      │                         │ • Registered legal corporate  │
    │ • Standard retail pricing     │                         │ • Negotiated Price Card / MOQ │
    │ • User account IS the customer│                         │ • Multiple authorised contacts│
    │ • Self-service profile        │                         │ • Multi-company relationships │
    │ • Status: Future Phase        │                         │ • Status: Phase 1 Priority    │
    └───────────────────────────────┘                         └───────────────────────────────┘
```

### 3.1 Individual Customer Model `[Proposed Requirement]` `[Future Implementation]`
- **Definition:** An individual natural person purchasing products for personal or sole-proprietor use.
- **Identity Relationship:** 1:1 relationship between the Security `User` account and the `Customer` record in `DomainDbContext`.
- **Pricing Basis:** Resolves strictly to standard retail selling prices (`Product.SellingPrice`). Corporate contract Price Cards and volume MOQs do not apply.
- **Operational Boundaries:** Individual customers have no concept of employee contacts, organizational hierarchy, or multi-user purchasing approvals. They view and manage only their own direct orders and shipments.

### 3.2 Company Customer Model `[Agreed Business Rule]` `[Phase 1 Priority]`
- **Definition:** A legally registered corporate entity, institution, or commercial enterprise purchasing products under formal commercial terms.
- **Identity Relationship:** 1:N relationship. A single `Customer` (Company) entity is associated with one or more distinct human contacts (`CustomerContact` linked to `Contact` and Security `User`).
- **Login Distinction:**
  - **Company Identity:** Company name, registration number, tax ID, general company email (`orders@acmecorp.com`), and corporate telephone.
  - **Contact Identity:** Each authorized human representative logs in with their own personal, verified business email address (`john.doe@acmecorp.com`).
  - **Rule:** A company **never shares a single group password**. Every human actor is individually authenticated and traceable.
- **Pricing Basis:** Resolves to the customer's assigned B2B Sales Price Card (`customer.DefaultSalesPriceCardId`) for the active Selling Company. Quotations allow custom project pricing that updates the contract card upon acceptance.

---

## 4. Customer Registration — One Shared Registration Architecture

### 4.1 Unified Registration User Experience `[Proposed Requirement]`
In alignment with the verified [ApexERP-Frontend-UI-Standards.md](file:///z:/ERP/ApexERP-Frontend-UI-Standards.md), Customer Registration is implemented as a single, clean, public-facing page (`/portal/register`) featuring an account-type toggle.

```
┌────────────────────────────────────────────────────────────────────────┐
│                        ApexERP Customer Portal                         │
│                           Create an Account                            │
├────────────────────────────────────────────────────────────────────────┤
│  Account Type:   (o) Company / Business       ( ) Individual           │
├────────────────────────────────────────────────────────────────────────┤
│                                                                        │
│  [COMPANY INFORMATION]                                                 │
│  Registered Company Name *       Trading Name (Optional)               │
│  [ Acme Industrial Ltd         ] [ Acme Tools                        ] │
│  Company Registration No. *      Tax / VAT Identification No.          │
│  [ REG-994821                  ] [ TAX-883719-X                      ] │
│  General Company Email *         Company Telephone *                   │
│  [ info@acmeindustrial.com     ] [ +1 (555) 019-2831                 ] │
│  Registered Address Line 1 *     City *             Country *          │
│  [ 100 Industrial Parkway      ] [ Chicago        ] [ United States  ] │
│                                                                        │
│  [AUTHORISED HUMAN CONTACT & LOGIN CREDENTIALS]                        │
│  Contact Person Full Name *      Job Title / Designation *             │
│  [ Johnathan Doe               ] [ Procurement Director              ] │
│  Verified Login Email *          Mobile Telephone *                    │
│  [ j.doe@acmeindustrial.com    ] [ +1 (555) 019-4482                 ] │
│  Password *                      Confirm Password *                    │
│  [ ••••••••••••••••••••••••••  ] [ ••••••••••••••••••••••••••        ] │
│                                                                        │
│  [x] I confirm I am authorised to register on behalf of this company.  │
│                                                                        │
│  [ Create Account ]               Already registered? [ Sign In ]      │
└────────────────────────────────────────────────────────────────────────┘
```

### 4.2 Phase 1 Registration Refinements `[Agreed Business Rule]`

#### A. Email Verification — Deferred to Future Enhancement `[Agreed Business Rule]`
- **No Email Verification in Phase 1:** Registration **does not require email verification**.
- **No Verification Tokens or Emails:** The backend will not generate email verification tokens or dispatch verification emails during registration.
- **Immediate Account Activation:** Newly created customer user accounts are created with `IsActive = true`.
- **Immediate Login Allowed:** The customer can immediately log in upon successful registration submission.
- **Future Enhancement `[Future Implementation]`:** Email verification via cryptographic HMAC tokens and `RetryingEmailServiceDecorator` is documented as a future security enhancement.
- **Security Boundary:** Deferring email verification does **not** bypass company verification or purchasing authority checks. An activated user can explore products and submit quotation requests, but cannot accept binding financial commitments until corporate authorization is confirmed.

#### B. Password Policy — Complexity Rules Not Required `[Agreed Business Rule]`
- **No Complexity Enforcement:** Phase 1 **does not implement password-complexity rules**.
- **No Regex Strength Requirements:** Does not require uppercase letters, lowercase letters, numbers, or special characters.
- **Field Retention:** Keeps standard `Password` and `ConfirmPassword` input fields with basic length validation (e.g., minimum 8 characters).
- **Existing Hashing Infrastructure `[Verified Existing Capability]`:** The system continues using the established, secure password-hashing engine (`IPasswordService` in `Backend/ERP/Security/Services/PasswordService.cs`) utilizing salted hashes. Registration remains streamlined and user-friendly.

### 4.3 Company Customer Registration Flow (Phase 1 Priority) `[Agreed Business Rule]`
1. **Input Fields (Company Details):**
   - Registered Legal Company Name (`CustomerName`).
   - Trading / Doing-Business-As (DBA) Name.
   - Company Registration Number (`CustomerCode` or explicit registration attribute).
   - Tax / VAT Registration Number (`TaxNumber`).
   - General Corporate Email & Telephone (`Email`, `Phone`).
   - Registered Business Address (`AddressLine1`, `AddressLine2`, `City`, `PostalCode`, `Country`).
2. **Input Fields (First Authorised Human Contact):**
   - Contact Person Full Name (`ContactName`).
   - Designation / Job Title (`JobTitle`, e.g., Head of Purchasing).
   - Personal Professional Email Address (used as `User.Username` and `User.Email`).
   - Direct Mobile Number (`MobileNumber`).
   - Password & Confirmation (Standard string, hashed via `IPasswordService`).
3. **Registration Processing Pipeline:**
   ```
   [Submit Registration]
            │
            ▼
   ┌──────────────────────────────────────────────────────────┐
   │ 1. Validate Form & Confirm Password Match                │
   │    (No complex regex rules; minimum length check only)   │
   └────────────────────────────┬─────────────────────────────┘
                                │
                                ▼
   ┌──────────────────────────────────────────────────────────┐
   │ 2. Deduplication Check (CustomerDeduplicationService)    │
   │    - Match on TaxNumber, Company Name, Registration No.   │
   └────────────────────────────┬─────────────────────────────┘
                                │
                                ▼
   ┌──────────────────────────────────────────────────────────┐
   │ 3. Match Evaluation & Entity Linking:                    │
   │    - If Match Found: Link Contact to existing Customer    │
   │      (Membership pending internal corporate validation). │
   │    - If New Company: Create Customer Master record.      │
   └────────────────────────────┬─────────────────────────────┘
                                │
                                ▼
   ┌──────────────────────────────────────────────────────────┐
   │ 4. Create Security User & Customer Contact               │
   │    - Hash Password via existing IPasswordService         │
   │    - Set User.IsActive = true (Immediate Login Allowed)  │
   │    - Create Contact & CustomerContact in DomainDbContext │
   │    - Assign Role: CUSTOMER_COMPANY_ADMIN                 │
   └────────────────────────────┬─────────────────────────────┘
                                │
                                ▼
   ┌──────────────────────────────────────────────────────────┐
   │ 5. Account Created Successfully                          │
   │    - Return JWT Access Token (or direct user to Login)   │
   └──────────────────────────────────────────────────────────┘
   ```
4. **Deduplication & Master Data Linking `[Verified Existing Capability]`:**
   - System reuses `ICustomerDeduplicationService` in `Backend/ERP/Domain/Features/CRM/Deduplication`.
   - If a matching company exists (e.g., existing offline customer onboarding digitally), the registration does not duplicate the `Customer` master. It creates a new `CustomerContact` linked to the existing `CustomerId`.
   - **Crucial Rule:** Matching an existing company **does not automatically prove that the registering human is authorised to represent it**. Company membership and purchasing authority must be separately validated by internal staff or the company's designated primary administrator.

### 4.4 Individual Customer Registration Flow `[Future Implementation]`
- Simplified registration for natural consumers (First Name, Last Name, Email, Mobile, Address, Password).
- Creates 1:1 `Customer` and `User` records.
- Implemented in a subsequent development phase.

---

## 5. Customer Workspace Layout and ERP Workspace Consistency

### 5.1 Architecture & UX Consistency Mandate
In strict accordance with the project instructions, **the Customer Workspace must use the same overall workspace layout, navigation structure, design system conventions, and visual consistency as the existing ERP Tenant Workspace**.

When a customer procurement officer logs in, the portal must feel like a natural, first-class workspace within ApexERP rather than a disconnected consumer microsite.

### 5.2 Inspected ERP Workspace Layout Structure `[Verified Existing Capability]`
Inspection of [tenant-layout.component.html](file:///z:/ERP/Frontend/Web/src/app/layout/tenant-layout/tenant-layout.component.html) confirms the authoritative 3-tier desktop shell:

```
┌──────────────────────────────────────────────────────────────────────────────────────────────────────────────┐
│ .workspace-shell                                                                                             │
├──────────────────────┬───────────────────────────────────────────────────────────────────────────────────────┤
│ .workspace-sidebar   │ .workspace-topbar                                                                     │
│                      │ ┌───────────────────────────────────────┬───────────────────────────────────────────┐ │
│ ┌──────────────────┐ │ │ .topbar-left                          │ .topbar-right                             │ │
│ │ .sidebar-header  │ │ │ [Building] Apex Chemicals Corp (Active│ [Theme Moon] │ [JD] john.doe@acme.com     │ │
│ │ [Icon] ApexERP   │ │ │ Switcher: [Apex Hardware Ltd ▼]       │ Acme Manufacturing │ [Sign Out Button]    │ │
│ │ [Collapse Btn]   │ │ └───────────────────────────────────────┴───────────────────────────────────────────┘ │
│ ├──────────────────┤ ├───────────────────────────────────────────────────────────────────────────────────────┤
│ │ .sidebar-nav     │ │ .workspace-content (<main>)                                                           │
│ │                  │ │                                                                                       │
│ │ ▼ WORKSPACE      │ │   ┌─────────────────────────────────────────────────────────────────────────────────┐ │
│ │   Dashboard      │ │   │ Page Header (Title + Breadcrumbs + Action Buttons)                              │ │
│ │   Explore Prods  │ │   ├─────────────────────────────────────────────────────────────────────────────────┤ │
│ │                  │ │   │                                                                                 │ │
│ │ ▼ COMMERCIAL     │ │   │ Router Outlet Viewport (<router-outlet></router-outlet>)                        │ │
│ │   Quotations     │ │   │ (Dashboard Cards / Data Tables / Step Form Cards / Detail Panels)               │ │
│ │   Sales Orders   │ │   │                                                                                 │ │
│ │   Deliveries     │ │   │                                                                                 │ │
│ │   Invoices       │ │   │                                                                                 │ │
│ │                  │ │   │                                                                                 │ │
│ │ ▼ ACCOUNT        │ │   │                                                                                 │ │
│ │   Company Info   │ │   │                                                                                 │ │
│ ├──────────────────┤ │   │                                                                                 │ │
│ │ .sidebar-footer  │ │   │                                                                                 │ │
│ │ [Avatar] Acme Ltd│ │   │                                                                                 │ │
│ └──────────────────┘ │   └─────────────────────────────────────────────────────────────────────────────────┘ │
└──────────────────────┴───────────────────────────────────────────────────────────────────────────────────────┘
```

### 5.3 Common Layout Elements Preserved in Customer Workspace

| Layout Shell Area | ERP Tenant Workspace (Internal) | Customer Workspace (Self-Service) | Architectural Treatment |
|---|---|---|---|
| **Outer Shell** | `.workspace-shell` with `.sidebar-collapsed` toggle state. | `.workspace-shell` with `.sidebar-collapsed` toggle state. | **100% Identical.** Reuses the same CSS grid, responsive layout, and transitions. |
| **Sidebar Brand Header** | `.sidebar-header` showing `ApexERP` + Tenant Brand tag (`pi-box`). | `.sidebar-header` showing `ApexERP` + `Customer Workspace` tag (`pi-shopping-bag`). | **Preserved Structure.** Brand icon altered to denote customer portal context. |
| **Sidebar Navigation** | Internal ERP modules (Inventory, Procurement, Sales, Master Data, Security). | Customer-facing navigation groups (Workspace, Commercial, Account). | **Separate Navigation Configuration.** Reuses grouped list hierarchy and chevrons. |
| **Sidebar Footer** | `.tenant-card` with Tenant Name and Active Branch scope. | `.tenant-card` displaying Customer Company Name (`Acme Manufacturing Ltd`) and Account Code. | **Preserved Structure.** Displays corporate purchasing identity. |
| **Topbar Left** | Tenant Brand block + Company/Branch scope switcher. | **Active Selling Company Block + Selling Company Switcher** dropdown. | **Consistent Ergonomics.** Allows switching between authorized Selling Companies. |
| **Topbar Right** | Light/Dark theme toggle + `.user-profile-pill` (email, internal role, logout). | Light/Dark theme toggle + `.user-profile-pill` (email, designation, logout). | **100% Identical.** Consistent avatar pill, tooltip, and sign-out mechanics. |
| **Main Content** | `<main class="workspace-content"><router-outlet></router-outlet></main>` | `<main class="workspace-content"><router-outlet></router-outlet></main>` | **100% Identical.** Consumes shared layout tokens. |

### 5.4 Customer Navigation & Design System Conventions `[Verified Existing Capability]`
In compliance with [ApexERP-Frontend-UI-Standards.md](file:///z:/ERP/ApexERP-Frontend-UI-Standards.md):
- **Design Tokens:** Strict consumption of CSS custom variables (`--erp-primary`, `--erp-space-*`, `--erp-radius-*`, `--erp-font-*`).
- **No New PrimeNG Rule:** Zero imports of `ButtonModule`, `DialogModule`, or `InputTextModule`. All interactive surfaces consume native `.erp-button`, `.erp-card`, `.p-inputtext` utility classes.
- **Shared Components Reused:**
  - `AppDataTableComponent`: Customer order registers, quotation lists, delivery trackers, invoice summaries.
  - `AppEditableDataTableComponent`: Quotation request line-item entry (product, quantity, notes).
  - `PageHeaderComponent`: Consistent title, status badge, and contextual action bar.
  - `ConfirmationDialogComponent`: Clean modal for quotation declines, order confirmations, and cancellations.
  - `StatusBadgeComponent`: Unified pill badges for transaction lifecycles.

---

## 6. Selling Company Context & Multi-Company Switching Within a Tenant

### 6.1 Multi-Company Selling Architecture `[Agreed Business Rule]` `[Proposed Requirement]`
Within an ApexERP Tenant, multiple distinct legal businesses or operating companies may sell products (e.g., *Company A: Industrial Fasteners*, *Company B: Petrochemicals*).
- **Single Customer Registration:** A customer registers once under the Tenant. Their login identity (`User.Email`) is global to that Tenant.
- **Multi-Selling-Company Relationships:** That customer may establish authorized purchasing relationships with multiple Selling Companies operating in the Tenant.
- **On-the-Fly Company Switching:** The customer can switch the active Selling Company context within the workspace header **without logging out, re-authenticating, or maintaining multiple user accounts**.

```
                           [Authenticated Customer User]
                                j.doe@acme.com
                                       │
                                       ▼
                     ┌───────────────────────────────────┐
                     │   Selling Company Switcher (UI)   │
                     │   [ Active: Selling Company A ▼ ] │
                     └─────────────────┬─────────────────┘
                                       │
                 ┌─────────────────────┴─────────────────────┐
                 ▼                                           ▼
   ┌───────────────────────────┐               ┌───────────────────────────┐
   │ Context: Selling Company A│               │ Context: Selling Company B│
   ├───────────────────────────┤               ├───────────────────────────┤
   │ • Company A Product Catalog│              │ • Company B Product Catalog│
   │ • Company A Sales PriceCard│              │ • Company B Sales PriceCard│
   │ • Company A Quotations    │               │ • Company B Quotations    │
   │ • Company A Sales Orders  │               │ • Company B Sales Orders  │
   │ • Company A Invoices      │               │ • Company B Invoices      │
   └───────────────────────────┘               └───────────────────────────┘
```

### 6.2 Context Isolation & Operational Rules `[Agreed Business Rule]`
1. **Catalog & Pricing Scope:**
   - When **Selling Company A** is selected, the Explore Products view displays only products offered by Company A, and prices resolve against the customer's Price Card issued by Company A.
   - When switching to **Selling Company B**, the catalog dynamically switches to Company B's catalog and negotiated contract pricing.
2. **Transaction Scoping:**
   - Quotation requests, Sales Orders, shipments, and invoices submitted or viewed while Company A is selected are strictly scoped:
     ```sql
     WHERE TenantId == @TenantId 
       AND CompanyId == @ActiveSellingCompanyId 
       AND CustomerId == @CustomerId
     ```
   - Company B's staff and systems cannot see Company A's transactions; Company A's transactions cannot leak into Company B's registers.
3. **Server-Side Validation — Never Trust Client CompanyId `[Mandatory]`:**
   - The active company is passed via request header `X-Selling-Company-Id` or route parameter.
   - The backend validates on **every single request** that:
     1. The `SellingCompanyId` belongs to the authenticated `TenantId`.
     2. The authenticated `CustomerId` has an active, authorized commercial relationship with that `SellingCompanyId`.
     3. The contact person's user identity is permitted to transact with that specific Selling Company.
   - An unauthorized `CompanyId` returns **HTTP 403 Forbidden**.
4. **Identity Invariant:** Switching companies changes only the operational filtering scope; it **does not alter the customer's user identity or grant extra unauthorized privileges**.

---

## 7. Individual vs. Company Workspace Capabilities

| Functional Dimension | Individual Customer `[Future Implementation]` | Company Customer `[Phase 1 Priority]` |
|---|---|---|
| **Primary Pricing Source** | Standard Retail Catalog Price (`Product.SellingPrice`). | Assigned Contract Sales Price Card for active Selling Company (`customer.DefaultSalesPriceCardId`). |
| **Minimum Order Quantity** | Default retail minimum ($1.00$ unit). | Enforced Price Card Line MOQ (`PriceCardLine.MinimumQuantity`). |
| **Quotation Requests** | Optional for large volume inquiries. | Core procurement channel (3 entry paths scoped to active Selling Company). |
| **Direct Order Creation** | Standard checkout workflow via retail cart. | Direct B2B order from assigned contract Price Card lines for active Selling Company. |
| **Quotation Acceptance** | Formal proposal acceptance converts to Sales Order. | Acceptance converts to Sales Order AND updates active Selling Company Price Card. |
| **Multi-Company Context** | Operates against the primary retail selling entity. | Seamlessly switches between authorized Selling Companies within the tenant. |
| **Multi-Contact Visibility** | Restricted strictly to the individual user. | All authorized company contacts see company orders according to role. |
| **Financial / Invoicing** | Simplified receipt / invoice download. | Comprehensive commercial invoice balance tracking and payment term terms. |
| **Account Management** | Personal profile and saved delivery addresses. | Corporate legal details, tax IDs, and authorized contact management. |

---

## 8. Company Quotation Request Workflows — Three Entry Paths

Company customers require rapid, flexible ways to initiate formal commercial bids. All three paths operate within the **active Selling Company context** and converge into a standard `SalesQuotation` in `SalesQuotationStatus.Draft`:

```
   Path A: New Request              Path B: From Sales Order           Path C: From Old Quotation
┌───────────────────────────┐    ┌───────────────────────────┐      ┌───────────────────────────┐
│ Explore Company Catalog   │    │ Select Past Sales Order   │      │ Select Historical Quotation│
│ Select Items & Quantities │    │ Clone Lines to Draft      │      │ Clone Lines to Draft      │
└─────────────┬─────────────┘    └─────────────┬─────────────┘      └─────────────┬─────────────┘
              │                                │                                  │
              └────────────────────────────────┼──────────────────────────────────┘
                                               │
                                               ▼
                              ┌───────────────────────────────────┐
                              │ Revalidate Product Catalog Active │
                              │ Validate Within Active Selling Co.│
                              │ Revalidate Unit of Measure (UOM)  │
                              │ Revalidate Minimum Quantities     │
                              │ Omit Expired Historical Prices    │
                              │ Preserve Source Document Link     │
                              └────────────────┬──────────────────┘
                                               │
                                               ▼
                              ┌───────────────────────────────────┐
                              │ Submit Quotation Request          │
                              │ Status: Draft (Seller Review)     │
                              │ Audit: Customer Submission Logged │
                              └───────────────────────────────────┘
```

### 8.1 Path A: New Quotation Request `[Agreed Business Rule]`
1. **User Action:** Customer explores products available from the **active Selling Company**.
2. **Product Selection:** Selects one or multiple active products.
3. **Staging:** Specifies requested quantities, required delivery dates, preferred UOM, and delivery destination.
4. **Validation:**
   - Products must be active (`Product.IsActive == true`) and owned by the active Selling Company.
   - Customer must be verified and active.
5. **Submission:** Invokes `POST /api/portal/quotations/request`. Creates `SalesQuotation` with `CompanyId = ActiveSellingCompanyId`, `DocumentType = Quotation`, and initial status `Draft`.

### 8.2 Path B: Request Based on an Existing Sales Order `[Agreed Business Rule]`
1. **Business Use-Case:** A corporate client wishes to repeat an order placed previously with the active Selling Company, but requires updated volume pricing or custom freight terms.
2. **User Action:** From the **Sales Orders** register for the active Selling Company, user clicks **"Request Quotation from Order"**.
3. **Cloning & Validation Invariants:**
   - **Source Document Immutability:** The historical `SalesOrder` is **strictly read-only** and never mutated.
   - **Revalidation of Master Data:** Each line item from `SalesOrderItems` is revalidated against current `Product` master for the active Selling Company. Inactive products are flagged and excluded.
   - **Price Stripping:** Historical unit prices from the previous order are **NOT blindly copied**. The quotation request is submitted with requested quantities; seller pricing resolution occurs dynamically on the backend.
   - **Traceability Link:** Stores `Notes` or source reference metadata indicating: `"Cloned from Sales Order SO-2026-0042"`.
4. **Draft Editing:** Customer can adjust quantities, add new products, or delete lines prior to final submission.

### 8.3 Path C: Request Based on an Old Quotation `[Agreed Business Rule]`
1. **Business Use-Case:** An earlier quotation expired before corporate purchase approval, or a previously declined quote is reopened for renegotiation.
2. **User Action:** From the **Quotations** register for the active Selling Company, customer clicks **"Re-quote / Clone to New Request"**.
3. **Cloning & Validation Invariants:**
   - **Original Proposal Preserved:** The original quotation remains permanently immutable in its historical terminal status (`Expired`, `DeclinedByCustomer`, or `Superseded`).
   - **Master Data Verification:** Products and UOMs are checked for ongoing validity within the active Selling Company.
   - **Traceability Reference:** Stores `SupersededByQuotationId` or source reference tracking lineage.
   - **Fresh Proposal Lifecycle:** Starts a fresh lifecycle as a new quotation request rather than an in-place revision.

---

## 9. Customer Quotation Review, Acceptance & Atomic Sales Order Conversion

### 9.1 Quotation Review & Information Security `[Agreed Business Rule]`
When the active Selling Company completes internal preparation and managerial approval, the quotation transitions to `SalesQuotationStatus.Sent`.
- **Customer Viewport:** Customer reviews line items, quantities, agreed unit prices, approved discounts, applicable tax amounts, total commitment, and the compulsory **Validity Expiry Date** (`ValidUntilDateUtc`).
- **Information Masking Rule `[Mandatory]`:** Under no circumstances does the customer API return:
  - Internal staff authoring notes (`InternalNotes`).
  - Internal approval audit logs (`ApprovalAuditRecords`).
  - Internal staff reviewer IDs (`ApprovedByUserId`, `RejectedByUserId`).
  - Internal managerial rejection remarks (`RejectionRemarks`).
- **Downloadable Document:** Customer can stream or download the official PDF proposal.

### 9.2 Quotation Acceptance & Atomic Conversion Flow `[Agreed Business Rule]`
When an authorised company contact clicks **"Accept Quotation"**:

```
[Customer Clicks Accept] ──► Validate Authentication & Contact Authority (CanAcceptQuotations)
                                          │
                                          ▼
                             Validate Quotation Status == Sent
                                          │
                                          ▼
                        Validate ValidUntilDateUtc >= UtcNow (Not Expired)
                                          │
                                          ▼
                  Validate Selling Company Context & Customer Membership
                                          │
                                          ▼
                      ┌───────────────────────────────────────────────┐
                      │    BEGIN DATABASE TRANSACTION (EF Core / SQL) │
                      ├───────────────────────────────────────────────┤
                      │ 1. Transition Quotation -> Accepted           │
                      │ 2. Set AcceptedAtUtc & AcceptedByContactId    │
                      │ 3. Create SalesOrder (Status = Draft)         │
                      │    - Bound to active Selling CompanyId        │
                      │    - Copy exact accepted line prices & terms  │
                      │    - Set SourceQuotationId = quotation.Id     │
                      │ 4. Transition Quotation -> Converted          │
                      │ 5. Update Customer PriceCard for Selling Co.  │
                      │    - Update existing PriceCardLines           │
                      │    - Set EffectiveFrom = Today                │
                      │ 6. Record Audit Event in DomainAuditRecords   │
                      ├───────────────────────────────────────────────┤
                      │    COMMIT TRANSACTION (Atomic All-or-Nothing) │
                      └───────────────────────────────────────────────┘
                                          │
                                          ▼
                        Dispatch Order Confirmation Email via Decorator
```

### 9.3 Concurrency & Idempotency Protection `[Proposed Requirement]`
- **Duplicate Click Guard:** Network retries or repeated user clicks on "Accept" must not create multiple Sales Orders.
- **Enforcement:** If a quotation status is already `Accepted` or `Converted`, subsequent acceptance calls return **HTTP 409 Conflict** with the existing `ConvertedSalesOrderId`.
- **Database Concurrency:** RowVersion check on `SalesQuotationItem` and in-memory status checking on `SalesQuotation` ensures isolation during concurrent acceptance calls.

### 9.4 Price Card Synchronization Rules `[Agreed Business Rule]`
1. **Target Price Card:** Resolves to the customer's assigned card for that specific Selling Company (`Customer.DefaultSalesPriceCardId`).
2. **Line Item Updates:** For each accepted product, system locates the matching `PriceCardLine`:
   - If present: Updates `UnitPrice` to accepted unit price, sets `EffectiveFrom = UtcNow.Date`, and maintains `MinimumQuantity`.
   - If absent: Creates a new `PriceCardLine` within the existing card.
3. **Missing Card Operational Handling:** If `Customer.DefaultSalesPriceCardId == null`, quotation acceptance and Sales Order creation **still succeed**, but the system skips the Price Card update and logs an operational audit warning: *"Order SO-XXXX created from Quotation SQ-YYYY, but customer has no assigned Sales Price Card to update."* System **does not invent** a new unassigned Price Card automatically.

---

## 10. Direct Sales Orders from Sales Price Card

### 10.1 B2B Direct Ordering Process `[Agreed Business Rule]`
For established corporate clients with negotiated contract terms, entering quotation bidding for routine replenishments is inefficient. Authorized contacts can order directly:

1. **Contract Price Resolution:** When browsing the active Selling Company's catalog, the portal resolves product prices directly against the customer's active `PriceCardLines` for that company.
2. **Contract Masking Invariant:** Contract pricing is strictly isolated to the authenticated company. Other tenant customers cannot view another company's negotiated prices.
3. **Minimum Order Quantity (MOQ) Enforcement:** If a `PriceCardLine` mandates a minimum order quantity (e.g., $50.00$ cases), the portal client and backend validation reject order lines below this threshold:
   ```csharp
   if (item.Quantity < priceCardLine.MinimumQuantity)
       throw new InvalidOperationException($"Product '{product.Name}' requires a minimum contract quantity of {priceCardLine.MinimumQuantity}.");
   ```
4. **Order Creation:** Creates an official `SalesOrder` (`SalesOrderStatus.Draft`, `SalesOrderType.Direct`) copying contract prices and line references, bound to the active Selling Company.
5. **Separation from Quotations:** Direct Sales Orders bypass quotation workflows entirely.

---

## 11. Sales Order History, Delivery Tracking & Invoices

### 11.1 Sales Order History & Status Lifecycle `[Verified Existing Capability]`
Company customers track their orders scoped to the active Selling Company:

| Backend `SalesOrderStatus` | Customer Portal Milestone | Customer Capability |
|---|---|---|
| `Draft (1)` | **Order Placed / Awaiting Review** | View order summary; cancel if not yet confirmed. |
| `Submitted (2)` | **Under Processing** | View line items and expected fulfillment schedule. |
| `Confirmed (3)` | **Confirmed / In Fulfillment** | Order locked; preparing warehouse staging. |
| `Fulfilled (4)` | **Completed** | Full delivery completed; access archival documents. |
| `Cancelled (5)` | **Cancelled** | Order withdrawn; view cancellation notes. |
| `Rejected (6)` | **Declined** | Review customer-facing rejection remarks. |

### 11.2 Delivery & Shipment Tracking `[Verified Existing Capability]`
Customers track shipments generated from their Sales Orders via `SalesShipment`:

```
   [Warehouse Stages Order] ──► SalesShipment Created (Status = Draft)
                                              │
                                              ▼
   [Carrier Dispatched]     ──► SalesShipment (Status = Dispatched)
                                - Tracking Number: TRK-994821
                                - Carrier / Method: Delivery (1) / CustomerPickup (2)
                                - Portal Status: "Out for Delivery"
                                              │
                                              ▼
   [Delivery Confirmed]     ──► SalesShipment (Status = Delivered)
                                - DeliveredAtUtc Recorded
                                - Portal Status: "Delivered"
```

- **Customer Visibility:** Shipment number, dispatch date, carrier name, tracking reference, line item delivery quantities, and fulfillment method (`Delivery`, `CustomerPickup`, `StoreHandover`).
- **Traceability:** Links directly back to parent `SalesOrderNumber` within the active Selling Company.

### 11.3 Sales Invoice Status & Balance `[Verified Existing Capability]`
Company customers inspect commercial billing associated with their fulfilled transactions:
- **Displayed Attributes:**
  - `InvoiceNumber`, `InvoiceDate`, `DueDate`.
  - `Currency`, `SubTotal`, `TaxAmount`, `TotalAmount`.
  - `AmountPaid`, `OutstandingBalance`.
  - `PaymentTerms` (e.g., "Net 30 Days").
- **Backend Status Mapping `[Verified Existing Capability]`:**
  - `SalesInvoiceStatus.Draft (1)` $\rightarrow$ **Pending Issuance** (Internal only; hidden from customer).
  - `SalesInvoiceStatus.Confirmed (2)` $\rightarrow$ **Awaiting Payment / Open**.
  - `SalesInvoiceStatus.Paid (4)` $\rightarrow$ **Paid in Full**.
  - `SalesInvoiceStatus.Cancelled (3)` $\rightarrow$ **Void / Cancelled**.
- **Scope Restriction:** Credit Management, credit limits, automated payment gateways, and collections dunning are strictly excluded from Phase 1.

---

## 12. Security, Multi-Company Authorization & Data Isolation

### 12.1 Customer Role Templates `[Proposed Requirement]`
To guarantee security isolation from internal staff, dedicated customer role templates are established in `SecurityDbContext`:

```
┌────────────────────────────────────────────────────────────────────────┐
│                        Customer Role Hierarchy                         │
├────────────────────────┬───────────────────────────────────────────────┤
│ Role Name              │ Effective Capabilities                        │
├────────────────────────┼───────────────────────────────────────────────┤
│ CUSTOMER_COMPANY_ADMIN │ Manage company profile, view authorized quotes│
│                        │ Request quotes, accept quotes (if authorized) │
│                        │ View company deliveries, invoices and orders  │
├────────────────────────┼───────────────────────────────────────────────┤
│ CUSTOMER_COMPANY_BUYER │ Explore products, request quotes, accept quotes│
│                        │ Submit direct orders from assigned Price Card │
│                        │ Track assigned deliveries and view invoices   │
├────────────────────────┼───────────────────────────────────────────────┤
│ CUSTOMER_COMPANY_VIEWER│ View company catalog, view submitted orders   │
│                        │ View delivery statuses and invoice balances   │
│                        │ (Cannot accept quotes or submit orders)       │
├────────────────────────┼───────────────────────────────────────────────┤
│ CUSTOMER_INDIVIDUAL    │ Retail shopping, personal order tracking      │
│                        │ (Isolated to single human identity)           │
└────────────────────────┴───────────────────────────────────────────────┘
```

### 12.2 Customer JWT Authentication & Claim Structure `[Agreed Business Rule]`
Customer authentication reuses the established ApexERP RS256 token-generation, signing, and cryptographic key infrastructure (`TokenService`, `RsaPrivateKeyLoader`, issuer `CSharpAuthServer`, audience `ERP`), but generates a dedicated token payload distinguishing Customer identities from internal employees:

| Claim Name | Value / Type | Purpose |
|---|---|---|
| `sub` (`NameIdentifier`) | `Guid` (`UserId`) | Stable human login identity in `SecurityDbContext.Users`. |
| `name` | `string` (`Username`) | Normalized email address of the human user. |
| `email` | `string` (`Email`) | Primary contact email address. |
| `tenant_id` | `Guid` (`TenantId`) | Tenant boundary identifier (compatible with `TenantHostConsistencyMiddleware`). |
| `customer_id` | `Guid` (`CustomerId`) | Customer Master record identifier in `DomainDbContext.Customers`. |
| `contact_id` | `Guid` (`ContactId`) | Contact Master record identifier in `DomainDbContext.Contacts`. |
| `role` | `string` (`Role`) | Customer-specific role (e.g. `CUSTOMER_COMPANY_ADMIN`). |
| `portal_type` | `string` (`"CUSTOMER"`) | Explicit discriminator separating portal customers from internal employees. |
| `permission` | `string[]` | Action-level customer portal permissions (e.g. `PORTAL_SALES.VIEW`, `PORTAL_PROFILE.VIEW`). |

Selling Company context is **not** hardcoded into the initial login JWT. It is resolved dynamically on a per-request basis through the established Selling Company selection mechanism, preventing unverified or tenant-wide company access.

### 12.3 Customer Security Boundary & Privilege Partitioning `[Mandatory]`
Customer authentication is strictly partitioned from internal employee authentication:
1. **Zero Internal Privileges:** A Customer JWT never inherits `Tenant Admin`, `Platform Admin`, or employee operational roles. It contains zero internal employee permissions (`SALES_ORDER.CREATE`, `SALES_QUOTATION.APPROVE`, `INVENTORY.VIEW`, `PURCHASE_ORDER.VIEW`, `ACCOUNTING.VIEW`).
2. **Defense-in-Depth Authorization Guard:** `PermissionAuthorizationHandler` and internal scope validators explicitly reject any token bearing `portal_type = "CUSTOMER"` when attempting to access employee endpoints.
3. **No Automatic Purchasing Authority:** Merely holding the `CUSTOMER_COMPANY_ADMIN` role does not confer automatic purchasing or quotation acceptance authority. If a user registered with deduplication matching an existing company (`IsExistingCompanyMatched = true`, `RequiresAuthorization = true`), the contact is restricted to read-only views until authorized by an internal sales administrator or verified company officer.
4. **No Password Complexity & No Arbitrary Length Barrier:** Phase 2.1 corrects the newly introduced 8-character minimum. Customer registration enforces only non-empty passwords and matching confirmation, reusing existing PBKDF2 hashing via `PasswordService`. No uppercase, lowercase, numeric, symbol, or regex rules are enforced, and email verification remains deferred.

### 12.4 Customer Company-Wide Sales Access & Multi-Selling-Company Scoping `[Agreed Business Rule]`
1. **Customer Company Boundary:** The authenticated customer can access all authorized Sales operations belonging to their own company (`CustomerId`) within the tenant:
   - Product catalogue browsing.
   - Company Sales Quotations and proposal reviews.
   - Sales Quotation acceptance and confirmation (where authorized).
   - Sales Orders, Order Tracking, and Delivery Milestones.
   - Commercial Sales Invoices and outstanding balances.
   - Customer Company Profile and authorized contact list.
2. **Cross-Customer Isolation:** A customer user can **never** access another Customer Company's records. All queries strictly filter by `TenantId == @TenantId AND CustomerId == @CustomerId`.
3. **Multi-Selling-Company Context & Commercial Authorization Boundary:** Where a tenant operates multiple Selling Companies (legal entities or subsidiaries), customer access is restricted to Selling Companies that belong to the same tenant and have an active, verified commercial relationship. Tenant membership and company activity alone do not constitute proof of commercial authorization. If an explicit commercial relationship cannot be established via confirmed historical transactions or contract bindings, operations must **fail closed** rather than granting speculative access.

### 12.5 Server-Side Identity and Authorization Validation Invariants `[Mandatory]`
Every backend portal operation must re-validate the complete identity chain from trusted database state:
- Authenticated `UserId` (from JWT `sub`).
- `TenantId` (from JWT `tenant_id` and domain context).
- `CustomerId` (from JWT `customer_id` validated against active `CustomerContact`).
- `ContactId` (from JWT `contact_id` validated against active `Contact`).
- Active status of both `Customer` and `Contact` records.
- Contact membership in the Customer Company (`CustomerContact.IsActive`).
- Contact authorization status (`RequiresAuthorization == false` for commercial mutations).
- Active Selling Company relationship in the same `TenantId`.

**Live Purchasing-Authority Revalidation Guard:** Sensitive purchasing operations (e.g. quotation acceptance, sales order submission) must NOT rely exclusively on permission claims present in an already-issued JWT. Handlers must execute real-time database validation verifying that the contact remains active (`CustomerContact.IsActive == true`) and that purchasing authority has not been revoked (`RequiresAuthorization == false`). If revoked, the operation is immediately rejected with HTTP 403 Forbidden.

**Client-supplied IDs are never trusted:** No request parameter or header claiming `CustomerId`, `ContactId`, or `TenantId` will be accepted without matching the authenticated JWT claims and active database relationships.

### 12.6 Multi-Company Server-Side Authorization Pipeline `[Mandatory]`
Every request to `/api/portal/*` passes through server-side authorization:

```
[Inbound Request to /api/portal/*]
                 │
                 ▼
┌────────────────────────────────────────────────────────┐
│ 1. Validate JWT Signature, Expiry & Audience (RS256)   │
└────────────────────────┬───────────────────────────────┘
                         │
                         ▼
┌────────────────────────────────────────────────────────┐
│ 2. Verify Portal Type & Tenant Scope:                  │
│    - Confirm portal_type == "CUSTOMER"                 │
│    - Validate tenant_id matches host / context         │
└────────────────────────┬───────────────────────────────┘
                         │
                         ▼
┌────────────────────────────────────────────────────────┐
│ 3. Validate Live Identity & Active Membership:         │
│    - User.IsActive in SecurityDbContext                │
│    - Contact.IsActive && Customer.IsActive in Domain   │
│    - CustomerContact.IsActive link verified           │
└────────────────────────┬───────────────────────────────┘
                         │
                         ▼
┌────────────────────────────────────────────────────────┐
│ 4. Resolve Active Selling Company Context:             │
│    - Verify Selling Company belongs to Tenant          │
│    - Verify active commercial relationship             │
│    - Reject cross-tenant or unauthorized companies     │
└────────────────────────┬───────────────────────────────┘
                         │
                         ▼
┌────────────────────────────────────────────────────────┐
│ 5. Enforce Action-Level Customer Permission:           │
│    - E.g. PORTAL_QUOTATION.ACCEPT requires authority   │
│    - Reject unauthorized contacts (HTTP 403)           │
└────────────────────────┬───────────────────────────────┘
                         │
                         ▼
┌────────────────────────────────────────────────────────┐
│ 6. Execute Scoped Domain Query:                        │
│    WHERE TenantId == @TenantId                         │
│      AND CompanyId == @ActiveSellingCompanyId          │
│      AND CustomerId == @CustomerCompanyId              │
└────────────────────────────────────────────────────────┘
```

---

## 13. Backend Architecture & API Responsibilities

### 13.1 API Endpoint Architecture

```
/api/portal
  ├── /auth
  │     ├── POST /register                    (Unified Registration: Co & Ind)
  │     ├── POST /login                       (Customer JWT Authentication)
  │     └── GET  /selling-companies           (List Customer's Authorized Selling Cos)
  │
  ├── /products
  │     ├── GET  /catalog                     (Explore Products in Active Selling Co)
  │     └── GET  /{id}/pricing                (Resolved Contract vs Retail Pricing)
  │
  ├── /quotations
  │     ├── GET  /                            (List Company Quotes in Active Selling Co)
  │     ├── GET  /{id}                        (Quotation Detail - Masked DTO)
  │     ├── GET  /{id}/pdf                    (Stream Proposal PDF)
  │     ├── POST /request                     (Path A: New Quotation Request)
  │     ├── POST /request-from-order          (Path B: Clone from Sales Order)
  │     ├── POST /request-from-quotation      (Path C: Clone from Old Quotation)
  │     ├── POST /{id}/accept                 (Accept Proposal & Convert to SO)
  │     └── POST /{id}/decline                (Decline Proposal with Remarks)
  │
  ├── /orders
  │     ├── GET  /                            (List Company Orders in Active Selling Co)
  │     ├── GET  /{id}                        (Sales Order Detail)
  │     ├── POST /direct                      (Direct B2B Order from Price Card)
  │     └── POST /{id}/cancel                 (Cancel Eligible Draft Order)
  │
  ├── /deliveries
  │     ├── GET  /                            (List Deliveries in Active Selling Co)
  │     └── GET  /{id}                        (Delivery / Tracking Detail)
  │
  ├── /invoices
  │     ├── GET  /                            (List Invoices in Active Selling Co)
  │     ├── GET  /{id}                        (Invoice Detail & Balances)
  │     └── GET  /{id}/pdf                    (Stream Invoice PDF)
  │
  └── /profile
        ├── GET  /                            (Company / Contact Profile)
        ├── PUT  /                            (Update Contact Info / Addresses)
        └── /contacts                         (Manage Additional Authorised Contacts)
```

### 13.2 Customer DTO Separation `[Mandatory]`
To protect enterprise information, dedicated customer-facing DTOs are established:

```csharp
// Customer-facing Quotation Detail: Stripped of internal data
public sealed record CustomerQuotationDto
{
    public Guid Id { get; init; }
    public Guid SellingCompanyId { get; init; }
    public string SellingCompanyName { get; init; } = string.Empty;
    public string QuotationNumber { get; init; } = string.Empty;
    public int RevisionNumber { get; init; }
    public DateTime QuotationDate { get; init; }
    public DateTime ValidUntilDateUtc { get; init; }
    public string Status { get; init; } = string.Empty;
    public string Currency { get; init; } = string.Empty;
    public decimal SubTotal { get; init; }
    public decimal DiscountAmount { get; init; }
    public decimal TaxAmount { get; init; }
    public decimal TotalAmount { get; init; }
    public string? PaymentTerms { get; init; }
    public string? DeliveryTerms { get; init; }
    public string? CustomerRemarks { get; init; }
    public IReadOnlyCollection<CustomerQuotationItemDto> Items { get; init; } = [];
    
    // EXCLUDED FROM CUSTOMER DTO:
    // - InternalNotes
    // - SubmittedByUserId, ApprovedByUserId, RejectedByUserId
    // - RejectionRemarks (Internal review remarks)
    // - BaseSubTotal, AppliedExchangeRate, ExchangeRateId (Internal accounting records)
}
```

---

## 14. Existing Capabilities to Reuse vs. New Capabilities Required

### 14.1 Existing Capabilities Reused `[Verified Existing Capability]`
1. **Master Data Foundations:**
   - `Customer.cs`, `CustomerContact.cs`, `CustomerAddress.cs`.
   - `Contact.cs` (`Domain/Features/MasterData/Contact`).
   - `Company.cs` (`Domain/Features/MasterData/Company`).
   - `CustomerDeduplicationService.cs` (`Domain/Features/CRM/Deduplication`).
2. **Security Infrastructure:**
   - `User.cs`, `UserRole.cs`, `Role.cs`, `Permission.cs` (`Security/Core/Models`).
   - `PasswordService.cs` (Password hashing in `Security/Services/PasswordService.cs`).
   - `TokenService.cs` (RSA token issuing in `Security/Services/TokenService.cs`).
3. **Commercial & Fulfillment Aggregates:**
   - `SalesQuotation.cs`, `SalesQuotationItem.cs`, `ISalesQuotationRepository.cs`.
   - `SalesOrder.cs`, `SalesOrderItem.cs`, `SalesOrderService.cs`.
   - `SalesShipment.cs`, `SalesShipmentItem.cs` (Delivery tracking).
   - `SalesInvoice.cs`, `SalesInvoiceItem.cs` (Invoice statuses and balances).
   - `PriceCard.cs`, `PriceCardLine.cs` (`Domain/Features/MasterData/PriceCard`).
4. **Cross-Cutting Utilities:**
   - `RetryingEmailServiceDecorator.cs` (Order notifications).
   - `ApprovalWorkflowService.cs` (Managerial approval workflows).

### 14.2 New Capabilities Required `[Proposed Requirement]`
1. **Customer Identity & Multi-Company Context:**
   - `ICustomerContext`: Resolves authenticated customer `CustomerId`, `CustomerContactId`, authorized `SellingCompanyIds`, active `SelectedSellingCompanyId`, and contact purchasing permissions.
2. **Customer Authorization Filter:**
   - `RequireCustomerPermissionAttribute`: Protects customer-facing endpoints without polluting employee permission catalogues.
3. **Customer Portal Controllers (`API/Features/Portal`):**
   - `CustomerAuthController`: Registration (no email verification, simple password hashing), customer login, authorized selling-company enumeration.
   - `CustomerQuotationController`: Three quotation request paths, proposal review, acceptance, decline.
   - `CustomerOrderController`: Direct order creation, order tracking.
   - `CustomerDeliveryController`: Shipment milestone queries.
   - `CustomerInvoiceController`: Commercial invoice and balance queries.
4. **Service Operations:**
   - `CloneQuotationRequestFromSalesOrderAsync` on `ISalesQuotationService`.
   - `CloneQuotationRequestFromHistoricalQuotationAsync` on `ISalesQuotationService`.
   - `AcceptQuotationAndConvertAtomicAsync`: Atomic transaction executing quotation acceptance, Sales Order draft generation, and Price Card line updates.

---

## 15. Implementation Strategy & Incremental Phases (Backend-First)

Development is strictly partitioned into eight modular, testable increments:

```
┌────────────────────────────────────────────────────────────────────────┐
│                      Backend Implementation Phases                     │
├─────────┬──────────────────────────────────────────────────────────────┤
│ Phase 1 │ [COMPLETED] Company Registration & Identity Foundation       │
│         │ • Registration endpoint POST /api/portal/auth/register       │
│         │ • Simple password hashing (PBKDF2, no complexity rules)      │
│         │ • Email verification deferred (immediate active login)       │
│         │ • Deduplication integration & Master Data linking            │
│         │ • Customer & Contact creation with tenant boundary           │
│         │ • Zero employee permissions (CUSTOMER_COMPANY_ADMIN role)   │
├─────────┼──────────────────────────────────────────────────────────────┤
│ Phase 2.1│ [COMPLETED] Customer Authentication Acceptance Corrections       │
│         │ • Removed 8-character minimum from registration pipeline         │
│         │ • Sanitized customer profile: excluded CreditLimit & PaymentTerms│
│         │ • Customer JWT issuing with customer identity claims             │
│         │   (sub, name, email, tenant_id, customer_id, contact_id,     │
│         │    portal_type=CUSTOMER, role, customer permissions)         │
│         │ • POST /api/portal/auth/login issuing Customer JWT           │
│         │ • GET /api/portal/profile returning sanitized profile DTO    │
│         │ • GET /api/portal/selling-companies listing valid entities   │
│         │ • Customer security boundary & employee endpoint protection  │
│         │ • Documented selling company authorization boundary (fail closed)│
│         │ • Documented live purchasing authority revalidation requirement  │
│         │ • Automated test coverage (14/14 tests passing)              │
├─────────┼──────────────────────────────────────────────────────────────┤
│ Phase 3 │ Customer Authorization & Tenant/Company Isolation Tests      │
│         │ • CustomerAuthorizationFilter & permission enforcement       │
│         │ • Automated cross-customer and cross-company isolation tests │
├─────────┼──────────────────────────────────────────────────────────────┤
│ Phase 4 │ Product Exploration & Three Quotation Request Paths          │
│         │ • Product catalog scoped to active Selling Company           │
│         │ • Path A: New quotation request                              │
│         │ • Path B: Clone from Sales Order                             │
│         │ • Path C: Clone from Old Quotation                           │
├─────────┼──────────────────────────────────────────────────────────────┤
│ Phase 5 │ Quotation Review, Acceptance & Atomic Order Conversion       │
│         │ • Customer quotation detail (masked DTO) & PDF streaming     │
│         │ • Atomic Acceptance: Quotation -> SalesOrder (Draft)         │
│         │ • Atomic Price Card Line updater for active Selling Company  │
│         │ • Concurrency & Idempotency protection                       │
├─────────┼──────────────────────────────────────────────────────────────┤
│ Phase 6 │ Direct Contract Orders from Selling Company Price Card       │
│         │ • Direct order endpoint with MOQ enforcement                 │
│         │ • Price Card contract validation for active Selling Company  │
├─────────┼──────────────────────────────────────────────────────────────┤
│ Phase 7 │ Fulfillment Tracking & Invoicing Visibility                  │
│         │ • Customer Sales Order history & tracking                    │
│         │ • Delivery progress and tracking number queries              │
│         │ • Invoice summary & balance tracking                         │
├─────────┼──────────────────────────────────────────────────────────────┤
│ Phase 8 │ Individual Customer Support & Final Regression Verification  │
│         │ • Individual customer registration & retail ordering         │
│         │ • End-to-end regression tests across Sales Order & RFQ       │
└─────────┴──────────────────────────────────────────────────────────────┘
```

---

## 16. Out-of-Scope Functionality (Phase 1 Boundaries)

The following items are explicitly **excluded from Phase 1**:
1. **Online Payment Gateways & Card Capture:** No payment gateways (Stripe, Adyen, PayPal), credit card tokenization, or real-time payment webhooks.
2. **Credit Management & Risk Limits:** No customer credit limit checks, credit insurance, hold-order rules, or collections dunning.
3. **Email Verification:** Registration does not require email verification tokens or emails in Phase 1.
4. **Password Complexity Policies:** No regex strength validation or mandatory special characters in Phase 1.
5. **Internal ERP Workspace Changes:** No modifications to internal employee screens or navigation.
6. **Frontend UI Implementation:** No Angular or Flutter component implementation in this documentation sprint.
7. **Individual Customer Self-Service Storefront:** Retail shopping cart, walk-in consumer checkout, and guest ordering are deferred to Phase 8.

---

## 17. Decision Register & Architectural Alternatives

| Decision ID | Area | Selected Architectural Choice | Alternative Considered | Rationale |
|---|---|---|---|---|
| **DR-01** | **Customer Authentication** | Dedicated Customer JWT claims (`customer_id`, `contact_id`) with customer-specific roles. | Granting internal employee permissions (e.g. `SALES_QUOTATION.CREATE`). | Violates principle of least privilege; employee permissions expose internal operational APIs to customers. |
| **DR-02** | **Email Verification** | Deferred to future enhancement; accounts activated immediately upon registration. | Mandatory email token verification before login. | Reduces registration friction in Phase 1 while maintaining backend authorization boundaries. |
| **DR-03** | **Password Complexity** | Standard minimum length with existing secure hashing (`IPasswordService`). | Strict complexity rules (symbols, mixed case, numbers). | Eliminates unnecessary user drop-off during onboarding; aligns with modern NIST SP 800-63B guidelines. |
| **DR-04** | **Multi-Company Context** | One customer login; on-the-fly switching of active Selling Company within the tenant. | Separate customer accounts for each Selling Company. | Drastically improves user experience for corporate buyers transacting across multiple subsidiaries of a tenant. |
| **DR-05** | **Identity Separation** | Strict separation of Tenant, Selling Company, Customer Company, and Customer Contact. | Conflating Tenant with Selling Company or Customer Company. | Multi-company and multi-tenant architectures require distinct ownership boundaries to prevent data cross-talk. |
| **DR-06** | **Quotation Cloning** | Revalidate all products and strip old historical prices; seller reprices proposal. | Blindly copying old unit prices into the new request. | Outdated or expired prices cannot be committed before current margin/tax evaluation. |
| **DR-07** | **Quotation Acceptance** | Atomic single-transaction execution of Quotation acceptance, Sales Order creation, and Price Card update. | Multi-step eventual consistency with background messaging. | Eliminates partial failure risks where an order is generated but quotation remains pending or price card un-updated. |
| **DR-08** | **Workspace UI Structure** | Reuse exact ERP Tenant Workspace desktop shell, topbar, and SCSS design tokens. | Creating a separate, consumer-style e-commerce layout. | Preserves seamless brand identity, minimizes frontend maintenance, and adheres to Fluent design system standards. |
| **DR-09** | **Customer JWT Boundary** | Reusable RS256 token service with dedicated `portal_type = "CUSTOMER"` claim and `customer_id`/`contact_id` identity claims; internal permission handlers explicitly block customer tokens. | Separate token signing service or shared generic user tokens. | Reuses existing cryptographic infrastructure and RSA key pairs while establishing an unbypassable server-side boundary. |
| **DR-10** | **Selling Company Context Resolution** | Filter selling companies by active tenant membership and historical transaction presence without creating new database tables in Phase 2. | Creating an unapproved `CustomerSellingCompanyRelationship` junction migration. | Complies with database migration restrictions while preserving tenant isolation and preparing for future schema migrations. |
| **DR-11** | **Password Length Policy** | Removal of newly introduced 8-character minimum; enforce only non-empty passwords and matching confirmation with PBKDF2 hashing. | Enforcing an arbitrary 8-character minimum length or complexity regex. | Aligns with existing tenant user authentication infrastructure and prevents onboarding friction. |
| **DR-12** | **Profile Sanitization & Purchasing Revalidation** | Exclude `CreditLimit` and `PaymentTerms` from customer-facing profile DTO; mandate real-time database revalidation of purchasing authority on sensitive mutations. | Trusting JWT permission claims indefinitely or exposing internal credit metadata. | Eliminates exposure of deferred credit management data and neutralizes risks from revoked contacts holding active JWTs. |
| **DR-13** | **E-commerce Publication Decoupling** | Dedicated `EcommerceProduct` entity linking `TenantId + CompanyId + ProductId` with separate `IsPublished` state and `OnlineDescription`. | Adding publication boolean and display properties directly to ERP Product Master (`Product.cs`). | Prevents company cross-talk in multi-company tenants, avoids breaking existing ERP manufacturing/inventory/procurement workflows, and allows multiple companies to independently publish the same product. |
| **DR-14** | **Image Functionality Deferral** | Defer all image columns, tables, uploads, storage, and retrieval to a future phase. | Creating premature image schemas or third-party cloud storage abstractions now. | Keeps the core product publishing model clean, lightweight, and focused on commercial visibility, allowing future media integration with existing ApexERP file infrastructure. |

---

## 18. Verification & Governance Confirmation

- **Backend implementation strictly completed without frontend code.**
- **No database migrations were created or applied without explicit approval.**
- **No Git commits, pushes, merges, branch switches, or resets were executed.**
- **All existing tracked and untracked changes preserved intact.**
- **All 44 test cases passed successfully (including 13 dedicated E-commerce publication test cases).**

---

## 19. E-commerce Product Publishing Feature `[Verified Implementation]`

### 19.1 Purpose and Overview
The E-commerce Product Publishing feature enables each Selling Company within an ApexERP tenant to independently control which existing ERP Product Master records are displayed in its public E-commerce Catalogue.

Key objectives:
1. **Company Autonomy:** Each Selling Company independently chooses which items to expose publicly.
2. **Multi-Company Coexistence:** The same ERP Product Master record can be published by multiple Selling Companies simultaneously with company-specific display orders and online descriptions.
3. **Workflow Isolation:** Publishing or unpublishing an item never mutates, deactivates, or alters the underlying ERP Product Master record, preserving manufacturing, procurement, and inventory integrity.

### 19.2 Decoupled Data Model (`EcommerceProduct`)
The entity is isolated in the domain layer at `Backend/ERP/Domain/Features/Ecommerce/EcommerceProduct.cs` and implements `Auditable`, `ITenantScopedEntity`, and `ICompanyScopedEntity`:

```csharp
public class EcommerceProduct : Auditable, ITenantScopedEntity, ICompanyScopedEntity
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid CompanyId { get; private set; }
    public Guid ProductId { get; private set; }
    public bool IsPublished { get; private set; }
    public string? OnlineDescription { get; private set; }
    public int DisplayOrder { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }

    public Company? Company { get; private set; }
    public Product? Product { get; private set; }
}
```

#### Relational Invariants & EF Core Configuration:
- **Unique Constraint:** `builder.HasIndex(x => new { x.TenantId, x.CompanyId, x.ProductId }).IsUnique();` — Prevents duplicate publication records for the same product in a single Selling Company.
- **Foreign Keys:** References `Company` (`OnDelete(DeleteBehavior.Restrict)`) and `Product` (`OnDelete(DeleteBehavior.Restrict)`).
- **Tenant Scope Consistency:** Validates that both Company and Product belong to the exact same Tenant before persistence.

### 19.3 Business Rules & Publication Lifecycle
1. **Sellable & Active Precondition:** Only products with `IsActive == true` and `IsSellable == true` in the ERP Product Master can be selected for publication. Attempting to publish an inactive or non-sellable item throws `InvalidOperationException`.
2. **Publish / Unpublish Transitions:**
   - `Publish()` sets `IsPublished = true` and updates `UpdatedAtUtc`.
   - `Unpublish()` sets `IsPublished = false` and updates `UpdatedAtUtc`.
3. **Immutability of Product Master:**
   - Unpublishing hides the product from the public catalogue; it **never** deactivates the Product Master.
   - Deleting an `EcommerceProduct` publication record removes only the storefront listing; the ERP Product Master remains completely intact.
4. **Master Data Sourcing:** Product Name, Code, Category, Brand, and UOM are read directly from the referenced ERP Product Master.
5. **Cost & Financial Data Masking:** Public catalogue DTOs (`PublicEcommerceProductDto`) strictly expose retail `SellingPrice` and `Currency`. `StandardCost`, valuation methods, vendor costs, and inventory margins are never queried or mapped to public endpoints.

### 19.4 API Specifications

#### A. Company Employee Management APIs (`/api/ecommerce/products/publications`)
Protected by `[Authorize]` and company scope validation (`_userAccessService.CanAccessCompanyAsync`):

| Method | Endpoint | Permission | Description |
|---|---|---|---|
| `GET` | `/api/ecommerce/products/publications?companyId={id}` | `ECOMMERCE_PRODUCT.VIEW` | Lists all publication records for the authorized Selling Company. |
| `GET` | `/api/ecommerce/products/publications/{id}?companyId={id}` | `ECOMMERCE_PRODUCT.VIEW` | Retrieves detail of a specific publication record. |
| `POST` | `/api/ecommerce/products/publications` | `ECOMMERCE_PRODUCT.CREATE` | Creates a new publication record for an active, sellable product. |
| `PUT` | `/api/ecommerce/products/publications/{id}?companyId={id}` | `ECOMMERCE_PRODUCT.EDIT` | Updates `OnlineDescription` and `DisplayOrder`. |
| `PUT` | `/api/ecommerce/products/publications/{id}/publish?companyId={id}` | `ECOMMERCE_PRODUCT.PUBLISH` | Transitions publication to `IsPublished = true`. |
| `PUT` | `/api/ecommerce/products/publications/{id}/unpublish?companyId={id}` | `ECOMMERCE_PRODUCT.UNPUBLISH` | Transitions publication to `IsPublished = false`. |
| `DELETE` | `/api/ecommerce/products/publications/{id}?companyId={id}` | `ECOMMERCE_PRODUCT.DELETE` | Deletes the publication record without affecting Product Master. |

#### B. Public Storefront Catalogue APIs (`/api/ecommerce/catalogue`)
Protected by `[AllowAnonymous]` with fail-closed public filtering:

| Method | Endpoint | Auth | Description |
|---|---|---|---|
| `GET` | `/api/ecommerce/catalogue/{companyId}/products` | Anonymous | Returns published products (`IsPublished == true && IsActive && IsSellable`) ordered by `DisplayOrder`. Filters: `searchTerm`, `categoryId`, `brandId`. |
| `GET` | `/api/ecommerce/catalogue/{companyId}/products/{productId}` | Anonymous | Returns product detail if published by that company; returns 404 if unpublished or inactive. |

### 19.5 Permissions & Least Privilege Matrix
Six fine-grained permissions added to `PermissionCatalogue.cs` and `EcommerceProductPermissionSeed.cs`:
- `ECOMMERCE_PRODUCT.VIEW` — Granted to `Tenant Admin`, `Tenant Read-only Auditor`, `Master Data Manager`, `Product & Category Officer`.
- `ECOMMERCE_PRODUCT.CREATE` — Granted to `Tenant Admin`, `Master Data Manager`, `Product & Category Officer`.
- `ECOMMERCE_PRODUCT.EDIT` — Granted to `Tenant Admin`, `Master Data Manager`, `Product & Category Officer`.
- `ECOMMERCE_PRODUCT.PUBLISH` — Granted to `Tenant Admin`, `Master Data Manager`, `Product & Category Officer`.
- `ECOMMERCE_PRODUCT.UNPUBLISH` — Granted to `Tenant Admin`, `Master Data Manager`, `Product & Category Officer`.
- `ECOMMERCE_PRODUCT.DELETE` — Granted to `Tenant Admin`, `Master Data Manager`, `Product & Category Officer`.

### 19.6 Explicit Image Functionality Deferral
In accordance with prompt instructions:
- No image columns, image tables, or image upload endpoints were created.
- `EcommerceProduct` is designed with zero image dependencies so future image management can integrate seamlessly with the existing shared media infrastructure.

### 19.7 Database Migration Status `[Verified Applied on Development]`
- **Migration Name:** `AddEcommerceProductCore`
- **Migration ID & File:** `20261120075340_AddEcommerceProductCore.cs` in `Backend/ERP/Domain/Migrations/`
- **Target Development Database:** `(localdb)\MSSQLLocalDB` / `DomainDB` (Confirmed from `Backend/ERP/API/appsettings.json`)
- **Pending Migrations Check:** Prior to applying, inspected `dbo.__EFMigrationsHistory`; confirmed all previous 32 migrations (including `20261120050836_AddSalesQuotationCore`) were already applied. Exactly one pending migration (`AddEcommerceProductCore`) existed.
- **Application Result:** Successfully applied using `dotnet ef database update --configuration TestBuild` without touching production or staging.
- **Schema & Data Verification:**
  - `dbo.__EFMigrationsHistory`: Migration `20261120075340_AddEcommerceProductCore` recorded with EF Core version 8.0.0.
  - Table Created: `dbo.EcommerceProducts` with all 13 columns (`Id`, `TenantId`, `CompanyId`, `ProductId`, `IsPublished`, `OnlineDescription`, `DisplayOrder`, `CreatedAtUtc`, `UpdatedAtUtc`, `CreatedAt`, `CreatedBy`, `ModifiedAt`, `ModifiedBy`).
  - Indexes: `PK_EcommerceProducts`, unique index `IX_EcommerceProducts_TenantId_CompanyId_ProductId`, and catalogue indexes on `(TenantId, CompanyId, DisplayOrder)` and `(TenantId, CompanyId, IsPublished)`.
  - Foreign Keys: `FK_EcommerceProducts_Companies_CompanyId` and `FK_EcommerceProducts_Products_ProductId` with `NO_ACTION` (`Restrict`).
  - Existing Data Integrity: Confirmed existing 86 Products, 18 Companies, 83 Categories, 21 Customers, and 16 SalesOrders remained completely intact.

### 19.8 Verified Test Suite
Thirteen dedicated integration and unit tests implemented in `ERP.SecurityTests.Ecommerce.EcommerceProductPublishingTests`:
1. `Test01_CompanyCanCreatePublication_ForExistingEligibleProduct_Succeeds`
2. `Test02_DuplicatePublication_SameTenantCompanyAndProduct_IsRejected`
3. `Test03_DifferentCompanies_CanPublishSameProduct_Independently`
4. `Test04_InactiveProduct_CannotBePublished_ThrowsInvalidOperationException`
5. `Test05_NonSellableProduct_CannotBePublished_ThrowsInvalidOperationException`
6. `Test06_PublishedProducts_AppearInPublicCatalogue`
7. `Test07_UnpublishedProducts_DoNotAppearPublicly`
8. `Test08_DeletingPublication_DoesNotDeleteOrModifyProductMaster`
9. `Test09_OneCompany_CannotManageAnotherCompanyPublications`
10. `Test10_TenantIsolation_IsEnforced_AcrossCompaniesAndProducts`
11. `Test11_PublicCatalogue_DoesNotExpose_StandardCostOrInternalFinancialData`
12. `Test12_EmployeeController_CompanyScopeValidation_RejectsCrossCompanyAccess`
13. `Test13_EcommerceProduct_DomainEntity_TransitionsCorrectly`

**All 13 E-commerce tests passed with 0 failures.**  
**Combined regression test suite: 44/44 passed.**

---

## 20. E-commerce Shopping Cart & Free Checkout Backend Architecture

### 20.1 Architecture Overview
ApexERP provides a fully persistent, server-side validated Shopping Cart and Free Checkout subsystem that seamlessly extends the existing E-commerce Product Publishing and Public Catalogue functionality.

Key principles enforced by the architecture:
1. **Persistent Cart Storage:** Every cart and cart line is stored in SQL Server via EF Core (`EcommerceCarts` and `EcommerceCartLines`), isolated by `TenantId` and Selling `CompanyId`.
2. **Dual Identity Support:** Supports both unauthenticated/anonymous storefront visitors (via persistent session token in `X-Cart-Session-Id` header) and authenticated B2B customer users (via `ICurrentCustomerContext`).
3. **Session-to-Customer Merging:** When an anonymous visitor logs into the Customer Portal, their active session cart is automatically linked to their authenticated `CustomerId`.
4. **Untrusted Client Pricing:** Cart lines persist strictly requested quantities, product IDs, UOMs, and packaging IDs. Client prices are **never trusted**. All unit prices, discounts, and taxes are evaluated dynamically server-side via `IPricingResolutionService` and `TaxCodeRepository`.
5. **Free Checkout (Zero Payment Overhead):** Free checkout creates an authoritative ERP `SalesOrder` in `Draft` status via `ISalesOrderService`. Zero payment gateway, zero mock payments, and zero dummy transaction records are introduced.
6. **Idempotency & Concurrency Protection:** Every checkout request accepts an `IdempotencyKey`. Repeated or concurrent submissions with the same key return the existing order details without duplicate order creation.

### 20.2 Persistent Shopping Cart Domain Model

#### `EcommerceCart` (`Backend/ERP/Domain/Features/Ecommerce/EcommerceCart.cs`)
- **Key & Scoping:** `Id` (Guid, PK), `TenantId` (Guid, `ITenantScopedEntity`), `CompanyId` (Guid, `ICompanyScopedEntity`).
- **Owner Identity:** `CustomerId` (Guid?, FK to `Customers`), `CustomerContactId` (Guid?), `SessionId` (string?, max 128 chars).
- **Status & Lifecycle:** `Status` (`Active = 1`, `CheckedOut = 2`, `Abandoned = 3`).
- **Checkout & Idempotency:** `SalesOrderId` (Guid?, FK to `SalesOrders`), `IdempotencyKey` (string?, max 128 chars), `CheckoutAttemptedAtUtc` (DateTime?).
- **Audit:** `CreatedAtUtc`, `UpdatedAtUtc`, `Auditable` base fields.
- **Collection:** `Lines` (`IReadOnlyCollection<EcommerceCartLine>`).

#### `EcommerceCartLine` (`Backend/ERP/Domain/Features/Ecommerce/EcommerceCartLine.cs`)
- **Key & Ownership:** `Id` (Guid, PK), `CartId` (Guid, FK to `EcommerceCarts`, cascade delete).
- **Product & Measure:** `ProductId` (Guid, FK to `Products`), `UnitOfMeasureId` (Guid?, FK to `UnitsOfMeasure`), `ProductPackagingId` (Guid?, FK to `ProductPackagings`).
- **Quantity:** `Quantity` (`decimal(18,4)`, strictly > 0).
- **Audit:** `CreatedAtUtc`, `UpdatedAtUtc`.

### 20.3 Server-Side Dynamic Pricing & Tax Evaluation
- Prices are evaluated in real-time on cart retrieval (`GetCartAsync`), add-to-cart (`AddToCartAsync`), and checkout (`CheckoutAsync`).
- For authenticated customers, `PricingResolutionService.ResolveSalesPriceAsync` checks the customer's assigned `DefaultSalesPriceCardId`.
- If no price card exists, pricing falls back to `Product.SellingPrice` with automated UOM unit conversion where applicable.
- For anonymous visitors, prices are evaluated against the default walk-in customer profile (`CUST-WALKIN`) or product master selling price.
- Active sales tax codes are dynamically applied from `DomainDbContext.TaxCodes` where `Applicability == TaxApplicability.Sales`.

### 20.4 Free Checkout Workflow & Sales Order Integration
Checkout coordinates with authoritative domain services:
1. **Cart Validation:** Ensures cart exists, is `Active`, and has non-zero line items.
2. **Catalog Validation:** Re-verifies that every product in the cart is currently active, sellable, and published for the Selling Company (`EcommerceProduct.IsPublished == true`).
3. **Selling Company & Branch Resolution:** Resolves active Selling Company currency and selects the default active Branch (e.g. `MAIN` or `HQ`).
4. **Customer Resolution:**
   - Authenticated portal customer uses verified `CustomerId`.
   - Anonymous visitor resolves tenant walk-in cash customer (`CUST-WALKIN`).
5. **Delivery Address Validation:** Validates normalized `CustomerAddress` or `Address` records belonging to the customer.
6. **Order Number Generation:** Formats distinct, human-readable ERP order number (e.g. `SO-WEB-{yyyyMMdd}-{hash}`).
7. **Sales Order Creation:** Passes `CreateSalesOrderRequest` to `ISalesOrderService.CreateOrderAsync`, generating an ERP `SalesOrder` in `Draft` status.
8. **Cart Finalization:** Transitions cart to `Status = CheckedOut`, sets `SalesOrderId`, and persists `IdempotencyKey`.

### 20.5 API Specifications (`/api/ecommerce/cart`)
Protected by `[AllowAnonymous]` with automatic detection of authenticated customer portal tokens:

| Method | Endpoint | Description |
|---|---|---|
| `GET` | `/api/ecommerce/cart?companyId={id}` | Retrieves active cart with server-calculated prices, line totals, and grand total. |
| `POST` | `/api/ecommerce/cart/items?companyId={id}` | Adds or increments product quantity in cart. Validates publication status. |
| `PUT` | `/api/ecommerce/cart/items/{lineId}?companyId={id}` | Updates line quantity. Quantity `<= 0` removes the item. |
| `DELETE` | `/api/ecommerce/cart/items/{lineId}?companyId={id}` | Removes a specific item from the cart. |
| `DELETE` | `/api/ecommerce/cart?companyId={id}` | Clears all items from the cart. |
| `POST` | `/api/ecommerce/cart/checkout?companyId={id}` | Executes free checkout, creates Draft Sales Order, and checks out cart. |

### 20.6 Database Migration Status `[Verified Applied on Development]`
- **Migration Name:** `AddEcommerceCartAndCheckout`
- **Migration ID & File:** `20261120103052_AddEcommerceCartAndCheckout.cs` in `Backend/ERP/Domain/Migrations/`
- **Database Backup:** `z:\ERP\DatabaseBackups\DomainDB_Pre_EcommerceCart.bak` verified with `RESTORE VERIFYONLY` prior to execution.
- **Target Database:** `(localdb)\MSSQLLocalDB` / `DomainDB`.
- **Tables Created:**
  - `dbo.EcommerceCarts` (16 columns, indexes on `(TenantId, CompanyId, Status)`, `(TenantId, CustomerId, Status)`, `(TenantId, SessionId, Status)`, `(TenantId, CompanyId, IdempotencyKey)`).
  - `dbo.EcommerceCartLines` (8 columns, indexes on `CartId`, `(CartId, ProductId)`, `ProductId`, `ProductPackagingId`, `UnitOfMeasureId`).
- **Referential Integrity:** Cascading delete from Cart to CartLines; restrictive foreign keys to Companies, Customers, Products, UOMs, and ProductPackagings; SetNull on SalesOrderId.

### 20.7 Automated Test Coverage
Twenty-eight automated unit and integration tests passing in `ERP.SecurityTests.Ecommerce`:
- `EcommerceCartServiceTests`: 9 tests covering published item adding, unpublished rejection, non-sellable rejection, quantity updates, zero-quantity removal, line removal, clearing, customer association merging, and dynamic pricing resolution.
- `EcommerceCheckoutTests`: 6 tests covering checkout draft creation, idempotency key deduplication, empty cart rejection, unpublished item detection at checkout, anonymous walk-in customer assignment, and authenticated B2B customer address preservation.
- `EcommerceProductPublishingTests`: 13 existing publication tests all passing.
- **Total Test Suite Result: 28/28 passed with 0 failures.**



---

## 21. Sales Backend Implementation Audit & Status Matrix (November 2026)

In compliance with enterprise backend completion instructions, this section documents the authoritative verification status across all sales workflows.

### 21.1 Capability Classification Matrix

| Workflow Area | Capability / Feature | Classification | Implementation Status & Evidence |
|---|---|---|---|
| **A. Internal Sales Order** | Sales Order Aggregate Root & Domain Logic | `IMPLEMENTED` | `SalesOrder.cs` in `Domain/Features/Sales/SalesOrder` with tax/currency snapshots, item calculation, and status progression. |
| **A. Internal Sales Order** | Sales Order Application Service & Controller | `IMPLEMENTED` | `SalesOrderService.cs` in `ERP.Application/Features/Sales/SalesOrder` and `SalesOrderController.cs` in `API/Features/Domain`. |
| **A. Internal Sales Order** | Automatic Draft Dispatch Note Creation | `IMPLEMENTED` | Verified in `SalesOrderService.ConfirmOrderAsync`: automatically creates Draft `SalesShipment` when physical items exist (`ProductType != ProductType.Service`). Test: `SalesOrder_Confirm_CreatesDraftDispatchNote_ForPhysicalItems` (PASS). |
| **B. Direct B2B Ordering** | Customer Direct B2B Order Application Service | `IMPLEMENTED` | `DirectCustomerOrderService` resolving assigned `DefaultSalesPriceCardId`, validating MOQ, UOM, and delivery address. Test: `DirectB2BOrder_ValidRequest_CreatesDraftSalesOrder_WithContractPricesAndTaxes` (PASS). |
| **B. Direct B2B Ordering** | Customer Direct B2B Order API Controller | `IMPLEMENTED` | `POST /api/portal/customer/orders/direct-b2b` in `DirectCustomerOrderController.cs` protected by customer context and idempotency. |
| **C. Sales Quotation** | Internal Quotation Lifecycle & Approval | `IMPLEMENTED` | `SalesQuotationService.cs` (Draft, Submit, Approve, Reject, Cancel) and 19 unit/integration tests passing. |
| **C. Sales Quotation** | Quotation Cloning from Order & Prior Quotation | `IMPLEMENTED` | `CloneCustomerQuotationRequestFromOrderAsync` and `CloneCustomerQuotationRequestFromQuotationAsync` in `ISalesQuotationService` re-validating catalog items without copying stale prices. |
| **C. Sales Quotation** | Formal Quotation Issuance (`Sent`) | `IMPLEMENTED` | `MarkSent` on `SalesQuotation` and `SendQuotationAsync` in service enabling customer acceptance eligibility. Exposed via `POST api/companies/{c}/branches/{b}/sales-quotations/{id}/send`. |
| **C. Sales Quotation** | Atomic Quotation Acceptance & Conversion | `IMPLEMENTED` | `AcceptQuotationAndConvertAtomicAsync` creating Draft `SalesOrder` and versioning `PriceCardLine` within single database transaction. |
| **C. Sales Quotation** | Customer Quotation Portal Controller & PDF Stream | `IMPLEMENTED` | `CustomerQuotationController` under `/api/portal/customer/quotations` including PDF download endpoint powered by pure BCL ISO 32000-1 generator `SalesQuotationPdfService`. |
| **D. E-commerce** | Product Publishing & Public Catalogue | `IMPLEMENTED` | `EcommerceProductService.cs`, `EcommerceCatalogueController.cs` (13 tests passing). |
| **D. E-commerce** | Persistent Shopping Cart & Free Checkout | `IMPLEMENTED` | `EcommerceCartService.cs`, `EcommerceCartController.cs` (15 tests passing, 28 total). |
| **D. E-commerce** | Individual Customer Registration | `IMPLEMENTED` | `RegisterIndividualCustomerAsync` in `CustomerRegistrationService` creating Individual customer, primary contact, security user, and assigning `CUSTOMER_INDIVIDUAL` standard role template. Exposed via `POST api/portal/auth/register-individual`. |
| **E. Customer Identity** | Dedicated Customer JWT & Context | `IMPLEMENTED` | `TokenService.GenerateCustomerAccessToken`, `CurrentCustomerContext` with `portal_type == "CUSTOMER"`. |
| **E. Customer Identity** | Purchasing Authority Enforcement (`CanApproveQuotes`) | `IMPLEMENTED` | Verified in `AcceptQuotationAndConvertAtomicAsync` and contact membership checks. |
| **F. Order Visibility** | Customer Orders, Tracking, Invoices & Dashboard | `IMPLEMENTED` | `CustomerOrderController` under `api/portal/customer` (`GET orders`, `GET orders/{id}`, `GET orders/{id}/tracking`, `GET invoices`, `GET dashboard`) with internal employee notes scrubbed. Tests: 4 passing in `SalesBackendCompletionTests`. |
| **G. Fulfilment** | Sales Shipment & Inventory Integration | `IMPLEMENTED` | `SalesShipmentService` supports draft creation and dispatch posting with inventory deduction. Automatic draft creation on order confirmation verified in `SalesOrderService.ConfirmOrderAsync`. |
| **H. Security** | Customer Role Permissions & Scope Validation | `IMPLEMENTED` | Customer roles (`CUSTOMER_COMPANY_ADMIN`, `CUSTOMER_INDIVIDUAL`) and portal permissions (`PORTAL_*`) verified in bootstrap catalogues and controller authorization. |
