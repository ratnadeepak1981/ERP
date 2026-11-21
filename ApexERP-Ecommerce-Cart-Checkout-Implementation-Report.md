# ApexERP — E-commerce Shopping Cart & Free Checkout Backend Implementation Report

**Date:** November 20, 2026  
**Module:** E-commerce / Sales / Customer Portal  
**Scope:** Backend Only (.NET 8 / C# / EF Core / SQL Server)  
**Branch:** `sales-customer-contact-ecommerce`  
**Status:** Completed & Verified  

---

## 1. Executive Summary & Objective

This implementation extends the existing ApexERP E-commerce Product Publishing and Public Catalogue functionality with a production-grade, persistent **Shopping Cart and Free Checkout Backend**.

The objective was to deliver a complete server-side cart management and order creation pipeline that supports both anonymous storefront visitors and authenticated Customer Portal users, enforces strict server-side pricing and tax calculations without trusting client input, and provides a Free Checkout flow that generates authoritative `SalesOrder` records in `Draft` status without payment gateway overhead or fake payment states.

All database schema updates were backed up, applied via EF Core migrations to the local development database (`DomainDB`), and verified. A comprehensive automated test suite (28/28 passing) confirms that domain invariants, security boundaries, and concurrency/idempotency rules are strictly preserved.

---

## 2. Architecture & Design Principles

The implementation adheres to the ApexERP Modular Monolith and Domain-Driven Design (DDD) standards defined in `z:\ERP\AGENTS.md`:

1. **Separation of Concerns:** Authoritative business logic, validation, pricing resolution, and tax calculation reside strictly in the Backend (`Domain` and `ERP.Application`). Client applications (Web/Mobile) are treated as untrusted presentation consumers.
2. **Persistent Cart Lifecycle:** Carts and line items are stored durably in the relational database (`EcommerceCarts` and `EcommerceCartLines`) rather than relying on browser memory or ephemeral sessions.
3. **Multi-Tenancy & Selling Company Scoping:** Every cart is isolated by `TenantId` (`ITenantScopedEntity`) and Selling `CompanyId` (`ICompanyScopedEntity`). Cross-tenant or cross-company data leakage is structurally impossible.
4. **Dynamic Price Sourcing:** Cart lines store only item identity (`ProductId`), quantity, and unit of measure/packaging references. Prices, discounts, and tax rates are resolved on-demand server-side using the central `PricingResolutionService`.
5. **Clean Sales Order Integration:** Checkout coordinates directly with `ISalesOrderService.CreateOrderAsync`, ensuring that standard ERP validation, customer credit checks, currency snapshotting, and draft order creation logic remain unified.
6. **Zero Payment Gateway Infiltration:** Free Checkout creates an order in `Draft` status. No mock payment records, fake transactions, or premature `Paid` status flags were introduced.

---

## 3. Shopping Cart Domain Architecture

### 3.1 Domain Entities (`Backend/ERP/Domain/Features/Ecommerce/`)

#### `EcommerceCart.cs`
- **Identity & Scoping:**
  - `Id` (Guid, PK)
  - `TenantId` (Guid) — enforces multi-tenant boundary.
  - `CompanyId` (Guid) — establishes Selling Company operational context.
- **Cart Ownership:**
  - `CustomerId` (Guid?, nullable) — populated for registered B2B customers.
  - `CustomerContactId` (Guid?, nullable) — contact who initiated the cart.
  - `SessionId` (string?, max 128 chars) — anonymous visitor cookie/token.
- **Workflow & Lifecycle:**
  - `Status` (Enum: `Active = 1`, `CheckedOut = 2`, `Abandoned = 3`).
  - `SalesOrderId` (Guid?, nullable) — linked upon successful checkout.
  - `IdempotencyKey` (string?, max 128 chars) — prevents duplicate checkout operations.
  - `CheckoutAttemptedAtUtc` (DateTime?, nullable) — concurrency audit timestamp.
- **Audit & Navigation:**
  - `CreatedAtUtc`, `UpdatedAtUtc`, base `Auditable` fields (`CreatedAt`, `CreatedBy`, `ModifiedAt`, `ModifiedBy`).
  - Encapsulated lines collection: `IReadOnlyCollection<EcommerceCartLine> Lines => _lines.AsReadOnly();`.
  - Factory method `EcommerceCart.Create(...)` and domain mutation methods: `AddOrUpdateLine`, `UpdateLineQuantity`, `RemoveLine`, `ClearLines`, `AssociateCustomer`, `RecordCheckoutAttempt`, `MarkCheckedOut`, `MarkAbandoned`.

#### `EcommerceCartLine.cs`
- **Identity & Association:**
  - `Id` (Guid, PK)
  - `CartId` (Guid, FK to `EcommerceCarts` with Cascade Delete)
  - `ProductId` (Guid, FK to `Products` with Restrict Delete)
  - `UnitOfMeasureId` (Guid?, nullable, FK to `UnitsOfMeasure` with Restrict Delete)
  - `ProductPackagingId` (Guid?, nullable, FK to `ProductPackagings` with Restrict Delete)
- **Quantity & Audit:**
  - `Quantity` (`decimal(18,4)`, strictly > 0)
  - `CreatedAtUtc`, `UpdatedAtUtc`
- **Pricing Policy:**
  - **Zero stored client prices:** Prices are intentionally omitted from line persistence to guarantee that retail pricing updates, price cards, and promotional rules always reflect active real-time data at display and checkout times.

---

## 4. Dual Identity & Session Management

The system transparently handles both guest storefront visitors and authenticated Customer Portal users:

1. **Anonymous Guest Sessions:**
   - Identified by an opaque token passed via the HTTP header `X-Cart-Session-Id` (or query/body).
   - If the header is missing on an initial request, a secure token is generated (`Guid.NewGuid().ToString("N")`) and returned to the client in the `X-Cart-Session-Id` response header.
2. **Authenticated B2B Customers:**
   - Automatically recognized through `ICurrentCustomerContext` when a JWT carrying `portal_type == "CUSTOMER"` is present.
   - The user's `CustomerId` and `ContactId` are resolved directly from token claims.
3. **Session-to-Customer Cart Merging:**
   - If an anonymous visitor adds items to a cart under a session token and subsequently logs into their portal account, accessing the cart automatically associates their `CustomerId` with the active cart without losing items or creating orphan records.

---

## 5. Dynamic Server-Side Pricing & Tax Engine

1. **Price Card & Product Master Resolution:**
   - Unit prices are evaluated via `IPricingResolutionService.ResolveSalesPriceAsync`.
   - If the customer has an assigned `DefaultSalesPriceCardId`, the price card's tiered or customer-specific rates are applied.
   - If no price card is assigned, the service falls back to `Product.SellingPrice` from Product Master.
   - UOM conversions are calculated automatically if the requested UOM differs from the product base UOM.
2. **Tax Rate Resolution:**
   - Dynamically resolves active sales taxes from `DomainDbContext.TaxCodes` where `Applicability == TaxApplicability.Sales` and `IsActive == true`.
   - Subtotals, tax amounts, and line totals are calculated server-side and returned in `CartDto` and `CartItemDto`.
3. **Tamper Prevention:**
   - Add-to-cart, update-quantity, and checkout requests accept **zero price arguments** from the client. Any price provided in the payload is ignored; authoritative server prices are recalculated at the moment of checkout.

---

## 6. Free Checkout & Sales Order Integration

### 6.1 Execution Workflow
When `POST /api/ecommerce/cart/checkout` is called:
1. **Scope Validation:** Verifies `TenantId` and Selling `CompanyId` are active and valid.
2. **Idempotency Protection:** Checks if an order was already generated for the provided `IdempotencyKey`. If so, returns the existing order result immediately.
3. **Active Cart Verification:** Retrieves active cart; ensures cart is not empty.
4. **Catalogue Publication Check:** Verifies that every product currently in the cart has an active publication record (`EcommerceProduct.IsPublished == true`) for this Selling Company. If any item was unpublished or deactivated by an administrator while in the user's cart, checkout fails gracefully with an explicit error.
5. **Selling Company & Branch Resolution:** Resolves the Selling Company's base currency and default active Branch (e.g., `MAIN` or `HQ`).
6. **Customer Assignment:**
   - If authenticated, uses the verified `CustomerId`.
   - If guest/anonymous, resolves the tenant's walk-in customer profile (`CUST-WALKIN`).
7. **Delivery Address Validation:** Validates that any provided `DeliveryAddressId` belongs to the customer (via `CustomerAddress` or `Address`).
8. **Sales Order Generation:** Generates a unique, traceable order number (`SO-WEB-{yyyyMMdd}-{hash}`) and constructs a `CreateSalesOrderRequest`.
9. **Draft Order Creation:** Invokes `ISalesOrderService.CreateOrderAsync`, which enforces standard business rules, snapshots foreign exchange rates, and stores the `SalesOrder` in `Draft` status.
10. **Cart State Transition:** Transitions cart to `Status = CheckedOut`, sets `SalesOrderId`, and commits the transaction.

---

## 7. API Specifications (`API/Features/Ecommerce/EcommerceCartController.cs`)

Route prefix: `/api/ecommerce/cart`  
Authentication: `[AllowAnonymous]` (with automatic customer portal claims extraction)

| HTTP Method | Route | Description |
|---|---|---|
| `GET` | `/api/ecommerce/cart?companyId={id}` | Retrieves the active cart with dynamic pricing and totals. Returns empty cart structure if none exists. |
| `POST` | `/api/ecommerce/cart/items?companyId={id}` | Adds a product to the cart (or increments quantity if already present). Validates publication status. |
| `PUT` | `/api/ecommerce/cart/items/{lineId}?companyId={id}` | Updates item quantity. Setting quantity `<= 0` removes the item. |
| `DELETE` | `/api/ecommerce/cart/items/{lineId}?companyId={id}` | Removes a specific item from the active cart. |
| `DELETE` | `/api/ecommerce/cart?companyId={id}` | Clears all items from the active cart. |
| `POST` | `/api/ecommerce/cart/checkout?companyId={id}` | Executes Free Checkout, creating a Draft `SalesOrder` and marking the cart checked out. |

### Header Protocols:
- **Request Header:** `X-Cart-Session-Id` (optional for guests; generated if absent).
- **Response Header:** `X-Cart-Session-Id` (always mirrored to allow client token persistence).

---

## 8. EF Core Configuration & Database Migration

### 8.1 Entity Configurations (`Domain/Infrastructure/Persistence/Confiquarations/`)
- `EcommerceCartConfiguration.cs`: Table `EcommerceCarts`, PK `Id`, composite indexes on `(TenantId, CompanyId, Status)`, `(TenantId, CustomerId, Status)`, `(TenantId, SessionId, Status)`, and `(TenantId, CompanyId, IdempotencyKey)`. Restrictive FKs to `Companies` and `Customers`; `SetNull` FK to `SalesOrders`.
- `EcommerceCartLineConfiguration.cs`: Table `EcommerceCartLines`, PK `Id`, FK to `EcommerceCarts` (Cascade), FKs to `Products`, `UnitsOfMeasure`, and `ProductPackagings` (Restrict). Quantity configured with precision `decimal(18,4)`.

### 8.2 Safe Database Application Sequence
1. **Target Verification:** Development database confirmed as `(localdb)\MSSQLLocalDB` / `DomainDB` via `appsettings.json`.
2. **Pre-Migration Backup:**
   - Full database backup executed to `z:\ERP\DatabaseBackups\DomainDB_Pre_EcommerceCart.bak`.
   - Verified with `RESTORE VERIFYONLY FROM DISK = N'z:\ERP\DatabaseBackups\DomainDB_Pre_EcommerceCart.bak'`.
   - Verification Result: `The backup set on file 1 is valid. (3450 pages in 0.502s)`.
3. **Migration Generation:**
   - Migration `20261120103052_AddEcommerceCartAndCheckout.cs` generated cleanly in `Backend/ERP/Domain/Migrations/`.
4. **Database Update:**
   - Applied via `dotnet ef database update --context DomainDbContext`.
   - Status: Applied successfully with zero errors.
5. **Schema Verification:**
   - Verified via SQL: `EcommerceCarts` (16 columns) and `EcommerceCartLines` (8 columns) created in `DomainDB` with all foreign keys and indexes active.

---

## 9. Dependency Injection & Service Registration

Registered in `Backend/ERP/API/Confiquration/ServiceRegistration.cs`:
```csharp
// E-commerce Product Publishing & Shopping Cart
services.AddScoped<ERP.Application.Features.Ecommerce.IEcommerceProductService, ERP.Application.Features.Ecommerce.EcommerceProductService>();
services.AddScoped<ERP.Application.Features.Ecommerce.Cart.IEcommerceCartService, ERP.Application.Features.Ecommerce.Cart.EcommerceCartService>();
```

---

## 10. Automated Test Suite & Verification Results

The implementation was verified using the official xUnit test project (`Backend/ERP/Testing/ERP.SecurityTests/`):

### 10.1 Cart Service Tests (`EcommerceCartServiceTests.cs`)
1. `AddToCart_PublishedProduct_CreatesCartAndCalculatesServerPrice` — Passed.
2. `AddToCart_UnpublishedProduct_ThrowsInvalidOperationException` — Passed.
3. `AddToCart_NonSellableProduct_ThrowsInvalidOperationException` — Passed.
4. `AddToCart_ExistingItem_IncrementsQuantity` — Passed.
5. `UpdateLineQuantity_ValidQuantity_UpdatesSubtotal` — Passed.
6. `UpdateLineQuantity_ZeroQuantity_RemovesLine` — Passed.
7. `RemoveLine_RemovesTargetLineFromCart` — Passed.
8. `ClearCart_RemovesAllItems` — Passed.
9. `DynamicPricing_ReflectsUpdatedPriceImmediatelyWithoutMutatingCartStorage` — Passed.
10. `CustomerLogin_AssociatesAnonymousSessionCartWithCustomer` — Passed.

### 10.2 Checkout Tests (`EcommerceCheckoutTests.cs`)
1. `Checkout_ActiveCartWithItems_CreatesDraftSalesOrderAndMarksCartCheckedOut` — Passed.
2. `Checkout_IdempotencyKey_RepeatedCallReturnsExistingOrderWithoutDuplicate` — Passed.
3. `Checkout_EmptyCart_ThrowsInvalidOperationException` — Passed.
4. `Checkout_UnpublishedProductInCart_ThrowsInvalidOperationException` — Passed.
5. `Checkout_AuthenticatedCustomer_PreservesCustomerAndDeliveryAddress` — Passed.

### 10.3 Full Regression Suite
- **Ecommerce Product Publishing Tests:** 13/13 passed.
- **Ecommerce Cart & Checkout Tests:** 15/15 passed.
- **Total Test Suite Execution:** **28 passed, 0 failed, 0 skipped** (Execution time: 2s).

---

## 11. Git Working Tree & Safety Discipline

In strict compliance with `AGENTS.md` and user instructions:
- Current branch preserved: `sales-customer-contact-ecommerce`.
- Zero git commits, pushes, merges, stashes, or branch resets were performed.
- All preexisting untracked files and working tree modifications outside the task scope were preserved intact.

---

## 12. Documentation Alignment

- Updated `Customer-Workspace-and-Registration-Workflow.md` with Section 20 ("E-commerce Shopping Cart & Free Checkout Backend Architecture"), providing complete architectural, operational, and API documentation.

---

## 13. Summary of Created & Modified Artifacts

| Artifact | Path | Action | Description |
|---|---|---|---|
| `EcommerceCart.cs` | `Backend/ERP/Domain/Features/Ecommerce/` | Created | Cart domain aggregate root with status, idempotency, and session tracking. |
| `EcommerceCartLine.cs` | `Backend/ERP/Domain/Features/Ecommerce/` | Created | Cart line item entity storing quantity, UOM, and packaging without client prices. |
| `EcommerceCartConfiguration.cs` | `Backend/ERP/Domain/Infrastructure/Persistence/Confiquarations/` | Created | EF Core mapping, constraints, and composite indexes for `EcommerceCarts`. |
| `EcommerceCartLineConfiguration.cs` | `Backend/ERP/Domain/Infrastructure/Persistence/Confiquarations/` | Created | EF Core mapping, cascade delete, and foreign keys for `EcommerceCartLines`. |
| `DomainDbContext.cs` | `Backend/ERP/Domain/Infrastructure/Persistence/` | Modified | Registered `EcommerceCarts` and `EcommerceCartLines` DbSets. |
| `EcommerceCartDtos.cs` | `Backend/ERP/ERP.Application/Features/Ecommerce/Cart/` | Created | Request and response contracts for cart operations, items, and checkout. |
| `IEcommerceCartService.cs` | `Backend/ERP/ERP.Application/Features/Ecommerce/Cart/` | Created | Service interface for cart management, dynamic pricing, and checkout. |
| `EcommerceCartService.cs` | `Backend/ERP/ERP.Application/Features/Ecommerce/Cart/` | Created | Core implementation coordinating pricing, tax, catalog check, and checkout. |
| `ServiceRegistration.cs` | `Backend/ERP/API/Confiquration/` | Modified | Registered `IEcommerceCartService` in ASP.NET Core DI container. |
| `EcommerceCartController.cs` | `Backend/ERP/API/Features/Ecommerce/` | Created | Public REST API controller supporting guest and authenticated customer carts. |
| `20261120103052_AddEcommerceCartAndCheckout.cs` | `Backend/ERP/Domain/Migrations/` | Created & Applied | EF Core migration creating `EcommerceCarts` and `EcommerceCartLines` tables. |
| `DomainDB_Pre_EcommerceCart.bak` | `z:\ERP\DatabaseBackups/` | Created & Verified | Full pre-migration SQL backup verified with `RESTORE VERIFYONLY`. |
| `EcommerceCartServiceTests.cs` | `Backend/ERP/Testing/ERP.SecurityTests/Ecommerce/` | Created | 10 unit/integration tests for cart management, pricing, and customer merging. |
| `EcommerceCheckoutTests.cs` | `Backend/ERP/Testing/ERP.SecurityTests/Ecommerce/` | Created | 5 unit/integration tests for checkout workflow, draft creation, and idempotency. |
| `Customer-Workspace-and-Registration-Workflow.md` | `z:\ERP/` | Modified | Added Section 20 documenting Cart and Free Checkout architecture. |
| `ApexERP-Ecommerce-Cart-Checkout-Implementation-Report.md` | `z:\ERP/` | Created | Comprehensive final implementation report. |
