import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import { TestBed, getTestBed } from '@angular/core/testing';
import {
  BrowserTestingModule,
  platformBrowserTesting
} from '@angular/platform-browser/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { signal, computed } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { EcommerceCartService } from './ecommerce-cart.service';
import { AuthService } from '../auth/auth.service';
import { UserSession } from '../models/auth.models';
import {
  CartDto,
  CartItemDto,
  AddToCartRequest,
  CheckoutRequest,
  CheckoutResultDto,
  EcommerceCartStatus
} from '../models/ecommerce-cart.models';

if (typeof (globalThis as any).document === 'undefined') {
  (globalThis as any).document = {
    createElement: () => ({ style: {} }),
    querySelector: () => null,
    querySelectorAll: () => [],
    documentElement: {
      classList: {
        remove: () => {},
        add: () => {},
        contains: () => false
      }
    },
    body: {
      querySelector: () => null,
      querySelectorAll: () => [],
      appendChild: () => {},
      removeChild: () => {}
    },
    location: { href: 'http://localhost' }
  };
  (globalThis as any).window = globalThis;
}

try {
  getTestBed().initTestEnvironment(
    BrowserTestingModule,
    platformBrowserTesting()
  );
} catch {
  // Already initialized in test environment
}

describe('EcommerceCartService', () => {
  let service: EcommerceCartService;
  let httpMock: HttpTestingController;

  const mockCompanyId = '6ad8fe26-04b3-4fe2-917b-c9e03f5b7ac6';
  const otherCompanyId = 'fff3a70a-197f-48c6-834b-b8ae891ae3b2';

  const mockUserSession: UserSession = {
    userId: 'user-001',
    username: 'john.retail@example.com',
    email: 'john.retail@example.com',
    tenantId: '11111111-1111-1111-1111-111111111111',
    roles: ['CUSTOMER_INDIVIDUAL'],
    permissions: [],
    token: 'valid-test-jwt-token'
  };

  const currentUserSignal = signal<UserSession | null>(mockUserSession);

  const mockAuthService = {
    currentUser: currentUserSignal,
    token: computed(() => currentUserSignal()?.token ?? null),
    isAuthenticated: computed(() => !!currentUserSignal()?.token),
    roles: computed(() => currentUserSignal()?.roles ?? []),
    isCustomer: computed(() => true),
    isCustomerIndividual: computed(() => true),
    isCustomerCompany: computed(() => false)
  };

  const sampleCartItem: CartItemDto = {
    lineId: 'line-001',
    productId: 'prod-001',
    productCode: 'SKU-BOLT-M8',
    productName: 'M8 Stainless Steel Hex Bolt',
    unitOfMeasureId: 'uom-001',
    unitOfMeasureCode: 'PCS',
    unitOfMeasureName: 'Pieces',
    productPackagingId: 'pkg-001',
    packagingName: 'Box of 100',
    quantity: 5,
    unitPrice: 12.5,
    subTotal: 62.5,
    discountRate: 0,
    discountAmount: 0,
    taxCodeId: 'tax-001',
    taxCode: 'VAT-15',
    taxRate: 15,
    taxAmount: 9.38,
    lineTotal: 71.88,
    isTaxInclusive: false,
    currency: 'USD',
    isAvailable: true,
    validationMessage: null
  };

  const sampleCart: CartDto = {
    cartId: 'cart-001',
    tenantId: '11111111-1111-1111-1111-111111111111',
    companyId: mockCompanyId,
    companyName: 'Alpha Manufacturing Ltd',
    customerId: 'cust-001',
    customerName: 'John Retail',
    sessionId: null,
    status: EcommerceCartStatus.Active,
    currency: 'USD',
    totalItems: 1,
    subTotal: 62.5,
    totalDiscount: 0,
    totalTax: 9.38,
    grandTotal: 71.88,
    lines: [sampleCartItem]
  };

  beforeEach(() => {
    TestBed.resetTestingModule();
    currentUserSignal.set({ ...mockUserSession });

    TestBed.configureTestingModule({
      providers: [
        EcommerceCartService,
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: AuthService, useValue: mockAuthService }
      ]
    });

    service = TestBed.inject(EcommerceCartService);
    httpMock = TestBed.inject(HttpTestingController);
    service.setCompanyContext(mockCompanyId);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('should be created with initial state', () => {
    expect(service).toBeTruthy();
    expect(service.cart()).toBeNull();
    expect(service.loading()).toBe(false);
    expect(service.error()).toBeNull();
    expect(service.totalItems()).toBe(0);
    expect(service.subTotal()).toBe(0);
    expect(service.grandTotal()).toBe(0);
    expect(service.isEmpty()).toBe(true);
  });

  // 1. Load active cart
  it('1. should load active cart and populate reactive state', async () => {
    const cartPromise = firstValueFrom(service.getCart());

    const req = httpMock.expectOne(request =>
      request.url.includes('/ecommerce/cart') &&
      request.params.get('companyId') === mockCompanyId
    );
    expect(req.request.method).toBe('GET');

    req.flush({ succeeded: true, message: 'Cart retrieved', data: sampleCart, errors: [] });

    const result = await cartPromise;
    expect(result.cartId).toBe('cart-001');
    expect(service.cart()).toEqual(sampleCart);
    expect(service.totalItems()).toBe(1);
    expect(service.subTotal()).toBe(62.5);
    expect(service.totalTax()).toBe(9.38);
    expect(service.grandTotal()).toBe(71.88);
    expect(service.currency()).toBe('USD');
    expect(service.isEmpty()).toBe(false);
    expect(service.loading()).toBe(false);
    expect(service.error()).toBeNull();
  });

  // 2. Add product with ProductId, Quantity, UnitOfMeasureId and ProductPackagingId
  it('2. should add product to cart with correct payload structure (no client price/tax)', async () => {
    const addRequest: AddToCartRequest = {
      productId: 'prod-001',
      quantity: 5,
      unitOfMeasureId: 'uom-001',
      productPackagingId: 'pkg-001'
    };

    const addPromise = firstValueFrom(service.addToCart(addRequest));

    const req = httpMock.expectOne(request =>
      request.url.includes('/ecommerce/cart/items') &&
      request.params.get('companyId') === mockCompanyId
    );
    expect(req.request.method).toBe('POST');

    // Verify payload strictly matches backend contract and contains zero price/tax overrides
    expect(req.request.body).toEqual({
      productId: 'prod-001',
      quantity: 5,
      unitOfMeasureId: 'uom-001',
      productPackagingId: 'pkg-001'
    });
    expect((req.request.body as any).unitPrice).toBeUndefined();
    expect((req.request.body as any).taxAmount).toBeUndefined();
    expect((req.request.body as any).subTotal).toBeUndefined();

    req.flush({ succeeded: true, message: 'Item added', data: sampleCart, errors: [] });

    const result = await addPromise;
    expect(result.lines.length).toBe(1);
    expect(service.cart()?.lines.length).toBe(1);
    expect(service.totalItems()).toBe(1);
  });

  // 3. Update quantity
  it('3. should update line quantity with { quantity } payload', async () => {
    const updatedCart: CartDto = {
      ...sampleCart,
      totalItems: 1,
      lines: [{ ...sampleCartItem, quantity: 10, subTotal: 125.0, lineTotal: 143.75 }],
      subTotal: 125.0,
      totalTax: 18.75,
      grandTotal: 143.75
    };

    const updatePromise = firstValueFrom(service.updateLineQuantity('line-001', 10));

    const req = httpMock.expectOne(request =>
      request.url.includes('/ecommerce/cart/items/line-001') &&
      request.params.get('companyId') === mockCompanyId
    );
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual({ quantity: 10 });

    req.flush({ succeeded: true, message: 'Quantity updated', data: updatedCart, errors: [] });

    const result = await updatePromise;
    expect(result.grandTotal).toBe(143.75);
    expect(service.grandTotal()).toBe(143.75);
    expect(service.lines()[0].quantity).toBe(10);
  });

  // 4. Remove line
  it('4. should remove a line item from cart', async () => {
    const emptyCart: CartDto = {
      ...sampleCart,
      totalItems: 0,
      lines: [],
      subTotal: 0,
      totalTax: 0,
      grandTotal: 0
    };

    const removePromise = firstValueFrom(service.removeLine('line-001'));

    const req = httpMock.expectOne(request =>
      request.url.includes('/ecommerce/cart/items/line-001') &&
      request.params.get('companyId') === mockCompanyId
    );
    expect(req.request.method).toBe('DELETE');

    req.flush({ succeeded: true, message: 'Line removed', data: emptyCart, errors: [] });

    const result = await removePromise;
    expect(result.lines.length).toBe(0);
    expect(service.isEmpty()).toBe(true);
    expect(service.totalItems()).toBe(0);
  });

  // 5. Clear cart
  it('5. should clear all cart lines', async () => {
    const clearedCart: CartDto = {
      ...sampleCart,
      totalItems: 0,
      lines: [],
      subTotal: 0,
      totalTax: 0,
      grandTotal: 0
    };

    const clearPromise = firstValueFrom(service.clearCart());

    const req = httpMock.expectOne(request =>
      request.url.includes('/ecommerce/cart') &&
      !request.url.includes('/items') &&
      request.params.get('companyId') === mockCompanyId
    );
    expect(req.request.method).toBe('DELETE');

    req.flush({ succeeded: true, message: 'Cart cleared', data: clearedCart, errors: [] });

    const result = await clearPromise;
    expect(result.lines.length).toBe(0);
    expect(service.isEmpty()).toBe(true);
    expect(service.grandTotal()).toBe(0);
  });

  // 6. Correct item count and totals derived authoritatively
  it('6. should correctly compute reactive totals from backend CartDto', async () => {
    const multiItemCart: CartDto = {
      ...sampleCart,
      totalItems: 2,
      subTotal: 150.0,
      totalTax: 22.5,
      grandTotal: 172.5,
      currency: 'EUR',
      lines: [
        sampleCartItem,
        { ...sampleCartItem, lineId: 'line-002', productId: 'prod-002', quantity: 2, lineTotal: 100.62 }
      ]
    };

    const cartPromise = firstValueFrom(service.getCart());
    const req = httpMock.expectOne(r => r.method === 'GET');
    req.flush({ succeeded: true, message: 'OK', data: multiItemCart, errors: [] });

    await cartPromise;
    expect(service.totalItems()).toBe(2);
    expect(service.subTotal()).toBe(150.0);
    expect(service.totalTax()).toBe(22.5);
    expect(service.grandTotal()).toBe(172.5);
    expect(service.currency()).toBe('EUR');
    expect(service.lines().length).toBe(2);
    expect(service.isEmpty()).toBe(false);
  });

  // 7. Loading and error states
  it('7. should manage loading state and capture backend error messages', async () => {
    let capturedError: Error | null = null;
    try {
      const getPromise = firstValueFrom(service.getCart());
      expect(service.loading()).toBe(true);

      const req = httpMock.expectOne(r => r.method === 'GET');
      req.flush(
        { message: 'Selling company is currently inactive.' },
        { status: 400, statusText: 'Bad Request' }
      );

      await getPromise;
    } catch (err: any) {
      capturedError = err;
    }

    expect(capturedError).not.toBeNull();
    expect(service.loading()).toBe(false);
    expect(service.error()).toBe('Selling company is currently inactive.');
  });

  // 8. Company context changes
  it('8. should clear in-memory cart when company context changes', async () => {
    // Populate cart for initial company
    const cartPromise = firstValueFrom(service.getCart());
    const req = httpMock.expectOne(r => r.method === 'GET');
    req.flush({ succeeded: true, message: 'OK', data: sampleCart, errors: [] });
    await cartPromise;
    expect(service.cart()).not.toBeNull();

    // Switch company context
    service.setCompanyContext(otherCompanyId);
    expect(service.activeCompanyId()).toBe(otherCompanyId);
    expect(service.cart()).toBeNull();
    expect(service.totalItems()).toBe(0);
    expect(service.grandTotal()).toBe(0);
  });

  // 9. Customer logout or identity changes
  it('9. should reject operations and reset cart when customer is unauthenticated or identity changes', async () => {
    // Simulate user logout
    currentUserSignal.set(null);

    let errorResult: Error | null = null;
    try {
      await firstValueFrom(service.getCart());
    } catch (err: any) {
      errorResult = err;
    }

    expect(errorResult).not.toBeNull();
    expect(errorResult?.message).toContain('Customer authentication is required');
    expect(service.error()).toContain('Customer authentication is required');
    expect(service.cart()).toBeNull();

    // Verify zero HTTP requests were dispatched
    httpMock.expectNone(r => r.url.includes('/ecommerce/cart'));
  });

  it('9b. should reset cart when user identity changes', async () => {
    // Load cart as user-001
    const cartPromise = firstValueFrom(service.getCart());
    const req = httpMock.expectOne(r => r.method === 'GET');
    req.flush({ succeeded: true, message: 'OK', data: sampleCart, errors: [] });
    await cartPromise;
    expect(service.cart()).not.toBeNull();

    // Change authenticated identity to user-002
    currentUserSignal.set({
      ...mockUserSession,
      userId: 'user-002',
      email: 'different.user@example.com'
    });

    // Attempt operation - identity mismatch must purge cart state
    const nextPromise = firstValueFrom(service.getCart());
    const nextReq = httpMock.expectOne(r => r.method === 'GET');
    nextReq.flush({
      succeeded: true,
      message: 'OK',
      data: { ...sampleCart, customerId: 'cust-002', lines: [] },
      errors: []
    });

    const result = await nextPromise;
    expect(result.customerId).toBe('cust-002');
  });

  // 10. No client-side price or tax values are submitted
  it('10. should strictly omit any client-side pricing or tax values in AddToCart requests', async () => {
    const forgedRequest = {
      productId: 'prod-999',
      quantity: 1,
      unitPrice: 0.01,
      taxAmount: 0,
      subTotal: 0.01,
      grandTotal: 0.01
    } as any;

    const addPromise = firstValueFrom(service.addToCart(forgedRequest));

    const req = httpMock.expectOne(r => r.method === 'POST');
    expect(req.request.body.productId).toBe('prod-999');
    expect(req.request.body.quantity).toBe(1);
    expect(req.request.body.unitPrice).toBeUndefined();
    expect(req.request.body.taxAmount).toBeUndefined();
    expect(req.request.body.subTotal).toBeUndefined();
    expect(req.request.body.grandTotal).toBeUndefined();

    req.flush({ succeeded: true, message: 'Item added', data: sampleCart, errors: [] });
    await addPromise;
  });

  // =========================================================================
  // Stage 1B: Asynchronous State Isolation & Race Condition Tests
  // =========================================================================

  describe('Asynchronous State Isolation & Race Conditions', () => {
    // 1. Company switch during pending GET
    it('1. should discard pending GET response if company context changes before response arrives', async () => {
      // Start GET for mockCompanyId
      let getResolved = false;
      service.getCart(mockCompanyId).subscribe({
        next: () => { getResolved = true; },
        error: () => { getResolved = true; }
      });

      expect(service.loading()).toBe(true);

      const reqA = httpMock.expectOne(r => r.method === 'GET' && r.params.get('companyId') === mockCompanyId);

      // User switches to otherCompanyId before response A returns
      service.setCompanyContext(otherCompanyId);
      expect(service.activeCompanyId()).toBe(otherCompanyId);
      expect(service.cart()).toBeNull();

      // Response A returns for mockCompanyId
      reqA.flush({ succeeded: true, message: 'OK', data: sampleCart, errors: [] });

      // Cart state for otherCompanyId must NOT be overwritten by Company A's stale cart
      expect(service.cart()).toBeNull();
      expect(service.loading()).toBe(false);
      expect(getResolved).toBe(true);
    });

    // 2. Customer logout during pending GET
    it('2. should discard pending GET response and remain empty if customer logs out before response arrives', async () => {
      let getResolved = false;
      service.getCart(mockCompanyId).subscribe({
        next: () => { getResolved = true; },
        error: () => { getResolved = true; }
      });

      expect(service.loading()).toBe(true);

      const req = httpMock.expectOne(r => r.method === 'GET');

      // Customer logs out
      currentUserSignal.set(null);
      service.resetCart();
      expect(service.cart()).toBeNull();

      // Response returns
      req.flush({ succeeded: true, message: 'OK', data: sampleCart, errors: [] });

      // Cart must not be restored after logout
      expect(service.cart()).toBeNull();
      expect(service.loading()).toBe(false);
      expect(getResolved).toBe(true);
    });

    // 3. Customer switch during pending GET
    it('3. should discard pending GET response if customer identity changes before response arrives', async () => {
      let getResolved = false;
      service.getCart(mockCompanyId).subscribe({
        next: () => { getResolved = true; },
        error: () => { getResolved = true; }
      });

      const req = httpMock.expectOne(r => r.method === 'GET');

      // User identity changes to user-002
      currentUserSignal.set({
        ...mockUserSession,
        userId: 'user-002',
        email: 'user-002@example.com'
      });
      service.resetCart();

      // Response for user-001 arrives
      req.flush({ succeeded: true, message: 'OK', data: sampleCart, errors: [] });

      // Stale user-001 cart must NOT be applied to user-002
      expect(service.cart()).toBeNull();
      expect(service.loading()).toBe(false);
      expect(getResolved).toBe(true);
    });

    // 4. Company switch during pending Add-to-Cart
    it('4. should discard pending Add-to-Cart response if company changes before response arrives', async () => {
      const addReq: AddToCartRequest = {
        productId: 'prod-001',
        quantity: 2
      };

      let addResolved = false;
      service.addToCart(addReq, mockCompanyId).subscribe({
        next: () => { addResolved = true; },
        error: () => { addResolved = true; }
      });

      const postReq = httpMock.expectOne(r => r.method === 'POST');

      // Switch to otherCompanyId
      service.setCompanyContext(otherCompanyId);
      expect(service.cart()).toBeNull();

      // Response arrives
      postReq.flush({ succeeded: true, message: 'Added', data: sampleCart, errors: [] });

      expect(service.cart()).toBeNull();
      expect(service.loading()).toBe(false);
      expect(addResolved).toBe(true);
    });

    // 5. Customer identity change during pending updateLineQuantity
    it('5. should discard pending updateLineQuantity response if customer changes before response arrives', async () => {
      let updateResolved = false;
      service.updateLineQuantity('line-001', 10, mockCompanyId).subscribe({
        next: () => { updateResolved = true; },
        error: () => { updateResolved = true; }
      });

      const putReq = httpMock.expectOne(r => r.method === 'PUT');

      // Switch user
      currentUserSignal.set({
        ...mockUserSession,
        userId: 'user-999',
        email: 'user-999@example.com'
      });
      service.resetCart();

      // Response arrives
      putReq.flush({
        succeeded: true,
        message: 'Updated',
        data: { ...sampleCart, subTotal: 125, grandTotal: 143.75 },
        errors: []
      });

      expect(service.cart()).toBeNull();
      expect(service.loading()).toBe(false);
      expect(updateResolved).toBe(true);
    });

    // 6. Customer logout during pending removeLine
    it('6. should discard pending removeLine response if customer logs out before response arrives', async () => {
      let removeResolved = false;
      service.removeLine('line-001', mockCompanyId).subscribe({
        next: () => { removeResolved = true; },
        error: () => { removeResolved = true; }
      });

      const delReq = httpMock.expectOne(r => r.method === 'DELETE');

      // Logout
      currentUserSignal.set(null);
      service.resetCart();

      delReq.flush({
        succeeded: true,
        message: 'Removed',
        data: { ...sampleCart, lines: [], totalItems: 0 },
        errors: []
      });

      expect(service.cart()).toBeNull();
      expect(service.loading()).toBe(false);
      expect(removeResolved).toBe(true);
    });

    // 7. Out-of-order GET responses (older GET cannot overwrite newer GET)
    it('7. should ensure an older overlapping GET response cannot overwrite a newer GET response', async () => {
      const staleCart: CartDto = {
        ...sampleCart,
        cartId: 'cart-stale-version',
        totalItems: 1
      };

      const freshCart: CartDto = {
        ...sampleCart,
        cartId: 'cart-fresh-version',
        totalItems: 5
      };

      // Dispatch Request 1 (GET #1)
      service.getCart(mockCompanyId).subscribe();
      // Dispatch Request 2 (GET #2)
      service.getCart(mockCompanyId).subscribe();

      const requests = httpMock.match(r => r.method === 'GET');
      expect(requests.length).toBe(2);
      const req1 = requests[0];
      const req2 = requests[1];

      // Req 2 (newer) finishes FIRST
      req2.flush({ succeeded: true, message: 'OK', data: freshCart, errors: [] });
      expect(service.cart()?.cartId).toBe('cart-fresh-version');
      expect(service.cart()?.totalItems).toBe(5);

      // Req 1 (older) finishes SECOND (out-of-order)
      req1.flush({ succeeded: true, message: 'OK', data: staleCart, errors: [] });

      // Newer cart state must be preserved; older state must be discarded!
      expect(service.cart()?.cartId).toBe('cart-fresh-version');
      expect(service.cart()?.totalItems).toBe(5);
      expect(service.loading()).toBe(false);
    });

    // 8. Out-of-order mutation responses (older mutation cannot overwrite newer mutation)
    it('8. should ensure an older overlapping mutation response cannot overwrite a newer mutation response', async () => {
      const cartAfterMutation1: CartDto = {
        ...sampleCart,
        cartId: 'cart-mutation-1',
        totalItems: 2
      };

      const cartAfterMutation2: CartDto = {
        ...sampleCart,
        cartId: 'cart-mutation-2',
        totalItems: 3
      };

      // Mutation 1 (update quantity)
      service.updateLineQuantity('line-001', 2, mockCompanyId).subscribe();
      // Mutation 2 (update quantity again)
      service.updateLineQuantity('line-001', 3, mockCompanyId).subscribe();

      const requests = httpMock.match(r => r.method === 'PUT');
      expect(requests.length).toBe(2);
      const mutReq1 = requests[0];
      const mutReq2 = requests[1];

      // Mutation 2 finishes first
      mutReq2.flush({ succeeded: true, message: 'OK', data: cartAfterMutation2, errors: [] });
      expect(service.cart()?.cartId).toBe('cart-mutation-2');
      expect(service.cart()?.totalItems).toBe(3);

      // Mutation 1 finishes second
      mutReq1.flush({ succeeded: true, message: 'OK', data: cartAfterMutation1, errors: [] });

      // Newer mutation result must persist
      expect(service.cart()?.cartId).toBe('cart-mutation-2');
      expect(service.cart()?.totalItems).toBe(3);
      expect(service.loading()).toBe(false);
    });

    // 9. Stale request completion cannot set loading to false while newer request remains in flight
    it('9. should keep loading true while newer concurrent requests remain pending', async () => {
      // Dispatch Request 1
      service.getCart(mockCompanyId).subscribe();
      expect(service.loading()).toBe(true);

      // Dispatch Request 2
      service.getCart(mockCompanyId).subscribe();
      expect(service.loading()).toBe(true);

      const requests = httpMock.match(r => r.method === 'GET');
      expect(requests.length).toBe(2);

      // Complete Request 1
      requests[0].flush({ succeeded: true, message: 'OK', data: sampleCart, errors: [] });

      // Request 2 is still pending, so loading must REMAIN true!
      expect(service.loading()).toBe(true);

      // Complete Request 2
      requests[1].flush({ succeeded: true, message: 'OK', data: sampleCart, errors: [] });

      // All requests done, loading becomes false
      expect(service.loading()).toBe(false);
    });

    // 10. Stale errors cannot overwrite current context error state
    it('10. should discard stale error responses and not overwrite newer context or state', async () => {
      // Set an initial active error on the service
      service.error.set('Active valid error on current company');

      // Dispatch Request 1 under mockCompanyId
      let req1Failed = false;
      service.getCart(mockCompanyId).subscribe({
        error: () => { req1Failed = true; }
      });

      const req1 = httpMock.expectOne(r => r.method === 'GET');

      // User switches context to otherCompanyId
      service.setCompanyContext(otherCompanyId);
      service.error.set('New company context error');

      // Request 1 fails with server error from previous company
      req1.flush(
        { message: 'Stale 500 error from company A' },
        { status: 500, statusText: 'Internal Server Error' }
      );

      // The stale error from Company A must NOT overwrite the new context error
      expect(service.error()).toBe('New company context error');
      expect(service.loading()).toBe(false);
      expect(req1Failed).toBe(true);
    });

    // 11. Checkout dispatches to API, enforces snapshot protection, and resets cart on confirmed success
    it('11. should perform checkout with company context and reset cart on confirmed success', async () => {
      service.setCompanyContext(mockCompanyId);
      // Pre-populate cart
      service.cart.set(sampleCart);
      expect(service.cart()?.lines.length).toBe(1);

      const checkoutPayload: CheckoutRequest = {
        deliveryAddressId: 'addr-001',
        customerNotes: 'Please handle with care.',
        requestedDeliveryDate: '2026-12-10',
        idempotencyKey: '00000000-0000-4000-8000-000000000001',
        sessionId: null
      };

      const mockCheckoutResult: CheckoutResultDto = {
        success: true,
        salesOrderId: 'so-001',
        orderNumber: 'SO-202611-0001',
        orderStatus: 0,
        cartId: 'cart-001',
        totalAmount: 71.88,
        currency: 'USD'
      };

      const checkoutPromise = firstValueFrom(service.checkout(checkoutPayload, mockCompanyId));

      expect(service.loading()).toBe(true);

      const req = httpMock.expectOne(r =>
        r.method === 'POST' &&
        r.url.endsWith('/checkout') &&
        r.params.get('companyId') === mockCompanyId
      );

      expect(req.request.body).toEqual(checkoutPayload);

      req.flush({
        succeeded: true,
        message: 'Order created successfully.',
        data: mockCheckoutResult,
        errors: []
      });

      const result = await checkoutPromise;
      expect(result).toEqual(mockCheckoutResult);
      expect(service.loading()).toBe(false);

      // Cart signal must be updated to CheckedOut status and cleared
      const updatedCart = service.cart();
      expect(updatedCart?.status).toBe(EcommerceCartStatus.CheckedOut);
      expect(updatedCart?.totalItems).toBe(0);
      expect(updatedCart?.grandTotal).toBe(0);
      expect(updatedCart?.lines.length).toBe(0);
    });

    // 12. Stale checkout response discarded if company context switched during in-flight request
    it('12. should discard stale checkout response if company context changed during in-flight request', async () => {
      service.setCompanyContext(mockCompanyId);
      service.cart.set(sampleCart);

      const checkoutPayload: CheckoutRequest = {
        deliveryAddressId: 'addr-001',
        customerNotes: null,
        requestedDeliveryDate: null,
        idempotencyKey: '00000000-0000-4000-8000-000000000002',
        sessionId: null
      };

      const mockCheckoutResult: CheckoutResultDto = {
        success: true,
        salesOrderId: 'so-001',
        orderNumber: 'SO-202611-0001',
        orderStatus: 0,
        cartId: 'cart-001',
        totalAmount: 71.88,
        currency: 'USD'
      };

      service.checkout(checkoutPayload, mockCompanyId).subscribe();

      const req = httpMock.expectOne(r => r.method === 'POST' && r.url.endsWith('/checkout'));

      // Context changes while request in flight
      service.setCompanyContext(otherCompanyId);
      const companyBCart: CartDto = {
        ...sampleCart,
        cartId: 'cart-company-b',
        companyId: otherCompanyId,
        lines: [sampleCartItem]
      };
      service.cart.set(companyBCart);

      // Request completes now
      req.flush({
        succeeded: true,
        message: 'Order created',
        data: mockCheckoutResult,
        errors: []
      });

      // Cart for company B must NOT be reset or overwritten by stale Company A checkout response!
      expect(service.cart()?.companyId).toBe(otherCompanyId);
      expect(service.cart()?.cartId).toBe('cart-company-b');
      expect(service.cart()?.lines.length).toBe(1);
    });
  });
});
