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
  children?: NavItem[];
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

  // Track expanded menu labels
  readonly expandedItems = signal<Record<string, boolean>>({
    Procurement: true,
    Inventory: true,
    Sales: true,
    'Master Data': true
  });

  constructor() {
    this.router.events
      .pipe(filter((e): e is NavigationEnd => e instanceof NavigationEnd))
      .subscribe(e => {
        const url = e.urlAfterRedirects || e.url;
        this.autoExpandActiveParents(url);
      });
  }

  private autoExpandActiveParents(url: string): void {
    if (!url) return;
    const checkItem = (item: NavItem) => {
      if (item.children && item.children.length > 0) {
        const matchesChild = item.children.some(c => url.startsWith(c.route));
        const matchesParent = item.route && url.startsWith(item.route);
        if (matchesChild || matchesParent) {
          this.expandedItems.update(state => {
            if (state[item.label] === true) return state;
            return { ...state, [item.label]: true };
          });
        }
      }
    };

    for (const group of this.navGroups()) {
      for (const item of group.items) {
        checkItem(item);
      }
    }
  }

  isItemExpanded(item: NavItem): boolean {
    if (this.expandedItems()[item.label] !== undefined) {
      return this.expandedItems()[item.label];
    }
    if (item.children && item.route) {
      const url = this.currentUrlSignal() || this.router.url;
      return url.startsWith(item.route);
    }
    return false;
  }

  toggleItemExpanded(item: NavItem, event?: Event): void {
    if (event) {
      event.preventDefault();
      event.stopPropagation();
    }
    const current = this.isItemExpanded(item);
    this.expandedItems.update(state => ({
      ...state,
      [item.label]: !current
    }));
  }

  isParentActive(item: NavItem): boolean {
    const url = this.currentUrlSignal() || this.router.url;
    if (!url) return false;
    if (item.route && url.startsWith(item.route)) return true;
    if (item.children && item.children.length > 0) {
      return item.children.some(c => url.startsWith(c.route));
    }
    return false;
  }

  private getSupplyChainNavItems(): NavItem[] {
    const items: NavItem[] = [
      {
        label: 'Master Data',
        icon: 'pi pi-database',
        route: '/app/master-data',
        children: [
          { label: 'Overview', icon: 'pi pi-chart-line', route: '/app/master-data' },
          { label: 'Products', icon: 'pi pi-box', route: '/app/master-data/products' },
          { label: 'Customers', icon: 'pi pi-users', route: '/app/master-data/customers' },
          { label: 'Suppliers', icon: 'pi pi-truck', route: '/app/master-data/suppliers' },
          { label: 'Warehouses & Locations', icon: 'pi pi-building', route: '/app/master-data/warehouses' },
          { label: 'Price Cards', icon: 'pi pi-tags', route: '/app/master-data/price-cards' },
          { label: 'Currencies', icon: 'pi pi-dollar', route: '/app/master-data/currencies' }
        ]
      }
    ];

    const procurementChildren: NavItem[] = [];
    if (this.canViewProcurementOverview()) {
      procurementChildren.push({ label: 'Overview', icon: 'pi pi-chart-line', route: '/app/procurement' });
    }
    if (this.canViewPurchaseRequisitions()) {
      procurementChildren.push({ label: 'Purchase Requisitions', icon: 'pi pi-file-edit', route: '/app/procurement/purchase-requisitions' });
    }
    if (this.canViewPurchaseOrders()) {
      procurementChildren.push({ label: 'Purchase Orders', icon: 'pi pi-shopping-cart', route: '/app/procurement/purchase-orders' });
    }
    if (this.canViewGoodsReceipts()) {
      procurementChildren.push({ label: 'Product Receipts (GRN)', icon: 'pi pi-arrow-circle-down', route: '/app/procurement/goods-receipts' });
    }

    if (procurementChildren.length > 0) {
      items.push({
        label: 'Procurement',
        icon: 'pi pi-shopping-cart',
        route: '/app/procurement',
        children: procurementChildren
      });
    }

    const inventoryChildren: NavItem[] = [
      { label: 'Overview', icon: 'pi pi-chart-line', route: '/app/inventory' },
      { label: 'Stock Receipt', icon: 'pi pi-arrow-circle-down', route: '/app/inventory/stock-receipts' }
    ];

    if (this.canViewStoragePutaway()) {
      inventoryChildren.push({ label: 'Storage & Putaway', icon: 'pi pi-inbox', route: '/app/inventory/store-putaway' });
    }

    inventoryChildren.push(
      { label: 'Stock Transfers', icon: 'pi pi-arrow-right-arrow-left', route: '/app/inventory/transfers' },
      { label: 'Stock Count & Adjustment', icon: 'pi pi-check-square', route: '/app/inventory/count-adjust' },
      { label: 'Stock Issue', icon: 'pi pi-arrow-circle-up', route: '/app/inventory/issue-stock' },
      { label: 'Stock by Storage Location', icon: 'pi pi-table', route: '/app/inventory/reports/stock-by-storage-location' },
      { label: 'Stock by Staging Location', icon: 'pi pi-list', route: '/app/inventory/reports/stock-by-staging-location' },
      { label: 'Stock Ledger', icon: 'pi pi-book', route: '/app/inventory/reports/stock-ledger' }
    );

    items.push({
      label: 'Inventory',
      icon: 'pi pi-box',
      route: '/app/inventory',
      children: inventoryChildren
    });

    if (this.canViewSales()) {
      items.push({
        label: 'Sales',
        icon: 'pi pi-dollar',
        route: '/app/sales',
        children: [
          { label: 'Overview', icon: 'pi pi-chart-line', route: '/app/sales' },
          { label: 'Sales Orders', icon: 'pi pi-shopping-bag', route: '/app/sales/orders' },
          { label: 'Dispatch Notes', icon: 'pi pi-send', route: '/app/sales/dispatches' },
          { label: 'Sales Invoices', icon: 'pi pi-file-invoice', route: '/app/sales/invoices' }
        ]
      });
    }

    return items;
  }

  canViewPurchaseRequisitions(): boolean {
    if (this.authService.isTenantAdmin() || this.authService.isPlatformAdmin() || this.authService.isPlatformStaff()) {
      return true;
    }
    const perms = this.authService.permissions();
    if (perms && perms.length > 0 && !perms.includes('PURCHASE_REQUISITION.VIEW')) {
      return false;
    }
    return true;
  }

  canViewPurchaseOrders(): boolean {
    if (this.authService.isTenantAdmin() || this.authService.isPlatformAdmin() || this.authService.isPlatformStaff()) {
      return true;
    }
    const perms = this.authService.permissions();
    if (perms && perms.length > 0 && !perms.includes('PURCHASE_ORDER.VIEW')) {
      return false;
    }
    return true;
  }

  canViewGoodsReceipts(): boolean {
    if (this.authService.isTenantAdmin() || this.authService.isPlatformAdmin() || this.authService.isPlatformStaff()) {
      return true;
    }
    const perms = this.authService.permissions();
    if (perms && perms.length > 0 && !perms.includes('GRN.VIEW') && !perms.includes('GOODS_RECEIPT.VIEW')) {
      return false;
    }
    return true;
  }

  canViewProcurementOverview(): boolean {
    if (this.authService.isTenantAdmin() || this.authService.isPlatformAdmin() || this.authService.isPlatformStaff()) {
      return true;
    }
    const perms = this.authService.permissions();
    if (perms && perms.length > 0 && !perms.includes('PROCUREMENT.VIEW') && !perms.includes('PURCHASE_ORDER.VIEW') && !perms.includes('Product.Read')) {
      return false;
    }
    return true;
  }

  canViewProcurement(): boolean {
    return this.canViewProcurementOverview() ||
      this.canViewPurchaseRequisitions() ||
      this.canViewPurchaseOrders() ||
      this.canViewGoodsReceipts();
  }

  canViewStoragePutaway(): boolean {
    if (this.authService.isTenantAdmin() || this.authService.isPlatformAdmin() || this.authService.isPlatformStaff()) {
      return true;
    }
    const perms = this.authService.permissions();
    if (perms && perms.length > 0 && !perms.includes('INVENTORY.VIEW')) {
      return false;
    }
    return true;
  }

  canViewSales(): boolean {
    if (this.authService.isTenantAdmin() || this.authService.isPlatformAdmin() || this.authService.isPlatformStaff()) {
      return true;
    }
    const perms = this.authService.permissions();
    if (perms && perms.length > 0 && !perms.includes('SALES_ORDER.VIEW')) {
      return false;
    }
    return true;
  }

  canViewCustomers(): boolean {
    if (this.authService.isTenantAdmin() || this.authService.isPlatformAdmin() || this.authService.isPlatformStaff()) {
      return true;
    }
    const perms = this.authService.permissions();
    if (perms && perms.length > 0 && !perms.includes('CUSTOMER.VIEW')) {
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
