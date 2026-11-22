# ApexERP — E-commerce UI/UX Design Specification & Wireframes

**Document Version:** 1.4.0  
**Status:** Architecture & Requirements Specification — Tenant Landing Page & Dynamic Storefront Completed (M1, M2), Customer Registration & Workspaces Active (M3)  
**Date:** 2026-11-22  
**Target Repository:** `ApexERP` (`Frontend/Web`)  
**Preceding Milestones:**  
- Milestone 1 — Tenant Subdomain Public Landing Page (Completed & Verified)  
- Milestone 2 — Dynamic E-commerce Storefront & Product Catalogue (Completed & Verified)  
**Active Milestone:** Milestone 3 — Customer Registration & Retail/Organisation Workspaces (Phase E4)  
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

### 1.2 Design Principle: One Consistent ApexERP Platform Design with Individual Tenant Branding `[Agreed Business Rule]`

The public landing page for each tenant subdomain (e.g. `abc.apexerp.com`, `nova.192.168.1.65.nip.io:4200`, `retail.localhost:4200`) must strictly adhere to the visual design system, layout hierarchy, and ergonomic conventions of the main ApexERP platform landing page.

#### Comparative Inspection & Defect Audit (Platform vs. Current Tenant Landing)

The design specification is benchmarked directly against active live screens:

1. **ApexERP Platform Landing Page (Image 1 / Benchmark Reference):**
   - **Header Layout:** Sticky surface header with company logo and SaaS brand title on the left, pill navigation in the available middle area (`Platform`, `Business Directory`), and action utilities on the right (Theme toggle, `Sign In` outlined, `View Plans` filled blue button).
   - **Hero Styling:** Luminous bottom-up radial blue gradient transition with a subtle fading geometric grid overlay; pill badge with sparkle icon; high-contrast headline with `--erp-primary` accent highlight; dual CTA buttons; right visual preview card simulating a macOS 3-dot window frame with live operational metrics.
   - **Capability Grid:** Clean, high-density white surface `.erp-card` grid with tinted rounded icon containers, bold titles, and structured descriptions ("Everything Your Business Needs").
   - **Footer:** Structured corporate footer with brand column, grouped navigation links, and legal notice.

2. **Current Tenant Subdomain Landing Page (Image 2 / Defect Analysis):**
   - **Header Space Waste:** In tenant mode, the middle navigation space is occupied by a static, non-interactive badge (`Dedicated Tenant Portal`), leaving the majority of the header completely blank and unused.
   - **Irrelevant Action Buttons:** The header right displays `View Plans`, which directs visitors to SaaS subscription plans—wholly irrelevant for a tenant's customer or supplier.
   - **Sparse & Incomplete Hero:** Displays a minimal `ENTERPRISE WORKSPACE` badge, tenant name, an unstyled or unlabelled button, and a basic tenant metadata card (`nova`, `Single Company`, `Disabled`).
   - **Missing Page Content:** Below the hero, the body is completely blank white space. There are no featured products, no categories, no company description, no services, and no supplier onboarding.
   - **Mismatched Footer:** Displays the SaaS platform footer (`ApexERP Manufacturing Cloud`, `Subscription Plans`, `Platform Admin Console`, `© ApexERP Inc.`) rather than the tenant's own corporate profile, contact details, and address.

#### Prescribed Architectural Rule
**One consistent ApexERP platform design, with individual tenant branding and business content.**
The tenant subdomain landing page preserves the exact visual style, typography, colours, spacing, buttons, and navigation conventions of the platform, while substituting SaaS platform marketing with authoritative tenant corporate data, product catalogues, business services, supplier registration, and contact information.

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

### View 1: Tenant Subdomain Public Landing Page (`/` on Tenant Subdomains) `[Agreed Business Rule]`

This view governs the public front door for every tenant subdomain on the ApexERP platform (e.g. `abc.apexerp.com`, `xyz.apexerp.com`, `nova.192.168.1.65.nip.io:4200`, `retail.localhost:4200`). It strictly implements the core principle: **One consistent ApexERP platform design, with individual tenant branding and business content.**

