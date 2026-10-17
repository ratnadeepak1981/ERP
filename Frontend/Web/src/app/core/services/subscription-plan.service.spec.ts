import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { SubscriptionPlanService } from './subscription-plan.service';
import { SubscriptionPlan } from '../models/subscription-plan.model';
import { ApiResponse } from '../models/auth.models';

describe('SubscriptionPlanService', () => {
  let service: SubscriptionPlanService;
  let httpMock: HttpTestingController;

  const mockPlans: SubscriptionPlan[] = [
    {
      id: '11111111-1111-1111-1111-111111111111',
      name: 'Free Manufacturing',
      code: 'FREE',
      description: 'Free tier for small operations',
      price: 0,
      billingCycle: 2,
      isActive: true
    },
    {
      id: '22222222-2222-2222-2222-222222222222',
      name: 'Dedicated Growth',
      code: 'DEDICATED',
      description: 'Dedicated isolated database tier',
      price: 199.99,
      billingCycle: 2,
      isActive: true
    }
  ];

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        SubscriptionPlanService,
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    });

    service = TestBed.inject(SubscriptionPlanService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('should call GET /api/SubscriptionPlan and return list of subscription plans', () => {
    const mockResponse: ApiResponse<SubscriptionPlan[]> = {
      succeeded: true,
      message: 'Request completed successfully.',
      data: mockPlans,
      errors: []
    };

    service.getSubscriptionPlans().subscribe(res => {
      expect(res.succeeded).toBe(true);
      expect(res.data.length).toBe(2);
      expect(res.data[0].code).toBe('FREE');
    });

    const req = httpMock.expectOne('/api/SubscriptionPlan');
    expect(req.request.method).toBe('GET');
    req.flush(mockResponse);
  });

  it('should call GET /api/SubscriptionPlan/:id for a single plan', () => {
    const singlePlan = mockPlans[0];
    const mockResponse: ApiResponse<SubscriptionPlan> = {
      succeeded: true,
      message: 'Request completed successfully.',
      data: singlePlan,
      errors: []
    };

    service.getSubscriptionPlanById(singlePlan.id).subscribe(res => {
      expect(res.succeeded).toBe(true);
      expect(res.data.id).toBe(singlePlan.id);
      expect(res.data.name).toBe('Free Manufacturing');
    });

    const req = httpMock.expectOne(`/api/SubscriptionPlan/${singlePlan.id}`);
    expect(req.request.method).toBe('GET');
    req.flush(mockResponse);
  });
});
