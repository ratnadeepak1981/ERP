import {
  Component,
  OnInit,
  OnDestroy,
  Input,
  Output,
  EventEmitter,
  inject,
  signal,
  computed,
  ViewChild,
  ChangeDetectionStrategy
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { Subscription } from 'rxjs';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { TooltipModule } from 'primeng/tooltip';
import { AppPageHeaderComponent } from '../../../../../shared/components/page-header/page-header.component';
import { AppLoadingStateComponent } from '../../../../../shared/components/loading-state/loading-state.component';
import { AppErrorStateComponent } from '../../../../../shared/components/error-state/error-state.component';
import { AccessDeniedComponent } from '../../../../../shared/components/access-denied/access-denied.component';
import { AppEditableDataTableComponent } from '../../../../../shared/components/data-table/editable-data-table.component';
import { DataTableColumn, SelectOption } from '../../../../../shared/models/data-table-config.model';
import { LookupItem } from '../../../../../shared/components/lookup-dropdown/lookup-item.model';
import { InventoryService } from '../../../../../core/services/inventory.service';
import { MasterDataService } from '../../../../../core/services/master-data.service';
import { WorkspaceScopeService } from '../../../../../core/services/workspace-scope.service';
import { AuthService } from '../../../../../core/auth/auth.service';
import {
  StockIssueDto,
  StockIssuePurpose,
  StockReceiptDto,
  StockReceiptReason,
  CreateStockIssueRequest,
  CreateStockReceiptRequest,
  getStockIssuePurposeName,
  getStockIssuePurposeSeverity,
  getStockReceiptReasonName,
  getStockReceiptReasonSeverity
} from '../../../../../core/models/inventory.models';
import {
  ProductItem,
  WarehouseItem,
  WarehouseLocationItem
} from '../../../../../core/models/scope.models';
import {
  StockTransactionType,
  StockTransactionMode,
  StockTransactionLineItem,
  ReasonOrPurposeOption
} from './stock-transaction-form.models';

@Component({
  selector: 'app-stock-transaction-form',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterModule,
    ButtonModule,
    InputTextModule,
    TooltipModule,
    AppPageHeaderComponent,
    AppLoadingStateComponent,
    AppErrorStateComponent,
    AccessDeniedComponent,
    AppEditableDataTableComponent
  ],
  templateUrl: './stock-transaction-form.component.html',
  styleUrl: './stock-transaction-form.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class StockTransactionFormComponent implements OnInit, OnDestroy {
  @ViewChild(AppEditableDataTableComponent) editableTable?: AppEditableDataTableComponent<StockTransactionLineItem>;

  readonly inventoryService = inject(InventoryService);
  readonly masterDataService = inject(MasterDataService);
  readonly scopeService = inject(WorkspaceScopeService);
  readonly authService = inject(AuthService);
  readonly router = inject(Router);

  private readonly subs = new Subscription();

  // Inputs
  @Input({ required: true }) transactionType: StockTransactionType = 'issue';
  @Input() mode: StockTransactionMode = 'create';
  @Input() recordId: string | null = null;

  // Outputs
  @Output() transactionPosted = new EventEmitter<StockReceiptDto[] | StockIssueDto[]>();
  @Output() cancel = new EventEmitter<void>();

  // Loading & State
  readonly loading = signal<boolean>(false);
  readonly saving = signal<boolean>(false);
  readonly error = signal<string | null>(null);
  readonly isAccessDenied = signal<boolean>(false);
  readonly accessDeniedMessage = signal<string>(
    "You don't have permission to record or view inventory stock transactions."
  );

  // Master Data
  readonly warehouses = signal<WarehouseItem[]>([]);
  readonly products = signal<ProductItem[]>([]);
  readonly locationsMap = signal<Record<string, WarehouseLocationItem[]>>({});

  // Form State (Header)
  readonly headerWarehouseId = signal<string>('');
  readonly transactionDate = signal<string>(new Date().toISOString().substring(0, 10));
  readonly selectedReceiptReason = signal<StockReceiptReason>(StockReceiptReason.OpeningBalance);
  readonly selectedIssuePurpose = signal<StockIssuePurpose>(StockIssuePurpose.InternalUse);
  readonly sourceDocumentReference = signal<string>('');
  readonly remarks = signal<string>('');

  // Form State (Line items)
  readonly lines = signal<StockTransactionLineItem[]>([]);

  // View Mode Data
  readonly viewReceipt = signal<StockReceiptDto | null>(null);
  readonly viewIssue = signal<StockIssueDto | null>(null);

  // Configuration options depending on transaction type
  readonly isReceipt = computed(() => this.transactionType === 'receipt');
  readonly isIssue = computed(() => this.transactionType === 'issue');

  readonly reasonOrPurposeOptions = computed<ReasonOrPurposeOption[]>(() => {
    if (this.isReceipt()) {
      return [
        {
          label: 'Opening / Initial Balance',
          value: StockReceiptReason.OpeningBalance,
          description: 'Initialize warehouse stock quantities and inventory valuation on system setup',
          badgeSeverity: 'info'
        },
        {
          label: 'Customer Sales Return',
          value: StockReceiptReason.CustomerReturn,
          description: 'Receive returned products from customer back into sellable or inspection inventory',
          badgeSeverity: 'warning'
        },
        {
          label: 'Internal Production / Assembly',
          value: StockReceiptReason.InternalProduction,
          description: 'Receipt of finished goods or sub-assemblies produced on the shop floor',
          badgeSeverity: 'success'
        },
        {
          label: 'Miscellaneous Inbound',
          value: StockReceiptReason.Miscellaneous,
          description: 'Authorized general stock addition with ledger tracking and audit remarks',
          badgeSeverity: 'neutral'
        }
      ];
    } else {
      return [
        {
          label: 'Internal Use / Maintenance (Active)',
          value: StockIssuePurpose.InternalUse,
          description: 'Issue stock consumed for internal facility operations, maintenance, or scrap write-offs',
          badgeSeverity: 'info'
        },
        {
          label: 'Sales / Customer Dispatch (Active)',
          value: StockIssuePurpose.SalesDispatch,
          description: 'Issue stock against confirmed customer sales order shipment and delivery note',
          badgeSeverity: 'success'
        },
        {
          label: 'Manufacturing / Material Issue (Manual Reference)',
          value: StockIssuePurpose.Manufacturing,
          description: 'Issue raw materials to production work order (Manufacturing module integration planned)',
          badgeSeverity: 'warning'
        },
        {
          label: 'Other Authorized Purpose',
          value: StockIssuePurpose.Other,
          description: 'General stock issue with authorized management justification and tracking reference',
          badgeSeverity: 'neutral'
        }
      ];
    }
  });

  // Breadcrumbs
  readonly breadcrumbs = computed(() => {
    const root: Array<{ label: string; routerLink?: string }> = [
      { label: 'Workspace', routerLink: '/app/dashboard' },
      { label: 'Inventory', routerLink: '/app/inventory' }
    ];

    if (this.isReceipt()) {
      root.push({ label: 'Stock Receipt', routerLink: '/app/inventory/stock-receipts' });
      root.push({ label: this.mode === 'create' ? 'New Stock Receipt' : 'Receipt Details' });
    } else {
      root.push({ label: 'Stock Issue', routerLink: '/app/inventory/issue-stock' });
      root.push({ label: this.mode === 'create' ? 'New Stock Issue' : 'Issue Details' });
    }

    return root;
  });

  // Validation
  readonly isValid = computed<boolean>(() => {
    if (!this.headerWarehouseId()) return false;
    const currentLines = this.lines();
    if (currentLines.length === 0) return false;

    return currentLines.every(line => {
      if (!line.productId) return false;
      if (!line.warehouseLocationId) return false;
      if (line.quantity <= 0) return false;

      // Inbound receipt validation: unitCost >= 0 if present
      if (this.isReceipt()) {
        if (line.unitCost !== null && line.unitCost !== undefined && line.unitCost < 0) {
          return false;
        }
      }

      // Outbound issue validation: must not exceed available stock (negative stock prevention)
      if (this.isIssue()) {
        if (line.quantity > line.availableStock) {
          return false;
        }
      }

      return true;
    });
  });

  // Currency
  readonly activeCurrency = computed(() => {
    return this.scopeService.selectedCompany()?.currency || 'LKR';
  });

  // Product Lookup Items for AppEditableDataTableComponent
  readonly productLookupItems = computed<LookupItem[]>(() => {
    return this.products().map(p => ({
      id: p.productId,
      code: p.productCode,
      name: p.productName,
      category: p.categoryName || 'General',
      isEligible: p.isActive && p.isStockTracked,
      meta: {
        standardCost: p.standardCost,
        unitOfMeasure: p.unitOfMeasure || 'PCS'
      }
    }));
  });

  // Warehouse Facility select options
  readonly warehouseOptions = computed<SelectOption[]>(() => {
    return this.warehouses().map(w => ({
      label: `${w.warehouseCode} - ${w.warehouseName}`,
      value: w.warehouseId
    }));
  });

  // Reactive totals for footer summary
  readonly totalQuantity = computed<number>(() => {
    return this.lines().reduce((sum, item) => sum + (Number(item.quantity) || 0), 0);
  });

  readonly totalReceiptCost = computed<number>(() => {
    return this.lines().reduce((sum, item) => {
      const qty = Number(item.quantity) || 0;
      const cost = Number(item.unitCost) || 0;
      return sum + (qty * cost);
    }, 0);
  });

  // Blank New Row Factory for AppEditableDataTableComponent
  readonly defaultNewRow = (): Partial<StockTransactionLineItem> => {
    const targetWh = this.headerWarehouseId() || (this.warehouses().length > 0 ? this.warehouses()[0].warehouseId : '');
    const locs = this.getLocationsForWarehouse(targetWh);
    const targetLoc = locs.length > 0 ? locs[0].locationId : '';

    return {
      id: `line-${Date.now()}-${Math.random().toString(36).substring(2, 6)}`,
      productId: '',
      productCode: '',
      productName: '',
      unitOfMeasure: 'PCS',
      warehouseId: targetWh,
      warehouseLocationId: targetLoc,
      availableStock: 0,
      loadingStock: false,
      quantity: 1,
      unitCost: this.isReceipt() ? 0 : null
    };
  };

  // Row Calculation Callback
  readonly calculateRow = (row: StockTransactionLineItem): void => {
    if (this.isReceipt()) {
      row.unitCost = Number(row.unitCost) || 0;
      (row as any).lineTotal = Math.round((Number(row.quantity) || 0) * (Number(row.unitCost) || 0) * 100) / 100;
    }
  };

  // Columns Configuration for AppEditableDataTableComponent
  readonly columns = computed<DataTableColumn<StockTransactionLineItem>[]>(() => {
    const isReceiptMode = this.isReceipt();
    const isIssueMode = this.isIssue();

    const cols: DataTableColumn<StockTransactionLineItem>[] = [
      {
        field: 'productCode',
        header: 'Product / SKU *',
        editable: true,
        editType: 'lookup',
        lookupOptions: this.productLookupItems(),
        width: '280px',
        sortable: false,
        filterable: false,
        format: (_val, row) => {
          if (row.productCode && row.productName) {
            return `${row.productCode} — ${row.productName}`;
          }
          if (row.productCode) return row.productCode;
          return '';
        },
        onLookupSelect: (item, row) => {
          row.productId = String(item.id);
          row.productCode = item.code;
          row.productName = item.name;
          row.unitOfMeasure = item.meta?.unitOfMeasure || 'PCS';
          if (isReceiptMode) {
            if (row.unitCost === null || row.unitCost === undefined || row.unitCost === 0) {
              row.unitCost = item.meta?.standardCost ?? 0;
            }
            this.calculateRow(row);
          }
          if (isIssueMode && row.warehouseLocationId) {
            this.refreshAvailableStock(row);
          }
          this.lines.update(curr => curr.map(l => l.id === row.id ? { ...l, ...row } : l));
        },
        validators: [
          (_val, row) => (row.productId ? null : 'Product is required')
        ]
      },
      {
        field: 'warehouseId',
        header: 'Warehouse Facility *',
        editable: true,
        editType: 'select',
        editOptions: this.warehouseOptions(),
        width: '220px',
        sortable: false,
        filterable: false,
        format: (val) => {
          const w = this.warehouses().find(x => x.warehouseId === val);
          return w ? `${w.warehouseCode} - ${w.warehouseName}` : (val || '');
        },
        validators: [
          (val) => (val ? null : 'Warehouse is required')
        ]
      },
      {
        field: 'warehouseLocationId',
        header: isReceiptMode
          ? 'Destination Location (Rack → Level → Bin) *'
          : 'Source Location (Rack → Level → Bin) *',
        editable: true,
        editType: 'select',
        getEditOptions: (row) => this.getLocationOptionsForRow(row),
        width: '280px',
        sortable: false,
        filterable: false,
        format: (val) => {
          const loc = this.findLocation(val);
          return loc ? this.formatLocationHierarchy(loc) : (val || '');
        },
        validators: [
          (val) => (val ? null : 'Storage location is required')
        ]
      }
    ];

    if (isIssueMode) {
      cols.push({
        field: 'availableStock',
        header: 'Available Stock',
        editable: false,
        align: 'right',
        width: '130px',
        sortable: false,
        filterable: false,
        format: (val, row) => {
          if (row.loadingStock) return 'Loading...';
          if (!row.productId || !row.warehouseLocationId) return '—';
          return `${Number(val || 0).toLocaleString()} ${row.unitOfMeasure || 'PCS'}`;
        }
      });
    }

    if (isReceiptMode) {
      cols.push({
        field: 'unitCost',
        header: `Unit Cost (${this.activeCurrency()})`,
        editable: true,
        editType: 'currency',
        type: 'currency',
        align: 'right',
        width: '140px',
        sortable: false,
        filterable: false,
        validators: [
          (val) => {
            if (val === null || val === undefined || val === '') return null;
            const num = Number(val);
            if (isNaN(num)) return 'Must be a valid number';
            if (num < 0) return 'Unit cost cannot be negative';
            return null;
          }
        ]
      });
    }

    cols.push(
      {
        field: 'quantity',
        header: isReceiptMode ? 'Receipt Qty *' : 'Issue Qty *',
        editable: true,
        editType: 'number',
        type: 'number',
        align: 'right',
        width: '120px',
        sortable: false,
        filterable: false,
        validators: [
          (val, row) => {
            const num = Number(val);
            if (isNaN(num)) return 'Must be a valid number';
            if (num <= 0) return 'Quantity must be > 0';
            if (isIssueMode && num > row.availableStock) {
              return `Exceeds available stock (${row.availableStock})`;
            }
            return null;
          }
        ]
      },
      {
        field: 'unitOfMeasure',
        header: 'UOM',
        editable: false,
        align: 'center',
        width: '80px',
        sortable: false,
        filterable: false
      }
    );

    return cols;
  });

  findLocation(locationId: string): WarehouseLocationItem | undefined {
    if (!locationId) return undefined;
    const map = this.locationsMap();
    for (const list of Object.values(map)) {
      const found = list.find(l => l.locationId === locationId);
      if (found) return found;
    }
    return undefined;
  }

  getLocationOptionsForRow(row: StockTransactionLineItem): SelectOption[] {
    const targetWh = row.warehouseId || this.headerWarehouseId();
    const locs = this.getLocationsForWarehouse(targetWh);
    if (!locs || locs.length === 0) {
      return [{ label: '-- No Storage Locations Available --', value: '' }];
    }
    return locs.map(l => ({
      label: this.formatLocationHierarchy(l),
      value: l.locationId
    }));
  }

  onLinesDataChange(updated: StockTransactionLineItem[]): void {
    this.lines.set([...updated]);
  }

  onTableCellValueChange(event: { row: StockTransactionLineItem; column: DataTableColumn<StockTransactionLineItem>; value: any }): void {
    const { row, column, value } = event;
    const field = String(column.field);

    if (field === 'warehouseId') {
      this.onLineWarehouseChange(row, value);
    } else if (field === 'productId' || field === 'productCode') {
      this.onProductChange(row, value);
    } else if (field === 'warehouseLocationId') {
      this.onLocationChange(row, value);
    } else if (field === 'quantity') {
      this.onQuantityChange(row, value);
    } else if (field === 'unitCost') {
      this.onUnitCostChange(row, value);
    }

    this.lines.update(curr => curr.map(l => l.id === row.id ? { ...l, ...row } : l));
  }

  onTableRowDelete(deleted: StockTransactionLineItem): void {
    const remaining = this.lines().filter(l => l.id !== deleted.id);
    if (remaining.length === 0) {
      this.lines.set([this.defaultNewRow() as StockTransactionLineItem]);
    } else {
      this.lines.set(remaining);
    }
  }

  canPost(): boolean {
    if (this.authService.isTenantAdmin() || this.authService.isPlatformAdmin()) {
      return true;
    }
    return this.authService.hasPermission('PRODUCT.EDIT');
  }

  ngOnInit(): void {
    if (this.mode === 'view' && this.recordId) {
      this.loadRecordDetails(this.recordId);
    } else {
      this.loadMasterData();
    }
  }

  ngOnDestroy(): void {
    this.subs.unsubscribe();
  }

  loadMasterData(): void {
    this.loading.set(true);

    this.masterDataService.getWarehouses().subscribe({
      next: whs => {
        const active = (whs || []).filter(w => w.isActive);
        this.warehouses.set(active);

        if (active.length > 0 && !this.headerWarehouseId()) {
          this.onHeaderWarehouseChange(active[0].warehouseId);
        }
        this.loading.set(false);
      },
      error: err => {
        this.loading.set(false);
        this.error.set(err?.message || 'Failed to load warehouses.');
      }
    });

    this.masterDataService.getProducts().subscribe({
      next: prods => {
        const stockTracked = (prods || []).filter(p => p.isActive && p.isStockTracked);
        this.products.set(stockTracked);
      },
      error: () => {}
    });
  }

  onHeaderWarehouseChange(warehouseId: string): void {
    this.headerWarehouseId.set(warehouseId);
    if (!warehouseId) return;

    this.ensureLocationsLoaded(warehouseId, () => {
      const locs = this.getLocationsForWarehouse(warehouseId);
      const defaultLocId = locs.length > 0 ? locs[0].locationId : '';

      // If no lines yet, initialize first line
      if (this.lines().length === 0) {
        this.addLine(warehouseId, defaultLocId);
      } else {
        // Update lines that were set to previous header warehouse or empty
        this.lines.update(curr =>
          curr.map(l => {
            const wh = l.warehouseId || warehouseId;
            const newLoc: string = l.warehouseLocationId || (wh === warehouseId ? (defaultLocId || '') : '') || '';
            const updated: StockTransactionLineItem = { ...l, warehouseId: wh, warehouseLocationId: newLoc };
            if (this.isIssue() && updated.productId && updated.warehouseLocationId) {
              this.refreshAvailableStock(updated);
            }
            return updated;
          })
        );
      }
    });
  }

  ensureLocationsLoaded(warehouseId: string, callback?: () => void): void {
    if (!warehouseId) return;
    const current = this.locationsMap();
    if (current[warehouseId]) {
      if (callback) callback();
      return;
    }

    this.masterDataService.getWarehouseLocations(warehouseId).subscribe({
      next: locs => {
        const activeLocs = (locs || []).filter(l => l.isActive);
        this.locationsMap.update(map => ({
          ...map,
          [warehouseId]: activeLocs
        }));
        if (callback) callback();
      },
      error: () => {
        this.locationsMap.update(map => ({
          ...map,
          [warehouseId]: []
        }));
        if (callback) callback();
      }
    });
  }

  getLocationsForWarehouse(warehouseId: string): WarehouseLocationItem[] {
    return this.locationsMap()[warehouseId] || [];
  }

  addLine(warehouseId?: string, locationId?: string): void {
    const targetWh = warehouseId || this.headerWarehouseId() || (this.warehouses().length > 0 ? this.warehouses()[0].warehouseId : '');
    const locs = this.getLocationsForWarehouse(targetWh);
    const targetLoc = locationId || (locs.length > 0 ? locs[0].locationId : '');

    const newLine: StockTransactionLineItem = {
      id: `line-${Date.now()}-${Math.random().toString(36).substring(2, 6)}`,
      productId: '',
      productCode: '',
      productName: '',
      unitOfMeasure: 'PCS',
      warehouseId: targetWh,
      warehouseLocationId: targetLoc || '',
      availableStock: 0,
      loadingStock: false,
      quantity: 1,
      unitCost: null
    };

    this.lines.update(curr => [...curr, newLine]);

    if (targetWh && !this.locationsMap()[targetWh]) {
      this.ensureLocationsLoaded(targetWh, () => {
        const updatedLocs = this.getLocationsForWarehouse(targetWh);
        if (updatedLocs.length > 0 && !newLine.warehouseLocationId) {
          newLine.warehouseLocationId = updatedLocs[0].locationId || updatedLocs[0].warehouseLocationId || '';
          this.lines.update(c => [...c]);
        }
      });
    }
  }

  removeLine(index: number): void {
    this.lines.update(curr => curr.filter((_, i) => i !== index));
    if (this.lines().length === 0) {
      this.addLine();
    }
  }

  onLineWarehouseChange(line: StockTransactionLineItem, warehouseId: string): void {
    line.warehouseId = warehouseId;
    line.warehouseLocationId = '';
    line.availableStock = 0;

    this.ensureLocationsLoaded(warehouseId, () => {
      const locs = this.getLocationsForWarehouse(warehouseId);
      if (locs.length > 0) {
        line.warehouseLocationId = locs[0].locationId || locs[0].warehouseLocationId || '';
        if (this.isIssue() && line.productId) {
          this.refreshAvailableStock(line);
        }
      }
      this.lines.update(curr => [...curr]);
    });
  }

  onProductChange(line: StockTransactionLineItem, productIdOrCode: string): void {
    const product = this.products().find(p => p.productId === productIdOrCode || p.productCode === productIdOrCode);
    line.productId = product?.productId || productIdOrCode;
    line.productCode = product?.productCode || '';
    line.productName = product?.productName || '';
    line.unitOfMeasure = product?.unitOfMeasure || 'PCS';

    if (this.isReceipt()) {
      if (line.unitCost === null || line.unitCost === undefined || line.unitCost === 0) {
        line.unitCost = product?.standardCost ?? 0;
      }
    }

    if (this.isIssue()) {
      this.refreshAvailableStock(line);
    }
  }

  onLocationChange(line: StockTransactionLineItem, locationId: string): void {
    line.warehouseLocationId = locationId;
    if (this.isIssue()) {
      this.refreshAvailableStock(line);
    }
  }

  onQuantityChange(line: StockTransactionLineItem, qty: number): void {
    line.quantity = Number(qty);
    this.lines.update(curr => [...curr]);
  }

  onUnitCostChange(line: StockTransactionLineItem, cost: number): void {
    line.unitCost = cost !== null && cost !== undefined ? Number(cost) : null;
    this.lines.update(curr => [...curr]);
  }

  refreshAvailableStock(line: StockTransactionLineItem): void {
    if (!line.productId || !line.warehouseLocationId) {
      line.availableStock = 0;
      return;
    }

    line.loadingStock = true;
    this.inventoryService
      .getAvailableStock(line.productId, line.warehouseLocationId, line.batchNumber, line.serialNumber)
      .subscribe({
        next: stock => {
          line.availableStock = stock?.quantityAvailable ?? 0;
          line.loadingStock = false;
          this.lines.update(curr => [...curr]);
        },
        error: () => {
          line.availableStock = 0;
          line.loadingStock = false;
          this.lines.update(curr => [...curr]);
        }
      });
  }

  formatLocationHierarchy(loc: WarehouseLocationItem): string {
    if (!loc) return '';
    // Format Rack → Level → Bin hierarchy if indicated in name or code
    if (loc.locationName && (loc.locationName.includes('>') || loc.locationName.includes('→'))) {
      return `${loc.locationCode} • ${loc.locationName.replace(/>/g, '→')}`;
    }

    const segments = loc.locationCode.split('-');
    if (segments.length >= 3) {
      return `${loc.locationCode} (Rack ${segments[0]} → Level ${segments[1]} → Bin ${segments[2]})`;
    }

    return `${loc.locationCode} — ${loc.locationName}`;
  }

  loadRecordDetails(id: string): void {
    this.loading.set(true);
    this.error.set(null);

    if (this.isReceipt()) {
      this.inventoryService.getStockReceipt(id).subscribe({
        next: receipt => {
          this.loading.set(false);
          if (!receipt) {
            this.error.set('Stock receipt record not found.');
          } else {
            this.viewReceipt.set(receipt);
          }
        },
        error: err => {
          this.loading.set(false);
          if (err?.status === 403) {
            this.isAccessDenied.set(true);
          } else {
            this.error.set(err?.message || 'Failed to load stock receipt details.');
          }
        }
      });
    } else {
      this.inventoryService.getStockIssue(id).subscribe({
        next: issue => {
          this.loading.set(false);
          if (!issue) {
            this.error.set('Stock issue record not found.');
          } else {
            this.viewIssue.set(issue);
          }
        },
        error: err => {
          this.loading.set(false);
          if (err?.status === 403) {
            this.isAccessDenied.set(true);
          } else {
            this.error.set(err?.message || 'Failed to load stock issue details.');
          }
        }
      });
    }
  }

  postTransaction(): void {
    if (!this.isValid() || this.saving()) return;

    this.saving.set(true);
    this.error.set(null);

    if (this.isReceipt()) {
      const payload: CreateStockReceiptRequest = {
        receiptDate: this.transactionDate(),
        reason: this.selectedReceiptReason(),
        warehouseId: this.headerWarehouseId(),
        sourceDocumentReference: this.sourceDocumentReference() || null,
        remarks: this.remarks() || null,
        lines: this.lines().map(l => ({
          productId: l.productId,
          warehouseLocationId: l.warehouseLocationId,
          quantity: l.quantity,
          unitCost: l.unitCost ?? null,
          batchNumber: l.batchNumber || null,
          serialNumber: l.serialNumber || null,
          remarks: l.remarks || null
        }))
      };

      this.inventoryService.createStockReceipt(payload).subscribe({
        next: results => {
          this.saving.set(false);
          this.transactionPosted.emit(results);
          if (results && results.length > 0) {
            this.router.navigate(['/app/inventory/stock-receipts', results[0].stockReceiptId]);
          } else {
            this.router.navigate(['/app/inventory/stock-receipts']);
          }
        },
        error: err => {
          this.saving.set(false);
          if (err?.status === 403) {
            this.isAccessDenied.set(true);
          } else {
            this.error.set(err?.error?.message || err?.message || 'Failed to post stock receipt.');
          }
        }
      });
    } else {
      const payload: CreateStockIssueRequest = {
        issueDate: this.transactionDate(),
        purpose: this.selectedIssuePurpose(),
        warehouseId: this.headerWarehouseId(),
        sourceDocumentReference: this.sourceDocumentReference() || null,
        remarks: this.remarks() || null,
        lines: this.lines().map(l => ({
          productId: l.productId,
          warehouseLocationId: l.warehouseLocationId,
          quantity: l.quantity,
          batchNumber: l.batchNumber || null,
          serialNumber: l.serialNumber || null,
          remarks: l.remarks || null
        }))
      };

      this.inventoryService.createStockIssue(payload).subscribe({
        next: results => {
          this.saving.set(false);
          this.transactionPosted.emit(results);
          if (results && results.length > 0) {
            this.router.navigate(['/app/inventory/issue-stock', results[0].stockIssueId]);
          } else {
            this.router.navigate(['/app/inventory/issue-stock']);
          }
        },
        error: err => {
          this.saving.set(false);
          if (err?.status === 403) {
            this.isAccessDenied.set(true);
          } else {
            this.error.set(err?.error?.message || err?.message || 'Failed to post stock issue.');
          }
        }
      });
    }
  }

  getReceiptReasonName(reason: StockReceiptReason | number): string {
    return getStockReceiptReasonName(reason);
  }

  getIssuePurposeName(purpose: StockIssuePurpose | number): string {
    return getStockIssuePurposeName(purpose);
  }

  onCancel(): void {
    this.cancel.emit();
    if (this.isReceipt()) {
      this.router.navigate(['/app/inventory/stock-receipts']);
    } else {
      this.router.navigate(['/app/inventory/issue-stock']);
    }
  }
}
