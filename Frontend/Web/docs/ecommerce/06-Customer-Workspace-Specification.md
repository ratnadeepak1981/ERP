# ApexERP — Customer Workspaces Specification (Retail & Company 5-Group)

**Document Version:** 1.2.0  
**Status:** Architecture & Requirements Specification — Ready for Implementation  
**Date:** 2026-11-22  
**Target Repository:** `ApexERP` (`Frontend/Web`)  
**Related Documents:**
- [README.md](README.md) — Documentation index and reading paths
- [00-Ecommerce-Frontend-Master-Plan.md](00-Ecommerce-Frontend-Master-Plan.md) — Master architectural charter and invariants
- [01-Customer-Journey-and-Workspaces.md](01-Customer-Journey-and-Workspaces.md) — Customer journey and routes
- [02-Ecommerce-UI-UX-Specification.md](02-Ecommerce-UI-UX-Specification.md) — Design system and wireframes
- [03-Retail-Customer-Workflow.md](03-Retail-Customer-Workflow.md) — Retail purchasing path and reordering
- [04-Company-Customer-Workflow.md](04-Company-Customer-Workflow.md) — Company B2B purchasing workflows and Price Card maintenance
- [05-API-and-Backend-Integration.md](05-API-and-Backend-Integration.md) — Backend API integration matrix and address linking
- [07-Implementation-and-Testing-Plan.md](07-Implementation-and-Testing-Plan.md) — Standardized E1–E10 phased rollout & QA tests

---

## 1. Architectural Foundation & Customer Isolation

The **ApexERP Customer Workspace** delivers a dedicated, authenticated self-service portal environment completely separate from the internal ERP operational workspace (`/app/*`).

### 1.1 Fundamental Workspace Principles:
1. **Strict Operational Boundary:** Internal employees operate inside `/app/*` (`TenantLayoutComponent`), while external customers operate inside `/customer-portal/*` (`CustomerPortalLayoutComponent`). The Angular guard `workspaceGuard` strictly blocks external customer accounts from entering `/app/*`.
2. **Two Distinct Customer Workspaces:**
   - **Retail Customer Workspace (`/customer-portal/retail/*`):** Designed for individual consumer buyers purchasing at standard retail prices. Features an uncluttered layout focused on personal orders, deliveries, saved addresses, and historical reordering (`Buy Again`).
   - **Company Customer Workspace (`/customer-portal/company/*`):** A professional, 5-group B2B enterprise workspace supporting multiple authorized contacts, Selling Company header switching, formal quotation negotiations, contract Price Card direct orders, and invoice reconciliation.
3. **Reactive Signal Architecture:** In compliance with modern Angular standards, all workspace components utilize Angular 18 reactive primitives (`signal`, `computed`, `inject`) with `ChangeDetectionStrategy.OnPush`.

### 1.2 Shared Customer Workspace Foundation (`CustomerWorkspaceLayoutComponent`)

Both customer account types share a unified foundational layout shell (`CustomerWorkspaceLayoutComponent`):

#### 1. Universal Command Header:
- **Tenant Corporate Identity:** Company Logo, Corporate Name, and "Customer Portal" badge.
- **Customer Context & Badges:**
  - Authenticated user name and job title / contact role.
  - Customer Master Code (`CustomerCode`: e.g. `CUST-IND-1002` or `CUST-0104`).
  - Account Type Badge: `[ Retail Customer ]` or `[ Corporate B2B Customer ]`.
- **B2B Selling Company Switcher:** When logged in as an Organisation buyer, renders an active dropdown selector allowing authorized contacts to switch their operational selling company context without re-authenticating.
- **Action Utilities:** Cart icon trigger with active line count badge, Fluent theme switcher (light/dark), and Sign Out button.

