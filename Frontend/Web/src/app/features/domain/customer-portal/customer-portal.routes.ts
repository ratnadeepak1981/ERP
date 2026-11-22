import { Routes } from '@angular/router';
import { customerTypeGuard } from '../../../core/guards/customer-type.guard';

export const customerPortalRoutes: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./layout/customer-workspace-layout.component').then(
        m => m.CustomerWorkspaceLayoutComponent
      ),
    children: [
      {
        path: '',
        pathMatch: 'full',
        redirectTo: 'dashboard'
      },
      {
        path: 'dashboard',
        loadComponent: () =>
          import('./dashboard/customer-dashboard.component').then(
            m => m.CustomerDashboardComponent
          )
      },
      {
        path: 'catalog',
        loadComponent: () =>
          import('../../storefront/storefront.component').then(
            m => m.StorefrontComponent
          )
      },
      {
        path: 'catalog/:productId',
        loadComponent: () =>
          import('../../storefront/product-detail/product-detail.component').then(
            m => m.ProductDetailComponent
          )
      },
      {
        path: 'cart',
        loadComponent: () =>
          import('./cart/customer-cart.component').then(
            m => m.CustomerCartComponent
          )
      },
      {
        path: 'orders',
        loadComponent: () =>
          import('./orders/customer-orders.component').then(
            m => m.CustomerOrdersComponent
          )
      },
      {
        path: 'quotations',
        canActivate: [customerTypeGuard('company')],
        loadComponent: () =>
          import('./quotations/customer-quotations.component').then(
            m => m.CustomerQuotationsComponent
          )
      },
      {
        path: 'direct-orders',
        canActivate: [customerTypeGuard('company')],
        loadComponent: () =>
          import('./direct-orders/customer-direct-orders.component').then(
            m => m.CustomerDirectOrdersComponent
          )
      },
      {
        path: 'addresses',
        loadComponent: () =>
          import('./addresses/customer-addresses.component').then(
            m => m.CustomerAddressesComponent
          )
      },
      {
        path: 'profile',
        loadComponent: () =>
          import('./profile/customer-profile.component').then(
            m => m.CustomerProfileComponent
          )
      }
    ]
  }
];
