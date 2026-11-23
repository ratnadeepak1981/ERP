import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { buildApiUrl } from '../utils/api-url.util';
import { ApiResponse } from '../models/auth.models';
import {
  SalesQuotationDto,
  SalesQuotationItemDto,
  CreateSalesQuotationDraftRequest,
  UpdateSalesQuotationDraftRequest,
  CreateSalesQuotationItemRequest,
  ReturnToDraftRequest,
  QuotationDecisionRequest,
  ApprovalAuditRecord,
  SalesQuotationStatus,
  SalesQuotationType
} from '../models/sales-quotation.models';

@Injectable({
  providedIn: 'root'
})
export class SalesQuotationService {
  private readonly http = inject(HttpClient);

  private baseUrl(companyId: string, branchId: string): string {
    return buildApiUrl(`/companies/${companyId}/branches/${branchId}/sales-quotations`);
  }

  getQuotations(
    companyId: string,
    branchId: string,
    status?: SalesQuotationStatus | number,
    documentType?: SalesQuotationType | number
  ): Observable<SalesQuotationDto[]> {
    let params = new HttpParams();
    if (status !== undefined && status !== null) {
      params = params.set('status', status.toString());
    }
    if (documentType !== undefined && documentType !== null) {
      params = params.set('documentType', documentType.toString());
    }

    return this.http
      .get<ApiResponse<SalesQuotationDto[] | { items: SalesQuotationDto[] }>>(
        this.baseUrl(companyId, branchId),
        { params }
      )
      .pipe(
        map(res => {
          if (!res?.data) return [];
          if (Array.isArray(res.data)) return res.data;
          return (res.data as { items: SalesQuotationDto[] }).items || [];
        })
      );
  }

  getQuotation(
    companyId: string,
    branchId: string,
    id: string
  ): Observable<SalesQuotationDto | null> {
    return this.http
      .get<ApiResponse<SalesQuotationDto>>(`${this.baseUrl(companyId, branchId)}/${id}`)
      .pipe(map(res => res?.data ?? null));
  }

  createDraft(
    companyId: string,
    branchId: string,
    request: CreateSalesQuotationDraftRequest
  ): Observable<SalesQuotationDto> {
    return this.http
      .post<ApiResponse<SalesQuotationDto>>(this.baseUrl(companyId, branchId), request)
      .pipe(map(res => res.data));
  }

  updateDraft(
    companyId: string,
    branchId: string,
    id: string,
    request: UpdateSalesQuotationDraftRequest
  ): Observable<SalesQuotationDto> {
    return this.http
      .put<ApiResponse<SalesQuotationDto>>(`${this.baseUrl(companyId, branchId)}/${id}`, request)
      .pipe(map(res => res.data));
  }

  addItem(
    companyId: string,
    branchId: string,
    quotationId: string,
    request: CreateSalesQuotationItemRequest
  ): Observable<SalesQuotationItemDto> {
    return this.http
      .post<ApiResponse<SalesQuotationItemDto>>(
        `${this.baseUrl(companyId, branchId)}/${quotationId}/items`,
        request
      )
      .pipe(map(res => res.data));
  }

  removeItem(
    companyId: string,
    branchId: string,
    quotationId: string,
    itemId: string
  ): Observable<void> {
    return this.http
      .delete<ApiResponse<void>>(
        `${this.baseUrl(companyId, branchId)}/${quotationId}/items/${itemId}`
      )
      .pipe(map(() => void 0));
  }

  submitQuotation(
    companyId: string,
    branchId: string,
    quotationId: string
  ): Observable<SalesQuotationDto> {
    return this.http
      .post<ApiResponse<SalesQuotationDto>>(
        `${this.baseUrl(companyId, branchId)}/${quotationId}/submit`,
        {}
      )
      .pipe(map(res => res.data));
  }

  approveQuotation(
    companyId: string,
    branchId: string,
    quotationId: string,
    remarks?: string
  ): Observable<SalesQuotationDto> {
    const body: QuotationDecisionRequest = { remarks };
    return this.http
      .post<ApiResponse<SalesQuotationDto>>(
        `${this.baseUrl(companyId, branchId)}/${quotationId}/approve`,
        body
      )
      .pipe(map(res => res.data));
  }

  rejectQuotation(
    companyId: string,
    branchId: string,
    quotationId: string,
    remarks: string
  ): Observable<SalesQuotationDto> {
    const body: QuotationDecisionRequest = { remarks };
    return this.http
      .post<ApiResponse<SalesQuotationDto>>(
        `${this.baseUrl(companyId, branchId)}/${quotationId}/reject`,
        body
      )
      .pipe(map(res => res.data));
  }

  returnToDraft(
    companyId: string,
    branchId: string,
    quotationId: string,
    remarks?: string
  ): Observable<SalesQuotationDto> {
    const body: ReturnToDraftRequest = { remarks };
    return this.http
      .post<ApiResponse<SalesQuotationDto>>(
        `${this.baseUrl(companyId, branchId)}/${quotationId}/return-to-draft`,
        body
      )
      .pipe(map(res => res.data));
  }

  cancelQuotation(
    companyId: string,
    branchId: string,
    quotationId: string,
    remarks?: string
  ): Observable<SalesQuotationDto> {
    const body: QuotationDecisionRequest = { remarks };
    return this.http
      .post<ApiResponse<SalesQuotationDto>>(
        `${this.baseUrl(companyId, branchId)}/${quotationId}/cancel`,
        body
      )
      .pipe(map(res => res.data));
  }

  sendQuotationEmail(
    companyId: string,
    branchId: string,
    quotationId: string,
    retry: boolean = false
  ): Observable<SalesQuotationDto> {
    const params = new HttpParams().set('retry', retry.toString());
    return this.http
      .post<ApiResponse<SalesQuotationDto>>(
        `${this.baseUrl(companyId, branchId)}/${quotationId}/send`,
        {},
        { params }
      )
      .pipe(map(res => res.data));
  }

  downloadPdf(
    companyId: string,
    branchId: string,
    quotationId: string
  ): Observable<Blob> {
    return this.http.get(
      `${this.baseUrl(companyId, branchId)}/${quotationId}/pdf`,
      { responseType: 'blob' }
    );
  }

  getApprovalHistory(
    companyId: string,
    branchId: string,
    quotationId: string
  ): Observable<ApprovalAuditRecord[]> {
    return this.http
      .get<ApiResponse<ApprovalAuditRecord[]>>(
        `${this.baseUrl(companyId, branchId)}/${quotationId}/approval-history`
      )
      .pipe(map(res => res?.data ?? []));
  }
}
