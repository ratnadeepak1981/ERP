# ApexERP — Backend API Integration Matrix, Gaps & Architectural Questions

**Document Version:** 1.1.0  
**Status:** Architecture & Requirements Specification — Ready for Implementation  
**Date:** 2026-11-21  
**Target Repository:** `ApexERP` (`Backend/ERP`, `Frontend/Web`)  
**Related Documents:**
- [README.md](README.md) — Documentation index and reading paths
- [00-Ecommerce-Frontend-Master-Plan.md](00-Ecommerce-Frontend-Master-Plan.md) — Master architectural charter and invariants
- [01-Customer-Journey-and-Workspaces.md](01-Customer-Journey-and-Workspaces.md) — Customer journey and routes
- [02-Ecommerce-UI-UX-Specification.md](02-Ecommerce-UI-UX-Specification.md) — Design system and wireframes
- [03-Retail-Customer-Workflow.md](03-Retail-Customer-Workflow.md) — Retail purchasing path and reordering
- [04-Company-Customer-Workflow.md](04-Company-Customer-Workflow.md) — Company B2B purchasing workflows and Price Card maintenance
- [06-Customer-Workspace-Specification.md](06-Customer-Workspace-Specification.md) — Workspace component details
- [07-Implementation-and-Testing-Plan.md](07-Implementation-and-Testing-Plan.md) — Standardized E1–E10 implementation roadmap & QA tests

---

## 1. Executive Summary & Verification Methodology

This document establishes the verified integration contract between the Angular 18+ frontend and the .NET 8 backend. 

### Capability Classification Taxonomy:
Every backend capability investigated in `Backend/ERP/` is categorized into one of four strict normative statuses:
- **Category A: Verified and Implemented:** Confirmed present in the codebase via source-code inspection of entities, services, controllers, or configurations.
- **Category B: Partially Implemented / Requires Modification:** Exists in domain or internal ERP controllers, but requires minor adaptation or enhancement for external customer portal exposure.
- **Category C: Missing Backend Capability:** The capability does not currently exist in the backend and must be addressed as a documented gap or future enhancement.
- **Category D: Requires Business Decision:** Capability where multiple architectural paths exist, requiring explicit stakeholder guidance before code implementation.

---

## 2. Comprehensive Backend Capability Audit Matrix

