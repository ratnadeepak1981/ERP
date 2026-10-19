import { describe, it, expect, beforeEach } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { Router, ActivatedRouteSnapshot, RouterStateSnapshot, UrlTree } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { authGuard, roleGuard } from './auth.guard';
import { AuthService } from '../auth/auth.service';

describe('Auth Guards', () => {
  let authService: AuthService;
  let router: Router;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([])
      ]
    });

    authService = TestBed.inject(AuthService);
    router = TestBed.inject(Router);
    // ensure clear session
    authService.logout('');
  });

  it('authGuard should redirect unauthenticated user to login with returnUrl', () => {
    const route = {} as ActivatedRouteSnapshot;
    const state = { url: '/platform-admin/tenants' } as RouterStateSnapshot;

    const result = TestBed.runInInjectionContext(() => authGuard(route, state));

    expect(result instanceof UrlTree).toBe(true);
    const tree = result as UrlTree;
    expect(tree.toString()).toContain('/login?returnUrl=%2Fplatform-admin%2Ftenants');
  });

  it('roleGuard should redirect unauthenticated user to login', () => {
    const guard = roleGuard(['Platform Admin']);
    const route = {} as ActivatedRouteSnapshot;
    const state = { url: '/platform-admin' } as RouterStateSnapshot;

    const result = TestBed.runInInjectionContext(() => guard(route, state));
    expect(result instanceof UrlTree).toBe(true);
    const tree = result as UrlTree;
    expect(tree.toString()).toContain('/login?returnUrl=%2Fplatform-admin');
  });
});
