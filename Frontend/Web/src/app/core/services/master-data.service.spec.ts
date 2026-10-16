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
});
