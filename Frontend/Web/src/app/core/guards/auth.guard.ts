import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../auth/auth.service';

export const authGuard: CanActivateFn = (_route, state) => {
  const authService = inject(AuthService);
  const router = inject(Router);

  if (authService.isAuthenticated()) {
    return true;
  }

  return router.createUrlTree(['/login'], {
    queryParams: { returnUrl: state.url }
  });
};

export function roleGuard(allowedRoles: string[]): CanActivateFn {
  return (_route, state) => {
    const authService = inject(AuthService);
    const router = inject(Router);

    if (!authService.isAuthenticated()) {
      return router.createUrlTree(['/login'], {
        queryParams: { returnUrl: state.url }
      });
    }

    if (authService.hasAnyRole(allowedRoles)) {
      return true;
    }

    return router.createUrlTree(['/access-denied'], {
      queryParams: {
        attempted: state.url,
        required: allowedRoles.join(',')
      }
    });
  };
}

export function permissionGuard(requiredPermissions: string[]): CanActivateFn {
  return (_route, state) => {
    const authService = inject(AuthService);
    const router = inject(Router);

    if (!authService.isAuthenticated()) {
      return router.createUrlTree(['/login'], {
        queryParams: { returnUrl: state.url }
      });
    }

    if (
      authService.isTenantAdmin() ||
      authService.isPlatformAdmin() ||
      requiredPermissions.some(p => authService.hasPermission(p))
    ) {
      return true;
    }

    return router.createUrlTree(['/access-denied'], {
      queryParams: {
        attempted: state.url,
        required: requiredPermissions.join(',')
      }
    });
  };
}

export const workspaceGuard: CanActivateFn = (_route, state) => {
  const authService = inject(AuthService);
  const router = inject(Router);

  if (!authService.isAuthenticated()) {
    return router.createUrlTree(['/login'], {
      queryParams: { returnUrl: state.url }
    });
  }

  // Strictly block customer portal accounts from internal ERP workspace
  if (authService.isCustomer()) {
    return router.createUrlTree(['/access-denied'], {
      queryParams: {
        attempted: state.url,
        required: 'Tenant Staff / Operational User'
      }
    });
  }

  // Strictly block platform staff without explicit tenant workspace authorization
  if (
    authService.isPlatformStaff() &&
    !authService.isPlatformAdmin() &&
    !authService.isTenantAdmin() &&
    !authService.isTenantStaff()
  ) {
    return router.createUrlTree(['/access-denied'], {
      queryParams: {
        attempted: state.url,
        required: 'Tenant Staff / Operational User'
      }
    });
  }

  // Require an active tenant assignment with at least one assigned role for tenant operational users
  const hasTenantAssignment = !!authService.tenantId() && authService.roles().length > 0;

  // Permit Platform Admin, Tenant Admin, Tenant Staff, or any authenticated user associated with an active tenant
  if (
    authService.isPlatformAdmin() ||
    authService.isTenantAdmin() ||
    authService.isTenantStaff() ||
    hasTenantAssignment
  ) {
    return true;
  }

  return router.createUrlTree(['/access-denied'], {
    queryParams: {
      attempted: state.url,
      required: 'Tenant Staff / Operational User'
    }
  });
};
