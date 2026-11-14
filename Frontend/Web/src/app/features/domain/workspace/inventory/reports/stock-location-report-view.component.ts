import {
  Component,
  Input,
  OnInit,
  OnDestroy,
  inject,
  signal,
  computed,
  ChangeDetectionStrategy
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { Subscription } from 'rxjs';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { AppPageHeaderComponent } from '../../../../../shared/components/page-header/page-header.component';
import { AppDataTableComponent } from '../../../../../shared/components/data-table/data-table.component';
import { AppLoadingStateComponent } from '../../../../../shared/components/loading-state/loading-state.component';
import { AppErrorStateComponent } from '../../../../../shared/components/error-state/error-state.component';
import { AppEmptyStateComponent } from '../../../../../shared/components/empty-state/empty-state.component';
import { AccessDeniedComponent } from '../../../../../shared/components/access-denied/access-denied.component';
import { InventoryService } from '../../../../../core/services/inventory.service';
import { MasterDataService } from '../../../../../core/services/master-data.service';
import { WorkspaceScopeService } from '../../../../../core/services/workspace-scope.service';
import { AuthService } from '../../../../../core/auth/auth.service';
import { isForbiddenError, toUserFacingErrorMessage } from '../../../../../core/utils/http-error.util';
import {
  StockByLocationReportDto,
  StockByLocationRowDto,
  StockByLocationReportQuery
} from '../../../../../core/models/inventory.models';
import { DataTableColumn, SelectOption } from '../../../../../shared/models/data-table-config.model';

export type ReportMode = 'storage' | 'staging';

@Component({
  selector: 'app-stock-location-report-view',
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
  templateUrl: './stock-location-report-view.component.html',
  styleUrl: './stock-location-report-view.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class StockLocationReportViewComponent implements OnInit, OnDestroy {
  @Input({ required: true }) mode: ReportMode = 'storage';

  readonly inventoryService = inject(InventoryService);
  readonly masterDataService = inject(MasterDataService);
  readonly scopeService = inject(WorkspaceScopeService);
  readonly authService = inject(AuthService);

  private readonly subs = new Subscription();

  // State
  readonly loading = signal<boolean>(false);
  readonly error = signal<string | null>(null);
  readonly isAccessDenied = signal<boolean>(false);
  readonly accessDeniedMessage = signal<string>(
    "You don't have permission to view inventory reports. Required permission: INVENTORY.VIEW."
  );

  // Master Data Options
  readonly warehouses = signal<SelectOption[]>([
    { label: 'All Warehouses', value: '' }
  ]);
  readonly locations = signal<SelectOption[]>([
    { label: 'All Locations', value: '' }
  ]);

  // Filters
  readonly selectedWarehouseId = signal<string>('');
  readonly selectedLocationId = signal<string>('');
  readonly searchTerm = signal<string>('');

  // Report Data
  readonly reportData = signal<StockByLocationReportDto>({
    items: [],
    totalCount: 0,
    totalQuantityOnHand: 0,
    totalQuantityAvailable: 0,
    totalStockValue: 0,
    currency: 'LKR',
    page: 1,
    pageSize: 25
  });

  // Computed Properties
  readonly isStorage = computed(() => this.mode === 'storage');

  readonly pageTitle = computed(() =>
    this.isStorage() ? 'Stock by Storage Location' : 'Stock by Staging Location'
  );

  readonly pageSubtitle = computed(() =>
    this.isStorage()
      ? 'Current physical stock balances held in permanent warehouse storage locations (racks, shelves, and bins).'
      : 'Current physical stock balances held in inbound receiving docks and staging locations awaiting Put Away.'
  );

  readonly breadcrumbs = computed(() => [
    { label: 'Workspace', routerLink: '/app/dashboard' },
    { label: 'Inventory', routerLink: '/app/inventory' },
    { label: 'Reports' },
    { label: this.pageTitle() }
  ]);

  readonly currency = computed(() => this.reportData().currency || 'LKR');
  readonly totalCount = computed(() => this.reportData().totalCount);
  readonly totalOnHand = computed(() => this.reportData().totalQuantityOnHand);
  readonly totalAvailable = computed(() => this.reportData().totalQuantityAvailable);
  readonly totalStockValue = computed(() => this.reportData().totalStockValue);

  // Table Columns
  readonly columns: DataTableColumn<StockByLocationRowDto>[] = [
    {
      field: 'productCode',
      header: 'PRODUCT CODE',
      sortable: true,
      filterable: true
    },
    {
      field: 'productName',
      header: 'PRODUCT NAME',
      sortable: true,
      filterable: true
    },
    {
      field: 'categoryName',
      header: 'CATEGORY',
      sortable: true,
      filterable: true,
      format: val => val || 'General'
    },
    {
      field: 'unitOfMeasure',
      header: 'UOM',
      align: 'center',
      sortable: true
    },
    {
      field: 'warehouseName',
      header: 'WAREHOUSE',
      sortable: true,
      filterable: true
    },
    {
      field: 'locationCode',
      header: 'LOCATION',
      sortable: true,
      filterable: true,
      format: (_, row) => `${row.locationCode}${row.locationName ? ' (' + row.locationName + ')' : ''}`
    },
    {
      field: 'hierarchyPath',
      header: 'HIERARCHY PATH',
      sortable: true,
      filterable: true,
      format: val => val || '—'
    },
    {
      field: 'locationClassification',
      header: 'TYPE',
      type: 'badge',
      align: 'center',
      sortable: true,
      badgeSeverityMap: val => (val === 'Storage' ? 'success' : 'warning')
    },
    {
      field: 'batchNumber',
      header: 'BATCH / LOT',
      sortable: true,
      filterable: true,
      format: val => val || '—'
    },
    {
      field: 'serialNumber',
      header: 'SERIAL NO',
      sortable: true,
      filterable: true,
      format: val => val || '—'
    },
    {
      field: 'quantityOnHand',
      header: 'ON-HAND QTY',
      align: 'right',
      sortable: true,
      format: (val, row) => `${Number(val).toLocaleString()} ${row.unitOfMeasure || 'PCS'}`
    },
    {
      field: 'quantityAvailable',
      header: 'AVAILABLE QTY',
      align: 'right',
      sortable: true,
      format: (val, row) => `${Number(val).toLocaleString()} ${row.unitOfMeasure || 'PCS'}`
    },
    {
      field: 'unitCost',
      header: 'UNIT COST',
      align: 'right',
      sortable: true,
      format: (val, row) =>
        `${row.currency} ${Number(val).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 4 })}`
    },
    {
      field: 'totalCostValue',
      header: 'TOTAL VALUE',
      align: 'right',
      sortable: true,
      format: (val, row) =>
        `${row.currency} ${Number(val).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`
    },
    {
      field: 'valuationMethod',
      header: 'VALUATION',
      align: 'center',
      sortable: true,
      type: 'badge',
      badgeSeverityMap: () => 'info'
    }
  ];

  ngOnInit(): void {
    if (!this.checkPermission()) {
      return;
    }

    this.loadWarehouses();
    this.loadReport();

    // Reload if active scope changes
    this.subs.add(
      this.scopeService.scopeChange$.subscribe(() => {
        this.loadWarehouses();
        this.loadReport();
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

  loadWarehouses(): void {
    this.masterDataService.getWarehouses().subscribe({
      next: whs => {
        const opts: SelectOption[] = [{ label: 'All Warehouses', value: '' }];
        if (whs && whs.length) {
          whs.forEach(w => opts.push({ label: `${w.warehouseCode} - ${w.warehouseName}`, value: w.warehouseId }));
        }
        this.warehouses.set(opts);
      },
      error: () => {
        this.warehouses.set([{ label: 'All Warehouses', value: '' }]);
      }
    });
  }

  onWarehouseChange(warehouseId: string): void {
    this.selectedWarehouseId.set(warehouseId);
    this.selectedLocationId.set('');

    if (!warehouseId) {
      this.locations.set([{ label: 'All Locations', value: '' }]);
      this.loadReport();
      return;
    }

    this.masterDataService.getWarehouseLocations(warehouseId, null, true).subscribe({
      next: locs => {
        const opts: SelectOption[] = [{ label: 'All Locations', value: '' }];
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
        this.loadReport();
      },
      error: () => {
        this.locations.set([{ label: 'All Locations', value: '' }]);
        this.loadReport();
      }
    });
  }

  onLocationChange(locationId: string): void {
    this.selectedLocationId.set(locationId);
    this.loadReport();
  }

  onSearch(term: string): void {
    this.searchTerm.set(term);
    this.loadReport();
  }

  resetFilters(): void {
    this.selectedWarehouseId.set('');
    this.selectedLocationId.set('');
    this.searchTerm.set('');
    this.locations.set([{ label: 'All Locations', value: '' }]);
    this.loadReport();
  }

  loadReport(): void {
    this.loading.set(true);
    this.error.set(null);

    const query: StockByLocationReportQuery = {
      locationClassification: this.mode,
      warehouseId: this.selectedWarehouseId() || null,
      locationId: this.selectedLocationId() || null,
      search: this.searchTerm() || null,
      pageSize: 1000 // Get all matching for rich client filtering/paging
    };

    const company = this.scopeService.selectedCompany();
    if (company?.id) query.companyId = company.id;

    const branch = this.scopeService.selectedBranch();
    if (branch?.id) query.branchId = branch.id;

    this.inventoryService.getStockByLocationReport(query).subscribe({
      next: res => {
        this.reportData.set(res);
        this.loading.set(false);
      },
      error: err => {
        this.loading.set(false);
        if (isForbiddenError(err)) {
          this.isAccessDenied.set(true);
        } else {
          this.error.set(toUserFacingErrorMessage(err, 'Failed to load stock location report.'));
        }
      }
    });
  }
}
