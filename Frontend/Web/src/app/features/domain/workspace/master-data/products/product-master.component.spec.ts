import { describe, it, expect, beforeEach, vi } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { ProductMasterComponent } from './product-master.component';
import { MasterDataService } from '../../../../../core/services/master-data.service';
import { of } from 'rxjs';
import { ProductItem } from '../../../../../core/models/scope.models';

describe('ProductMasterComponent', () => {
  let component: ProductMasterComponent;
  let fixture: ComponentFixture<ProductMasterComponent>;
  let masterDataService: MasterDataService;

  const mockProducts: ProductItem[] = [
    {
      productId: 'p1',
      tenantId: 't1',
      productCode: 'PRD-001',
      productName: 'Example Item A',
      categoryId: 'cat-1',
      unitOfMeasure: 'Each',
      isPurchasable: true,
      isSellable: true,
      isManufacturable: false,
      canConsumeInProduction: false,
      canConsumeInMaintenance: false,
      isStockTracked: true,
      productType: 1,
      trackingMode: 0,
      valuationMethod: 1,
      standardCost: 10,
      sellingPrice: 20,
      reorderLevel: 5,
      reorderQuantity: 20,
      isActive: true
    },
    {
      productId: 'p2',
      tenantId: 't1',
      productCode: 'PRD-002',
      productName: 'Example Item B',
      categoryId: 'cat-1',
      unitOfMeasure: 'Kg',
      isPurchasable: true,
      isSellable: false,
      isManufacturable: true,
      canConsumeInProduction: true,
      canConsumeInMaintenance: false,
      isStockTracked: true,
      productType: 1,
      trackingMode: 0,
      valuationMethod: 1,
      standardCost: 5,
      sellingPrice: 0,
      reorderLevel: 2,
      reorderQuantity: 10,
      isActive: false
    }
  ];

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ProductMasterComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        MasterDataService
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(ProductMasterComponent);
    component = fixture.componentInstance;
    masterDataService = TestBed.inject(MasterDataService);
  });

  it('should create and load products table matching Image 5', () => {
    vi.spyOn(masterDataService, 'getProducts').mockReturnValue(of(mockProducts));
    vi.spyOn(masterDataService, 'getCategories').mockReturnValue(of([]));
    vi.spyOn(masterDataService, 'getUnitsOfMeasure').mockReturnValue(of([]));

    fixture.detectChanges();

    expect(component).toBeTruthy();
    expect(component.products().length).toBe(2);
    expect(component.filteredProducts().length).toBe(2);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('PRD-001');
    expect(compiled.textContent).toContain('Example Item A');
    expect(compiled.textContent).toContain('Purchase, Sell');
  });

  it('should filter products by search term', () => {
    vi.spyOn(masterDataService, 'getProducts').mockReturnValue(of(mockProducts));
    fixture.detectChanges();

    component.searchTerm.set('Item B');
    expect(component.filteredProducts().length).toBe(1);
    expect(component.filteredProducts()[0].productCode).toBe('PRD-002');
  });

  it('should filter products by active status', () => {
    vi.spyOn(masterDataService, 'getProducts').mockReturnValue(of(mockProducts));
    fixture.detectChanges();

    component.activeStatusFilter.set('active');
    expect(component.filteredProducts().length).toBe(1);
    expect(component.filteredProducts()[0].productCode).toBe('PRD-001');
  });

  it('should filter products by purchasable and sellable flags', () => {
    vi.spyOn(masterDataService, 'getProducts').mockReturnValue(of(mockProducts));
    fixture.detectChanges();

    component.sellableOnly.set(true);
    expect(component.filteredProducts().length).toBe(1);
    expect(component.filteredProducts()[0].productCode).toBe('PRD-001');
  });
});
