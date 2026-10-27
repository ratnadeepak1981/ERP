import '@angular/compiler';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { Injector, runInInjectionContext } from '@angular/core';
import { HttpRequest, HttpHeaders, HttpErrorResponse } from '@angular/common/http';
import { Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import { authInterceptor } from './auth.interceptor';
import { AuthService } from '../auth/auth.service';

describe('authInterceptor', () => {
  let mockAuthService: {
    token: ReturnType<typeof vi.fn>;
    tenantId: ReturnType<typeof vi.fn>;
    refreshToken: ReturnType<typeof vi.fn>;
    logout: ReturnType<typeof vi.fn>;
  };
  let mockRouter: {
    navigate: ReturnType<typeof vi.fn>;
    url: string;
  };
  let injector: Injector;

  beforeEach(() => {
    mockAuthService = {
      token: vi.fn().mockReturnValue(null),
      tenantId: vi.fn().mockReturnValue(null),
      refreshToken: vi.fn(),
      logout: vi.fn()
    };

    mockRouter = {
      navigate: vi.fn(),
      url: '/dashboard'
    };

    injector = Injector.create({
      providers: [
        { provide: AuthService, useValue: mockAuthService },
        { provide: Router, useValue: mockRouter }
      ]
    });
  });

  it('should attach Authorization and X-Tenant-Id headers when authenticated', () => {
    mockAuthService.token.mockReturnValue('jwt-access-token');
    mockAuthService.tenantId.mockReturnValue('tenant-123');

    const req = new HttpRequest('GET', '/api/Product');
    let capturedReq: HttpRequest<any> | null = null;
    const next = (modified: HttpRequest<any>) => {
      capturedReq = modified;
      return of({} as any);
    };

    runInInjectionContext(injector, () => authInterceptor(req, next)).subscribe();

    expect(capturedReq).not.toBeNull();
    expect(capturedReq!.headers.get('Authorization')).toBe('Bearer jwt-access-token');
    expect(capturedReq!.headers.get('X-Tenant-Id')).toBe('tenant-123');
    expect(capturedReq!.headers.get('X-Requested-With')).toBe('XMLHttpRequest');
  });

  it('should set withCredentials=true on Security refresh endpoint', () => {
    const req = new HttpRequest('POST', '/api/Security/refresh', {});
    let capturedReq: HttpRequest<any> | null = null;
    const next = (modified: HttpRequest<any>) => {
      capturedReq = modified;
      return of({} as any);
    };

    runInInjectionContext(injector, () => authInterceptor(req, next)).subscribe();

    expect(capturedReq).not.toBeNull();
    expect(capturedReq!.withCredentials).toBe(true);
  });

  it('should refresh token on 401 and replay the failed request with new Authorization header', () => {
    mockAuthService.token.mockReturnValue('expired-token');
    mockAuthService.refreshToken.mockReturnValue(
      of({
        succeeded: true,
        data: {
          accessToken: 'rotated-access-token',
          token: 'rotated-access-token'
        }
      })
    );

    const req = new HttpRequest('GET', '/api/Product', {
      headers: new HttpHeaders()
    });

    let callCount = 0;
    const dispatchedHeaders: string[] = [];
    const next = (clonedReq: HttpRequest<any>) => {
      callCount++;
      dispatchedHeaders.push(clonedReq.headers.get('Authorization') ?? '');
      if (callCount === 1) {
        return throwError(
          () => new HttpErrorResponse({ status: 401, statusText: 'Unauthorized' })
        );
      }
      return of({ id: 1, name: 'Product A' } as any);
    };

    let finalResponse: any = null;
    runInInjectionContext(injector, () => authInterceptor(req, next)).subscribe(res => {
      finalResponse = res;
    });

    expect(mockAuthService.refreshToken).toHaveBeenCalled();
    expect(callCount).toBe(2);
    expect(dispatchedHeaders[0]).toBe('Bearer expired-token');
    expect(dispatchedHeaders[1]).toBe('Bearer rotated-access-token');
    expect(finalResponse).toEqual({ id: 1, name: 'Product A' });
  });

  it('should not enter infinite refresh loop when /api/Security/refresh returns 401', () => {
    const req = new HttpRequest('POST', '/api/Security/refresh', {});
    const next = () => {
      return throwError(
        () => new HttpErrorResponse({ status: 401, statusText: 'Unauthorized' })
      );
    };

    let errorThrown: any = null;
    runInInjectionContext(injector, () => authInterceptor(req, next)).subscribe({
      error: err => {
        errorThrown = err;
      }
    });

    expect(errorThrown).not.toBeNull();
    expect(errorThrown.status).toBe(401);
    expect(mockAuthService.refreshToken).not.toHaveBeenCalled();
    expect(mockAuthService.logout).toHaveBeenCalled();
    expect(mockRouter.navigate).toHaveBeenCalledWith(['/login'], expect.anything());
  });

  it('should not trigger refresh token flow when /api/Security/login returns 401', () => {
    const req = new HttpRequest('POST', '/api/Security/login', { email: 'bad@user.com' });
    const next = () => {
      return throwError(
        () => new HttpErrorResponse({ status: 401, statusText: 'Unauthorized' })
      );
    };

    let errorThrown: any = null;
    runInInjectionContext(injector, () => authInterceptor(req, next)).subscribe({
      error: err => {
        errorThrown = err;
      }
    });

    expect(errorThrown).not.toBeNull();
    expect(errorThrown.status).toBe(401);
    expect(mockAuthService.refreshToken).not.toHaveBeenCalled();
  });
});
