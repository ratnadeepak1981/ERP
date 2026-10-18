import { Routes } from '@angular/router';

export const tenantAdminRoutes: Routes = [
  {
    path: '',
    redirectTo: 'console',
    pathMatch: 'full'
  },
  {
    path: 'console',
    loadComponent: () =>
      import('./tenant-admin.component').then(m => m.TenantAdminComponent)
  },
  {
    path: 'company-setup',
    loadComponent: () =>
      import('../workspace/company-setup/company-setup.component').then(m => m.CompanySetupComponent)
  },
  {
    path: 'companies',
    loadComponent: () =>
      import('../workspace/company-setup/company-setup.component').then(m => m.CompanySetupComponent)
  },
  {
    path: 'branches',
    loadComponent: () =>
      import('../workspace/company-setup/company-setup.component').then(m => m.CompanySetupComponent)
  },
  {
    path: 'users',
    loadComponent: () =>
      import('./user-security/user-security.component').then(m => m.UserSecurityComponent),
    data: { defaultTab: 'users' }
  },
  {
    path: 'roles',
    loadComponent: () =>
      import('./user-security/user-security.component').then(m => m.UserSecurityComponent),
    data: { defaultTab: 'roles' }
  },
  {
    path: 'permissions',
    loadComponent: () =>
      import('./user-security/user-security.component').then(m => m.UserSecurityComponent),
    data: { defaultTab: 'permissions' }
  },
  {
    path: 'user-scope',
    loadComponent: () =>
      import('./user-security/user-security.component').then(m => m.UserSecurityComponent),
    data: { defaultTab: 'user-scope' }
  }
];
