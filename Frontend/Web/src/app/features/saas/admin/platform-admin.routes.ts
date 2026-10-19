import { Routes } from '@angular/router';

export const platformAdminRoutes: Routes = [
  {
    path: '',
    redirectTo: 'dashboard',
    pathMatch: 'full'
  },
  {
    path: 'dashboard',
    loadComponent: () =>
      import('./dashboard/platform-admin-dashboard.component').then(
        m => m.PlatformAdminDashboardComponent
      )
  },
  {
    path: 'tenants',
    loadComponent: () =>
      import('./tenants/tenants.component').then(m => m.TenantsComponent)
  },
  {
    path: 'tenants/provisioning',
    loadComponent: () =>
      import('./tenant-provisioning/tenant-provisioning.component').then(
        m => m.TenantProvisioningComponent
      )
  },
  {
    path: 'tenants/:tenantId/provisioning',
    loadComponent: () =>
      import('./tenant-provisioning/tenant-provisioning.component').then(
        m => m.TenantProvisioningComponent
      )
  },
  {
    path: 'diagnostics',
    loadComponent: () =>
      import('./diagnostics/diagnostics.component').then(m => m.DiagnosticsComponent)
  },
  {
    path: 'subscriptions',
    redirectTo: 'dashboard'
  },
  {
    path: 'audit',
    redirectTo: 'dashboard'
  }
];

// Backward-compatible alias
export const adminRoutes = platformAdminRoutes;
