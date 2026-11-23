import { Routes } from '@angular/router';

export const storefrontRoutes: Routes = [
  {
    path: '',
    loadComponent: () => import('./storefront.component').then(m => m.StorefrontComponent)
  },
  {
    path: 'catalog',
    redirectTo: '',
    pathMatch: 'full'
  },
  {
    path: 'products/:productId',
    loadComponent: () =>
      import('./product-detail/product-detail.component').then(m => m.ProductDetailComponent)
  }
];