| Capability / Feature Area | Classification Status | Verified Source References (Controller / Service / Entity) | Evaluation & Frontend Integration Notes |
|---|---|---|---|
| **1. Current Catalogue Availability & Pricing** | **Category A: Verified and Implemented** | `EcommerceCatalogueController.cs`, `EcommerceProductService.cs`, `PublicEcommerceProductDto.cs` | Exposes published products per Selling Company. Server calculates prices; internal costs, valuation, and vendor margins are scrubbed. |
| **2. Persistent Cart & Session Handling** | **Category A: Verified and Implemented** | `EcommerceCartController.cs`, `EcommerceCartService.cs`, `EcommerceCart.cs` (`Domain/Features/Ecommerce`) | Server-side cart stored in relational DB table `EcommerceCarts`. Supports session header `X-Cart-Session-Id` and authenticated user merging. |
| **3. Anonymous Browsing vs. Authenticated Cart** | **Category A: Verified and Implemented** | `EcommerceCartController.cs`, `auth.guard.ts`, `LoginComponent.ts` | Catalogue and details permit `[AllowAnonymous]`. Adding to cart enforces login; pending actions are restored via `returnUrl` and query flags. |
| **4. Shopping Cart History & Restoration** | **Category C: Missing Backend Capability** | `EcommerceCart.Status` (`Abandoned`, `CheckedOut`) in `EcommerceCart.cs` | **Missing Capability:** While `EcommerceCart` stores historical status, `EcommerceCartService` and controller only query `Active` carts. No endpoint exists to list or restore past carts. Reordering from **Sales Order History** is the supported alternative. |
| **5. Retail Checkout (Creating Draft Sales Order)** | **Category A: Verified and Implemented** | `EcommerceCartController.Checkout`, `EcommerceCartService.CheckoutAsync`, `ISalesOrderService` | Executes Retail Checkout. Generates official `SalesOrder` (`Draft`) linking Customer Address Master. Excludes payment gateways by design. |
| **6. Retail Reorder from Order History (`Buy Again`)** | **Category A: Verified and Implemented** | `CustomerOrderController.GetOrderById`, `CustomerOrderDetailDto.Items`, `EcommerceCartController.AddToCart` | `CustomerOrderDetailDto` supplies full line metadata (`ProductId`, `Quantity`, `UomCode`). Frontend stages lines into active cart; backend re-evaluates current prices and stock. |
| **7. Customer Delivery Address Master & SO Linking** | **Category B: Partially Implemented / Requires Modification** | `CustomerAddress.cs`, `Address.cs`, `SalesOrder.DeliveryAddressId`, `SalesOrder.DeliveryAddressSnapshot` | **Domain Verified:** `SalesOrder` links to `DeliveryAddressId` and preserves snapshot. **Gap:** Customer portal lacks self-service address CRUD endpoint (`/api/portal/customer/addresses`). Requires internal staff permission `ADDRESS.CREATE` currently. |
| **8. Quotation Request & Cloning** | **Category A: Verified and Implemented** | `CustomerQuotationController.SubmitQuotationRequest`, `.../clone-order/{id}`, `.../clone-quotation/{id}` | Allows new requests with customer requested prices, or cloning from prior `SalesOrder` or `SalesQuotation`. |
| **9. Quotation Immutability Enforcement** | **Category A: Verified and Implemented** | `SalesQuotation.cs`, `SalesQuotationService.cs` | Once issued (`Status = Sent` / `Approved`), quotation lines and prices are strictly read-only. Edits throw `InvalidOperationException`. |
| **10. Quotation Acceptance & Price Card Updates** | **Category B: Partially Implemented / Requires Modification** | `CustomerQuotationController.AcceptQuotation`, `SalesQuotationService.AcceptQuotationAndConvertAtomicAsync` | **Atomic Conversion Verified:** Converts quote to `SalesOrder` (`Draft`) and updates existing `PriceCardLine` (Scenarios B, C). **Proposed Enhancement:** Update atomic conversion to resolve/create `PriceCard` via `CustomerSellingCompany` relationship for the quotation's Selling Company (Scenario A & D). |
| **11. Direct Company Order Eligibility & Price Card** | **Category A: Verified and Implemented** | `DirectCustomerOrderController.CreateDirectB2BOrder`, `DirectCustomerOrderService.cs` | Enforces assigned contract Price Card, MOQ validation, and permission `PORTAL_ORDER.SUBMIT`. **Proposed Enhancement:** Resolve contract card from active `CustomerSellingCompany` relationship before falling back to `Customer.DefaultSalesPriceCardId`. |
| **12. Customer Order Tracking & Post-Order Scrubbing** | **Category A: Verified and Implemented** | `CustomerOrderController.GetOrders`, `.../tracking`, `CustomerOrderTrackingDto.cs` | Delivers 8-stage fulfillment tracking, waybills, and carriers. All internal picker IDs, worker notes, and margins are completely scrubbed. |
| **13. Invoice & Payment Balance Information** | **Category A: Verified and Implemented** | `CustomerOrderController.GetInvoices`, `CustomerInvoiceSummaryDto.cs` | Exposes invoice number, due date, billed total, paid amount, and outstanding balance without internal financial ledger overhead. |
| **14. Selling Company Switching & Tenant Scoping** | **Category A: Verified and Implemented** | `CustomerProfileController.GetSellingCompanies`, `.../select`, `CurrentCustomerContext.cs` | Validates customer company eligibility against selling entities in tenant. Activates operational context securely. |
| **15. Backend Permission Enforcement** | **Category A: Verified and Implemented** | `CurrentCustomerContext.cs`, JWT token claims, `CustomerAuthService.cs` | Enforces permissions (`PORTAL_QUOTATION.ACCEPT`, `PORTAL_ORDER.SUBMIT`, `PORTAL_SALES.VIEW`) on API endpoints independently of UI visibility. |
| **16. Customer User Management & Invitations** | **Category C: Missing Backend Capability** | `CustomerRegistrationService.cs`, `CustomerContact.cs` | **Missing Capability:** No customer-facing endpoint exists for primary company admin (`PORTAL_USERS.MANAGE`) to invite, update, or revoke secondary buyer permissions. |
| **17. Tenant Landing Page Content CMS** | **Category C: Missing Backend Capability** | `TenantController.GetCurrentTenantContext` | **Missing Capability:** Backend returns basic flags (`isTenant`, `eCommerceEnabled`). No CMS endpoint exists for custom marketing copy or hero banners. |
| **18. Online Payment Gateway Integration** | **Category D: Requires Business Decision (Deferred)** | None in Phase 1 | Excluded from Phase 1 by stakeholder policy. Retail Checkout operates on-account creating Draft orders. |