```
+==================================================================================================================================================+
|  HEADER (Sticky 64px, Surface Background, 1px Border Bottom)                                                                                     |
|  +------------------------------+  +-------------------------------------------------------------------------+  +------------------------------+  |
|  | [LOGO] Nova Manufacturing    |  |  ( ) Home   ( ) Products   ( ) About Us   ( ) Services   ( ) Suppliers  |  | [Theme] [ Sign In ] [Register] |  |
|  |        Discrete Manufacturing|  |  ( ) Contact Us                                                         |  |                              |  |
|  +------------------------------+  +-------------------------------------------------------------------------+  +------------------------------+  |
+==================================================================================================================================================+
|                                                                                                                                                  |
|  SECTION 1: HERO & PROMOTIONAL BANNER                                                                                                            |
|  Background: Luminous bottom-up radial blue gradient + subtle geometric 36px fading grid overlay                                                |
|  +------------------------------------------------------------------------------------+  +----------------------------------------------------+  |
|  | [pi-building] VERIFIED ENTERPRISE • DISCRETE MANUFACTURING                         |  | [o][o][o]  Nova Operations Hub                     |  |
|  |                                                                                    |  | -------------------------------------------------- |  |
|  | Precision Engineered Components.                                                   |  | MULTI-FACILITY CAPACITY                            |  |
|  | Delivered with Guaranteed Quality.                                                 |  | 2 Manufacturing Plants • Active Live               |  |
|  |                                                                                    |  |                                                    |  |
|  | Delivering high-tolerance CNC parts, hydraulic assemblies, and industrial raw      |  | QUALITY ASSURANCE                                  |  |
|  | materials with full batch traceability and ISO 9001:2015 certification.            |  | ISO 9001:2015 & Batch Traceable                     |  |
|  |                                                                                    |  |                                                    |  |
|  | [ Explore Products (pi-compass) ]    [ Become a Supplier (pi-truck) ]              |  | SUPPLY CHAIN FULFILLMENT                           |  |
|  |                                                                                    |  | Direct B2B & On-Account Supply                     |  |
|  +------------------------------------------------------------------------------------+  +----------------------------------------------------+  |
|                                                                                                                                                  |
+--------------------------------------------------------------------------------------------------------------------------------------------------+
|                                                                                                                                                  |
|  SECTION 2: COMPANY INTRODUCTION ("ABOUT US")                                                                                                    |
|  +--------------------------------------------------------------------+  +--------------------------------------------------------------------+  |
|  | Who We Are: Engineering Excellence Since 1998                      |  | Corporate Milestones & Capability Metrics                          |  |
|  | Nova Manufacturing is an advanced engineering and industrial parts |  | +--------------------+ +--------------------+                      |  |
|  | producer operating multi-site facilities. We specialize in         |  | | 25+ Years          | | 1,400+ Active      |                      |  |
|  | rapid prototyping, high-volume production, and reliable supply     |  | | Engineering Exp    | | Product SKUs       |                      |  |
|  | chain distribution across retail and enterprise commercial clients.|  | +--------------------+ +--------------------+                      |  |
|  |                                                                    |  | +--------------------+ +--------------------+                      |  |
|  | Our integrated production management and inventory infrastructure  |  | | 99.4% On-Time      | | 100% Certified     |                      |  |
|  | ensures full batch traceability from raw material intake to final  |  | | Dispatch Rate      | | Material Quality   |                      |  |
|  | customer delivery.                                                 |  | +--------------------+ +--------------------+                      |  |
|  +--------------------------------------------------------------------+  +--------------------------------------------------------------------+  |
|                                                                                                                                                  |
+--------------------------------------------------------------------------------------------------------------------------------------------------+
|                                                                                                                                                  |
|  SECTION 3: FEATURED PRODUCTS & PRODUCT CATEGORIES                                                                                               |
|  Title: "Our Product Categories" • Subtitle: "Browse certified components and materials manufactured to rigid standards."                       |
|  +--------------------------+  +--------------------------+  +--------------------------+  +--------------------------+                          |
|  | [pi-cog]                 |  | [pi-wrench]              |  | [pi-box]                 |  | [pi-sliders-h]           |                          |
|  | CNC Precision Components |  | Hydraulic Valves & Flow  |  | Fasteners & Hardware     |  | Electrical & Control     |                          |
|  | 420+ Items Available     |  | 180+ Items Available     |  | 1,200+ Items Available   |  | 95+ Items Available      |                          |
|  +--------------------------+  +--------------------------+  +--------------------------+  +--------------------------+                          |
|                                                                                                                                                  |
|  Title: "Featured Products" • Subtitle: "Popular items available for immediate order or contract quotation."                                    |
|  +--------------------------+  +--------------------------+  +--------------------------+  +--------------------------+                          |
|  | [Product Image]          |  | [Product Image]          |  | [Product Image]          |  | [Product Image]          |                          |
|  | SKU: CNC-SP-001          |  | SKU: HYD-VAL-350         |  | SKU: FAST-TI-100         |  | SKU: ELEC-CTRL-12        |                          |
|  | High-Speed Spindle 24k   |  | Pressure Regulator Valve |  | Titanium Hex Bolts (Pack)|  | Digital Flow Sensor      |                          |
|  | $1,250.00 / EA           |  | $620.00 / EA             |  | $185.50 / PK             |  | $440.00 / EA             |                          |
|  | [ IN STOCK ]             |  | [ MADE TO ORDER ]        |  | [ IN STOCK ]             |  | [ IN STOCK ]             |                          |
|  | [ View Details ]         |  | [ View Details ]         |  | [ View Details ]         |  | [ View Details ]         |                          |
|  +--------------------------+  +--------------------------+  +--------------------------+  +--------------------------+                          |
|  [ View Entire Product Catalogue -> ]                                                                                                            |
|                                                                                                                                                  |
+--------------------------------------------------------------------------------------------------------------------------------------------------+
|                                                                                                                                                  |
|  SECTION 4: BUSINESS SERVICES & CORPORATE CAPABILITIES                                                                                           |
|  Title: "Comprehensive Business Capabilities" • Subtitle: "Supporting your business with full-lifecycle manufacturing and supply services."     |
|  +------------------------------------+  +------------------------------------+  +------------------------------------+                          |
|  | [pi-sliders-v] Custom Fabrication  |  | [pi-truck] Logistics & Fulfillment |  | [pi-verified] Quality Assurance    |                          |
|  | Build-to-print discrete components |  | Multi-warehouse inventory with     |  | Mill certs, CMM dimensional reports|                          |
|  | and assemblies with CNC precision. |  | scheduled batch delivery options.  |  | and full lot traceability.         |                          |
|  +------------------------------------+  +------------------------------------+  +------------------------------------+                          |
|  +------------------------------------+  +------------------------------------+  +------------------------------------+                          |
|  | [pi-credit-card] Trade Accounts    |  | [pi-sync] Contract Volume Pricing  |  | [pi-shield] Dedicated Support      |                          |
|  | Approved 30-day commercial terms   |  | Tiered price agreements bound to   |  | Direct technical sales and customer|                          |
|  | for corporate purchasing partners. |  | your assigned Sales Price Cards.   |  | account management teams.          |                          |
|  +------------------------------------+  +------------------------------------+  +------------------------------------+                          |
|                                                                                                                                                  |
+--------------------------------------------------------------------------------------------------------------------------------------------------+
|                                                                                                                                                  |
|  SECTION 5: SUPPLIER REGISTRATION & ONBOARDING PORTAL                                                                                            |
|  +--------------------------------------------------------------------------------------------------------------------------------------------+  |
|  | Partner with Us — Trade Vendor & Supplier Network                                                                                          |  |
|  | We actively partner with certified raw material producers, specialized finishing processors, and logistics carriers.                      |  |
|  |                                                                                                                                            |  |
|  | [ Submit Active RFQ Quotation (/public/rfq-submit) ]        [ Register as an Approved Supplier (Vendor Form) ]                             |  |
|  +--------------------------------------------------------------------------------------------------------------------------------------------+  |
|                                                                                                                                                  |
+--------------------------------------------------------------------------------------------------------------------------------------------------+
|                                                                                                                                                  |
|  SECTION 6: CONTACT INFORMATION & CORPORATE HEAD OFFICE                                                                                          |
|  +------------------------------------+  +------------------------------------+  +------------------------------------+                          |
|  | [pi-map-marker] Registered Office  |  | [pi-phone] Inquiries & Sales Desk  |  | [pi-clock] Operating Hours         |                          |
|  | 100 Industrial Parkway, Dock 4     |  | Phone: +44 (0) 161 946 0912        |  | Monday - Friday: 08:00 - 17:30 GMT |                          |
|  | Trafford Park, Manchester M17 1TN  |  | Email: sales@novamfg.co.uk         |  | Warehouse Receiving: 07:00 - 16:00 |                          |
|  | United Kingdom                     |  | Orders: desk@novamfg.co.uk         |  | Weekend: Closed (Emergency on-call)|                          |
|  +------------------------------------+  +------------------------------------+  +------------------------------------+                          |
|                                                                                                                                                  |
+==================================================================================================================================================+
|  FOOTER: TENANT-SPECIFIC CORPORATE FOOTER (Replaces Platform SaaS Footer)                                                                        |
|  +---------------------------------------+  +-------------------------------+  +-------------------------------+  +---------------------------+  |
|  | [LOGO] Nova Manufacturing             |  | Quick Navigation              |  | Commercial Portals            |  | Corporate & Compliance    |  |
|  | Precision Engineering & Distribution. |  | • Home                        |  | • Customer Sign In            |  | • Terms & Conditions      |  |
|  | Dedicated commercial portal.          |  | • Products Catalogue          |  | • Request B2B Account         |  | • Privacy Policy          |  |
|  |                                       |  | • About Us                    |  | • Supplier RFQ Portal         |  | • ISO 9001 Certifications |  |
|  |                                       |  | • Services & Capabilities     |  | • Employee ERP Login          |  | • Company Reg: CRN-984421 |  |
|  |                                       |  | • Contact Information         |  | • Customer Portal             |  | • VAT: GB-123456789       |  |
|  +---------------------------------------+  +-------------------------------+  +-------------------------------+  +---------------------------+  |
|  ----------------------------------------------------------------------------------------------------------------------------------------------- |
|  (c) 2026 Nova Manufacturing Ltd. All rights reserved. Powered by ApexERP Enterprise Cloud.                                                      |
+==================================================================================================================================================+
```

