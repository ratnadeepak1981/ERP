import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { buildApiUrl } from '../utils/api-url.util';
import { ApiResponse } from '../models/auth.models';
import {
  SalesShipmentDto,
  SalesShipmentStatus,
  CreateShipmentFromOrderRequest,
  UpdateSalesShipmentRequest,
  DeliverShipmentRequest
} from '../models/sales-shipment.models';

@Injectable({
  providedIn: 'root'
})
export class SalesShipmentService {
  private readonly http = inject(HttpClient);

  private baseUrl(companyId: string, branchId?: string | null): string {
    if (branchId) {
      return buildApiUrl(`/companies/${companyId}/branches/${branchId}/sales-shipments`);
    }
    return buildApiUrl(`/companies/${companyId}/sales-shipments`);
  }

  getShipments(
    companyId: string,
    branchId?: string | null,
    status?: SalesShipmentStatus | number
  ): Observable<SalesShipmentDto[]> {
    let params = new HttpParams();
    if (status !== undefined && status !== null) {
      params = params.set('status', status.toString());
    }

    return this.http
      .get<ApiResponse<SalesShipmentDto[] | { items: SalesShipmentDto[] }>>(
        this.baseUrl(companyId, branchId),
        { params }
      )
      .pipe(
        map(res => {
          if (!res?.data) return [];
          if (Array.isArray(res.data)) return res.data;
          return (res.data as { items: SalesShipmentDto[] }).items || [];
        })
      );
  }

  getShipment(companyId: string, branchId: string | null, id: string): Observable<SalesShipmentDto | null> {
    return this.http
      .get<ApiResponse<SalesShipmentDto>>(`${this.baseUrl(companyId, branchId)}/${id}`)
      .pipe(map(res => res?.data ?? null));
  }

  createFromOrder(
    companyId: string,
    branchId: string,
    salesOrderId: string,
    request: CreateShipmentFromOrderRequest
  ): Observable<SalesShipmentDto> {
    return this.http
      .post<ApiResponse<SalesShipmentDto>>(
        `${this.baseUrl(companyId, branchId)}/from-order/${salesOrderId}`,
        request
      )
      .pipe(map(res => res.data));
  }

  updateDraft(
    companyId: string,
    branchId: string,
    id: string,
    request: UpdateSalesShipmentRequest
  ): Observable<SalesShipmentDto> {
    return this.http
      .put<ApiResponse<SalesShipmentDto>>(
        `${this.baseUrl(companyId, branchId)}/${id}`,
        request
      )
      .pipe(map(res => res.data));
  }

  deliverShipment(
    companyId: string,
    branchId: string,
    id: string,
    request?: DeliverShipmentRequest
  ): Observable<SalesShipmentDto> {
    return this.http
      .post<ApiResponse<SalesShipmentDto>>(
        `${this.baseUrl(companyId, branchId)}/${id}/deliver`,
        request || {}
      )
      .pipe(map(res => res.data));
  }

  cancelDraft(
    companyId: string,
    branchId: string,
    id: string
  ): Observable<SalesShipmentDto> {
    return this.http
      .post<ApiResponse<SalesShipmentDto>>(
        `${this.baseUrl(companyId, branchId)}/${id}/cancel`,
        {}
      )
      .pipe(map(res => res.data));
  }
}