#### 2. Dynamic Sidebar Navigation:
- **Retail Navigation Menu (`CUSTOMER_INDIVIDUAL`):**
  1. `Dashboard` (`/customer-portal/retail/dashboard`)
  2. `Product Catalogue` (`/storefront/catalog`)
  3. `Shopping Cart` (`/storefront/cart`)
  4. `My Orders` (`/customer-portal/retail/orders`)
  5. `My Addresses` (`/customer-portal/retail/profile#addresses`)
  6. `My Profile` (`/customer-portal/retail/profile`)
- **Organisation 5-Group Menu (`CUSTOMER_COMPANY_ADMIN`):**
  1. `1. Dashboard` (`/customer-portal/company/dashboard`)
  2. `2. Purchasing` (`/purchasing/catalog`, `/purchasing/cart`, `/purchasing/quotations/request`, `/purchasing/quotations`, `/purchasing/direct-orders`)
  3. `3. Orders & Fulfilment` (`/orders`, `/orders/tracking`)
  4. `4. Finance` (`/finance/invoices`)
  5. `5. Company Administration` (`/management/profile`, `/management/users`, `/management/permissions`, `/management/locations`, `/management/sellers`)

#### 3. Corporate Verification Alert Banner:
If a corporate organisation customer account has `RequiresAuthorization == true` (awaiting administrative verification after matching an existing company), a high-visibility warning banner is rendered at the top of the workspace content area:
> **Account Pending Corporate Verification:** Your account has been linked to an existing corporate master record and is awaiting confirmation from the tenant administrator. Commercial ordering, Price Card contract rates, and quotation acceptance are locked until verification is complete.

#### 4. Route Security & Boundaries:
- `authGuard`: Enforces active JWT authentication.
- `workspaceGuard`: Confirms user is an external customer; blocks internal tenant staff and blocks customer accounts from accessing `/app/*`.
- `roleGuard`: Ensures Retail accounts cannot access `/customer-portal/company/*` and Corporate accounts cannot access `/customer-portal/retail/*`.

---

## 2. Retail Customer Workspace Specification

### 2.1 Component Architecture & Route Map

```
/customer-portal/retail/
├── dashboard      -> RetailDashboardComponent
├── orders         -> RetailOrdersComponent (with "Buy Again" action)
│   └── :id        -> RetailOrderDetailComponent (lines, tracking & reorder)
├── checkout       -> RetailCheckoutComponent (Address Master selection)
└── profile        -> RetailProfileComponent (personal details & saved addresses)
```

### 2.2 Detailed Component Breakdown

#### 1. `RetailDashboardComponent`
- **Purpose:** Central landing page for individual retail customers upon authentication.
- **Visual Structure:**
  - Header with user greeting and personal account summary.
  - Quick KPI summary cards: Total Orders Placed, Active Shipments in Transit, Saved Delivery Addresses.
  - "Recent Purchases" list rendering the last 3 orders with status badges and quick tracking links.
  - Quick action shortcuts: "Browse Storefront", "View Complete Order History", "Update Profile".
- **Backend API:** `GET /api/portal/customer/orders` (limited to top 5 recent orders).

#### 2. `RetailOrdersComponent`
- **Purpose:** Paginated, sortable register of all historical orders placed by the individual.
- **Implementation:** Reuses `DataTableComponent` (`Frontend/Web/src/app/shared/components/data-table/`).
- **Columns:** Order Number (`OrderNumber`), Order Date (`OrderDate`), Item Count, Currency, Grand Total (`TotalAmount`), Fulfillment Status (`StatusBadgeComponent`), Actions ("View Details", "Buy Again").
- **Backend API:** `GET /api/portal/customer/orders`.

#### 3. `RetailOrderDetailComponent` & Reordering Modal
- **Purpose:** Comprehensive post-order view for a specific order and launchpad for reordering.
- **Visual Structure:**
  - Order metadata card (Order Number, Date, Status, Delivery Destination).
  - **Fulfillment Progress Tracker:** Graphical timeline showing progression through milestones: `Draft -> Submitted -> Confirmed -> Picking -> Packing -> Dispatched -> Delivered`.
  - Ordered line items table with product name, SKU, unit price, quantity, tax, and line total.
  - Logistics info: Carrier name, Waybill tracking number, tracking link.
  - **"Buy Again / Reorder" Action:** Opens the line selection modal, allowing the customer to select lines, adjust quantities, revalidate current price and inventory via `POST /api/ecommerce/cart/items`, and stage items into the active cart.
