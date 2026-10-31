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

  // Dedicated User Administration Routes
  {
    path: 'users',
    loadComponent: () =>
      import('./user-security/user-register.component').then(m => m.UserRegisterComponent)
  },
  {
    path: 'users/new',
    loadComponent: () =>
      import('./user-security/user-page.component').then(m => m.UserPageComponent)
  },
  {
    path: 'users/:id',
    loadComponent: () =>
      import('./user-security/user-page.component').then(m => m.UserPageComponent)
  },
  {
    path: 'users/:id/edit',
    loadComponent: () =>
      import('./user-security/user-page.component').then(m => m.UserPageComponent)
  },

  // Dedicated Role Administration Routes
  {
    path: 'roles',
    loadComponent: () =>
      import('./user-security/role-register.component').then(m => m.RoleRegisterComponent)
  },
  {
    path: 'roles/new',
    loadComponent: () =>
      import('./user-security/role-page.component').then(m => m.RolePageComponent)
  },
  {
    path: 'roles/:id',
    loadComponent: () =>
      import('./user-security/role-page.component').then(m => m.RolePageComponent)
  },
  {
    path: 'roles/:id/edit',
    loadComponent: () =>
      import('./user-security/role-page.component').then(m => m.RolePageComponent)
  },

  // Dedicated User Operating Scope Governance Routes
  {
    path: 'user-scope',
    loadComponent: () =>
      import('./user-security/user-scope-register.component').then(m => m.UserScopeRegisterComponent)
  },
  {
    path: 'user-scope/new',
    loadComponent: () =>
      import('./user-security/user-scope-page.component').then(m => m.UserScopePageComponent)
  },

  // Dedicated Capability & Permission Catalog Route
  {
    path: 'permissions',
    loadComponent: () =>
      import('./user-security/permission-catalog-page.component').then(m => m.PermissionCatalogPageComponent)
  },

  // Fallback / legacy console support
  {
    path: 'legacy-security',
    loadComponent: () =>
      import('./user-security/user-security.component').then(m => m.UserSecurityComponent)
  }
];
