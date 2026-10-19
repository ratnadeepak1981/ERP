import { Routes } from '@angular/router';

export const storefrontRoutes: Routes = [
  {
    path: '',
    loadComponent: () => import('./storefront.component').then(m => m.StorefrontComponent)
  }
];
