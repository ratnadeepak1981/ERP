import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { buildApiUrl } from '../utils/api-url.util';
import { ApiResponse } from '../models/auth.models';
import {
  CustomerAddressPortalDto,
  CreateCustomerAddressPortalRequest,
  UpdateCustomerAddressPortalRequest,
  PortalAddressTypeDto
} from '../models/customer-address.models';

@Injectable({
  providedIn: 'root'
})
export class CustomerAddressService {
  private readonly http = inject(HttpClient);

  /**
   * Retrieves all active addresses linked to the authenticated customer account.
   */
  getAddresses(): Observable<CustomerAddressPortalDto[]> {
    return this.http
      .get<ApiResponse<CustomerAddressPortalDto[]>>(buildApiUrl('/portal/customer/addresses'))
      .pipe(map(res => res?.data || []));
  }

  /**
   * Retrieves address types available for classification.
   */
  getAddressTypes(): Observable<PortalAddressTypeDto[]> {
    return this.http
      .get<ApiResponse<PortalAddressTypeDto[]>>(buildApiUrl('/portal/customer/addresses/types'))
      .pipe(map(res => res?.data || []));
  }

  /**
   * Creates a new Address Master record and links it to the authenticated customer.
   */
  createAddress(request: CreateCustomerAddressPortalRequest): Observable<CustomerAddressPortalDto> {
    return this.http
      .post<ApiResponse<CustomerAddressPortalDto>>(buildApiUrl('/portal/customer/addresses'), request)
      .pipe(map(res => res.data));
  }

  /**
   * Updates an existing customer address.
   */
  updateAddress(
    customerAddressId: string,
    request: UpdateCustomerAddressPortalRequest
  ): Observable<CustomerAddressPortalDto> {
    return this.http
      .put<ApiResponse<CustomerAddressPortalDto>>(
        buildApiUrl(`/portal/customer/addresses/${customerAddressId}`),
        request
      )
      .pipe(map(res => res.data));
  }

  /**
   * Sets an address as the customer's default for its address type.
   */
  setDefault(customerAddressId: string): Observable<boolean> {
    return this.http
      .put<ApiResponse<boolean>>(
        buildApiUrl(`/portal/customer/addresses/${customerAddressId}/default`),
        {}
      )
      .pipe(map(res => res?.succeeded ?? true));
  }

  /**
   * Deactivates a customer address linkage while preserving historical integrity.
   */
  deleteAddress(customerAddressId: string): Observable<boolean> {
    return this.http
      .delete<ApiResponse<boolean>>(
        buildApiUrl(`/portal/customer/addresses/${customerAddressId}`)
      )
      .pipe(map(res => res?.succeeded ?? true));
  }
}
