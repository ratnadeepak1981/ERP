import {
  Component,
  OnInit,
  OnDestroy,
  Input,
  inject,
  signal,
  computed,
  ChangeDetectionStrategy
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { Subscription } from 'rxjs';
import { ButtonModule } from 'primeng/button';
import { AppModuleOverviewComponent } from '../../../../shared/components/module-overview/module-overview.component';
import { InventoryService } from '../../../../core/services/inventory.service';
import { WorkspaceScopeService } from '../../../../core/services/workspace-scope.service';
import { AuthService } from '../../../../core/auth/auth.service';
import {
  InventorySummaryDto,
  InventoryRecentTransactionDto,
  getMovementTypeDisplayName,
  getMovementTypeBadgeSeverity
} from '../../../../core/models/inventory.models';
import {
  ModuleOverviewConfig,
  DashboardCardConfig,
  ProcessStageConfig
} from '../../../../shared/models/module-overview-config.model';
import { DataTableColumn, DataTableAction } from '../../../../shared/models/data-table-config.model';

@Component({
  selector: 'app-inventory-overview',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ButtonModule,
    AppModuleOverviewComponent
  ],
  templateUrl: './inventory-overview.component.html',
  styleUrl: './inventory-overview.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class InventoryOverviewComponent implements OnInit, OnDestroy {
  readonly inventoryService = inject(InventoryService);
  readonly scopeService = inject(WorkspaceScopeService);
  readonly authService = inject(AuthService);
  readonly router = inject(Router);

  /** Configurable limit of recent transactions displayed on the overview dashboard. Defaults to 10. */
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
    "You don't have permission to access inventory records. Contact your administrator if you believe this is an error."
  );

  // Raw data from persistent database via real API
  readonly summary = signal<InventorySummaryDto>({
    totalStockItems: 0,
    totalWarehouses: 0,
    totalLocations: 0,
    lowStockItems: 0,
    totalTransactions: 0,
    totalOnHandQuantity: 0,
    totalInventoryValue: 0
  });

  readonly transactions = signal<InventoryRecentTransactionDto[]>([]);

  // Search & Filter State
  readonly recentSearchTerm = signal<string>('');
  readonly recentMovementFilter = signal<string>('all');

  private readonly subs = new Subscription();
  private activeRequestId = 0;

  // Filter options for movement type
  readonly statusOptions = [
    { label: 'All movement types', value: 'all' },
    { label: 'Goods Receipt', value: 'goodsreceipt' },
    { label: 'Transfer In', value: 'internaltransferin' },
    { label: 'Transfer Out', value: 'internaltransferout' },
    { label: 'Adjustment In', value: 'stockadjustmentin' },
    { label: 'Adjustment Out', value: 'stockadjustmentout' },
    { label: 'Shipment Dispatch', value: 'shipmentdispatch' }
  ];

  // Breadcrumbs
  readonly breadcrumbs = [
    { label: 'Workspace', routerLink: '/app/dashboard' },
    { label: 'Inventory Overview' }
  ];

  // Computed metrics
  readonly totalStockItems = computed<number>(() => this.summary().totalStockItems);
  readonly totalWarehouses = computed<number>(() => this.summary().totalWarehouses);
  readonly totalLocations = computed<number>(() => this.summary().totalLocations);
  readonly lowStockItems = computed<number>(() => this.summary().lowStockItems);
  readonly totalTransactions = computed<number>(() => this.summary().totalTransactions);
  readonly totalOnHandQuantity = computed<number>(() => this.summary().totalOnHandQuantity);
  readonly totalInventoryValue = computed<number>(() => this.summary().totalInventoryValue);

  // Active currency
  readonly activeCurrency = computed<string>(() => {
    return this.scopeService.selectedCompany()?.currency || 'LKR';
  });

  // Filtered and limited recent transactions
  readonly recentTransactions = computed<InventoryRecentTransactionDto[]>(() => {
    let list = [...this.transactions()];

    const filter = this.recentMovementFilter().toLowerCase();
    if (filter !== 'all') {
      list = list.filter(t => (t.movementType || '').toLowerCase() === filter);
    }

    const term = this.recentSearchTerm().trim().toLowerCase();
    if (term) {
      list = list.filter(t => {
        const num = (t.transactionNumber || '').toLowerCase();
        const code = (t.productCode || '').toLowerCase();
        const name = (t.productName || '').toLowerCase();
        const wh = (t.warehouseName || '').toLowerCase();
        const batch = (t.batchNumber || '').toLowerCase();
        return (
          num.includes(term) ||
          code.includes(term) ||
          name.includes(term) ||
          wh.includes(term) ||
          batch.includes(term)
        );
      });
    }

    return list.slice(0, Math.max(1, this._recentLimit()));
  });

  // Configuration contract for AppModuleOverviewComponent
  readonly overviewConfig = computed<ModuleOverviewConfig<InventoryRecentTransactionDto>>(() => ({
    moduleName: 'Inventory Overview',
    moduleDescription: 'Real-time stock balances, warehouse storage, and inventory ledger movements',
    breadcrumbs: this.breadcrumbs,
    dashboardCards: [
      {
        id: 'stockItems',
        title: 'Stock Items',
        icon: 'pi pi-box',
        value: this.totalStockItems(),
        colorVariant: 'blue',
        description: 'Tracked SKU catalogue',
        route: '/app/master-data/products',
        requiredPermissions: ['PRODUCT.VIEW']
      },
      {
        id: 'warehouses',
        title: 'Warehouses',
        icon: 'pi pi-building',
        value: this.totalWarehouses(),
        colorVariant: 'slate',
        description: `${this.totalLocations()} storage locations`,
        route: '/app/master-data',
        requiredPermissions: ['WAREHOUSE.VIEW']
      },
      {
        id: 'lowStock',
        title: 'Low Stock Alerts',
        icon: 'pi pi-exclamation-triangle',
        value: this.lowStockItems(),
        colorVariant: this.lowStockItems() > 0 ? 'amber' : 'green',
        description: this.lowStockItems() > 0 ? 'At or below reorder level' : 'Stock levels healthy',
        route: '/app/master-data/products',
        requiredPermissions: ['PRODUCT.VIEW']
      },
      {
        id: 'movements',
        title: 'Ledger Movements',
        icon: 'pi pi-history',
        value: this.totalTransactions(),
        colorVariant: 'green',
        description: `${this.totalOnHandQuantity().toLocaleString()} units on hand`,
        requiredPermissions: ['PRODUCT.VIEW']
      }
    ],
    processFlow: {
      enabled: true,
      position: 'belowCards',
      title: 'Supply Chain Material Flow & Stock Ownership',
      subtitle: 'From Purchasing GRN inbound receipt to Inventory storage, stock control, and Sales dispatch',
      layout: 'horizontal',
      stages: [
        {
          id: 'inbound-grn',
          code: 'Goods Receiving (GRN)',
          label: 'Goods Receiving (GRN)',
          description: 'Purchasing: Confirmed Goods Receipt Notes for PO shipments inbound to warehouse',
          icon: 'package-check',
          status: 'completed',
          statusLabel: 'Purchasing Inbound',
          badge: this.summary().totalTransactions > 0 ? this.summary().totalTransactions : undefined,
          isAlternative: true,
          pathGroup: 'inbound',
          convergesTo: 'store-putaway',
          route: '/app/procurement/goods-receipts',
          requiredPermissions: ['GRN.VIEW']
        },
        {
          id: 'stock-receipt',
          code: 'Stock Receipt',
          label: 'Stock Receipt',
          description: 'Inventory: Opening balance, customer return, assembly, and general stock inflow',
          icon: 'arrow-down-to-dot',
          status: 'active',
          statusLabel: 'General Inbound',
          isAlternative: true,
          pathGroup: 'inbound',
          convergesTo: 'store-putaway',
          route: '/app/inventory/stock-receipts',
          requiredPermissions: ['PRODUCT.VIEW']
        },
        {
          id: 'store-putaway',
          code: 'Storage & Putaway',
          label: 'Storage & Putaway',
          description: 'Inventory: Storage bin placement, receiving storage locations, and putaway status',
          icon: 'boxes',
          status: 'active',
          statusLabel: 'Storage & Bins',
          isOptional: true,
          stageType: 'optional',
          route: '/app/inventory/store-putaway',
          requiredPermissions: ['PRODUCT.VIEW', 'WAREHOUSE.VIEW']
        },
        {
          id: 'transfer-stock',
          code: 'Stock Transfers',
          label: 'Stock Transfers',
          description: 'Inventory: Internal warehouse movements and bin-to-bin stock transfers',
          icon: 'arrow-right-left',
          status: 'active',
          statusLabel: 'Internal Movements',
          route: '/app/inventory/transfers',
          requiredPermissions: ['PRODUCT.VIEW', 'WAREHOUSE.VIEW']
        },
        {
          id: 'count-adjust',
          code: 'Stock Count & Adjustment',
          label: 'Stock Count & Adjustment',
          description: 'Inventory: Physical inventory cycle counting, stock variance review, and authorized adjustments',
          icon: 'check-square',
          status: 'active',
          statusLabel: 'Stock Audit',
          isConditional: true,
          stageType: 'conditional',
          route: '/app/inventory/count-adjust',
          requiredPermissions: ['PRODUCT.VIEW']
        },
        {
          id: 'issue-stock',
          code: 'Stock Issue',
          label: 'Stock Issue',
          description: 'Inventory: Common stock issuing to Sales dispatch, Manufacturing, and internal consumption',
          icon: 'truck',
          status: 'active',
          statusLabel: 'Stock Deduction',
          route: '/app/inventory/issue-stock',
          requiredPermissions: ['PRODUCT.VIEW']
        }
      ]
    },
    recentTransactions: {
      title: 'Recent Inventory Movements',
      subtitle: `Audit ledger activity · default ${this.recentLimit} movements`,
      limit: this.recentLimit,
      showSearch: true,
      showFilters: true,
      showViewAll: false,
      createActionLabel: 'Receive Stock',
      createActionIcon: 'pi pi-plus',
      createActionRoute: '/app/inventory/stock-receipts/new',
      createActionPermission: 'PRODUCT.EDIT'
    },
    navigationLinks: [
      {
        label: 'Stock Receipt',
        icon: 'pi pi-arrow-circle-down',
        route: '/app/inventory/stock-receipts',
        outlined: true,
        severity: 'secondary',
        requiredPermissions: ['PRODUCT.VIEW']
      },
      {
        label: 'Storage & Putaway',
        icon: 'pi pi-inbox',
        route: '/app/inventory/store-putaway',
        outlined: true,
        severity: 'secondary',
        requiredPermissions: ['PRODUCT.VIEW', 'WAREHOUSE.VIEW']
      },
      {
        label: 'Stock Transfers',
        icon: 'pi pi-arrow-right-arrow-left',
        route: '/app/inventory/transfers',
        outlined: true,
        severity: 'secondary',
        requiredPermissions: ['PRODUCT.VIEW', 'WAREHOUSE.VIEW']
      },
      {
        label: 'Stock Count & Adjustment',
        icon: 'pi pi-check-square',
        route: '/app/inventory/count-adjust',
        outlined: true,
        severity: 'secondary',
        requiredPermissions: ['PRODUCT.VIEW']
      },
      {
        label: 'Stock Issue',
        icon: 'pi pi-arrow-circle-up',
        route: '/app/inventory/issue-stock',
        outlined: true,
        severity: 'secondary',
        requiredPermissions: ['PRODUCT.VIEW']
      },
      {
        label: 'Goods Receiving (GRN)',
        icon: 'pi pi-box',
        route: '/app/procurement/goods-receipts',
        outlined: true,
        severity: 'secondary',
        requiredPermissions: ['GRN.VIEW']
      },
      {
        label: 'Item Master (Products)',
        icon: 'pi pi-database',
        route: '/app/master-data/products',
        outlined: true,
        severity: 'secondary',
        requiredPermissions: ['PRODUCT.VIEW']
      }
    ]
  }));

  // Columns for the recent movements table
  readonly recentColumns: DataTableColumn<InventoryRecentTransactionDto>[] = [
    {
      field: 'transactionNumber',
      header: 'TRANSACTION #',
      sortable: true
    },
    {
      field: 'productName',
      header: 'ITEM / SKU',
      sortable: true,
      format: (_, row) => `${row.productCode} - ${row.productName}`
    },
    {
      field: 'warehouseName',
      header: 'WAREHOUSE / BIN',
      sortable: true,
      format: (_, row) => `${row.warehouseName}${row.locationCode ? ' (' + row.locationCode + ')' : ''}`
    },
    {
      field: 'quantity',
      header: 'QUANTITY',
      sortable: true,
      align: 'right',
      format: (val, row) => `${Number(val) > 0 ? '+' : ''}${Number(val).toLocaleString()} ${row.unitOfMeasure || 'PCS'}`
    },
    {
      field: 'movementType',
      header: 'MOVEMENT TYPE',
      type: 'badge',
      sortable: true,
      align: 'center',
      format: val => getMovementTypeDisplayName(val),
      badgeSeverityMap: val => getMovementTypeBadgeSeverity(val)
    },
    {
      field: 'transactionDate',
      header: 'DATE',
      sortable: true,
      align: 'right',
      format: val => (val ? new Date(val).toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' }) : '-')
    }
  ];

  readonly recentActions: DataTableAction<InventoryRecentTransactionDto>[] = [];

  ngOnInit(): void {
    this.loadOverviewData();
    this.subs.add(
      this.scopeService.scopeChange$.subscribe(() => {
        this.loadOverviewData();
      })
    );
  }

  ngOnDestroy(): void {
    this.subs.unsubscribe();
  }

  loadOverviewData(): void {
    const requestId = ++this.activeRequestId;
    this.loading.set(true);
    this.error.set(null);
    this.isAccessDenied.set(false);

    // Call real API endpoints
    this.inventoryService.getSummary().subscribe({
      next: summary => {
        if (requestId !== this.activeRequestId) return;
        this.summary.set(summary);
      },
      error: err => {
        if (requestId !== this.activeRequestId) return;
        if (err?.status === 403) {
          this.isAccessDenied.set(true);
        } else {
          this.error.set(err?.message || 'Failed to load inventory summary metrics.');
        }
      }
    });

    this.inventoryService.getRecentTransactions(this.recentLimit).subscribe({
      next: transactions => {
        if (requestId !== this.activeRequestId) return;
        this.transactions.set(transactions || []);
        this.loading.set(false);
      },
      error: err => {
        if (requestId !== this.activeRequestId) return;
        this.loading.set(false);
        if (err?.status === 403) {
          this.isAccessDenied.set(true);
        } else if (!this.error()) {
          this.error.set(err?.message || 'Failed to load recent inventory movements.');
        }
      }
    });
  }

  navigateToCreate(): void {
    this.router.navigate(['/app/inventory/stock-receipts/new']);
  }

  onCardClick(card: DashboardCardConfig): void {
    if (card.route) {
      this.router.navigate([card.route], { queryParams: card.queryParams });
    }
  }

  onStageClick(stage: ProcessStageConfig): void {
    if (stage.route && !stage.planned) {
      this.router.navigate([stage.route], { queryParams: stage.queryParams });
    }
  }
}
