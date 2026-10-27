import { HttpInterceptorFn, HttpErrorResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError, BehaviorSubject, filter, take, switchMap } from 'rxjs';
import { AuthService } from '../auth/auth.service';

let isRefreshing = false;
const refreshTokenSubject = new BehaviorSubject<string | null>(null);

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const authService = inject(AuthService);
  const router = inject(Router);

  const token = authService.token();
  const tenantId = authService.tenantId();

  let headers = req.headers;

  if (!headers.has('X-Requested-With')) {
    headers = headers.set('X-Requested-With', 'XMLHttpRequest');
  }

  if (token && !headers.has('Authorization')) {
    headers = headers.set('Authorization', `Bearer ${token}`);
  }

  if (tenantId && !headers.has('X-Tenant-Id')) {
    headers = headers.set('X-Tenant-Id', tenantId);
  }

  // Ensure withCredentials: true on refresh and logout requests
  const isAuthCookieEndpoint =
    req.url.includes('/Security/refresh') ||
    req.url.includes('/Security/logout') ||
    req.url.includes('/Security/login');

  const modifiedReq = req.clone({
    headers,
    withCredentials: isAuthCookieEndpoint ? true : req.withCredentials
  });

  return next(modifiedReq).pipe(
    catchError((error: unknown) => {
      if (error instanceof HttpErrorResponse && error.status === 401) {
        // Prevent infinite loops on refresh or login endpoints
        if (req.url.includes('/Security/refresh') || req.url.includes('/Security/login')) {
          isRefreshing = false;
          refreshTokenSubject.next(null);

          const currentUrl = router.url;
          const isPublicRoute =
            currentUrl === '/' ||
            currentUrl === '' ||
            currentUrl.startsWith('/overview') ||
            currentUrl.startsWith('/plans') ||
            currentUrl.startsWith('/subscription-plans') ||
            currentUrl.startsWith('/register') ||
            currentUrl.startsWith('/directory');

          if (!isPublicRoute && !currentUrl.startsWith('/login')) {
            authService.logout('');
            router.navigate(['/login'], { queryParams: { returnUrl: currentUrl } });
          }

          return throwError(() => error);
        }

        // Mutual exclusion / queuing for concurrent refresh requests
        if (!isRefreshing) {
          isRefreshing = true;
          refreshTokenSubject.next(null);

          return authService.refreshToken().pipe(
            switchMap(response => {
              isRefreshing = false;
              const newToken = response.data?.accessToken || response.data?.token || '';
              refreshTokenSubject.next(newToken);

              // Retry the original request with the freshly issued access token
              const retryHeaders = req.headers.set('Authorization', `Bearer ${newToken}`);
              return next(req.clone({ headers: retryHeaders }));
            }),
            catchError(refreshErr => {
              isRefreshing = false;
              refreshTokenSubject.next(null);

              const currentUrl = router.url;
              authService.logout('');
              router.navigate(['/login'], { queryParams: { returnUrl: currentUrl } });

              return throwError(() => refreshErr);
            })
          );
        } else {
          // Refresh is already in flight: queue behind the active refresh
          return refreshTokenSubject.pipe(
            filter(newToken => newToken !== null),
            take(1),
            switchMap(newToken => {
              const retryHeaders = req.headers.set('Authorization', `Bearer ${newToken}`);
              return next(req.clone({ headers: retryHeaders }));
            })
          );
        }
      }

      return throwError(() => error);
    })
  );
};