---

#### 4.1 Detailed Header Architecture & Unused Middle Space Utilization

##### 1. Left Brand Area
- **Logo Mark:** Tenant company logo mark (or SVG/PNG uploaded via Company master; fallback to themed `.logo-mark` with `pi-building` or `pi-box`).
- **Brand Title:** Primary corporate legal or trading name (`tenantContext.currentTenant()?.name || tenantContext.subdomain()`).
- **Brand Subtitle/Tag:** Industry sector tag (`tenantContext.currentTenant()?.industryType || 'Corporate Portal'`).

##### 2. Middle Navigation Area (Eliminating Unused Space)
In the existing implementation, the middle area rendered only a static badge (`<div class="tenant-nav-badge"><span class="badge-text"><i class="pi pi-verified"></i> Dedicated Tenant Portal</span></div>`), wasting critical horizontal navigation space. In the approved design, the middle area features a modern, accessible pill navigation bar styled identically to the platform's `.public-nav` (`background: var(--erp-bg-secondary)`, `border-radius: var(--erp-radius-pill)`):
- **Home:** Smooth-scroll to `#home` (or root `/`).
- **Products:** Routes to `/storefront/catalog` or smooth-scrolls to `#products`.
- **About Us:** Smooth-scrolls to `#about`.
- **Services:** Smooth-scrolls to `#services`.
- **Suppliers:** Smooth-scrolls to `#suppliers` (or routes to `/public/rfq-submit`).
- **Contact Us:** Smooth-scrolls to `#contact`.

