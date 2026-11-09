import { describe, it, expect, beforeEach, vi } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient, HttpErrorResponse } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { ProductMasterComponent } from './product-master.component';
import { MasterDataService } from '../../../../../core/services/master-data.service';
import { of, throwError } from 'rxjs';
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

  describe('F2 authorization and error states', () => {
    beforeEach(() => {
      vi.spyOn(masterDataService, 'getCategories').mockReturnValue(of([]));
      vi.spyOn(masterDataService, 'getUnitsOfMeasure').mockReturnValue(of([]));
    });

    it('should stop loading and show Access Denied when the products endpoint returns 403', () => {
      vi.spyOn(masterDataService, 'getProducts').mockReturnValue(
        throwError(() => new HttpErrorResponse({ status: 403, statusText: 'Forbidden' }))
      );

      fixture.detectChanges();

      expect(component.loading()).toBe(false);
      expect(component.isAccessDenied()).toBe(true);
      expect(component.error()).toBeNull();

      const compiled = fixture.nativeElement as HTMLElement;
      expect(compiled.textContent).toContain('Access Restricted');
      expect(compiled.textContent).not.toContain('Failed to retrieve products');
    });

    it('should show a safe retryable error instead of raw transport text for a 500', () => {
      vi.spyOn(masterDataService, 'getProducts').mockReturnValue(
        throwError(
          () =>
            new HttpErrorResponse({
              status: 500,
              statusText: 'Internal Server Error',
              url: 'http://localhost:4200/api/products'
            })
        )
      );

      fixture.detectChanges();

      expect(component.loading()).toBe(false);
      expect(component.isAccessDenied()).toBe(false);
      expect(component.error()).not.toMatch(/http failure response/i);

      const compiled = fixture.nativeElement as HTMLElement;
      expect(compiled.textContent).toContain('Unable to Load Products');
      expect(compiled.textContent).toContain('Retry');
      expect(compiled.textContent).not.toContain('Http failure response');
    });

    it('should re-invoke the products lookup and clear the error when retried', () => {
      const productSpy = vi
        .spyOn(masterDataService, 'getProducts')
        .mockReturnValue(
          throwError(
            () => new HttpErrorResponse({ status: 0, statusText: 'Unknown Error' })
          )
        );

      fixture.detectChanges();
      expect(component.error()).toBeTruthy();

      productSpy.mockReturnValue(of(mockProducts));
      component.loadData();

      expect(productSpy).toHaveBeenCalledTimes(2);
      expect(component.error()).toBeNull();
      expect(component.products().length).toBe(2);
      expect(component.isAccessDenied()).toBe(false);
    });
  });
});