- **Backend APIs:**
  - `GET /api/portal/customer/orders/{id}`
  - `GET /api/portal/customer/orders/{id}/tracking`

#### 4. `RetailCheckoutComponent`
- **Purpose:** Final step of the Retail purchasing path (Retail Checkout).
- **Form Controls:** Saved address dropdown populated from Customer Address Master (`CustomerAddress`), new address input option, preferred delivery date picker, delivery remarks textarea.
- **Order Summary:** Displays item subtotal, tax amount, and delivery fee status.
- **Action Button:** "Confirm & Submit Sales Order" &rarr; calls `POST /api/ecommerce/cart/checkout?companyId={companyId}` with `deliveryAddressId`.

#### 5. `RetailProfileComponent`
- **Purpose:** Self-service maintenance of personal customer contact data and saved delivery addresses in Customer Address Master.
- **Backend API:** `GET /api/portal/profile`.

---

## 3. Company Customer Workspace Specification (5 Groups)

The Company Customer Workspace is organized into five distinct operational groups:

### 3.1 Group 1: Dashboard

```
/customer-portal/company/
└── dashboard/     -> CompanyDashboardComponent
```

#### `CompanyDashboardComponent`
- **Purpose:** Executive B2B purchasing command center displaying high-level metrics, commercial alerts, and quick actions.
- **Header:** Renders Customer Company name, Active Selling Company, and Selling Company selector.
- **Key Metrics (KPI Cards):**
  1. **Pending Quotation Requests:** Count and total value of requests currently being reviewed by Selling Company sales teams.
  2. **Quotations Awaiting Acceptance:** Count and total value of formal proposals issued by the seller awaiting customer signature/acceptance.
  3. **Active Sales Orders:** Count of confirmed orders currently progressing through warehouse fulfillment.
  4. **Recent Deliveries:** Shipments delivered within the last 14 days.
- **Widgets:**
  - **Recent Transactions Feed:** Unified table displaying the latest quotations, direct orders, and shipments.
  - **Live Dispatch Tracker:** Displays active waybills, carrier details, and real-time transit milestones.
  - **Quick Action Bar:** Direct buttons to "Request a Quotation" and "Create Direct Sales Order".
- **Backend API:** `GET /api/portal/customer/dashboard?sellingCompanyId={companyId}`.

---

### 3.2 Group 2: Purchasing

```
/customer-portal/company/purchasing/
├── catalog                -> CompanyCatalogComponent
├── cart                   -> CartViewComponent (B2B Context)
├── quotations/request     -> QuotationRequestComponent
├── quotations             -> QuotationRegisterComponent
├── quotations/:id         -> QuotationReviewComponent (Atomic Accept & Price Card Update)
├── direct-orders/new      -> DirectOrderComponent
└── direct-orders          -> DirectOrderHistoryComponent
```

#### 1. `CompanyCatalogComponent`
- **Purpose:** Product discovery scoped to the selected Selling Company, highlighting contract Price Card items and volume availability.
- **Features:** Category navigation tree, instant search, stock status badges, UOM conversion details, and "Add to Cart" / "Request Quote" buttons.
- **Backend API:** `GET /api/ecommerce/catalogue/{companyId}/products`.

#### 2. `QuotationRequestComponent`
- **Purpose:** Interactive form allowing corporate buyers to prepare and submit formal quotation requests.
- **Entry Shortcuts:**
  - **"Clone from Past Order" Button:** Invokes modal to select historical `SalesOrder`; calls `POST /api/portal/customer/quotations/request/clone-order/{orderId}`.
  - **"Clone from Past Quotation" Button:** Invokes modal to select prior `SalesQuotation`; calls `POST /api/portal/customer/quotations/request/clone-quotation/{quotationId}`.
