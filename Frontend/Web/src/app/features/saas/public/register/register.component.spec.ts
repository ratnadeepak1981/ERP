import { describe, it, expect, beforeEach, vi } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, ActivatedRoute } from '@angular/router';
import { of, throwError } from 'rxjs';
import { RegisterComponent } from './register.component';
import { TenantRegistrationService } from '../../../../core/services/tenant-registration.service';
import { SubscriptionPlanService } from '../../../../core/services/subscription-plan.service';
import { ApiResponse } from '../../../../core/models/auth.models';
import { TenantRegistrationResult } from '../../../../core/models/tenant-registration.model';

describe('RegisterComponent', () => {
  let component: RegisterComponent;
  let fixture: ComponentFixture<RegisterComponent>;
  let registrationService: TenantRegistrationService;
  let planService: SubscriptionPlanService;

  const validPlanId = '11111111-1111-1111-1111-111111111111';

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [RegisterComponent],
      providers: [
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              queryParamMap: {
                get: (key: string) => (key === 'planId' ? validPlanId : null)
              }
            }
          }
        },
        {
          provide: TenantRegistrationService,
          useValue: {
            registerTenant: vi.fn()
          }
        },
        {
          provide: SubscriptionPlanService,
          useValue: {
            getSubscriptionPlanById: vi.fn().mockReturnValue(of({
              data: {
                id: validPlanId,
                name: 'Free Manufacturing',
                code: 'FREE',
                price: 0
              }
            }))
          }
        }
      ]
    }).compileComponents();

    registrationService = TestBed.inject(TenantRegistrationService);
    planService = TestBed.inject(SubscriptionPlanService);
    fixture = TestBed.createComponent(RegisterComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create register component', () => {
    expect(component).toBeTruthy();
  });

  it('should carry forward selectedPlanId from query parameters', () => {
    fixture.detectChanges();
    expect(component.selectedPlanId()).toBe(validPlanId);
  });

  it('should validate form fields and display validation errors when fields are empty or invalid', () => {
    fixture.detectChanges();

    component.tenantName = '';
    component.adminFullName = '';
    component.adminEmail = 'invalid-email-address';

    const isValid = component.validateForm();
    fixture.detectChanges();

    expect(isValid).toBe(false);
    expect(component.tenantNameError()).toContain('Organization / Tenant Name is required');
    expect(component.adminFullNameError()).toContain('Primary administrator name is required');
    expect(component.adminEmailError()).toContain('Please enter a valid email address');

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Organization / Tenant Name is required');
    expect(compiled.textContent).toContain('Primary administrator name is required');
    expect(compiled.textContent).toContain('Please enter a valid email address');
  });

  it('should require a valid subscription plan ID before submitting', () => {
    component.selectedPlanId.set('');
    component.tenantName = 'Acme Corp';
    component.adminFullName = 'John Admin';
    component.adminEmail = 'admin@acme.com';

    const isValid = component.validateForm();
    expect(isValid).toBe(false);
    expect(component.planIdError()).toContain('valid subscription plan must be selected');
  });

  it('should prevent duplicate submissions while request is in progress', () => {
    const registerSpy = vi.spyOn(registrationService, 'registerTenant').mockReturnValue(of({} as any));

    component.tenantName = 'Acme Corp';
    component.adminFullName = 'John Admin';
    component.adminEmail = 'admin@acme.com';
    component.isSubmitting.set(true);

    component.onRegister();

    expect(registerSpy).not.toHaveBeenCalled();
  });

  it('should submit exact contract payload { name, subscriptionPlanId } to registration API when valid', () => {
    const mockResult: ApiResponse<TenantRegistrationResult> = {
      succeeded: true,
      message: 'Tenant registration use case completed',
      data: {
        tenant: {
          id: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
          name: 'Acme Precision Ltd',
          code: 'TEN-AAAA1111',
          isActive: true
        },
        subscription: {
          id: 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
          tenantId: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
          subscriptionPlanId: validPlanId,
          subscriptionName: 'Free Manufacturing',
          subscriptionType: 1,
          storageMode: 1,
          startDate: '2026-10-15T00:00:00Z',
          endDate: null,
          isActive: true
        }
      },
      errors: []
    };

    const registerSpy = vi.spyOn(registrationService, 'registerTenant').mockReturnValue(of(mockResult));

    component.tenantName = ' Acme Precision Ltd ';
    component.adminFullName = 'Johnathan Miller';
    component.adminEmail = 'admin@acme.com';

    component.onRegister();

    expect(registerSpy).toHaveBeenCalledWith({
      name: 'Acme Precision Ltd',
      subscriptionPlanId: validPlanId
    });
    expect(component.registrationResult()?.tenant.name).toBe('Acme Precision Ltd');
  });

  it('should display actual API error message and allow retry when registration fails', () => {
    const apiError = {
      status: 401,
      error: { message: 'Unauthorized: Platform authority is required for tenant registration.' }
    };
    vi.spyOn(registrationService, 'registerTenant').mockReturnValue(throwError(() => apiError));

    component.tenantName = 'Acme Precision Ltd';
    component.adminFullName = 'John Admin';
    component.adminEmail = 'admin@acme.com';

    component.onRegister();

    expect(component.isSubmitting()).toBe(false);
    expect(component.apiError()).toContain('Tenant Registration Request Failed (HTTP 401)');
    expect(component.apiErrorDetails()).toContain('Unauthorized: Platform authority is required');

    fixture.detectChanges();
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('.api-error-card')).toBeTruthy();
    expect(compiled.textContent).toContain('You may correct the information below and click Submit to retry');
  });

  it('should display registration confirmation with tenant and subscription details when registration succeeds', () => {
    const mockResult: ApiResponse<TenantRegistrationResult> = {
      succeeded: true,
      message: 'Success',
      data: {
        tenant: {
          id: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
          name: 'Acme Precision Ltd',
          code: 'TEN-AAAA1111',
          isActive: true
        },
        subscription: {
          id: 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
          tenantId: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
          subscriptionPlanId: validPlanId,
          subscriptionName: 'Free Manufacturing',
          subscriptionType: 1,
          storageMode: 1,
          startDate: '2026-10-15T00:00:00Z',
          endDate: null,
          isActive: true
        }
      },
      errors: []
    };
    vi.spyOn(registrationService, 'registerTenant').mockReturnValue(of(mockResult));

    component.tenantName = 'Acme Precision Ltd';
    component.adminFullName = 'John Admin';
    component.adminEmail = 'admin@acme.com';

    component.onRegister();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('.confirmation-view')).toBeTruthy();
    expect(compiled.textContent).toContain('Tenant Registration Confirmed');
    expect(compiled.textContent).toContain('TEN-AAAA1111');
    expect(compiled.textContent).toContain('Free Manufacturing');
    expect(compiled.textContent).toContain('Phase 1 Complete');
  });
});
