import { describe, it, expect, beforeEach } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { Router, ActivatedRouteSnapshot, RouterStateSnapshot, UrlTree } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { authGuard, roleGuard, permissionGuard } from './auth.guard';
import { AuthService } from '../auth/auth.service';
import { UserSession } from '../models/auth.models';

describe('Auth Guards', () => {
  let authService: AuthService;
  let router: Router;

  const createSession = (roles: string[], tenantId: string | null = 'ten-001'): UserSession => ({
    userId: 'usr-test',
    username: 'testuser',
    email: 'test@test.local',
    tenantId,
    roles,
    permissions: [],
    token: 'mock.valid.token'
  });

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
    authService.logout('');
  });

  describe('authGuard', () => {
    it('should redirect unauthenticated user to login with returnUrl', () => {
      const route = {} as ActivatedRouteSnapshot;
      const state = { url: '/platform-admin/tenants' } as RouterStateSnapshot;

      const result = TestBed.runInInjectionContext(() => authGuard(route, state));

      expect(result instanceof UrlTree).toBe(true);
      const tree = result as UrlTree;
      expect(tree.toString()).toContain('/login?returnUrl=%2Fplatform-admin%2Ftenants');
    });

    it('should permit authenticated user', () => {
      authService.currentUser.set(createSession(['AnyRole']));
      const route = {} as ActivatedRouteSnapshot;
      const state = { url: '/some-page' } as RouterStateSnapshot;

      const result = TestBed.runInInjectionContext(() => authGuard(route, state));
      expect(result).toBe(true);
    });
  });

  describe('roleGuard', () => {
    it('should redirect unauthenticated user to login with returnUrl', () => {
      const guard = roleGuard(['Platform Admin']);
      const route = {} as ActivatedRouteSnapshot;
      const state = { url: '/platform-admin' } as RouterStateSnapshot;

      const result = TestBed.runInInjectionContext(() => guard(route, state));
      expect(result instanceof UrlTree).toBe(true);
      const tree = result as UrlTree;
      expect(tree.toString()).toContain('/login?returnUrl=%2Fplatform-admin');
    });

    it('should permit user having authorized role', () => {
      authService.currentUser.set(createSession(['Platform Admin']));
      const guard = roleGuard(['Platform Admin', 'Security Manager']);
      const route = {} as ActivatedRouteSnapshot;
      const state = { url: '/platform-admin/dashboard' } as RouterStateSnapshot;

      const result = TestBed.runInInjectionContext(() => guard(route, state));
      expect(result).toBe(true);
    });

    it('should redirect unauthorized role to /access-denied with attempted and required queryParams', () => {
      authService.currentUser.set(createSession(['Tenant User']));
      const guard = roleGuard(['Platform Admin', 'Security Manager']);
      const route = {} as ActivatedRouteSnapshot;
      const state = { url: '/platform-admin' } as RouterStateSnapshot;

      const result = TestBed.runInInjectionContext(() => guard(route, state));
      expect(result instanceof UrlTree).toBe(true);
      const tree = result as UrlTree;
      expect(tree.queryParams['attempted']).toBe('/platform-admin');
      expect(tree.queryParams['required']).toBe('Platform Admin,Security Manager');
    });
  });

  describe('Tenant ERP Workspace Boundary (/app)', () => {
    const workspaceGuard = roleGuard(['Tenant Admin', 'Tenant User', 'Branch User', 'Viewer', 'Platform Admin']);

    it('should permit verified tenant user roles (Tenant User, Branch User, Viewer, Tenant Admin)', () => {
      for (const role of ['Tenant Admin', 'Tenant User', 'Branch User', 'Viewer', 'Platform Admin']) {
        authService.currentUser.set(createSession([role]));
        const route = {} as ActivatedRouteSnapshot;
        const state = { url: '/app/dashboard' } as RouterStateSnapshot;

        const result = TestBed.runInInjectionContext(() => workspaceGuard(route, state));
        expect(result).toBe(true);
      }
    });

    it('should block customer accounts from accessing internal ERP workspace', () => {
      authService.currentUser.set(createSession(['Customer']));
      const route = {} as ActivatedRouteSnapshot;
      const state = { url: '/app/dashboard' } as RouterStateSnapshot;

      const result = TestBed.runInInjectionContext(() => workspaceGuard(route, state));
      expect(result instanceof UrlTree).toBe(true);
      const tree = result as UrlTree;
      expect(tree.toString()).toContain('/access-denied');
      expect(tree.queryParams['attempted']).toBe('/app/dashboard');
    });

    it('should block platform staff without explicit workspace authorization from accessing internal ERP workspace', () => {
      authService.currentUser.set(createSession(['Platform Staff']));
      const route = {} as ActivatedRouteSnapshot;
      const state = { url: '/app/dashboard' } as RouterStateSnapshot;

      const result = TestBed.runInInjectionContext(() => workspaceGuard(route, state));
      expect(result instanceof UrlTree).toBe(true);
      const tree = result as UrlTree;
      expect(tree.toString()).toContain('/access-denied');
    });
  });

  describe('Customer Portal Boundary (/customer-portal)', () => {
    const customerGuard = roleGuard(['Customer']);

    it('should permit authenticated customer identity', () => {
      authService.currentUser.set(createSession(['Customer']));
      const route = {} as ActivatedRouteSnapshot;
      const state = { url: '/customer-portal' } as RouterStateSnapshot;

      const result = TestBed.runInInjectionContext(() => customerGuard(route, state));
      expect(result).toBe(true);
    });

    it('should block platform admin or tenant staff from accessing customer portal without customer role', () => {
      for (const role of ['Platform Admin', 'Platform Staff', 'Tenant Admin', 'Tenant User']) {
        authService.currentUser.set(createSession([role]));
        const route = {} as ActivatedRouteSnapshot;
        const state = { url: '/customer-portal' } as RouterStateSnapshot;

        const result = TestBed.runInInjectionContext(() => customerGuard(route, state));
        expect(result instanceof UrlTree).toBe(true);
        const tree = result as UrlTree;
        expect(tree.toString()).toContain('/access-denied');
        expect(tree.queryParams['required']).toBe('Customer');
      }
    });
  });

  describe('permissionGuard', () => {
    const permGuard = permissionGuard(['PURCHASE_ORDER.VIEW']);

    it('should redirect unauthenticated user to login with returnUrl', () => {
      const route = {} as ActivatedRouteSnapshot;
      const state = { url: '/app/procurement' } as RouterStateSnapshot;

      const result = TestBed.runInInjectionContext(() => permGuard(route, state));
      expect(result instanceof UrlTree).toBe(true);
      const tree = result as UrlTree;
      expect(tree.toString()).toContain('/login?returnUrl=%2Fapp%2Fprocurement');
    });

    it('should permit authenticated user having required permission', () => {
      const session = createSession(['Tenant User']);
      session.permissions = ['PURCHASE_ORDER.VIEW'];
      authService.currentUser.set(session);

      const route = {} as ActivatedRouteSnapshot;
      const state = { url: '/app/procurement' } as RouterStateSnapshot;

      const result = TestBed.runInInjectionContext(() => permGuard(route, state));
      expect(result).toBe(true);
    });

    it('should permit tenant admin by default regardless of explicit permission', () => {
      authService.currentUser.set(createSession(['Tenant Admin']));

      const route = {} as ActivatedRouteSnapshot;
      const state = { url: '/app/procurement' } as RouterStateSnapshot;

      const result = TestBed.runInInjectionContext(() => permGuard(route, state));
      expect(result).toBe(true);
    });

    it('should redirect to /access-denied when user lacks permission', () => {
      const session = createSession(['Tenant User']);
      session.permissions = ['INVENTORY.VIEW'];
      authService.currentUser.set(session);

      const route = {} as ActivatedRouteSnapshot;
      const state = { url: '/app/procurement' } as RouterStateSnapshot;

      const result = TestBed.runInInjectionContext(() => permGuard(route, state));
      expect(result instanceof UrlTree).toBe(true);
      const tree = result as UrlTree;
      expect(tree.toString()).toContain('/access-denied');
      expect(tree.queryParams['attempted']).toBe('/app/procurement');
      expect(tree.queryParams['required']).toBe('PURCHASE_ORDER.VIEW');
    });
  });
});
