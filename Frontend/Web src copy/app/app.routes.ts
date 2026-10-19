import { Routes } from '@angular/router';
import { PublicLayoutComponent } from './layout/public-layout/public-layout.component';
import { WorkspaceLayoutComponent } from './layout/workspace-layout/workspace-layout.component';
import { PlatformAdminLayoutComponent } from './layout/platform-admin-layout/platform-admin-layout.component';
import { authGuard, roleGuard } from './core/guards/auth.guard';

export const routes: Routes = [
  // 1. Platform Public (Public SaaS Shell)
  {
    path: '',
    component: PublicLayoutComponent,
    children: [
      {
        path: '',
        loadComponent: () => import('./features/public/home/home.component').then(m => m.HomeComponent)
      },
      {
        path: 'directory',
        loadComponent: () => import('./features/public/directory/directory.component').then(m => m.DirectoryComponent)
      },
      {
        path: 'login',
        loadComponent: () => import('./features/public/login/login.component').then(m => m.LoginComponent)
      },
      {
        path: 'register',
        loadComponent: () => import('./features/public/register/register.component').then(m => m.RegisterComponent)
      }
    ]
  },

  // 2. Platform Admin Private (Platform Admin Workspace Shell)
  {
    path: 'platform-admin',
    component: PlatformAdminLayoutComponent,
    canActivate: [authGuard, roleGuard(['Platform Admin', 'Security Manager'])],
    children: [
      {
        path: '',
        redirectTo: 'dashboard',
        pathMatch: 'full'
      },
      {
        path: 'dashboard',
        loadComponent: () =>
          import('./features/platform-admin/dashboard/platform-admin-dashboard.component').then(
            m => m.PlatformAdminDashboardComponent
          )
      },
      {
        path: 'tenants',
        loadComponent: () =>
          import('./features/platform-admin/tenants/tenants.component').then(m => m.TenantsComponent)
      },
      {
        path: 'diagnostics',
        loadComponent: () =>
          import('./features/platform-admin/diagnostics/diagnostics.component').then(m => m.DiagnosticsComponent)
      },
      {
        path: 'subscriptions',
        redirectTo: 'dashboard'
      },
      {
        path: 'audit',
        redirectTo: 'dashboard'
      }
    ]
  },

  // 3. Platform Staff Private (Platform Staff Operations)
  {
    path: 'platform-staff',
    canActivate: [authGuard, roleGuard(['Platform Staff', 'Platform Admin', 'Security Manager'])],
    loadComponent: () =>
      import('./features/platform-staff/platform-staff.component').then(m => m.PlatformStaffComponent)
  },

  // 4. Tenant Admin Private (Tenant Organization & User Governance)
  {
    path: 'tenant-admin',
    canActivate: [authGuard, roleGuard(['Tenant Admin', 'Platform Admin'])],
    loadComponent: () =>
      import('./features/tenant-admin/tenant-admin.component').then(m => m.TenantAdminComponent)
  },

  // 5. Tenant Staff Private (Authenticated ERP Operational Workspace Shell)
  {
    path: 'app',
    component: WorkspaceLayoutComponent,
    canActivate: [authGuard],
    children: [
      {
        path: '',
        redirectTo: 'dashboard',
        pathMatch: 'full'
      },
      {
        path: 'dashboard',
        loadComponent: () => import('./features/erp/dashboard/dashboard.component').then(m => m.DashboardComponent)
      },
      {
        path: 'demonstration',
        loadComponent: () => import('./features/erp/demonstration/demonstration.component').then(m => m.DemonstrationComponent)
      },
      {
        path: 'master-data',
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
        redirectTo: 'demonstration'
      }
    ]
  },

  // 6. Tenant Storefront Public (Public Catalog & Storefront)
  {
    path: 'storefront',
    loadComponent: () => import('./features/storefront/storefront.component').then(m => m.StorefrontComponent)
  },

  // 7. Customer Private (Customer Account & Order Portal)
  {
    path: 'customer-portal',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/customer-portal/customer-portal.component').then(m => m.CustomerPortalComponent)
  },

  // 8. Access Denied (403 Forbidden Page)
  {
    path: 'access-denied',
    loadComponent: () =>
      import('./features/access-denied/access-denied.component').then(m => m.AccessDeniedComponent)
  },

  // Fallback Route
  {
    path: '**',
    redirectTo: ''
  }
];
