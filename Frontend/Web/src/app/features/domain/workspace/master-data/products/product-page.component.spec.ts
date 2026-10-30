import { describe, it, expect, beforeEach, vi } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router, ActivatedRoute } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { of, throwError } from 'rxjs';
import { ProductPageComponent } from './product-page.component';
import { MasterDataService } from '../../../../../core/services/master-data.service';
import { WorkspaceScopeService } from '../../../../../core/services/workspace-scope.service';
import {
  ProductItem,
  CategoryItem,
  UnitOfMeasureItem,
  BrandItem,
  ProductPackagingItem,
  ProductType,
  TrackingMode,
  ValuationMethod
} from '../../../../../core/models/scope.models';

describe('ProductPageComponent — New / Edit / View Synchronization', () => {
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

  const mockBrands: BrandItem[] = [
    {
      brandId: 'brand-1',
      tenantId: 't1',
      brandCode: 'BRD-01',
      brandName: 'Apex Precision',
      description: 'In-house brand',
      isActive: true
    }
  ];

  const mockPackagings: ProductPackagingItem[] = [
    {
      productPackagingId: 'pkg-1',
      tenantId: 't1',
      productId: 'p-101',
      packagingUomId: 'uom-box',
      packagingCode: 'BOX-24',
      packagingName: 'Carton of 24',
      baseQuantity: 24,
      purchaseAllowed: true,
      salesAllowed: true,
      isActive: true
    }
  ];

  const mockProduct: ProductItem = {
    productId: 'p-101',
    tenantId: 't1',
    productCode: 'PRD-001',
    productName: 'Galvanized Sheet 2mm',
    categoryId: 'cat-1',
    brandId: 'brand-1',
    unitOfMeasure: 'PCS',
    unitOfMeasureId: 'uom-1',
    standardCost: 25.5,
    sellingPrice: 38.0,
    reorderLevel: 10,
    reorderQuantity: 50,
    minStockQuantity: 15,
    maxStockQuantity: 100,
    safetyStockQuantity: 5,
    leadTimeDays: 7,
    isPurchasable: true,
    isSellable: true,
    isManufacturable: false,
    canConsumeInProduction: true,
    canConsumeInMaintenance: false,
    isStockTracked: true,
    productType: ProductType.StockItem,
    trackingMode: TrackingMode.Batch,
    valuationMethod: ValuationMethod.FIFO,
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
            url: of([])
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
    vi.spyOn(masterDataService, 'getBrands').mockReturnValue(of(mockBrands));
    vi.spyOn(masterDataService, 'getProductPackagings').mockReturnValue(of(mockPackagings));
  });

  it('1. should initialize in Create mode with standard enterprise defaults and enabled controls', () => {
    fixture.detectChanges();

    expect(component).toBeTruthy();
    expect(component.mode()).toBe('create');
    expect(component.pageTitle()).toBe('New Product Definition');
    expect(component.productForm.get('productCode')?.value).toBe('');
    expect(component.productForm.get('productCode')?.enabled).toBe(true);
    expect(component.productForm.get('productName')?.value).toBe('');
    expect(component.productForm.get('categoryId')?.value).toBe('cat-1');
    expect(component.productForm.get('unitOfMeasure')?.value).toBe('PCS');
    expect(component.productForm.get('unitOfMeasureId')?.value).toBe('uom-1');
    expect(component.productForm.get('productType')?.value).toBe(ProductType.StockItem);
    expect(component.productForm.get('isStockTracked')?.value).toBe(true);
    expect(component.productForm.get('isPurchasable')?.value).toBe(true);
    expect(component.productForm.get('isSellable')?.value).toBe(true);
    expect(component.productForm.get('isActive')?.value).toBe(true);
  });

  it('2. should reject save in Create mode when mandatory fields are missing', () => {
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

  it('3. should save product in Create mode with complete backend DTO payload mapping', () => {
    const createSpy = vi.spyOn(masterDataService, 'createProduct').mockReturnValue(of(mockProduct));

    fixture.detectChanges();

    component.productForm.patchValue({
      productCode: 'PRD-001',
      productName: 'Galvanized Sheet 2mm',
      categoryId: 'cat-1',
      brandId: 'brand-1',
      productType: ProductType.StockItem,
      unitOfMeasure: 'PCS',
      unitOfMeasureId: 'uom-1',
      standardCost: 25.5,
      sellingPrice: 38.0,
      reorderLevel: 10,
      reorderQuantity: 50,
      minStockQuantity: 15,
      maxStockQuantity: 100,
      safetyStockQuantity: 5,
      leadTimeDays: 7,
      isPurchasable: true,
      isSellable: true,
      isManufacturable: false,
      canConsumeInProduction: true,
      canConsumeInMaintenance: false,
      isStockTracked: true,
      trackingMode: TrackingMode.Batch,
      valuationMethod: ValuationMethod.FIFO,
      description: 'High-grade galvanized metal sheet'
    });

    component.saveProduct();

    expect(createSpy).toHaveBeenCalledWith(
      expect.objectContaining({
        productCode: 'PRD-001',
        productName: 'Galvanized Sheet 2mm',
        categoryId: 'cat-1',
        brandId: 'brand-1',
        productType: ProductType.StockItem,
        unitOfMeasure: 'PCS',
        unitOfMeasureId: 'uom-1',
        standardCost: 25.5,
        sellingPrice: 38.0,
        reorderLevel: 10,
        reorderQuantity: 50,
        minStockQuantity: 15,
        maxStockQuantity: 100,
        safetyStockQuantity: 5,
        leadTimeDays: 7,
        isStockTracked: true,
        trackingMode: TrackingMode.Batch
      })
    );
    expect(component.message()?.severity).toBe('success');
  });

  it('4. should load existing product into View mode with all controls disabled/read-only', () => {
    const route = TestBed.inject(ActivatedRoute);
    vi.spyOn(route.snapshot.paramMap, 'get').mockImplementation((k: string) => (k === 'id' ? 'p-101' : null));
    vi.spyOn(masterDataService, 'getProduct').mockReturnValue(of(mockProduct));

    // Current URL represents /app/master-data/products/p-101 (View mode)
    (router as any).url = '/app/master-data/products/p-101';

    fixture.detectChanges();

    expect(component.mode()).toBe('view');
    expect(component.productId()).toBe('p-101');
    expect(component.pageTitle()).toBe('View Product — PRD-001');
    expect(component.productForm.disabled).toBe(true);
    expect(component.productForm.get('productCode')?.value).toBe('PRD-001');
    expect(component.productForm.get('productName')?.value).toBe('Galvanized Sheet 2mm');
    expect(component.productForm.get('brandId')?.value).toBe('brand-1');
    expect(component.productForm.get('minStockQuantity')?.value).toBe(15);
    expect(component.productForm.get('maxStockQuantity')?.value).toBe(100);
    expect(component.productForm.get('safetyStockQuantity')?.value).toBe(5);
    expect(component.packagings().length).toBe(1);
    expect(component.packagings()[0].packagingCode).toBe('BOX-24');
  });

  it('5. should load existing product into Edit mode with productCode immutable and fields editable', () => {
    const route = TestBed.inject(ActivatedRoute);
    vi.spyOn(route.snapshot.paramMap, 'get').mockImplementation((k: string) => (k === 'id' ? 'p-101' : null));
    vi.spyOn(masterDataService, 'getProduct').mockReturnValue(of(mockProduct));

    // Current URL represents /app/master-data/products/p-101/edit (Edit mode)
    (router as any).url = '/app/master-data/products/p-101/edit';

    fixture.detectChanges();

    expect(component.mode()).toBe('edit');
    expect(component.productId()).toBe('p-101');
    expect(component.pageTitle()).toBe('Edit Product — PRD-001');
    expect(component.productForm.get('productCode')?.value).toBe('PRD-001');
    expect(component.productForm.get('productCode')?.disabled).toBe(true);
    expect(component.productForm.get('productName')?.enabled).toBe(true);
    expect(component.productForm.get('standardCost')?.enabled).toBe(true);
  });

  it('6. should update existing product in Edit mode and transition to View mode', () => {
    const route = TestBed.inject(ActivatedRoute);
    vi.spyOn(route.snapshot.paramMap, 'get').mockImplementation((k: string) => (k === 'id' ? 'p-101' : null));
    vi.spyOn(masterDataService, 'getProduct').mockReturnValue(of(mockProduct));
    const updateSpy = vi.spyOn(masterDataService, 'updateProduct').mockReturnValue(of(mockProduct));

    (router as any).url = '/app/master-data/products/p-101/edit';

    fixture.detectChanges();

    component.productForm.patchValue({
      productName: 'Galvanized Sheet 2mm — Updated'
    });

    component.saveProduct();

    expect(updateSpy).toHaveBeenCalledWith(
      'p-101',
      expect.objectContaining({
        productCode: 'PRD-001',
        productName: 'Galvanized Sheet 2mm — Updated'
      })
    );
    expect(component.message()?.severity).toBe('success');
  });

  it('7. should enforce Planning Threshold hierarchy: SafetyStock <= MinStock <= MaxStock', () => {
    fixture.detectChanges();

    component.productForm.patchValue({
      productCode: 'PRD-002',
      productName: 'Threshold Test',
      categoryId: 'cat-1',
      minStockQuantity: 50,
      maxStockQuantity: 30, // Invalid: min > max
      safetyStockQuantity: 10
    });

    component.saveProduct();

    expect(component.message()?.severity).toBe('danger');
    expect(component.message()?.text).toContain('Minimum stock quantity cannot be greater than maximum stock quantity');
  });

  it('8. should automatically disable stock tracking and traceability mode when Service or Non-Stock is selected', () => {
    fixture.detectChanges();

    expect(component.productForm.get('isStockTracked')?.value).toBe(true);

    // Switch to Service Item
    component.productForm.get('productType')?.setValue(ProductType.Service);

    expect(component.productForm.get('isStockTracked')?.value).toBe(false);
    expect(component.productForm.get('isStockTracked')?.disabled).toBe(true);
    expect(component.productForm.get('trackingMode')?.value).toBe(TrackingMode.None);
    expect(component.productForm.get('trackingMode')?.disabled).toBe(true);
  });
});
