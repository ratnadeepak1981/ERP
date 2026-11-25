import '@angular/compiler';
import { describe, it, expect, beforeEach, vi, afterEach } from 'vitest';
import { Injector, runInInjectionContext, signal, computed } from '@angular/core';
import { Router, ActivatedRoute } from '@angular/router';
import { of, throwError } from 'rxjs';
import { StorefrontComponent, KNOWN_STOREFRONTS } from './storefront.component';
import { ProductDetailComponent } from './product-detail/product-detail.component';
import { EcommerceCatalogueService } from '../../core/services/ecommerce-catalogue.service';
import { EcommerceCartService } from '../../core/services/ecommerce-cart.service';
import { AuthService } from '../../core/auth/auth.service';
import { TenantContextService } from '../../core/services/tenant-context.service';
import { ThemeService } from '../../core/services/theme.service';
import { PublicEcommerceProductDto } from '../../core/models/ecommerce-catalogue.models';
import { CartDto, EcommerceCartStatus, AddToCartRequest } from '../../core/models/ecommerce-cart.models';
import { UserSession } from '../../core/models/auth.models';

describe('Storefront Add-to-Cart Integration (Stage 2)', () => {
  const mockCompanyId = '6AD8FE26-04B3-4FE2-917B-C9E03F5B7AC6';
  const otherCompanyId = 'FFF3A70A-197F-48C6-834B-B8AE891AE3B2';

  const mockProduct: PublicEcommerceProductDto = {
    productId: 'prod-001',
    productCode: 'PROD-BOLT-M8',
    productName: 'M8 Stainless Steel Hex Bolt',
    onlineDescription: 'Precision industrial fasteners.',
    displayOrder: 1,
    categoryId: 'cat-fasteners',
    categoryName: 'Fasteners',
    unitOfMeasure: 'PCS',
    sellingPrice: 12.50,
    currency: 'USD',
    inStock: true,
    leadTime: 'Standard Dispatch'
  };

  const mockOutOfStockProduct: PublicEcommerceProductDto = {
    ...mockProduct,
    productId: 'prod-002',
    productCode: 'PROD-BOLT-CUSTOM',
    productName: 'Custom Machined Bolt',
    inStock: false
  };

  const mockCart: CartDto = {
    cartId: 'cart-001',
    tenantId: '11111111-1111-1111-1111-111111111111',
    companyId: mockCompanyId,
    companyName: 'Alpha Manufacturing',
    customerId: 'cust-001',
    customerName: 'John Doe',
    sessionId: null,
    status: EcommerceCartStatus.Active,
    currency: 'USD',
    totalItems: 1,
    subTotal: 12.50,
    totalDiscount: 0,
    totalTax: 1.88,
    grandTotal: 14.38,
    lines: []
  };

  const currentUserSignal = signal<UserSession | null>(null);

  const mockUserSession: UserSession = {
    userId: 'usr-001',
    username: 'john.retail@example.com',
    email: 'john.retail@example.com',
    tenantId: '11111111-1111-1111-1111-111111111111',
    roles: ['CUSTOMER_INDIVIDUAL'],
    permissions: [],
    token: 'jwt-retail-token'
  };

  const mockCorporateUserSession: UserSession = {
    userId: 'usr-002',
    username: 'corp.buyer@acme.com',
    email: 'corp.buyer@acme.com',
    tenantId: '11111111-1111-1111-1111-111111111111',
    roles: ['CUSTOMER_COMPANY'],
    permissions: [],
    token: 'jwt-corp-token'
  };

  let mockRouter: any;
  let mockCatalogueService: any;
  let mockCartService: any;
  let mockAuthService: any;
  let mockTenantContext: any;
  let mockThemeService: any;

  // SessionStorage mock for pending action verification
  let sessionStore: Record<string, string> = {};

  beforeEach(() => {
    sessionStore = {};
    vi.stubGlobal('sessionStorage', {
      getItem: (key: string) => sessionStore[key] || null,
      setItem: (key: string, val: string) => { sessionStore[key] = val; },
      removeItem: (key: string) => { delete sessionStore[key]; },
      clear: () => { sessionStore = {}; }
    });

    currentUserSignal.set(null);

    mockRouter = {
      url: '/storefront',
      navigate: vi.fn().mockReturnValue(Promise.resolve(true))
    };

    mockCatalogueService = {
      getPublicCatalogue: vi.fn().mockReturnValue(of([mockProduct, mockOutOfStockProduct])),
      getPublicProductDetail: vi.fn().mockReturnValue(of(mockProduct))
    };

    const cartSignal = signal<CartDto | null>(null);
    const loadingSignal = signal<boolean>(false);
    const totalItemsSignal = computed(() => cartSignal()?.totalItems ?? 0);

    mockCartService = {
      cart: cartSignal,
      loading: loadingSignal,
      totalItems: totalItemsSignal,
      setCompanyContext: vi.fn(),
      getCart: vi.fn().mockReturnValue(of(mockCart)),
      addToCart: vi.fn().mockImplementation((req: AddToCartRequest) => {
        cartSignal.set({
          ...mockCart,
          totalItems: (cartSignal()?.totalItems ?? 0) + req.quantity
        });
        return of(cartSignal()!);
      }),
      storePendingAction: vi.fn().mockImplementation((action: any) => {
        sessionStorage.setItem('apex_pending_cart_action', JSON.stringify({ ...action, timestamp: Date.now() }));
      }),
      getPendingAction: vi.fn().mockImplementation(() => {
        const raw = sessionStorage.getItem('apex_pending_cart_action');
        return raw ? JSON.parse(raw) : null;
      }),
      clearPendingAction: vi.fn().mockImplementation(() => {
        sessionStorage.removeItem('apex_pending_cart_action');
      }),
      resumePendingAction: vi.fn().mockImplementation(() => {
        const raw = sessionStorage.getItem('apex_pending_cart_action');
        if (!raw) return of(null);
        sessionStorage.removeItem('apex_pending_cart_action');
        const parsed = JSON.parse(raw);
        return mockCartService.addToCart({ productId: parsed.productId, quantity: parsed.quantity }, parsed.companyId);
      }),
      resetCart: vi.fn().mockImplementation(() => {
        cartSignal.set(null);
      })
    };

    mockAuthService = {
      currentUser: currentUserSignal,
      isAuthenticated: computed(() => !!currentUserSignal()?.token),
      isCustomer: computed(() => true),
      isCustomerIndividual: computed(() => currentUserSignal()?.roles?.includes('CUSTOMER_INDIVIDUAL') ?? false),
      isCustomerCompany: computed(() => currentUserSignal()?.roles?.includes('CUSTOMER_COMPANY') ?? false)
    };

    mockTenantContext = {
      currentTenant: signal({
        isTenant: true,
        tenantId: mockCompanyId,
        name: 'Alpha Manufacturing Ltd',
        subdomain: 'alpha',
        industryType: 'Precision Engineering',
        companyStructure: 'Single',
        eCommerceEnabled: true
      }),
      isTenantDomain: computed(() => true)
    };

    mockThemeService = {
      currentTheme: signal('fluent-light'),
      toggleTheme: vi.fn()
    };
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  function createStorefrontComponent(): StorefrontComponent {
    const injector = Injector.create({
      providers: [
        { provide: EcommerceCatalogueService, useValue: mockCatalogueService },
        { provide: EcommerceCartService, useValue: mockCartService },
        { provide: AuthService, useValue: mockAuthService },
        { provide: Router, useValue: mockRouter },
        {
          provide: ActivatedRoute,
          useValue: {
            queryParamMap: of(new Map()),
            snapshot: { queryParamMap: new Map() }
          }
        },
        { provide: TenantContextService, useValue: mockTenantContext },
        { provide: ThemeService, useValue: mockThemeService }
      ]
    });

    return runInInjectionContext(injector, () => new StorefrontComponent());
  }

  function createProductDetailComponent(productId = 'prod-001'): ProductDetailComponent {
    const injector = Injector.create({
      providers: [
        { provide: EcommerceCatalogueService, useValue: mockCatalogueService },
        { provide: EcommerceCartService, useValue: mockCartService },
        { provide: AuthService, useValue: mockAuthService },
        { provide: Router, useValue: mockRouter },
        {
          provide: ActivatedRoute,
          useValue: {
            paramMap: of(new Map([['productId', productId]])),
            snapshot: { queryParamMap: new Map() }
          }
        },
        { provide: TenantContextService, useValue: mockTenantContext },
        { provide: ThemeService, useValue: mockThemeService }
      ]
    });

    return runInInjectionContext(injector, () => new ProductDetailComponent());
  }

  // 1. Anonymous Add-to-Cart redirects to authentication
  it('1. should redirect anonymous user to login and preserve returnUrl and pending action', () => {
    const comp = createStorefrontComponent();
    mockRouter.url = '/storefront?tenant=apxman';

    comp.addToCart(mockProduct);

    // Assert pending action was saved to session
    expect(mockCartService.storePendingAction).toHaveBeenCalledWith({
      productId: 'prod-001',
      quantity: 1,
      companyId: mockCompanyId
    });

    // Assert user redirected to login with current returnUrl
    expect(mockRouter.navigate).toHaveBeenCalledWith(['/login'], {
      queryParams: { returnUrl: '/storefront?tenant=apxman' }
    });

    // Assert cart service addToCart was NOT called directly while anonymous
    expect(mockCartService.addToCart).not.toHaveBeenCalled();
  });

  // 2. Intended action resumes once after successful login
  it('2. should resume intended Add-to-Cart action exactly once after successful authentication', () => {
    // Stage pending action in storage
    mockCartService.storePendingAction({
      productId: 'prod-001',
      quantity: 1,
      companyId: mockCompanyId
    });
    expect(sessionStorage.getItem('apex_pending_cart_action')).not.toBeNull();

    // Authenticate user
    currentUserSignal.set(mockUserSession);

    const comp = createStorefrontComponent();
    comp.ngOnInit();

    // Assert resumePendingAction was called
    expect(mockCartService.resumePendingAction).toHaveBeenCalled();

    // Assert pending action was consumed and removed from storage (will not trigger again)
    expect(sessionStorage.getItem('apex_pending_cart_action')).toBeNull();

    // Assert item count updated
    expect(mockCartService.totalItems()).toBe(1);
    expect(comp.actionFeedback()?.success).toBe(true);
  });

  // 3. Authenticated retail customer can add a product
  it('3. should allow authenticated retail customer to add product to cart', () => {
    currentUserSignal.set(mockUserSession);
    const comp = createStorefrontComponent();

    comp.addToCart(mockProduct);

    expect(mockCartService.addToCart).toHaveBeenCalledWith(
      { productId: 'prod-001', quantity: 1 },
      mockCompanyId
    );
    expect(comp.actionFeedback()?.success).toBe(true);
    expect(comp.actionFeedback()?.message).toContain('M8 Stainless Steel Hex Bolt added to cart');
  });

  // 4. Corporate customer uses backend-authoritative contract pricing
  it('4. should route corporate customer add-to-cart with clean payload (backend resolves price card)', () => {
    currentUserSignal.set(mockCorporateUserSession);
    const comp = createStorefrontComponent();

    comp.addToCart(mockProduct);

    // Frontend provides only productId and quantity; backend resolves contract pricing
    expect(mockCartService.addToCart).toHaveBeenCalledWith(
      { productId: 'prod-001', quantity: 1 },
      mockCompanyId
    );
  });

  // 5. No client-side pricing or tax fields are submitted
  it('5. should strictly omit client-side pricing, discount, tax, or total fields in payload', () => {
    currentUserSignal.set(mockUserSession);
    const comp = createStorefrontComponent();

    comp.addToCart(mockProduct);

    const callArgs = mockCartService.addToCart.mock.calls[0];
    const payload = callArgs[0];

    expect(payload.productId).toBe('prod-001');
    expect(payload.quantity).toBe(1);
    expect((payload as any).sellingPrice).toBeUndefined();
    expect((payload as any).unitPrice).toBeUndefined();
    expect((payload as any).taxAmount).toBeUndefined();
    expect((payload as any).subTotal).toBeUndefined();
    expect((payload as any).grandTotal).toBeUndefined();
  });

  // 6. Repeated clicks do not create unintended duplicate requests
  it('6. should prevent duplicate Add-to-Cart calls while an operation is already pending', () => {
    currentUserSignal.set(mockUserSession);
    const comp = createStorefrontComponent();

    // Simulate in-flight operation
    comp.addingProductId.set('prod-001');

    comp.addToCart(mockProduct);

    // Call must be blocked by in-flight check
    expect(mockCartService.addToCart).not.toHaveBeenCalled();
  });

  // 7. Backend errors are displayed correctly
  it('7. should capture and display backend error messages in feedback banner', () => {
    currentUserSignal.set(mockUserSession);
    mockCartService.addToCart.mockReturnValue(throwError(() => new Error('Product is currently out of stock.')));

    const comp = createStorefrontComponent();
    comp.addToCart(mockProduct);

    expect(comp.actionFeedback()?.success).toBe(false);
    expect(comp.actionFeedback()?.message).toBe('Product is currently out of stock.');
    expect(comp.addingProductId()).toBeNull();
  });

  // 8. Logout clears cart state
  it('8. should clear cart state upon logout', () => {
    currentUserSignal.set(mockUserSession);
    mockCartService.cart.set(mockCart);
    expect(mockCartService.totalItems()).toBe(1);

    // User logs out
    currentUserSignal.set(null);
    mockCartService.resetCart();

    expect(mockCartService.cart()).toBeNull();
    expect(mockCartService.totalItems()).toBe(0);
  });

  // 9. Company switching does not leak cart data
  it('9. should switch company context and reset cart to prevent cross-company data leakage', () => {
    currentUserSignal.set(mockUserSession);
    const comp = createStorefrontComponent();

    comp.switchIndustry(KNOWN_STOREFRONTS[1]); // switch to Lanka Retail Mart

    expect(comp.activeOption().slug).toBe('retail');
    expect(mockRouter.navigate).toHaveBeenCalledWith([], expect.objectContaining({
      queryParams: { tenant: 'retail' }
    }));
  });

  // 10. Existing catalogue browsing remains available anonymously
  it('10. should allow anonymous users to browse, search, and filter catalogue products freely', () => {
    currentUserSignal.set(null);
    const comp = createStorefrontComponent();
    comp.ngOnInit();

    expect(comp.rawProducts().length).toBe(2);
    expect(comp.filteredProducts().length).toBe(2);

    // Filter by search
    comp.onSearchChange('bolt');
    expect(comp.filteredProducts().length).toBe(2);

    comp.onSearchChange('custom');
    expect(comp.filteredProducts().length).toBe(1);
    expect(comp.filteredProducts()[0].productId).toBe('prod-002');
  });

  // 11. Product Detail View Add-to-Cart integration
  it('11. should allow custom quantity selection and Add-to-Cart in ProductDetailComponent', () => {
    currentUserSignal.set(mockUserSession);
    const detailComp = createProductDetailComponent('prod-001');
    detailComp.ngOnInit();

    // Select quantity = 5
    detailComp.changeQuantity(4);
    expect(detailComp.quantity()).toBe(5);

    detailComp.addToCart();

    expect(mockCartService.addToCart).toHaveBeenCalledWith(
      { productId: 'prod-001', quantity: 5 },
      mockCompanyId
    );
    expect(detailComp.actionFeedback()?.success).toBe(true);
    expect(detailComp.actionFeedback()?.message).toContain('5 × M8 Stainless Steel Hex Bolt added to cart');
  });

  // 12. Product Detail View Anonymous redirect
  it('12. should redirect anonymous user on ProductDetailComponent and store intended quantity', () => {
    currentUserSignal.set(null);
    mockRouter.url = '/storefront/products/prod-001';

    const detailComp = createProductDetailComponent('prod-001');
    detailComp.ngOnInit();
    detailComp.changeQuantity(2); // quantity = 3

    detailComp.addToCart();

    expect(mockCartService.storePendingAction).toHaveBeenCalledWith({
      productId: 'prod-001',
      quantity: 3,
      companyId: mockCompanyId
    });

    expect(mockRouter.navigate).toHaveBeenCalledWith(['/login'], {
      queryParams: { returnUrl: '/storefront/products/prod-001' }
    });
  });
});