---

## 3. Detailed Endpoint Contracts for Customer Integration

### 3.1 Public Storefront & Catalogue Endpoints

#### `GET /api/ecommerce/catalogue/{companyId}/products`
- **Controller:** `EcommerceCatalogueController`
- **Permission:** Anonymous (`[AllowAnonymous]`)
- **Query Parameters:** `searchTerm` (string), `categoryId` (Guid), `brandId` (Guid)
- **Response Model:** `ApiResponse<List<PublicEcommerceProductDto>>`
  ```csharp
  public class PublicEcommerceProductDto
  {
      public Guid ProductId { get; set; }
      public string ProductCode { get; set; }
      public string ProductName { get; set; }
      public string? OnlineDescription { get; set; }
      public int DisplayOrder { get; set; }
      public Guid CategoryId { get; set; }
      public string? CategoryName { get; set; }
      public Guid? BrandId { get; set; }
      public string? BrandName { get; set; }
      public string UnitOfMeasure { get; set; }
      public decimal SellingPrice { get; set; }
      public string Currency { get; set; }
  }
  ```

#### `GET /api/ecommerce/catalogue/{companyId}/products/{productId}`
- **Controller:** `EcommerceCatalogueController`
- **Permission:** Anonymous (`[AllowAnonymous]`)
- **Response Model:** `ApiResponse<PublicEcommerceProductDto>`

---

### 3.2 Authenticated Shopping Cart Endpoints

#### `GET /api/ecommerce/cart?companyId={companyId}`
- **Controller:** `EcommerceCartController`
- **Header:** `X-Cart-Session-Id: <token>`, `Authorization: Bearer <jwt>`
- **Response Model:** `ApiResponse<CartDto>`
  ```csharp
  public class CartDto
  {
      public Guid CartId { get; set; }
      public Guid TenantId { get; set; }
      public Guid CompanyId { get; set; }
      public string CompanyName { get; set; }
      public Guid? CustomerId { get; set; }
      public string? CustomerName { get; set; }
      public string? SessionId { get; set; }
      public EcommerceCartStatus Status { get; set; }
      public string Currency { get; set; }
      public int TotalItems { get; set; }
      public decimal SubTotal { get; set; }
      public decimal TotalDiscount { get; set; }
      public decimal TotalTax { get; set; }
      public decimal GrandTotal { get; set; }
      public List<CartItemDto> Lines { get; set; }
  }
  ```

#### `POST /api/ecommerce/cart/items?companyId={companyId}`
- **Payload:** `AddToCartRequest` (`ProductId`, `Quantity`, `UnitOfMeasureId`, `ProductPackagingId`, `SessionId`)
- **Response:** `ApiResponse<CartDto>`

#### `PUT /api/ecommerce/cart/lines/{lineId}?companyId={companyId}`
- **Payload:** `UpdateCartLineRequest` (`Quantity`, `SessionId`)
- **Response:** `ApiResponse<CartDto>`

#### `DELETE /api/ecommerce/cart/lines/{lineId}?companyId={companyId}`
- **Response:** `ApiResponse<CartDto>`

#### `POST /api/ecommerce/cart/checkout?companyId={companyId}`
- **Payload:** `CheckoutRequest`
  ```csharp
  public class CheckoutRequest
  {
      public string? SessionId { get; set; }
      public string? IdempotencyKey { get; set; }
      public Guid? CustomerId { get; set; }
      public Guid? DeliveryAddressId { get; set; }
      public string? CustomerNotes { get; set; }
      public DateTime? RequestedDeliveryDate { get; set; }
  }
  ```
- **Response:** `ApiResponse<CheckoutResultDto>` (`Success`, `SalesOrderId`, `OrderNumber`, `OrderStatus: Draft`)

---

### 3.3 Customer Authentication & Registration Endpoints

