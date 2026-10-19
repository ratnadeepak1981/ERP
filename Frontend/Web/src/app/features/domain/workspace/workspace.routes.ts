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
      },
      {
        path: 'warehouses',
        children: [
          {
            path: '',
            loadComponent: () =>
              import('./master-data/warehouses/warehouse-master.component').then(
                m => m.WarehouseMasterComponent
              )
          },
          {
            path: 'new',
            loadComponent: () =>
              import('./master-data/warehouses/warehouse-page.component').then(
                m => m.WarehousePageComponent
              )
          },
          {
            path: ':id/edit',
            loadComponent: () =>
              import('./master-data/warehouses/warehouse-page.component').then(
                m => m.WarehousePageComponent
              )
          },
          {
            path: ':id',
            loadComponent: () =>
              import('./master-data/warehouses/warehouse-page.component').then(
                m => m.WarehousePageComponent
              )
          },
          {
            path: ':warehouseId/zones/new',
            loadComponent: () =>
              import('./master-data/warehouses/zone-page.component').then(
                m => m.ZonePageComponent
              )
          },
          {
            path: ':warehouseId/zones/:zoneId/edit',
            loadComponent: () =>
              import('./master-data/warehouses/zone-page.component').then(
                m => m.ZonePageComponent
              )
          },
          {
            path: ':warehouseId/zones/:zoneId',
            loadComponent: () =>
              import('./master-data/warehouses/zone-page.component').then(
                m => m.ZonePageComponent
              )
          },
          {
            path: ':warehouseId/locations/new',
            loadComponent: () =>
              import('./master-data/warehouses/location-page.component').then(
                m => m.LocationPageComponent
              )
          },
          {
            path: ':warehouseId/locations/:locationId/edit',
            loadComponent: () =>
              import('./master-data/warehouses/location-page.component').then(
                m => m.LocationPageComponent
              )
          },
          {
            path: ':warehouseId/locations/:locationId',
            loadComponent: () =>
              import('./master-data/warehouses/location-page.component').then(
                m => m.LocationPageComponent
              )
          }
        ]
      },
      {
        path: 'locations',
        redirectTo: 'warehouses',
        pathMatch: 'full'
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
    children: [
      {
        path: '',
        loadComponent: () =>
          import('./inventory/inventory-overview.component').then(m => m.InventoryOverviewComponent)
      },
      {
        path: 'overview',
        redirectTo: '',
        pathMatch: 'full'
      },
      {
        path: 'stock-receipts',
        children: [
          {
            path: '',
            loadComponent: () =>
              import('./inventory/stock-receipt/stock-receipt-register.component').then(
                m => m.StockReceiptRegisterComponent
              )
          },
          {
            path: 'new',
            loadComponent: () =>
              import('./inventory/stock-receipt/stock-receipt-page.component').then(
                m => m.StockReceiptPageComponent
              )
          },
          {
            path: ':id',
            loadComponent: () =>
              import('./inventory/stock-receipt/stock-receipt-page.component').then(
                m => m.StockReceiptPageComponent
              )
          }
        ]
      },
      {
        path: 'store-putaway',
        loadComponent: () =>
          import('./inventory/store-putaway/store-putaway.component').then(
            m => m.StorePutawayComponent
          )
      },
      {
        path: 'transfers',
        loadComponent: () =>
          import('./inventory/transfers/transfer-stock.component').then(
            m => m.TransferStockComponent
          )
      },
      {
        path: 'count-adjust',
        loadComponent: () =>
          import('./inventory/count-adjust/count-adjust.component').then(
            m => m.CountAdjustComponent
          )
      },
      {
        path: 'issue-stock',
        children: [
          {
            path: '',
            loadComponent: () =>
              import('./inventory/issue-stock/issue-stock-register.component').then(
                m => m.IssueStockRegisterComponent
              )
          },
          {
            path: 'new',
            loadComponent: () =>
              import('./inventory/issue-stock/issue-stock-page.component').then(
                m => m.IssueStockPageComponent
              )
          },
          {
            path: ':id',
            loadComponent: () =>
              import('./inventory/issue-stock/issue-stock-page.component').then(
                m => m.IssueStockPageComponent
              )
          }
        ]
      }
    ]
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
