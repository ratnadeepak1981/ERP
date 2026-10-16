import { Routes } from '@angular/router';

export const workspaceRoutes: Routes = [
  {
    path: '',
    redirectTo: 'dashboard',
    pathMatch: 'full'
  },
  {
    path: 'dashboard',
    loadComponent: () => import('./dashboard/dashboard.component').then(m => m.DashboardComponent)
  },
  {
    path: 'company-setup',
    loadComponent: () => import('./company-setup/company-setup.component').then(m => m.CompanySetupComponent)
  },
  {
    path: 'companies',
    loadComponent: () => import('./company-setup/company-setup.component').then(m => m.CompanySetupComponent)
  },
  {
    path: 'branches',
    loadComponent: () => import('./company-setup/company-setup.component').then(m => m.CompanySetupComponent)
  },
  {
    path: 'users',
    loadComponent: () =>
      import('../admin/user-security/user-security.component').then(m => m.UserSecurityComponent),
    data: { defaultTab: 'users' }
  },
  {
    path: 'roles',
    loadComponent: () =>
      import('../admin/user-security/user-security.component').then(m => m.UserSecurityComponent),
    data: { defaultTab: 'roles' }
  },
  {
    path: 'permissions',
    loadComponent: () =>
      import('../admin/user-security/user-security.component').then(m => m.UserSecurityComponent),
    data: { defaultTab: 'permissions' }
  },
  {
    path: 'user-scope',
    loadComponent: () =>
      import('../admin/user-security/user-security.component').then(m => m.UserSecurityComponent),
    data: { defaultTab: 'user-scope' }
  },
  {
    path: 'tenant-admin-console',
    loadComponent: () => import('../admin/tenant-admin.component').then(m => m.TenantAdminComponent)
  },
  {
    path: 'master-data',
    children: [
      {
        path: '',
        loadComponent: () =>
          import('./master-data/master-data-overview.component').then(m => m.MasterDataOverviewComponent)
      },
      {
        path: 'products',
        loadComponent: () =>
          import('./master-data/products/product-master.component').then(m => m.ProductMasterComponent)
      },
      {
        path: 'suppliers',
        loadComponent: () =>
          import('./master-data/suppliers/supplier-master.component').then(m => m.SupplierMasterComponent)
      }
    ]
  },
  {
    path: 'demonstration',
    loadComponent: () => import('./demonstration/demonstration.component').then(m => m.DemonstrationComponent)
  },
  {
    path: 'showcase',
    redirectTo: 'demonstration'
  },
  {
    path: 'procurement',
    redirectTo: 'demonstration'
  },
  {
    path: 'inventory',
    redirectTo: 'demonstration'
  },
  {
    path: 'sales',
    redirectTo: 'demonstration'
  },
  {
    path: 'saas',
    redirectTo: 'demonstration'
  },
  {
    path: 'security',
    redirectTo: 'users'
  }
];
