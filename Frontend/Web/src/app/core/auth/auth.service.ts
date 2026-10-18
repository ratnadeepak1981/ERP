import { Injectable, signal, computed, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { Observable, tap, catchError, throwError } from 'rxjs';
import {
  LoginRequest,
  LoginResponse,
  UserSession,
  ApiResponse,
  DiagnosticStepResult
} from '../models/auth.models';
import { buildApiUrl } from '../utils/api-url.util';

const SESSION_STORAGE_KEY = 'apex_erp_session';

@Injectable({
  providedIn: 'root'
})
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);

  readonly currentUser = signal<UserSession | null>(this.loadInitialSession());
  readonly token = computed(() => this.currentUser()?.token ?? null);
  readonly isAuthenticated = computed(() => !!this.currentUser()?.token);
  readonly roles = computed(() => this.currentUser()?.roles ?? []);
  readonly permissions = computed(() => this.currentUser()?.permissions ?? []);
  readonly tenantId = computed(() => this.currentUser()?.tenantId ?? null);

  readonly isPlatformAdmin = computed(() => {
    const userRoles = this.roles();
    return userRoles.includes('Platform Admin') || userRoles.includes('Security Manager');
  });

  readonly isPlatformStaff = computed(() => {
    const userRoles = this.roles();
    return userRoles.includes('Platform Staff') || this.isPlatformAdmin();
  });

  readonly isTenantAdmin = computed(() => {
    return this.roles().includes('Tenant Admin');
  });

  readonly isTenantStaff = computed(() => {
    const userRoles = this.roles();
    return (
      userRoles.includes('Tenant User') ||
      userRoles.includes('Branch User') ||
      userRoles.includes('Viewer')
    );
  });

  readonly isCustomer = computed(() => {
    return this.roles().includes('Customer');
  });

  private loadInitialSession(): UserSession | null {
    try {
      if (typeof window !== 'undefined' && typeof localStorage !== 'undefined') {
        const stored = localStorage.getItem(SESSION_STORAGE_KEY);
        if (stored) {
          return JSON.parse(stored) as UserSession;
        }
      }
    } catch {
      // Gracefully handle any storage read errors
    }
    return null;
  }

  private saveSession(session: UserSession | null): void {
    try {
      if (typeof window !== 'undefined' && typeof localStorage !== 'undefined') {
        if (session) {
          localStorage.setItem(SESSION_STORAGE_KEY, JSON.stringify(session));
        } else {
          localStorage.removeItem(SESSION_STORAGE_KEY);
        }
      }
    } catch {
      // Gracefully handle storage errors
    }
    this.currentUser.set(session);
  }

  login(credentials: LoginRequest): Observable<ApiResponse<LoginResponse>> {
    return this.http
      .post<ApiResponse<LoginResponse>>(buildApiUrl('/Security/login'), {
        email: credentials.email.trim(),
        password: credentials.password
      })
      .pipe(
        tap(response => {
          if (response?.succeeded && response.data) {
            const data = response.data;
            const session: UserSession = {
              userId: data.userId,
              username: data.username,
              email: data.email,
              tenantId: data.tenantId,
              roles: data.roles || [],
              permissions: data.permissions || [],
              token: data.token
            };
            this.saveSession(session);
          }
        }),
        catchError(error => {
          return throwError(() => error);
        })
      );
  }

  loginTest(username = 'admin', password = '123'): Observable<ApiResponse<any>> {
    return this.http
      .post<ApiResponse<any>>(buildApiUrl(`/Security/login-test?username=${encodeURIComponent(username)}&password=${encodeURIComponent(password)}`), null)
      .pipe(
        tap(response => {
          if (response?.succeeded && response.data) {
            const data = response.data;
            const session: UserSession = {
              userId: data.userId || '00000000-0000-0000-0000-000000000001',
              username: data.username || username,
              email: `${username}@platform.local`,
              tenantId: data.tenantId || null,
              roles: data.roles || ['Tenant Admin'],
              permissions: data.permissions || [
                'Product.Read',
                'Product.Write',
                'PURCHASE_ORDER.VIEW',
                'PURCHASE_ORDER.CREATE',
                'PURCHASE_ORDER.SUBMIT',
                'PURCHASE_ORDER.APPROVE',
                'PURCHASE_ORDER.CANCEL'
              ],
              token: data.token
            };
            this.saveSession(session);
          }
        })
      );
  }

  logout(redirectUrl = '/login'): void {
    this.saveSession(null);
    if (redirectUrl) {
      this.router.navigate([redirectUrl]);
    }
  }

  hasRole(role: string): boolean {
    return this.roles().includes(role);
  }

  hasAnyRole(roles: string[]): boolean {
    const userRoles = this.roles();
    return roles.some(role => userRoles.includes(role));
  }

  hasPermission(permission: string): boolean {
    return this.permissions().includes(permission);
  }

  checkProtectedTest(): Observable<ApiResponse<any>> {
    return this.http.get<ApiResponse<any>>(buildApiUrl('/Security/protected-test'));
  }

  checkPlatformAdminTest(): Observable<ApiResponse<any>> {
    return this.http.get<ApiResponse<any>>(buildApiUrl('/Security/platform-admin-test'));
  }

  getSecurityStatus(): Observable<ApiResponse<any>> {
    return this.http.get<ApiResponse<any>>(buildApiUrl('/Security/status'));
  }

  runDiagnostic(): Observable<ApiResponse<DiagnosticStepResult[]>> {
    return this.http.get<ApiResponse<DiagnosticStepResult[]>>(buildApiUrl('/Security/diagnostic'));
  }
}
