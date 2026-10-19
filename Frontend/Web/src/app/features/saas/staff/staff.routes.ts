import { Routes } from '@angular/router';

export const staffRoutes: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./platform-staff.component').then(m => m.PlatformStaffComponent)
  }
];
