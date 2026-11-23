import { TestBed } from '@angular/core/testing';
import { Router, UrlTree } from '@angular/router';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { customerTypeGuard } from './customer-type.guard';
import { AuthService } from '../auth/auth.service';

describe('customerTypeGuard', () => {
  let mockAuthService: any;
  let mockRouter: any;

  beforeEach(() => {
    mockAuthService = {
      isCustomerCompany: vi.fn(),
      isCustomerIndividual: vi.fn()
    };

    mockRouter = {
      createUrlTree: vi.fn().mockImplementation((commands) => ({ toString: () => commands.join('/') } as UrlTree))
    };

    TestBed.configureTestingModule({
      providers: [
        { provide: AuthService, useValue: mockAuthService },
        { provide: Router, useValue: mockRouter }
      ]
    });
  });

  it('should allow company customer to access company routes', () => {
    mockAuthService.isCustomerCompany.mockReturnValue(true);

    const guard = customerTypeGuard('company');
    const result = TestBed.runInInjectionContext(() => guard(null as any, null as any));

    expect(result).toBe(true);
  });

  it('should redirect retail customer away from company routes to dashboard', () => {
    mockAuthService.isCustomerCompany.mockReturnValue(false);

    const guard = customerTypeGuard('company');
    const result = TestBed.runInInjectionContext(() => guard(null as any, null as any));

    expect(result).not.toBe(true);
    expect(mockRouter.createUrlTree).toHaveBeenCalledWith(['/customer-portal/dashboard']);
  });

  it('should allow retail customer to access retail routes', () => {
    mockAuthService.isCustomerIndividual.mockReturnValue(true);

    const guard = customerTypeGuard('retail');
    const result = TestBed.runInInjectionContext(() => guard(null as any, null as any));

    expect(result).toBe(true);
  });
});