##### 3. Right Action Area
- **Theme Switcher:** Moon/Sun icon button (`themeService.toggleTheme()`) preserving full light and dark mode compatibility.
- **Sign In Button:** `.nav-btn-link.outlined` linking to `/login`. Serves internal employees, B2B company buyers, and retail customers with unified credentials.
- **Register Button:** `.nav-btn-link.filled` linking to `/register` (customer account self-registration with Retail and Company B2B tabs).
- **Removal of Platform SaaS Buttons:** The `View Plans` button is strictly hidden or replaced on tenant subdomains, as pricing and subscription plans belong exclusively to the ApexERP SaaS platform host.

---

#### 4.2 Page Sections Detailed Specification

##### Section 1: Hero & Promotional Banner
- **Visual Staging:** Sits upon the exact luminous radial gradient (`ellipse 90% 75% at 50% 100%`) with the 36px fading geometric grid overlay defined in `home.component.scss`.
- **Top Badge:** Pill badge with verified enterprise icon (`<i class="pi pi-building"></i>`) displaying the industry type (e.g. `Discrete & Process Manufacturing`).
- **Headline:** High-contrast corporate headline with the tenant's primary brand highlight (`highlight-blue` token class).
- **Subheading:** Multi-line value proposition detailing products, certifications, and operational scope.
- **CTA Actions:**
  - Primary filled button: `Explore Products` (icon: `pi pi-compass`, links to `#products` or `/storefront/catalog`).
  - Secondary outlined button: `Become a Supplier` (icon: `pi pi-truck`, links to `#suppliers` / `/public/rfq-submit`).
