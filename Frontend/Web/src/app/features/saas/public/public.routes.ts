import { Routes } from '@angular/router';

export const publicRoutes: Routes = [
  {
    path: '',
    pathMatch: 'full',
    loadComponent: () => import('./home/home.component').then(m => m.HomeComponent)
  },
  {
    path: 'overview',
    loadComponent: () => import('./overview/overview.component').then(m => m.OverviewComponent)
  },
  {
    path: 'plans',
    loadComponent: () => import('./plans/plans.component').then(m => m.PlansComponent)
  },
  {
    path: 'subscription-plans',
    redirectTo: 'plans'
  },
  {
    path: 'directory',
    loadComponent: () => import('./directory/directory.component').then(m => m.DirectoryComponent)
  },
  {
    path: 'login',
    loadComponent: () => import('./login/login.component').then(m => m.LoginComponent)
  },
  {
    path: 'register',
    loadComponent: () => import('./register/register.component').then(m => m.RegisterComponent)
  }
];
