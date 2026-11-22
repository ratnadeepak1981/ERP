import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../auth/auth.service';

/**
 * Guard that enforces customer account type boundaries within the Customer Portal.
 * Prevents Retail customers from accessing B2B Quotations and Direct Orders.
 */
export const customerTypeGuard = (allowedType: 'retail' | 'company'): CanActivateFn => {
  return () => {
    const authService = inject(AuthService);
    const router = inject(Router);

    if (allowedType === 'company') {
      if (authService.isCustomerCompany()) {
        return true;
      }
      return router.createUrlTree(['/customer-portal/dashboard']);
    }

    if (allowedType === 'retail') {
      if (authService.isCustomerIndividual()) {
        return true;
      }
      return router.createUrlTree(['/customer-portal/dashboard']);
    }

    return true;
  };
};
