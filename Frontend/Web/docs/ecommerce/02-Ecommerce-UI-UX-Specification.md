# ApexERP — E-commerce UI/UX Design Specification & Wireframes

**Document Version:** 1.1.0  
**Status:** Architecture & Requirements Specification — Ready for Implementation  
**Date:** 2026-11-21  
**Target Repository:** `ApexERP` (`Frontend/Web`)  
**Design Reference:** Clean Enterprise Fluent-Inspired UI ([ApexERP-Frontend-UI-Standards.md](file:///z:/ERP/ApexERP-Frontend-UI-Standards.md))  
**Related Documents:**
- [README.md](README.md) — Documentation index and reading paths
- [00-Ecommerce-Frontend-Master-Plan.md](00-Ecommerce-Frontend-Master-Plan.md) — Architectural charter and invariants
- [01-Customer-Journey-and-Workspaces.md](01-Customer-Journey-and-Workspaces.md) — Customer journey and routes
- [03-Retail-Customer-Workflow.md](03-Retail-Customer-Workflow.md) — Retail purchasing path and reordering
- [04-Company-Customer-Workflow.md](04-Company-Customer-Workflow.md) — Company B2B purchasing workflows and Price Card maintenance
- [05-API-and-Backend-Integration.md](05-API-and-Backend-Integration.md) — API endpoints, address linking, and DTO matrix
- [06-Customer-Workspace-Specification.md](06-Customer-Workspace-Specification.md) — Workspace component details
- [07-Implementation-and-Testing-Plan.md](07-Implementation-and-Testing-Plan.md) — Phased rollout roadmap (E1–E10)

---

## 1. Design System Reference & Policy

The ApexERP E-commerce frontend complies with the approved enterprise Fluent design standard defined in `ApexERP-Frontend-UI-Standards.md`.

### 1.1 Strict "No New PrimeNG Components" Policy `[Agreed Business Rule]`
- **PrimeNG Inspection-Only Policy:** While legacy components in the repository may reference PrimeNG modules (`ButtonModule`, `InputTextModule`, `PasswordModule`), **no new PrimeNG modules or components may be introduced**.
- All newly authored components must use native semantic HTML5 elements styled directly with Fluent CSS utility classes and design tokens (`.erp-button`, `.erp-card`, `.erp-table`, `.p-inputtext`, CSS variables).
- Reuse existing shared ApexERP components:
  - `PageHeaderComponent` (`Frontend/Web/src/app/shared/components/page-header/`)
  - `DataTableComponent` (`Frontend/Web/src/app/shared/components/data-table/`)
  - `EditableDataTableComponent` (`Frontend/Web/src/app/shared/components/data-table/editable-data-table.component.ts`)
  - `StatusBadgeComponent` (`Frontend/Web/src/app/shared/components/status-badge/`)
  - `ConfirmationDialogComponent` (`Frontend/Web/src/app/shared/components/confirmation-dialog/`)
  - `LookupDropdownComponent` (`Frontend/Web/src/app/shared/components/lookup-dropdown/`)
  - `EmptyStateComponent` (`Frontend/Web/src/app/shared/components/empty-state/`)
  - `LoadingStateComponent` (`Frontend/Web/src/app/shared/components/loading-state/`)
  - `ErrorStateComponent` (`Frontend/Web/src/app/shared/components/error-state/`)

---

## 2. Global Design Tokens & Typography

All visual elements, spacing, borders, shadows, and color states are strictly bound to CSS custom properties defined in `Frontend/Web/src/styles/_tokens.scss` and `_themes.scss`. Hardcoding hex color codes or literal pixel margins in component stylesheets is prohibited.

### 2.1 Spacing Grid & Container Geometry

```scss
// Spacing scale (4px base)
--erp-space-1: 4px;
--erp-space-2: 8px;
--erp-space-3: 12px;
--erp-space-4: 16px;
--erp-space-5: 20px;
--erp-space-6: 24px;
--erp-space-8: 32px;

// Corner radii
--erp-radius-sm: 4px;      // Buttons, inputs, filter chips
--erp-radius-md: 6px;      // Product cards, containers, dialogs
--erp-radius-lg: 8px;      // Hero banners, major workspaces
--erp-radius-pill: 9999px; // Badges, category pills, status indicators

// Typography Stack
--erp-font-sans: 'Segoe UI', 'Inter', -apple-system, BlinkMacSystemFont, 'Roboto', sans-serif;
--erp-font-mono: 'Cascadia Code', 'Fira Code', Consolas, monospace;
```

### 2.2 Color Tokens & Surface Hierarchy

| CSS Variable | Light Mode (Default) | Dark Mode (`.theme-fluent-dark`) | Semantic Usage |
|---|---|---|---|
| `--erp-primary` | `#0f6cbd` | `#2886de` | Primary actions, branding accents, active nav tabs |
| `--erp-primary-hover` | `#115ea3` | `#479ef5` | Hover state for primary buttons |
| `--erp-primary-light` | `#ebf3fc` | `#1e3a5f` | Active tab background, selected row tint |
| `--erp-bg-app` | `#f8fafc` | `#0f172a` | Viewport canvas background |
| `--erp-bg-surface` | `#ffffff` | `#1e293b` | Cards, table containers, modal bodies |
| `--erp-border` | `#e2e8f0` | `#334155` | Card borders, dividers, table cell borders |
| `--erp-border-subtle` | `#f1f5f9` | `#1e293b` | Secondary line separators |
| `--erp-text-main` | `#1e293b` | `#f8fafc` | Primary headings, product titles, table data |
| `--erp-text-secondary` | `#475569` | `#94a3b8` | Subtitles, labels, descriptions |
| `--erp-text-muted` | `#64748b` | `#64748b` | Timestamps, SKUs, breadcrumbs |
| `--erp-success` | `#107c41` | `#22c55e` | "In Stock", "Delivered", "Approved" |
| `--erp-warning` | `#d83b01` | `#f97316` | "Made to Order", "Pending Acceptance" |
| `--erp-danger` | `#a80000` | `#ef4444` | "Out of Stock", "Declined", "Cancelled" |

---

## 3. Two Distinct Visual Experiences

1. **Public E-commerce Storefront (B2C & Public View):**
   - Clean, modern, open digital presence representing a professional enterprise website.
   - High visual emphasis on product photography, category discovery, feature highlights, and clear pricing.
   - Wide, responsive container layout with hero banners and frictionless search filters.
2. **Customer Workspaces (Retail & Company Portal):**
   - Follows the proven ApexERP enterprise application layout with sidebar navigation, breadcrumbs, and command bars.
   - Dense, high-efficiency data tables (`DataTableComponent`, `EditableDataTableComponent`), filter toolbars, status badges, and sticky footer action cards.
   - Company Workspace provides 5-group hierarchical navigation (Dashboard, Purchasing, Orders & Fulfilment, Finance, Company Administration) with a Selling Company scope selector in the header.

---

## 4. Page-Level Wireframe Specifications

### View 1: Tenant Landing Page (`/` or `/storefront/landing`)

```
+---------------------------------------------------------------------------------------------------------+
| [LOGO] Alpha Industrial Parts       Selling Company: [ Alpha Tools Ltd v ]    [Search Parts...]  [Sign In] [Register] |
| Navigation:  Home  |  Product Catalogue  |  About Us  |  Capabilities  |  Contact  |  Customer Portal       |
+---------------------------------------------------------------------------------------------------------+
|                                                                                                         |
|  [ HERO BANNER ]                                                                                        |
|  High-Precision Manufactured Components & Assemblies                                                    |
|  Direct from ISO 9001 certified production lines with guaranteed batch traceability.                    |
|                                                                                                         |
|  [ Explore Products & Materials CTA ]      [ Request Commercial B2B Account CTA ]                       |
|                                                                                                         |
+---------------------------------------------------------------------------------------------------------+
|  FEATURED CATEGORIES                                                                                    |
|  +--------------------+ +--------------------+ +--------------------+ +--------------------+            |
|  | [Icon] CNC Parts   | | [Icon] Raw Hardware| | [Icon] Hydraulics  | | [Icon] Assemblies  |            |
|  | 420+ Items         | | 1,200+ Fasteners   | | Valves & Manifolds | | Sub-assemblies     |            |
|  +--------------------+ +--------------------+ +--------------------+ +--------------------+            |
+---------------------------------------------------------------------------------------------------------+
|  CURATED FEATURED PRODUCTS                                                                               |
|  +----------------------+ +----------------------+ +----------------------+ +----------------------+    |
|  | [IMG]                | | [IMG]                | | [IMG]                | | [IMG]                |    |
|  | SKU: CNC-SP-001      | | SKU: FAST-TI-100     | | SKU: HYD-VAL-350     | | SKU: CONV-ROL-2M     |    |
|  | Milling Spindle      | | Titanium Hex Pack    | | Pressure Regulator   | | Conveyor Assembly    |    |
|  | $1,250.00 / unit     | | $185.50 / pack       | | $620.00 / unit       | | $480.00 / unit       |    |
|  | [In Stock]           | | [In Stock]           | | [Made to Order]      | | [In Stock]           |    |
|  | [ View Details ]     | | [ View Details ]     | | [ View Details ]     | | [ View Details ]     |    |
|  +----------------------+ +----------------------+ +----------------------+ +----------------------+    |
+---------------------------------------------------------------------------------------------------------+
|  SERVICES & ASSURANCES: [ Batch Traceability ]  [ Material Mill Certs ]  [ Contract Volume Pricing ]     |
+---------------------------------------------------------------------------------------------------------+
|  FOOTER: Corporate details, legal address, contact numbers, inquiries@alpha.com, © 2026 Alpha Holdings  |
+---------------------------------------------------------------------------------------------------------+
```

---

### View 2: Public Storefront Catalogue (`/storefront/catalog`)

```
+---------------------------------------------------------------------------------------------------------+
| [Header & Brand Bar]                                                      [Cart (0)] [Sign In] [Register] |
+---------------------------------------------------------------------------------------------------------+
| Breadcrumb: Storefront > Product Catalogue                                                              |
| Page Header: "Product Catalogue & Industrial Materials" (420 Products Available)                       |
+-----------------------------+---------------------------------------------------------------------------+
| SIDEBAR FILTERS             | CATALOGUE CONTROLS: [ Search SKU or Name... ]  Sort By: [ Price: Low-High v ]|
|                             +---------------------------------------------------------------------------+
| Category                    | ACTIVE FILTERS: [ Hydraulics (x) ] [ In Stock Only (x) ] [ Clear All ]    |
| [x] All Categories          +---------------------------------------------------------------------------+
| [ ] CNC Components (85)     | +----------------------+ +----------------------+ +----------------------+ |
| [x] Hydraulics (42)         | | [Product Image]      | | [Product Image]      | | [Product Image]      | |
| [ ] Raw Hardware (210)      | | CAT: Hydraulics      | | CAT: Hydraulics      | | CAT: Hydraulics      | |
|                             | | [Available Badge]    | | [Available Badge]    | | [Made to Order]      | |
| Availability                | | SKU: HYD-VAL-350     | | SKU: HYD-PUMP-200    | | SKU: HYD-CYL-100     | |
| [x] In Stock (340)          | | Pressure Regulator   | | Gear Pump 200 Bar    | | Double-Acting Cylinder|
| [ ] Made to Order (80)      | | Lead: 3-5 Days       | | Lead: Immediate      | | Lead: 10-14 Days     | |
|                             | | $620.00 / unit       | | $840.00 / unit       | | $310.00 / unit       | |
| Price Range                 | | [Details] [Add Cart*]| | [Details] [Add Cart*]| | [Details] [Add Cart*]| |
| Min: [$____]  Max: [$____]  | +----------------------+ +----------------------+ +----------------------+ |
|                             | * Note: Clicking "Add Cart" prompts unauthenticated visitors to sign in.   |
| Brand / Manufacturer        | +-----------------------------------------------------------------------+ |
| [x] Alpha Precision         | | [Pagination:  <  1  [2]  3  4  5  >  ]  Displaying 1-12 of 42 Items   | |
+-----------------------------+---------------------------------------------------------------------------+
```

---

### View 3: Product Detail View (`/storefront/products/:productId`)

```
+---------------------------------------------------------------------------------------------------------+
| [Header & Brand Bar]                                                      [Cart (0)] [Sign In] [Register] |
+---------------------------------------------------------------------------------------------------------+
| Breadcrumb: Storefront > Hydraulics > Pressure Regulator Valve 350 Bar                                  |
+---------------------------------------------+-----------------------------------------------------------+
| PRODUCT GALLERY                             | PRODUCT SPECIFICATIONS & ORDERING                         |
| +-----------------------------------------+ | SKU: HYD-VAL-350                                          |
| |                                         | | Category: Hydraulic Components                            |
| |                                         | | Brand: Alpha Precision Flow Systems                       |
| |              [ HIGH-RES ]               | | Status: [ AVAILABLE IN STOCK ]                            |
| |             [ 3D VIEWER ]               | | Lead Time: 3-5 Business Days                              |
| |                                         | +-----------------------------------------------------------+
| |                                         | | Selling Price: $620.00 USD / unit (Tax Excl.)             |
| +-----------------------------------------+ +-----------------------------------------------------------+
| THUMBNAILS: [Img 1] [Img 2] [Img 3] [Cad]   | UNIT OF MEASURE: [ Each (EA) v ]                          |
|                                             | PACKAGING: [ Single Unit Box v ]                          |
| TECHNICAL DOCUMENTATION:                    | QUANTITY:  [ - ] [  10  ] [ + ] (Min Order: 1 unit)       |
| [Download PDF Spec Sheet] [Download 3D CAD] | +-----------------------------------------------------------+
|                                             | TOTAL ESTIMATE: $6,200.00 USD                             |
|                                             |                                                           |
|                                             | [ ADD TO SHOPPING CART ]  (Prompts Login if guest)        |
|                                             | [ REQUEST CUSTOM B2B QUOTATION ]                          |
+---------------------------------------------+-----------------------------------------------------------+
```

---

### View 4: Customer Login View (`/login`)

```
+---------------------------------------------------------------------------------------------------------+
| [Header Brand Bar: Alpha Industrial Store]                                 [Return to Storefront]       |
+---------------------------------------------------------------------------------------------------------+
|                                  +------------------------------------+                                 |
|                                  |        CUSTOMER PORTAL SIGN IN     |                                 |
|                                  |   Access B2C Orders or B2B Portal  |                                 |
|                                  +------------------------------------+                                 |
|                                  | [!] You must be logged in to add   |                                 |
|                                  |     items to your shopping cart.   |                                 |
|                                  |                                    |                                 |
|                                  | Email Address:                     |                                 |
|                                  | [ customer@company.com           ] |                                 |
|                                  |                                    |                                 |
|                                  | Password:                          |                                 |
|                                  | [ ****************************** ] |                                 |
|                                  |                                    |                                 |
|                                  | [ SIGN IN TO CUSTOMER PORTAL ]     |                                 |
|                                  |                                    |                                 |
|                                  | Don't have an account?             |                                 |
|                                  | [ Create New Customer Account ]    |                                 |
|                                  +------------------------------------+                                 |
+---------------------------------------------------------------------------------------------------------+
```

---

### View 5: Customer Registration View (`/register`)

```
+---------------------------------------------------------------------------------------------------------+
| [Header Brand Bar: Alpha Industrial Store]                                 [Return to Storefront]       |
+---------------------------------------------------------------------------------------------------------+
|                     +-------------------------------------------------------------+                     |
|                     |                  CREATE YOUR CUSTOMER ACCOUNT               |                     |
|                     |    Select your account type to proceed with registration    |                     |
|                     +------------------------------+------------------------------+                     |
|                     |  [ TAB 1: RETAIL INDIVIDUAL ]|  [ TAB 2: COMPANY / B2B (Active)]                  |
|                     +------------------------------+------------------------------+                     |
|                     | COMPANY MASTER DATA:                                        |                     |
|                     | Company Legal Name*:  [ Acme Manufacturing Ltd             ]|                     |
|                     | Trading Name:         [ Acme Parts                         ]|                     |
|                     | Registration Number:  [ CRN-9844210                        ]|                     |
|                     | Tax / VAT Number*:    [ GB-123456789                       ]|                     |
|                     | Company Phone:        [ +44 20 7946 0912                   ]|                     |
|                     | Primary Delivery Address*: [ 100 Industrial Park Way       ]|                     |
|                     | City*: [ Manchester ]     Postal Code*: [ M1 2AB ]          |                     |
|                     | Country*: [ United Kingdom v ]                              |                     |
|                     |                                                             |                     |
|                     | PRIMARY HUMAN CONTACT (Login Identifier):                   |                     |
|                     | Contact Full Name*:   [ Sarah Jenkins                      ]|                     |
|                     | Job Title / Role:     [ Procurement Director               ]|                     |
|                     | Business Email*:      [ s.jenkins@acme.com                 ]|                     |
|                     | Mobile Phone:         [ +44 7700 900123                    ]|                     |
|                     |                                                             |                     |
|                     | SECURITY CREDENTIALS:                                       |                     |
|                     | Password*:            [ ****************************** ]    |                     |
|                     | Confirm Password*:    [ ****************************** ]    |                     |
|                     |                                                             |                     |
|                     | [x] I agree to the B2B commercial terms and conditions.     |                     |
|                     |                                                             |                     |
|                     | [ REGISTER COMPANY ACCOUNT ]                                |                     |
|                     +-------------------------------------------------------------+                     |
+---------------------------------------------------------------------------------------------------------+
```

---

### View 6: Retail Customer Dashboard & Order History (`/customer-portal/retail/dashboard`)

```
+---------------------------------------------------------------------------------------------------------+
| [LOGO] Alpha Retail Portal       Welcome, John Doe (Individual Customer)       [Cart (2)] [Sign Out]   |
+---------------------------------------------------------------------------------------------------------+
| Navigation:  [Overview]  |  [My Orders / History]  |  [Browse Storefront]  |  [Saved Addresses]  | [Profile]|
+---------------------------------------------------------------------------------------------------------+
| Page Header: "My Account & Orders Overview"                                                            |
+---------------------------------------------+-----------------------------------------------------------+
| RECENT PURCHASES (ORDER HISTORY)            | QUICK METRICS                                             |
| +-----------------------------------------+ | Total Orders Placed:  5 Orders                            |
| | Order #SO-2026-0891     Date: 12-Nov-2026| | Active Deliveries:    1 In Transit                        |
| | 3 Items  •  Total: $1,420.00 USD        | | Default Address:      14 Oak Street, London (Default)     |
| | Status: [ IN DISPATCH ]                 | +-----------------------------------------------------------+
| | [ View Details ]  [ BUY AGAIN / REORDER ] | REORDERING NOTICE:                                        |
| +-----------------------------------------+ | Clicking "Buy Again" re-stages available lines into your   |
| | Order #SO-2026-0744     Date: 28-Oct-2026| | active cart with real-time price and stock validation.  |
| | 1 Item   •  Total: $185.50 USD          | +-----------------------------------------------------------+
| | Status: [ DELIVERED ]                   | QUICK ACTIONS:                                            |
| | [ View Details ]  [ BUY AGAIN / REORDER ] | [ Browse Storefront Catalogue ]                           |
| +-----------------------------------------+ | [ Manage Delivery Address Master ]                       |
+---------------------------------------------------------------------------------------------------------+
```

---

### View 7: Company Customer Dashboard (5 Groups) (`/customer-portal/company/dashboard`)

```
+---------------------------------------------------------------------------------------------------------+
| [LOGO] Apex B2B Portal | Acme Manufacturing Ltd   Active Seller: [ Alpha Tools Ltd v ] [Sign Out]      |
+---------------------------------------------------------------------------------------------------------+
| SIDEBAR NAVIGATION (5 GROUPS) | DASHBOARD OVERVIEW & KEY PERFORMANCE INDICATORS                         |
|                               +-------------------+ +-------------------+ +-------------------+        |
| > 1. DASHBOARD                | PENDING QUOTES    | OFFERS TO ACCEPT  | ACTIVE ORDERS     |        |
|   Company Dashboard           | 3 Requests        | 2 Awaiting Signoff| 8 in Fulfillment  |        |
|                               | Total: $48,200    | Total: $14,900    | Total: $124,500   |        |
| > 2. PURCHASING               +-------------------+ +-------------------+ +-------------------+        |
|   Product Catalogue           | RECENT B2B TRANSACTIONS                                                 |
|   Shopping Cart (4)           | Search: [ Filter transactions... ]                                      |
|   Request a Quotation         | +--------------+------------+--------------------+------------+--------+ |
|   Quotation Requests          | | Reference #  | Type       | Description        | Amount     | Status | |
|   Quotations to Accept (2)    | +--------------+------------+--------------------+------------+--------+ |
|   Direct Sales Orders         | | SQ-2026-0042 | Quotation  | CNC Tooling Batch  | $14,900.00 | [OFFER]| |
|                               | | SO-2026-0891 | Direct SO  | Hydraulic Valves   | $6,200.00  | [PACK] | |
| > 3. ORDERS & FULFILMENT      | | SQ-2026-0038 | Quotation  | Special Fasteners  | $8,400.00  | [ACC]  | |
|   Sales Orders                +--------------+------------+--------------------+------------+--------+ |
|   Dispatch Tracking           | ACTIVE SHIPMENTS & TRACKING                                             |
|                               | Waybill: DHL-EXP-88912 • Carrier: DHL Express • Destination: Dock 4     |
| > 4. FINANCE                  | Current Milestone: [ In Transit - Out for Delivery Today ]              |
|   Invoices & Payments         +-------------------------------------------------------------------------+
|                               | SHORTCUTS:                                                              |
| > 5. COMPANY ADMINISTRATION   | [ Prepare New Quotation Request ]    [ Place Direct Contract Order ]    |
|   Company Profile             +-------------------------------------------------------------------------+
|   Authorized Users            |                                                                         |
|   Delivery Locations (Address)|                                                                         |
|   Seller Relationships        |                                                                         |
+-------------------------------+-------------------------------------------------------------------------+
```

---

### View 8: Authenticated Shopping Cart View (`/storefront/cart`)

```
+---------------------------------------------------------------------------------------------------------+
| [Header Brand Bar]                                  Active Account: Acme Manufacturing Ltd  [Sign Out]  |
+---------------------------------------------------------------------------------------------------------+
| Breadcrumb: Storefront > Shopping Cart                                                                  |
| Page Header: "Review Your Staged Items" (Active Selling Company: Alpha Precision Tools Ltd)             |
+---------------------------------------------------------------------------------------------------------+
| +-----------------------------------------------------------------------------------------------------+ |
| | Product Description        | SKU         | UOM | Packaging | Unit Price | Qty   | Subtotal  | Action| |
| +----------------------------+-------------+-----+-----------+------------+-------+-----------+-------+ |
| | CNC Milling Spindle        | CNC-SP-001  | EA  | Single Box| $1,250.00  | [ 2 ] | $2,500.00 | [Del] | |
| | Titanium Fasteners (100pk) | FAST-TI-100 | PK  | Pack 100  | $185.50    | [ 5 ] | $927.50   | [Del] | |
| | Pressure Regulator 350 Bar | HYD-VAL-350 | EA  | Single Box| $620.00    | [ 4 ] | $2,480.00 | [Del] | |
| +-----------------------------------------------------------------------------------------------------+ |
| [ Clear Cart ]  [ Continue Shopping ]                                                                   |
+-------------------------------------------------------------------------+-------------------------------+
| PURCHASING PATH SELECTION:                                              | COMMERCIAL SUMMARY            |
|                                                                         | Lines Subtotal:   $5,907.50   |
| [ Path 1: Proceed to Retail Checkout ]                                  | Estimated Tax:      $472.60   |
| (Select saved address from Address Master and submit Draft order)       | ----------------------------- |
|                                                                         | Grand Total:      $6,380.10   |
| [ Path 2: Submit as B2B Quotation Request ] (Recommended for Companies) +-------------------------------+
| (Propose custom pricing, volume discounts, and project terms)           | STICKY ACTION BUTTONS:        |
|                                                                         | [ SUBMIT QUOTATION REQUEST ]  |
| [ Path 3: Place Direct Contract Order ]                                 | [ PROCEED TO RETAIL CHECKOUT ]|
| (Available for approved corporate accounts with assigned Price Cards)   +-------------------------------+
+---------------------------------------------------------------------------------------------------------+
```

---

### View 9: Retail Checkout View (`/customer-portal/retail/checkout`)

```
+---------------------------------------------------------------------------------------------------------+
| [Header Brand Bar]                                                          Customer: John Doe (Retail) |
+---------------------------------------------------------------------------------------------------------+
| Breadcrumb: Cart > Retail Checkout                                                                      |
| Page Header: "Confirm Delivery Address & Submit Order"                                                  |
+-------------------------------------------------------------+-------------------------------------------+
| 1. DELIVERY ADDRESS (Customer Address Master Selection)     | ORDER SUMMARY (3 Items)                   |
| Select saved delivery address:                              | - 2x CNC Milling Spindle       $2,500.00  |
| (•) [Default] 14 Oak Street, London, W1A 1AA, UK            | - 5x Titanium Fasteners Pack     $927.50  |
| ( ) Office: 22 High Street, Birmingham, B1 1BB, UK          | - 4x Pressure Regulator        $2,480.00  |
| [ + Add New Delivery Address to Master ]                    | ----------------------------------------- |
|                                                             | Subtotal:                      $5,907.50  |
| 2. DELIVERY INSTRUCTIONS & PREFERRED DATE                   | Sales Tax (VAT):                 $472.60  |
| Requested Delivery Date: [ 28-Nov-2026 v ]                  | Shipping:                          TBD    |
| Delivery Notes: [ Leave package with building security ]    | ----------------------------------------- |
|                                                             | Total Amount Due:              $6,380.10  |
| 3. COMMERCIAL TERMS NOTICE                                  |                                           |
| [*] Retail Checkout: Submitting creates an official Sales   | STICKY ACTION CARD:                       |
|     Order in Draft status. Terms and invoice payment follow | [ CONFIRM & SUBMIT SALES ORDER ]          |
|     backend on-account rules. No credit card is captured.   | [ Return to Shopping Cart ]               |
+-------------------------------------------------------------+-------------------------------------------+
```

---

### View 10: Company Quotation Request View (`/customer-portal/company/purchasing/quotations/request`)

```
+---------------------------------------------------------------------------------------------------------+
| [B2B Shell Header]                                        Acme Manufacturing Ltd • Contact: S. Jenkins  |
+---------------------------------------------------------------------------------------------------------+
| Breadcrumb: Purchasing > Request a Quotation                                                            |
| Page Header: "Prepare Formal B2B Sales Quotation Request"                                               |
+---------------------------------------------------------------------------------------------------------+
| COMMERCIAL CONTEXT:                                                                                     |
| Selling Company*: [ Alpha Precision Tools Ltd v ]    Branch*: [ Main Production Facility v ]            |
| Delivery Destination*: [ Warehouse Dock 4, Manchester Facility (Customer Address Master) v ]            |
| Required By Date*: [ 15-Dec-2026 v ]                  Customer Project Code: [ PRJ-ACME-2026-Q4 ]        |
+---------------------------------------------------------------------------------------------------------+
| REQUESTED ITEMS & TARGET PRICING:                                                                       |
| +-----------------------------+-------+-----+----------+------------------+---------------------------+ |
| | Product Item                | UOM   | Qty | List Prc | Target Price/Unit| Customer Remarks          | |
| +-----------------------------+-------+-----+----------+------------------+---------------------------+ |
| | CNC Milling Spindle         | EA    | 10  | $1,250.00| [ $1,100.00    ] | Volume discount for 10 un | |
| | Pressure Regulator 350 Bar  | EA    | 25  | $620.00  | [ $550.00      ] | Project batch requirement | |
| +-----------------------------+-------+-----+----------+------------------+---------------------------+ |
| [ + Add Another Product Item ]  [ Clone from Past Order ]  [ Clone from Past Quotation ]                |
+---------------------------------------------------------------------------------------------------------+
| COMMERCIAL REMARKS & ATTACHMENTS:                                                                       |
| General Inquiry Notes: [ Seeking commercial terms for annual replenishment contract. ]                  |
| Technical Spec Upload: [ Upload Drawing / RFQ Document ] (Optional PDF/DWG)                             |
+---------------------------------------------------------------------------------------------------------+
| STICKY ACTION BAR:                                                                                      |
| Target Quotation Value: $24,750.00 USD                [ CANCEL ]  [ SUBMIT QUOTATION REQUEST ]          |
+---------------------------------------------------------------------------------------------------------+
```

---

### View 11: Company Quotation Review & Acceptance View (`/customer-portal/company/purchasing/quotations/:id`)

```
+---------------------------------------------------------------------------------------------------------+
| [B2B Shell Header]                                        Acme Manufacturing Ltd • Contact: S. Jenkins  |
+---------------------------------------------------------------------------------------------------------+
| Breadcrumb: Purchasing > Quotations > SQ-2026-0042                                                      |
| Page Header: "Sales Quotation Review: SQ-2026-0042"           Status: [ AWAITING CUSTOMER ACCEPTANCE ]  |
+-------------------------------------------------------------+-------------------------------------------+
| PROPOSAL DETAILS                                            | FORMAL QUOTATION DOCUMENT PREVIEW         |
| Issuing Company:  Alpha Precision Tools Ltd                 | +---------------------------------------+ |
| Issuing Branch:   Main Production Facility                  | | [ PDF VIEWER EMBED ]                  | |
| Quotation Date:   18-Nov-2026                               | | Formal Proposal Document               | |
| Valid Until*:     05-Dec-2026 (14 Days Remaining)           | | Document Reference: SQ-2026-0042.pdf  | |
| Delivery Address: Dock 4, Manchester Facility               | | [ Open PDF in New Tab ] [ Download ]  | |
| Payment Terms:    30 Days Net                               | +---------------------------------------+ |
+-------------------------------------------------------------+                                           |
| OFFERED COMMERCIAL LINE ITEMS:                                                                          |
| +-----------------------------+-----+-----+-----------+----------+-----------+------------------------+ |
| | Item Description            | UOM | Qty | Offrd Prc | Discount | Line Total| Seller Notes           | |
| +-----------------------------+-----+-----+-----------+----------+-----------+------------------------+ |
| | CNC Milling Spindle         | EA  | 10  | $1,150.00 | 8.00%    | $10,580.00| Approved bulk price    | |
| | Pressure Regulator 350 Bar  | EA  | 25  | $570.00   | 8.06%    | $13,100.00| Special batch run      | |
| +-----------------------------+-----+-----+-----------+----------+-----------+------------------------+ |
| Commercial Subtotal: $23,680.00 | Tax (VAT 8%): $1,894.40 | Final Offered Total: $25,574.40 USD         |
+---------------------------------------------------------------------------------------------------------+
| MANDATORY ACCEPTANCE BUSINESS RULE NOTICE:                                                              |
| [*] Accepting converts this quotation atomically into a Sales Order (Draft) AND updates/creates your    |
|     contract Price Card lines (Scenarios A-D) with agreed commercial terms. Original quote is immutable |
+---------------------------------------------------------------------------------------------------------+
| CUSTOMER DECISION ACTION BAR:                                                                           |
| Delivery Address Confirmation*: [ Dock 4, Manchester Facility (Address Master) v ]                      |
| Customer PO Ref: [ PO-ACME-991204       ]  Customer Remarks: [ Approved by Board ]                      |
|                                                                                                         |
| [ DECLINE QUOTATION ] (Prompts Reason Modal)                [ ACCEPT QUOTATION & CONVERT TO ORDER ]     |
+---------------------------------------------------------------------------------------------------------+
```

---

### View 12: Direct Company Order View (`/customer-portal/company/purchasing/direct-orders/new`)

```
+---------------------------------------------------------------------------------------------------------+
| [B2B Shell Header]                                        Acme Manufacturing Ltd • Contact: S. Jenkins  |
+---------------------------------------------------------------------------------------------------------+
| Breadcrumb: Purchasing > Direct Sales Orders > New Contract Order                                       |
| Page Header: "Place Direct Sales Order from Assigned Price Card"                                        |
+---------------------------------------------------------------------------------------------------------+
| CONTRACT PRICING CONTEXT:                                                                               |
| Active Selling Company: [ Alpha Precision Tools Ltd v ]                                                 |
| Assigned Contract Price Card: "Acme Corporate Master Tier 1 (PC-ACME-01)" (Valid: Active)               |
+---------------------------------------------------------------------------------------------------------+
| CONTRACT PRODUCT LINE SELECTION:                                                                        |
| +-----------------------------+---------+-----+-----------+-------+------------+-----------+----------+ |
| | Contract Item Description   | SKU     | UOM | Min Order | Stock | Contract Prc| Order Qty | Subtotal | |
| +-----------------------------+---------+-----+-----------+-------+------------+-----------+----------+ |
| | Titanium Hex Pack (100pk)   | FAST-TI | PK  | 5 packs   | 850 pk| $165.00    | [  10  ]  | $1,650.00| |
| | CNC Precision Bushing 50mm  | CNC-BSH | EA  | 20 units  | 400 ea| $42.00     | [  50  ]  | $2,100.00| |
| +-----------------------------+---------+-----+-----------+-------+------------+-----------+----------+ |
| [ + Select Another Contract Line ]                                                                      |
+---------------------------------------------------------------------------------------------------------+
| ORDER SPECIFICS & DELIVERY ADDRESS MASTER:                                                              |
| Customer PO Number*: [ PO-ACME-2026-881   ]    Requested Delivery Date*: [ 10-Dec-2026 v ]              |
| Delivery Address*:   [ Warehouse Dock 4, Manchester Facility (Customer Address Master) v ]              |
| Order Remarks:       [ Priority production replenishment ]                                              |
+---------------------------------------------------------------------------------------------------------+
| STICKY ACTION BAR:                                                                                      |
| Order Subtotal: $3,750.00 | Tax: $300.00 | Total: $4,050.00 USD     [ CANCEL ]  [ SUBMIT DIRECT ORDER ] |
+---------------------------------------------------------------------------------------------------------+
```

---

### View 13: Customer Order Details & Tracking View (`/customer-portal/company/orders/:id`)

```
+---------------------------------------------------------------------------------------------------------+
| [B2B Shell Header]                                        Acme Manufacturing Ltd • Order #SO-2026-0891  |
+---------------------------------------------------------------------------------------------------------+
| Breadcrumb: Orders & Fulfilment > Orders > SO-2026-0891                                                 |
| Page Header: "Sales Order Details: SO-2026-0891"                       Status: [ IN WAREHOUSE PACKING ] |
+---------------------------------------------------------------------------------------------------------+
| 8-STAGE FULFILLMENT TRACKER:                                                                            |
| [1. Draft] === [2. Submitted] === [3. Confirmed] === [*4. Picking] === [*5. Packing] --- [6. Dispatched] --- [7. In Transit] --- [8. Delivered] |
| Current Milestone: Goods picked from bin WH-A-04; currently in protective packaging at Dock 2.          |
+-------------------------------------------------------------+-------------------------------------------+
| ORDER SUMMARY                                               | SHIPPING & CARRIER LOGISTICS              |
| Selling Company:  Alpha Precision Tools Ltd                 | Carrier:          DHL Express Industrial  |
| Order Date:       19-Nov-2026                               | Tracking / Waybill: DHL-EXP-88912         |
| Customer PO Ref:  PO-ACME-2026-881                          | Estimated Delivery: 23-Nov-2026           |
| Delivery Address: Dock 4, Manchester (Snapshot Preserved)   | Delivery Dock:    Dock 4, Manchester      |
+-------------------------------------------------------------+-------------------------------------------+
| ORDERED LINE ITEMS:                                                                                     |
| +--------------------------------------+-----+-----+-----------+----------+-----------+---------------+ |
| | Product Description                  | SKU | UOM | Qty       | Unit Prc | Tax Rate  | Line Total    | |
| +--------------------------------------+-----+-----+-----------+----------+-----------+---------------+ |
| | Titanium Hex Pack (100pk)            | FAST| PK  | 10 packs  | $165.00  | 8.00%     | $1,782.00     | |
| | CNC Precision Bushing 50mm           | CNC | EA  | 50 units  | $42.00   | 8.00%     | $2,268.00     | |
| +--------------------------------------+-----+-----+-----------+----------+-----------+---------------+ |
| Order Net Total: $3,750.00 | Total VAT: $300.00 | Gross Total Amount: $4,050.00 USD                     |
+---------------------------------------------------------------------------------------------------------+
| POST-ORDER ACTIONS:                                                                                     |
| [ Download Order Acknowledgment PDF ]  [ Track Dispatch Live ]  [ REORDER / BUY AGAIN ]                 |
+---------------------------------------------------------------------------------------------------------+
```
