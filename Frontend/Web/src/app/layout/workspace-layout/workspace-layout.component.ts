import { Component, signal, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterOutlet, RouterLink, RouterLinkActive } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { TooltipModule } from 'primeng/tooltip';
import { ThemeService } from '../../core/services/theme.service';
import { AuthService } from '../../core/auth/auth.service';
import { WorkspaceScopeService } from '../../core/services/workspace-scope.service';
import { WorkspaceScopeSwitcherComponent } from '../../shared/components/scope-switcher/workspace-scope-switcher.component';

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
  imports: [
    CommonModule,
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    ButtonModule,
    TooltipModule,
    WorkspaceScopeSwitcherComponent
  ],
  templateUrl: './workspace-layout.component.html',
  styleUrl: './workspace-layout.component.scss'
})
export class WorkspaceLayoutComponent implements OnInit {
  readonly themeService = inject(ThemeService);
  readonly authService = inject(AuthService);
  readonly scopeService = inject(WorkspaceScopeService);

  readonly sidebarCollapsed = signal<boolean>(false);
  readonly currentTenantName = signal<string>('Alpha Manufacturing Ltd');
  readonly currentCompanyBranch = signal<string>('Main Plant (Colombo)');

  ngOnInit(): void {
    this.scopeService.initializeScope();
    const activeName = this.scopeService.activeTenantName();
    if (activeName) {
      this.currentTenantName.set(activeName);
    }
  }

  onSignOut(): void {
    this.authService.logout('/login');
  }

  readonly navGroups: NavGroup[] = [
    {
      groupName: 'Workspace',
      items: [
        { label: 'Dashboard', icon: 'pi pi-home', route: '/app/dashboard' },
        { label: 'Company Setup', icon: 'pi pi-cog', route: '/app/company-setup' },
        { label: 'Companies', icon: 'pi pi-building', route: '/app/companies' },
        { label: 'Branches', icon: 'pi pi-sitemap', route: '/app/branches' },
        { label: 'Users & Roles', icon: 'pi pi-users', route: '/app/users' },
        { label: 'User Scope', icon: 'pi pi-shield', route: '/app/user-scope' }
      ]
    },
    {
      groupName: 'Supply Chain',
      items: [
        { label: 'Master Data', icon: 'pi pi-database', route: '/app/master-data' },
        { label: 'Procurement', icon: 'pi pi-shopping-cart', route: '/app/procurement' },
        { label: 'Inventory', icon: 'pi pi-box', route: '/app/inventory' }
      ]
    },
    {
      groupName: 'System',
      items: [
        { label: 'Component Showcase', icon: 'pi pi-th-large', route: '/app/demonstration' }
      ]
    }
  ];

  toggleSidebar(): void {
    this.sidebarCollapsed.update(collapsed => !collapsed);
  }
}
