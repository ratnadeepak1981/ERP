import { Component, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterOutlet, RouterLink, RouterLinkActive } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { TooltipModule } from 'primeng/tooltip';
import { ThemeService } from '../../core/services/theme.service';
import { AuthService } from '../../core/auth/auth.service';

interface NavItem {
  label: string;
  icon: string;
  route: string;
  badge?: string;
}

interface NavGroup {
  groupName: string;
  items: NavItem[];
}

@Component({
  selector: 'app-platform-staff-layout',
  standalone: true,
  imports: [CommonModule, RouterOutlet, RouterLink, RouterLinkActive, ButtonModule, TooltipModule],
  templateUrl: './platform-staff-layout.component.html',
  styleUrl: './platform-staff-layout.component.scss'
})
export class PlatformStaffLayoutComponent {
  readonly themeService = inject(ThemeService);
  readonly authService = inject(AuthService);

  readonly sidebarCollapsed = signal<boolean>(false);

  readonly navGroups: NavGroup[] = [
    {
      groupName: 'Staff Operations',
      items: [
        { label: 'Support & Triage', icon: 'pi pi-inbox', route: '/platform-staff' }
      ]
    },
    {
      groupName: 'Platform Portals',
      items: [
        { label: 'Platform Admin', icon: 'pi pi-shield', route: '/platform-admin/dashboard' },
        { label: 'Tenant Operations', icon: 'pi pi-desktop', route: '/app/dashboard' }
      ]
    }
  ];

  toggleSidebar(): void {
    this.sidebarCollapsed.update(collapsed => !collapsed);
  }

  onSignOut(): void {
    this.authService.logout('/login');
  }
}
