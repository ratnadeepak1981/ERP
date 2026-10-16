import { Routes } from '@angular/router';

export const tenantAdminRoutes: Routes = [
  {
    path: '',
    redirectTo: '/app/dashboard',
    pathMatch: 'full'
  },
  {
    path: 'console',
    loadComponent: () =>
      import('./tenant-admin.component').then(m => m.TenantAdminComponent)
  }
];
