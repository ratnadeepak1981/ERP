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
    path: 'demonstration',
    loadComponent: () => import('./demonstration/demonstration.component').then(m => m.DemonstrationComponent)
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
];
