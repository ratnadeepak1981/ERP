import { HttpInterceptorFn, HttpErrorResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { AuthService } from '../auth/auth.service';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const authService = inject(AuthService);
  const router = inject(Router);

  const token = authService.token();
  const tenantId = authService.tenantId();

  let headers = req.headers;

  if (token && !headers.has('Authorization')) {
    headers = headers.set('Authorization', `Bearer ${token}`);
  }

  if (tenantId && !headers.has('X-Tenant-Id')) {
    headers = headers.set('X-Tenant-Id', tenantId);
  }

  const modifiedReq = req.clone({ headers });

  return next(modifiedReq).pipe(
    catchError((error: unknown) => {
      if (error instanceof HttpErrorResponse) {
        if (error.status === 401) {
          const currentUrl = router.url;
          authService.logout('');
          if (!currentUrl.startsWith('/login')) {
            router.navigate(['/login'], { queryParams: { returnUrl: currentUrl } });
          }
        }
      }
      return throwError(() => error);
    })
  );
};
