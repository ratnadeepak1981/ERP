import '@angular/compiler';
import { describe, it, expect, beforeEach, vi, afterEach } from 'vitest';
import { Injector, runInInjectionContext } from '@angular/core';
import { Router, ActivatedRoute, convertToParamMap } from '@angular/router';
import { CustomerOrderConfirmationComponent } from './customer-order-confirmation.component';
import { CheckoutResultDto } from '../../../../../core/models/ecommerce-cart.models';

const mockHistory = {
  state: {} as any,
  replaceState: vi.fn((state: any) => {
    mockHistory.state = state;
  })
};

const mockWindow = {
  print: vi.fn(),
  history: mockHistory
};

(globalThis as any).window = mockWindow;
(globalThis as any).history = mockHistory;

describe('CustomerOrderConfirmationComponent (Stage 4C)', () => {
  const mockOrder: CheckoutResultDto = {
    success: true,
    salesOrderId: 'so-uuid-001',
    orderNumber: 'SO-202611-0042',
    orderStatus: 0,
    cartId: 'cart-001',
    totalAmount: 145.50,
    currency: 'USD'
  };

  let mockRouter: any;
  let mockActivatedRoute: any;
  let queryParamsMap: any;

  beforeEach(() => {
    queryParamsMap = convertToParamMap({});

    mockRouter = {
      navigate: vi.fn(),
      getCurrentNavigation: vi.fn().mockReturnValue(null),
      url: '/customer-portal/checkout/confirmation'
    };

    mockActivatedRoute = {
      snapshot: {
        queryParamMap: queryParamsMap
      }
    };

    // Clean up history.state mock
    if (typeof history !== 'undefined') {
      try {
        history.replaceState({}, '');
      } catch {
        // Ignored in test environment
      }
    }
  });

  afterEach(() => {
    vi.clearAllMocks();
  });

  function createComponent(): CustomerOrderConfirmationComponent {
    const injector = Injector.create({
      providers: [
        { provide: Router, useValue: mockRouter },
        { provide: ActivatedRoute, useValue: mockActivatedRoute }
      ]
    });

    return runInInjectionContext(injector, () => new CustomerOrderConfirmationComponent());
  }

  // 1. Navigation state loading
  it('1. should load order information from router navigation state', () => {
    mockRouter.getCurrentNavigation.mockReturnValue({
      extras: {
        state: { order: mockOrder }
      }
    });

    const comp = createComponent();
    comp.ngOnInit();

    expect(comp.hasOrder()).toBe(true);
    expect(comp.order()).toEqual(mockOrder);
    expect(comp.order()?.orderNumber).toBe('SO-202611-0042');
    expect(comp.statusLabel()).toBe('Draft');
    expect(comp.order()?.totalAmount).toBe(145.50);
    expect(comp.order()?.currency).toBe('USD');
  });

  // 2. History.state fallback loading
  it('2. should load order information from history.state if router state is not present', () => {
    mockRouter.getCurrentNavigation.mockReturnValue(null);
    history.replaceState({ order: mockOrder }, '');

    const comp = createComponent();
    comp.ngOnInit();

    expect(comp.hasOrder()).toBe(true);
    expect(comp.order()?.orderNumber).toBe('SO-202611-0042');
  });

  // 3. Query parameters fallback loading
  it('3. should parse and populate order details from query parameters on page refresh', () => {
    mockRouter.getCurrentNavigation.mockReturnValue(null);
    history.replaceState({}, '');

    mockActivatedRoute.snapshot.queryParamMap = convertToParamMap({
      orderNumber: 'SO-202611-9999',
      salesOrderId: 'so-uuid-9999',
      orderStatus: '0',
      total: '250.75',
      currency: 'EUR',
      cartId: 'cart-9999'
    });

    const comp = createComponent();
    comp.ngOnInit();

    expect(comp.hasOrder()).toBe(true);
    const ord = comp.order()!;
    expect(ord.orderNumber).toBe('SO-202611-9999');
    expect(ord.salesOrderId).toBe('so-uuid-9999');
    expect(comp.statusLabel()).toBe('Draft');
    expect(ord.totalAmount).toBe(250.75);
    expect(ord.currency).toBe('EUR');
  });

  // 4. Direct access without order state
  it('4. should indicate no active order when visited directly without state or query parameters', () => {
    mockRouter.getCurrentNavigation.mockReturnValue(null);
    history.replaceState({}, '');
    mockActivatedRoute.snapshot.queryParamMap = convertToParamMap({});

    const comp = createComponent();
    comp.ngOnInit();

    expect(comp.hasOrder()).toBe(false);
    expect(comp.order()).toBeNull();
  });

  // 5. Navigation actions
  it('5. should navigate to customer orders page when goToOrders is invoked', () => {
    const comp = createComponent();
    comp.goToOrders();
    expect(mockRouter.navigate).toHaveBeenCalledWith(['/customer-portal/orders']);
  });

  it('6. should navigate to catalog page when goToCatalog is invoked', () => {
    const comp = createComponent();
    comp.goToCatalog();
    expect(mockRouter.navigate).toHaveBeenCalledWith(['/customer-portal/catalog']);
  });

  // 7. Print confirmation action
  it('7. should invoke window.print safely when printConfirmation is called', () => {
    mockWindow.print.mockClear();
    const comp = createComponent();
    comp.printConfirmation();
    expect(mockWindow.print).toHaveBeenCalledTimes(1);
  });

  // 8. Breadcrumbs definition
  it('8. should expose structured breadcrumbs for customer portal navigation', () => {
    const comp = createComponent();
    expect(comp.breadcrumbs.length).toBe(3);
    expect(comp.breadcrumbs[0].label).toBe('Customer Portal');
    expect(comp.breadcrumbs[1].label).toBe('Orders');
    expect(comp.breadcrumbs[2].label).toBe('Order Confirmation');
  });
});