#### `POST /api/portal/auth/register-individual`
- **Payload:** `RegisterIndividualCustomerRequest` (`TenantId`, `FirstName`, `LastName`, `Email`, `MobileNumber`, `AddressLine1`, `City`, `PostalCode`, `Country`, `Password`, `ConfirmPassword`)
- **Response:** `ApiResponse<CustomerRegistrationResult>` (`CustomerId`, `CustomerCode`, `UserId`, `Email`, `CanLoginImmediately: true`)

#### `POST /api/portal/auth/register` (Company Registration)
- **Payload:** `RegisterCompanyCustomerRequest` (`TenantId`, `CompanyName`, `CompanyRegistrationNumber`, `TaxNumber`, `ContactPersonName`, `JobTitle`, `Email`, `MobileNumber`, `Password`, `ConfirmPassword`)
- **Response:** `ApiResponse<CustomerRegistrationResult>`

#### `POST /api/portal/auth/login`
- **Payload:** `CustomerLoginRequest` (`Email`, `Password`)
- **Response:** `ApiResponse<CustomerLoginResponse>`
  - Returns `AccessToken` with claims `portal_type = "CUSTOMER"`, `tenant_id`, `customer_id`, `contact_id`, `roles`, `permissions`.

---

### 3.4 Customer Quotations & Atomic Acceptance Endpoints

#### `POST /api/portal/customer/quotations/request?companyId={companyId}&branchId={branchId}`
- **Payload:** `CreateCustomerQuotationRequest` (`CustomerId`, `CustomerContactId`, `RequiredByDate`, `CustomerRemarks`, `Items`)
- **Response:** `ApiResponse<SalesQuotationDto>`

#### `POST /api/portal/customer/quotations/{id}/accept`
- **Payload:** `AcceptQuotationRequest`
  ```csharp
  public class AcceptQuotationRequest
  {
      public Guid? DeliveryAddressId { get; set; }
      public string? CustomerRemarks { get; set; }
      public string? IdempotencyKey { get; set; }
  }
  ```
- **Response:** `ApiResponse<SalesQuotationDto>` (Marks quote `Accepted` & `Converted`; atomically creates `SalesOrder` in `Draft` and maintains contract Price Card lines).

#### `GET /api/portal/customer/quotations/{id}/pdf`
- **Response:** `FileContentResult` (`application/pdf`)

---

### 3.5 Direct Contract Ordering & Tracking Endpoints

#### `POST /api/portal/customer/orders/direct-b2b`
- **Payload:** `CreateDirectB2BOrderRequest` (`SellingCompanyId`, `BranchId`, `DeliveryAddressId`, `CustomerRemarks`, `IdempotencyKey`, `Items`)
- **Response:** `ApiResponse<SalesOrderDto>` in status `Draft`

#### `GET /api/portal/customer/orders`
- **Query:** `sellingCompanyId` (Guid?), `status` (SalesOrderStatus?)
- **Response:** `ApiResponse<List<CustomerOrderSummaryDto>>`

#### `GET /api/portal/customer/orders/{id}`
- **Response:** `ApiResponse<CustomerOrderDetailDto>` (contains `List<CustomerOrderItemDto> Items` powering Retail Reordering / `Buy Again`)

#### `GET /api/portal/customer/orders/{id}/tracking`
- **Response:** `ApiResponse<CustomerOrderTrackingDto>` (8-stage progress tracker, carrier, waybill, estimated delivery)

---

## 4. Analysis of Backend Gaps & Future Enhancements

The following table details every identified backend gap, why the frontend requires it, whether it blocks Phase 1, and the recommended backend enhancement:

