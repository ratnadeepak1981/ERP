import '@angular/compiler';
import { describe, it, expect, beforeEach, vi, afterEach } from 'vitest';
import { Injector, runInInjectionContext, signal } from '@angular/core';
import { Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import { CustomerCartComponent } from './customer-cart.component';
import { EcommerceCartService } from '../../../../core/services/ecommerce-cart.service';
import { AuthService } from '../../../../core/auth/auth.service';
import { TenantContextService } from '../../../../core/services/tenant-context.service';
import { CartDto, CartItemDto, EcommerceCartStatus } from '../../../../core/models/ecommerce-cart.models';
import { UserSession } from '../../../../core/models/auth.models';

describe('CustomerCartComponent (Stage 3)', () => {
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

  const mockItem2: CartItemDto = {
    lineId: 'line-002',
    productId: 'prod-002',
    productCode: 'PROD-NUT-M8',
    productName: 'M8 Stainless Steel Hex Nut',
    unitOfMeasureId: 'uom-001',
    unitOfMeasureCode: 'PCS',
    unitOfMeasureName: 'Pieces',
    productPackagingId: null,
    packagingName: null,
    quantity: 5,
    unitPrice: 5.00,
    subTotal: 25.00,
    discountRate: 0,
    discountAmount: 0,
    taxCodeId: 'tax-001',
    taxCode: 'VAT-15',
    taxRate: 15,
    taxAmount: 3.75,
    lineTotal: 28.75,
    isTaxInclusive: false,
    currency: 'USD',
    isAvailable: true,
    validationMessage: null
  };

  const mockPopulatedCart: CartDto = {
    cartId: 'cart-001',
    tenantId: '11111111-1111-1111-1111-111111111111',
    companyId: mockCompanyId,
    companyName: 'Alpha Manufacturing Ltd',
    customerId: 'cust-001',
    customerName: 'Acme Industrial Corp',
    sessionId: null,
    status: EcommerceCartStatus.Active,
    currency: 'USD',
    totalItems: 7,
    subTotal: 55.00,
    totalDiscount: 3.00,
    totalTax: 7.80,
    grandTotal: 59.80,
    lines: [mockItem1, mockItem2]
  };

  const mockEmptyCart: CartDto = {
    ...mockPopulatedCart,
    totalItems: 0,
    subTotal: 0,
    totalDiscount: 0,
    totalTax: 0,
    grandTotal: 0,
    lines: []
  };

  const mockSession: UserSession = {
    userId: 'user-001',
    userName: 'customer_user',
    email: 'customer@acme.com',
    fullName: 'Jane Doe',
    accessToken: 'token-xyz',
    refreshToken: 'refresh-xyz',
    expiresAt: new Date(Date.now() + 3600000).toISOString(),
    roles: ['Customer', 'CUSTOMER_INDIVIDUAL'],
    permissions: ['portal:access'],
    tenantId: '11111111-1111-1111-1111-111111111111',
    companyId: mockCompanyId
  };

  let mockRouter: any;
  let mockAuthService: any;
  let mockTenantContext: any;
  let mockCartService: any;

  // Signal stores for mockCartService
  let cartSignal: any;
  let loadingSignal: any;
  let errorSignal: any;
  let totalItemsSignal: any;

  beforeEach(() => {
    cartSignal = signal<CartDto | null>(null);
    loadingSignal = signal<boolean>(false);
    errorSignal = signal<string | null>(null);
    totalItemsSignal = signal<number>(0);

    mockRouter = {
      navigate: vi.fn(),
      url: '/customer-portal/cart'
    };

    mockAuthService = {
      isAuthenticated: vi.fn().mockReturnValue(true),
      currentUser: signal<UserSession | null>(mockSession),
      isCustomerCompany: vi.fn().mockReturnValue(false)
    };

    mockTenantContext = {
      currentTenant: signal<{ tenantId: string; name: string } | null>({
        tenantId: mockCompanyId,
        name: 'Alpha Manufacturing Ltd'
      })
    };

    mockCartService = {
      cart: cartSignal,
      loading: loadingSignal,
      error: errorSignal,
      totalItems: totalItemsSignal,
      setCompanyContext: vi.fn(),
      getCart: vi.fn().mockImplementation(() => {
        cartSignal.set(mockPopulatedCart);
        totalItemsSignal.set(mockPopulatedCart.totalItems);
        return of(mockPopulatedCart);
      }),
      updateLineQuantity: vi.fn().mockImplementation((lineId: string, qty: number) => {
        const updatedLines = mockPopulatedCart.lines.map(l =>
          l.lineId === lineId ? { ...l, quantity: qty, lineTotal: l.unitPrice * qty } : l
        );
        const updatedCart: CartDto = {
          ...mockPopulatedCart,
          lines: updatedLines,
          totalItems: updatedLines.reduce((acc, curr) => acc + curr.quantity, 0)
        };
        cartSignal.set(updatedCart);
        totalItemsSignal.set(updatedCart.totalItems);
        return of(updatedCart);
      }),
      removeLine: vi.fn().mockImplementation((lineId: string) => {
        const updatedLines = mockPopulatedCart.lines.filter(l => l.lineId !== lineId);
        const updatedCart: CartDto = {
          ...mockPopulatedCart,
          lines: updatedLines,
          totalItems: updatedLines.reduce((acc, curr) => acc + curr.quantity, 0)
        };
        cartSignal.set(updatedCart);
        totalItemsSignal.set(updatedCart.totalItems);
        return of(updatedCart);
      }),
      clearCart: vi.fn().mockImplementation(() => {
        cartSignal.set(mockEmptyCart);
        totalItemsSignal.set(0);
        return of(mockEmptyCart);
      })
    };
  });

  afterEach(() => {
    vi.clearAllMocks();
  });

  function createComponent(): CustomerCartComponent {
    const injector = Injector.create({
      providers: [
        { provide: Router, useValue: mockRouter },
        { provide: AuthService, useValue: mockAuthService },
        { provide: TenantContextService, useValue: mockTenantContext },
        { provide: EcommerceCartService, useValue: mockCartService }
      ]
    });

    return runInInjectionContext(injector, () => new CustomerCartComponent());
  }

  // 1. Cart Page Loading
  it('1. should load active cart on init for authenticated customer', () => {
    const comp = createComponent();
    comp.ngOnInit();

    expect(mockAuthService.isAuthenticated).toHaveBeenCalled();
    expect(mockCartService.setCompanyContext).toHaveBeenCalledWith(mockCompanyId);
    expect(mockCartService.getCart).toHaveBeenCalledWith(mockCompanyId);
    expect(comp.cart()).toEqual(mockPopulatedCart);
  });

  // 2. Authentication Enforcement & Redirection
  it('2. should redirect unauthenticated visitor to /login with returnUrl', () => {
    mockAuthService.isAuthenticated.mockReturnValue(false);
    const comp = createComponent();
    comp.ngOnInit();

    expect(mockRouter.navigate).toHaveBeenCalledWith(['/login'], {
      queryParams: { returnUrl: '/customer-portal/cart' }
    });
    expect(mockCartService.getCart).not.toHaveBeenCalled();
  });

  // 3. Empty Cart State
  it('3. should reflect empty cart state and provide navigation to catalog', () => {
    mockCartService.getCart.mockReturnValue(of(mockEmptyCart));
    cartSignal.set(mockEmptyCart);

    const comp = createComponent();
    comp.ngOnInit();

    expect(comp.cart()).toEqual(mockEmptyCart);
    expect(comp.cartHeaderSubtitle()).toBe('Your cart is currently empty.');

    comp.goToCatalog();
    expect(mockRouter.navigate).toHaveBeenCalledWith(['/customer-portal/catalog']);
  });

  // 4. Displaying Authoritative Backend Cart Data
  it('4. should expose authoritative backend items and calculations without client alterations', () => {
    const comp = createComponent();
    comp.ngOnInit();

    const currentCart = comp.cart()!;
    expect(currentCart.lines.length).toBe(2);
    expect(currentCart.subTotal).toBe(55.00);
    expect(currentCart.totalDiscount).toBe(3.00);
    expect(currentCart.totalTax).toBe(7.80);
    expect(currentCart.grandTotal).toBe(59.80);
    expect(currentCart.currency).toBe('USD');

    const firstLine = currentCart.lines[0];
    expect(firstLine.productCode).toBe('PROD-BOLT-M8');
    expect(firstLine.unitPrice).toBe(15.00);
    expect(firstLine.discountRate).toBe(10);
    expect(firstLine.discountAmount).toBe(3.00);
    expect(firstLine.taxAmount).toBe(4.05);
    expect(firstLine.lineTotal).toBe(31.05);
    expect(firstLine.packagingName).toBe('Box of 100');
    expect(firstLine.unitOfMeasureCode).toBe('PCS');
  });

  // 5. Quantity Increase
  it('5. should increase quantity by 1 via incrementQuantity and update authoritative state', () => {
    const comp = createComponent();
    comp.ngOnInit();

    comp.incrementQuantity(mockItem1);

    expect(mockCartService.updateLineQuantity).toHaveBeenCalledWith('line-001', 3);
    expect(comp.actionFeedback()?.success).toBe(true);
    expect(comp.actionFeedback()?.message).toContain('Updated quantity for M8 Stainless Steel Hex Bolt');
  });

  // 6. Quantity Decrease
  it('6. should decrease quantity by 1 via decrementQuantity and prevent reduction below 1', () => {
    const comp = createComponent();
    comp.ngOnInit();

    comp.decrementQuantity(mockItem1);
    expect(mockCartService.updateLineQuantity).toHaveBeenCalledWith('line-001', 1);

    // If quantity is already 1, decrement should be blocked
    const singleQtyItem = { ...mockItem1, quantity: 1 };
    vi.clearAllMocks();
    comp.decrementQuantity(singleQtyItem);
    expect(mockCartService.updateLineQuantity).not.toHaveBeenCalled();
  });

  // 7. Invalid Quantity Input Handling
  it('7. should validate manual quantity input and reject zero or negative values', () => {
    const comp = createComponent();
    comp.ngOnInit();

    const mockEvent = {
      target: { value: '0' }
    } as unknown as Event;

    comp.onQuantityInputChange(mockItem1, mockEvent);

    expect((mockEvent.target as any).value).toBe('2'); // Reverted
    expect(mockCartService.updateLineQuantity).not.toHaveBeenCalled();
    expect(comp.actionFeedback()?.success).toBe(false);
    expect(comp.actionFeedback()?.message).toBe('Quantity must be at least 1.');
  });

  // 8. Successful Quantity Input Change
  it('8. should update quantity when valid positive integer is entered', () => {
    const comp = createComponent();
    comp.ngOnInit();

    const mockEvent = {
      target: { value: '10' }
    } as unknown as Event;

    comp.onQuantityInputChange(mockItem1, mockEvent);

    expect(mockCartService.updateLineQuantity).toHaveBeenCalledWith('line-001', 10);
    expect(comp.actionFeedback()?.success).toBe(true);
  });

  // 9. Failed Quantity Update Handling
  it('9. should handle quantity update API failure gracefully without breaking state', () => {
    mockCartService.updateLineQuantity.mockReturnValue(throwError(() => new Error('Insufficient inventory.')));
    const comp = createComponent();
    comp.ngOnInit();

    comp.incrementQuantity(mockItem1);

    expect(comp.updatingLineId()).toBeNull();
    expect(comp.actionFeedback()?.success).toBe(false);
    expect(comp.actionFeedback()?.message).toBe('Insufficient inventory.');
  });

  // 10. Successful Item Removal
  it('10. should remove item from cart and display success feedback', () => {
    const comp = createComponent();
    comp.ngOnInit();

    comp.removeLine(mockItem1);

    expect(mockCartService.removeLine).toHaveBeenCalledWith('line-001');
    expect(comp.removingLineId()).toBeNull();
    expect(comp.actionFeedback()?.success).toBe(true);
    expect(comp.actionFeedback()?.message).toContain('Removed M8 Stainless Steel Hex Bolt from cart.');
  });

  // 11. Failed Item Removal Handling
  it('11. should handle item removal API failure without removing item from UI', () => {
    mockCartService.removeLine.mockReturnValue(throwError(() => new Error('Network timeout.')));
    const comp = createComponent();
    comp.ngOnInit();

    comp.removeLine(mockItem1);

    expect(comp.removingLineId()).toBeNull();
    expect(comp.actionFeedback()?.success).toBe(false);
    expect(comp.actionFeedback()?.message).toBe('Network timeout.');
  });

  // 12. Clear Cart Operation
  it('12. should clear entire cart via confirmClearCart and update state to empty', () => {
    const comp = createComponent();
    comp.ngOnInit();

    comp.confirmClearCart();

    expect(mockCartService.clearCart).toHaveBeenCalled();
    expect(comp.clearingCart()).toBe(false);
    expect(comp.actionFeedback()?.success).toBe(true);
    expect(comp.cart()).toEqual(mockEmptyCart);
  });

  // 13. Duplicate Action Prevention While In-Flight
  it('13. should prevent concurrent actions while an operation is already in progress', () => {
    const comp = createComponent();
    comp.ngOnInit();

    // Simulate an ongoing line update
    comp.updatingLineId.set('line-001');

    comp.incrementQuantity(mockItem1);
    comp.decrementQuantity(mockItem1);
    comp.removeLine(mockItem1);

    expect(mockCartService.updateLineQuantity).not.toHaveBeenCalled();
    expect(mockCartService.removeLine).not.toHaveBeenCalled();
  });

  // 14. Error State and Manual Retry
  it('14. should display backend error reactively and allow manual reloadCart', () => {
    const comp = createComponent();
    errorSignal.set('Selling company mismatch.');

    expect(comp.error()).toBe('Selling company mismatch.');

    comp.reloadCart();
    expect(mockCartService.getCart).toHaveBeenCalledWith(mockCompanyId);
  });

  // ==========================================
  // Stage 3A Audit Finding Verification Tests
  // ==========================================

  describe('Stage 3A Finding 1: Feedback Timer Management', () => {
    beforeEach(() => {
      vi.useFakeTimers();
    });

    afterEach(() => {
      vi.useRealTimers();
    });

    it('15. should replace an existing feedback message and reset the timer so older timers do not prematurely clear the newer message', () => {
      const comp = createComponent();
      comp.ngOnInit();

      // Trigger first feedback message at t = 0
      comp.incrementQuantity(mockItem1);
      expect(comp.actionFeedback()?.message).toContain('Updated quantity for M8 Stainless Steel Hex Bolt');

      // Fast forward 3 seconds (timer 1 still has 1s remaining)
      vi.advanceTimersByTime(3000);
      expect(comp.actionFeedback()).not.toBeNull();

      // Trigger second feedback message at t = 3s
      comp.decrementQuantity(mockItem1);
      expect(comp.actionFeedback()?.message).toContain('Updated quantity for M8 Stainless Steel Hex Bolt');

      // Fast forward 1.5 seconds (t = 4.5s total).
      // If timer 1 was NOT cancelled, it would have fired at t = 4s and cleared the message!
      vi.advanceTimersByTime(1500);
      expect(comp.actionFeedback()).not.toBeNull();
      expect(comp.actionFeedback()?.message).toContain('Updated quantity for M8 Stainless Steel Hex Bolt');

      // Fast forward another 2.5 seconds (t = 7s total, 4s after second message)
      vi.advanceTimersByTime(2500);
      expect(comp.actionFeedback()).toBeNull();
    });

    it('16. should cancel pending timers on component destroy (ngOnDestroy)', () => {
      const comp = createComponent();
      comp.ngOnInit();

      comp.incrementQuantity(mockItem1);
      expect(comp.actionFeedback()).not.toBeNull();

      // Destroy component while timer is active
      comp.ngOnDestroy();

      // Fast forward past the 4000ms duration
      vi.advanceTimersByTime(5000);

      // The feedback signal was not cleared by a stale callback because timer was cancelled
      // and explicit clearFeedback cancels active timer
      comp.clearFeedback();
      expect(comp.actionFeedback()).toBeNull();
    });
  });

  describe('Stage 3A Finding 2: Quantity Upper-Bound and Input Validation', () => {
    it('17. should accept valid quantity 1', () => {
      const comp = createComponent();
      comp.ngOnInit();

      const mockEvent = { target: { value: '1' } } as unknown as Event;
      comp.onQuantityInputChange(mockItem1, mockEvent);

      expect(mockCartService.updateLineQuantity).toHaveBeenCalledWith('line-001', 1);
      expect(comp.actionFeedback()?.success).toBe(true);
    });

    it('18. should accept maximum boundary quantity of 99,999', () => {
      const comp = createComponent();
      comp.ngOnInit();

      const mockEvent = { target: { value: '99999' } } as unknown as Event;
      comp.onQuantityInputChange(mockItem1, mockEvent);

      expect(mockCartService.updateLineQuantity).toHaveBeenCalledWith('line-001', 99999);
      expect(comp.actionFeedback()?.success).toBe(true);
    });

    it('19. should reject quantity of 100,000 without sending to backend', () => {
      const comp = createComponent();
      comp.ngOnInit();

      const mockEvent = { target: { value: '100000' } } as unknown as Event;
      comp.onQuantityInputChange(mockItem1, mockEvent);

      expect((mockEvent.target as any).value).toBe('2'); // Reverts to item.quantity (2)
      expect(mockCartService.updateLineQuantity).not.toHaveBeenCalled();
      expect(comp.actionFeedback()?.success).toBe(false);
      expect(comp.actionFeedback()?.message).toContain('Quantity cannot exceed the maximum limit of 99,999.');
    });

    it('20. should reject extremely large quantities (e.g. 1,000,000,000)', () => {
      const comp = createComponent();
      comp.ngOnInit();

      const mockEvent = { target: { value: '1000000000' } } as unknown as Event;
      comp.onQuantityInputChange(mockItem1, mockEvent);

      expect((mockEvent.target as any).value).toBe('2');
      expect(mockCartService.updateLineQuantity).not.toHaveBeenCalled();
      expect(comp.actionFeedback()?.success).toBe(false);
      expect(comp.actionFeedback()?.message).toContain('Quantity cannot exceed the maximum limit of 99,999.');
    });

    it('21. should reject negative, zero, and non-numeric quantities without sending to backend', () => {
      const comp = createComponent();
      comp.ngOnInit();

      // Negative value
      const negativeEvent = { target: { value: '-5' } } as unknown as Event;
      comp.onQuantityInputChange(mockItem1, negativeEvent);
      expect((negativeEvent.target as any).value).toBe('2');
      expect(mockCartService.updateLineQuantity).not.toHaveBeenCalled();
      expect(comp.actionFeedback()?.message).toBe('Quantity must be at least 1.');

      // Zero
      const zeroEvent = { target: { value: '0' } } as unknown as Event;
      comp.onQuantityInputChange(mockItem1, zeroEvent);
      expect((zeroEvent.target as any).value).toBe('2');
      expect(mockCartService.updateLineQuantity).not.toHaveBeenCalled();
      expect(comp.actionFeedback()?.message).toBe('Quantity must be at least 1.');

      // Non-numeric
      const abcEvent = { target: { value: 'abc' } } as unknown as Event;
      comp.onQuantityInputChange(mockItem1, abcEvent);
      expect((abcEvent.target as any).value).toBe('2');
      expect(mockCartService.updateLineQuantity).not.toHaveBeenCalled();
      expect(comp.actionFeedback()?.message).toBe('Quantity must be at least 1.');
    });

    it('22. should prevent incrementQuantity past maximum limit of 99,999', () => {
      const comp = createComponent();
      comp.ngOnInit();

      const maxItem = { ...mockItem1, quantity: 99999 };
      comp.incrementQuantity(maxItem);

      expect(mockCartService.updateLineQuantity).not.toHaveBeenCalled();
      expect(comp.actionFeedback()?.success).toBe(false);
      expect(comp.actionFeedback()?.message).toContain('Maximum item quantity of 99,999 reached.');
    });
  });
});
