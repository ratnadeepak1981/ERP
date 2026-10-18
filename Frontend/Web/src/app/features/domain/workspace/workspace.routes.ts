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
    path: 'master-data',
    children: [
      {
        path: '',
        loadComponent: () =>
          import('./master-data/master-data-overview.component').then(m => m.MasterDataOverviewComponent)
      },
      {
        path: 'products',
        children: [
          {
            path: '',
            loadComponent: () =>
              import('./master-data/products/product-master.component').then(m => m.ProductMasterComponent)
          },
          {
            path: 'new',
            loadComponent: () =>
              import('./master-data/products/product-page.component').then(m => m.ProductPageComponent)
          },
          {
            path: ':id/edit',
            loadComponent: () =>
              import('./master-data/products/product-page.component').then(m => m.ProductPageComponent)
          },
          {
            path: ':id',
            loadComponent: () =>
              import('./master-data/products/product-page.component').then(m => m.ProductPageComponent)
          }
        ]
      },
      {
        path: 'suppliers',
        children: [
          {
            path: '',
            loadComponent: () =>
              import('./master-data/suppliers/supplier-master.component').then(m => m.SupplierMasterComponent)
          },
          {
            path: 'new',
            loadComponent: () =>
              import('./master-data/suppliers/supplier-page.component').then(m => m.SupplierPageComponent)
          },
          {
            path: ':id/edit',
            loadComponent: () =>
              import('./master-data/suppliers/supplier-page.component').then(m => m.SupplierPageComponent)
          },
          {
            path: ':id',
            loadComponent: () =>
              import('./master-data/suppliers/supplier-page.component').then(m => m.SupplierPageComponent)
          }
        ]
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
    children: [
      {
        path: '',
        loadComponent: () =>
          import('./procurement/procurement-overview.component').then(m => m.ProcurementOverviewComponent)
      },
      {
        path: 'overview',
        redirectTo: '',
        pathMatch: 'full'
      },
      {
        path: 'purchase-orders',
        children: [
          {
            path: '',
            loadComponent: () =>
              import('./procurement/purchase-order.component').then(m => m.PurchaseOrderComponent)
          },
          {
            path: 'new',
            loadComponent: () =>
              import('./procurement/purchase-order-page.component').then(m => m.PurchaseOrderPageComponent)
          },
          {
            path: ':id/edit',
            loadComponent: () =>
              import('./procurement/purchase-order-page.component').then(m => m.PurchaseOrderPageComponent)
          },
          {
            path: ':id',
            loadComponent: () =>
              import('./procurement/purchase-order-page.component').then(m => m.PurchaseOrderPageComponent)
          }
        ]
      },
      {
        path: 'goods-receipts',
        children: [
          {
            path: '',
            loadComponent: () =>
              import('./procurement/goods-receipt-register.component').then(m => m.GoodsReceiptRegisterComponent)
          },
          {
            path: 'new',
            loadComponent: () =>
              import('./procurement/goods-receipt-page.component').then(m => m.GoodsReceiptPageComponent)
          },
          {
            path: ':id',
            loadComponent: () =>
              import('./procurement/goods-receipt-page.component').then(m => m.GoodsReceiptPageComponent)
          }
        ]
      }
    ]
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

  // Backward-compatibility guarded redirects to dedicated Tenant Admin workspace
  {
    path: 'company-setup',
    redirectTo: '/tenant-admin/company-setup'
  },
  {
    path: 'companies',
    redirectTo: '/tenant-admin/companies'
  },
  {
    path: 'branches',
    redirectTo: '/tenant-admin/branches'
  },
  {
    path: 'users',
    redirectTo: '/tenant-admin/users'
  },
  {
    path: 'roles',
    redirectTo: '/tenant-admin/roles'
  },
  {
    path: 'permissions',
    redirectTo: '/tenant-admin/permissions'
  },
  {
    path: 'user-scope',
    redirectTo: '/tenant-admin/user-scope'
  },
  {
    path: 'tenant-admin-console',
    redirectTo: '/tenant-admin/console'
  },
  {
    path: 'security',
    redirectTo: '/tenant-admin/users'
  }
];