| Gap # | Missing Capability | Why Frontend Needs It | Blocks Phase 1? | Can Be Deferred? | Recommended Backend Enhancement |
|---|---|---|---|---|---|
| **Gap 1** | **Customer Address Master Portal CRUD** | Allows customers to manage multiple delivery addresses and docks in `CustomerAddress` without requiring internal ERP staff permissions (`ADDRESS.CREATE`). | **No** (Workaround: select primary registered address or enter destination in checkout/order payload) | **Yes** (Enhance in Phase 2) | Add `CustomerAddressPortalController` with `GET /api/portal/customer/addresses` and `POST /api/portal/customer/addresses`. |
| **Gap 2** | **Quotation Acceptance: Price Card Creation (Scenario A)** | When customer has no assigned Price Card (`DefaultSalesPriceCardId == null`), quote acceptance skips card creation. | **No** (Existing assigned cards update correctly; backend logic can be added without API breaking changes) | **Must be resolved in backend for full Scenario A** | Enhance `SalesQuotationService.AcceptQuotationAndConvertAtomicAsync` to create and assign a new `PriceCard` when `DefaultSalesPriceCardId == null`. |
| **Gap 3** | **Shopping Cart History / Restore** | Allows customers to inspect abandoned carts and restore items into an active cart. | **No** (Sales Order History reordering `Buy Again` provides full re-purchasing functionality) | **Yes** (Enhance in future phase) | Add `GET /api/ecommerce/cart/history` and `POST /api/ecommerce/cart/{id}/restore`. |
| **Gap 4** | **Customer User Invitation & Management** | Primary company admin cannot invite secondary buyers or delegate permissions directly in the portal. | **No** (Phase 1 supports single-contact corporate registration and internal staff contact assignment) | **Yes** (Enhance in Phase 2) | Implement `POST /api/portal/customer/users/invite` with email dispatch. |
| **Gap 5** | **Tenant Landing Page CMS** | Tenants cannot configure rich hero headlines, marketing copy, or custom promotional banners via API. | **No** (Frontend renders high-quality default template using `TenantContextDto` and Selling Company profile) | **Yes** (Enhance in future CMS phase) | Add `TenantLandingPageController` with `GET/PUT /api/Tenant/landing-page-config`. |
| **Gap 6** | **Batch Add-to-Cart for Reordering** | Frontend must currently execute individual `POST /items` calls for each reordered line item. | **No** (Frontend can loop through selected items sequentially via RxJS `concat` or `forkJoin`) | **Yes** (Optimization) | Add `POST /api/ecommerce/cart/items/batch` accepting `List<AddToCartRequest>`. |
| **Gap 7** | **Customer–Selling Company Relationship & Multi-Company Price Card Scoping** | Corporate customers transacting with multiple Selling Companies in the same tenant require independent contract Price Cards. Currently, `Customer.DefaultSalesPriceCardId` only holds a single default card. | **No** (Single card is shared as default fallback; multi-company partitioning is cleanly introduced via relationship entity) | **Must be provisioned in backend prior to Phase E8** | Add `CustomerSellingCompany` entity (`(TenantId, CustomerId, CompanyId)` with `SalesPriceCardId`), update `SalesQuotationService`, and update `PricingResolutionService`. |

### 4.1 Customer–Selling Company Relationship Architecture `[PROPOSED]`

#### Target Entity Specification:
```csharp
namespace ERP.Domain.Features.MasterData.Customer;

public class CustomerSellingCompany : Auditable, ITenantScopedEntity
{
    public Guid CustomerSellingCompanyId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid CustomerId { get; private set; }
    public Guid CompanyId { get; private set; } // Selling Company
    public Guid? SalesPriceCardId { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTime EffectiveFrom { get; private set; }
    public DateTime? EffectiveTo { get; private set; }
    public decimal? CreditLimit { get; private set; }
    public string? PaymentTerms { get; private set; }
    public Guid? CurrencyId { get; private set; }
}
```

#### EF Core Configuration (`CustomerSellingCompanyConfiguration.cs`):
- Table: `CustomerSellingCompanies`
- Unique index on `(TenantId, CustomerId, CompanyId)`
- Filtered active index on `(TenantId, CustomerId, CompanyId)` where `[IsActive] = 1`
- Composite tenant foreign keys linking `Customer` `(TenantId, CustomerId)` and `Company` `(TenantId, CompanyId)` with `DeleteBehavior.Restrict`
- Foreign key linking `PriceCard` on `(TenantId, SalesPriceCardId)` with `DeleteBehavior.Restrict`

#### Pricing Resolution Hierarchy in `IPricingResolutionService`:
1. Transaction-level `overridePriceCardId`
2. Relationship-assigned `CustomerSellingCompany.SalesPriceCardId` for the active `SellingCompanyId`
3. Legacy/Default fallback `Customer.DefaultSalesPriceCardId`
4. Product Master retail price `Product.SellingPrice`

---

## 5. Architectural Questions for Stakeholder Sign-Off

