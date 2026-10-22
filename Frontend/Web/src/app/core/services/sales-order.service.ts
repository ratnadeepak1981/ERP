import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { buildApiUrl } from '../utils/api-url.util';
import { ApiResponse } from '../models/auth.models';
import {
  SalesOrderDto,
  SalesOrderItemDto,
  CreateSalesOrderRequest,
  CreateSalesOrderItemRequest,
  SalesOrderDecisionRequest,
  SalesOrderStatus
} from '../models/sales-order.models';

@Injectable({
  providedIn: 'root'
})
export class SalesOrderService {
  private readonly http = inject(HttpClient);

  private baseUrl(companyId: string, branchId: string): string {
    return buildApiUrl(`/companies/${companyId}/branches/${branchId}/sales-orders`);
  }

  getOrders(
    companyId: string,
    branchId: string,
    status?: SalesOrderStatus | number
  ): Observable<SalesOrderDto[]> {
    let params = new HttpParams();
    if (status !== undefined && status !== null) {
      params = params.set('status', status.toString());
    }

    return this.http
      .get<ApiResponse<SalesOrderDto[] | { items: SalesOrderDto[] }>>(
        this.baseUrl(companyId, branchId),
        { params }
      )
      .pipe(
        map(res => {
          if (!res?.data) return [];
          if (Array.isArray(res.data)) return res.data;
          return (res.data as { items: SalesOrderDto[] }).items || [];
        })
      );
  }

  getOrder(companyId: string, branchId: string, id: string): Observable<SalesOrderDto | null> {
    return this.http
      .get<ApiResponse<SalesOrderDto>>(`${this.baseUrl(companyId, branchId)}/${id}`)
      .pipe(map(res => res?.data ?? null));
  }

  createOrder(
    companyId: string,
    branchId: string,
    request: CreateSalesOrderRequest
  ): Observable<SalesOrderDto> {
    return this.http
      .post<ApiResponse<SalesOrderDto>>(this.baseUrl(companyId, branchId), request)
      .pipe(map(res => res.data));
  }

  addItem(
    companyId: string,
    branchId: string,
    orderId: string,
    request: CreateSalesOrderItemRequest
  ): Observable<SalesOrderItemDto> {
    return this.http
      .post<ApiResponse<SalesOrderItemDto>>(
        `${this.baseUrl(companyId, branchId)}/${orderId}/items`,
        request
      )
      .pipe(map(res => res.data));
  }

  submitOrder(companyId: string, branchId: string, orderId: string): Observable<SalesOrderDto> {
    return this.http
      .post<ApiResponse<SalesOrderDto>>(
        `${this.baseUrl(companyId, branchId)}/${orderId}/submit`,
        {}
      )
      .pipe(map(res => res.data));
  }

  confirmOrder(
    companyId: string,
    branchId: string,
    orderId: string,
    remarks?: string
  ): Observable<SalesOrderDto> {
    const body: SalesOrderDecisionRequest = { remarks };
    return this.http
      .post<ApiResponse<SalesOrderDto>>(
        `${this.baseUrl(companyId, branchId)}/${orderId}/confirm`,
        body
      )
      .pipe(map(res => res.data));
  }

  rejectOrder(
    companyId: string,
    branchId: string,
    orderId: string,
    remarks: string
  ): Observable<SalesOrderDto> {
    const body: SalesOrderDecisionRequest = { remarks };
    return this.http
      .post<ApiResponse<SalesOrderDto>>(
        `${this.baseUrl(companyId, branchId)}/${orderId}/reject`,
        body
      )
      .pipe(map(res => res.data));
  }

  cancelOrder(
    companyId: string,
    branchId: string,
    orderId: string,
    remarks?: string
  ): Observable<SalesOrderDto> {
    const body: SalesOrderDecisionRequest = { remarks };
    return this.http
      .post<ApiResponse<SalesOrderDto>>(
        `${this.baseUrl(companyId, branchId)}/${orderId}/cancel`,
        body
      )
      .pipe(map(res => res.data));
  }
}
