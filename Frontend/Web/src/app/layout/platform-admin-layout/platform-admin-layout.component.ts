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
  selector: 'app-platform-admin-layout',
  standalone: true,
  imports: [CommonModule, RouterOutlet, RouterLink, RouterLinkActive, ButtonModule, TooltipModule],
  templateUrl: './platform-admin-layout.component.html',
  styleUrl: './platform-admin-layout.component.scss'
})
export class PlatformAdminLayoutComponent {
  readonly themeService = inject(ThemeService);
  readonly authService = inject(AuthService);

  readonly sidebarCollapsed = signal<boolean>(false);

  readonly navGroups: NavGroup[] = [
    {
      groupName: 'Platform Overview',
      items: [
        { label: 'Platform Dashboard', icon: 'pi pi-chart-pie', route: '/platform-admin/dashboard' },
        { label: 'Security & Diagnostics', icon: 'pi pi-shield', route: '/platform-admin/diagnostics' }
      ]
    },
    {
      groupName: 'SaaS Administration',
      items: [
        { label: 'Tenants Directory', icon: 'pi pi-building', route: '/platform-admin/tenants' },
        { label: 'Subscription Plans', icon: 'pi pi-credit-card', route: '/platform-admin/subscriptions' },
        { label: 'Audit & Compliance', icon: 'pi pi-history', route: '/platform-admin/audit' }
      ]
    },
    {
      groupName: 'Cross-Portal Access',
      items: [
        { label: 'Platform Staff Portal', icon: 'pi pi-users', route: '/platform-staff' },
        { label: 'Tenant Operations Workspace', icon: 'pi pi-desktop', route: '/app/dashboard' },
        { label: 'Tenant Storefront', icon: 'pi pi-shopping-bag', route: '/storefront' }
      ]
    }
  ];

  toggleSidebar(): void {
    this.sidebarCollapsed.update(c => !c);
  }

  onSignOut(): void {
    this.authService.logout('/login');
  }
}
