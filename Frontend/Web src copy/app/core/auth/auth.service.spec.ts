import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { AuthService } from './auth.service';
import { ApiResponse, LoginResponse } from '../models/auth.models';

describe('AuthService', () => {
  let service: AuthService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([])
      ]
    });

    service = TestBed.inject(AuthService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('should be created and start in unauthenticated state by default', () => {
    expect(service).toBeTruthy();
    expect(service.isAuthenticated()).toBe(false);
    expect(service.token()).toBeNull();
  });

  it('should send login request and update session on success', () => {
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
        token: 'mock.rs256.jwt'
      },
      errors: []
    };

    service.login({ email: 'admin@platform.local', password: '123' }).subscribe(res => {
      expect(res.succeeded).toBe(true);
      expect(service.isAuthenticated()).toBe(true);
      expect(service.isPlatformAdmin()).toBe(true);
      expect(service.token()).toBe('mock.rs256.jwt');
    });

    const req = httpMock.expectOne('/api/Security/login');
    expect(req.request.method).toBe('POST');
    req.flush(mockResponse);
  });

  it('should verify roles correctly', () => {
    expect(service.hasRole('Platform Admin')).toBe(false);
    expect(service.hasAnyRole(['Platform Admin', 'Tenant Admin'])).toBe(false);
  });

  it('should clear session on logout', () => {
    service.logout('');
    expect(service.isAuthenticated()).toBe(false);
    expect(service.token()).toBeNull();
  });
});
