import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { firstValueFrom } from 'rxjs';
import { EcommerceCatalogueService } from './ecommerce-catalogue.service';

describe('EcommerceCatalogueService', () => {
  let service: EcommerceCatalogueService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        EcommerceCatalogueService,
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    });
    service = TestBed.inject(EcommerceCatalogueService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('should return empty list when companyId is missing or empty', async () => {
    const productsPromise = firstValueFrom(service.getPublicCatalogue(''));
    const products = await productsPromise;
    expect(products).toEqual([]);
  });

  it('should fetch public products when companyId is valid', async () => {
    const mockCompanyId = '11111111-1111-1111-1111-111111111111';
    const mockProducts = [
      {
        productId: 'prod-1',
        productCode: 'SKU-001',
        productName: 'Milling Spindle',
        displayOrder: 1,
        categoryId: 'cat-1',
        categoryName: 'CNC Parts',
        unitOfMeasure: 'EA',
        sellingPrice: 1250,
        currency: 'USD'
      }
    ];

    const productsPromise = firstValueFrom(service.getPublicCatalogue(mockCompanyId));

    const req = httpMock.expectOne(request => request.url.includes(`/ecommerce/catalogue/${mockCompanyId}/products`));
    expect(req.request.method).toBe('GET');
    req.flush({ succeeded: true, data: mockProducts, message: 'OK', statusCode: 200 });

    const products = await productsPromise;
    expect(products.length).toBe(1);
    expect(products[0].productCode).toBe('SKU-001');
  });

  it('should handle errors gracefully by returning empty array', async () => {
    const mockCompanyId = '11111111-1111-1111-1111-111111111111';

    const productsPromise = firstValueFrom(service.getPublicCatalogue(mockCompanyId));

    const req = httpMock.expectOne(request => request.url.includes(`/ecommerce/catalogue/${mockCompanyId}/products`));
    req.flush({ message: 'Error' }, { status: 404, statusText: 'Not Found' });

    const products = await productsPromise;
    expect(products).toEqual([]);
  });
});
