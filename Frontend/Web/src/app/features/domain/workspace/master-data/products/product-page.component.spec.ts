import { describe, it, expect, beforeEach, vi } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router, ActivatedRoute } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { of, throwError } from 'rxjs';
import { ProductPageComponent } from './product-page.component';
import { MasterDataService } from '../../../../../core/services/master-data.service';
import { WorkspaceScopeService } from '../../../../../core/services/workspace-scope.service';
import { ProductItem, CategoryItem, UnitOfMeasureItem } from '../../../../../core/models/scope.models';

describe('ProductPageComponent', () => {
  let component: ProductPageComponent;
  let fixture: ComponentFixture<ProductPageComponent>;
  let masterDataService: MasterDataService;
  let router: Router;

  const mockCategories: CategoryItem[] = [
    {
      categoryId: 'cat-1',
      tenantId: 't1',
      categoryCode: 'CAT-01',
      categoryName: 'Industrial Metals',
      description: 'Metal items',
      isActive: true
    }
  ];

  const mockUnits: UnitOfMeasureItem[] = [
    {
      unitOfMeasureId: 'uom-1',
      tenantId: 't1',
      code: 'PCS',
      name: 'Pieces',
      description: 'Standard item unit',
      isActive: true
    }
  ];

  const mockProduct: ProductItem = {
    productId: 'p-101',
    tenantId: 't1',
    productCode: 'PRD-001',
    productName: 'Galvanized Sheet 2mm',
    categoryId: 'cat-1',
    unitOfMeasure: 'PCS',
    standardCost: 25.5,
    sellingPrice: 38.0,
    reorderLevel: 10,
    reorderQuantity: 50,
    isPurchasable: true,
    isSellable: true,
    isManufacturable: false,
    canConsumeInProduction: true,
    canConsumeInMaintenance: false,
    isStockTracked: true,
    productType: 1,
    trackingMode: 0,
    valuationMethod: 1,
    isActive: true,
    description: 'High-grade galvanized metal sheet'
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ProductPageComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        MasterDataService,
        WorkspaceScopeService,
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: {
                get: (key: string) => (key === 'id' ? null : null)
              }
            },
            paramMap: of({
              get: (key: string) => (key === 'id' ? null : null)
            })
          }
        }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(ProductPageComponent);
    component = fixture.componentInstance;
    masterDataService = TestBed.inject(MasterDataService);
    router = TestBed.inject(Router);

    vi.spyOn(masterDataService, 'getCategories').mockReturnValue(of(mockCategories));
    vi.spyOn(masterDataService, 'getUnitsOfMeasure').mockReturnValue(of(mockUnits));
  });

  it('should initialize in create mode with default fields and empty code/name', () => {
    fixture.detectChanges();

    expect(component).toBeTruthy();
    expect(component.mode()).toBe('create');
    expect(component.pageTitle()).toBe('New Product Definition');
    expect(component.productForm.get('productCode')?.value).toBe('');
    expect(component.productForm.get('productName')?.value).toBe('');
    expect(component.productForm.get('unitOfMeasure')?.value).toBe('PCS');
    expect(component.productForm.get('isPurchasable')?.value).toBe(true);
    expect(component.productForm.get('isStockTracked')?.value).toBe(true);
  });

  it('should validate form and reject save when required fields are missing', () => {
    fixture.detectChanges();

    component.productForm.patchValue({
      productCode: '',
      productName: ''
    });

    component.saveProduct();

    expect(component.productForm.invalid).toBe(true);
    expect(component.message()?.severity).toBe('danger');
    expect(component.message()?.text).toContain('Please correct the highlighted form errors');
  });

  it('should save new product and navigate to product master list', () => {
    const createSpy = vi.spyOn(masterDataService, 'createProduct').mockReturnValue(of(mockProduct));
    const navSpy = vi.spyOn(router, 'navigate');

    fixture.detectChanges();

    component.productForm.patchValue({
      productCode: 'PRD-001',
      productName: 'Galvanized Sheet 2mm',
      categoryId: 'cat-1',
      unitOfMeasure: 'PCS',
      standardCost: 25.5,
      sellingPrice: 38.0,
      reorderLevel: 10,
      isPurchasable: true,
      isSellable: true,
      isManufacturable: false,
      canConsumeInProduction: true,
      isStockTracked: true,
      description: 'High-grade galvanized metal sheet'
    });

    component.saveProduct();

    expect(createSpy).toHaveBeenCalledWith(
      expect.objectContaining({
        productCode: 'PRD-001',
        productName: 'Galvanized Sheet 2mm',
        standardCost: 25.5,
        sellingPrice: 38.0
      })
    );
    expect(component.message()?.severity).toBe('success');
  });

  it('should load product and disable code in edit mode', () => {
    const route = TestBed.inject(ActivatedRoute);
    vi.spyOn(route.snapshot.paramMap, 'get').mockImplementation((k: string) => (k === 'id' ? 'p-101' : null));
    vi.spyOn(masterDataService, 'getProduct').mockReturnValue(of(mockProduct));

    fixture.detectChanges();

    expect(component.mode()).toBe('edit');
    expect(component.productId()).toBe('p-101');
    expect(component.pageTitle()).toContain('PRD-001');
    expect(component.productForm.get('productCode')?.value).toBe('PRD-001');
    expect(component.productForm.get('productCode')?.disabled).toBe(true);
    expect(component.productForm.get('productName')?.value).toBe('Galvanized Sheet 2mm');
    expect(component.productForm.get('standardCost')?.value).toBe(25.5);
  });

  it('should update existing product in edit mode', () => {
    const route = TestBed.inject(ActivatedRoute);
    vi.spyOn(route.snapshot.paramMap, 'get').mockImplementation((k: string) => (k === 'id' ? 'p-101' : null));
    vi.spyOn(masterDataService, 'getProduct').mockReturnValue(of(mockProduct));
    const updateSpy = vi.spyOn(masterDataService, 'updateProduct').mockReturnValue(of(mockProduct));

    fixture.detectChanges();

    component.productForm.patchValue({
      productName: 'Galvanized Sheet 2mm — Updated'
    });

    component.saveProduct();

    expect(updateSpy).toHaveBeenCalledWith(
      'p-101',
      expect.objectContaining({
        productName: 'Galvanized Sheet 2mm — Updated'
      })
    );
    expect(component.message()?.severity).toBe('success');
  });

  it('should display server error message when API call fails', () => {
    vi.spyOn(masterDataService, 'createProduct').mockReturnValue(
      throwError(() => ({ error: { message: 'Product code PRD-001 already exists in tenant scope.' } }))
    );

    fixture.detectChanges();

    component.productForm.patchValue({
      productCode: 'PRD-001',
      productName: 'Test Item',
      categoryId: 'cat-1',
      unitOfMeasure: 'PCS'
    });

    component.saveProduct();

    expect(component.saving()).toBe(false);
    expect(component.message()?.severity).toBe('danger');
    expect(component.message()?.text).toContain('already exists');
  });
});
