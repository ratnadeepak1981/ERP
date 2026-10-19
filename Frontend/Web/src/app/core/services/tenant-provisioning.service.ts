import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Observable, catchError, map, of } from 'rxjs';
import { ApiResponse } from '../models/auth.models';
import {
  TenantProvisioningRequest,
  TenantProvisioningResult,
  ProvisioningQueueItem,
  ProvisioningMetrics,
  TenantDetails,
  TenantLifecycleStatus
} from '../models/tenant-provisioning.model';
import { buildApiUrl } from '../utils/api-url.util';

export * from '../models/tenant-provisioning.model';

@Injectable({
  providedIn: 'root'
})
export class TenantProvisioningService {
  private readonly http = inject(HttpClient);

  /**
   * Dispatches tenant provisioning request to backend Platform API.
   * Requires authenticated Platform User with TENANT.CREATE permission.
   */
  provisionTenant(request: TenantProvisioningRequest): Observable<ApiResponse<TenantProvisioningResult>> {
    return this.http.post<ApiResponse<TenantProvisioningResult>>(
      buildApiUrl('/Tenant/provision'),
      request
    );
  }

  /**
   * Fetches real tenant registration records for the provisioning queue.
   * If the backend query endpoint is not yet exposed, returns a graceful response
   * indicating the live endpoint status without inventing fabricated records.
   */
  getProvisioningQueue(): Observable<{ items: ProvisioningQueueItem[]; endpointAvailable: boolean; message: string }> {
    return this.http.get<ApiResponse<ProvisioningQueueItem[]>>(buildApiUrl('/Tenant')).pipe(
      map(response => ({
        items: response?.data || [],
        endpointAvailable: true,
        message: 'Live tenant registration records loaded successfully.'
      })),
      catchError((error: HttpErrorResponse) => {
        const isNotExposed = error.status === 404 || error.status === 405;
        let message: string;
        if (error.status === 401) {
          message = 'Authentication session expired or missing token (HTTP 401).';
        } else if (error.status === 403) {
          message = 'Access forbidden (HTTP 403). Platform Administrator privileges required to inspect tenant registry.';
        } else if (isNotExposed) {
          message = 'Live tenant registration query endpoint (GET /api/Tenant) is not yet exposed by the backend service. Provisioning queue awaits registration records from backend API.';
        } else if (error.status >= 500) {
          message = `Tenant registry backend service error (HTTP ${error.status}).`;
        } else {
          message = error.error?.message || error.message || 'Unable to connect to tenant registry API.';
        }

        return of({
          items: [],
          endpointAvailable: !isNotExposed,
          message
        });
      })
    );
  }

  /**
   * Retrieves platform-wide provisioning KPI metrics from real PlatformDB records.
   * Returns null values gracefully if backend metric aggregation endpoint is not reachable,
   * adhering strictly to the requirement that metrics must never be fabricated.
   */
  getProvisioningMetrics(): Observable<ProvisioningMetrics> {
    return this.http.get<ApiResponse<ProvisioningMetrics>>(buildApiUrl('/Tenant/metrics')).pipe(
      map(res => res?.data || {
        pending: null,
        inProgress: null,
        failed: null,
        completed: null,
        isLive: false
      }),
      catchError(() => of({
        pending: null,
        inProgress: null,
        failed: null,
        completed: null,
        isLive: false
      }))
    );
  }

  /**
   * Retrieves detailed tenant inspection model by GUID.
   */
  getTenantDetails(tenantId: string): Observable<ApiResponse<TenantDetails>> {
    return this.http.get<ApiResponse<TenantDetails>>(buildApiUrl(`/Tenant/${tenantId}`));
  }

  /**
   * Retrieves verified persisted lifecycle and database provisioning status.
   */
  getTenantStatus(tenantId: string): Observable<ApiResponse<TenantLifecycleStatus>> {
    return this.http.get<ApiResponse<TenantLifecycleStatus>>(buildApiUrl(`/Tenant/${tenantId}/status`));
  }
}
