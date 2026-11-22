import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { CustomerRegisterComponent } from './customer-register.component';
import { CustomerRegistrationService } from '../../../core/services/customer-registration.service';
import { TenantContextService } from '../../../core/services/tenant-context.service';

describe('CustomerRegisterComponent', () => {
  let component: CustomerRegisterComponent;
  let fixture: ComponentFixture<CustomerRegisterComponent>;
  let mockRegistrationService: any;
  let mockTenantContext: any;

  beforeEach(async () => {
    mockRegistrationService = {
      registerIndividual: vi.fn().mockReturnValue(of({
        succeeded: true,
        message: 'Account created',
        data: {
          customerId: 'cust-123',
          customerCode: 'CUST-IND-001',
          companyName: 'Jane Doe',
          contactId: 'cnt-123',
          userId: 'usr-123',
          email: 'jane.doe@example.com',
          isExistingCompanyMatched: false,
          requiresAuthorization: false,
          canLoginImmediately: true,
          message: 'Account created successfully'
        }
      })),
      registerCompany: vi.fn().mockReturnValue(of({
        succeeded: true,
        message: 'Company account created',
        data: {
          customerId: 'cust-456',
          customerCode: 'CUST-ORG-001',
          companyName: 'Acme Industries Ltd',
          contactId: 'cnt-456',
          userId: 'usr-456',
          email: 'jane.smith@acme.com',
          isExistingCompanyMatched: false,
          requiresAuthorization: false,
          canLoginImmediately: true,
          message: 'Company account created successfully'
        }
      }))
    };

    mockTenantContext = {
      currentTenant: vi.fn().mockReturnValue({
        isTenant: true,
        tenantId: '11111111-1111-1111-1111-111111111111',
        name: 'Alpha Manufacturing Ltd',
        subdomain: 'alpha',
        industryType: 'Manufacturing',
        companyStructure: 'Single',
        eCommerceEnabled: true
      }),
      subdomain: vi.fn().mockReturnValue('alpha'),
      isTenantDomain: vi.fn().mockReturnValue(true)
    };

    await TestBed.configureTestingModule({
      imports: [CustomerRegisterComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: CustomerRegistrationService, useValue: mockRegistrationService },
        { provide: TenantContextService, useValue: mockTenantContext }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(CustomerRegisterComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create CustomerRegisterComponent', () => {
    expect(component).toBeTruthy();
    expect(component.accountType()).toBe('individual');
  });

  it('should switch between Individual and Organisation account tabs', () => {
    component.setAccountType('organisation');
    expect(component.accountType()).toBe('organisation');

    component.setAccountType('individual');
    expect(component.accountType()).toBe('individual');
  });

  it('should validate required fields for Individual registration', () => {
    component.indFirstName = '';
    component.indLastName = '';
    component.onSubmit();

    expect(component.errorMessage()).toContain('First name and last name are required');
    expect(mockRegistrationService.registerIndividual).not.toHaveBeenCalled();
  });

  it('should reject password mismatch for Individual registration', () => {
    component.indFirstName = 'Jane';
    component.indLastName = 'Doe';
    component.indEmail = 'jane.doe@example.com';
    component.indPassword = 'Password123!';
    component.indConfirmPassword = 'DifferentPassword!';
    component.onSubmit();

    expect(component.errorMessage()).toContain('Passwords do not match');
    expect(mockRegistrationService.registerIndividual).not.toHaveBeenCalled();
  });

  it('should submit valid Individual registration and render success state', () => {
    component.indFirstName = 'Jane';
    component.indLastName = 'Doe';
    component.indEmail = 'jane.doe@example.com';
    component.indMobileNumber = '+94771234567';
    component.indAddressLine1 = '123 Main St';
    component.indCity = 'Colombo';
    component.indPassword = 'Password123!';
    component.indConfirmPassword = 'Password123!';

    component.onSubmit();

    expect(mockRegistrationService.registerIndividual).toHaveBeenCalledWith(expect.objectContaining({
      firstName: 'Jane',
      lastName: 'Doe',
      email: 'jane.doe@example.com'
    }));

    expect(component.successResult()).toBeTruthy();
    expect(component.successResult()?.customerCode).toBe('CUST-IND-001');

    fixture.detectChanges();
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('.success-card')?.textContent).toContain('Registration Successful!');
    expect(compiled.querySelector('.success-card')?.textContent).toContain('CUST-IND-001');
  });

  it('should submit valid Organisation registration and render success state', () => {
    component.setAccountType('organisation');
    component.orgCompanyName = 'Acme Industries Ltd';
    component.orgContactPersonName = 'Jane Smith';
    component.orgEmail = 'jane.smith@acme.com';
    component.orgPassword = 'Password123!';
    component.orgConfirmPassword = 'Password123!';

    component.onSubmit();

    expect(mockRegistrationService.registerCompany).toHaveBeenCalledWith(expect.objectContaining({
      companyName: 'Acme Industries Ltd',
      contactPersonName: 'Jane Smith',
      email: 'jane.smith@acme.com'
    }));

    expect(component.successResult()).toBeTruthy();
    expect(component.successResult()?.customerCode).toBe('CUST-ORG-001');
  });

  it('should display error banner if registration service fails', () => {
    mockRegistrationService.registerIndividual.mockReturnValueOnce(
      throwError(() => ({ error: { message: 'A user with this email address is already registered in this tenant.' } }))
    );

    component.indFirstName = 'Jane';
    component.indLastName = 'Doe';
    component.indEmail = 'duplicate@example.com';
    component.indPassword = 'Password123!';
    component.indConfirmPassword = 'Password123!';

    component.onSubmit();

    expect(component.errorMessage()).toContain('already registered in this tenant');
    expect(component.successResult()).toBeNull();
  });
});
