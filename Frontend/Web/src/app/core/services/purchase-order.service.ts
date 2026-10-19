import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { buildApiUrl } from '../utils/api-url.util';
import { ApiResponse } from '../models/auth.models';
import {
  PurchaseOrderDto,
  PurchaseOrderItemDto,
  CreatePurchaseOrderRequest,
  CreatePurchaseOrderItemRequest,
  ApprovalDecisionRequest,
  PurchaseOrderHistoryDto
} from '../models/purchase-order.models';

export interface PurchaseOrderQueryParams {
  page?: number;
  pageSize?: number;
  sortBy?: string;
  sortDirection?: string;
}

@Injectable({
  providedIn: 'root'
})
export class PurchaseOrderService {
  private readonly http = inject(HttpClient);

  private buildPath(companyId: string, branchId: string, suffix = ''): string {
    return buildApiUrl(`/companies/${companyId}/branches/${branchId}/purchase-orders${suffix}`);
  }

  getOrders(
    companyId: string,
    branchId: string,
    params?: PurchaseOrderQueryParams
  ): Observable<PurchaseOrderDto[]> {
    let httpParams = new HttpParams();
    if (params?.page !== undefined) httpParams = httpParams.set('page', params.page.toString());
    if (params?.pageSize !== undefined) httpParams = httpParams.set('pageSize', params.pageSize.toString());
    if (params?.sortBy) httpParams = httpParams.set('sortBy', params.sortBy);
    if (params?.sortDirection) httpParams = httpParams.set('sortDirection', params.sortDirection);

    return this.http
      .get<ApiResponse<PurchaseOrderDto[] | { items: PurchaseOrderDto[]; totalCount: number }>>(
        this.buildPath(companyId, branchId),
        { params: httpParams }
      )
      .pipe(
        map(res => {
          if (!res?.data) return [];
          if (Array.isArray(res.data)) return res.data;
          return (res.data as { items: PurchaseOrderDto[] }).items || [];
        })
      );
  }

  getOrder(companyId: string, branchId: string, id: string): Observable<PurchaseOrderDto | null> {
    return this.http
      .get<ApiResponse<PurchaseOrderDto>>(this.buildPath(companyId, branchId, `/${id}`))
      .pipe(map(res => res?.data ?? null));
  }

  createOrder(
    companyId: string,
    branchId: string,
    request: CreatePurchaseOrderRequest
  ): Observable<PurchaseOrderDto> {
    return this.http
      .post<ApiResponse<PurchaseOrderDto>>(this.buildPath(companyId, branchId), request)
      .pipe(map(res => res.data));
  }

  addItem(
    companyId: string,
    branchId: string,
    orderId: string,
    request: CreatePurchaseOrderItemRequest
  ): Observable<PurchaseOrderItemDto> {
    return this.http
      .post<ApiResponse<PurchaseOrderItemDto>>(this.buildPath(companyId, branchId, `/${orderId}/items`), request)
      .pipe(map(res => res.data));
  }

  submitOrder(companyId: string, branchId: string, orderId: string): Observable<PurchaseOrderDto> {
    return this.http
      .post<ApiResponse<PurchaseOrderDto>>(this.buildPath(companyId, branchId, `/${orderId}/submit`), {})
      .pipe(map(res => res.data));
  }

  approveOrder(
    companyId: string,
    branchId: string,
    orderId: string,
    remarks?: string
  ): Observable<PurchaseOrderDto> {
    const payload: ApprovalDecisionRequest = { remarks };
    return this.http
      .post<ApiResponse<PurchaseOrderDto>>(this.buildPath(companyId, branchId, `/${orderId}/approve`), payload)
      .pipe(map(res => res.data));
  }

  rejectOrder(
    companyId: string,
    branchId: string,
    orderId: string,
    remarks: string
  ): Observable<PurchaseOrderDto> {
    const payload: ApprovalDecisionRequest = { remarks };
    return this.http
      .post<ApiResponse<PurchaseOrderDto>>(this.buildPath(companyId, branchId, `/${orderId}/reject`), payload)
      .pipe(map(res => res.data));
  }

  cancelOrder(
    companyId: string,
    branchId: string,
    orderId: string,
    remarks?: string
  ): Observable<PurchaseOrderDto> {
    const payload: ApprovalDecisionRequest = { remarks };
    return this.http
      .post<ApiResponse<PurchaseOrderDto>>(this.buildPath(companyId, branchId, `/${orderId}/cancel`), payload)
      .pipe(map(res => res.data));
  }

  getApprovalHistory(
    companyId: string,
    branchId: string,
    orderId: string
  ): Observable<PurchaseOrderHistoryDto[]> {
    return this.http
      .get<ApiResponse<PurchaseOrderHistoryDto[]>>(this.buildPath(companyId, branchId, `/${orderId}/approval-history`))
      .pipe(map(res => res?.data ?? []));
  }
}
