import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import { CustomerRegistrationService } from './customer-registration.service';
import { RegisterIndividualCustomerRequest, RegisterCompanyCustomerRequest } from '../models/customer-registration.models';

describe('CustomerRegistrationService', () => {
  let service: CustomerRegistrationService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        CustomerRegistrationService,
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    });

    service = TestBed.inject(CustomerRegistrationService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('should post individual customer registration to /portal/auth/register-individual', () => {
    const payload: RegisterIndividualCustomerRequest = {
      tenantId: '11111111-1111-1111-1111-111111111111',
      firstName: 'John',
      lastName: 'Doe',
      email: 'john.doe@example.com',
      mobileNumber: '+94771234567',
      addressLine1: '123 Main Street',
      city: 'Colombo',
      postalCode: '00100',
      country: 'Sri Lanka',
      password: 'Password123!',
      confirmPassword: 'Password123!'
    };

    service.registerIndividual(payload).subscribe(res => {
      expect(res.succeeded).toBe(true);
      expect(res.data.customerCode).toBe('CUST-IND-001');
      expect(res.data.email).toBe('john.doe@example.com');
    });

    const req = httpMock.expectOne(r => r.url.includes('/portal/auth/register-individual'));
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(payload);

    req.flush({
      succeeded: true,
      message: 'Account created successfully',
      data: {
        customerId: '22222222-2222-2222-2222-222222222222',
        customerCode: 'CUST-IND-001',
        companyName: 'John Doe',
        contactId: '33333333-3333-3333-3333-333333333333',
        userId: '44444444-4444-4444-4444-444444444444',
        email: 'john.doe@example.com',
        isExistingCompanyMatched: false,
        requiresAuthorization: false,
        canLoginImmediately: true,
        message: 'Account created successfully'
      },
      statusCode: 201
    });
  });

  it('should post company customer registration to /portal/auth/register-company', () => {
    const payload: RegisterCompanyCustomerRequest = {
      tenantId: '11111111-1111-1111-1111-111111111111',
      companyName: 'Acme Industries Ltd',
      tradingName: 'Acme Corp',
      taxNumber: 'TAX-12345',
      companyEmail: 'procurement@acme.com',
      companyPhone: '+94112345678',
      addressLine1: '45 Industrial Way',
      city: 'Colombo',
      postalCode: '00200',
      country: 'Sri Lanka',
      contactPersonName: 'Jane Smith',
      jobTitle: 'Head of Purchasing',
      email: 'jane.smith@acme.com',
      mobileNumber: '+94779876543',
      password: 'Password123!',
      confirmPassword: 'Password123!'
    };

    service.registerCompany(payload).subscribe(res => {
      expect(res.succeeded).toBe(true);
      expect(res.data.companyName).toBe('Acme Industries Ltd');
      expect(res.data.canLoginImmediately).toBe(true);
    });

    const req = httpMock.expectOne(r => r.url.includes('/portal/auth/register-company'));
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(payload);

    req.flush({
      succeeded: true,
      message: 'Company account created successfully',
      data: {
        customerId: '55555555-5555-5555-5555-555555555555',
        customerCode: 'CUST-ORG-001',
        companyName: 'Acme Industries Ltd',
        contactId: '66666666-6666-6666-6666-666666666666',
        userId: '77777777-7777-7777-7777-777777777777',
        email: 'jane.smith@acme.com',
        isExistingCompanyMatched: false,
        requiresAuthorization: false,
        canLoginImmediately: true,
        message: 'Company account created successfully'
      },
      statusCode: 201
    });
  });
});
