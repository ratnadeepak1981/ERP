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
  selector: 'app-workspace-layout',
  standalone: true,
  imports: [CommonModule, RouterOutlet, RouterLink, RouterLinkActive, ButtonModule, TooltipModule],
  templateUrl: './workspace-layout.component.html',
  styleUrl: './workspace-layout.component.scss'
})
export class WorkspaceLayoutComponent {
  readonly themeService = inject(ThemeService);
  readonly authService = inject(AuthService);

  readonly sidebarCollapsed = signal<boolean>(false);
  readonly currentTenantName = signal<string>('Alpha Manufacturing Ltd');
  readonly currentCompanyBranch = signal<string>('Main Plant (Colombo)');

  onSignOut(): void {
    this.authService.logout('/login');
  }

  readonly navGroups: NavGroup[] = [
    {
      groupName: 'Overview',
      items: [
        { label: 'ERP Dashboard', icon: 'pi pi-chart-bar', route: '/app/dashboard' },
        { label: 'Component Showcase', icon: 'pi pi-th-large', route: '/app/demonstration' }
      ]
    },
    {
      groupName: 'Operations & Supply Chain',
      items: [
        { label: 'Master Data', icon: 'pi pi-database', route: '/app/master-data' },
        { label: 'Procurement', icon: 'pi pi-shopping-cart', route: '/app/procurement' },
        { label: 'Inventory & Stock', icon: 'pi pi-box', route: '/app/inventory' },
        { label: 'Sales & Fulfillment', icon: 'pi pi-send', route: '/app/sales' }
      ]
    },
    {
      groupName: 'Platform & Security',
      items: [
        { label: 'SaaS Administration', icon: 'pi pi-sliders-h', route: '/app/saas' },
        { label: 'Access & Roles', icon: 'pi pi-shield', route: '/app/security' }
      ]
    }
  ];

  toggleSidebar(): void {
    this.sidebarCollapsed.update(collapsed => !collapsed);
  }
}
