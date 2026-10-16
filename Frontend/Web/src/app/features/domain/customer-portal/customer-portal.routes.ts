import { Routes } from '@angular/router';

export const customerPortalRoutes: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./customer-portal.component').then(m => m.CustomerPortalComponent)
  }
];
