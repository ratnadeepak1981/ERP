import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, map, of, catchError } from 'rxjs';
import { buildApiUrl } from '../utils/api-url.util';
import { ApiResponse } from '../models/auth.models';
import {
  CustomerSalesQuotationDto,
  CreateCustomerQuotationRequest
} from '../models/customer-quotation.models';

@Injectable({
  providedIn: 'root'
})
export class CustomerQuotationService {
  private readonly http = inject(HttpClient);

  /**
   * Retrieves all quotations belonging to the authenticated customer account.
   */
  getQuotations(status?: number): Observable<CustomerSalesQuotationDto[]> {
    let params = new HttpParams();
    if (status !== undefined && status !== null) {
      params = params.set('status', status.toString());
    }

    return this.http
      .get<ApiResponse<CustomerSalesQuotationDto[]>>(buildApiUrl('/portal/customer/quotations'), { params })
      .pipe(
        map(res => res?.data || []),
        catchError(() => of([]))
      );
  }

  /**
   * Retrieves detailed information for a single sales quotation.
   */
  getQuotationDetails(id: string): Observable<CustomerSalesQuotationDto | null> {
    if (!id) return of(null);

    return this.http
      .get<ApiResponse<CustomerSalesQuotationDto>>(buildApiUrl(`/portal/customer/quotations/${id}`))
      .pipe(
        map(res => res?.data || null),
        catchError(() => of(null))
      );
  }

  /**
   * Submits a formal Sales Quotation Request from the authenticated Company Customer.
   */
  submitQuotationRequest(
    request: CreateCustomerQuotationRequest,
    companyId?: string,
    branchId?: string
  ): Observable<ApiResponse<CustomerSalesQuotationDto>> {
    let params = new HttpParams();
    if (companyId) {
      params = params.set('companyId', companyId);
    }
    if (branchId) {
      params = params.set('branchId', branchId);
    }

    return this.http.post<ApiResponse<CustomerSalesQuotationDto>>(
      buildApiUrl('/portal/customer/quotations/request'),
      request,
      { params }
    );
  }

  /**
   * Downloads formal Sales Quotation PDF.
   */
  downloadQuotationPdf(id: string): Observable<Blob> {
    return this.http.get(buildApiUrl(`/portal/customer/quotations/${id}/pdf`), {
      responseType: 'blob'
    });
  }
}
