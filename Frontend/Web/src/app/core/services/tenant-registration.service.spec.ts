import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TenantRegistrationService } from './tenant-registration.service';
import { CreateTenantRequest, TenantRegistrationResult } from '../models/tenant-registration.model';
import { ApiResponse } from '../models/auth.models';

describe('TenantRegistrationService', () => {
  let service: TenantRegistrationService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        TenantRegistrationService,
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    });

    service = TestBed.inject(TenantRegistrationService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('should call POST /api/Tenant/tenant-registration-test with correct contract payload', () => {
    const requestPayload: CreateTenantRequest = {
      name: 'Acme Manufacturing Corp',
      subscriptionPlanId: '11111111-1111-1111-1111-111111111111'
    };

    const mockResponse: ApiResponse<TenantRegistrationResult> = {
      succeeded: true,
      message: 'Tenant registration use case completed',
      data: {
        tenant: {
          id: '99999999-9999-9999-9999-999999999999',
          name: 'Acme Manufacturing Corp',
          code: 'TEN-99999999',
          isActive: true
        },
        subscription: {
          id: '88888888-8888-8888-8888-888888888888',
          tenantId: '99999999-9999-9999-9999-999999999999',
          subscriptionPlanId: '11111111-1111-1111-1111-111111111111',
          subscriptionName: 'Free Manufacturing',
          subscriptionType: 1,
          storageMode: 1,
          startDate: '2026-10-15T00:00:00Z',
          endDate: null,
          isActive: true
        }
      },
      errors: []
    };

    service.registerTenant(requestPayload).subscribe(res => {
      expect(res.succeeded).toBe(true);
      expect(res.data.tenant.name).toBe('Acme Manufacturing Corp');
      expect(res.data.tenant.code).toBe('TEN-99999999');
      expect(res.data.subscription.subscriptionPlanId).toBe('11111111-1111-1111-1111-111111111111');
    });

    const req = httpMock.expectOne('/api/Tenant/tenant-registration-test');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(requestPayload);
    req.flush(mockResponse);
  });
});
