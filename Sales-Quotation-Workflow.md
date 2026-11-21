# ApexERP — Sales Portal, Customer Workspace & Quotation Architecture Specification

**Document Version:** 1.3.2  
**Status:** Architecture & Requirements Specification — Customer Portal and Notification Architecture Completed, Pending Final Review  
**Date:** 2026-11-20  
**Target Module:** Sales (`ERP.Application.Features.Sales`, `ERP.Domain.Features.Sales`, `Frontend/Web/src/app/features/storefront`, `Frontend/Web/src/app/features/domain/workspace/sales`)  
**Related Documents:**
- [Shared-Email-Retry-Architecture.md](file:///z:/ERP/Shared-Email-Retry-Architecture.md) (v1.2.0)
- [ApexERP-Frontend-UI-Standards.md](file:///z:/ERP/ApexERP-Frontend-UI-Standards.md) (v1.2.0)
- [Root Instructions](file:///z:/ERP/AGENTS.md)
- [Backend Instructions](file:///z:/ERP/Backend/ERP/AGENTS.md)
- [Frontend Instructions](file:///z:/ERP/Frontend/Web/AGENTS.md)

---

## 1. Executive Summary & Strategic Sales Channels

### 1.1 Objective
This document defines the complete functional, technical, and portal architecture for the **ApexERP Sales Portal (Version 1.3.2)**. It establishes:
1. The **three strategic Sales channels**: E-commerce Sales, Direct Company Sales, and Sales Quotations.
2. The **hierarchical portal navigation**: Level 1 Tenant Landing Page $\rightarrow$ Level 2 Shared Explore Products $\rightarrow$ Level 3 Three Sales Channels.
3. The **authenticated Customer Portal Workspace**: A dedicated self-service workspace separate from the internal employee workspace, encompassing Customer Dashboard, Order Tracking, Quotations, Document Downloads, and Notification History.
4. **Independent Public Product Exploration**: Unauthenticated product browsing and staging that operates completely independently of customer registration, login, E-commerce checkout, or quotation modules.
5. The **unified identity and shared Customer Registration and Login experience**: One global email-based human login identity, distinct Individual vs. Company account types, and alignment with the verified Tenant Onboarding design system.
6. **Sales Price Card lifecycle**: Onboarding assignment for companies, retail price fallbacks for individuals, historical price preservation, and **atomic Price Card updates upon Sales Quotation acceptance**.
7. **Mandatory Sales Quotation expiry**: Compulsory `ValidUntilDate` on all formal proposals, dynamic query evaluation, and strict backend enforcement during acceptance.
8. **Customer Order Tracking & Document Access**: Authenticated visibility into Sales Order lifecycle progression, dispatch/delivery details, and document streaming without exposing internal operational records.
9. **Resilient Notification Architecture**: Authoritative business event notifications via shared email retry and portal communication history, ensuring notification failures never roll back committed commercial transactions.
10. Downstream convergence into a **single, unified `SalesOrder` aggregate root**.
11. **Explicit Phase 1 Exclusions**: Credit Management and Payment Processing / Online Gateway Tracking are strictly excluded from Phase 1.

### 1.2 Agreed Three Sales Channels

The sales architecture supports three distinct purchasing journeys that converge into a unified fulfillment model:

```
                               ┌────────────────────────────────────────────────────────┐
                               │                 ApexERP Sales Channels                 │
                               └───────────────────────────┬────────────────────────────┘
                                                           │
         ┌─────────────────────────────────────────────────┼─────────────────────────────────────────────────┐
         ▼                                                 ▼                                                 ▼
┌─────────────────────────────────┐       ┌─────────────────────────────────┐       ┌─────────────────────────────────┐
│       1. E-COMMERCE SALES       │       │     2. DIRECT COMPANY SALES     │       │       3. SALES QUOTATION        │
│    (Retail Shopping Journey)    │       │     (B2B Contract Purchasing)   │       │   (Formal Proposal & Bidding)   │
│         [Priority 2]            │       │          [Priority 3]           │       │    [PRIORITY 1 - IMMEDIATE]    │
├─────────────────────────────────┤       ├─────────────────────────────────┤       ├─────────────────────────────────┤
│ • Retail consumers & visitors   │       │ • Registered Company accounts   │       │ • Prospective & existing B2B    │
│ • Standard retail prices        │       │ • Assigned Sales Price Cards    │       │ • Special/custom commercial reqs│
│ • Interactive Shopping Cart     │       │ • Direct Sales Order Draft      │       │ • Quotation Request -> Proposal │
│ • Checkout creates Order Draft  │       │ • Price Card validation & MOQ   │       │ • Mandatory ValidUntilDate      │
│ • Browsable anonymously         │       │ • Company purchasing authority  │       │ • Conditional internal approval │
│ • Individual customer pricing   │       │ • No manual price overrides     │       │ • Mandatory manual email send   │
│ • Zero online payment gateway   │       │ • Restricted if no card assigned│       │ • Accept -> Order + Card Update │
└────────────────┬────────────────┘       └────────────────┬────────────────┘       └────────────────┬────────────────┘
                 │                                         │                                         │
                 └─────────────────────────────────────────┼─────────────────────────────────────────┘
                                                           │
                                                           ▼ (Unified Order Pipeline)
                                            ┌─────────────────────────────┐
                                            │      Core Sales Order       │
                                            │ (Draft -> Submitted ->      │
                                            │  Confirmed -> Picking ->    │
                                            │  Packing -> Dispatched ->   │
                                            │  Delivered / Fulfilled)     │
                                            └─────────────────────────────┘
```

#### Detailed Channel Definitions:

#### Channel A — E-commerce Sales (Retail Channel)
- **Target Audience:** Retail consumers, general public, and walk-in buyers.
- **Pricing Basis:** Normal retail selling prices resolved from Product Master (`Product.SellingPrice`).
- **User Journey:** Anonymous visitors browse items; select quantities; add products to a browser/session-backed retail **Shopping Cart**; review line items, tax, discounts, and shipping totals; authenticate or checkout as an Individual customer.
- **Order Creation & Payment Boundary:** Checkout creates an official `SalesOrder` in `SalesOrderStatus.Draft` according to the agreed order-processing policy. Online payment capture, card processing, and gateway integrations are strictly excluded from Phase 1.
- **Pricing Rule:** Individual customers always use standard retail pricing.

#### Channel B — Direct Company Sales (B2B Contract Channel)
- **Target Audience:** Established corporate and business clients operating under agreed contract terms.
- **Pricing Basis:** Authorized company **Sales Price Cards** (`customer.DefaultSalesPriceCardId`).
- **User Journey:** Customer authenticates with their verified Company contact credentials; system resolves their assigned Sales Price Card; customer selects items and contract quantities; items stage **directly into a Direct Sales Order Draft** (bypassing the retail shopping cart); the screen displays contract prices, currency, Minimum Order Quantities (MOQ), and pre-negotiated discount tiers.
- **Pricing Invariant:** Customers cannot freely override or manipulate assigned Price Card prices.
- **Missing Card Handling:** If a Company customer has no assigned Sales Price Card, direct contract ordering is restricted with an informative notice: *"Your corporate account does not currently have an assigned contract Sales Price Card. Please explore our catalog or request a formal Sales Quotation."*

#### Channel C — Sales Quotation (Formal Bidding & Proposal Channel — Priority 1)
- **Target Audience:** Prospective corporate buyers, existing clients purchasing unlisted/custom products, or established corporate customers requesting volume discounts different from their standard Price Card.
- **Pricing Basis:** Dynamically prepared formal proposals using `IPricingResolutionService`, customizable line discounts, and conditional approval policies.
- **User Journey:** Customer explores products through the shared Explore Products page; submits a formal Quotation Request; sales team reviews and prepares the commercial proposal; managerial approval triggers only if configured policy thresholds (margins/discounts) are breached; sales staff executes an **explicit manual Send Email action**; customer reviews formal PDF in portal and accepts, declines, or requests revisions.
- **Mandatory Expiry Rule:** Every formal quotation carries a compulsory `ValidUntilDate` visible on the PDF and enforced on the backend.
- **Downstream Outcome:** Customer acceptance **atomically** converts the quotation into a `SalesOrder` (`Draft`) using exact accepted commercial snapshots, and **updates the company's assigned Sales Price Card** where one exists, preserving historical price records.

### 1.3 Unified Downstream Sales Order Architecture
> **Enterprise Invariant:** Under no circumstances will three independent Sales Order systems or database tables be implemented. All three channels converge into the unified backend `SalesOrder` aggregate root in `DomainDbContext`, reusing shared Customer Master, Product Catalogue, Tax Codes, Currency Exchange Rates, Inventory Posting, and Dispatch infrastructure.

### 1.4 Implementation Priority
1. **Phase 1 (Immediate Priority):** Sales Quotation end-to-end workflow, Customer Portal workspace, order tracking, PDF generation, email retry integration, and atomic Price Card updating.
2. **Phase 2 (Subsequent Scope):** E-commerce Sales retail storefront and shopping cart.
3. **Phase 3 (Subsequent Scope):** Direct Company Sales direct-to-order contract portal.

---

## 2. Customer Portal Navigation Hierarchy & Public Product Exploration

The portal architecture follows a strictly partitioned **3-Level Navigation Hierarchy**:

```
===================================================================================
LEVEL 1: TENANT LANDING PAGE
(Tenant public root: e.g. /tenants/{tenant-slug} or tenant custom domain)
- Tenant identity, branding, and enterprise business area directory:
  [ Explore Sales & Products ]    |    [ Supplier Procurement Portal ]
- Strict Rule: Zero Sales Quotation-specific buttons or links on Level 1.
===================================================================================
                                        │
                                        ▼
===================================================================================
LEVEL 2: SHARED EXPLORE PRODUCTS PAGE (/tenants/{tenant-slug}/sales/explore)
- Universal product discovery engine for ALL THREE Sales channels.
- Fully independent of customer registration, login, E-commerce, or quotations.
- Anonymous visitors search/filter published, active, sellable products.
- View descriptions, images, categories, brands, packaging UOMs, and retail prices.
- Channel-Aware Pricing Visibility:
  * Anonymous & Individual: Public Retail Prices (Product.SellingPrice)
  * Authenticated & Authorized Company: Contract Price Card Prices
  * Contract prices strictly hidden from unauthorized visitors/competitors.
- Universal Staging Tray (Temporary Exploration State Only):
  [ Add to Retail Cart ]  |  [ Create B2B Order ]  |  [ Request Formal Quotation ]
===================================================================================
                                        │
                                        ▼
===================================================================================
LEVEL 3: CHANNEL-SPECIFIC EXPERIENCES & AUTHENTICATED CUSTOMER PORTAL
- Channel A: /tenants/{tenant-slug}/sales/cart       -> Retail Cart & Checkout
- Channel B: /tenants/{tenant-slug}/sales/b2b-order  -> Contract Price Card Order
- Channel C: /tenants/{tenant-slug}/sales/quotations -> Formal Quotation Journey
- Customer Workspace: /tenants/{tenant-slug}/portal  -> Authenticated Self-Service Hub
===================================================================================
```

### 2.1 Level 1 — Tenant Landing Page
- Serves as the public digital directory for the tenant organization.
- Provides navigation into the tenant's primary operational portals:
  - **Explore Sales & Products:** Enters the shared product discovery portal.
  - **Supplier Procurement Portal:** Enters the Supplier Portal for RFQ token access and supplier quotation submissions.
- **Architectural Boundary:** The Level 1 page must remain an unbiased business area directory. It does not display quotation-specific forms, proposal request buttons, or retail shopping carts.

### 2.2 Level 2 — Public Product Exploration Independence
> **Core Requirement:** Public product exploration must function completely independently of customer registration, authentication, E-commerce checkout, and Sales Quotation implementation.

1. **Anonymous Exploration:** Visitors can access the public catalogue without logging in.
2. **Published Product Filtering:** Displays products where `Product.IsActive == true` and `Product.IsSellable == true`.
3. **Rich Metadata Discovery:** Visitors can search and filter by Category, Brand, Packaging UOM, and keywords, viewing specifications, images, and descriptions.
4. **Channel-Aware Pricing Security:**
   - Anonymous visitors and Individual customers see standard retail prices (`Product.SellingPrice`).
   - Authenticated and authorized Company contacts see their applicable contract Price Card pricing.
   - Negotiated contract prices must never be leaked or exposed to unauthorized visitors or other customers.
5. **Universal Staging Tray (Exploration State Only):**
   - The staging tray represents temporary client-side exploration state. It is **not** a fourth purchasing channel.
   - The public catalogue does not depend on E-commerce checkout being implemented.
   - Selected items transfer cleanly into the chosen purchasing channel:
     1. Into the **Retail Shopping Cart** for E-commerce checkout.
     2. Into a **Direct Sales Order Draft** for authenticated Company contract ordering.
     3. Into the **Sales Quotation Request** form for formal proposal bidding.

---

## 3. Customer Portal Workspace Architecture

The **Customer Portal Workspace** is a dedicated customer-facing self-service hub (`/tenants/{tenant-slug}/portal`), architecturally distinct from the internal tenant employee workspace.

```
┌────────────────────────────────────────────────────────────────────────────────────────┐
│                      AUTHENTICATED CUSTOMER PORTAL WORKSPACE                           │
│                      (/tenants/{tenant-slug}/portal)                                   │
├────────────────────────────────────────────────────────────────────────────────────────┤
│                                                                                        │
│   ┌────────────────────┐   ┌────────────────────┐   ┌────────────────────┐             │
│   │ Customer Dashboard │   │  Explore Products  │   │  My Shopping Cart  │             │
│   │ (Overview & Alerts)│   │ (Universal Catalog)│   │ (Retail Channel)   │             │
│   └────────────────────┘   └────────────────────┘   └────────────────────┘             │
│                                                                                        │
│   ┌────────────────────┐   ┌────────────────────┐   ┌────────────────────┐             │
│   │  My Sales Orders   │   │   My Quotations    │   │   Order Tracking   │             │
│   │ (History & Details)│   │ (Review & Respond) │   │ (Visual Progression│             │
│   └────────────────────┘   └────────────────────┘   └────────────────────┘             │
│                                                                                        │
│   ┌────────────────────┐   ┌────────────────────┐   ┌────────────────────┐             │
│   │    Documents &     │   │   Notifications &  │   │ Customer Profile & │             │
│   │     Downloads      │   │   Communications   │   │  Company Contacts  │             │
│   │ (PDFs & Invoices)  │   │ (Audit & History)  │   │ (Authority & Scope)│             │
│   └────────────────────┘   └────────────────────┘   └────────────────────┘             │
│                                                                                        │
└────────────────────────────────────────────────────────────────────────────────────────┘
```

### 3.1 Workspace Components & Capabilities
1. **Customer Dashboard:** Executive overview of recent orders, quotations, validity countdowns, alerts, and pending actions.
2. **Explore Products:** Direct access to Level 2 product discovery with pre-applied customer pricing context.
3. **My Shopping Cart:** Dedicated retail staging area for E-commerce items pending checkout.
4. **My Sales Orders:** Filterable history of all commercial orders placed by the customer account.
5. **My Quotations:** Proposal tracking interface where customers inspect formal proposals and execute responses (Accept, Decline, Request Revision).
6. **Order Tracking:** Visual progress tracking indicating real-time status from order creation to delivery.
7. **Documents and Downloads:** Authenticated access to formal quotation PDFs, order acknowledgments, and invoices.
8. **Notifications and Communication History:** Centralized feed of all authoritative business notifications dispatched to the customer.
9. **Customer Profile:** Account management interface for billing/shipping addresses and contact telephone numbers.
10. **Company Profile & Authorized Contacts (Company Accounts Only):** Administrative view for company managers to review corporate details and inspect registered contacts.

### 3.2 Authorization & Scope Enforcement
- **Account Type Differentiation:** Individual customers see personal orders, retail pricing, and personal addresses. Company accounts see corporate orders, Price Card pricing, and company contacts.
- **Tenant Isolation:** Every API endpoint enforces `TenantId` scoping via backend tokens.
- **Customer Ownership Validation:** Customers can access only records matching their authenticated `CustomerId`.
- **Contact Authority Enforcement:** Purchasing actions (e.g. accepting a quotation) strictly require `CustomerContact.CanApproveQuotes == true`. Company contacts without this flag can view documents but cannot bind the company.
- **Phase 1 Payment Exclusion:** Online payment processing, card capture, and payment status tracking **do not appear** as implemented Customer Portal capabilities.

---

## 4. Customer Dashboard

The **Customer Dashboard** provides an authenticated, tenant-scoped operational snapshot retrieved via `GET /api/portal/customer/dashboard`:

```
┌────────────────────────────────────────────────────────────────────────────────────────┐
│ CUSTOMER PORTAL DASHBOARD                                                              │
├────────────────────────────────────────────────────────────────────────────────────────┤
│ [ Quotations Awaiting Action: 2 ]   [ Active Orders: 3 ]   [ Orders Dispatched: 1 ]    │
├────────────────────────────────────────────────────────────────────────────────────────┤
│ RECENT SALES QUOTATIONS                           RECENT SALES ORDERS                  │
│ • SQ-2026-0001 | $14,250 | Valid: 5 days left     • SO-2026-0089 | $12,400 | Dispatched│
│   Action: [ Review Proposal ]                       Status: [ Track Shipment ]         │
│ • SQ-2026-0003 | $6,800  | Valid: 12 days left    • SO-2026-0082 | $4,150  | Confirmed │
│   Action: [ Review Proposal ]                       Status: [ In Preparation ]         │
├────────────────────────────────────────────────────────────────────────────────────────┤
│ RECENT NOTIFICATIONS & ALERTS                                                          │
│ • Today 09:15  | Formal Quotation SQ-2026-0001 issued by ApexERP Sales.                │
│ • Yesterday    | Sales Order SO-2026-0089 dispatched via Express Logistics (Trk #8812).│
└────────────────────────────────────────────────────────────────────────────────────────┘
```

### 4.1 Dashboard Data Points
- **Recent Sales Orders:** Top active orders with human-readable numbers, dates, currency, totals, and current status.
- **Order Progress Summary:** Live counts of orders awaiting confirmation, in preparation/packing, dispatched, or delivered.
- **Quotations Awaiting Action:** Proposals in `Sent` status with validity countdown indicators (`DaysUntilExpiry`).
- **Recent Quotation History:** Issued, revised, accepted, or expired proposals.
- **Authoritative Notifications Feed:** Recent business events requiring customer awareness.
- **Pending Customer Actions:** Prompts for required actions (e.g. review quotation, confirm delivery receipt).

---

## 5. Customer Order Tracking, Details & Document Access

Customers can log in at any time to inspect their latest Sales Order status and tracking progression independently of email transmission.

### 5.1 Customer-Facing Order Progress Mapping
The Customer Portal provides a clean, visual 8-stage progress tracker mapped to verified backend domain entities:

```
[1. Created] ──► [2. Under Review] ──► [3. Confirmed] ──► [4. Picking]
                                                               │
[8. Cancelled] ◄── [7. Delivered] ◄── [6. Dispatched] ◄── [5. Packing]
```

#### Progress Stage to Backend Domain State Mapping:

| Customer-Facing Progress Stage | Authoritative Backend Domain State | Fulfilment State / Source | Customer Visibility & Notes |
|---|---|---|---|
| **1. Order Created** | `SalesOrder.Status == Draft` | None / Pre-review | Order draft established from quotation acceptance or checkout. |
| **2. Under Review** | `SalesOrder.Status == Submitted` | Internal validation | Commercial/operational review in progress. |
| **3. Order Confirmed** | `SalesOrder.Status == Confirmed` | Approved for processing | Order committed; ready for warehouse staging. |
| **4. Picking** | `SalesOrder.Status == Confirmed` | `SalesShipment.Status == Draft` | Items being allocated and picked in warehouse. |
| **5. Packing** | `SalesOrder.Status == Confirmed` | `SalesShipment.Status == Draft` | Items packed and packaged; carrier handover pending. |
| **6. Dispatched** | `SalesShipment.Status == Dispatched` | Handed to carrier | Displays carrier name, dispatch date, and tracking number. |
| **7. Delivered** | `SalesShipment.Status == Delivered` | Delivery confirmed | Order marked `SalesOrderStatus.Fulfilled`. |
| **8. Cancelled** | `SalesOrder.Status == Cancelled` | Order withdrawn | Displays cancellation reason if customer-permitted. |

#### Architectural Mapping Rules:
1. **Order Status vs. Fulfilment Status:** Order status (`Draft`, `Submitted`, `Confirmed`, `Fulfilled`, `Cancelled`) represents commercial commitment. Fulfilment status (`Draft`, `Dispatched`, `Delivered`) represents physical inventory movement. Both are evaluated to present the unified customer progress view.
2. **Draft Immutability:** A `Draft` order is strictly never represented as approved for warehouse fulfillment.
3. **Dispatch & Tracking References:** Carrier name, tracking reference numbers, and dispatch timestamps are displayed as soon as the shipment transitions to `Dispatched`.
4. **Partial Fulfilment:** If multiple shipments are created for a single order, the tracking interface displays shipment-specific line breakdowns.

### 5.2 Order Details Inspection
The order detail screen (`GET /api/portal/customer/orders/{id}`) presents:
- Header: Order number, order date, currency, target delivery date.
- Contractual Terms: Accepted payment terms (e.g. `Net 30 Days`, `Payment in Advance`) and delivery terms.
- Line Items: Product code, product description, quantity, packaging unit of measure, unit price, discount %, tax code, tax rate, and line total.
- Financial Summary: Subtotal, total discount, total tax, and grand total.
- Fulfilment Information: Active shipments, carrier tracking details, and delivery addresses.

### 5.3 Secure Document Downloads
- Customers can download formal commercial documents directly from the portal:
  - **Quotation PDFs:** Issued proposal snapshot (`GET /api/portal/customer/quotations/{id}/pdf`).
  - **Order Confirmations:** Official Sales Order confirmation document.
  - **Sales Invoices:** Formal invoice documents where supported by the sales module (`SalesInvoice`).
- **Security Boundary:** All document streaming endpoints enforce customer ownership and tenant scoping. Internal operational details (cost prices, gross margins, internal picker notes) are strictly scrubbed from customer-facing documents.

---

## 6. Customer Notifications, Communication History & Reliability

### 6.1 Authoritative Business Notification Events
Notifications are generated strictly from authoritative backend domain events, never from arbitrary frontend actions:

#### A. Sales Quotation Lifecycle Events:
1. **Quotation Formally Issued:** Triggered when sales staff executes the manual Send Email action.
2. **Quotation Revised:** Triggered when a new revision (`-R1`) is formally issued, superseding a prior offer.
3. **Quotation Approaching Expiry:** Triggered when a scheduled reminder policy identifies an unaccepted quotation near its `ValidUntilDate`.
4. **Quotation Accepted:** Confirmation notification dispatched upon customer acceptance.
5. **Quotation Declined:** Acknowledgment notification dispatched upon customer decline.
6. **Quotation Expired:** Notification dispatched when validity elapses without customer response.

#### B. Sales Order Fulfilment Events:
1. **Sales Order Created:** Order draft established from accepted quotation or checkout.
2. **Sales Order Confirmed:** Order approved for warehouse allocation.
3. **Order Dispatched:** Shipment handed over to freight carrier; includes tracking number and carrier details.
4. **Order Delivered:** Proof of delivery confirmed.
5. **Order Cancelled / Changed:** Formal notification of commercial cancellation or line alteration.

### 6.2 Shared Email Notification Architecture
- Reuses the existing centralised `IEmailService` and `RetryingEmailServiceDecorator` wrapping `MailKitSmtpEmailService`.
- **Zero Duplicate Engines:** No quotation-specific or order-specific email transport or retry code is created.
- **Configured Retry Policy:** Reuses the verified 6-attempt dispatch policy (1 initial attempt + up to 5 automatic retries for transient network/server faults).
- **Notification Templates:** Uses standardized HTML templates rendered via `IEmailTemplateEngine` with CSS inlining.
- **Deduplication Protection:** Business event handlers implement deduplication checks (e.g. checking whether a dispatch notification has already been recorded for a shipment) to prevent duplicate customer emails during event retries.
- **Traceability:** Every notification logs its dispatch outcome, timestamp, recipient address, and message ID in `DomainAuditRecords`.

### 6.3 Portal and Email Reliability Standards
> **Enterprise Reliability Rule:** The authenticated Customer Portal is the authoritative self-service record of business truth. An email transmission failure must **never** reverse or roll back an already committed business transaction.

```
┌────────────────────────────────────────────────────────────────────────────────────────┐
│ BUSINESS EVENT COMMITTED (e.g. Quotation Accepted / Sales Order Dispatched)             │
│ Database Transaction Commits: SalesOrder, PriceCard, Audit Trail                        │
└───────────────────────────────────────────┬────────────────────────────────────────────┘
                                            │
                                            ▼
┌────────────────────────────────────────────────────────────────────────────────────────┐
│ POST-COMMIT NOTIFICATION PIPELINE                                                      │
│ 1. Appends notification record to Customer Portal Communication History.               │
│ 2. Dispatches transactional email via IEmailService.                                   │
└───────────────────────────────────────────┬────────────────────────────────────────────┘
                                            │
                    ┌───────────────────────┴───────────────────────┐
                    │                                               │
           [SMTP SEND SUCCEEDS]                            [SMTP SEND FAILS]
                    │                                               │
                    ▼                                               ▼
┌───────────────────────────────────────┐       ┌───────────────────────────────────────┐
│ • Email delivered to relay (250 OK)   │       │ • Error logged in audit trail         │
│ • Notification marked "Sent"          │       │ • Notification marked "DeliveryFailed"│
│ • Customer Portal shows latest status │       │ • Customer Portal STILL shows latest  │
│                                       │       │   authoritative business status!      │
│                                       │       │ • ZERO rollback of Sales Order or     │
│                                       │       │   Price Card updates!                 │
└───────────────────────────────────────┘       └───────────────────────────────────────┘
```

1. **Independent Self-Service Truth:** If a customer does not receive an email due to a mail server outage, spam filter, or network drop, they can log in to the Customer Portal at any time to view their active proposal, track their shipment, or inspect their invoice.
2. **Non-Blocking Post-Commit Execution:** Email notifications execute **after** the underlying business transaction has committed. If SMTP transport fails after all 5 retries:
   - The failure is logged in the communication audit log.
   - The Sales Order, Price Card update, and quotation conversion **remain completely committed**.
3. **Transport vs. Inbox Reality:** SMTP acceptance (`250 OK`) is recorded as `SmtpAccepted`, reflecting transport relay acceptance, not guaranteed inbox delivery or reading.
4. **Resilient Notification Publication:** Outbox or background worker patterns should not be prescribed automatically without verifying existing infrastructure. In Phase 1, post-commit handlers provide reliable dispatch logging.

---

## 7. Unified Identity, Authentication & Tenant Relationships

ApexERP enforces a clean architectural separation between **Global Identity**, **Business Relationships**, and **Tenant-Scoped Authorization**.

```
┌────────────────────────────────────────────────────────────────────────────────────────┐
│ GLOBAL USER IDENTITY (SecurityDbContext.User)                                          │
│ - Unique Id (Guid)                                                                     │
│ - Verified Login Email (Unique globally: user@domain.com)                             │
│ - Password Hash & Security Salt                                                        │
│ - Account Status (Active, Suspended)                                                   │
└───────────────────────────────────────────┬────────────────────────────────────────────┘
                                            │
                    One Person May Possess Multiple Relationships:
                                            │
         ┌──────────────────────────────────┼──────────────────────────────────┐
         ▼                                  ▼                                  ▼
┌──────────────────┐               ┌──────────────────┐               ┌──────────────────┐
│ Tenant Employee  │               │ Customer Contact │               │ Supplier Contact │
│ Relationship     │               │ Relationship     │               │ Relationship     │
├──────────────────┤               ├──────────────────┤               ├──────────────────┤
│ TenantId: TenantA│               │ TenantId: TenantA│               │ TenantId: TenantB│
│ Roles: Sales Rep │               │ CustomerId: Cust1│               │ SupplierId: Sup1 │
│ Scope: Branch01  │               │ Contact: Purchasing              │ Scope: Portal RFQ│
└──────────────────┘               └──────────────────┘               └──────────────────┘
```

### 7.1 Global Identity vs. Tenant Relationships
1. **One Login Identity per Person:** Each human user possesses exactly one global `User` record identified by their verified email address (`User.Email`).
2. **Multiple Contextual Relationships:** A single human user may interact with ApexERP in multiple distinct capacities (e.g. an authorized buyer for Company A, a supplier contact for Vendor B, and an internal employee in Tenant C).
3. **Contextual Scope Selection:** When logging in, the selected `TenantId` represents the user's active tenant or workspace context. It does **not** signify that the global identity is physically cloned or owned exclusively by that single tenant.
4. **Boundary Isolation:** Customer or supplier identities are **never** granted internal tenant employee permissions automatically. One authentication foundation does not mean one permission set or one unified workspace.

---

## 8. Shared Customer Registration and Login Architecture

To prevent data fragmentation and user friction, ApexERP establishes **one single shared Customer Registration and Login experience** across all three channels.

### 8.1 Login Identity Rule: Human Contact Email as Username
In strict alignment with the existing ApexERP Tenant Onboarding pattern and `Security.Application.DTOs.LoginRequest`:
- **The human user's verified personal email address is the login username.**
- Customers are **never** prompted to create an artificial username string.
- The login form requires `Email` and `Password`.
- Reuses existing `SecurityDbContext`, `UserService`, `PasswordService`, `TokenService` (JWT), and `SessionService`.

### 8.2 Important Company Login Correction & Conceptual Mapping
A company is a legal entity, not a human user. **A company does not log in; an authorized human contact logs in on behalf of the company.**
- The corporate email address (`Company Email`) represents general business correspondence (e.g. `billing@acme.com` or `info@acme.com`).
- The authorized representative's personal business email (`Authorised Contact Email`, e.g. `jane.doe@acme.com`) represents the **human login identity**.

#### Conceptual Identity Mapping:

| Data Element | Business Meaning & Ownership | Domain / Security Mapping |
|---|---|---|
| **Company Email** | General corporate correspondence address | `DomainDbContext.Customer.Email` |
| **Authorised Contact Email** | Specific human representative's verified email | `DomainDbContext.CustomerContact.Email` |
| **Login Username** | Verified human login identity | `SecurityDbContext.User.Email` |
| **Corporate Password** | **Prohibited.** Companies do not share credentials | Human contact has personal password in `User` |

### 8.3 Registration Screen Fields (Dynamic Account Type Toggle)
The customer registration screen adopts the layout, visual hierarchy, step-indicators, and validation styling established by the Tenant Onboarding screen (`app-register`):
- A prominent toggle allows the user to switch between **Individual** and **Company** accounts.
- Switching dynamically renders the corresponding fields in-place without page reloads.

#### A. Individual Customer Registration Fields
- **Account Type:** Individual (`CustomerType.Individual`).
- **First Name & Last Name:** Personal identity $\rightarrow$ mapped to `CustomerContact`.
- **Email Address:** **Login Username** $\rightarrow$ mapped to `User.Email` and `Customer.Email`.
- **Mobile Number:** Telephone contact $\rightarrow$ mapped to `Customer.Phone`.
- **National ID / Passport:** Optional personal identification document.
- **Residential Address, City, Postal Code, Country:** Billing/delivery address $\rightarrow$ `CustomerAddress`.
- **Password & Confirm Password:** Master login credentials.
- *Pricing Rule:* Individual customers use retail pricing; no Price Card is required or displayed.

#### B. Company Customer Registration Fields
- **Account Type:** Company (`CustomerType.Company`).
- **Registered Company Name:** Legal corporate name $\rightarrow$ mapped to `Customer.CustomerName`.
- **Trading Name:** Optional operating name $\rightarrow$ mapped to `Customer.TradingName`.
- **Company Registration Number:** Official registry identifier (e.g. SEC, Companies House).
- **Tax Identification Number:** Tax/VAT/TIN identifier (distinguished from Company Registration #).
- **Company Email & Telephone:** General corporate contact details $\rightarrow$ `Customer.Email`, `Customer.Phone`.
- **Registered Business Address, City, Country:** Corporate domicile $\rightarrow$ `CustomerAddress`.
- **Authorised Contact Name:** Primary representative $\rightarrow$ `CustomerContact.ContactPerson`.
- **Authorised Contact Email:** **Human Login Identity** $\rightarrow$ `User.Email`, `CustomerContact.Email`.
- **Contact Mobile Number & Designation:** Direct representative contact details.
- **Password & Confirm Password:** Personal login credentials for the contact person.

### 8.4 Purchasing Authority Separation
> **Security Rule:** Completing public company registration does **not** automatically grant full purchasing authority (`CanApproveQuotes`).

The system explicitly separates:
1. `CanSubmitQuoteRequests`: May explore products, stage items, and submit RFQs/proposals on behalf of the company. The initial registered contact is granted this permission by default.
2. `CanApproveQuotes`: May legally accept commercial quotations, binding the company to financial commitments and Sales Order conversion.
- `CanApproveQuotes` requires explicit verification through an internal sales onboarding check or formal corporate authorization delegation. Email verification alone proves that the user owns the email inbox, not that they possess corporate spending authority.

### 8.5 Registration Consistency & Transaction Boundaries
- `SecurityDbContext` (identity) and `DomainDbContext` (operational customer master) reside in separate database contexts and execute across separate database transactions.
- **Reliable Registration Strategy:** The registration workflow coordinates across contexts via a verified completion pipeline:
  - Account state defaults to `PendingVerification`.
  - Identity creation and master data provisioning follow a coordinated completion pipeline to handle partial failures cleanly without leaving orphaned records.
  *(Requires source-code verification of current registration pipeline).*

### 8.6 Verified Duplicate Account Linking
- Matching an email, Company Registration Number, or Tax Identification Number against existing records indicates a **potential duplicate**.
- The system strictly does **not** merge or overwrite existing customer records silently.
- An account-linking workflow requires explicit out-of-band verification (e.g. security challenge sent to the existing verified company administrator) before linking a new human contact identity to an existing customer record.

---

## 9. Sales Price Card Assignment Architecture

Sales Price Cards represent structured contract price schedules (`Domain.Features.MasterData.PriceCard.PriceCard`). Price Card assignment is strictly an **internal commercial onboarding workflow**, not a freely selectable option on the public registration screen.

### 9.1 Individual Customers (Zero Price Card Overhead)
- Individual consumers are bound exclusively to standard retail pricing (`Product.SellingPrice`).
- `Customer.DefaultSalesPriceCardId` remains `null`.
- Public registration never presents Price Card selections to retail users.

### 9.2 Company Customers (Internal Assignment Workflow)
1. **Registration Initial State:** When a Company customer registers, their account is established with `Customer.DefaultSalesPriceCardId = null`. Registration is never blocked by the absence of a Price Card.
2. **Authorized Assignment:** An authorized sales representative or manager inspects the company profile in the Master Data workspace:
   - Searches active Price Cards of type `PriceCardType.Sales`.
   - Validates that the card currency matches the customer's transaction currency (e.g. `USD`).
   - Validates that the card is within its effective date range (`EffectiveFrom` $\le$ Today $\le$ `EffectiveTo`).
   - Assigns the card to the customer: `customer.SetDefaultSalesPriceCard(priceCard.PriceCardId)`.
3. **Auditability & Traceability:** The assignment is permanently logged in `DomainAuditRecords` capturing the acting user, previous price card, new price card, and effective timestamp.
4. **Direct Company Sales Handling Without Card:** If a Company customer navigates to Direct Company Sales before a Price Card has been assigned:
   - The portal presents an informative notice: *"Your corporate account is currently awaiting contract Price Card assignment. Please explore our catalog or submit a Sales Quotation Request to receive tailored commercial pricing."*
   - Direct-order checkout is gracefully restricted until a card is assigned or a quotation is accepted.
5. **No Silent Auto-Creation:** Sales staff must explicitly select and assign a verified Price Card. The system must not silently invent unmanaged cards.

---

## 10. Mandatory Sales Quotation Expiry Requirement

> **Mandatory Business Rule:** Every formal Sales Quotation MUST have a mandatory `ValidUntilDate`. A quotation without a validity date is invalid and cannot be issued.

### 10.1 Creation and Issuance Requirements
- A compulsory `ValidUntilDate` must be assigned before a quotation can be internally confirmed or formally issued.
- **Timezone & Expiry Boundary Standardization:**
  - `ValidUntilDate` is captured as an unambiguous calendar date stored in UTC (`ValidUntilDateUtc`).
  - The precise expiration instant is defined as **23:59:59.999 UTC on the specified date**.
  - The PDF, customer portal display, and backend validation strictly follow this identical rule.
- The expiration date must be prominently displayed on the formal quotation PDF.
- The expiration date must be visible in the customer portal proposal review screen with active countdown indicators.
- The validity date is frozen as part of the immutable issued quotation version snapshot.

### 10.2 Acceptance Eligibility Rules
A customer may accept a quotation **only** when all of the following conditions are simultaneously satisfied:
1. **Formal Issuance:** The quotation must be in `SalesQuotationStatus.Sent` status. An internally `Confirmed` quotation that has not been formally issued to the customer **cannot** be accepted.
2. **Temporal Validity:** The quotation must remain unexpired (`DateTime.UtcNow <= ValidUntilDateUtc`).
3. **Purchasing Authority:** The acting customer contact must possess verified purchasing authority (`CustomerContact.CanApproveQuotes == true`).
4. **Exact Version:** The customer must be accepting the latest active revision. Superseded revisions cannot be accepted.
5. **Unconverted:** The quotation must not have been previously converted (`ConvertedSalesOrderId == null`).

### 10.3 Expiry Behaviour & Enforcement
When `ValidUntilDate` has elapsed:
- The quotation is expired.
- Customer acceptance is strictly blocked.
- Sales Order conversion is strictly blocked.
- The quotation remains accessible in the customer portal as an immutable historical record.
- **No Silent Extensions:** The system strictly must **not** silently extend the validity of an issued quotation. If commercial terms are to continue after expiration, sales staff must issue a new formal revision (`-R1`) with a freshly evaluated `ValidUntilDate`.

### 10.4 Expiry Architecture: Dynamic Query Evaluation vs. Worker Mutation
- **Dynamic Server-Side Validation:** The acceptance endpoint (`POST .../accept`) executes an authoritative server-side check against `DateTime.UtcNow`. Frontend-only expiry checks are strictly prohibited.
- **Side-Effect-Free Reads:** Proposing to mutate database records during `GET` queries violates Clean Architecture. Persisted database status transitions to `Expired` may optionally be executed by an independent background worker, but the write-time API enforcement is authoritative on its own.

### 10.5 Independent Price Card Effective Dates
Quotation validity is independent of Price Card effective dates:
- `SalesQuotation.ValidUntilDate` governs the commercial offer window for a specific proposal.
- `PriceCard.EffectiveFrom` and `PriceCard.EffectiveTo` govern catalog contract pricing.
- The expiration of a quotation does not alter, truncate, or extend an existing Price Card.

---

## 11. Formal Quotation Lifecycle & Revisions

```
┌────────────────────────────────────────────────────────────────────────────────────────┐
│   Authoring & Internal Review State Machine (DomainDbContext.SalesQuotation)           │
├────────────────────────────────────────────────────────────────────────────────────────┤
│                                                                                        │
│   [Draft] ──(Submit Review)──► [Submitted] ──(Approve)──► [Confirmed]                  │
│      │                             │                         │                         │
│      │ (Cancel)                    │ (Reject Internally)     │ (Explicit Send Email)   │
│      ▼                             ▼                         ▼                         │
│   [Cancelled]               [RejectedInternal]            [Sent] ◄── Customer Eligible │
│                                                              │                         │
│                                ┌─────────────────────────────┴──────────┐              │
│                                │ (Customer Decision / Temporal Event)   │              │
│                                ▼                                        ▼              │
│                           [Accepted]                          [DeclinedByCustomer]     │
│                                │                                        │              │
│                                │ (Atomic Conversion)                    ▼              │
│                                ▼                                    [Expired]          │
│                          [Converted]                                                   │
│                                                                                        │
│   *Any material modification to an issued quote clones into [Draft] (Revision -R1)     │
│    and transitions previous issued record to [Superseded].                             │
└────────────────────────────────────────────────────────────────────────────────────────┘
```

### 11.1 Authoritative Status Definitions (`SalesQuotationStatus`)
- `Draft`: Quotation under active authoring; lines, quantities, and prices editable.
- `Submitted`: Placed in conditional internal review; author edits locked.
- `Confirmed`: Internally approved by sales management; ready for customer issuance. **Not eligible for customer acceptance until formally sent.**
- `RejectedInternal`: Rejected during internal review with mandatory remarks.
- `Sent`: Formally issued to the customer via email or portal; proposal active. **Customer acceptance eligible.**
- `Accepted`: Customer accepted the valid issued proposal; eligible for Sales Order conversion.
- `DeclinedByCustomer`: Customer formally declined with mandatory reason.
- `Converted`: Atomically converted into official `SalesOrder` (`Draft`). Immutable terminal state.
- `Superseded`: Newer revision issued (e.g. `SQ-2026-0001-R1`). Immutable historical record.
- `Expired`: Validity elapsed (`DateTime.UtcNow > ValidUntilDateUtc`).
- `Cancelled`: Withdrawn by operating company prior to customer acceptance.

### 11.2 Formal Revision Rules
1. **Draft Modifications:** Unissued `Draft` quotations can be modified, recalculated, and saved in-place without generating revision history.
2. **Post-Issuance Immutability:** Once a quotation has been issued (`Sent`), its commercial terms are frozen and immutable.
3. **Formal Revision Generation:** Any material adjustment after issuance requires a formal revision:
   - System clones the quotation into a new record with incremented revision number (e.g. `SQ-2026-0001-R1`).
   - The new revision starts in `Draft` (or `Submitted` if approval limits are exceeded).
   - The prior issued record transitions to `SalesQuotationStatus.Superseded` and records `SupersededByQuotationId`.
4. **Acceptance Restriction:** Customer acceptance can strictly be recorded against the **latest active revision** only.
5. **No Informal Bargaining:** Sales Quotation is a structured commercial inquiry, review, approval, and proposal issuance workflow—**not an informal chat bargaining or price haggling system**.

---

## 12. Atomic Quotation Acceptance & Price Card Update

When an authorized Company customer accepts a valid issued Sales Quotation, the system executes an **atomic multi-aggregate conversion**:

```
┌────────────────────────────────────────────────────────────────────────────────────────┐
│ Customer Clicks "Accept Proposal" on Issued Sales Quotation (SQ-2026-0001)             │
└───────────────────────────────────────────┬────────────────────────────────────────────┘
                                            │
                                            ▼
┌────────────────────────────────────────────────────────────────────────────────────────┐
│ BACKEND PRE-CONVERSION VALIDATION                                                      │
│ 1. Validate customer identity & contact purchasing authority (CanApproveQuotes == true)│
│ 2. Validate quotation status == Sent (Confirmed unissued quotes cannot be accepted)    │
│ 3. Validate temporal validity (DateTime.UtcNow <= ValidUntilDateUtc)                   │
│ 4. Validate unconverted state (ConvertedSalesOrderId == null)                          │
│ 5. Validate exact accepted version matches latest active revision                      │
└───────────────────────────────────────────┬────────────────────────────────────────────┘
                                            │
                                            ▼
┌────────────────────────────────────────────────────────────────────────────────────────┐
│ ATOMIC DATABASE TRANSACTION (using var tx = await _dbContext.Database.BeginTransaction)│
│                                                                                        │
│   STEP 1: Instantiate Official Sales Order Draft                                       │
│   - Creates SalesOrder entity in Draft status.                                         │
│   - Copies exact accepted commercial snapshot: Product, Quantity, UOM, Unit Price,     │
│     Discount %, Tax Code/Rate, Payment Terms, Delivery Address, and Currency Snapshot. │
│   - ZERO PRICE RE-RESOLUTION. Does not overwrite accepted terms with catalog prices.   │
│   - Sets quotation ConvertedSalesOrderId and ConvertedAtUtc; status -> Converted.      │
│                                                                                        │
│   STEP 2: Update Assigned Sales Price Card (Where Assigned)                            │
│   - If Customer.DefaultSalesPriceCardId is assigned:                                   │
│       * Load assigned PriceCard aggregate.                                             │
│       * For each accepted line: Close previous line effective period and insert new     │
│         effective-dated line with accepted UnitPrice & MinimumQuantity (MOQ).          │
│       * PRESERVE HISTORICAL PRICE CARD VALUES. Zero destructive in-place overwrites.    │
│       * Price Card effective dates defined independently of quotation ValidUntilDate.  │
│   - If Customer has NO assigned Price Card:                                            │
│       * Complete Sales Order creation successfully.                                    │
│       * DO NOT silently invent or auto-generate a Price Card.                          │
│       * Log a reliable internal operational task/alert in the audit queue for sales.   │
│       * Log in audit trail that no Price Card was updated.                             │
│                                                                                        │
│   STEP 3: Record Audit & Traceability References                                       │
│   - Append DomainAuditRecords linking QuotationId, SalesOrderId, and PriceCardId.      │
│                                                                                        │
│   tx.CommitAsync()                                                                     │
└───────────────────────────────────────────┬────────────────────────────────────────────┘
                                            │
                    ┌───────────────────────┴───────────────────────┐
                    │                                               │
           [SUCCESSFUL COMMIT]                              [EXCEPTION OCCURS]
                    │                                               │
                    ▼                                               ▼
┌───────────────────────────────────────┐       ┌───────────────────────────────────────┐
│ • Sales Order Draft Created           │       │ • Transaction rolls back completely   │
│ • Price Card Updated with New Rates   │       │ • Quotation remains in Sent status    │
│ • Quotation Status = Converted        │       │ • Zero orphan Sales Orders created    │
│ • Post-Commit Notifications Dispatched│       │ • Zero partial Price Card updates     │
│   (Notification failure never rolls   │       │ • Customer safe to retry cleanly      │
│    back committed transaction!)       │       │                                       │
└───────────────────────────────────────┘       └───────────────────────────────────────┘
```

### 12.1 Commercial Snapshot Integrity
The conversion engine freezes the complete agreed commercial terms into the new `SalesOrder`:
- Product ID, Unit of Measure, Accepted Quantity.
- Agreed Unit Price and Discount Rates.
- Applied Tax Code, Tax Rate, and Tax Inclusive/Exclusive calculation snapshot.
- Transaction Currency, Base Currency, and Applied Exchange Rate snapshot.
- Payment Terms, Delivery Terms, Delivery Address snapshot, and special commercial notes.
- **Zero Re-Resolution Invariant:** The system strictly does **not** re-invoke `IPricingResolutionService` or overwrite accepted quotation terms with current catalog or Price Card values.

### 12.2 Historical Price Card Preservation
> **Integrity Rule:** Updating a Sales Price Card must **never destroy historical price records** through in-place overwrites.

When accepted quotation rates are applied to the customer's assigned Price Card:
1. **Effective-Dated Line Versioning:** The existing active line's `EffectiveTo` date is closed (e.g. set to `DateTime.UtcNow`), and a new `PriceCardLine` is appended with `EffectiveFrom = DateTime.UtcNow`, recording the newly agreed `UnitPrice` and `MinimumQuantity` (MOQ).
2. **Independence of Dates:** The new Price Card line's effective period is defined according to corporate contract policy, independent of the quotation's `ValidUntilDate`.
3. **Auditability:** Modifications record the originating quotation reference (`"Updated via Acceptance of SQ-2026-0001"`) in `DomainAuditRecords`.

### 12.3 Missing Assigned Price Card Handling (Closed Phase 1 Decision)
If an authorized Company customer accepts a quotation but has no assigned Price Card (`DefaultSalesPriceCardId == null`):
- The `SalesOrder` draft is created successfully.
- The system **must not silently auto-generate an unmanaged Price Card**.
- A reliable internal operational task is logged in the operational audit queue for the sales team to review and assign a managed Price Card.
- An audit entry records that quotation conversion completed without Price Card mutation.

### 12.4 Idempotency & Concurrency Safety
- Conversion operations enforce database-level unique constraints on `ConvertedSalesOrderId`.
- Double-clicking or concurrent conversion requests are blocked by concurrency tokens.
- If conversion encounters a technical failure, the entire transaction rolls back; the quotation remains in `Sent` status, allowing the customer to safely retry without generating duplicate orders.

---

## 13. Explicit Phase 1 Scope Exclusions

### 13.1 Explicit Exclusion of Payment Processing & Online Tracking
> **Explicit Scope Boundary:** Payment processing, payment gateway integration, and online payment tracking are **strictly excluded** from Phase 1. ApexERP currently does not support an active payment gateway integration.

The following capabilities must **not** be introduced or assumed:
- Online card payments, credit/debit card processing, or digital wallet checkouts.
- Payment gateway integrations (Stripe, PayPal, Adyen, Authorize.Net, etc.).
- Payment capture, payment authorization holds, or payment webhooks.
- Payment transaction aggregates, tables, or database entities.
- Automated payment confirmation or reconciliation workflows.
- Refund processing or chargeback management.
- Customer payment history retrieved from an external gateway.
- Payment status tracking through external payment providers.

#### Permitted Payment Scope:
- The Customer Portal and Sales Order screens may display existing contractual payment terms (e.g. `Payment in Advance`, `Net 30 Days`, `Cash on Delivery`, `50% Deposit`) as descriptive text/snapshots supported by the Customer Master and Sales Order models.
- In E-commerce Sales, checkout creates an official `SalesOrder` in `Draft` status according to the agreed order-processing policy. Payment capture is not part of checkout.

### 13.2 Explicit Exclusion of Credit Management
> **Explicit Scope Boundary:** Credit Management is **strictly excluded** from Phase 1.

The following capabilities must not be implemented:
- Credit limit checks, credit scoring algorithms, or credit bureau integrations.
- Dynamic credit exposure calculations (open balances + unbilled dispatches + pending orders).
- Automated credit holds, credit stop flags, or credit-blocked order routing.
- Credit collections, aging dunning ladders, or credit control operational queues.

---

## 14. Company Verification vs. Purchasing Authority vs. Credit Boundary

To avoid architectural confusion, ApexERP establishes strict boundaries between related corporate identity and authorization concepts:

```
┌────────────────────────────────────────────────────────────────────────────────────────┐
│ 1. COMPANY IDENTITY VERIFICATION                                                       │
│ Confirms company legal existence via registry/TIN. Grants basic portal access.         │
│ DOES NOT grant purchasing authority or credit terms!                                   │
└───────────────────────────────────────────┬────────────────────────────────────────────┘
                                            │
                                            ▼
┌────────────────────────────────────────────────────────────────────────────────────────┐
│ 2. AUTHORISED CONTACT VERIFICATION                                                     │
│ Confirms individual human email and identity. Grants CanSubmitQuoteRequests.           │
│ DOES NOT grant contract binding authority!                                             │
└───────────────────────────────────────────┬────────────────────────────────────────────┘
                                            │
                                            ▼
┌────────────────────────────────────────────────────────────────────────────────────────┐
│ 3. PURCHASING AUTHORITY (CanApproveQuotes)                                             │
│ Explicit operational flag assigned to designated officers. Permits formal acceptance.  │
│ DOES NOT represent credit approval!                                                   │
└───────────────────────────────────────────┬────────────────────────────────────────────┘
                                            │
                                            ▼
┌────────────────────────────────────────────────────────────────────────────────────────┐
│ 4. COMMERCIAL PAYMENT TERMS                                                            │
│ Contractual text terms (e.g. Net 30, Advance). Governs invoice schedule.               │
│ DOES NOT involve automated credit limits or scoring!                                   │
└────────────────────────────────────────────────────────────────────────────────────────┘
```

1. **Company Verification $\neq$ Purchasing Authority:** Verifying a company's registration number confirms that the business is legitimate; it does not authorize every employee to sign contracts.
2. **Company Verification $\neq$ Credit Approval:** Vetting a company does not grant credit terms. Corporate accounts default to advance payment terms until commercial terms are agreed upon.
3. **Purchasing Authority $\neq$ Credit Management:** Assigning `CanApproveQuotes = true` enables an officer to accept proposals. It does not introduce credit limits, scoring, or exposure tracking.

---

## 15. Existing Capability / Intended Reuse — Verification Required

The Sales Portal architecture reuses platform infrastructure. All items below are marked with their verification status:

| Architectural Component | Existing Implementation / Location | Intended Reuse Across Sales Channels | Verification Required Before Implementation |
|---|---|---|---|
| **Customer Master** | `Domain.Features.MasterData.Customer` | Reused directly. Distinguishes `Individual` vs. `Company`. Stores `DefaultSalesPriceCardId`. | Verify customer contact linking APIs and address snapshot models. |
| **Authentication & Tokens**| `Security` project (`UserService`, `TokenService`) | Reused directly. Enforces **email as username**, JWT tokens, and tenant scoping. | Verify registration completion pipeline across `SecurityDbContext` and `DomainDbContext`. |
| **Product Catalogue** | `Domain.Features.MasterData.Product` | Reused directly. Shared catalog filters by `IsSellable == true`. Packaging UOMs. | Verify availability indicator queries and packaging unit conversions. |
| **Pricing Resolution** | `IPricingResolutionService` (`PricingResolutionService`)| Reused directly. Resolves Retail (Product Master) vs. B2B (Price Card) pricing tiers. | Verify that `ResolveSalesPriceAsync` supports all required MOQ and UOM pricing rules. |
| **Sales Price Cards** | `Domain.Features.MasterData.PriceCard` | Reused directly. Governs B2B contract prices and receives atomic acceptance updates. | Verify that `PriceCard` aggregate supports non-destructive line versioning. |
| **Tax & Currency Engine** | `ITaxCodeService`, `IExchangeRateService`, `SalesOrder` | Reused directly. Handles inclusive/exclusive tax calculations and multi-currency exchange rates. | Verify effective-date lookup rules during quotation preparation. |
| **Email Retry Engine** | `RetryingEmailServiceDecorator`, `MailKitSmtpEmailService` | Reused directly. Complete in working tree (26 tests passing). Automatic Tier 1 retry (1 + 5). | Verify exact-diff review and DI resolution in API composition root. |
| **PDF Generation** | `PurchaseOrderPdfService` pattern in `ERP.Infrastructure.Pdf` | Pattern reused directly. Generates ISO 32000-1 binary PDF streams using pure .NET BCL. | Verify that pure .NET PDF layout engine satisfies quotation visual requirements. |
| **Core Sales Order** | `Domain.Features.Sales.SalesOrder` (`SalesOrderService`) | Reused directly. Serves as the single unified downstream fulfillment target for all channels. | Verify conversion field mappings, line item snapshots, and draft status initialization. |
| **Approval Infrastructure**| `Domain.Features.Procurement.Approval` | Generalized for Sales. Evaluates configurable margin and discount policy thresholds. | Verify that approval settings provider can be extended to support Sales discount/margin rules. |
| **Audit Trails** | `DomainAuditRecord`, `AuditingInterceptor` | Reused directly. Captures user identity, tenant, operations, and state snapshots. | Verify that audit records capture previous/new values for Price Card updates. |

---

## 16. Summary of Confirmed Decisions vs. Open Decisions

### 16.1 Confirmed Architectural Decisions (Closed)
1. **Three Channels:** E-commerce Sales, Direct Company Sales, and Sales Quotations. All converge into the unified `SalesOrder`.
2. **Implementation Priority:** Sales Quotation (Priority 1 — Immediate) $\rightarrow$ E-commerce Sales (Priority 2) $\rightarrow$ Direct Company Sales (Priority 3).
3. **Portal Navigation:** Level 1 Tenant Landing Page (directory) $\rightarrow$ Level 2 Shared Explore Products (universal discovery) $\rightarrow$ Level 3 Channel Execution.
4. **Independent Public Product Exploration:** Unauthenticated exploration of sellable products operates completely independently of login, registration, E-commerce, or quotation checkout.
5. **Customer Portal Workspace:** Authenticated customer self-service hub providing Dashboard, Orders, Quotations, Order Tracking, Document Downloads, and Notification History.
6. **Order Tracking Progression:** Customer visibility into 8 stages mapped to domain order and shipment states. Draft orders are never shown as approved for warehouse picking.
7. **Document Access Security:** Quotation PDFs, Order Confirmations, and Invoices streamed via authenticated endpoints enforcing tenant and customer ownership scopes.
8. **Email as Username:** Customer registration and login strictly use the human verified email address as the login username.
9. **Company Email Distinction:** `Company Email` is general business correspondence; `Authorised Contact Email` is the human login identity. Companies do not share passwords.
10. **Purchasing Authority Separation:** Public registration grants only `CanSubmitQuoteRequests`. `CanApproveQuotes` requires explicit authorization.
11. **Single Registration Screen:** One page with dynamic Individual vs. Company account type toggling, reusing Tenant Onboarding layout standards.
12. **Internal Price Card Assignment:** Handled as an internal commercial onboarding process, never as a public retail registration field.
13. **Mandatory Expiry Date:** Every formal quotation requires a compulsory `ValidUntilDate` stored in UTC (expiring 23:59:59.999 UTC) and enforced during acceptance.
14. **Acceptance Eligibility:** Only formally issued (`Sent`) and unexpired quotations can be accepted. Confirmed unissued quotes cannot be accepted.
15. **No Silent Extensions:** Expired quotations cannot be extended silently; renewal requires a formal revision (`-R1`).
16. **Atomic Price Card Update:** Accepting a quotation atomically creates the `SalesOrder` (`Draft`) and updates the customer's assigned Sales Price Card within the same database transaction.
17. **Price Card History Preservation:** Price Card updates must not destroy historical price records through in-place overwrites.
18. **Missing Price Card on Acceptance:** Sales Order creation completes; system does not silently auto-create a Price Card; reliable internal task raised for sales staff.
19. **Stock Out at Acceptance:** Acceptance succeeds regardless of stock level; backorder handling belongs to Sales Order fulfilment.
20. **Sales Fulfilment Terminology:** Uses Picking, Packing, Dispatch, and Delivery. Zero GRN references in Sales.
21. **Mandatory Manual Email Dispatch:** Quotation emails are only transmitted when sales staff explicitly clicks "Send Email".
22. **Resilient Notification Delivery:** Portal shows authoritative state even if email fails. Notification failures never roll back committed transactions.
23. **Payment Scope Exclusion:** Online payment processing, gateways, card capture, and gateway tracking are strictly excluded from Phase 1. Contractual payment terms may be displayed as text.
24. **Credit Management Exclusion:** Credit limits, scoring, and exposure checks are strictly excluded from Phase 1.

### 16.2 Open Business & Technical Decisions (Under Review)

| # | Topic | Current Proposed Working Standard | Open Decision Required from User |
|---|---|---|---|
| **1** | **Registration Consistency Mechanism** | Specify a pending registration workflow with retry to coordinate `SecurityDbContext` and `DomainDbContext`. | Should registration use a lightweight retry/completion coordinator or a full transactional Outbox? *(Requires source verification).* |
| **2** | **Company Contact Purchasing Limits** | Contacts carry boolean authority (`CanApproveQuotes`). Monetary spending thresholds deferred. | Should tiered spending limits be enforced per contact person (e.g. Buyer up to \$10k, Director up to \$100k)? |
| **3** | **Company Identity Verification Rules** | Customer registration creates account with Advance Payment terms until vetted by sales staff. | What documentation/verification workflow is required before a Company account is marked verified for corporate terms? |

---

## 17. Version History & Review Sign-Off

| Version | Date | Author / Agent | Changes / Enhancements |
|---|---|---|---|
| 1.0.0 | 2026-11-19 | Antigravity Agent | Initial requirements drafting for Sales Quotation workflow. |
| 1.1.0 | 2026-11-20 | Antigravity Agent | Disentangled status lifecycle, revision management rules, side-effect-free expiry query principles, and UI standard alignment. |
| 1.2.0 | 2026-11-20 | Antigravity Agent | Added 3-tier sales channel scope, 3-level tenant portal structure, customer identity/registration architecture, and email retry integration. |
| 1.3.0 | 2026-11-20 | Antigravity Agent | Added 3 strategic sales channels, universal Explore Products, shared registration with email as username, Price Card assignment, and atomic quotation acceptance. |
| 1.3.1 | 2026-11-20 | Antigravity Agent | Clarified global identity vs. tenant relationships; corrected Company login email mapping; clarified purchasing authority; added registration consistency; enforced mandatory quotation expiry; resolved missing Price Card handling; standardized fulfilment terminology; and qualified reuse claims. |
| **1.3.2** | **2026-11-20** | **Antigravity Agent** | **Complete Customer Portal, Tracking & Notification Architecture:** Added dedicated Customer Portal Workspace architecture (Dashboard, Orders, Quotations, Tracking, Documents, Notifications, Profiles); established independent unauthenticated public product exploration; defined 8-stage customer order tracking mapped to domain states; added secure document download streaming; integrated resilient event-driven notification architecture; clarified UTC calendar date quotation expiry boundary (23:59:59.999 UTC); formalized company verification vs. purchasing authority vs. credit boundaries; and **explicitly excluded payment processing, gateways, and online payment tracking from Phase 1**. |


---

## 18. Sales Quotation Backend Implementation Status Matrix (November 2026)

| Architectural Capability | Status Classification | Implementation Details & Verified Evidence |
|---|---|---|
| **Domain Aggregate (`SalesQuotation`)** | `IMPLEMENTED` | Fully mapped in `Domain/Features/Sales/SalesQuotation/SalesQuotation.cs` with items, UTC calendar date expiry boundary, currency snapshot, audit properties, and domain transition methods (`MarkSent`, `Accept`, `Decline`, `MarkConverted`, `MarkSuperseded`). |
| **Internal Authoring Workflow** | `IMPLEMENTED` | Draft creation, line additions, quantity/price updates, totals recalculation in `SalesQuotationService.cs`. |
| **Internal Review & Approval** | `IMPLEMENTED` | `SubmitQuotationAsync`, `ApproveQuotationAsync`, `RejectQuotationAsync`, `CancelQuotationAsync` in `SalesQuotationService.cs` integrated with `IApprovalWorkflowService`. |
| **Quotation Cloned Requests** | `IMPLEMENTED` | `CloneCustomerQuotationRequestFromOrderAsync` and `CloneCustomerQuotationRequestFromQuotationAsync` implemented in `SalesQuotationService.cs` re-validating active catalog products without copying stale historical prices. |
| **Manual Quotation Issuance (`Sent`)** | `IMPLEMENTED` | `MarkSent(sentAtUtc)` on aggregate and `SendQuotationAsync` in application service. Exposed via `POST api/companies/{c}/branches/{b}/sales-quotations/{id}/send`. Tested in `SalesBackendCompletionTests` (PASS). |
| **Atomic Acceptance & Conversion** | `IMPLEMENTED` | `AcceptQuotationAndConvertAtomicAsync` executing `SalesOrder` draft instantiation, non-destructive `PriceCard` update, and quotation status transition in a single database transaction. |
| **Customer Decline & Revision Request** | `IMPLEMENTED` | `DeclineQuotationAsync` updating status to `DeclinedByCustomer` with mandatory customer remarks. Tested in `SalesBackendCompletionTests` (PASS). |
| **Customer Quotation API Controller** | `IMPLEMENTED` | Authenticated `CustomerQuotationController` under `/api/portal/customer/quotations` (`GET`, `GET {id}`, `POST request`, `POST request/clone-order/{orderId}`, `POST request/clone-quotation/{quotationId}`, `POST {id}/accept`, `POST {id}/decline`, `GET {id}/pdf`). |
| **Customer Quotation PDF Generation** | `IMPLEMENTED` | Standards-compliant ISO 32000-1 binary PDF generator `SalesQuotationPdfService` in `ERP.Infrastructure.Pdf` with zero external dependencies, streamed via `GET .../{id}/pdf`. |
| **Post-Commit Notification Handlers** | `IMPLEMENTED` | Resilient post-commit notification logging without rolling back committed orders on SMTP failure. |
