import { TestBed, getTestBed } from '@angular/core/testing';
import {
  BrowserTestingModule,
  platformBrowserTesting
} from '@angular/platform-browser/testing';
import { signal } from '@angular/core';
import { provideRouter } from '@angular/router';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { CustomerWorkspaceLayoutComponent } from './customer-workspace-layout.component';
import { AuthService } from '../../../../core/auth/auth.service';
import { TenantContextService } from '../../../../core/services/tenant-context.service';
import { ThemeService } from '../../../../core/services/theme.service';

if (typeof (globalThis as any).document === 'undefined') {
  (globalThis as any).document = {
    createElement: () => ({
      style: {},
      addEventListener: () => {},
      removeEventListener: () => {},
      dispatchEvent: () => true
    }),
    createDocumentFragment: () => ({
      addEventListener: () => {},
      removeEventListener: () => {},
      dispatchEvent: () => true
    }),
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

if ((globalThis as any).navigation && !(globalThis as any).navigation.eventTarget?.dispatchEvent) {
  (globalThis as any).navigation.eventTarget = {
    addEventListener: () => {},
    removeEventListener: () => {},
    dispatchEvent: () => true
  };
}

try {
  getTestBed().initTestEnvironment(
    BrowserTestingModule,
    platformBrowserTesting()
  );
} catch {
  // Already initialized in test environment
}

describe('CustomerWorkspaceLayoutComponent', () => {
  let component: CustomerWorkspaceLayoutComponent;
  let mockAuthService: any;
  let mockTenantContext: any;
  let mockThemeService: any;
  let isCompanySignal: ReturnType<typeof signal<boolean>>;
  let isIndividualSignal: ReturnType<typeof signal<boolean>>;

  beforeEach(() => {
    isCompanySignal = signal(false);
    isIndividualSignal = signal(true);

    mockAuthService = {
      currentUser: signal({
        userId: 'usr-123',
        email: 'customer@example.com',
        roles: ['CUSTOMER_INDIVIDUAL']
      }),
      isCustomerIndividual: isIndividualSignal,
      isCustomerCompany: isCompanySignal,
      logout: vi.fn()
    };

    mockTenantContext = {
      currentTenant: signal({
        name: 'Alpha Manufacturing Ltd',
        subdomain: 'alpha'
      }),
      subdomain: signal('alpha')
    };

    mockThemeService = {
      currentTheme: signal('clean-enterprise'),
      toggleTheme: vi.fn()
    };

    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: mockAuthService },
        { provide: TenantContextService, useValue: mockTenantContext },
        { provide: ThemeService, useValue: mockThemeService }
      ]
    });

    component = TestBed.runInInjectionContext(() => new CustomerWorkspaceLayoutComponent());
  });

  it('should create CustomerWorkspaceLayoutComponent', () => {
    expect(component).toBeTruthy();
    expect(component.brandTag()).toBe('Retail Customer Portal');
  });

  it('should render Retail Customer navigation items for individual accounts', () => {
    const groups = component.navGroups();
    const allLabels = groups.flatMap(g => g.items.map(i => i.label));

    expect(allLabels).toContain('Dashboard');
    expect(allLabels).toContain('Product Catalogue');
    expect(allLabels).toContain('Shopping Cart');
    expect(allLabels).toContain('My Orders');
    expect(allLabels).toContain('My Addresses');
    expect(allLabels).toContain('My Profile');

    // Must NOT contain B2B quotation or direct orders in retail navigation
    expect(allLabels).not.toContain('Quotations');
    expect(allLabels).not.toContain('Direct Orders');
  });

  it('should render Organisation B2B navigation items for company accounts', () => {
    isCompanySignal.set(true);
    isIndividualSignal.set(false);

    expect(component.brandTag()).toBe('B2B Corporate Portal');

    const groups = component.navGroups();
    const allLabels = groups.flatMap(g => g.items.map(i => i.label));

    expect(allLabels).toContain('Dashboard');
    expect(allLabels).toContain('Product Catalogue');
    expect(allLabels).toContain('Shopping Cart');
    expect(allLabels).toContain('Quotations');
    expect(allLabels).toContain('Direct Orders');
    expect(allLabels).toContain('Order History');
    expect(allLabels).toContain('Company Profile');
    expect(allLabels).toContain('Delivery Addresses');
  });

  it('should toggle sidebar collapsed state', () => {
    expect(component.sidebarCollapsed()).toBe(false);
    component.toggleSidebar();
    expect(component.sidebarCollapsed()).toBe(true);
    component.toggleSidebar();
    expect(component.sidebarCollapsed()).toBe(false);
  });

  it('should call authService.logout on sign out', () => {
    component.onSignOut();
    expect(mockAuthService.logout).toHaveBeenCalled();
  });
});

