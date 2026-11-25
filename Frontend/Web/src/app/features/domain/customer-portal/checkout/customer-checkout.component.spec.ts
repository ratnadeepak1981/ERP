import '@angular/compiler';
import { describe, it, expect, beforeEach, vi, afterEach } from 'vitest';
import { Injector, runInInjectionContext, signal } from '@angular/core';
import { Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import { CustomerCheckoutComponent } from './customer-checkout.component';
import { EcommerceCartService } from '../../../../core/services/ecommerce-cart.service';
import { CustomerAddressService } from '../../../../core/services/customer-address.service';
import { AuthService } from '../../../../core/auth/auth.service';
import { TenantContextService } from '../../../../core/services/tenant-context.service';
import { CartDto, CartItemDto, CheckoutRequest, CheckoutResultDto, EcommerceCartStatus } from '../../../../core/models/ecommerce-cart.models';
import { CustomerAddressPortalDto } from '../../../../core/models/customer-address.models';
import { UserSession } from '../../../../core/models/auth.models';

describe('CustomerCheckoutComponent (Stage 4A)', () => {
  const mockCompanyId = '6AD8FE26-04B3-4FE2-917B-C9E03F5B7AC6';

  const mockItem1: CartItemDto = {
    lineId: 'line-001',
    productId: 'prod-001',
    productCode: 'PROD-BOLT-M8',
    productName: 'M8 Stainless Steel Hex Bolt',
    unitOfMeasureId: 'uom-001',
    unitOfMeasureCode: 'PCS',
    unitOfMeasureName: 'Pieces',
    productPackagingId: 'pkg-001',
    packagingName: 'Box of 100',
    quantity: 2,
    unitPrice: 15.00,
    subTotal: 30.00,
    discountRate: 10,
    discountAmount: 3.00,
    taxCodeId: 'tax-001',
    taxCode: 'VAT-15',
    taxRate: 15,
    taxAmount: 4.05,
    lineTotal: 31.05,
    isTaxInclusive: false,
    currency: 'USD',
    isAvailable: true,
    validationMessage: null
  };

  const mockCart: CartDto = {
    cartId: 'cart-001',
    tenantId: '11111111-1111-1111-1111-111111111111',
    companyId: mockCompanyId,
    companyName: 'Alpha Manufacturing Ltd',
    customerId: 'cust-001',
    customerName: 'Acme Industrial Corp',
    sessionId: null,
    status: EcommerceCartStatus.Active,
    currency: 'USD',
    totalItems: 2,
    subTotal: 30.00,
    totalDiscount: 3.00,
    totalTax: 4.05,
    grandTotal: 31.05,
    lines: [mockItem1]
  };

  const mockEmptyCart: CartDto = {
    ...mockCart,
    totalItems: 0,
    subTotal: 0,
    totalDiscount: 0,
    totalTax: 0,
    grandTotal: 0,
    lines: []
  };

  const mockCheckoutResult: CheckoutResultDto = {
    success: true,
    salesOrderId: 'so-001',
    orderNumber: 'SO-202611-0001',
    orderStatus: 0,
    cartId: 'cart-001',
    totalAmount: 31.05,
    currency: 'USD'
  };

  const mockAddressDefault: CustomerAddressPortalDto = {
    customerAddressId: 'ca-001',
    addressId: 'addr-001',
    addressName: 'Main Warehouse Dock',
    addressLine1: '100 Industrial Parkway',
    addressLine2: 'Bay 4',
    city: 'Detroit',
    stateProvince: 'MI',
    postalCode: '48201',
    country: 'United States',
    addressTypeId: 'at-001',
    addressTypeCode: 'DELIVERY',
    addressTypeName: 'Delivery / Shipping',
    isDefault: true,
    isActive: true
  };

  const mockAddressSecondary: CustomerAddressPortalDto = {
    customerAddressId: 'ca-002',
    addressId: 'addr-002',
    addressName: 'Assembly Plant 2',
    addressLine1: '250 Factory Lane',
    addressLine2: null,
    city: 'Flint',
    stateProvince: 'MI',
    postalCode: '48502',
    country: 'United States',
    addressTypeId: 'at-001',
    addressTypeCode: 'DELIVERY',
    addressTypeName: 'Delivery / Shipping',
    isDefault: false,
    isActive: true
  };

  const mockSession: UserSession = {
    userId: 'user-001',
    userName: 'customer_user',
    email: 'purchasing@acme.com',
    fullName: 'Jane Doe',
    accessToken: 'token-xyz',
    refreshToken: 'refresh-xyz',
    expiresAt: new Date(Date.now() + 3600000).toISOString(),
    roles: ['Customer', 'CUSTOMER_COMPANY_ADMIN'],
    permissions: ['portal:access'],
    tenantId: '11111111-1111-1111-1111-111111111111',
    companyId: mockCompanyId
  };

  let mockRouter: any;
  let mockAuthService: any;
  let mockTenantContext: any;
  let mockCartService: any;
  let mockAddressService: any;

  let cartSignal: any;
  let loadingCartSignal: any;
  let cartErrorSignal: any;

  beforeEach(() => {
    cartSignal = signal<CartDto | null>(null);
    loadingCartSignal = signal<boolean>(false);
    cartErrorSignal = signal<string | null>(null);

    mockRouter = {
      navigate: vi.fn(),
      url: '/customer-portal/checkout'
    };

    mockAuthService = {
      isAuthenticated: vi.fn().mockReturnValue(true),
      currentUser: signal<UserSession | null>(mockSession),
      isCustomerCompany: vi.fn().mockReturnValue(true)
    };

    mockTenantContext = {
      currentTenant: signal<{ tenantId: string; name: string } | null>({
        tenantId: mockCompanyId,
        name: 'Alpha Manufacturing Ltd'
      })
    };

    mockCartService = {
      cart: cartSignal,
      loading: loadingCartSignal,
      error: cartErrorSignal,
      setCompanyContext: vi.fn(),
      getCart: vi.fn().mockImplementation(() => {
        cartSignal.set(mockCart);
        return of(mockCart);
      }),
      checkout: vi.fn().mockReturnValue(of(mockCheckoutResult))
    };

    mockAddressService = {
      getAddresses: vi.fn().mockReturnValue(of([mockAddressDefault, mockAddressSecondary]))
    };
  });

  afterEach(() => {
    vi.clearAllMocks();
  });

  function createComponent(): CustomerCheckoutComponent {
    const injector = Injector.create({
      providers: [
        { provide: Router, useValue: mockRouter },
        { provide: AuthService, useValue: mockAuthService },
        { provide: TenantContextService, useValue: mockTenantContext },
        { provide: EcommerceCartService, useValue: mockCartService },
        { provide: CustomerAddressService, useValue: mockAddressService }
      ]
    });

    return runInInjectionContext(injector, () => new CustomerCheckoutComponent());
  }

  // 1. Checkout Component Rendering
  it('1. should render checkout component and load active cart and addresses on init', () => {
    const comp = createComponent();
    comp.ngOnInit();

    expect(mockAuthService.isAuthenticated).toHaveBeenCalled();
    expect(mockCartService.setCompanyContext).toHaveBeenCalledWith(mockCompanyId);
    expect(mockCartService.getCart).toHaveBeenCalledWith(mockCompanyId);
    expect(mockAddressService.getAddresses).toHaveBeenCalled();
    expect(comp.cart()).toEqual(mockCart);
    expect(comp.addresses().length).toBe(2);
  });

  // 2. Authentication Protection
  it('2. should redirect unauthenticated visitor to /login with returnUrl', () => {
    mockAuthService.isAuthenticated.mockReturnValue(false);
    const comp = createComponent();
    comp.ngOnInit();

    expect(mockRouter.navigate).toHaveBeenCalledWith(['/login'], {
      queryParams: { returnUrl: '/customer-portal/checkout' }
    });
    expect(mockCartService.getCart).not.toHaveBeenCalled();
    expect(mockAddressService.getAddresses).not.toHaveBeenCalled();
  });

  // 3. Address Loading and Auto-Selection of Default Address
  it('3. should auto-select the default customer address upon loading', () => {
    const comp = createComponent();
    comp.ngOnInit();

    expect(comp.selectedAddressId()).toBe('addr-001');
    expect(comp.addresses()[0].isDefault).toBe(true);
  });

  // 4. Manual Address Selection
  it('4. should allow customer to select an alternative valid delivery address', () => {
    const comp = createComponent();
    comp.ngOnInit();

    comp.selectAddress('addr-002');
    expect(comp.selectedAddressId()).toBe('addr-002');
  });

  // 5. No Saved Addresses State
  it('5. should handle zero saved addresses gracefully and disable order button', () => {
    mockAddressService.getAddresses.mockReturnValue(of([]));
    const comp = createComponent();
    comp.ngOnInit();

    expect(comp.addresses().length).toBe(0);
    expect(comp.selectedAddressId()).toBeNull();
    expect(comp.canProceedToOrder()).toBe(false);
  });

  // 6. Address API Failure Handling
  it('6. should capture address API failure reactively without breaking component', () => {
    mockAddressService.getAddresses.mockReturnValue(throwError(() => new Error('Address service unavailable.')));
    const comp = createComponent();
    comp.ngOnInit();

    expect(comp.loadingAddresses()).toBe(false);
    expect(comp.addressError()).toBe('Address service unavailable.');
    expect(comp.errorState()).toBe('Address service unavailable.');
    expect(comp.canProceedToOrder()).toBe(false);
  });

  // 7. Empty Cart Review Fallback
  it('7. should display empty state when cart has no lines and provide navigation to catalog', () => {
    mockCartService.getCart.mockReturnValue(of(mockEmptyCart));
    cartSignal.set(mockEmptyCart);

    const comp = createComponent();
    comp.ngOnInit();

    expect(comp.cart()).toEqual(mockEmptyCart);
    expect(comp.canProceedToOrder()).toBe(false);

    comp.goToCatalog();
    expect(mockRouter.navigate).toHaveBeenCalledWith(['/customer-portal/catalog']);
  });

  // 8. Final Cart Review Accuracy & Authoritative Totals
  it('8. should expose authoritative cart totals and lines from backend without client computation', () => {
    const comp = createComponent();
    comp.ngOnInit();

    const c = comp.cart()!;
    expect(c.subTotal).toBe(30.00);
    expect(c.totalDiscount).toBe(3.00);
    expect(c.totalTax).toBe(4.05);
    expect(c.grandTotal).toBe(31.05);
    expect(c.currency).toBe('USD');

    const item = c.lines[0];
    expect(item.productCode).toBe('PROD-BOLT-M8');
    expect(item.quantity).toBe(2);
    expect(item.lineTotal).toBe(31.05);
    expect(item.packagingName).toBe('Box of 100');
  });

  // 9. Order Notes and Supported Delivery Date Input
  it('9. should capture customer notes and requested delivery date', () => {
    const comp = createComponent();
    comp.ngOnInit();

    // Notes
    const notesEvent = { target: { value: 'Receiving dock open until 4 PM.' } } as unknown as Event;
    comp.onCustomerNotesChange(notesEvent);
    expect(comp.customerNotes()).toBe('Receiving dock open until 4 PM.');

    // Future date
    const futureDate = '2026-12-01';
    const dateEvent = { target: { value: futureDate } } as unknown as Event;
    comp.onDeliveryDateChange(dateEvent);
    expect(comp.requestedDeliveryDate()).toBe(futureDate);
  });

  // 10. Rejection of Past Delivery Date
  it('10. should reject past delivery date and notify customer', () => {
    const comp = createComponent();
    comp.ngOnInit();

    const pastDate = '2020-01-01';
    const dateEvent = { target: { value: pastDate } } as unknown as Event;
    comp.onDeliveryDateChange(dateEvent);

    expect((dateEvent.target as any).value).toBe('');
    expect(comp.requestedDeliveryDate()).toBe('');
    expect(comp.actionFeedback()?.success).toBe(false);
    expect(comp.actionFeedback()?.message).toBe('Requested delivery date cannot be in the past.');
  });

  // 11. Checkout Dispatch and Draft Sales Order Creation (Stage 4B)
  it('11. should dispatch checkout request with valid payload and update state upon order creation', () => {
    const comp = createComponent();
    comp.ngOnInit();

    expect(comp.canProceedToOrder()).toBe(true);

    // Provide customer notes and requested delivery date
    const notesEvent = { target: { value: 'Urgent delivery needed.' } } as unknown as Event;
    comp.onCustomerNotesChange(notesEvent);

    const futureDate = '2026-12-15';
    const dateEvent = { target: { value: futureDate } } as unknown as Event;
    comp.onDeliveryDateChange(dateEvent);

    comp.onPlaceOrderClick();

    expect(mockCartService.checkout).toHaveBeenCalledTimes(1);
    const [payload, companyId] = mockCartService.checkout.mock.calls[0];

    expect(payload.deliveryAddressId).toBe('addr-001');
    expect(payload.customerNotes).toBe('Urgent delivery needed.');
    expect(payload.requestedDeliveryDate).toBe('2026-12-15');
    expect(payload.idempotencyKey).toBeDefined();
    expect(payload.idempotencyKey.length).toBeGreaterThan(0);
    expect(companyId).toBe(mockCompanyId);

    expect(comp.checkoutResult()).toEqual(mockCheckoutResult);
    expect(comp.isSubmitting()).toBe(false);
    expect(comp.actionFeedback()?.success).toBe(true);
    expect(comp.actionFeedback()?.message).toContain('SO-202611-0001');
    expect(comp.actionFeedback()?.message).toContain('Draft');
    expect(mockRouter.navigate).toHaveBeenCalledWith(
      ['/customer-portal/checkout/confirmation'],
      expect.objectContaining({ state: { order: mockCheckoutResult } })
    );
  });

  // 12. Block Order Button If Address Not Selected
  it('12. should block order action and notify customer if no delivery address is selected', () => {
    const comp = createComponent();
    comp.ngOnInit();

    comp.selectedAddressId.set(null);
    expect(comp.canProceedToOrder()).toBe(false);

    comp.onPlaceOrderClick();
    expect(comp.actionFeedback()?.success).toBe(false);
    expect(comp.actionFeedback()?.message).toBe('Please select a delivery address to proceed.');
    expect(mockCartService.checkout).not.toHaveBeenCalled();
  });

  // 13. Cryptographically Valid UUID v4 Idempotency Key
  it('13. should generate a cryptographically valid UUID v4 idempotency key on submission', () => {
    const comp = createComponent();
    comp.ngOnInit();

    comp.onPlaceOrderClick();

    const [payload] = mockCartService.checkout.mock.calls[0];
    const uuidRegex = /^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;
    expect(payload.idempotencyKey).toMatch(uuidRegex);
  });

  // 14. Preservation and Reuse of Idempotency Key on Retry
  it('14. should preserve and reuse the exact same idempotency key on failure or retry', () => {
    mockCartService.checkout.mockReturnValueOnce(throwError(() => new Error('Network timeout.')));

    const comp = createComponent();
    comp.ngOnInit();

    // First attempt fails
    comp.onPlaceOrderClick();
    expect(mockCartService.checkout).toHaveBeenCalledTimes(1);
    const firstKey = mockCartService.checkout.mock.calls[0][0].idempotencyKey;
    expect(comp.currentIdempotencyKey()).toBe(firstKey);
    expect(comp.actionFeedback()?.success).toBe(false);
    expect(comp.actionFeedback()?.message).toBe('Network timeout.');
    expect(comp.isSubmitting()).toBe(false);

    // Second attempt (retry)
    mockCartService.checkout.mockReturnValueOnce(of(mockCheckoutResult));
    comp.onPlaceOrderClick();
    expect(mockCartService.checkout).toHaveBeenCalledTimes(2);
    const secondKey = mockCartService.checkout.mock.calls[1][0].idempotencyKey;

    // Must be the identical idempotency key!
    expect(secondKey).toBe(firstKey);
    expect(comp.checkoutResult()).toEqual(mockCheckoutResult);
    expect(comp.currentIdempotencyKey()).toBeNull(); // Cleared after success
  });

  // 15. Submission Locking and In-Flight Duplicate Prevention
  it('15. should prevent duplicate submissions while an order is currently in-flight', () => {
    const comp = createComponent();
    comp.ngOnInit();

    comp.isSubmitting.set(true);
    comp.onPlaceOrderClick();

    expect(mockCartService.checkout).not.toHaveBeenCalled();
  });

  // 16. Empty Cart Submission Guard
  it('16. should reject order submission if cart is empty or null', () => {
    mockCartService.getCart.mockReturnValue(of(mockEmptyCart));
    cartSignal.set(mockEmptyCart);
    const comp = createComponent();
    comp.ngOnInit();

    comp.onPlaceOrderClick();

    expect(mockCartService.checkout).not.toHaveBeenCalled();
    expect(comp.actionFeedback()?.success).toBe(false);
    expect(comp.actionFeedback()?.message).toBe('Cannot checkout an empty shopping cart.');
  });

  // 17. Explicit Selling Company Resolution
  it('17. should pass selling company ID explicitly to the checkout service', () => {
    const comp = createComponent();
    comp.ngOnInit();

    comp.onPlaceOrderClick();

    const companyIdPassed = mockCartService.checkout.mock.calls[0][1];
    expect(companyIdPassed).toBe(mockCompanyId);
  });

  // 18. Clearing Idempotency Key on Confirmed Success
  it('18. should clear idempotency key after successful order placement so future orders get fresh keys', () => {
    const comp = createComponent();
    comp.ngOnInit();

    comp.onPlaceOrderClick();
    expect(mockCartService.checkout).toHaveBeenCalledTimes(1);
    const firstKey = mockCartService.checkout.mock.calls[0][0].idempotencyKey;
    expect(comp.currentIdempotencyKey()).toBeNull();

    // Trigger another checkout after cart reset
    comp.onPlaceOrderClick();
    expect(mockCartService.checkout).toHaveBeenCalledTimes(2);
    const secondKey = mockCartService.checkout.mock.calls[1][0].idempotencyKey;

    expect(secondKey).not.toBe(firstKey);
  });
});