- **Right Visual Preview Card:** Reuses the macOS 3-dot window frame (red, yellow, green window controls) styled identically to the platform's "Connected Operations Hub":
  - Title: `[Tenant Name] Operations Hub`
  - Stat 1: Primary production/distribution capacity with live indicator (`green dot`).
  - Stat 2: Quality standard & traceability status (`ISO 9001 & Batch Traceable`).
  - Stat 3: Commercial fulfillment channels (`Direct B2B & On-Account Supply`).

##### Section 2: Company Introduction ("About Us")
- **Container:** `.erp-container` with smooth-scroll ID `#about`.
- **Layout:** 2-column balanced grid (1.2fr / 0.8fr on desktop, stacked on mobile).
- **Left Column:** Corporate narrative detailing history, production capacity, specialized equipment, engineering personnel, and customer service commitments.
- **Right Column:** 4-card metric grid showcasing key operational milestones:
  - Years of Experience / Industry Heritage
  - Active Product SKUs / Materials Available
  - On-Time Dispatch & Fulfillment Metric
  - Certified Quality & Batch Traceability Standard

##### Section 3: Featured Products & Product Categories
- **Container:** `.erp-container` with smooth-scroll ID `#products`.
- **Category Grid:** Reuses the exact visual card layout of the platform's "Everything Your Business Needs" section (`.capability-card.erp-card`):
  - White surface background, 1px subtle border, rounded icon badge, bold category title, item count badge.
  - Clicking any category navigates to `/storefront/catalog?categoryId={id}` with pre-filtered products.
- **Curated Featured Products:** 4-column responsive grid displaying active products retrieved via `GET /api/ecommerce/catalogue/{companyId}/products`:
  - Product thumbnail with fallback placeholder.
  - SKU code and product title.
  - Formatted unit price and currency.
  - Stock availability status badge (`StatusBadgeComponent` with `In Stock` / `Made to Order`).
  - "View Details" button linking to `/storefront/products/:productId`.
- **Catalogue CTA:** "View Entire Product Catalogue" link navigating directly to the full searchable storefront.

##### Section 4: Business Services & Corporate Capabilities
- **Container:** `.erp-container` with smooth-scroll ID `#services`.
- **Layout:** 3-column / 2-row card grid showcasing core B2B services:
  1. *Custom Manufacturing & Fabrication:* CNC milling, lathe turning, tolerances down to $\pm 0.005\text{ mm}$.
  2. *Logistics & Warehousing:* Multi-warehouse holding, scheduled JIT delivery, regional distribution.
  3. *Quality Assurance & Traceability:* Material test reports (MTR), CMM inspection reports, batch tracking.
  4. *Commercial Trade Accounts:* 30-day payment terms and direct credit facilities for approved corporate buyers.
  5. *Contract Volume Pricing:* Customer-specific pricing structures managed via assigned Sales Price Cards.
  6. *Dedicated Technical Support:* In-house engineering consultation and application support.

##### Section 5: Supplier Registration & Onboarding Portal
- **Container:** `.erp-container` with smooth-scroll ID `#suppliers`.
- **Purpose:** Direct engagement channel for trade vendors, material suppliers, packaging partners, and transport carriers.
- **Layout:** High-impact callout card (`.growth-card.erp-card`) featuring:
  - Clear narrative inviting suppliers to join the authorized vendor network.
  - Direct CTA button: `Submit Active RFQ Quotation` linking to `/public/rfq-submit` (preserving tenant context).
  - Direct CTA button: `Register as a Supplier` (initiating vendor qualification / procurement contact).

##### Section 6: Contact Information & Corporate Head Office
- **Container:** `.erp-container` with smooth-scroll ID `#contact`.
- **Layout:** 3-column structured grid consuming authoritative data from Company Master & Address Master:
  - *Card 1 (Registered Office & Address):* Street address, suite/dock number, city, postal code, country.
  - *Card 2 (Direct Inquiries):* General office phone, sales desk email, order fulfillment desk.
  - *Card 3 (Operating Hours):* Business hours, warehouse receiving windows, emergency support lines.

