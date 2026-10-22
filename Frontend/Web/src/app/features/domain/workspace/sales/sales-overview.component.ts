import { Component, OnInit, OnDestroy, Input, inject, signal, computed, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { Subscription, forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { ButtonModule } from 'primeng/button';
import { AppModuleOverviewComponent } from '../../../../shared/components/module-overview/module-overview.component';
import { SalesOrderService } from '../../../../core/services/sales-order.service';
import { MasterDataService } from '../../../../core/services/master-data.service';
import { WorkspaceScopeService } from '../../../../core/services/workspace-scope.service';
import { AuthService } from '../../../../core/auth/auth.service';
import {
  SalesOrderDto,
  SalesOrderStatus,
  getSalesOrderStatusName,
  getSalesOrderStatusSeverity
} from '../../../../core/models/sales-order.models';
import { CustomerItem } from '../../../../core/models/scope.models';
import {
  ModuleOverviewConfig,
  DashboardCardConfig,
  ProcessStageConfig
} from '../../../../shared/models/module-overview-config.model';
import { DataTableColumn, DataTableAction } from '../../../../shared/models/data-table-config.model';

@Component({
  selector: 'app-sales-overview',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ButtonModule,
    AppModuleOverviewComponent
  ],
  templateUrl: './sales-overview.component.html',
  styleUrl: './sales-overview.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class SalesOverviewComponent implements OnInit, OnDestroy {
  readonly salesOrderService = inject(SalesOrderService);
  readonly masterDataService = inject(MasterDataService);
  readonly scopeService = inject(WorkspaceScopeService);
  readonly authService = inject(AuthService);
  readonly router = inject(Router, { optional: true })!;

  /** Configurable limit of recent sales orders displayed on the overview dashboard. Defaults to 10. */
  private readonly _recentLimit = signal<number>(10);
  @Input() set recentLimit(val: number) {
    this._recentLimit.set(val || 10);
  }
  get recentLimit(): number {
    return this._recentLimit();
  }

  // Loading & State
  readonly loading = signal<boolean>(false);
  readonly error = signal<string | null>(null);
  readonly isAccessDenied = signal<boolean>(false);
  readonly accessDeniedMessage = signal<string>(
    "You don't have permission to access sales records for this operating company and branch. Contact your administrator if you believe this is an error."
  );

  // Raw orders retrieved from the real API and enriched with customer master data
  readonly orders = signal<SalesOrderDto[]>([]);
  readonly customers = signal<CustomerItem[]>([]);

  // Search & Filter State in Recent Sales Orders
  readonly recentSearchTerm = signal<string>('');
  readonly recentStatusFilter = signal<string>('all');

  private readonly subs = new Subscription();
  private activeRequestId = 0;

  // Status options for the filter dropdown
  readonly statusOptions = [
    { label: 'All statuses', value: 'all' },
    { label: 'Draft', value: 'draft' },
    { label: 'Pending Confirmation', value: 'submitted' },
    { label: 'Confirmed', value: 'confirmed' },
    { label: 'Fulfilled', value: 'fulfilled' },
    { label: 'Cancelled', value: 'cancelled' }
  ];

  // Breadcrumbs
  readonly breadcrumbs = [
    { label: 'Workspace', routerLink: '/app/dashboard' },
    { label: 'Sales Overview' }
  ];

  /** Checks if the Sales Order transaction register/detail route is registered in the Angular Router */
  readonly hasOrdersRoute = computed<boolean>(() => {
    if (!this.router?.config) return false;
    const checkRoutes = (routes: any[], prefix = ''): boolean => {
      for (const r of routes) {
        const full = prefix ? `${prefix}/${r.path || ''}` : (r.path || '');
        if (full === 'app/sales/orders' || full === 'sales/orders' || full.includes('sales/orders')) {
          return true;
        }
        if (r.children && checkRoutes(r.children, full)) {
          return true;
        }
      }
      return false;
    };
    return checkRoutes(this.router.config);
  });

  // Computed summary counts based on real API data
  readonly draftCount = computed<number>(() => {
    return this.orders().filter(o =>
      Number(o.status) === SalesOrderStatus.Draft ||
      String(o.status).toLowerCase() === 'draft'
    ).length;
  });

  readonly pendingCount = computed<number>(() => {
    return this.orders().filter(o =>
      Number(o.status) === SalesOrderStatus.Submitted ||
      String(o.status).toLowerCase() === 'submitted' ||
      String(o.status).toLowerCase() === 'pending confirmation'
    ).length;
  });

  readonly confirmedCount = computed<number>(() => {
    return this.orders().filter(o =>
      Number(o.status) === SalesOrderStatus.Confirmed ||
      String(o.status).toLowerCase() === 'confirmed'
    ).length;
  });

  // Computed total order value from real API data
  readonly totalOrderValue = computed<number>(() => {
    return this.orders().reduce((sum, o) => sum + (Number(o.totalAmount) || 0), 0);
  });

  // Active currency code derived from selected company
  readonly activeCurrency = computed<string>(() => {
    return this.scopeService.selectedCompany()?.currency || 'LKR';
  });

  // Formatted order value string (e.g., "LKR 2.4M" or formatted currency)
  readonly formattedTotalOrderValue = computed<string>(() => {
    const val = this.totalOrderValue();
    const curr = this.activeCurrency();
    if (val >= 1_000_000) {
      const millions = (val / 1_000_000).toFixed(1).replace(/\.0$/, '');
      return `${curr} ${millions}M`;
    }
    if (val >= 1_000) {
      const thousands = (val / 1_000).toFixed(1).replace(/\.0$/, '');
      return `${curr} ${thousands}K`;
    }
    return `${curr} ${val.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
  });

  // Recent orders list: sorted by latest date/orderNumber, filtered, and limited to recentLimit
  readonly recentOrders = computed<SalesOrderDto[]>(() => {
    let list = [...this.orders()];

    // Filter by recent status filter
    const status = this.recentStatusFilter();
    if (status !== 'all') {
      list = list.filter(o => {
        const name = getSalesOrderStatusName(o.status).toLowerCase();
        if (status === 'draft') return name === 'draft';
        if (status === 'submitted') return name === 'pending confirmation' || name === 'submitted';
        if (status === 'confirmed') return name === 'confirmed';
        if (status === 'fulfilled') return name === 'fulfilled';
        if (status === 'cancelled') return name === 'cancelled' || name === 'rejected';
        return name === status;
      });
    }

    // Filter by search term
    const term = this.recentSearchTerm().trim().toLowerCase();
    if (term) {
      list = list.filter(o => {
        const num = (o.orderNumber || '').toLowerCase();
        const cust = (o.customerName || '').toLowerCase();
        return num.includes(term) || cust.includes(term);
      });
    }

    // Return only the configured recent limit (default 10)
    return list.slice(0, Math.max(1, this._recentLimit()));
  });

  // Shared configuration contract for Module Overview
  readonly overviewConfig = computed<ModuleOverviewConfig<SalesOrderDto>>(() => {
    const hasRoute = this.hasOrdersRoute();

    return {
      moduleName: 'Sales Overview',
      moduleDescription: 'Operational sales order overview, confirmation status, and order dispatch activity',
      breadcrumbs: this.breadcrumbs,
      dashboardCards: [
        {
          id: 'draft',
          title: 'Draft Orders',
          icon: 'pi pi-file-edit',
          value: this.draftCount(),
          colorVariant: 'blue',
          description: 'In draft',
          route: hasRoute ? '/app/sales/orders' : undefined,
          queryParams: hasRoute ? { status: 'draft' } : undefined,
          requiredPermissions: ['SALES_ORDER.VIEW']
        },
        {
          id: 'pending',
          title: 'Pending Confirmation',
          icon: 'pi pi-clock',
          value: this.pendingCount(),
          colorVariant: 'amber',
          description: 'Awaiting confirmation',
          route: hasRoute ? '/app/sales/orders' : undefined,
          queryParams: hasRoute ? { status: 'submitted' } : undefined,
          requiredPermissions: ['SALES_ORDER.VIEW']
        },
        {
          id: 'confirmed',
          title: 'Confirmed Orders',
          icon: 'pi pi-check-circle',
          value: this.confirmedCount(),
          colorVariant: 'green',
          description: 'Ready for fulfillment',
          route: hasRoute ? '/app/sales/orders' : undefined,
          queryParams: hasRoute ? { status: 'confirmed' } : undefined,
          requiredPermissions: ['SALES_ORDER.VIEW']
        },
        {
          id: 'orderValue',
          title: 'Sales Order Value',
          icon: 'pi pi-wallet',
          value: this.formattedTotalOrderValue(),
          colorVariant: 'slate',
          description: 'Total order value',
          route: hasRoute ? '/app/sales/orders' : undefined,
          requiredPermissions: ['SALES_ORDER.VIEW']
        }
      ],
      processFlow: {
        enabled: true,
        position: 'belowCards',
        title: 'Sales order process',
        subtitle: 'From sales draft and confirmation to stock issuance and planned delivery',
        layout: 'horizontal',
        stages: [
          {
            id: 'so',
            code: 'SO',
            label: 'Sales Order',
            description: 'Create and save draft customer sales orders',
            icon: 'cart',
            status: 'current',
            statusLabel: 'Current',
            badge: this.orders().length,
            route: hasRoute ? '/app/sales/orders' : undefined,
            requiredPermissions: ['SALES_ORDER.VIEW']
          },
          {
            id: 'confirmation',
            code: 'Confirm',
            label: 'Order Confirmation',
            description: 'Submit and confirm customer sales orders',
            icon: 'check',
            status: 'upcoming',
            statusLabel: 'Upcoming',
            route: hasRoute ? '/app/sales/orders' : undefined,
            queryParams: hasRoute ? { status: 'submitted' } : undefined,
            requiredPermissions: ['SALES_ORDER.CONFIRM']
          },
          {
            id: 'dispatch',
            code: 'Dispatch',
            label: 'Dispatch Note',
            description: 'Generate and save draft goods dispatch notes for warehouse fulfillment',
            icon: 'send',
            status: 'upcoming',
            statusLabel: 'Step 3',
            route: '/app/sales/dispatches',
            requiredPermissions: ['SALES_SHIPMENT.VIEW']
          },
          {
            id: 'deliver',
            code: 'Deliver',
            label: 'Deliver & Issue Stock',
            description: 'Deliver goods, issue stock from warehouse, and auto-generate invoice draft',
            icon: 'boxes',
            status: 'upcoming',
            statusLabel: 'Step 4',
            route: '/app/sales/dispatches',
            requiredPermissions: ['SALES_SHIPMENT.DELIVER']
          },
          {
            id: 'invoice',
            code: 'Invoice',
            label: 'Sales Invoice',
            description: 'Review, amend line amounts, save draft, and explicitly confirm commercial invoice',
            icon: 'file-text',
            status: 'upcoming',
            statusLabel: 'Step 5',
            route: '/app/sales/invoices',
            requiredPermissions: ['SALES_INVOICE.VIEW']
          }
        ]
      },
      recentTransactions: {
        title: 'Recent Sales Orders',
        subtitle: `Latest activity · default ${this.recentLimit} records`,
        limit: this.recentLimit,
        showSearch: true,
        showFilters: true,
        showViewAll: hasRoute,
        viewAllRoute: hasRoute ? '/app/sales/orders' : undefined,
        viewAllLabel: 'View all Sales Orders',
        createActionLabel: hasRoute && this.canCreateOrder() ? 'Create Sales Order' : undefined,
        createActionIcon: 'pi pi-plus',
        createActionRoute: hasRoute ? '/app/sales/orders/new' : undefined,
        createActionPermission: 'SALES_ORDER.CREATE'
      },
      navigationLinks: [
        {
          label: 'Sales Orders',
          icon: 'pi pi-shopping-bag',
          route: '/app/sales/orders',
          outlined: true,
          severity: 'secondary'
        },
        {
          label: 'Dispatch Notes',
          icon: 'pi pi-send',
          route: '/app/sales/dispatches',
          outlined: true,
          severity: 'secondary'
        },
        {
          label: 'Sales Invoices',
          icon: 'pi pi-file-invoice',
          route: '/app/sales/invoices',
          outlined: true,
          severity: 'secondary'
        }
      ]
    };
  });

  // Columns for the recent records table
  readonly recentColumns: DataTableColumn<SalesOrderDto>[] = [
    {
      field: 'orderNumber',
      header: 'ORDER NUMBER / CUSTOMER',
      sortable: true,
      format: (_, row) => `${row.orderNumber} - ${row.customerName || 'Standard Customer'}`
    },
    {
      field: 'orderDate',
      header: 'ORDER DATE',
      sortable: true,
      format: val => (val ? new Date(val).toLocaleDateString() : '—')
    },
    {
      field: 'totalAmount',
      header: 'AMOUNT',
      sortable: true,
      align: 'right',
      format: (val, row) => {
        const curr = this.activeCurrency();
        const num = Number(val) || 0;
        return `${curr} ${num.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
      }
    },
    {
      field: 'status',
      header: 'STATUS',
      type: 'badge',
      sortable: true,
      align: 'center',
      format: val => getSalesOrderStatusName(val),
      badgeSeverityMap: val => getSalesOrderStatusSeverity(val)
    }
  ];

  // Actions for the recent records table
  readonly recentActions = computed<DataTableAction<SalesOrderDto>[]>(() => {
    if (!this.hasOrdersRoute()) {
      return [];
    }
    return [
      {
        id: 'view',
        label: 'View',
        icon: 'pi pi-eye',
        outlined: true,
        severity: 'secondary',
        action: (row: SalesOrderDto) => this.navigateToOrder(row)
      }
    ];
  });

  ngOnInit(): void {
    if (this.scopeService.canOperateTransactions()) {
      this.loadOverviewData();
    }

    this.subs.add(
      this.scopeService.scopeChange$.subscribe(() => {
        if (this.scopeService.canOperateTransactions()) {
          this.loadOverviewData();
        } else {
          this.orders.set([]);
          this.customers.set([]);
        }
      })
    );
  }

  ngOnDestroy(): void {
    this.subs.unsubscribe();
  }

  loadOverviewData(): void {
    const company = this.scopeService.selectedCompany();
    const branch = this.scopeService.selectedBranch();
    if (!company || !branch) {
      this.orders.set([]);
      this.customers.set([]);
      return;
    }

    const requestId = ++this.activeRequestId;
    this.loading.set(true);
    this.error.set(null);
    this.isAccessDenied.set(false);

    forkJoin({
      orders: this.salesOrderService.getOrders(company.id, branch.id),
      customers: this.masterDataService.getCustomers().pipe(
        catchError(() => of([] as CustomerItem[]))
      )
    }).subscribe({
      next: ({ orders, customers }) => {
        if (requestId !== this.activeRequestId) return; // Stale response protection
        this.loading.set(false);
        this.isAccessDenied.set(false);
        this.customers.set(customers || []);

        const enrichedOrders = (orders || []).map(order => {
          const matched = (customers || []).find(
            c => (c.customerId || (c as any).id) === order.customerId
          );
          return {
            ...order,
            customerName:
              order.customerName ||
              matched?.customerName ||
              (order.customerId ? `Customer (${order.customerId.substring(0, 8)})` : 'Standard Customer')
          };
        });

        this.orders.set(enrichedOrders);
      },
      error: err => {
        if (requestId !== this.activeRequestId) return; // Stale response protection
        this.loading.set(false);
        if (this.isForbidden(err)) {
          this.isAccessDenied.set(true);
          this.error.set(null);
        } else {
          this.isAccessDenied.set(false);
          this.error.set(this.getCleanErrorMessage(err, 'Failed to load sales overview data.'));
        }
      }
    });
  }

  private isForbidden(err: any): boolean {
    if (!err) return false;
    if (err.status === 403 || err.statusText === 'Forbidden') return true;
    if (err?.error?.status === 403) return true;
    const msg = String(err?.message || err?.error?.message || '');
    return msg.includes('403') || msg.toLowerCase().includes('forbidden');
  }

  private getCleanErrorMessage(err: any, fallback: string): string {
    if (this.isForbidden(err)) {
      return "You don't have authorization to access sales data. Contact your administrator if you believe this is an error.";
    }
    const raw = err?.error?.message;
    if (raw && !raw.includes('http://') && !raw.includes('localhost')) {
      return raw;
    }
    return fallback;
  }

  canCreateOrder(): boolean {
    if (this.authService.isTenantAdmin() || this.authService.isPlatformAdmin()) {
      return true;
    }
    return this.authService.hasPermission('SALES_ORDER.CREATE');
  }

  // Navigation handlers
  navigateToStatus(status: 'draft' | 'submitted' | 'confirmed' | 'all'): void {
    if (this.hasOrdersRoute()) {
      this.router.navigate(['/app/sales/orders'], {
        queryParams: { status }
      });
    }
  }

  navigateToAllOrders(): void {
    if (this.hasOrdersRoute()) {
      this.router.navigate(['/app/sales/orders']);
    }
  }

  navigateToCreate(): void {
    if (this.hasOrdersRoute()) {
      this.router.navigate(['/app/sales/orders/new']);
    }
  }

  navigateToOrder(order: SalesOrderDto): void {
    if (this.hasOrdersRoute()) {
      this.router.navigate(['/app/sales/orders', order.id]);
    }
  }

  onCardClick(card: DashboardCardConfig): void {
    if (card.route && this.hasOrdersRoute()) {
      this.router.navigate([card.route], { queryParams: card.queryParams });
    }
  }

  onStageClick(stage: ProcessStageConfig): void {
    if (stage.route) {
      if (stage.route.includes('sales/orders') && !this.hasOrdersRoute()) {
        return;
      }
      this.router.navigate([stage.route], { queryParams: stage.queryParams });
    }
  }
}