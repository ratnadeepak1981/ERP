import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { buildApiUrl } from '../utils/api-url.util';
import { ApiResponse } from '../models/auth.models';
import {
  RegisterIndividualCustomerRequest,
  RegisterCompanyCustomerRequest,
  CustomerRegistrationResult
} from '../models/customer-registration.models';

@Injectable({
  providedIn: 'root'
})
export class CustomerRegistrationService {
  private readonly http = inject(HttpClient);

  /**
   * Registers a new Individual Retail Customer.
   * Creates Customer, Contact, CustomerContact, and Security User with CUSTOMER_INDIVIDUAL role.
   */
  registerIndividual(
    request: RegisterIndividualCustomerRequest
  ): Observable<ApiResponse<CustomerRegistrationResult>> {
    return this.http.post<ApiResponse<CustomerRegistrationResult>>(
      buildApiUrl('/portal/auth/register-individual'),
      request
    );
  }

  /**
   * Registers a new Organisation / Company Customer.
   * Performs deduplication on company, creates Contact, CustomerContact, and Security User with CUSTOMER_COMPANY_ADMIN role.
   */
  registerCompany(
    request: RegisterCompanyCustomerRequest
  ): Observable<ApiResponse<CustomerRegistrationResult>> {
    return this.http.post<ApiResponse<CustomerRegistrationResult>>(
      buildApiUrl('/portal/auth/register-company'),
      request
    );
  }
}