- **Line Items Table (`EditableDataTableComponent`):**
  - Columns: Product Item, UOM, Required Quantity, List Price (Reference), **Requested Target Unit Price**, Line Customer Remarks.
- **Commercial Metadata:** Required By Date, Customer Project Code, Delivery Address selection, Technical Specification Upload.
- **Submission:** Calls `POST /api/portal/customer/quotations/request?companyId={companyId}&branchId={branchId}`.

#### 3. `QuotationRegisterComponent`
- **Purpose:** Searchable, filterable register tracking all quotation requests across their full lifecycle.
- **Status Filter Tabs:** All, Submitted (Pending Review), **Approved (Awaiting Acceptance)**, Accepted, Declined, Expired.
- **Columns:** Quotation Number, Issue Date, Validity Expiration Date, Item Count, Target Value, Offered Commercial Total, Status, Action ("Review Proposal").
- **Backend API:** `GET /api/portal/customer/quotations?status={status}`.

#### 4. `QuotationReviewComponent`
- **Purpose:** Detailed review and decision screen for formal proposals issued by the Selling Company.
- **Visual Layout:**
  - **Header:** Seller details, validity countdown badge, commercial terms (30 Days Net, FOB Destination).
  - **Embedded PDF Preview:** Sanitized iframe rendering `GET /api/portal/customer/quotations/{id}/pdf`.
  - **Line Comparison Grid:** Side-by-side comparison of customer requested price vs. seller final offered price and applied discount.
  - **Decision Panel:**
    - Delivery Address Master confirmation (`DeliveryAddressId`).
    - Input: Customer PO Reference (`CustomerPoNumber`), Decision Remarks.
    - Button: "Decline Proposal" &rarr; modal requires remarks; calls `POST .../decline`.
    - Button: **"Accept Quotation & Convert to Order"** &rarr; calls `POST .../accept` (requires `PORTAL_QUOTATION.ACCEPT`). Triggers atomic `SalesOrder` (`Draft`) creation and Price Card creation/updating across Scenarios A–D.

#### 5. `DirectOrderComponent`
- **Purpose:** High-speed order entry screen for pre-approved corporate clients purchasing directly from assigned contract Price Cards.
- **Eligibility Check:**
  - Validates that customer holds an assigned `DefaultSalesPriceCardId` and permission `PORTAL_ORDER.SUBMIT`.
  - If ineligible, renders an informative empty state directing the user to the quotation request channel.
- **Contract Line Table:**
  - Displays contract SKUs, approved prices, UOM, and **Minimum Order Quantity (MOQ)**.
  - Stepper input enforces `Quantity >= MOQ`.
- **Submission:** Submits `CreateDirectB2BOrderRequest` with `deliveryAddressId` to `POST /api/portal/customer/orders/direct-b2b`.

---

### 3.3 Group 3: Orders & Fulfilment

```
/customer-portal/company/orders/
├── list           -> CompanyOrdersComponent
├── :id            -> CompanyOrderDetailComponent
└── tracking       -> DispatchTrackingComponent
```

#### 1. `CompanyOrdersComponent`
- **Purpose:** Master register of all corporate Sales Orders placed across quotation and direct channels.
- **Filters:** Selling Company selector, Date Range, Fulfillment Status (`Draft`, `Submitted`, `Confirmed`, `Picking`, `Packing`, `Dispatched`, `Delivered`, `Cancelled`).
- **Columns:** Order Number, Date, Customer PO Ref, Channel (Quotation / Direct / Online), Total Net, Total VAT, Gross Total, Status Badge, Actions ("View Details").
- **Backend API:** `GET /api/portal/customer/orders`.

