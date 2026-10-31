import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { buildApiUrl } from '../utils/api-url.util';
import { ApiResponse } from '../models/auth.models';
import {
  GoodsReceiptNoteDto,
  CreateGoodsReceiptNoteRequest,
  UpdateGoodsReceiptNoteRequest
} from '../models/goods-receipt-note.models';

export interface GrnQueryParams {
  page?: number;
  pageSize?: number;
  sortBy?: string;
  sortDirection?: string;
}

@Injectable({
  providedIn: 'root'
})
export class GoodsReceiptNoteService {
  private readonly http = inject(HttpClient);

  private buildPath(companyId: string, branchId: string, suffix = ''): string {
    return buildApiUrl(`/companies/${companyId}/branches/${branchId}/goods-receipt-notes${suffix}`);
  }

  getGrns(
    companyId: string,
    branchId: string,
    params?: GrnQueryParams
  ): Observable<GoodsReceiptNoteDto[]> {
    let httpParams = new HttpParams();
    if (params?.page !== undefined) httpParams = httpParams.set('page', params.page.toString());
    if (params?.pageSize !== undefined) httpParams = httpParams.set('pageSize', params.pageSize.toString());
    if (params?.sortBy) httpParams = httpParams.set('sortBy', params.sortBy);
    if (params?.sortDirection) httpParams = httpParams.set('sortDirection', params.sortDirection);

    return this.http
      .get<ApiResponse<GoodsReceiptNoteDto[] | { items: GoodsReceiptNoteDto[]; totalCount: number }>>(
        this.buildPath(companyId, branchId),
        { params: httpParams }
      )
      .pipe(
        map(res => {
          if (!res?.data) return [];
          if (Array.isArray(res.data)) return res.data;
          return (res.data as { items: GoodsReceiptNoteDto[] }).items || [];
        })
      );
  }

  getGrn(companyId: string, branchId: string, id: string): Observable<GoodsReceiptNoteDto | null> {
    return this.http
      .get<ApiResponse<GoodsReceiptNoteDto>>(this.buildPath(companyId, branchId, `/${id}`))
      .pipe(map(res => res?.data ?? null));
  }

  createDraftFromPo(
    companyId: string,
    branchId: string,
    purchaseOrderId: string,
    grnNumber?: string,
    warehouseId?: string,
    warehouseLocationId?: string,
    remarks?: string
  ): Observable<GoodsReceiptNoteDto> {
    let httpParams = new HttpParams();
    if (grnNumber) httpParams = httpParams.set('grnNumber', grnNumber);
    if (warehouseId) httpParams = httpParams.set('warehouseId', warehouseId);
    if (warehouseLocationId) httpParams = httpParams.set('warehouseLocationId', warehouseLocationId);
    if (remarks) httpParams = httpParams.set('remarks', remarks);

    return this.http
      .post<ApiResponse<GoodsReceiptNoteDto>>(
        this.buildPath(companyId, branchId, `/from-po/${purchaseOrderId}`),
        null,
        { params: httpParams }
      )
      .pipe(map(res => res.data));
  }

  createManualDraft(
    companyId: string,
    branchId: string,
    request: CreateGoodsReceiptNoteRequest
  ): Observable<GoodsReceiptNoteDto> {
    return this.http
      .post<ApiResponse<GoodsReceiptNoteDto>>(this.buildPath(companyId, branchId), request)
      .pipe(map(res => res.data));
  }

  updateDraft(
    companyId: string,
    branchId: string,
    id: string,
    request: UpdateGoodsReceiptNoteRequest
  ): Observable<GoodsReceiptNoteDto> {
    return this.http
      .put<ApiResponse<GoodsReceiptNoteDto>>(this.buildPath(companyId, branchId, `/${id}`), request)
      .pipe(map(res => res.data));
  }

  deleteDraft(companyId: string, branchId: string, id: string): Observable<void> {
    return this.http.delete<void>(this.buildPath(companyId, branchId, `/${id}`));
  }

  confirmGrn(companyId: string, branchId: string, id: string): Observable<GoodsReceiptNoteDto> {
    return this.http
      .post<ApiResponse<GoodsReceiptNoteDto>>(this.buildPath(companyId, branchId, `/${id}/confirm`), null)
      .pipe(map(res => res.data));
  }

  cancelGrn(companyId: string, branchId: string, id: string): Observable<GoodsReceiptNoteDto> {
    return this.http
      .post<ApiResponse<GoodsReceiptNoteDto>>(this.buildPath(companyId, branchId, `/${id}/cancel`), null)
      .pipe(map(res => res.data));
  }
}
