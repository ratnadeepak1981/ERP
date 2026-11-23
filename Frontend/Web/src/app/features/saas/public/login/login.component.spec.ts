import { describe, it, expect, beforeEach, vi } from 'vitest';
import { ComponentFixture, TestBed, getTestBed } from '@angular/core/testing';
import {
  BrowserTestingModule,
  platformBrowserTesting
} from '@angular/platform-browser/testing';
import { Router, provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { LoginComponent } from './login.component';
import { AuthService } from '../../../../core/auth/auth.service';

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

describe('LoginComponent', () => {
  let component: LoginComponent;

  beforeEach(() => {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    });

    component = TestBed.runInInjectionContext(() => new LoginComponent());
  });

  it('should create login component', () => {
    expect(component).toBeTruthy();
  });

  it('should allow quick fill of test credentials', () => {
    component.onQuickFill('admin@platform.local', '123');
    expect(component.email).toBe('admin@platform.local');
    expect(component.password).toBe('123');
  });

  it('should display error when submitting empty fields', () => {
    component.email = '';
    component.password = '';
    component.onLogin();
    expect(component.errorMessage()).toContain('Please provide both email and password');
  });

  describe('Post-login returnUrl navigation behavior', () => {
    it('should navigate to safe returnUrl when validated', () => {
      const router = TestBed.inject(Router);
      const navigateByUrlSpy = vi.spyOn(router as any, 'navigateByUrl').mockImplementation(() => Promise.resolve(true));

      component.returnUrl.set('/inventory/items?page=2#results');
      (component as any).navigateAfterLogin();

      expect(navigateByUrlSpy).toHaveBeenCalledWith('/inventory/items?page=2#results');
    });

    it('should reject unsafe returnUrl like https://evil.example and navigate to safe default route', () => {
      const router = TestBed.inject(Router);
      const navigateByUrlSpy = vi.spyOn(router as any, 'navigateByUrl').mockImplementation(() => Promise.resolve(true));
      const navigateSpy = vi.spyOn(router as any, 'navigate').mockImplementation(() => Promise.resolve(true));

      component.returnUrl.set('https://evil.example');
      (component as any).navigateAfterLogin();

      expect(navigateByUrlSpy).not.toHaveBeenCalled();
      expect(navigateSpy).toHaveBeenCalled();
    });

    it('should reject protocol-relative returnUrl //evil.example and use safe default route', () => {
      const router = TestBed.inject(Router);
      const navigateByUrlSpy = vi.spyOn(router as any, 'navigateByUrl').mockImplementation(() => Promise.resolve(true));
      const navigateSpy = vi.spyOn(router as any, 'navigate').mockImplementation(() => Promise.resolve(true));

      component.returnUrl.set('//evil.example');
      (component as any).navigateAfterLogin();

      expect(navigateByUrlSpy).not.toHaveBeenCalled();
      expect(navigateSpy).toHaveBeenCalled();
    });

    it('should reject auth loop /login returnUrl and use safe default route', () => {
      const router = TestBed.inject(Router);
      const navigateByUrlSpy = vi.spyOn(router as any, 'navigateByUrl').mockImplementation(() => Promise.resolve(true));
      const navigateSpy = vi.spyOn(router as any, 'navigate').mockImplementation(() => Promise.resolve(true));

      component.returnUrl.set('/login?returnUrl=/dashboard');
      (component as any).navigateAfterLogin();

      expect(navigateByUrlSpy).not.toHaveBeenCalled();
      expect(navigateSpy).toHaveBeenCalled();
    });

    it('should route Retail Customer (CUSTOMER_INDIVIDUAL or Individual Retail Customer) to /customer-portal', () => {
      const router = TestBed.inject(Router);
      const authService = TestBed.inject(AuthService);
      const navigateSpy = vi.spyOn(router, 'navigate').mockImplementation(() => Promise.resolve(true));

      authService.currentUser.set({
        userId: 'cust-retail-1',
        username: 'retailuser',
        email: 'retail@test.local',
        tenantId: 'ten-1',
        roles: ['Individual Retail Customer'],
        permissions: [],
        token: 'token-retail'
      });

      component.returnUrl.set('');
      (component as any).navigateAfterLogin();

      expect(navigateSpy).toHaveBeenCalledWith(['/customer-portal']);
    });

    it('should route Company B2B Customer (CUSTOMER_COMPANY_ADMIN or Customer Company Administrator) to /customer-portal', () => {
      const router = TestBed.inject(Router);
      const authService = TestBed.inject(AuthService);
      const navigateSpy = vi.spyOn(router, 'navigate').mockImplementation(() => Promise.resolve(true));

      authService.currentUser.set({
        userId: 'cust-company-1',
        username: 'companyuser',
        email: 'b2b@acme.com',
        tenantId: 'ten-1',
        roles: ['Customer Company Administrator'],
        permissions: [],
        token: 'token-company'
      });

      component.returnUrl.set('');
      (component as any).navigateAfterLogin();

      expect(navigateSpy).toHaveBeenCalledWith(['/customer-portal']);
    });

    it('should route Tenant Admin to /app/dashboard', () => {
      const router = TestBed.inject(Router);
      const authService = TestBed.inject(AuthService);
      const navigateSpy = vi.spyOn(router, 'navigate').mockImplementation(() => Promise.resolve(true));

      authService.currentUser.set({
        userId: 'admin-1',
        username: 'tenantadmin',
        email: 'admin@tenant.com',
        tenantId: 'ten-1',
        roles: ['Tenant Admin'],
        permissions: [],
        token: 'token-admin'
      });

      component.returnUrl.set('');
      (component as any).navigateAfterLogin();

      expect(navigateSpy).toHaveBeenCalledWith(['/app/dashboard']);
    });
  });
});
