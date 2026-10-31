import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { MasterDataService } from './master-data.service';
import { environment } from '../../../environments/environment';
import { CreateProductDto, CreateSupplierDto } from '../models/scope.models';

describe('MasterDataService', () => {
  let service: MasterDataService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [MasterDataService, provideHttpClient(), provideHttpClientTesting()]
    });

    service = TestBed.inject(MasterDataService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('should get products list', () => {
    service.getProducts().subscribe(res => {
      expect(res.length).toBe(1);
      expect(res[0].productCode).toBe('P1');
    });

    const req = httpMock.expectOne(`${environment.apiBaseUrl}/products`);
    expect(req.request.method).toBe('GET');
    req.flush({ succeeded: true, data: [{ productId: 'p1', productCode: 'P1', productName: 'Item 1' }] });
  });

  it('should create product with valid DTO', () => {
    const dto: CreateProductDto = {
      productCode: 'NEW-P1',
      productName: 'New Product',
      categoryId: 'cat-1',
      unitOfMeasure: 'PCS',
      standardCost: 10,
      sellingPrice: 15,
      reorderLevel: 5,
      reorderQuantity: 20,
      isPurchasable: true,
      isSellable: true,
      isManufacturable: false,
      canConsumeInProduction: false,
      isStockTracked: true
    };

    service.createProduct(dto).subscribe(res => {
      expect(res.productCode).toBe('NEW-P1');
    });

    const req = httpMock.expectOne(`${environment.apiBaseUrl}/products`);
    expect(req.request.method).toBe('POST');
    req.flush({ succeeded: true, data: { ...dto, productId: 'prod-new' } });
  });

  it('should fetch suppliers and create supplier', () => {
    const dto: CreateSupplierDto = {
      supplierCode: 'SUP-01',
      supplierName: 'Apex Suppliers',
      city: 'Colombo'
    };

    service.createSupplier(dto).subscribe(res => {
      expect(res.supplierName).toBe('Apex Suppliers');
    });

    const req = httpMock.expectOne(`${environment.apiBaseUrl}/suppliers`);
    expect(req.request.method).toBe('POST');
    req.flush({ succeeded: true, data: { ...dto, supplierId: 'sup-1' } });
  });

  describe('Hierarchy & Lazy Loading methods', () => {
    const mockCategories = [
      { categoryId: 'cat-root-1', tenantId: 't1', categoryCode: 'R1', categoryName: 'Root 1', parentCategoryId: null, isActive: true },
      { categoryId: 'cat-root-2', tenantId: 't1', categoryCode: 'R2', categoryName: 'Root 2', parentCategoryId: null, isActive: true },
      { categoryId: 'cat-child-1', tenantId: 't1', categoryCode: 'C1', categoryName: 'Child 1', parentCategoryId: 'cat-root-1', isActive: true },
      { categoryId: 'cat-grandchild-1', tenantId: 't1', categoryCode: 'GC1', categoryName: 'Grandchild 1', parentCategoryId: 'cat-child-1', isActive: true }
    ];

    const mockProducts = [
      { productId: 'p1', tenantId: 't1', productCode: 'PRD-1', productName: 'Prod 1', categoryId: 'cat-root-1', unitOfMeasure: 'PCS', isActive: true, isPurchasable: true, isSellable: true, isManufacturable: false, canConsumeInProduction: false, canConsumeInMaintenance: false, isStockTracked: true, productType: 1, trackingMode: 0, valuationMethod: 1, standardCost: 10, sellingPrice: 20, reorderLevel: 5, reorderQuantity: 10 },
      { productId: 'p2', tenantId: 't1', productCode: 'PRD-2', productName: 'Prod 2', categoryId: 'cat-root-1', unitOfMeasure: 'PCS', isActive: true, isPurchasable: true, isSellable: true, isManufacturable: false, canConsumeInProduction: false, canConsumeInMaintenance: false, isStockTracked: true, productType: 1, trackingMode: 0, valuationMethod: 1, standardCost: 10, sellingPrice: 20, reorderLevel: 5, reorderQuantity: 10 },
      { productId: 'p3', tenantId: 't1', productCode: 'PRD-3', productName: 'Prod 3', categoryId: 'cat-child-1', unitOfMeasure: 'PCS', isActive: true, isPurchasable: true, isSellable: true, isManufacturable: false, canConsumeInProduction: false, canConsumeInMaintenance: false, isStockTracked: true, productType: 1, trackingMode: 0, valuationMethod: 1, standardCost: 10, sellingPrice: 20, reorderLevel: 5, reorderQuantity: 10 }
    ];

    it('getRootCategories should filter root categories, detect hasChildren and paginate', () => {
      service.getRootCategories(1, 1).subscribe(result => {
        expect(result.totalCount).toBe(2);
        expect(result.items.length).toBe(1);
        expect(result.items[0].categoryId).toBe('cat-root-1');
        expect(result.items[0].hasChildren).toBe(true);
        expect(result.hasMore).toBe(true);
      });

      const req = httpMock.expectOne(`${environment.apiBaseUrl}/categories`);
      expect(req.request.method).toBe('GET');
      req.flush({ succeeded: true, data: mockCategories });

      // Second call should reuse cache without making another HTTP request
      service.getRootCategories(2, 1).subscribe(result => {
        expect(result.items.length).toBe(1);
        expect(result.items[0].categoryId).toBe('cat-root-2');
        expect(result.items[0].hasChildren).toBe(false);
        expect(result.hasMore).toBe(false);
      });

      httpMock.expectNone(`${environment.apiBaseUrl}/categories`);
    });

    it('getCategoryChildren should filter immediate children of parent and paginate', () => {
      service.getCategoryChildren('cat-root-1', 1, 10).subscribe(result => {
        expect(result.totalCount).toBe(1);
        expect(result.items.length).toBe(1);
        expect(result.items[0].categoryId).toBe('cat-child-1');
        expect(result.items[0].hasChildren).toBe(true);
        expect(result.hasMore).toBe(false);
      });

      const req = httpMock.expectOne(`${environment.apiBaseUrl}/categories`);
      req.flush({ succeeded: true, data: mockCategories });
    });

    it('getCategoryProducts should filter products by categoryId and paginate', () => {
      service.getCategoryProducts('cat-root-1', 1, 1).subscribe(result => {
        expect(result.totalCount).toBe(2);
        expect(result.items.length).toBe(1);
        expect(result.items[0].productId).toBe('p1');
        expect(result.hasMore).toBe(true);
      });

      const req = httpMock.expectOne(`${environment.apiBaseUrl}/products`);
      req.flush({ succeeded: true, data: mockProducts });

      // Cache reuse
      service.getCategoryProducts('cat-root-1', 2, 1).subscribe(result => {
        expect(result.items.length).toBe(1);
        expect(result.items[0].productId).toBe('p2');
        expect(result.hasMore).toBe(false);
      });

      httpMock.expectNone(`${environment.apiBaseUrl}/products`);
    });

    it('clearHierarchyCache should clear in-memory cache and re-request from server', () => {
      service.getRootCategories(1, 10).subscribe();
      const req1 = httpMock.expectOne(`${environment.apiBaseUrl}/categories`);
      req1.flush({ succeeded: true, data: mockCategories });

      service.clearHierarchyCache();

      service.getRootCategories(1, 10).subscribe();
      const req2 = httpMock.expectOne(`${environment.apiBaseUrl}/categories`);
      req2.flush({ succeeded: true, data: mockCategories });
    });
  });
});
