import { Routes } from '@angular/router';

export const tenantAdminRoutes: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./tenant-admin.component').then(m => m.TenantAdminComponent)
  }
];