##### Section 7: Tenant-Specific Corporate Footer
- **Layout:** Distinct multi-column corporate footer replacing the SaaS platform footer:
  - *Column 1 (Brand & Mission):* Tenant logo, legal company name, concise capability statement.
  - *Column 2 (Quick Links):* Home, Products, About Us, Services, Suppliers, Contact Us.
  - *Column 3 (Commercial Portals):* Customer Sign In, Request B2B Account, Supplier RFQ Portal, Employee ERP Login.
  - *Column 4 (Corporate Governance):* Legal registrations (CRN, VAT/Tax ID), terms of supply, privacy policy, quality certifications.
  - *Bottom Bar:* `© {currentYear} {Tenant Name}. All rights reserved. Powered by ApexERP Enterprise Cloud.`

---

#### 4.3 Route Mapping & Domain Resolution Matrix

| URL / Host Pattern | Environment | Resolved Mode | Loaded Layout & Component | Navigation Header Links | Primary Action Buttons |
|---|---|---|---|---|---|
| `apexerp.com`, `localhost:4200`, `192.168.1.65:4200` | SaaS Platform Root | Platform Mode (`isTenantDomain() == false`) | `PublicLayoutComponent` &rarr; `HomeComponent` (Platform Marketing View) | `Platform`, `Business Directory` | `Sign In`, `View Plans` |
| `abc.apexerp.com`, `nova.192.168.1.65.nip.io:4200`, `retail.localhost:4200` | Tenant Subdomain | Tenant Mode (`isTenantDomain() == true`) | `PublicLayoutComponent` &rarr; `HomeComponent` (Tenant Corporate View) | `Home`, `Products`, `About Us`, `Services`, `Suppliers`, `Contact Us` | `Sign In`, `Register` |
| `[tenant]/storefront/catalog` | Tenant Subdomain | Public Catalogue | `StorefrontLayout` &rarr; `ProductCatalogComponent` | Full catalogue search, categories, filter pills | `Cart (n)`, `Sign In`, `Register` |
| `[tenant]/public/rfq-submit` | Tenant Subdomain | Supplier RFQ Portal | `PublicRfqSubmitComponent` | Minimal portal header | `Submit Quotation` |
| `[tenant]/login` | Tenant Subdomain | Unified Authentication | `LoginComponent` | Storefront Return link | `Sign In` |
| `[tenant]/register` | Tenant Subdomain | Customer Self-Registration | `RegisterComponent` (with Retail & Company tabs) | Storefront Return link | `Create Account` |

---

#### 4.4 Tenant-Specific Customisation & Isolation Rules

1. **Branding Isolation:** Each tenant displays its own company name, logo mark, industry category, and slogan. Data is loaded dynamically via `TenantContextService` from `GET /Tenant/context` and Company Master (`GET /api/company`).
2. **Approved Brand Theme Tokens:** Tenants may customize primary brand accent tokens within the approved design system palette (`--erp-primary`, `--erp-primary-hover`, `--erp-primary-light`) without breaking Fluent design consistency or readability in dark mode.
3. **No Cross-Tenant Data Leakage:** Product catalogues, categories, company addresses, and supplier links strictly filter by the resolved `TenantId` and active Selling `CompanyId`.
4. **Distinct Public vs. Internal Boundaries:** The public tenant landing page is accessible anonymously (`[AllowAnonymous]`). It does not expose internal ERP menus, employee workflows, financial accounts, or warehouse bins. Employee access requires authentication and navigation to `/app/*` guarded by `workspaceGuard`.

---

#### 4.5 Responsive Layout Specification

