import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { TenantProvisioningService } from './tenant-provisioning.service';
import { TenantProvisioningRequest, TenantProvisioningResult } from '../models/tenant-provisioning.model';
import { ApiResponse } from '../models/auth.models';

describe('TenantProvisioningService', () => {
  let service: TenantProvisioningService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        TenantProvisioningService
      ]
    });

    service = TestBed.inject(TenantProvisioningService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('should send POST /api/Tenant/provision with valid provisioning request payload', () => {
    const request: TenantProvisioningRequest = {
      tenantId: '11111111-2222-3333-4444-555555555555',
      adminUserName: 'tenantadmin',
      adminEmail: 'admin@acme.corp',
      adminPassword: 'Password123!'
    };

    const mockResponse: ApiResponse<TenantProvisioningResult> = {
      succeeded: true,
      message: 'Tenant provisioned successfully.',
      data: {
        tenantId: '11111111-2222-3333-4444-555555555555',
        databaseProvisioned: true,
        configurationProvisioned: true,
        isProvisioned: true,
        storageMode: 'Shared'
      },
      errors: []
    };

    service.provisionTenant(request).subscribe(res => {
      expect(res.succeeded).toBe(true);
      expect(res.data.isProvisioned).toBe(true);
      expect(res.data.databaseProvisioned).toBe(true);
      expect(res.data.storageMode).toBe('Shared');
    });

    const req = httpMock.expectOne('/api/Tenant/provision');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(request);
    req.flush(mockResponse);
  });

  it('should gracefully handle unexposed GET /api/Tenant endpoint without crashing', () => {
    service.getProvisioningQueue().subscribe(result => {
      expect(result.items.length).toBe(0);
      expect(result.endpointAvailable).toBe(false);
      expect(result.message).toContain('not yet exposed by the backend service');
    });

    const req = httpMock.expectOne('/api/Tenant');
    expect(req.request.method).toBe('GET');
    req.flush('Not Found', { status: 404, statusText: 'Not Found' });
  });

  it('should handle 401 and 403 authorization failures in getProvisioningQueue', () => {
    service.getProvisioningQueue().subscribe(result => {
      expect(result.endpointAvailable).toBe(true);
      expect(result.message).toContain('HTTP 403');
    });

    const req = httpMock.expectOne('/api/Tenant');
    req.flush('Forbidden', { status: 403, statusText: 'Forbidden' });
  });

  it('should handle 500 server errors in getProvisioningQueue', () => {
    service.getProvisioningQueue().subscribe(result => {
      expect(result.endpointAvailable).toBe(true);
      expect(result.message).toContain('HTTP 500');
    });

    const req = httpMock.expectOne('/api/Tenant');
    req.flush('Server Error', { status: 500, statusText: 'Internal Server Error' });
  });

  it('should fetch real tenant metrics when backend endpoint is available', () => {
    const mockMetrics: ApiResponse<any> = {
      succeeded: true,
      message: 'Tenant metrics retrieved successfully.',
      data: {
        totalRegistered: 10,
        pending: 6,
        inProgress: 0,
        failed: 0,
        completed: 4,
        activeSubscriptions: 8,
        isLive: true
      },
      errors: []
    };

    service.getProvisioningMetrics().subscribe(metrics => {
      expect(metrics.totalRegistered).toBe(10);
      expect(metrics.pending).toBe(6);
      expect(metrics.completed).toBe(4);
      expect(metrics.isLive).toBe(true);
    });

    const req = httpMock.expectOne('/api/Tenant/metrics');
    expect(req.request.method).toBe('GET');
    req.flush(mockMetrics);
  });

  it('should fetch tenant details by GUID', () => {
    const tenantId = '11111111-2222-3333-4444-555555555555';
    service.getTenantDetails(tenantId).subscribe(res => {
      expect(res.succeeded).toBe(true);
      expect(res.data.name).toBe('Acme Corp');
    });

    const req = httpMock.expectOne(`/api/Tenant/${tenantId}`);
    expect(req.request.method).toBe('GET');
    req.flush({
      succeeded: true,
      data: { id: tenantId, name: 'Acme Corp', code: 'ACME' }
    });
  });

  it('should fetch tenant lifecycle status by GUID', () => {
    const tenantId = '11111111-2222-3333-4444-555555555555';
    service.getTenantStatus(tenantId).subscribe(res => {
      expect(res.succeeded).toBe(true);
      expect(res.data.isProvisioned).toBe(true);
    });

    const req = httpMock.expectOne(`/api/Tenant/${tenantId}/status`);
    expect(req.request.method).toBe('GET');
    req.flush({
      succeeded: true,
      data: { tenantId, isRegistered: true, isProvisioned: true, overallStatus: 'Completed' }
    });
  });

  it('should return null metrics when backend aggregation endpoint is unavailable', () => {
    service.getProvisioningMetrics().subscribe(metrics => {
      expect(metrics.pending).toBeNull();
      expect(metrics.inProgress).toBeNull();
      expect(metrics.failed).toBeNull();
      expect(metrics.completed).toBeNull();
      expect(metrics.isLive).toBe(false);
    });

    const req = httpMock.expectOne('/api/Tenant/metrics');
    req.flush('Not Found', { status: 404, statusText: 'Not Found' });
  });
});
