import { Routes } from '@angular/router';
import { PublicLayoutComponent } from './layout/public-layout/public-layout.component';
import { TenantLayoutComponent } from './layout/tenant-layout/tenant-layout.component';
import { PlatformAdminLayoutComponent } from './layout/platform-admin-layout/platform-admin-layout.component';
import { PlatformStaffLayoutComponent } from './layout/platform-staff-layout/platform-staff-layout.component';
import { roleGuard } from './core/guards/auth.guard';

export const routes: Routes = [
  // 1. Platform Public (Public SaaS Shell)
  {
    path: '',
    component: PublicLayoutComponent,
    loadChildren: () => import('./features/saas/public/public.routes').then(m => m.publicRoutes)
  },

  // 2. Platform Admin Private (Platform Admin Workspace Shell)
  {
    path: 'platform-admin',
    component: PlatformAdminLayoutComponent,
    canActivate: [roleGuard(['Platform Admin', 'Security Manager'])],
    loadChildren: () => import('./features/saas/admin/platform-admin.routes').then(m => m.platformAdminRoutes)
  },

  // 3. Platform Staff Private (Platform Staff Operations Shell)
  {
    path: 'platform-staff',
    component: PlatformStaffLayoutComponent,
    canActivate: [roleGuard(['Platform Staff', 'Platform Admin', 'Security Manager'])],
    loadChildren: () => import('./features/saas/staff/staff.routes').then(m => m.staffRoutes)
  },

  // 4. Tenant Admin Private (Authenticated Tenant Admin Workspace Shell)
  {
    path: 'tenant-admin',
    component: TenantLayoutComponent,
    canActivate: [roleGuard(['Tenant Admin', 'Platform Admin'])],
    loadChildren: () => import('./features/domain/admin/tenant-admin.routes').then(m => m.tenantAdminRoutes)
  },

  // 5. Tenant Staff Private (Authenticated ERP Operational Workspace Shell)
  {
    path: 'app',
    component: TenantLayoutComponent,
    canActivate: [roleGuard(['Tenant Admin', 'Tenant User', 'Branch User', 'Viewer', 'Platform Admin'])],
    loadChildren: () => import('./features/domain/workspace/workspace.routes').then(m => m.workspaceRoutes)
  },

  // 6. Tenant Storefront Public (Public Catalog & Storefront)
  {
    path: 'storefront',
    loadChildren: () => import('./features/storefront/storefront.routes').then(m => m.storefrontRoutes)
  },

  // 7. Customer Private (Customer Account & Order Portal)
  {
    path: 'customer-portal',
    canActivate: [roleGuard(['Customer'])],
    loadChildren: () =>
      import('./features/domain/customer-portal/customer-portal.routes').then(m => m.customerPortalRoutes)
  },

  // 8. Access Denied (403 Forbidden Page)
  {
    path: 'access-denied',
    loadComponent: () =>
      import('./shared/components/access-denied/access-denied.component').then(m => m.AccessDeniedComponent)
  },

  // Fallback Route
  {
    path: '**',
    redirectTo: ''
  }
];
