import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { buildApiUrl } from '../utils/api-url.util';
import { ApiResponse } from '../models/auth.models';
import {
  RfqHeaderDto,
  RfqDetailsDto,
  RfqLineDto,
  RfqSupplierDto,
  SupplierQuotationDto,
  RfqComparisonMatrixDto,
  CreateRfqRequest,
  UpdateRfqRequest,
  CreateRfqLineRequest,
  AddRfqSupplierRequest,
  SendRfqInvitationsRequest,
  RfqInvitationResultDto,
  RecordManualQuotationRequest,
  ApproveQuotationRequest,
  RejectQuotationRequest,
  PublicRfqDetailsDto,
  SubmitPublicQuotationRequest,
  SubmitQuotationResultDto,
  getRfqStatusName,
  getSupplierInvitationStatusName,
  getSupplierQuotationStatusName
} from '../models/rfq.models';

export interface RfqQueryParams {
  page?: number;
  pageSize?: number;
  status?: string;
}

@Injectable({
  providedIn: 'root'
})
export class RfqService {
  private readonly http = inject(HttpClient);

  private buildPath(companyId: string, branchId: string, suffix = ''): string {
    return buildApiUrl(`/companies/${companyId}/branches/${branchId}/rfqs${suffix}`);
  }

  private normalizeRfqHeader(header: RfqHeaderDto): RfqHeaderDto {
    if (!header) return header;
    header.status = getRfqStatusName(header.status);
    return header;
  }

  private normalizeRfqDetails(rfq: RfqDetailsDto | null): RfqDetailsDto | null {
    if (!rfq) return null;
    this.normalizeRfqHeader(rfq);
    if (rfq.invitedSuppliers) {
      for (const sup of rfq.invitedSuppliers) {
        sup.invitationStatus = getSupplierInvitationStatusName(sup.invitationStatus);
      }
    }
    if (rfq.quotations) {
      for (const q of rfq.quotations) {
        this.normalizeSupplierQuotation(q);
      }
    }
    return rfq;
  }

  private normalizeSupplierQuotation(q: SupplierQuotationDto): SupplierQuotationDto {
    if (!q) return q;
    q.status = getSupplierQuotationStatusName(q.status);
    return q;
  }

  // --- RFQ Lifecycle Management ---

  getRfqs(
    companyId: string,
    branchId: string,
    params?: RfqQueryParams
  ): Observable<RfqHeaderDto[]> {
    let httpParams = new HttpParams();
    if (params?.page !== undefined) httpParams = httpParams.set('page', params.page.toString());
    if (params?.pageSize !== undefined) httpParams = httpParams.set('pageSize', params.pageSize.toString());
    if (params?.status) httpParams = httpParams.set('status', params.status);

    return this.http
      .get<ApiResponse<RfqHeaderDto[]>>(this.buildPath(companyId, branchId), { params: httpParams })
      .pipe(map(res => (res?.data ?? []).map(r => this.normalizeRfqHeader(r))));
  }

  getRfqById(companyId: string, branchId: string, id: string): Observable<RfqDetailsDto | null> {
    return this.http
      .get<ApiResponse<RfqDetailsDto>>(this.buildPath(companyId, branchId, `/${id}`))
      .pipe(map(res => this.normalizeRfqDetails(res?.data ?? null)));
  }

  createRfq(
    companyId: string,
    branchId: string,
    request: CreateRfqRequest
  ): Observable<RfqDetailsDto> {
    return this.http
      .post<ApiResponse<RfqDetailsDto>>(this.buildPath(companyId, branchId), request)
      .pipe(map(res => this.normalizeRfqDetails(res.data)!));
  }

  updateRfq(
    companyId: string,
    branchId: string,
    id: string,
    request: UpdateRfqRequest
  ): Observable<RfqDetailsDto> {
    return this.http
      .put<ApiResponse<RfqDetailsDto>>(this.buildPath(companyId, branchId, `/${id}`), request)
      .pipe(map(res => this.normalizeRfqDetails(res.data)!));
  }

  addLine(
    companyId: string,
    branchId: string,
    id: string,
    request: CreateRfqLineRequest
  ): Observable<RfqDetailsDto> {
    return this.http
      .post<ApiResponse<RfqDetailsDto>>(this.buildPath(companyId, branchId, `/${id}/lines`), request)
      .pipe(map(res => this.normalizeRfqDetails(res.data)!));
  }

  removeLine(
    companyId: string,
    branchId: string,
    id: string,
    lineId: string
  ): Observable<RfqDetailsDto> {
    return this.http
      .delete<ApiResponse<RfqDetailsDto>>(this.buildPath(companyId, branchId, `/${id}/lines/${lineId}`))
      .pipe(map(res => this.normalizeRfqDetails(res.data)!));
  }

  addSupplier(
    companyId: string,
    branchId: string,
    id: string,
    request: AddRfqSupplierRequest
  ): Observable<RfqDetailsDto> {
    return this.http
      .post<ApiResponse<RfqDetailsDto>>(this.buildPath(companyId, branchId, `/${id}/suppliers`), request)
      .pipe(map(res => this.normalizeRfqDetails(res.data)!));
  }

  removeSupplier(
    companyId: string,
    branchId: string,
    id: string,
    supplierId: string
  ): Observable<RfqDetailsDto> {
    return this.http
      .delete<ApiResponse<RfqDetailsDto>>(this.buildPath(companyId, branchId, `/${id}/suppliers/${supplierId}`))
      .pipe(map(res => this.normalizeRfqDetails(res.data)!));
  }

