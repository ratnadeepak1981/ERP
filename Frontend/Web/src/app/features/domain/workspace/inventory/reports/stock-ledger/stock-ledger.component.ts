import {
  Component,
  OnInit,
  OnDestroy,
  inject,
  signal,
  computed,
  ChangeDetectionStrategy
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule, ActivatedRoute } from '@angular/router';
import { Subscription } from 'rxjs';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { AppPageHeaderComponent } from '../../../../../../shared/components/page-header/page-header.component';
import { AppDataTableComponent } from '../../../../../../shared/components/data-table/data-table.component';
import { AppLoadingStateComponent } from '../../../../../../shared/components/loading-state/loading-state.component';
import { AppErrorStateComponent } from '../../../../../../shared/components/error-state/error-state.component';
import { AccessDeniedComponent } from '../../../../../../shared/components/access-denied/access-denied.component';
import { InventoryService } from '../../../../../../core/services/inventory.service';
import { MasterDataService } from '../../../../../../core/services/master-data.service';
import { WorkspaceScopeService } from '../../../../../../core/services/workspace-scope.service';
import { AuthService } from '../../../../../../core/auth/auth.service';
import { isForbiddenError, toUserFacingErrorMessage } from '../../../../../../core/utils/http-error.util';
import {
  StockLedgerReportDto,
  StockLedgerRowDto,
  StockLedgerReportQuery,
  getMovementTypeDisplayName,
  getMovementTypeBadgeSeverity
} from '../../../../../../core/models/inventory.models';
import { DataTableColumn, SelectOption } from '../../../../../../shared/models/data-table-config.model';
import { WarehouseItem } from '../../../../../../core/models/scope.models';

