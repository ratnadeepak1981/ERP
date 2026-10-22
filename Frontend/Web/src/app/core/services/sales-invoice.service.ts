import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { buildApiUrl } from '../utils/api-url.util';
import { ApiResponse } from '../models/auth.models';
import {
  SalesInvoiceDto,
  SalesInvoiceStatus,
  UpdateSalesInvoiceRequest,
  ConfirmSalesInvoiceRequest
} from '../models/sales-invoice.models';

@Injectable({
  providedIn: 'root'
})
export class SalesInvoiceService {
  private readonly http = inject(HttpClient);

  private baseUrl(companyId: string, branchId?: string | null): string {
    if (branchId) {
      return buildApiUrl(`/companies/${companyId}/branches/${branchId}/sales-invoices`);
    }
    return buildApiUrl(`/companies/${companyId}/sales-invoices`);
  }

  getInvoices(
    companyId: string,
    branchId?: string | null,
    status?: SalesInvoiceStatus | number
  ): Observable<SalesInvoiceDto[]> {
    let params = new HttpParams();
    if (status !== undefined && status !== null) {
      params = params.set('status', status.toString());
    }

    return this.http
      .get<ApiResponse<SalesInvoiceDto[] | { items: SalesInvoiceDto[] }>>(
        this.baseUrl(companyId, branchId),
        { params }
      )
      .pipe(
        map(res => {
          if (!res?.data) return [];
          if (Array.isArray(res.data)) return res.data;
          return (res.data as { items: SalesInvoiceDto[] }).items || [];
        })
      );
  }

  getInvoice(companyId: string, branchId: string | null, id: string): Observable<SalesInvoiceDto | null> {
    return this.http
      .get<ApiResponse<SalesInvoiceDto>>(`${this.baseUrl(companyId, branchId)}/${id}`)
      .pipe(map(res => res?.data ?? null));
  }

  getInvoiceByShipment(companyId: string, branchId: string | null, shipmentId: string): Observable<SalesInvoiceDto | null> {
    return this.http
      .get<ApiResponse<SalesInvoiceDto>>(`${this.baseUrl(companyId, branchId)}/by-shipment/${shipmentId}`)
      .pipe(map(res => res?.data ?? null));
  }

  createFromShipment(
    companyId: string,
    branchId: string,
    shipmentId: string,
    invoiceNumber?: string
  ): Observable<SalesInvoiceDto> {
    let params = new HttpParams();
    if (invoiceNumber) {
      params = params.set('invoiceNumber', invoiceNumber);
    }
    return this.http
      .post<ApiResponse<SalesInvoiceDto>>(
        `${this.baseUrl(companyId, branchId)}/from-shipment/${shipmentId}`,
        {},
        { params }
      )
      .pipe(map(res => res.data));
  }

  updateDraft(
    companyId: string,
    branchId: string,
    id: string,
    request: UpdateSalesInvoiceRequest
  ): Observable<SalesInvoiceDto> {
    return this.http
      .put<ApiResponse<SalesInvoiceDto>>(
        `${this.baseUrl(companyId, branchId)}/${id}`,
        request
      )
      .pipe(map(res => res.data));
  }

  confirmInvoice(
    companyId: string,
    branchId: string,
    id: string,
    notes?: string
  ): Observable<SalesInvoiceDto> {
    const body: ConfirmSalesInvoiceRequest = { notes };
    return this.http
      .post<ApiResponse<SalesInvoiceDto>>(
        `${this.baseUrl(companyId, branchId)}/${id}/confirm`,
        body
      )
      .pipe(map(res => res.data));
  }

  cancelInvoice(
    companyId: string,
    branchId: string,
    id: string
  ): Observable<SalesInvoiceDto> {
    return this.http
      .post<ApiResponse<SalesInvoiceDto>>(
        `${this.baseUrl(companyId, branchId)}/${id}/cancel`,
        {}
      )
      .pipe(map(res => res.data));
  }
}
