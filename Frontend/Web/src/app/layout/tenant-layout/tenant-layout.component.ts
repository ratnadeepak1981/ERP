import { Component, signal, inject, OnInit, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterOutlet, RouterLink, RouterLinkActive, Router, NavigationEnd } from '@angular/router';
import { toSignal } from '@angular/core/rxjs-interop';
import { filter, map } from 'rxjs';
import { ButtonModule } from 'primeng/button';
import { TooltipModule } from 'primeng/tooltip';
import { ThemeService } from '../../core/services/theme.service';
import { AuthService } from '../../core/auth/auth.service';
import { WorkspaceScopeService } from '../../core/services/workspace-scope.service';
import { TenantScopeSwitcherComponent } from '../../shared/components/scope-switcher/tenant-scope-switcher/tenant-scope-switcher.component';

export interface NavItem {
  label: string;
  icon: string;
  route: string;
  badge?: string;
}

export interface NavGroup {
  groupName: string;
  items: NavItem[];
}

@Component({
  selector: 'app-tenant-layout',
  standalone: true,
  imports: [
    CommonModule,
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    ButtonModule,
    TooltipModule,
    TenantScopeSwitcherComponent
  ],
  templateUrl: './tenant-layout.component.html',
  styleUrl: './tenant-layout.component.scss'
})
export class TenantLayoutComponent implements OnInit {
  readonly themeService = inject(ThemeService);
  readonly authService = inject(AuthService);
  readonly scopeService = inject(WorkspaceScopeService);
  private readonly router = inject(Router);

  readonly sidebarCollapsed = signal<boolean>(false);
  readonly currentTenantName = signal<string>('Alpha Manufacturing Ltd');
  readonly currentCompanyBranch = signal<string>('Main Plant (Colombo)');

  // Track the current URL reactively
  private readonly currentUrlSignal = toSignal(
    this.router.events.pipe(
      filter((e): e is NavigationEnd => e instanceof NavigationEnd),
      map(e => e.urlAfterRedirects || e.url)
    ),
    { initialValue: this.router.url }
  );

  // Determine if currently in Tenant Admin Workspace (/tenant-admin)
  readonly isTenantAdminWorkspace = computed(() => {
    const url = this.currentUrlSignal() || this.router.url;
    return url.startsWith('/tenant-admin');
  });

  // Dynamic brand subtitle based on workspace
  readonly brandTag = computed(() => {
    return this.isTenantAdminWorkspace() ? 'Tenant Admin' : 'Workspace';
  });

  // Distinct navigation groups for Tenant Admin vs Tenant Staff
  readonly navGroups = computed<NavGroup[]>(() => {
    if (this.isTenantAdminWorkspace()) {
      return [
        {
          groupName: 'Administration',
          items: [
            { label: 'Admin Console', icon: 'pi pi-th-large', route: '/tenant-admin/console' }
          ]
        },
        {
          groupName: 'Organization',
          items: [
            { label: 'Company Setup', icon: 'pi pi-cog', route: '/tenant-admin/company-setup' },
            { label: 'Companies', icon: 'pi pi-building', route: '/tenant-admin/companies' },
            { label: 'Branches', icon: 'pi pi-sitemap', route: '/tenant-admin/branches' }
          ]
        },
        {
          groupName: 'Access & Security',
          items: [
            { label: 'Users', icon: 'pi pi-users', route: '/tenant-admin/users' },
            { label: 'Roles & Permissions', icon: 'pi pi-key', route: '/tenant-admin/roles' },
            { label: 'User Operating Scope', icon: 'pi pi-shield', route: '/tenant-admin/user-scope' }
          ]
        },
        {
          groupName: 'Operational View',
          items: [
            { label: 'Tenant Operations ERP', icon: 'pi pi-desktop', route: '/app/dashboard', badge: 'ERP' }
          ]
        }
      ];
    }

    // Tenant Staff / Operational User Navigation
    const staffNav: NavGroup[] = [
      {
        groupName: 'Overview',
        items: [
          { label: 'Dashboard', icon: 'pi pi-home', route: '/app/dashboard' }
        ]
      },
      {
        groupName: 'Supply Chain & Operations',
        items: this.getSupplyChainNavItems()
      },
      {
        groupName: 'System',
        items: [
          { label: 'Component Showcase', icon: 'pi pi-sliders-h', route: '/app/demonstration' }
        ]
      }
    ];

    // If user is also an authorized Tenant Administrator, provide link to Admin Console
    if (this.authService.isTenantAdmin() || this.authService.isPlatformAdmin()) {
      staffNav.push({
        groupName: 'Management',
        items: [
          { label: 'Tenant Admin Console', icon: 'pi pi-shield', route: '/tenant-admin/console', badge: 'Admin' }
        ]
      });
    }

    return staffNav;
  });

  private getSupplyChainNavItems(): NavItem[] {
    const items: NavItem[] = [
      { label: 'Master Data', icon: 'pi pi-database', route: '/app/master-data' }
    ];

    if (this.canViewProcurement()) {
      items.push({ label: 'Procurement Overview', icon: 'pi pi-chart-line', route: '/app/procurement' });
      items.push({ label: 'Purchase Orders', icon: 'pi pi-shopping-cart', route: '/app/procurement/purchase-orders' });
    }

    items.push({ label: 'Inventory', icon: 'pi pi-box', route: '/app/inventory' });
    return items;
  }

  canViewProcurement(): boolean {
    if (this.authService.isTenantAdmin() || this.authService.isPlatformAdmin() || this.authService.isPlatformStaff()) {
      return true;
    }
    const perms = this.authService.permissions();
    if (perms && perms.length > 0 && !perms.includes('PURCHASE_ORDER.VIEW') && !perms.includes('Product.Read')) {
      return false;
    }
    return true;
  }

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

  toggleSidebar(): void {
    this.sidebarCollapsed.update(collapsed => !collapsed);
  }
}