@Component({
  selector: 'app-stock-ledger',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterModule,
    ButtonModule,
    InputTextModule,
    AppPageHeaderComponent,
    AppDataTableComponent,
    AppLoadingStateComponent,
    AppErrorStateComponent,
    AccessDeniedComponent
  ],
  templateUrl: './stock-ledger.component.html',
  styleUrl: './stock-ledger.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class StockLedgerComponent implements OnInit, OnDestroy {
  readonly inventoryService = inject(InventoryService);
  readonly masterDataService = inject(MasterDataService);
  readonly scopeService = inject(WorkspaceScopeService);
  readonly authService = inject(AuthService);
  readonly route = inject(ActivatedRoute);

  private readonly subs = new Subscription();
  private allWarehousesRaw: WarehouseItem[] = [];
  private activeRequestId = 0;

  // Permissions and View State
  readonly loading = signal<boolean>(false);
  readonly error = signal<string | null>(null);
  readonly isAccessDenied = signal<boolean>(false);
  readonly accessDeniedMessage = signal<string>(
    "You don't have permission to view inventory reports. Required permission: INVENTORY.VIEW."
  );

  // Master Data Options
  readonly products = signal<SelectOption[]>([
    { label: '-- Select Product --', value: '' }
  ]);
  readonly warehouses = signal<SelectOption[]>([
    { label: '-- Select Warehouse --', value: '' }
  ]);
  readonly locations = signal<SelectOption[]>([
    { label: 'All Locations (Whole Warehouse)', value: '' }
  ]);

  // Filter Selections
  readonly selectedProductId = signal<string>('');
  readonly selectedWarehouseId = signal<string>('');
  readonly selectedLocationId = signal<string>('');
  readonly dateFrom = signal<string>('');
  readonly dateTo = signal<string>('');
  readonly searchTerm = signal<string>('');

  // Report Data
  readonly reportData = signal<StockLedgerReportDto | null>(null);

  // Workspace Context Computeds
  readonly currentCompanyName = computed(() => {
    const comp = this.scopeService.selectedCompany();
    return comp ? `${comp.name}${comp.code ? ' (' + comp.code + ')' : ''}` : 'All Permitted Companies';
  });

  readonly currentBranchName = computed(() => {
    if (this.scopeService.isAllBranches()) {
      return 'All Permitted Branches';
    }
    const branch = this.scopeService.selectedBranch();
    return branch ? `${branch.name}${branch.code ? ' (' + branch.code + ')' : ''}` : 'All Permitted Branches';
  });

  // Breadcrumbs & Navigation
  readonly breadcrumbs = computed(() => [
    { label: 'Workspace', routerLink: '/app/dashboard' },
    { label: 'Inventory', routerLink: '/app/inventory' },
    { label: 'Reports' },
    { label: 'Stock Ledger – Product by Location' }
  ]);

  // Validation & Ready State
  readonly hasRequiredFilters = computed(() =>
    !!this.selectedProductId() && !!this.selectedWarehouseId()
  );

  // Financial & Metric Computeds
  readonly currency = computed(() => this.reportData()?.currency || 'LKR');
  readonly valuationMethod = computed(() => this.reportData()?.valuationMethod || 'FIFO');
  readonly openingQuantity = computed(() => this.reportData()?.openingQuantity ?? 0);
  readonly openingValue = computed(() => this.reportData()?.openingValue ?? 0);
  readonly totalQuantityIn = computed(() => this.reportData()?.totalQuantityIn ?? 0);
  readonly totalInwardValue = computed(() => this.reportData()?.totalInwardValue ?? 0);
  readonly totalQuantityOut = computed(() => this.reportData()?.totalQuantityOut ?? 0);
  readonly totalOutwardValue = computed(() => this.reportData()?.totalOutwardValue ?? 0);
  readonly closingQuantity = computed(() => this.reportData()?.closingQuantity ?? 0);
  readonly closingValue = computed(() => this.reportData()?.closingValue ?? 0);
  readonly totalTransactions = computed(() => this.reportData()?.totalCount ?? 0);

  // Detailed Ledger Columns
  readonly columns: DataTableColumn<StockLedgerRowDto>[] = [
    {
      field: 'transactionDate',
      header: 'TXN DATE',
      sortable: true,
      format: val => (val ? new Date(val).toLocaleString() : '—')
    },
    {
      field: 'postingDate',
      header: 'POSTED DATE',
      sortable: true,
      format: val => (val ? new Date(val).toLocaleDateString() : '—')
    },
    {
      field: 'transactionType',
      header: 'TYPE',
      type: 'badge',
      sortable: true,
      format: val => getMovementTypeDisplayName(val),
      badgeSeverityMap: val => getMovementTypeBadgeSeverity(val)
    },
    {
      field: 'movementTypeCode',
      header: 'CODE',
      align: 'center',
      sortable: true
    },
    {
      field: 'documentReference',
      header: 'DOC REFERENCE',
      sortable: true,
      filterable: true
    },
    {
      field: 'sourceDocumentType',
      header: 'SOURCE TYPE',
      sortable: true
    },
    {
      field: 'sourceLocationCode',
      header: 'FROM LOCATION',
      sortable: true,
      format: val => val || '—'
    },
    {
      field: 'destinationLocationCode',
      header: 'TO LOCATION',
      sortable: true,
      format: val => val || '—'
    },
    {
      field: 'batchNumber',
      header: 'BATCH / LOT',
      sortable: true,
      format: val => val || '—'
    },
    {
      field: 'serialNumber',
      header: 'SERIAL NO',
      sortable: true,
      format: val => val || '—'
    },
    {
      field: 'quantityIn',
      header: 'QTY IN',
      align: 'right',
      sortable: true,
      format: (val, row) => (val > 0 ? `+${Number(val).toLocaleString()} ${row.unitOfMeasure}` : '—')
    },
    {
      field: 'quantityOut',
      header: 'QTY OUT',
      align: 'right',
      sortable: true,
      format: (val, row) => (val > 0 ? `-${Number(val).toLocaleString()} ${row.unitOfMeasure}` : '—')
    },
    {
      field: 'costRate',
      header: 'COST RATE',
      align: 'right',
      sortable: true,
      format: (val, row) =>
        `${row.currency} ${Number(val).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 4 })}`
    },
    {
      field: 'inwardValue',
      header: 'INWARD VALUE',
      align: 'right',
      sortable: true,
      format: (val, row) =>
        val > 0
          ? `${row.currency} ${Number(val).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`
          : '—'
    },
    {
      field: 'outwardValue',
      header: 'OUTWARD VALUE',
      align: 'right',
      sortable: true,
      format: (val, row) =>
        val > 0
          ? `${row.currency} ${Number(val).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`
          : '—'
    },
    {
      field: 'runningQuantity',
      header: 'RUNNING QTY',
      align: 'right',
      sortable: true,
      format: (val, row) => `${Number(val).toLocaleString()} ${row.unitOfMeasure}`
    },
    {
      field: 'runningValue',
      header: 'RUNNING VALUE',
      align: 'right',
      sortable: true,
      format: (val, row) =>
        `${row.currency} ${Number(val).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`
    }
  ];

  ngOnInit(): void {
    if (!this.checkPermission()) {
      return;
    }

    this.loadProducts();
    this.loadWarehouses();

    // Check query params for deep-link / drill-down navigation
    this.subs.add(
      this.route.queryParams.subscribe(params => {
        if (params['productId']) {
          this.selectedProductId.set(params['productId']);
        }
        if (params['warehouseId']) {
          this.selectedWarehouseId.set(params['warehouseId']);
          this.loadWarehouseLocations(params['warehouseId'], params['locationId'] || params['warehouseLocationId']);
        }
        if (this.hasRequiredFilters()) {
          this.loadLedger();
        }
      })
    );

    // Reactively handle workspace company/branch scope changes
    this.subs.add(
      this.scopeService.scopeChange$.subscribe(() => {
        this.handleWorkspaceChange();
      })
    );
  }

  ngOnDestroy(): void {
    this.subs.unsubscribe();
  }

  private checkPermission(): boolean {
    const hasView = this.authService.hasPermission('INVENTORY.VIEW');
    if (!hasView) {
      this.isAccessDenied.set(true);
      return false;
    }
    return true;
  }

  loadProducts(): void {
    this.masterDataService.getProducts().subscribe({
      next: prods => {
        const opts: SelectOption[] = [{ label: '-- Select Product --', value: '' }];
        if (prods && prods.length) {
          prods.forEach(p => {
            opts.push({
              label: `${p.productCode} - ${p.productName} (${p.unitOfMeasure || 'PCS'})`,
              value: p.productId
            });
          });
        }
        this.products.set(opts);
      },
      error: () => {
        this.products.set([{ label: '-- Select Product --', value: '' }]);
      }
    });
  }

  loadWarehouses(): void {
    this.masterDataService.getWarehouses().subscribe({
      next: whs => {
        this.allWarehousesRaw = whs || [];
        this.filterWarehousesByActiveScope();
      },
      error: () => {
        this.allWarehousesRaw = [];
        this.warehouses.set([{ label: '-- Select Warehouse --', value: '' }]);
      }
    });
  }

  private filterWarehousesByActiveScope(): void {
    const company = this.scopeService.selectedCompany();
    const branch = this.scopeService.selectedBranch();
    const isAllBranches = this.scopeService.isAllBranches();

    let filtered = this.allWarehousesRaw;
    if (company?.id) {
      filtered = filtered.filter(w => !w.companyId || w.companyId === company.id);
    }
    if (!isAllBranches && branch?.id) {
      filtered = filtered.filter(w => !w.branchId || w.branchId === branch.id);
    }

    const opts: SelectOption[] = [{ label: '-- Select Warehouse --', value: '' }];
    filtered.forEach(w => {
      opts.push({ label: `${w.warehouseCode} - ${w.warehouseName}`, value: w.warehouseId });
    });
    this.warehouses.set(opts);

    // If previously selected warehouse is not in current scope, clear it
    if (this.selectedWarehouseId() && !filtered.some(w => w.warehouseId === this.selectedWarehouseId())) {
      this.selectedWarehouseId.set('');
      this.selectedLocationId.set('');
      this.locations.set([{ label: 'All Locations (Whole Warehouse)', value: '' }]);
      this.reportData.set(null);
    }
  }

  onProductChange(productId: string): void {
    this.selectedProductId.set(productId);
    if (this.hasRequiredFilters()) {
      this.loadLedger();
    } else {
      this.reportData.set(null);
    }
  }

  onWarehouseChange(warehouseId: string): void {
    this.selectedWarehouseId.set(warehouseId);
    this.selectedLocationId.set('');
    this.locations.set([{ label: 'All Locations (Whole Warehouse)', value: '' }]);
    this.reportData.set(null);

    if (warehouseId) {
      this.loadWarehouseLocations(warehouseId);
      if (this.hasRequiredFilters()) {
        this.loadLedger();
      }
    }
  }

  loadWarehouseLocations(warehouseId: string, initialLocationId?: string): void {
    this.masterDataService.getWarehouseLocations(warehouseId, null, true).subscribe({
      next: locs => {
        const opts: SelectOption[] = [{ label: 'All Locations (Whole Warehouse)', value: '' }];
        if (locs && locs.length) {
          locs.forEach(l => {
            const locId = l.warehouseLocationId || l.locationId || '';
            opts.push({
              label: `${l.locationCode} - ${l.locationName} (${l.locationTypeCode || 'General'})`,
              value: locId
            });
          });
        }
        this.locations.set(opts);

        if (initialLocationId && opts.some(o => o.value === initialLocationId)) {
          this.selectedLocationId.set(initialLocationId);
        } else {
          this.selectedLocationId.set('');
        }

        if (this.hasRequiredFilters()) {
          this.loadLedger();
        }
      },
      error: () => {
        this.locations.set([{ label: 'All Locations (Whole Warehouse)', value: '' }]);
      }
    });
  }

  onLocationChange(locationId: string): void {
    this.selectedLocationId.set(locationId);
    if (this.hasRequiredFilters()) {
      this.loadLedger();
    } else {
      this.reportData.set(null);
    }
  }

  onDateFromChange(date: string): void {
    this.dateFrom.set(date);
    if (this.hasRequiredFilters()) {
      this.loadLedger();
    }
  }

  onDateToChange(date: string): void {
    this.dateTo.set(date);
    if (this.hasRequiredFilters()) {
      this.loadLedger();
    }
  }

  onSearch(term: string): void {
    this.searchTerm.set(term);
    if (this.hasRequiredFilters()) {
      this.loadLedger();
    }
  }

  resetFilters(): void {
    this.selectedProductId.set('');
    this.selectedWarehouseId.set('');
    this.selectedLocationId.set('');
    this.dateFrom.set('');
    this.dateTo.set('');
    this.searchTerm.set('');
    this.locations.set([{ label: 'All Locations (Whole Warehouse)', value: '' }]);
    this.reportData.set(null);
    this.error.set(null);
  }

  handleWorkspaceChange(): void {
    // Re-filter warehouses according to new company/branch scope
    this.filterWarehousesByActiveScope();

    // If still have required filters valid, reload data; otherwise clear
    if (this.hasRequiredFilters()) {
      this.loadLedger();
    } else {
      this.reportData.set(null);
    }
  }

  loadLedger(): void {
    if (!this.hasRequiredFilters()) {
      return;
    }

    const currentReq = ++this.activeRequestId;
    this.loading.set(true);
    this.error.set(null);

    const query: StockLedgerReportQuery = {
      productId: this.selectedProductId(),
      warehouseId: this.selectedWarehouseId(),
      warehouseLocationId: this.selectedLocationId(),
      dateFrom: this.dateFrom() || null,
      dateTo: this.dateTo() || null,
      search: this.searchTerm() || null,
      pageSize: 1000
    };

    const company = this.scopeService.selectedCompany();
    if (company?.id) query.companyId = company.id;

    const branch = this.scopeService.selectedBranch();
    if (!this.scopeService.isAllBranches() && branch?.id) query.branchId = branch.id;

    this.inventoryService.getStockLedgerReport(query).subscribe({
      next: res => {
        if (currentReq !== this.activeRequestId) {
          return; // Ignore stale response
        }
        this.reportData.set(res);
        this.loading.set(false);
      },
      error: err => {
        if (currentReq !== this.activeRequestId) {
          return; // Ignore stale response
        }
        this.loading.set(false);
        if (isForbiddenError(err)) {
          this.isAccessDenied.set(true);
        } else {
          this.error.set(toUserFacingErrorMessage(err, 'Failed to load stock ledger transactions.'));
        }
      }
    });
  }
}
