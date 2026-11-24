import { describe, it, expect, beforeEach } from 'vitest';
import { TestBed, getTestBed } from '@angular/core/testing';
import { BrowserTestingModule, platformBrowserTesting } from '@angular/platform-browser/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { CustomerPortalComponent } from './customer-portal.component';
import { AuthService } from '../../../core/auth/auth.service';

if (typeof document === 'undefined') {
  (globalThis as any).document = {
    createElement: () => ({ setAttribute: () => {}, getAttribute: () => null }),
    getElementsByTagName: () => [],
    head: { appendChild: () => {} },
    body: { appendChild: () => {} }
  };
}

try {
  getTestBed().initTestEnvironment(BrowserTestingModule, platformBrowserTesting());
} catch {
  // Already initialized
}

describe('CustomerPortalComponent', () => {
  let component: CustomerPortalComponent;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        {
          provide: AuthService,
          useValue: {
            currentUser: () => ({ email: 'test@customer.com' }),
            logout: () => {}
          }
        }
      ]
    });

    component = TestBed.runInInjectionContext(() => new CustomerPortalComponent());
  });

  it('should create customer portal component and have recent orders', () => {
    expect(component).toBeTruthy();
    expect(component.recentOrders().length).toBeGreaterThan(0);
    expect(component.authService.currentUser()?.email).toBe('test@customer.com');
  });
});
