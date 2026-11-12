import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import {
  TenantSetupService,
  TenantOrganizationSetupRequest,
  TenantOrganizationSetupResult,
  TenantUserSetupRequest,
  TenantUserSetupResult,
  TenantMasterDataSetupRequest,
  TenantMasterDataSetupResult
} from './tenant-setup.service';
import { ApiResponse } from '../models/auth.models';

describe('TenantSetupService', () => {
  let service: TenantSetupService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        TenantSetupService,
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    });

    service = TestBed.inject(TenantSetupService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('should call POST /api/tenant-setup/initialize-organization', () => {
    const request: TenantOrganizationSetupRequest = {
      companyName: 'Lanka Retail Mart',
      companyCode: 'RETAIL',
      baseCurrencyCode: 'LKR',
      branchName: 'Colombo Main Branch',
      branchCode: 'MAIN'
    };

    const mockResponse: ApiResponse<TenantOrganizationSetupResult> = {
      succeeded: true,
      message: 'Tenant organization setup completed successfully.',
      data: {
        tenantId: '10000000-0000-0000-0000-000000000001',
        companyId: 'fff3a70a-197f-48c6-834b-b8ae891ae3b2',
        companyCode: 'RETAIL',
        companyName: 'Lanka Retail Mart',
        companyCreated: true,
        branchId: 'a9c0eb86-9e02-4e32-ba78-612b478bf64f',
        branchCode: 'MAIN',
        branchName: 'Colombo Main Branch',
        branchCreated: true,
        companyAddressCreated: true,
        branchAddressCreated: true,
        messages: ['Company and branch verified.']
      }
    };

    service.initializeOrganization(request).subscribe(res => {
      expect(res.succeeded).toBe(true);
      expect(res.data?.companyCode).toBe('RETAIL');
    });

    const req = httpMock.expectOne('/api/tenant-setup/initialize-organization');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(request);
    req.flush(mockResponse);
  });

  it('should call POST /api/tenant-setup/initialize-users', () => {
    const request: TenantUserSetupRequest = {
      tenantShortCode: 'ret',
      emailDomain: 'retail.local'
    };

    const mockResponse: ApiResponse<TenantUserSetupResult> = {
      succeeded: true,
      message: 'Tenant user setup completed successfully.',
      data: {
        tenantId: '10000000-0000-0000-0000-000000000001',
        success: true,
        users: [
          {
            userId: '20000000-0000-0000-0000-000000000001',
            username: 'retadm',
            email: 'admin@retail.local',
            roleTemplateKey: 'TENANT_ADMIN',
            roleName: 'Tenant Administrator',
            roleId: '30000000-0000-0000-0000-000000000001',
            userCreated: true,
            roleAssigned: true,
            scopeAssigned: true,
            status: 'Created'
          }
        ],
        messages: ['Demo users provisioned.']
      }
    };

    service.initializeUsers(request).subscribe(res => {
      expect(res.succeeded).toBe(true);
      expect(res.data?.users.length).toBe(1);
    });

    const req = httpMock.expectOne('/api/tenant-setup/initialize-users');
    expect(req.request.method).toBe('POST');
    req.flush(mockResponse);
  });

  it('should call POST /api/tenant-setup/initialize-master-data', () => {
    const request: TenantMasterDataSetupRequest = {
      profileCode: 'retail'
    };

    const mockResponse: ApiResponse<TenantMasterDataSetupResult> = {
      succeeded: true,
      message: 'Tenant master data setup completed successfully.',
      data: {
        tenantId: '10000000-0000-0000-0000-000000000001',
        companyId: 'fff3a70a-197f-48c6-834b-b8ae891ae3b2',
        branchId: 'a9c0eb86-9e02-4e32-ba78-612b478bf64f',
        success: true,
        currenciesProcessed: 5,
        exchangeRatesProcessed: 4,
        paymentTermsProcessed: 4,
        taxCodesProcessed: 3,
        categoriesProcessed: 6,
        brandsProcessed: 3,
        uomsProcessed: 13,
        conversionsProcessed: 6,
        productsProcessed: 5,
        packagingsProcessed: 5,
        identifiersProcessed: 5,
        suppliersProcessed: 1,
        customersProcessed: 1,
        priceCardsProcessed: 2,
        warehousesProcessed: 2,
        zonesProcessed: 3,
        locationsProcessed: 3,
        messages: ['Master data seeded successfully.']
      }
    };

    service.initializeMasterData(request).subscribe(res => {
      expect(res.succeeded).toBe(true);
      expect(res.data?.productsProcessed).toBe(5);
      expect(res.data?.warehousesProcessed).toBe(2);
    });

    const req = httpMock.expectOne('/api/tenant-setup/initialize-master-data');
    expect(req.request.method).toBe('POST');
    req.flush(mockResponse);
  });
});
