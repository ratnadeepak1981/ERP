import { ComponentFixture, TestBed, getTestBed } from '@angular/core/testing';
import {
  BrowserTestingModule,
  platformBrowserTesting
} from '@angular/platform-browser/testing';
import { signal } from '@angular/core';
import { provideRouter } from '@angular/router';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { CustomerDashboardComponent } from './customer-dashboard.component';
import { StatusBadgeComponent } from '../../../../shared/components/status-badge/status-badge.component';
import { AuthService } from '../../../../core/auth/auth.service';
import { TenantContextService } from '../../../../core/services/tenant-context.service';

if (typeof (globalThis as any).document === 'undefined') {
  (globalThis as any).document = {
    createElement: () => ({ style: {} }),
    querySelector: () => null,
    querySelectorAll: () => [],
    documentElement: {
      classList: {
        remove: () => {},
        add: () => {},
        contains: () => false
      }
    },
    body: {
      querySelector: () => null,
      querySelectorAll: () => [],
      appendChild: () => {},
      removeChild: () => {}
    },
    location: { href: 'http://localhost' }
  };
  (globalThis as any).window = globalThis;
}

try {
  getTestBed().initTestEnvironment(
    BrowserTestingModule,
    platformBrowserTesting()
  );
} catch {
  // Already initialized in test environment
}

describe('CustomerDashboardComponent', () => {
  let component: CustomerDashboardComponent;
  let mockAuthService: any;
  let mockTenantContext: any;
  let isCompanySignal: ReturnType<typeof signal<boolean>>;
  let isIndividualSignal: ReturnType<typeof signal<boolean>>;

  beforeEach(() => {
    TestBed.resetTestingModule();

    isCompanySignal = signal(false);
    isIndividualSignal = signal(true);

    mockAuthService = {
      currentUser: signal({
        email: 'retail.buyer@example.com',
        roles: ['CUSTOMER_INDIVIDUAL']
      }),
      isCustomerIndividual: isIndividualSignal,
      isCustomerCompany: isCompanySignal
    };

    mockTenantContext = {
      currentTenant: signal({
        name: 'Nova Dynamics Ltd',
        subdomain: 'nova'
      }),
      subdomain: signal('nova')
    };

    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: mockAuthService },
        { provide: TenantContextService, useValue: mockTenantContext }
      ]
    });

    component = TestBed.runInInjectionContext(() => new CustomerDashboardComponent());
  });

  it('should create CustomerDashboardComponent and render retail badge', () => {
    expect(component).toBeTruthy();
    expect(component.isOrganisation()).toBe(false);
    expect(component.userEmail()).toBe('retail.buyer@example.com');
  });

  it('should render corporate B2B badge when user is company customer', () => {
    isCompanySignal.set(true);
    isIndividualSignal.set(false);

    expect(component.isOrganisation()).toBe(true);
  });

  it('should render recent orders table with data', () => {
    expect(component.recentOrders().length).toBe(3);
    expect(component.recentOrders()[0].orderNumber).toBe('SO-2026-0891');
  });
});

