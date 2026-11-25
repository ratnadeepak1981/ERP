import { describe, it, expect, beforeEach, vi } from 'vitest';
import { ComponentFixture, TestBed, getTestBed } from '@angular/core/testing';
import {
  BrowserTestingModule,
  platformBrowserTesting
} from '@angular/platform-browser/testing';
import { provideRouter, Router } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { of, throwError } from 'rxjs';
import { StorefrontComponent } from './storefront.component';
import { EcommerceCatalogueService } from '../../core/services/ecommerce-catalogue.service';
import { EcommerceCartService } from '../../core/services/ecommerce-cart.service';
import { AuthService } from '../../core/auth/auth.service';
import { TenantContextService } from '../../core/services/tenant-context.service';
import { PublicEcommerceProductDto } from '../../core/models/ecommerce-catalogue.models';

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
  // Already initialized
}

describe('StorefrontComponent', () => {
  let component: StorefrontComponent;
  let fixture: ComponentFixture<StorefrontComponent>;
  let catalogueService: EcommerceCatalogueService;
  let cartService: EcommerceCartService;
  let authService: AuthService;
  let router: Router;

  const mockProducts: PublicEcommerceProductDto[] = [
    {
      productId: 'p1',
      productCode: 'PROD-CNC-001',
      productName: 'CNC Milling Spindle 24k',
      onlineDescription: 'High-speed precision machining spindle.',
      displayOrder: 1,
      categoryId: 'cat-machinery',
      categoryName: 'Machinery Components',
      unitOfMeasure: 'PCS',
      sellingPrice: 1250.00,
      currency: 'USD',
      inStock: true,
      leadTime: '3-5 Business Days'
    },
    {
      productId: 'p2',
      productCode: 'PROD-TI-002',
      productName: 'Titanium Grade 5 Hex Fasteners',
      onlineDescription: 'Pack of 100 titanium bolts.',
      displayOrder: 2,
      categoryId: 'cat-hardware',
      categoryName: 'Raw Hardware',
      unitOfMeasure: 'PK',
      sellingPrice: 185.50,
      currency: 'USD',
      inStock: true,
      leadTime: 'Immediate Dispatch'
    },
    {
      productId: 'p3',
      productCode: 'PROD-HYD-003',
      productName: 'Hydraulic Regulator Valve 350 Bar',
      onlineDescription: 'Precision pressure control valve.',
      displayOrder: 3,
      categoryId: 'cat-hydraulics',
      categoryName: 'Hydraulics',
      unitOfMeasure: 'EA',
      sellingPrice: 620.00,
      currency: 'USD',
      inStock: false,
      leadTime: '10-14 Business Days'
    }
  ];

  beforeEach(async () => {
    TestBed.resetTestingModule();

    TestBed.overrideComponent(StorefrontComponent, {
      set: {
        template: '<div>Storefront Test Template</div>'
      }
    });

    await TestBed.configureTestingModule({
      imports: [StorefrontComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    }).compileComponents();

    catalogueService = TestBed.inject(EcommerceCatalogueService);
    cartService = TestBed.inject(EcommerceCartService);
    authService = TestBed.inject(AuthService);
    router = TestBed.inject(Router);
    const tenantContext = TestBed.inject(TenantContextService);
    tenantContext.currentTenant.set({
      isTenant: true,
      tenantId: 'tenant-test-id',
      name: 'Nova Manufacturing',
      subdomain: 'nova',
      industryType: 'Discrete & Process Manufacturing',
      companyStructure: 'Single Company',
      eCommerceEnabled: true
    });

    vi.spyOn(catalogueService, 'getPublicCatalogue').mockReturnValue(of(mockProducts));

    fixture = TestBed.createComponent(StorefrontComponent);
    component = fixture.componentInstance;
  });

  it('should create storefront component and render dynamic tenant branding and products', () => {
    fixture.detectChanges();
    expect(component).toBeTruthy();
    expect(component.rawProducts().length).toBe(3);
    expect(component.filteredProducts().length).toBe(3);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('.brand-title')?.textContent).toContain('Nova Manufacturing Storefront');
    expect(compiled.querySelector('.brand-sub')?.textContent).toContain('Discrete & Process Manufacturing');
    expect(compiled.querySelectorAll('.product-card').length).toBe(3);
  });

  it('should generate dynamic category pills with accurate product counts', () => {
    fixture.detectChanges();
    const categories = component.categories();

    expect(categories.length).toBe(4); // ALL + Machinery Components + Raw Hardware + Hydraulics
    expect(categories.find(c => c.name === 'ALL')?.count).toBe(3);
    expect(categories.find(c => c.name === 'Machinery Components')?.count).toBe(1);
    expect(categories.find(c => c.name === 'Raw Hardware')?.count).toBe(1);
    expect(categories.find(c => c.name === 'Hydraulics')?.count).toBe(1);
  });

  it('should filter products when a category pill is selected', () => {
    fixture.detectChanges();
    component.selectCategory('Hydraulics');
    fixture.detectChanges();

    expect(component.filteredProducts().length).toBe(1);
    expect(component.filteredProducts()[0].productCode).toBe('PROD-HYD-003');
  });

  it('should filter products live via search query', () => {
    fixture.detectChanges();
    component.onSearchChange('titanium');
    fixture.detectChanges();

    expect(component.filteredProducts().length).toBe(1);
    expect(component.filteredProducts()[0].productName).toContain('Titanium Grade 5');
  });

  it('should sort products by price ascending and descending', () => {
    fixture.detectChanges();

    component.onSortChange('price-asc');
    expect(component.filteredProducts()[0].sellingPrice).toBe(185.50);
    expect(component.filteredProducts()[2].sellingPrice).toBe(1250.00);

    component.onSortChange('price-desc');
    expect(component.filteredProducts()[0].sellingPrice).toBe(1250.00);
    expect(component.filteredProducts()[2].sellingPrice).toBe(185.50);
  });

  it('should display empty state when search matches no products and reset on clear', () => {
    fixture.detectChanges();
    component.onSearchChange('nonexistent item 99999');
    fixture.detectChanges();

    expect(component.filteredProducts().length).toBe(0);
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('.empty-container')).not.toBeNull();

    component.clearFilters();
    fixture.detectChanges();
    expect(component.filteredProducts().length).toBe(3);
  });

  it('should navigate to product detail on viewProductDetail', () => {
    const navigateSpy = vi.spyOn(router, 'navigate').mockImplementation(() => Promise.resolve(true));
    component.viewProductDetail('p1');
    expect(navigateSpy).toHaveBeenCalledWith(['/storefront/products', 'p1']);
  });

  it('should display error state when catalogue service fails', () => {
    vi.spyOn(catalogueService, 'getPublicCatalogue').mockReturnValue(throwError(() => new Error('Server error')));
    component.loadCatalogue();
    fixture.detectChanges();

    expect(component.errorMessage()).toContain('Unable to load catalogue items');
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('.error-container')).not.toBeNull();
  });
});