The following technical architecture questions remain explicitly documented for review:

### Question 1: Multi-Tenant Host Routing Protocol
- **Options Available:**
  1. *Subdomain-Only Routing:* `https://<tenant>.apexerp.com` or `http://<tenant>.localhost:4200` (Currently supported by `TenantHostService`).
  2. *Path-Based Prefix Routing:* `https://apexerp.com/t/<tenantSlug>/storefront`.
  3. *Custom Vanity Domains:* `https://portal.acme-industrial.com`.
- **Architectural Status:** `TenantHostService` currently supports subdomains (`.localhost`, `.apexerp.com`) and custom domains. Path-based routing requires altering Angular router configuration.
- **Recommendation:** Retain subdomain-first resolution for public storefronts; preserve legacy shared host fallback for local development.

### Question 2: Long-Term Customer Delivery Address Modeling
- **Options Available:**
  1. *Customer Address Master (`CustomerAddress` + `Address`):* Already present in `DomainDbContext`. Linked via `SalesOrder.DeliveryAddressId`.
  2. *Dedicated Delivery Dock Aggregate (`CustomerDeliveryLocation`):* Introduce specialized entity storing receiving hours, contact gate phone, and forklift capability.
- **Architectural Status:** The existing `CustomerAddress` + `Address` master combined with `SalesOrder.DeliveryAddressSnapshot` fully satisfies commercial requirements for Phase 1.
- **Recommendation:** Do not create a new aggregate. Enhance the Customer Portal with a dedicated self-service address management controller (`CustomerAddressPortalController`) in Phase 2.

## Unified Order & Notification API Summary

**Verified Backend Endpoints** (available in the current codebase):
- `GET /api/portal/customer/orders` – Returns list of `CustomerOrderSummaryDto` (Retail & Company). `[VERIFIED]`
- `GET /api/portal/customer/orders/{id}` – Returns `CustomerOrderDetailDto` with order lines, status, delivery snapshot, invoice reference. `[VERIFIED]`
- `GET /api/portal/customer/orders/{id}/tracking` – Provides customer‑safe tracking milestones (dispatch, delivery). `[VERIFIED]`
- `GET /api/portal/customer/invoices` – List of `InvoiceDto` for the authenticated customer. `[VERIFIED]`
- `GET /api/portal/customer/invoices/{id}` – Invoice details and payment status. `[VERIFIED]`
- `POST /api/portal/quotations/{id}/accept` – Accepts a quotation and atomically creates `SalesOrder` and updates the relationship `PriceCard`. `[VERIFIED]`
- `POST /api/ecommerce/cart/checkout` – Retail checkout creates a `SalesOrder` draft. `[VERIFIED]`
- `POST /api/portal/customer/orders/direct-b2b` – Direct B2B order creation (requires valid `CustomerSellingCompany` relationship). `[VERIFIED]`
- `GET /api/notifications` – Retrieves pending workspace notifications for the logged‑in customer. `[VERIFIED]`
- `GET /api/notifications/email-templates/{event}` – Returns the email template metadata for a given event (used by the Notification Service). `[VERIFIED]`

**Proposed / Missing Endpoints** (not currently implemented but required for the full workspace experience):
- `GET /api/notifications/email/{event}/{orderId}` – Sends or previews the email for a specific order‑related event. `[PROPOSED]`
- `POST /api/notifications/acknowledge/{notificationId}` – Marks a workspace notification as read/dismissed. `[PROPOSED]`
- `GET /api/portal/customer/quotations/history` – Returns historical quotation records for reuse in new requests. `[PROPOSED]`
- `GET /api/portal/customer/pricecards/{sellingCompanyId}` – Exposes the active contract `PriceCard` for a given selling company (read‑only for customers). `[PROPOSED]`
- `GET /api/portal/customer/cart/history` – List of previously checked‑out carts (currently unsupported). `[PROPOSED]`

All pricing validation for order creation continues to be performed server‑side by `PricingResolutionService`, respecting the hierarchy:
1. Transaction‑level override (internal use only). 
2. Active `CustomerSellingCompany.SalesPriceCardId` for the selected selling company. 
3. `Customer.DefaultSalesPriceCardId` (single‑company fallback). 
4. `Product.SellingPrice` (retail fallback). 

---