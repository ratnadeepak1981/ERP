import '@angular/compiler';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { Injector, runInInjectionContext } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { of } from 'rxjs';
import { AuthService } from './auth.service';
import { ApiResponse, LoginResponse } from '../models/auth.models';

const storageMock = (() => {
  let store: Record<string, string> = {};
  return {
    getItem: (key: string) => store[key] || null,
    setItem: (key: string, value: string) => {
      store[key] = value.toString();
    },
    removeItem: (key: string) => {
      delete store[key];
    },
    clear: () => {
      store = {};
    }
  };
})();

if (typeof globalThis.localStorage === 'undefined') {
  Object.defineProperty(globalThis, 'localStorage', {
    value: storageMock,
    writable: true,
    configurable: true
  });
}

describe('AuthService', () => {
  let service: AuthService;
  let mockHttp: {
    get: ReturnType<typeof vi.fn>;
    post: ReturnType<typeof vi.fn>;
    delete: ReturnType<typeof vi.fn>;
  };
  let mockRouter: {
    navigate: ReturnType<typeof vi.fn>;
    url: string;
  };

  beforeEach(() => {
    globalThis.localStorage.clear();

    mockHttp = {
      get: vi.fn(),
      post: vi.fn(),
      delete: vi.fn()
    };

    mockRouter = {
      navigate: vi.fn(),
      url: '/dashboard'
    };

    const injector = Injector.create({
      providers: [
        { provide: HttpClient, useValue: mockHttp },
        { provide: Router, useValue: mockRouter }
      ]
    });

    service = runInInjectionContext(injector, () => new AuthService());
  });

  it('should start in unauthenticated state by default', () => {
    expect(service).toBeTruthy();
    expect(service.isAuthenticated()).toBe(false);
    expect(service.token()).toBeNull();
    expect(service.currentUser()).toBeNull();
  });

  it('should send login request with credentials and update session on success without saving refresh token to localStorage', () => {
    const mockResponse: ApiResponse<LoginResponse> = {
      succeeded: true,
      message: 'Login successful.',
      data: {
        status: 'PASS',
        message: 'Login successful.',
        userId: 'usr-001',
        username: 'admin',
        email: 'admin@platform.local',
        tenantId: null,
        roles: ['Platform Admin'],
        permissions: ['PLATFORM.ALL'],
        algorithm: 'RS256',
        issuer: 'CSharpAuthServer',
        audience: 'ERP',
        token: 'mock.rs256.jwt',
        refreshToken: 'raw-refresh-secret-token'
      },
      errors: []
    };

    mockHttp.post.mockReturnValue(of(mockResponse));

    service.login({ email: 'admin@platform.local', password: '123' }).subscribe(res => {
      expect(res.succeeded).toBe(true);
      expect(service.isAuthenticated()).toBe(true);
      expect(service.isPlatformAdmin()).toBe(true);
      expect(service.token()).toBe('mock.rs256.jwt');
    });

    expect(mockHttp.post).toHaveBeenCalledWith(
      '/api/Security/login',
      { email: 'admin@platform.local', password: '123' },
      { withCredentials: true }
    );

    // Verify localStorage NEVER contains the raw refresh token
    const stored = globalThis.localStorage.getItem('apex_erp_session');
    expect(stored).not.toBeNull();
    expect(stored).not.toContain('raw-refresh-secret-token');
  });

  it('should call refreshToken endpoint with withCredentials=true and update session on success', () => {
    const refreshResponse: ApiResponse<LoginResponse> = {
      succeeded: true,
      message: 'Token refreshed successfully.',
      data: {
        status: 'PASS',
        message: 'Token refreshed.',
        userId: 'usr-001',
        username: 'admin',
        email: 'admin@platform.local',
        tenantId: null,
        roles: ['Platform Admin'],
        permissions: ['PLATFORM.ALL'],
        token: 'new.rotated.jwt',
        accessToken: 'new.rotated.jwt'
      },
      errors: []
    };

    mockHttp.post.mockReturnValue(of(refreshResponse));

    service.refreshToken().subscribe(res => {
      expect(res.succeeded).toBe(true);
      expect(service.token()).toBe('new.rotated.jwt');
    });

    expect(mockHttp.post).toHaveBeenCalledWith(
      '/api/Security/refresh',
      {},
      { withCredentials: true }
    );
  });

  it('should verify roles correctly', () => {
    expect(service.hasRole('Platform Admin')).toBe(false);
    expect(service.hasAnyRole(['Platform Admin', 'Tenant Admin'])).toBe(false);
  });

  it('should clear session on logout and call logout endpoint', () => {
    mockHttp.post.mockReturnValue(of({ succeeded: true }));

    service.saveSession({
      userId: 'usr-1',
      username: 'test',
      email: 'test@local',
      roles: [],
      permissions: [],
      token: 'jwt'
    });
    expect(service.isAuthenticated()).toBe(true);

    service.logout('');
    expect(service.isAuthenticated()).toBe(false);
    expect(service.token()).toBeNull();

    expect(mockHttp.post).toHaveBeenCalledWith(
      '/api/Security/logout',
      {},
      { withCredentials: true }
    );
  });

  it('should call logout-all endpoint and clear session on logoutAll', () => {
    mockHttp.post.mockReturnValue(of({ succeeded: true }));

    service.saveSession({
      userId: 'usr-1',
      username: 'test',
      email: 'test@local',
      roles: [],
      permissions: [],
      token: 'jwt'
    });

    service.logoutAll('').subscribe();

    expect(service.isAuthenticated()).toBe(false);
    expect(service.token()).toBeNull();

    expect(mockHttp.post).toHaveBeenCalledWith(
      '/api/Security/logout-all',
      {},
      { withCredentials: true }
    );
  });

  it('should fetch active sessions from /api/Security/sessions', () => {
    mockHttp.get.mockReturnValue(of({
      succeeded: true,
      data: [{ id: 'sess-1', userId: 'usr-1', createdAt: '2026-10-27', isCurrent: true }]
    }));

    service.getActiveSessions().subscribe(res => {
      expect(res.succeeded).toBe(true);
      expect(res.data.length).toBe(1);
    });

    expect(mockHttp.get).toHaveBeenCalledWith('/api/Security/sessions');
  });

  it('should revoke a session via DELETE /api/Security/sessions/{id}', () => {
    mockHttp.delete.mockReturnValue(of({ succeeded: true }));

    service.revokeSession('sess-1').subscribe(res => {
      expect(res.succeeded).toBe(true);
    });

    expect(mockHttp.delete).toHaveBeenCalledWith('/api/Security/sessions/sess-1');
  });

  it('should assign canonical "Tenant Admin" role during test-login fallback and recognize isTenantAdmin', () => {
    mockHttp.post.mockReturnValue(of({
      succeeded: true,
      message: 'Test login success',
      data: {
        userId: 'tenant-user-1',
        token: 'mock-tenant-jwt'
      },
      errors: []
    }));

    service.loginTest('tenantadmin', 'pass').subscribe(res => {
      expect(res.succeeded).toBe(true);
      expect(service.isAuthenticated()).toBe(true);
      expect(service.isTenantAdmin()).toBe(true);
      expect(service.roles()).toContain('Tenant Admin');
    });

    expect(mockHttp.post).toHaveBeenCalledWith(
      '/api/Security/login-test?username=tenantadmin&password=pass',
      null
    );
  });
});
