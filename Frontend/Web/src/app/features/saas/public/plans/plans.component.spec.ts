import { describe, it, expect, beforeEach, vi } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { of, throwError, Subject } from 'rxjs';
import { PlansComponent } from './plans.component';
import { SubscriptionPlanService } from '../../../../core/services/subscription-plan.service';
import { SubscriptionPlan } from '../../../../core/models/subscription-plan.model';
import { ApiResponse } from '../../../../core/models/auth.models';

describe('PlansComponent', () => {
  let component: PlansComponent;
  let fixture: ComponentFixture<PlansComponent>;
  let planService: SubscriptionPlanService;
  let router: Router;

  const mockPlans: SubscriptionPlan[] = [
    {
      id: '11111111-1111-1111-1111-111111111111',
      name: 'Free Manufacturing',
      code: 'FREE',
      description: 'Free Manufacturing ERP subscription plan',
      price: 0,
      billingCycle: 2,
      isActive: true
    },
    {
      id: '22222222-2222-2222-2222-222222222222',
      name: 'Dedicated Enterprise',
      code: 'DEDICATED',
      description: 'Dedicated tenant subscription plan',
      price: 299,
      billingCycle: 2,
      isActive: true
    }
  ];

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [PlansComponent],
      providers: [
        provideRouter([]),
        {
          provide: SubscriptionPlanService,
          useValue: {
            getSubscriptionPlans: vi.fn(),
            getSubscriptionPlanById: vi.fn()
          }
        }
      ]
    }).compileComponents();

    planService = TestBed.inject(SubscriptionPlanService);
    router = TestBed.inject(Router);
    fixture = TestBed.createComponent(PlansComponent);
    component = fixture.componentInstance;
  });

  it('should create plans component', () => {
    expect(component).toBeTruthy();
  });

  it('should display loading state while fetching plans from API', () => {
    vi.spyOn(planService, 'getSubscriptionPlans').mockReturnValue(new Subject());
    fixture.detectChanges();

    expect(component.loading()).toBe(true);
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('app-loading-state')).toBeTruthy();
  });

  it('should display real subscription plans when API request is successful', () => {
    const successResponse: ApiResponse<SubscriptionPlan[]> = {
      succeeded: true,
      message: 'Plans loaded',
      data: mockPlans,
      errors: []
    };
    vi.spyOn(planService, 'getSubscriptionPlans').mockReturnValue(of(successResponse));

    fixture.detectChanges(); // triggers ngOnInit

    expect(component.loading()).toBe(false);
    expect(component.plans().length).toBe(2);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Free Manufacturing');
    expect(compiled.textContent).toContain('Dedicated Enterprise');
    expect(compiled.textContent).toContain('FREE');
    expect(compiled.textContent).toContain('DEDICATED');
    expect(compiled.textContent).toContain('11111111-1111-1111-1111-111111111111');
  });

  it('should display error message and retry option when API request fails', () => {
    const apiError = {
      status: 401,
      error: { message: 'Unauthorized access to subscription plans' }
    };
    vi.spyOn(planService, 'getSubscriptionPlans').mockReturnValue(throwError(() => apiError));

    fixture.detectChanges();

    expect(component.loading()).toBe(false);
    expect(component.errorMessage()).toContain('Backend Subscription Plans Request Failed (HTTP 401)');

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('app-error-state')).toBeTruthy();
  });

  it('should retry calling subscription plans API when retry action is invoked', () => {
    const getPlansSpy = vi.spyOn(planService, 'getSubscriptionPlans').mockReturnValue(
      throwError(() => ({ status: 500, message: 'Server error' }))
    );

    fixture.detectChanges();
    expect(getPlansSpy).toHaveBeenCalledTimes(1);

    component.loadPlans();
    expect(getPlansSpy).toHaveBeenCalledTimes(2);
  });

  it('should display empty state if API returns empty array', () => {
    const emptyResponse: ApiResponse<SubscriptionPlan[]> = {
      succeeded: true,
      message: 'Empty',
      data: [],
      errors: []
    };
    vi.spyOn(planService, 'getSubscriptionPlans').mockReturnValue(of(emptyResponse));

    fixture.detectChanges();

    expect(component.plans().length).toBe(0);
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('app-empty-state')).toBeTruthy();
  });

  it('should preserve selected plan ID and navigate to /register when plan is selected', () => {
    const successResponse: ApiResponse<SubscriptionPlan[]> = {
      succeeded: true,
      message: 'Success',
      data: mockPlans,
      errors: []
    };
    vi.spyOn(planService, 'getSubscriptionPlans').mockReturnValue(of(successResponse));
    const navigateSpy = vi.spyOn(router, 'navigate');

    fixture.detectChanges();

    const selectedPlan = mockPlans[0];
    component.selectPlan(selectedPlan);

    expect(component.selectedPlan()?.id).toBe(selectedPlan.id);
    expect(navigateSpy).toHaveBeenCalledWith(['/register'], {
      queryParams: { planId: selectedPlan.id },
      state: { plan: selectedPlan }
    });
  });
});
