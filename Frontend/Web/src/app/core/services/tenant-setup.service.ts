import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ApiResponse } from '../models/auth.models';
import { buildApiUrl } from '../utils/api-url.util';

export interface TenantOrganizationSetupRequest {
  targetTenantId?: string;
  companyName?: string;
  companyCode?: string;
  baseCurrencyCode?: string;
  branchName?: string;
  branchCode?: string;
  addressLine1?: string;
  addressLine2?: string;
  city?: string;
  stateProvince?: string;
  postalCode?: string;
  countryName?: string;
}

export interface TenantOrganizationSetupResult {
  tenantId: string;
  companyId: string;
  companyCode: string;
  companyName: string;
  companyCreated: boolean;
  branchId: string;
  branchCode: string;
  branchName: string;
  branchCreated: boolean;
  companyAddressId?: string;
  companyAddressCreated: boolean;
  branchAddressId?: string;
  branchAddressCreated: boolean;
  messages: string[];
}

export interface TenantUserSpec {
  username: string;
  email: string;
  roleTemplateKey: string;
  roleName?: string;
  password?: string;
  companyId?: string;
  branchId?: string;
  description?: string;
}

export interface TenantUserSetupRequest {
  targetTenantId?: string;
  companyId?: string;
  branchId?: string;
  tenantShortCode?: string;
  emailDomain?: string;
  defaultInitialPassword?: string;
  customUsers?: TenantUserSpec[];
}

export interface TenantUserResultItem {
  userId: string;
  username: string;
  email: string;
  roleTemplateKey: string;
  roleName: string;
  roleId: string;
  companyId?: string;
  branchId?: string;
  userCreated: boolean;
  roleAssigned: boolean;
  scopeAssigned: boolean;
  generatedPassword?: string;
  status: string;
}

export interface TenantUserSetupResult {
  tenantId: string;
  success: boolean;
  users: TenantUserResultItem[];
  messages: string[];
}

export interface TenantMasterDataSetupRequest {
  targetTenantId?: string;
  companyId?: string;
  branchId?: string;
  profileCode?: string;
  profileJson?: string;
}

export interface TenantMasterDataSetupResult {
  tenantId: string;
  companyId: string;
  branchId: string;
  success: boolean;
  currenciesProcessed: number;
  exchangeRatesProcessed: number;
  paymentTermsProcessed: number;
  taxCodesProcessed: number;
  categoriesProcessed: number;
  brandsProcessed: number;
  uomsProcessed: number;
  conversionsProcessed: number;
  productsProcessed: number;
  packagingsProcessed: number;
  identifiersProcessed: number;
  suppliersProcessed: number;
  customersProcessed: number;
  priceCardsProcessed: number;
  warehousesProcessed: number;
  zonesProcessed: number;
  locationsProcessed: number;
  messages: string[];
}

@Injectable({
  providedIn: 'root'
})
export class TenantSetupService {
  private readonly http = inject(HttpClient);

  initializeOrganization(request?: TenantOrganizationSetupRequest): Observable<ApiResponse<TenantOrganizationSetupResult>> {
    return this.http.post<ApiResponse<TenantOrganizationSetupResult>>(
      buildApiUrl('/tenant-setup/initialize-organization'),
      request ?? {}
    );
  }

  initializeUsers(request?: TenantUserSetupRequest): Observable<ApiResponse<TenantUserSetupResult>> {
    return this.http.post<ApiResponse<TenantUserSetupResult>>(
      buildApiUrl('/tenant-setup/initialize-users'),
      request ?? {}
    );
  }

  initializeMasterData(request?: TenantMasterDataSetupRequest): Observable<ApiResponse<TenantMasterDataSetupResult>> {
    return this.http.post<ApiResponse<TenantMasterDataSetupResult>>(
      buildApiUrl('/tenant-setup/initialize-master-data'),
      request ?? {}
    );
  }
}
