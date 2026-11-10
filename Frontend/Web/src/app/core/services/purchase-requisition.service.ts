import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { buildApiUrl } from '../utils/api-url.util';
import { ApiResponse } from '../models/auth.models';
import {
  PurchaseRequisitionDto,
  PurchaseRequisitionItemDto,
  CreatePurchaseRequisitionRequest,
  CreatePurchaseRequisitionItemRequest,
  UpdatePurchaseRequisitionRequest,
  UpdatePurchaseRequisitionItemRequest,
  ApprovalDecisionRequest,
  PurchaseRequisitionHistoryDto
} from '../models/purchase-requisition.models';

export interface PurchaseRequisitionQueryParams {
  page?: number;
  pageSize?: number;
  sortBy?: string;
  sortDirection?: string;
}

@Injectable({
  providedIn: 'root'
})
export class PurchaseRequisitionService {
  private readonly http = inject(HttpClient);

  private buildPath(companyId: string, branchId: string, suffix = ''): string {
    return buildApiUrl(`/companies/${companyId}/branches/${branchId}/purchase-requisitions${suffix}`);
  }

  getRequisitions(
    companyId: string,
    branchId: string,
    params?: PurchaseRequisitionQueryParams
  ): Observable<PurchaseRequisitionDto[]> {
    let httpParams = new HttpParams();
    if (params?.page !== undefined) httpParams = httpParams.set('page', params.page.toString());
    if (params?.pageSize !== undefined) httpParams = httpParams.set('pageSize', params.pageSize.toString());
    if (params?.sortBy) httpParams = httpParams.set('sortBy', params.sortBy);
    if (params?.sortDirection) httpParams = httpParams.set('sortDirection', params.sortDirection);

    return this.http
      .get<ApiResponse<PurchaseRequisitionDto[] | { items: PurchaseRequisitionDto[]; totalCount: number }>>(
        this.buildPath(companyId, branchId),
        { params: httpParams }
      )
      .pipe(
        map(res => {
          if (!res?.data) return [];
          if (Array.isArray(res.data)) return res.data;
          return (res.data as { items: PurchaseRequisitionDto[] }).items || [];
        })
      );
  }

  getRequisition(
    companyId: string,
    branchId: string,
    id: string
  ): Observable<PurchaseRequisitionDto | null> {
    return this.http
      .get<ApiResponse<PurchaseRequisitionDto>>(this.buildPath(companyId, branchId, `/${id}`))
      .pipe(map(res => res?.data ?? null));
  }

  createRequisition(
    companyId: string,
    branchId: string,
    request: CreatePurchaseRequisitionRequest
  ): Observable<PurchaseRequisitionDto> {
    return this.http
      .post<ApiResponse<PurchaseRequisitionDto>>(this.buildPath(companyId, branchId), request)
      .pipe(map(res => res.data));
  }

  updateRequisition(
    companyId: string,
    branchId: string,
    id: string,
    request: UpdatePurchaseRequisitionRequest
  ): Observable<PurchaseRequisitionDto> {
    return this.http
      .put<ApiResponse<PurchaseRequisitionDto>>(this.buildPath(companyId, branchId, `/${id}`), request)
      .pipe(map(res => res.data));
  }

  deleteRequisition(
    companyId: string,
    branchId: string,
    id: string
  ): Observable<void> {
    return this.http
      .delete<ApiResponse<void>>(this.buildPath(companyId, branchId, `/${id}`))
      .pipe(map(() => undefined));
  }

  addItem(
    companyId: string,
    branchId: string,
    requisitionId: string,
    request: CreatePurchaseRequisitionItemRequest
  ): Observable<PurchaseRequisitionItemDto> {
    return this.http
      .post<ApiResponse<PurchaseRequisitionItemDto>>(
        this.buildPath(companyId, branchId, `/${requisitionId}/items`),
        request
      )
      .pipe(map(res => res.data));
  }

  updateItem(
    companyId: string,
    branchId: string,
    requisitionId: string,
    itemId: string,
    request: UpdatePurchaseRequisitionItemRequest
  ): Observable<PurchaseRequisitionItemDto> {
    return this.http
      .put<ApiResponse<PurchaseRequisitionItemDto>>(
        this.buildPath(companyId, branchId, `/${requisitionId}/items/${itemId}`),
        request
      )
      .pipe(map(res => res.data));
  }

  deleteItem(
    companyId: string,
    branchId: string,
    requisitionId: string,
    itemId: string
  ): Observable<void> {
    return this.http
      .delete<ApiResponse<void>>(
        this.buildPath(companyId, branchId, `/${requisitionId}/items/${itemId}`)
      )
      .pipe(map(() => undefined));
  }

  submitRequisition(
    companyId: string,
    branchId: string,
    requisitionId: string
  ): Observable<PurchaseRequisitionDto> {
    return this.http
      .post<ApiResponse<PurchaseRequisitionDto>>(
        this.buildPath(companyId, branchId, `/${requisitionId}/submit`),
        {}
      )
      .pipe(map(res => res.data));
  }

  approveRequisition(
    companyId: string,
    branchId: string,
    requisitionId: string,
    remarks?: string
  ): Observable<PurchaseRequisitionDto> {
    const payload: ApprovalDecisionRequest = { remarks };
    return this.http
      .post<ApiResponse<PurchaseRequisitionDto>>(
        this.buildPath(companyId, branchId, `/${requisitionId}/approve`),
        payload
      )
      .pipe(map(res => res.data));
  }

  rejectRequisition(
    companyId: string,
    branchId: string,
    requisitionId: string,
    remarks: string
  ): Observable<PurchaseRequisitionDto> {
    const payload: ApprovalDecisionRequest = { remarks };
    return this.http
      .post<ApiResponse<PurchaseRequisitionDto>>(
        this.buildPath(companyId, branchId, `/${requisitionId}/reject`),
        payload
      )
      .pipe(map(res => res.data));
  }

  cancelRequisition(
    companyId: string,
    branchId: string,
    requisitionId: string,
    remarks?: string
  ): Observable<PurchaseRequisitionDto> {
    const payload: ApprovalDecisionRequest = { remarks };
    return this.http
      .post<ApiResponse<PurchaseRequisitionDto>>(
        this.buildPath(companyId, branchId, `/${requisitionId}/cancel`),
        payload
      )
      .pipe(map(res => res.data));
  }

  getApprovalHistory(
    companyId: string,
    branchId: string,
    requisitionId: string
  ): Observable<PurchaseRequisitionHistoryDto[]> {
    return this.http
      .get<ApiResponse<PurchaseRequisitionHistoryDto[]>>(
        this.buildPath(companyId, branchId, `/${requisitionId}/approval-history`)
      )
      .pipe(map(res => res?.data ?? []));
  }
}
