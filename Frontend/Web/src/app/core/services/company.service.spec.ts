import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { CompanyService } from './company.service';
import { environment } from '../../../environments/environment';

describe('CompanyService', () => {
  let service: CompanyService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [CompanyService, provideHttpClient(), provideHttpClientTesting()]
    });

    service = TestBed.inject(CompanyService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('should fetch companies list from backend API', () => {
    const mockData = [
      { id: '1', tenantId: 't1', name: 'Company One', code: 'C1', isActive: true }
    ];

    service.getCompanies().subscribe(res => {
      expect(res.length).toBe(1);
      expect(res[0].name).toBe('Company One');
    });

    const req = httpMock.expectOne(`${environment.apiBaseUrl}/companies`);
    expect(req.request.method).toBe('GET');
    req.flush({ succeeded: true, data: mockData });
  });

  it('should fetch branches for specific company', () => {
    const mockBranches = [
      { id: 'b1', companyId: '1', name: 'Main Branch', code: 'BR1', isActive: true }
    ];

    service.getBranches('1').subscribe(res => {
      expect(res.length).toBe(1);
      expect(res[0].code).toBe('BR1');
    });

    const req = httpMock.expectOne(`${environment.apiBaseUrl}/companies/1/branches`);
    expect(req.request.method).toBe('GET');
    req.flush({ succeeded: true, data: mockBranches });
  });
});
