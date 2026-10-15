import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ApiResponse } from '../models/auth.models';
import { CreateTenantRequest, TenantRegistrationResult } from '../models/tenant-registration.model';
import { buildApiUrl } from '../utils/api-url.util';

@Injectable({
  providedIn: 'root'
})
export class TenantRegistrationService {
  private readonly http = inject(HttpClient);

  registerTenant(request: CreateTenantRequest): Observable<ApiResponse<TenantRegistrationResult>> {
    return this.http.post<ApiResponse<TenantRegistrationResult>>(buildApiUrl('/Tenant/tenant-registration-test'), request);
  }
}
