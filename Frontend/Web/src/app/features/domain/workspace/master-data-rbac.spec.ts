import { describe, it, expect, beforeEach, vi } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { Router, ActivatedRouteSnapshot, RouterStateSnapshot, UrlTree } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { permissionGuard } from '../../../core/guards/auth.guard';
import { AuthService } from '../../../core/auth/auth.service';

describe('Master Data RBAC and Permission Guard Verification', () => {
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
  });

  function createMockRoute(): ActivatedRouteSnapshot {
    return new ActivatedRouteSnapshot();
  }

  function createMockRouterState(url: string = '/app/master-data/products/new'): RouterStateSnapshot {
    return { url, root: new ActivatedRouteSnapshot() };
  }

  it('should redirect unauthenticated users to /login', () => {
    vi.spyOn(authService, 'isAuthenticated').mockReturnValue(false);

    const guard = permissionGuard(['PRODUCT.CREATE']);
    const result = TestBed.runInInjectionContext(() => guard(createMockRoute(), createMockRouterState()));

    expect(result instanceof UrlTree).toBe(true);
    const tree = result as UrlTree;
    expect(tree.toString()).toContain('/login?returnUrl=%2Fapp%2Fmaster-data%2Fproducts%2Fnew');
  });

  it('should redirect users lacking required permission to /access-denied', () => {
    vi.spyOn(authService, 'isAuthenticated').mockReturnValue(true);
    vi.spyOn(authService, 'isTenantAdmin').mockReturnValue(false);
    vi.spyOn(authService, 'isPlatformAdmin').mockReturnValue(false);
    vi.spyOn(authService, 'hasPermission').mockReturnValue(false);

    const guard = permissionGuard(['PRODUCT.CREATE']);
    const result = TestBed.runInInjectionContext(() => guard(createMockRoute(), createMockRouterState()));

    expect(result instanceof UrlTree).toBe(true);
    const tree = result as UrlTree;
    expect(tree.toString()).toContain('/access-denied');
    expect(tree.toString()).toContain('required=PRODUCT.CREATE');
  });

  it('should allow navigation when user has the required permission', () => {
    vi.spyOn(authService, 'isAuthenticated').mockReturnValue(true);
    vi.spyOn(authService, 'isTenantAdmin').mockReturnValue(false);
    vi.spyOn(authService, 'isPlatformAdmin').mockReturnValue(false);
    vi.spyOn(authService, 'hasPermission').mockImplementation((perm: string) => perm === 'PRODUCT.CREATE');

    const guard = permissionGuard(['PRODUCT.CREATE']);
    const result = TestBed.runInInjectionContext(() => guard(createMockRoute(), createMockRouterState()));

    expect(result).toBe(true);
  });

  it('should allow navigation for Tenant Admin regardless of individual permission', () => {
    vi.spyOn(authService, 'isAuthenticated').mockReturnValue(true);
    vi.spyOn(authService, 'isTenantAdmin').mockReturnValue(true);

    const guard = permissionGuard(['PRODUCT.CREATE']);
    const result = TestBed.runInInjectionContext(() => guard(createMockRoute(), createMockRouterState()));

    expect(result).toBe(true);
  });

  it('should allow navigation when user has any of the multi-permission options', () => {
    vi.spyOn(authService, 'isAuthenticated').mockReturnValue(true);
    vi.spyOn(authService, 'isTenantAdmin').mockReturnValue(false);
    vi.spyOn(authService, 'isPlatformAdmin').mockReturnValue(false);
    vi.spyOn(authService, 'hasPermission').mockImplementation((perm: string) => perm === 'WAREHOUSE.MANAGE_ZONES');

    const guard = permissionGuard(['WAREHOUSE.CREATE', 'WAREHOUSE.MANAGE_ZONES']);
    const result = TestBed.runInInjectionContext(() =>
      guard(createMockRoute(), createMockRouterState('/app/master-data/warehouses/wh-01/zones/new'))
    );

    expect(result).toBe(true);
  });

  it('should block navigation when user lacks all of the multi-permission options', () => {
    vi.spyOn(authService, 'isAuthenticated').mockReturnValue(true);
    vi.spyOn(authService, 'isTenantAdmin').mockReturnValue(false);
    vi.spyOn(authService, 'isPlatformAdmin').mockReturnValue(false);
    vi.spyOn(authService, 'hasPermission').mockReturnValue(false);

    const guard = permissionGuard(['WAREHOUSE.CREATE', 'WAREHOUSE.MANAGE_ZONES']);
    const result = TestBed.runInInjectionContext(() =>
      guard(createMockRoute(), createMockRouterState('/app/master-data/warehouses/wh-01/zones/new'))
    );

    expect(result instanceof UrlTree).toBe(true);
    const tree = result as UrlTree;
    expect(tree.toString()).toContain('/access-denied');
  });
});