  sendInvitations(
    companyId: string,
    branchId: string,
    id: string,
    request: SendRfqInvitationsRequest
  ): Observable<RfqInvitationResultDto> {
    return this.http
      .post<ApiResponse<RfqInvitationResultDto>>(
        this.buildPath(companyId, branchId, `/${id}/send-invitations`),
        request
      )
      .pipe(map(res => res.data));
  }

  closeRfq(companyId: string, branchId: string, id: string): Observable<RfqDetailsDto> {
    return this.http
      .post<ApiResponse<RfqDetailsDto>>(this.buildPath(companyId, branchId, `/${id}/close`), {})
      .pipe(map(res => this.normalizeRfqDetails(res.data)!));
  }

  cancelRfq(
    companyId: string,
    branchId: string,
    id: string,
    reason?: string
  ): Observable<RfqDetailsDto> {
    return this.http
      .post<ApiResponse<RfqDetailsDto>>(this.buildPath(companyId, branchId, `/${id}/cancel`), { reason })
      .pipe(map(res => this.normalizeRfqDetails(res.data)!));
  }

  // --- Quotation Management ---

  getQuotations(
    companyId: string,
    branchId: string,
    id: string
  ): Observable<SupplierQuotationDto[]> {
    return this.http
      .get<ApiResponse<SupplierQuotationDto[]>>(this.buildPath(companyId, branchId, `/${id}/quotations`))
      .pipe(map(res => (res?.data ?? []).map(q => this.normalizeSupplierQuotation(q))));
  }

  getQuotation(
    companyId: string,
    branchId: string,
    id: string,
    quotationId: string
  ): Observable<SupplierQuotationDto> {
    return this.http
      .get<ApiResponse<SupplierQuotationDto>>(this.buildPath(companyId, branchId, `/${id}/quotations/${quotationId}`))
      .pipe(map(res => this.normalizeSupplierQuotation(res.data)));
  }

  recordManualQuotation(
    companyId: string,
    branchId: string,
    id: string,
    request: RecordManualQuotationRequest
  ): Observable<SupplierQuotationDto> {
    return this.http
      .post<ApiResponse<SupplierQuotationDto>>(
        this.buildPath(companyId, branchId, `/${id}/manual-quotations`),
        request
      )
      .pipe(map(res => this.normalizeSupplierQuotation(res.data)));
  }

  getComparisonMatrix(
    companyId: string,
    branchId: string,
    id: string
  ): Observable<RfqComparisonMatrixDto> {
    return this.http
      .get<ApiResponse<RfqComparisonMatrixDto>>(this.buildPath(companyId, branchId, `/${id}/comparison-matrix`))
      .pipe(map(res => {
        const matrix = res.data;
        if (matrix?.suppliers) {
          for (const s of matrix.suppliers) {
            s.status = getSupplierQuotationStatusName(s.status);
          }
        }
        return matrix;
      }));
  }

  approveQuotation(
    companyId: string,
    branchId: string,
    id: string,
    quotationId: string,
    request: ApproveQuotationRequest
  ): Observable<SupplierQuotationDto> {
    return this.http
      .post<ApiResponse<SupplierQuotationDto>>(
        this.buildPath(companyId, branchId, `/${id}/quotations/${quotationId}/approve`),
        request
      )
      .pipe(map(res => this.normalizeSupplierQuotation(res.data)));
  }

  rejectQuotation(
    companyId: string,
    branchId: string,
    id: string,
    quotationId: string,
    request: RejectQuotationRequest
  ): Observable<SupplierQuotationDto> {
    return this.http
      .post<ApiResponse<SupplierQuotationDto>>(
        this.buildPath(companyId, branchId, `/${id}/quotations/${quotationId}/reject`),
        request
      )
      .pipe(map(res => this.normalizeSupplierQuotation(res.data)));
  }

  retryAwardNotification(
    companyId: string,
    branchId: string,
    id: string,
    quotationId: string
  ): Observable<SupplierQuotationDto> {
    return this.http
      .post<ApiResponse<SupplierQuotationDto>>(
        this.buildPath(companyId, branchId, `/${id}/quotations/${quotationId}/retry-award-notification`),
        {}
      )
      .pipe(map(res => this.normalizeSupplierQuotation(res.data)));
  }

  // --- Public Supplier Portal ---

  getPublicInvitation(token: string): Observable<PublicRfqDetailsDto> {
    const url = buildApiUrl(`/public/rfqs/invitation`);
    const params = new HttpParams().set('token', token);
    return this.http
      .get<ApiResponse<PublicRfqDetailsDto>>(url, { params })
      .pipe(map(res => res.data));
  }

  submitPublicQuotation(
    token: string,
    request: SubmitPublicQuotationRequest
  ): Observable<SubmitQuotationResultDto> {
    const url = buildApiUrl(`/public/rfqs/submit`);
    const params = new HttpParams().set('token', token);
    return this.http
      .post<ApiResponse<SubmitQuotationResultDto>>(url, request, { params })
      .pipe(map(res => res.data));
  }

  validatePoCreation(
    companyId: string,
    branchId: string,
    rfqId: string,
    quotationId: string
  ): Observable<{ canProceed: boolean; message?: string }> {
    return this.http
      .get<ApiResponse<{ canProceed: boolean; message?: string }>>(
        this.buildPath(companyId, branchId, `/${rfqId}/quotations/${quotationId}/validate-po`)
      )
      .pipe(map(res => res.data));
  }
}
