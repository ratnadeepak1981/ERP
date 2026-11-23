import { describe, it, expect, beforeEach, vi } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, ActivatedRoute } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { of } from 'rxjs';
import { ProductDetailComponent } from './product-detail.component';
import { EcommerceCatalogueService } from '../../../core/services/ecommerce-catalogue.service';
import { TenantContextService } from '../../../core/services/tenant-context.service';
import { PublicEcommerceProductDto } from '../../../core/models/ecommerce-catalogue.models';

describe('ProductDetailComponent', () => {
  let component: ProductDetailComponent;
  let fixture: ComponentFixture<ProductDetailComponent>;
  let catalogueService: EcommerceCatalogueService;

  const mockProduct: PublicEcommerceProductDto = {
    productId: 'prod-001',
    productCode: 'CNC-SP-001',
    productName: 'High Precision CNC Milling Spindle',
    onlineDescription: '24,000 RPM precision spindle with ceramic bearings for high duty machining.',
    displayOrder: 1,
    categoryId: 'cat-001',
    categoryName: 'Machinery Components',
    unitOfMeasure: 'EA',
    sellingPrice: 1250.00,
    currency: 'USD',
    inStock: true
  };

  const mockRelated: PublicEcommerceProductDto[] = [
    {
      productId: 'prod-002',
      productCode: 'CNC-COL-002',
      productName: 'Precision ER32 Collet Set',
      onlineDescription: 'Complete set of 18 collets.',
      displayOrder: 2,
      categoryId: 'cat-001',
      categoryName: 'Machinery Components',
      unitOfMeasure: 'SET',
      sellingPrice: 180.00,
      currency: 'USD',
      inStock: true
    }
  ];

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ProductDetailComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        {
          provide: ActivatedRoute,
          useValue: {
            paramMap: of({
              get: (key: string) => (key === 'productId' ? 'prod-001' : null)
            })
          }
        }
      ]
    }).compileComponents();

    catalogueService = TestBed.inject(EcommerceCatalogueService);
    const tenantContext = TestBed.inject(TenantContextService);
    tenantContext.currentTenant.set({
      isTenant: true,
      tenantId: 'tenant-test-id',
      name: 'Nova Manufacturing',
      subdomain: 'nova',
      industryType: 'Discrete Manufacturing',
      companyStructure: 'Single Company',
      eCommerceEnabled: true
    });

    vi.spyOn(catalogueService, 'getPublicProductDetail').mockReturnValue(of(mockProduct));
    vi.spyOn(catalogueService, 'getPublicCatalogue').mockReturnValue(of(mockRelated));

    fixture = TestBed.createComponent(ProductDetailComponent);
    component = fixture.componentInstance;
  });

  it('should create ProductDetailComponent and load product details', () => {
    fixture.detectChanges();
    expect(component).toBeTruthy();
    expect(component.product()).toEqual(mockProduct);
    expect(component.loading()).toBe(false);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('.product-title')?.textContent).toContain('High Precision CNC Milling Spindle');
    expect(compiled.querySelector('.sku-code')?.textContent).toContain('CNC-SP-001');
    expect(compiled.querySelector('.price-val')?.textContent).toContain('USD $1250.00');
  });

  it('should render technical specifications and certified quality badges', () => {
    fixture.detectChanges();
    const compiled = fixture.nativeElement as HTMLElement;

    expect(compiled.textContent).toContain('Machinery Components');
    expect(compiled.textContent).toContain('ISO 9001 Certified');
    expect(compiled.textContent).toContain('Full Batch Traceability');
    expect(compiled.textContent).toContain('Sign In to Order');
  });

  it('should render related products when available', () => {
    fixture.detectChanges();
    expect(component.relatedProducts().length).toBe(1);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('.related-section')).not.toBeNull();
    expect(compiled.textContent).toContain('Precision ER32 Collet Set');
  });

  it('should display empty state when product is not found', () => {
    vi.spyOn(catalogueService, 'getPublicProductDetail').mockReturnValue(of(null));
    component.loadProduct('missing-id');
    fixture.detectChanges();

    expect(component.errorMessage()).toContain('Product not found');
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('.error-container')).not.toBeNull();
  });
});
