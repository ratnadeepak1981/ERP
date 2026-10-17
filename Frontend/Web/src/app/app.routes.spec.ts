import { describe, it, expect, beforeEach } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { Location } from '@angular/common';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { routes } from './app.routes';
import { AuthService } from './core/auth/auth.service';

describe('App Route Composition & Navigation', () => {
  let router: Router;
  let location: Location;
  let authService: AuthService;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter(routes)
      ]
    }).compileComponents();

    router = TestBed.inject(Router);
    location = TestBed.inject(Location);
    authService = TestBed.inject(AuthService);
    authService.logout('');
  });

  it('should have 9 top-level routes matching the target architecture', () => {
    expect(routes.length).toBe(9);
    expect(routes[0].path).toBe('');
    expect(routes[1].path).toBe('platform-admin');
    expect(routes[2].path).toBe('platform-staff');
    expect(routes[3].path).toBe('tenant-admin');
    expect(routes[4].path).toBe('app');
    expect(routes[5].path).toBe('storefront');
    expect(routes[6].path).toBe('customer-portal');
    expect(routes[7].path).toBe('access-denied');
    expect(routes[8].path).toBe('**');
  });

  it('should redirect unknown URLs to the fallback public homepage', async () => {
    await router.navigate(['/non-existent-random-page']);
    expect(['', '/']).toContain(location.path());
  });

  it('should redirect /subscription-plans to /plans', async () => {
    await router.navigate(['/subscription-plans']);
    expect(location.path()).toBe('/plans');
  });

  it('should redirect unauthenticated users navigating to /platform-admin to /login', async () => {
    await router.navigate(['/platform-admin']);
    expect(location.path()).toContain('/login?returnUrl=%2Fplatform-admin');
  });

  it('should redirect unauthenticated users navigating to /app to /login', async () => {
    await router.navigate(['/app']);
    expect(location.path()).toContain('/login?returnUrl=%2Fapp');
  });

  it('should redirect unauthenticated users navigating to /customer-portal to /login', async () => {
    await router.navigate(['/customer-portal']);
    expect(location.path()).toContain('/login?returnUrl=%2Fcustomer-portal');
  });
});