#### 2. `CompanyOrderDetailComponent`
- **Purpose:** Deep dive into specific order lines, delivery status, and commercial snapshots.
- **8-Stage Fulfillment Tracker:** Graphical visual component displaying progress through internal ERP milestones.
- **Historical Address Preservation:** Displays `DeliveryAddressSnapshot` ensuring historical delivery destinations remain accurate even if master addresses are subsequently updated.
- **Data Scrubbing:** Internal employee notes, worker IDs, picking batch routes, and profit margins are completely scrubbed by the backend.
- **Backend APIs:** `GET /api/portal/customer/orders/{id}` and `GET /api/portal/customer/orders/{id}/tracking`.

#### 3. `DispatchTrackingComponent`
- **Purpose:** Comprehensive logistics and freight tracking view for dispatched shipments.
- **Features:** Carrier badge (e.g., DHL Express, FedEx Freight), Waybill/Tracking Number, Estimated Delivery Date, Delivery Receiving Dock, and parcel list.
- **Backend API:** `GET /api/portal/customer/orders/{id}/tracking`.

---

### 3.4 Group 4: Finance

```
/customer-portal/company/finance/
└── invoices       -> CompanyInvoicesComponent
```

#### `CompanyInvoicesComponent`
- **Purpose:** Commercial invoice summary and payment tracking for corporate accounting reconciliation.
- **Columns:** Invoice Number, Invoice Date, Due Date, Sales Order Reference, Total Billed, Balance Due, Payment Status (`Unpaid`, `Partially Paid`, `Paid`).
- **Backend API:** `GET /api/portal/customer/invoices`.

---

### 3.5 Group 5: Company Administration

```
/customer-portal/company/management/
├── profile        -> CompanyProfileComponent
├── users          -> CompanyUsersComponent
├── permissions    -> UserPermissionsComponent
├── locations      -> DeliveryLocationsComponent (Customer Address Master)
└── sellers        -> SellingCompaniesComponent
```

#### 1. `CompanyProfileComponent`
- **Purpose:** Displays corporate master record: Legal Corporate Name, Registration Number (CRN), Tax/VAT Number, Headquarters Address, Primary Phone, Billing Currency.
- **Backend API:** `GET /api/portal/profile`.

#### 2. `CompanyUsersComponent` & `UserPermissionsComponent`
- **Purpose:** Administrative overview of authorized company employees registered to transact under this corporate account.
- **Columns:** Contact Person Name, Job Title, Business Email, Mobile Number, Primary Representative Status, Assigned Role (`CUSTOMER_COMPANY_ADMIN`, `CUSTOMER_BUYER`, `CUSTOMER_VIEWER`).
- **Authority Delegation:** Primary administrators (`CUSTOMER_COMPANY_ADMIN`) can view permissions assigned to secondary buyers.

#### 3. `DeliveryLocationsComponent` (Customer Address Master)
- **Purpose:** Management of corporate receiving docks, warehouse locations, and delivery destinations configured in Customer Address Master (`CustomerAddress` / `Address`).

#### 4. `SellingCompaniesComponent`
- **Purpose:** Commercial overview of all Customer–Selling Company relationships operating within the tenant.
- **Key Display Elements:**
  - **Selling Company Profile:** Legal Name, Company Code, Base Currency.
  - **Commercial Relationship Status:** `Active`, `Under Review`, or `Suspended`.
  - **Assigned Contract Sales Price Card:** Shows the specific contract Price Card assigned to this relationship (`PriceCardCode`, `PriceCardName`, Effective Dates). If unassigned, displays *"No Contract Card (Standard Pricing / RFQ Only)"*.
  - **Commercial Terms:** Relationship-specific payment terms and credit limit overrides.
  - **Primary Actions:**
    - Button: **"Set as Active Selling Company"** &rarr; invokes `POST /api/portal/profile/select-company` to switch active purchasing context.
    - Button: **"View Contract Price Schedule"** &rarr; displays a modal/view of contract products, unit prices, and MOQs for this specific relationship.
- **Backend API:** `GET /api/portal/selling-companies` and `GET /api/portal/selling-companies/{id}/relationship`.
