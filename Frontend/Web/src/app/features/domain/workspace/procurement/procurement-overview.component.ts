import { Component, OnInit, OnDestroy, Input, inject, signal, computed, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { Subscription } from 'rxjs';
import { ButtonModule } from 'primeng/button';
import { AppModuleOverviewComponent } from '../../../../shared/components/module-overview/module-overview.component';
import { PurchaseOrderService } from '../../../../core/services/purchase-order.service';
import { WorkspaceScopeService } from '../../../../core/services/workspace-scope.service';
import { AuthService } from '../../../../core/auth/auth.service';
import {
  PurchaseOrderDto,
  PurchaseOrderStatus,
  getPurchaseOrderStatusName,
  getPurchaseOrderStatusSeverity
} from '../../../../core/models/purchase-order.models';
import {
  ModuleOverviewConfig,
  DashboardCardConfig,
  ProcessStageConfig
} from '../../../../shared/models/module-overview-config.model';
import { DataTableColumn, DataTableAction } from '../../../../shared/models/data-table-config.model';

@Component({
  selector: 'app-procurement-overview',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ButtonModule,
    AppModuleOverviewComponent
  ],
  templateUrl: './procurement-overview.component.html',
  styleUrl: './procurement-overview.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ProcurementOverviewComponent implements OnInit, OnDestroy {
  readonly poService = inject(PurchaseOrderService);
  readonly scopeService = inject(WorkspaceScopeService);
  readonly authService = inject(AuthService);
  readonly router = inject(Router);

  /** Configurable limit of recent purchase orders displayed on the overview dashboard. Defaults to 10. */
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
    "You don't have permission to access procurement records for this operating company and branch. Contact your administrator if you believe this is an error."
  );

  // Raw orders retrieved from the real API
  readonly orders = signal<PurchaseOrderDto[]>([]);

  // Search & Filter State in Recent POs
  readonly recentSearchTerm = signal<string>('');
  readonly recentStatusFilter = signal<string>('all');

  private readonly subs = new Subscription();
  private activeRequestId = 0;

  // Status options for the filter dropdown
  readonly statusOptions = [
    { label: 'All statuses', value: 'all' },
    { label: 'Draft', value: 'draft' },
    { label: 'Pending Approval', value: 'submitted' },
    { label: 'Approved', value: 'approved' },
    { label: 'Cancelled', value: 'cancelled' }
  ];

  // Breadcrumbs
  readonly breadcrumbs = [
    { label: 'Workspace', routerLink: '/app/dashboard' },
    { label: 'Procurement Overview' }
  ];

  // Computed summary counts based on real API data
  readonly draftCount = computed<number>(() => {
    return this.orders().filter(o =>
      Number(o.status) === PurchaseOrderStatus.Draft ||
      String(o.status).toLowerCase() === 'draft'
    ).length;
  });

  readonly pendingCount = computed<number>(() => {
    return this.orders().filter(o =>
      Number(o.status) === PurchaseOrderStatus.Submitted ||
      String(o.status).toLowerCase() === 'submitted'
    ).length;
  });

  readonly approvedCount = computed<number>(() => {
    return this.orders().filter(o =>
      Number(o.status) === PurchaseOrderStatus.Approved ||
      String(o.status).toLowerCase() === 'approved'
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
  readonly recentOrders = computed<PurchaseOrderDto[]>(() => {
    let list = [...this.orders()];

    // Filter by recent status filter
    const status = this.recentStatusFilter();
    if (status !== 'all') {
      list = list.filter(o => {
        const name = getPurchaseOrderStatusName(o.status).toLowerCase();
        return name === status;
      });
    }

    // Filter by search term
    const term = this.recentSearchTerm().trim().toLowerCase();
    if (term) {
      list = list.filter(o => {
        const num = (o.orderNumber || '').toLowerCase();
        const sup = (o.supplierName || '').toLowerCase();
        return num.includes(term) || sup.includes(term);
      });
    }

    // Return only the configured recent limit (default 10)
    return list.slice(0, Math.max(1, this._recentLimit()));
  });

  // Shared configuration contract for Module Overview
  readonly overviewConfig = computed<ModuleOverviewConfig<PurchaseOrderDto>>(() => ({
    moduleName: 'Procurement Overview',
    moduleDescription: 'Operational procurement overview and recent purchase activity',
    breadcrumbs: this.breadcrumbs,
    dashboardCards: [
      {
        id: 'draft',
        title: 'Draft POs',
        icon: 'pi pi-file-edit',
        value: this.draftCount(),
        colorVariant: 'blue',
        description: 'In progress',
        route: '/app/procurement/purchase-orders',
        queryParams: { status: 'draft' }
      },
      {
        id: 'pending',
        title: 'Pending Approval',
        icon: 'pi pi-clock',
        value: this.pendingCount(),
        colorVariant: 'amber',
        description: 'Awaiting action',
        route: '/app/procurement/purchase-orders',
        queryParams: { status: 'submitted' },
        requiredPermissions: ['PURCHASE_ORDER.VIEW']
      },
      {
        id: 'approved',
        title: 'Approved POs',
        icon: 'pi pi-check-circle',
        value: this.approvedCount(),
        colorVariant: 'green',
        description: 'Ready for next step',
        route: '/app/procurement/purchase-orders',
        queryParams: { status: 'approved' },
        requiredPermissions: ['PURCHASE_ORDER.VIEW']
      },
      {
        id: 'orderValue',
        title: 'Order Value',
        icon: 'pi pi-wallet',
        value: this.formattedTotalOrderValue(),
        colorVariant: 'slate',
        description: 'Total order value',
        route: '/app/procurement/purchase-orders',
        requiredPermissions: ['PURCHASE_ORDER.VIEW']
      }
    ],
    processFlow: {
      enabled: true,
      position: 'belowCards',
      title: 'Procurement process',
      subtitle: 'From demand requisition to inventory receipt and posting',
      layout: 'horizontal',
      stages: [
        {
          id: 'pr',
          code: 'PR',
          label: 'Requisition',
          description: 'Purchase requisitions and demand planning',
          icon: 'file-text',
          stageType: 'optional',
          isOptional: true,
          status: 'upcoming',
          statusLabel: 'Optional',
          pathGroup: 'entry',
          route: '/app/procurement/purchase-orders',
          queryParams: { tab: 'requisitions' },
          requiredPermissions: ['PURCHASE_REQUISITION.VIEW']
        },
        {
          id: 'direct-po',
          code: 'Direct PO',
          label: 'Direct PO',
          description: 'Create purchase order directly without requisition',
          icon: 'cart',
          isAlternative: true,
          pathGroup: 'entry',
          status: 'active',
          statusLabel: 'Alternative',
          route: '/app/procurement/purchase-orders',
          queryParams: { action: 'create' },
          requiredPermissions: ['PURCHASE_ORDER.CREATE']
        },
        {
          id: 'po',
          code: 'PO',
          label: 'Purchase Order',
          description: 'Manage purchase order registers and approvals',
          icon: 'cart',
          status: 'current',
          statusLabel: 'Current',
          badge: this.orders().length,
          route: '/app/procurement/purchase-orders',
          requiredPermissions: ['PURCHASE_ORDER.VIEW'],
          convergesTo: 'po'
        },
        {
          id: 'grn',
          code: 'GRN',
          label: 'Goods Receipt',
          description: 'Inbound shipment receipt and vendor delivery verification planned',
          icon: 'package',
          status: 'planned',
          planned: true,
          statusLabel: 'Upcoming'
        },
        {
          id: 'stock',
          code: 'Stock',
          label: 'Stock Posting',
          description: 'Automated warehouse inventory adjustment upon verified delivery',
          icon: 'boxes',
          stageType: 'conditional',
          isConditional: true,
          conditionalExplanation: 'Applies when purchased line items affect warehouse inventory',
          status: 'upcoming',
          statusLabel: 'When applicable',
          route: '/app/inventory',
          requiredPermissions: ['INVENTORY.VIEW']
        }
      ]
    },
    recentTransactions: {
      title: 'Recent Purchase Orders',
      subtitle: `Latest activity · default ${this.recentLimit} records`,
      limit: this.recentLimit,
      showSearch: true,
      showFilters: true,
      showViewAll: true,
      viewAllRoute: '/app/procurement/purchase-orders',
      viewAllLabel: 'View all Purchase Orders',
      createActionLabel: 'Create PO',
      createActionIcon: 'pi pi-plus',
      createActionRoute: '/app/procurement/purchase-orders',
      createActionQueryParams: { action: 'create' },
      createActionPermission: 'PURCHASE_ORDER.CREATE'
    },
    navigationLinks: [
      {
        label: 'Manage Purchase Orders',
        icon: 'pi pi-list',
        route: '/app/procurement/purchase-orders',
        outlined: true,
        severity: 'secondary'
      }
    ]
  }));

  // Columns for the recent records table
  readonly recentColumns: DataTableColumn<PurchaseOrderDto>[] = [
    {
      field: 'orderNumber',
      header: 'PO NUMBER / SUPPLIER',
      sortable: true,
      format: (_, row) => `${row.orderNumber} - ${row.supplierName || 'Standard Vendor'}`
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
      format: val => getPurchaseOrderStatusName(val),
      badgeSeverityMap: val => getPurchaseOrderStatusSeverity(val)
    }
  ];

  // Action to navigate to details
  readonly recentActions: DataTableAction<PurchaseOrderDto>[] = [
    {
      id: 'view',
      label: 'View',
      icon: 'pi pi-eye',
      outlined: true,
      severity: 'secondary',
      action: (row: PurchaseOrderDto) => this.navigateToOrder(row)
    }
  ];

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
      return;
    }

    const requestId = ++this.activeRequestId;
    this.loading.set(true);
    this.error.set(null);
    this.isAccessDenied.set(false);

    // Retrieve orders using real API
    this.poService.getOrders(company.id, branch.id).subscribe({
      next: orders => {
        if (requestId !== this.activeRequestId) return; // Stale response protection
        this.loading.set(false);
        this.isAccessDenied.set(false);
        this.orders.set(orders);
      },
      error: err => {
        if (requestId !== this.activeRequestId) return; // Stale response protection
        this.loading.set(false);
        if (this.isForbidden(err)) {
          this.isAccessDenied.set(true);
          this.error.set(null);
        } else {
          this.isAccessDenied.set(false);
          this.error.set(this.getCleanErrorMessage(err, 'Failed to load procurement overview data.'));
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
      return "You don't have authorization to access procurement data. Contact your administrator if you believe this is an error.";
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
    return this.authService.hasPermission('PURCHASE_ORDER.CREATE');
  }

  // Navigation handlers
  navigateToStatus(status: 'draft' | 'submitted' | 'approved' | 'all'): void {
    this.router.navigate(['/app/procurement/purchase-orders'], {
      queryParams: { status }
    });
  }

  navigateToAllOrders(): void {
    this.router.navigate(['/app/procurement/purchase-orders']);
  }

  navigateToCreate(): void {
    this.router.navigate(['/app/procurement/purchase-orders'], {
      queryParams: { action: 'create' }
    });
  }

  navigateToOrder(order: PurchaseOrderDto): void {
    this.router.navigate(['/app/procurement/purchase-orders'], {
      queryParams: { orderId: order.id }
    });
  }

  onCardClick(card: DashboardCardConfig): void {
    if (card.route) {
      this.router.navigate([card.route], { queryParams: card.queryParams });
    }
  }

  onStageClick(stage: ProcessStageConfig): void {
    if (stage.route) {
      this.router.navigate([stage.route], { queryParams: stage.queryParams });
    }
  }
}