| Breakpoint | Header Navigation | Hero Section | Capability & Product Grids | Corporate Footer |
|---|---|---|---|---|
| **Desktop** ($\ge 1024\text{ px}$) | Full horizontal pill bar (`Home`, `Products`, `About Us`, `Services`, `Suppliers`, `Contact Us`). Sign In and Register buttons visible side-by-side. | 2-column grid (1.25fr / 0.75fr); macOS 3-dot visual card positioned adjacent to hero text. | 4-column categories; 4-column featured products; 3-column services. | 4-column link layout with separate brand summary and bottom legal bar. |
| **Tablet** ($768\text{ px} - 1023\text{ px}$) | Header collapses into compact pill tabs or horizontal scroll bar. Buttons remain accessible. | Stacked or compact 2-column; visual card adapts to 100% width below text. | 2-column categories; 2-column products; 2-column services. | 2-column link layout with full-width brand summary. |
| **Mobile** ($< 768\text{ px}$) | Header transforms into a mobile hamburger menu. Logo and Sign In remain in top bar. | Single-column stacked layout; action buttons expand to 100% width with touch-friendly tap targets ($44\text{ px}$). | Single-column swipeable cards or vertical list stack. | Single-column stacked footer with expandable link sections. |

---

#### 4.6 Implementation Plan for Frontend Engineers

| Step | Target File | Specific Implementation Responsibility |
|---|---|---|
| **1. Header Refactor** | `Frontend/Web/src/app/layout/public-layout/public-layout.component.html`<br>`public-layout.component.ts`<br>`public-layout.component.scss` | - Replace static `.tenant-nav-badge` with responsive navigation links (`Home`, `Products`, `About Us`, `Services`, `Suppliers`, `Contact Us`).<br>- Replace `View Plans` button on tenant subdomains with primary `Register` button.<br>- Implement smooth-scroll anchor navigation and route coordination. |
| **2. Landing Page Component** | `Frontend/Web/src/app/features/saas/public/home/home.component.html`<br>`home.component.ts`<br>`home.component.scss` | - Expand `@if (tenantContext.isTenantDomain())` from bare placeholder into the 7 standard corporate sections.<br>- Implement Section 1 (Hero with 3-dot window operational card).<br>- Implement Section 2 (About Us with milestone stat cards).<br>- Implement Section 3 (Category cards & featured products connected to catalogue service).<br>- Implement Section 4 (Business services grid).<br>- Implement Section 5 (Supplier registration callout with `/public/rfq-submit` link).<br>- Implement Section 6 (Contact details consuming Address Master). |
| **3. Footer Specialization** | `Frontend/Web/src/app/layout/public-layout/public-layout.component.html`<br>`public-layout.component.scss` | - Conditionally render Tenant-Specific Corporate Footer when `tenantContext.isTenantDomain() == true`, reserving the SaaS platform footer for platform root domains.<br>- Populate dynamic company name, contact info, and commercial portal links. |
| **4. Integration & Testing** | Unit & E2E Test Suite | - Verify subdomain resolution (`abc.apexerp.com`, `nova.192.168.1.65.nip.io:4200`, `localhost:4200`).<br>- Verify theme toggling (Light/Dark mode) across all 7 sections.<br>- Verify responsive viewport behavior across Desktop, Tablet, and Mobile. |

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

---

### View 14: Customer Self-Registration View (`/register` on Tenant Domain or `/customer-register`)

```
+---------------------------------------------------------------------------------------------------------+
| [Header] Logo: Nova Dynamics Ltd • Tenant Portal                [ Theme Toggle ]  [ Sign In ]           |
+---------------------------------------------------------------------------------------------------------+
|                                                                                                         |
|                                CREATE YOUR CUSTOMER ACCOUNT                                             |
|              Join Nova Dynamics Ltd's commercial purchasing and supply chain portal                     |
|                                                                                                         |
|       +---------------------------------------------+---------------------------------------------+     |
|       |  [O] Individual / Retail Customer           |  [ ] Organisation / Company (B2B) Customer  |     |
|       +---------------------------------------------+---------------------------------------------+     |
|                                                                                                         |
|       ACCOUNT INFORMATION:                                                                              |
|       First Name: * [ Jane                ]     Last Name: * [ Doe                 ]                    |
|       Email (Login Username): * [ jane.doe@example.com                                  ]               |
|       Mobile Number: * [ +94 77 123 4567       ]                                                        |
|                                                                                                         |
|       PRIMARY BILLING & DELIVERY ADDRESS:                                                               |
|       Address Line 1: * [ 123 Commercial Road                                           ]               |
|       Address Line 2:   [ Suite 400                                                     ]               |
|       City: *           [ Colombo                 ]     Postal Code: * [ 00300          ]               |
|       Country: *        [ Sri Lanka               ]                                                     |
|                                                                                                         |
|       SECURITY & CREDENTIALS:                                                                           |
|       Password: * [ ****************            ]     Confirm: * [ ****************             ]       |
|                                                                                                         |
|       [ CANCEL ]                                                  [ CREATE INDIVIDUAL ACCOUNT ]         |
|                                                                                                         |
|       Already have an account? [ Sign In to Customer Portal ]                                           |
|                                                                                                         |
+---------------------------------------------------------------------------------------------------------+
```

---

### View 15: Shared Customer Workspace Foundation (`/customer-portal/*`)

```
+---------------------------------------------------------------------------------------------------------+
| [=] Logo: Nova Dynamics Ltd • Customer Portal       [ Theme ]  [ Cart (0) ]  Signed in: user@corp.com [Out] |
+-----------------------+---------------------------------------------------------------------------------+
| SIDEBAR               | WORKSPACE CONTENT AREA                                                          |
|                       |                                                                                 |
| [Retail Customer]     | Breadcrumb: Customer Portal > Dashboard                                         |
| • Dashboard           |                                                                                 |
| • Product Catalogue   | WELCOME BACK, JANE DOE                                                          |
| • Shopping Cart       | Account Type: Individual Retail Customer • Customer Code: CUST-IND-8812         |
| • My Orders           |                                                                                 |
| • My Addresses        | +-----------------------------------+-----------------------------------+       |
| • My Profile          | | Active Orders: 2 In Progress      | Saved Addresses: 1 Default        |       |
|                       | +-----------------------------------+-----------------------------------+       |
|                       |                                                                                 |
| [Organisation B2B]    | RECENT REQUISITIONS & ORDERS                                                    |
| • Dashboard           | +--------------------+-------------+------------+--------------------+          |
| • Product Catalogue   | | Reference Number   | Date        | Total ($)  | Fulfillment Status |          |
| • Shopping Cart       | +--------------------+-------------+------------+--------------------+          |
| • Quotations          | | SO-2026-0891       | 12-Oct-2026 | $6,420.00  | [ In Production ]  |          |
| • Direct Orders       | | SO-2026-0744       | 28-Sep-2026 | $18,900.00 | [ Shipped ]        |          |
| • Order History       | +--------------------+-------------+------------+--------------------+          |
| • Company Profile     |                                                                                 |
| • Delivery Addresses  | QUICK ACTIONS                                                                   |
|                       | [ Browse Catalogue ]  [ View Cart ]  [ Update Profile ]                         |
+-----------------------+---------------------------------------------------------------------------------+
```

---

### View 16: Corporate Organisation Pending Approval State (`/customer-portal/company/dashboard`)

```
+---------------------------------------------------------------------------------------------------------+
| [=] Logo: Nova Dynamics Ltd • Customer Portal       [ Theme ]  [ Cart (0) ]  Signed in: buyer@corp.com [Out] |
+-----------------------+---------------------------------------------------------------------------------+
| SIDEBAR               | WORKSPACE CONTENT AREA                                                          |
|                       |                                                                                 |
| [Organisation B2B]    | Breadcrumb: Customer Portal > Corporate Dashboard                               |
| • Dashboard (Active)  |                                                                                 |
| • Product Catalogue   | +-----------------------------------------------------------------------------+ |
| • Shopping Cart       | | [!] CORPORATE ACCOUNT PENDING VERIFICATION                                  | |
| • Quotations (Locked) | | Your registration has been matched to Acme Manufacturing Ltd and is currently | |
| • Direct Orders (Lock)| | awaiting confirmation from the tenant administrator or corporate manager.   | |
| • Order History (Lock)| | Features such as Price Card schedules, quotation approvals, and direct      | |
| • Company Profile     | | orders will remain locked until your contact verification is approved.      | |
| • Delivery Addresses  | +-----------------------------------------------------------------------------+ |
|                       |                                                                                 |
|                       | ORGANISATION DETAILS:                                                           |
|                       | Corporate Legal Name: Acme Manufacturing Ltd • Code: CUST-0104                  |
|                       | Tax / VAT Registration: GB-123456789 • Verification Status: [ PENDING APPROVAL ]|
|                       | Contact Representative: Sarah Jenkins (Procurement Director)                    |
|                       |                                                                                 |
|                       | AVAILABLE SELF-SERVICE ACTIONS:                                                 |
|                       | [ Browse Public Catalogue ]  [ Review Company Profile ]  [ Contact Support ]    |
+-----------------------+---------------------------------------------------------------------------------+
```
