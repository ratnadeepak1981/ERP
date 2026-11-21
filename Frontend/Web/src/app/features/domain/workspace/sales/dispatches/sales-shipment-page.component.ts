import {
  Component,
  OnInit,
  AfterViewInit,
  OnDestroy,
  inject,
  signal,
  computed,
  ViewChild,
  TemplateRef,
  ChangeDetectionStrategy
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormGroup, FormControl, Validators } from '@angular/forms';
import { Router, ActivatedRoute, RouterModule } from '@angular/router';
import { Subscription, forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { DialogModule } from 'primeng/dialog';
import { TooltipModule } from 'primeng/tooltip';
import { AppPageHeaderComponent } from '../../../../../shared/components/page-header/page-header.component';
import { AppStatusBadgeComponent } from '../../../../../shared/components/status-badge/status-badge.component';
import { AppLoadingStateComponent } from '../../../../../shared/components/loading-state/loading-state.component';
import { AppErrorStateComponent } from '../../../../../shared/components/error-state/error-state.component';
import { AccessDeniedComponent } from '../../../../../shared/components/access-denied/access-denied.component';
import { AppEditableDataTableComponent } from '../../../../../shared/components/data-table/editable-data-table.component';
import { DataTableColumn, SelectOption } from '../../../../../shared/models/data-table-config.model';
import { SalesShipmentService } from '../../../../../core/services/sales-shipment.service';
import { SalesOrderService } from '../../../../../core/services/sales-order.service';
import { MasterDataService } from '../../../../../core/services/master-data.service';
import { WorkspaceScopeService } from '../../../../../core/services/workspace-scope.service';
import { AuthService } from '../../../../../core/auth/auth.service';
import {
  SalesShipmentDto,
  SalesShipmentItemDto,
  SalesShipmentStatus,
  FulfillmentMethod,
  getSalesShipmentStatusName,
  getSalesShipmentStatusSeverity,
  SalesOrderDispatchAvailabilityDto,
  SalesOrderDispatchLineAvailabilityDto,
  SuggestedLocationSplitDto,
  LocationStockAvailabilityDto,
  WarehouseStockSummaryDto,
  DispatchOrderRequest,
  CreateSalesShipmentItemRequest,
  CreateShipmentFromOrderRequest,
  UpdateSalesShipmentRequest,
  CreateReplenishmentTransferRequest,
  CreateReplenishmentRequisitionRequest,
  EditableShipmentLine
} from '../../../../../core/models/sales-shipment.models';
import { SalesOrderDto } from '../../../../../core/models/sales-order.models';
import { CustomerItem, ProductItem, WarehouseItem, WarehouseLocationItem, TrackingMode } from '../../../../../core/models/scope.models';
import { BatchSerialAllocation } from '../../../../../core/models/goods-receipt-note.models';
import {
  BatchSerialTrackingDialogComponent,
  BatchSerialDialogData
} from '../../procurement/batch-serial-dialog/batch-serial-dialog.component';

@Component({
  selector: 'app-sales-shipment-page',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    RouterModule,
    ButtonModule,
    InputTextModule,
    DialogModule,
    TooltipModule,
    AppPageHeaderComponent,
    AppStatusBadgeComponent,
    AppLoadingStateComponent,
    AppErrorStateComponent,
    AccessDeniedComponent,
    AppEditableDataTableComponent,
    MatDialogModule
  ],
  templateUrl: './sales-shipment-page.component.html',
  styleUrl: './sales-shipment-page.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class SalesShipmentPageComponent implements OnInit, AfterViewInit, OnDestroy {
  // Template references for custom cell rendering in AppEditableDataTable
  readonly trackingTemplate = signal<TemplateRef<any> | null>(null);
  @ViewChild('trackingCellTemplate', { static: true })
  set trackingCellTemplateRef(tmpl: TemplateRef<any> | undefined) {
    if (tmpl) {
      this.trackingCellTemplate = tmpl;
      this.trackingTemplate.set(tmpl);
    }
  }
  trackingCellTemplate?: TemplateRef<any>;

  readonly availableStockTemplateSignal = signal<TemplateRef<any> | null>(null);
  @ViewChild('availableStockTemplate', { static: true })
  set availableStockTemplateRef(tmpl: TemplateRef<any> | undefined) {
    if (tmpl) {
      this.availableStockTemplate = tmpl;
      this.availableStockTemplateSignal.set(tmpl);
    }
  }
  availableStockTemplate?: TemplateRef<any>;

  readonly splitActionTemplateSignal = signal<TemplateRef<any> | null>(null);
  @ViewChild('splitActionTemplate', { static: true })
  set splitActionTemplateRef(tmpl: TemplateRef<any> | undefined) {
    if (tmpl) {
      this.splitActionTemplate = tmpl;
      this.splitActionTemplateSignal.set(tmpl);
    }
  }
  splitActionTemplate?: TemplateRef<any>;

  // Injected services
  readonly getSalesShipmentStatusName = getSalesShipmentStatusName;
  readonly getSalesShipmentStatusSeverity = getSalesShipmentStatusSeverity;
  readonly shipmentService = inject(SalesShipmentService);
  readonly salesOrderService = inject(SalesOrderService);
  readonly masterDataService = inject(MasterDataService);
  readonly scopeService = inject(WorkspaceScopeService);
  readonly authService = inject(AuthService);
  readonly router = inject(Router);
  readonly route = inject(ActivatedRoute);
  private readonly matDialog = inject(MatDialog);

  private readonly subs = new Subscription();

  // Mode & Route State
  readonly mode = signal<'create' | 'view' | 'edit'>('create');
  readonly shipmentId = signal<string | null>(null);
  readonly sourceOrderId = signal<string | null>(null);
  readonly currentShipment = signal<SalesShipmentDto | null>(null);
  readonly sourceOrder = signal<SalesOrderDto | null>(null);

  // Live Availability & Shortage State
  readonly availability = signal<SalesOrderDispatchAvailabilityDto | null>(null);
  readonly revalidatingStock = signal<boolean>(false);
  readonly showAllShortages = signal<boolean>(false);

  // Loading & Action States
  readonly loading = signal<boolean>(false);
  readonly actionInProgress = signal<boolean>(false);
  readonly error = signal<string | null>(null);
  readonly isAccessDenied = signal<boolean>(false);
  readonly message = signal<{ text: string; severity: 'success' | 'info' | 'warn' | 'danger' } | null>(null);

  // Master Data & Location Cache
  readonly customers = signal<CustomerItem[]>([]);
  readonly products = signal<ProductItem[]>([]);
  readonly warehouses = signal<WarehouseItem[]>([]);
  readonly locationsMap = signal<Record<string, WarehouseLocationItem[]>>({});
  readonly defaultWarehouseId = signal<string>('');

  // Working allocation lines in editable data table
  readonly workingLines = signal<EditableShipmentLine[]>([]);

  // Replenishment Dialog States
  readonly transferDialogVisible = signal<boolean>(false);
  readonly selectedTransferLine = signal<SalesOrderDispatchLineAvailabilityDto | null>(null);
  readonly transferForm = new FormGroup({
    sourceWarehouseId: new FormControl<string>('', { nonNullable: true, validators: [Validators.required] }),
    destinationWarehouseId: new FormControl<string>('', { nonNullable: true, validators: [Validators.required] }),
    quantity: new FormControl<number>(1, { nonNullable: true, validators: [Validators.required, Validators.min(0.01)] }),
    notes: new FormControl<string>('')
  });

  readonly requisitionDialogVisible = signal<boolean>(false);
  readonly selectedRequisitionLine = signal<SalesOrderDispatchLineAvailabilityDto | null>(null);
  readonly requisitionForm = new FormGroup({
    requiredDate: new FormControl<string>(new Date(Date.now() + 3 * 86400000).toISOString().substring(0, 10), {
      nonNullable: true,
      validators: [Validators.required]
    }),
    quantity: new FormControl<number>(1, { nonNullable: true, validators: [Validators.required, Validators.min(0.01)] }),
    estimatedUnitPrice: new FormControl<number | null>(null),
    notes: new FormControl<string>('')
  });

  // Fulfillment Method Options
  readonly methodOptions = [
    { label: 'Delivery', value: FulfillmentMethod.Delivery },
    { label: 'Customer Pickup', value: FulfillmentMethod.CustomerPickup },
    { label: 'Store Handover', value: FulfillmentMethod.StoreHandover }
  ];

  // Header Form
  readonly headerForm = new FormGroup({
    shipmentNumber: new FormControl<string>('', { nonNullable: true, validators: [Validators.required] }),
    orderNumber: new FormControl<string>({ value: '', disabled: true }),
    customerName: new FormControl<string>({ value: '', disabled: true }),
    method: new FormControl<number>(FulfillmentMethod.Delivery, { nonNullable: true, validators: [Validators.required] }),
    shipmentDate: new FormControl<string>(new Date().toISOString().substring(0, 10), {
      nonNullable: true,
      validators: [Validators.required]
    }),
    carrierName: new FormControl<string>(''),
    trackingNumber: new FormControl<string>(''),
    recipientName: new FormControl<string>(''),
    deliveryAddress: new FormControl<string>(''),
    vehicleNumber: new FormControl<string>(''),
    notes: new FormControl<string>('')
  });

  // Warehouse select options for editable table
  readonly warehouseOptions = computed<SelectOption[]>(() => {
    return this.warehouses().map(w => ({
      label: `${w.warehouseCode} - ${w.warehouseName}`,
      value: w.warehouseId
    }));
  });

  // Status & Permission Computations
  readonly isDraft = computed<boolean>(() => {
    const s = this.currentShipment();
    if (!s) return this.mode() === 'create';
    return Number(s.status) === SalesShipmentStatus.Draft;
  });

  readonly isDelivered = computed<boolean>(() => {
    const s = this.currentShipment();
    return s ? Number(s.status) === SalesShipmentStatus.Delivered : false;
  });

  readonly isDispatched = computed<boolean>(() => {
    const s = this.currentShipment();
    return s ? Number(s.status) === SalesShipmentStatus.Dispatched : false;
  });

  readonly canEditDraft = computed<boolean>(() => {
    return this.mode() === 'create' || this.isDraft();
  });

  readonly canDeliver = computed<boolean>(() => {
    return (
      (this.authService.hasPermission('SALES_SHIPMENT.DELIVER') ||
        this.authService.isTenantAdmin() ||
        this.authService.isPlatformAdmin()) &&
      this.isDispatched()
    );
  });

  readonly canDispatchShipment = computed<boolean>(() => {
    return (
      (this.authService.hasPermission('SALES_SHIPMENT.DISPATCH') ||
        this.authService.isTenantAdmin() ||
        this.authService.isPlatformAdmin()) &&
      this.canEditDraft() &&
      !this.isOrderFullyDispatched()
    );
  });

  readonly isOrderFullyDispatched = computed<boolean>(() => {
    const avail = this.availability();
    if (!avail?.lines || avail.lines.length === 0) return false;
    return avail.lines.every(l => Number(l.remainingQuantity) <= 0);
  });

  // Shortage & Line Computations
  readonly shortageCount = computed<number>(() => {
    const avail = this.availability();
    if (!avail?.lines) return 0;
    return avail.lines.filter(l => l.shortageQuantity > 0 || !l.canFulfil).length;
  });

  readonly totalShortageQuantity = computed<number>(() => {
    const avail = this.availability();
    if (!avail?.lines) return 0;
    return avail.lines.reduce((sum, l) => sum + (Number(l.shortageQuantity) || 0), 0);
  });

  readonly totalPickedQuantity = computed<number>(() => {
    return this.workingLines().reduce((sum, l) => sum + (Number(l.quantityDispatched) || 0), 0);
  });

  readonly totalOutstandingDemand = computed<number>(() => {
    // Unique order lines total outstanding
    const seen = new Set<string>();
    let total = 0;
    for (const l of this.workingLines()) {
      if (!seen.has(l.salesOrderItemId)) {
        seen.add(l.salesOrderItemId);
        total += Number(l.outstandingQuantity) || 0;
      }
    }
    return total;
  });

  // Breadcrumbs
  readonly breadcrumbs = computed(() => {
    const num = this.currentShipment()?.shipmentNumber || this.headerForm.get('shipmentNumber')?.value || 'New Dispatch';
    return [
      { label: 'Workspace', routerLink: '/app/dashboard' },
      { label: 'Sales', routerLink: '/app/sales' },
      { label: 'Dispatch Notes', routerLink: '/app/sales/dispatches' },
      { label: this.mode() === 'create' ? 'New Dispatch Note' : num }
    ];
  });

  // Editable Data Table Columns Definition
  readonly itemColumns = computed<DataTableColumn<EditableShipmentLine>[]>(() => {
    const isEditable = this.canEditDraft();
    const cellTmpl = this.trackingTemplate() || this.trackingCellTemplate;
    const stockTmpl = this.availableStockTemplateSignal() || this.availableStockTemplate;
    const actionTmpl = this.splitActionTemplateSignal() || this.splitActionTemplate;

    return [
      {
        field: 'productCode',
        header: 'Item Code',
        sortable: true,
        width: '130px',
        editable: false
      },
      {
        field: 'productName',
        header: 'Product Description',
        sortable: true,
        width: '210px',
        editable: false
      },
      {
        field: 'unitOfMeasure',
        header: 'UOM',
        width: '80px',
        align: 'center',
        editable: false
      },
      {
        field: 'orderedQuantity',
        header: 'Ordered',
        sortable: true,
        align: 'right',
        width: '100px',
        editable: false,
        format: val => Number(val || 0).toFixed(2)
      },
      {
        field: 'alreadyDispatchedQuantity',
        header: 'Prev. Sent',
        sortable: true,
        align: 'right',
        width: '105px',
        editable: false,
        format: val => Number(val || 0).toFixed(2)
      },
      {
        field: 'outstandingQuantity',
        header: 'Outstanding',
        sortable: true,
        align: 'right',
        width: '110px',
        editable: false,
        format: val => Number(val || 0).toFixed(2)
      },
      {
        field: 'warehouseId',
        header: 'Warehouse Facility *',
        sortable: false,
        width: '200px',
        editable: isEditable,
        editType: 'select',
        getEditOptions: () => this.warehouseOptions(),
        editOptions: this.warehouseOptions(),
        format: val => {
          const w = this.warehouses().find(x => x.warehouseId === val);
          return w ? `${w.warehouseCode} - ${w.warehouseName}` : (val || '—');
        },
        validators: [
          (val, row) => (row && row.quantityDispatched > 0 && !val ? 'Warehouse is required' : null)
        ]
      },
      {
        field: 'warehouseLocationId',
        header: 'Picking Bin / Location *',
        sortable: false,
        width: '240px',
        editable: isEditable,
        editType: 'select',
        getEditOptions: row => this.getLocationOptionsForRow(row),
        format: val => {
          const loc = this.findLocation(val);
          return loc ? this.formatLocationHierarchy(loc) : (val || '—');
        },
        validators: [
          (val, row) => (row && row.quantityDispatched > 0 && !val ? 'Location is required' : null)
        ]
      },
      {
        field: 'availableQuantity',
        header: 'Stock at Bin',
        sortable: false,
        width: '115px',
        align: 'right',
        editable: false,
        cellTemplate: stockTmpl
      },
      {
        field: 'quantityDispatched',
        header: 'Picking Qty *',
        sortable: true,
        align: 'right',
        width: '125px',
        editable: isEditable,
        editType: 'number',
        validators: [
          (val: any, row?: EditableShipmentLine) => {
            const num = Number(val);
            if (isNaN(num)) return 'Must be a number';
            if (num <= 0) return 'Must be greater than 0';
            if (!row) return null;
            const siblingRows = this.workingLines().filter(
              l => l.salesOrderItemId === row.salesOrderItemId && l.lineId !== row.lineId
            );
            const siblingTotal = siblingRows.reduce((sum, l) => sum + (Number(l.quantityDispatched) || 0), 0);
            const totalAllocated = siblingTotal + num;
            if (totalAllocated > row.outstandingQuantity + 0.0001) {
              return `Exceeds outstanding (${row.outstandingQuantity.toFixed(2)})`;
            }
            return null;
          }
        ]
      },
      {
        field: 'trackingMode',
        header: 'Tracking Details',
        width: '175px',
        align: 'center',
        editable: false,
        cellTemplate: cellTmpl
      },
      {
        field: 'actions',
        header: 'Allocation Actions',
        width: '150px',
        align: 'center',
        editable: false,
        cellTemplate: actionTmpl
      }
    ];
  });

  ngOnInit(): void {
    this.loadMasterData();
    this.detectRoute();
  }

  ngAfterViewInit(): void {
    if (this.trackingCellTemplate && !this.trackingTemplate()) {
      this.trackingTemplate.set(this.trackingCellTemplate);
    }
    if (this.availableStockTemplate && !this.availableStockTemplateSignal()) {
      this.availableStockTemplateSignal.set(this.availableStockTemplate);
    }
    if (this.splitActionTemplate && !this.splitActionTemplateSignal()) {
      this.splitActionTemplateSignal.set(this.splitActionTemplate);
    }
  }

  ngOnDestroy(): void {
    this.subs.unsubscribe();
  }

  private detectRoute(): void {
    const id = this.route.snapshot.paramMap.get('id');
    const orderIdQuery = this.route.snapshot.queryParamMap.get('orderId');

    if (id) {
      this.shipmentId.set(id);
      const url = this.router.url;
      this.mode.set(url.endsWith('/edit') ? 'edit' : 'view');
      this.loadShipment(id);
    } else {
      this.mode.set('create');
      this.shipmentId.set(null);
      if (orderIdQuery) {
        this.sourceOrderId.set(orderIdQuery);
        this.initFromOrder(orderIdQuery);
      } else {
        this.initManualDraft();
      }
    }
  }

  loadMasterData(): void {
    forkJoin({
      customers: this.masterDataService.getCustomers().pipe(catchError(() => of([] as CustomerItem[]))),
      products: this.masterDataService.getProducts().pipe(catchError(() => of([] as ProductItem[]))),
      warehouses: this.masterDataService.getWarehouses().pipe(catchError(() => of([] as WarehouseItem[])))
    }).subscribe({
      next: ({ customers, products, warehouses }) => {
        this.customers.set(customers || []);
        this.products.set(products || []);
        const whList = warehouses || [];
        this.warehouses.set(whList);

        if (whList.length > 0) {
          const locObservables = whList.map(wh =>
            this.masterDataService
              .getWarehouseLocations(wh.warehouseId)
              .pipe(catchError(() => of([] as WarehouseLocationItem[])))
          );
          forkJoin(locObservables).subscribe({
            next: locArrays => {
              const newMap: Record<string, WarehouseLocationItem[]> = {};
              whList.forEach((wh, idx) => {
                newMap[wh.warehouseId] = locArrays[idx] || [];
              });
              this.locationsMap.set(newMap);
              this.refreshAvailableQuantities();
            }
          });
        }
      }
    });
  }

  ensureLocationsLoaded(warehouseId: string, callback?: () => void): void {
    if (!warehouseId) {
      if (callback) callback();
      return;
    }
    if (this.locationsMap()[warehouseId]) {
      if (callback) callback();
      return;
    }

    this.masterDataService.getWarehouseLocations(warehouseId).subscribe({
      next: locs => {
        this.locationsMap.update(curr => ({
          ...curr,
          [warehouseId]: locs || []
        }));
        if (callback) callback();
      },
      error: () => {
        if (callback) callback();
      }
    });
  }

  getLocationsForWarehouse(warehouseId: string): WarehouseLocationItem[] {
    return this.locationsMap()[warehouseId] || [];
  }

  getLocationOptionsForRow(row: EditableShipmentLine): SelectOption[] {
    const targetWh = row.warehouseId;
    if (!targetWh) {
      return [{ label: '-- Select Warehouse Facility First --', value: '' }];
    }
    const locs = this.getLocationsForWarehouse(targetWh);
    if (!locs || locs.length === 0) {
      this.ensureLocationsLoaded(targetWh);
      return [{ label: '-- No Storage Locations Available --', value: '' }];
    }
    return locs.map(l => ({
      label: this.formatLocationHierarchy(l),
      value: l.warehouseLocationId ?? l.locationId
    }));
  }

  onDraftLinesChange(lines: EditableShipmentLine[]): void {
    const current = this.workingLines();
    const currentMap = new Map<string, EditableShipmentLine>();
    for (const l of current) {
      const key = l.lineId || l.salesOrderItemId;
      if (key) currentMap.set(key, l);
    }

    const merged = lines.map(incoming => {
      const key = incoming.lineId || incoming.salesOrderItemId;
      const existing = key ? currentMap.get(key) : undefined;
      return {
        ...existing,
        ...incoming,
        trackingAllocations: existing?.trackingAllocations ?? incoming.trackingAllocations
      };
    });

    this.workingLines.set(merged);
  }

  findLocation(locationId: string): WarehouseLocationItem | undefined {
    if (!locationId) return undefined;
    const map = this.locationsMap();
    for (const list of Object.values(map)) {
      const found = list.find(l => (l.warehouseLocationId ?? l.locationId) === locationId);
      if (found) return found;
    }
    return undefined;
  }

  formatLocationHierarchy(loc: WarehouseLocationItem): string {
    if (!loc) return '';
    if (loc.locationName && (loc.locationName.includes('>') || loc.locationName.includes('→'))) {
      return `${loc.locationCode} • ${loc.locationName.replace(/>/g, '→')}`;
    }
    const segments = loc.locationCode.split('-');
    if (segments.length >= 3) {
      return `${loc.locationCode} (Rack ${segments[0]} → Level ${segments[1]} → Bin ${segments[2]})`;
    }
    return `${loc.locationCode} — ${loc.locationName}`;
  }

  getAvailableQuantityForRow(row: EditableShipmentLine): number {
    if (!row.warehouseId || !row.warehouseLocationId) return 0;
    const avail = this.availability();
    if (!avail) return row.availableQuantity ?? 0;
    const line = avail.lines.find(l => l.salesOrderItemId === row.salesOrderItemId);
    if (!line || !line.eligibleLocations) return 0;
    const match = line.eligibleLocations.find(
      loc => loc.warehouseId === row.warehouseId && loc.warehouseLocationId === row.warehouseLocationId
    );
    return match ? match.availableQuantity : 0;
  }

  private refreshAvailableQuantities(): void {
    this.workingLines.update(curr =>
      curr.map(l => ({
        ...l,
        availableQuantity: this.getAvailableQuantityForRow(l)
      }))
    );
  }

  private initFromOrder(orderId: string): void {
    const comp = this.scopeService.selectedCompany();
    const branch = this.scopeService.selectedBranch();
    if (!comp?.id || !branch?.id) return;

    this.loading.set(true);
    forkJoin({
      order: this.salesOrderService.getOrder(comp.id, branch.id, orderId).pipe(catchError(() => of(null))),
      avail: this.shipmentService.getOrderDispatchAvailability(comp.id, branch.id, orderId).pipe(catchError(() => of(null))),
      drafts: this.shipmentService.getShipments(comp.id, branch.id, SalesShipmentStatus.Draft).pipe(catchError(() => of([] as SalesShipmentDto[])))
    }).subscribe({
      next: ({ order, avail, drafts }) => {
        this.loading.set(false);
        if (!order) {
          this.error.set('Associated sales order not found.');
          return;
        }

        // If an open draft already exists for this order, redirect to it to prevent duplicate orphan drafts
        const existingDraft = (drafts || []).find(d => d.salesOrderId === orderId);
        if (existingDraft) {
          this.showMessage(
            `An open draft dispatch note (${existingDraft.shipmentNumber}) already exists for Sales Order ${order.orderNumber}. Loading existing draft.`,
            'info'
          );
          this.router.navigate(['/app/sales/dispatches', existingDraft.id, 'edit'], { replaceUrl: true });
          return;
        }

        this.sourceOrder.set(order);
        this.availability.set(avail);

        const rand = Math.floor(1000 + Math.random() * 9000);
        const year = new Date().getFullYear();
        this.headerForm.patchValue({
          shipmentNumber: `DSP-${year}-${rand}`,
          orderNumber: order.orderNumber,
          customerName: order.customerName,
          method: FulfillmentMethod.Delivery,
          shipmentDate: new Date().toISOString().substring(0, 10),
          deliveryAddress: order.deliveryAddressSnapshot || '',
          notes: `Created for Sales Order ${order.orderNumber}`
        });

        // Initialize picking lines from availability
        const lines: EditableShipmentLine[] = [];
        const availLines = avail?.lines || [];

        for (const al of availLines) {
          const prods = this.products();
          const p = prods.find(x => x.productId === al.productId || x.productCode === al.productCode);
          const trkMode = p ? Number(p.trackingMode) || TrackingMode.None : Number(al.trackingMode) || TrackingMode.None;

          // Default suggested source
          const whId = al.defaultSuggestedWarehouseId || avail?.defaultWarehouseId || (this.warehouses()[0]?.warehouseId || '');
          const locId = al.defaultSuggestedLocationId || avail?.defaultLocationId || '';

          if (whId) {
            this.ensureLocationsLoaded(whId);
          }

          const targetQty = Number(al.remainingQuantity) || 0;
          lines.push({
            lineId: `line-${al.salesOrderItemId}`,
            salesOrderItemId: al.salesOrderItemId,
            productId: al.productId,
            productCode: al.productCode,
            productName: al.productName,
            unitOfMeasureId: al.unitOfMeasureId,
            unitOfMeasure: al.unitOfMeasure || (p?.unitOfMeasure || 'PCS'),
            orderedQuantity: Number(al.orderedQuantity) || 0,
            alreadyDispatchedQuantity: Number(al.alreadyDispatchedQuantity) || 0,
            outstandingQuantity: targetQty,
            quantityDispatched: targetQty,
            warehouseId: whId,
            warehouseLocationId: locId,
            availableQuantity: al.availableQuantity || 0,
            isSuggestedSource: true,
            trackingMode: trkMode,
            trackingAllocations: []
          });
        }

        this.workingLines.set(lines);
        this.refreshAvailableQuantities();
      },
      error: err => {
        this.loading.set(false);
        this.error.set(err?.message || 'Failed to initialize dispatch picking availability.');
      }
    });
  }

  loadShipment(id: string): void {
    const comp = this.scopeService.selectedCompany();
    const branch = this.scopeService.selectedBranch();
    if (!comp?.id) return;

    this.loading.set(true);
    this.shipmentService.getShipment(comp.id, branch?.id || null, id).subscribe({
      next: (shipment: SalesShipmentDto | null) => {
        this.loading.set(false);
        if (!shipment) {
          this.error.set('Dispatch note not found in current scope.');
          return;
        }

        this.currentShipment.set(shipment);
        this.headerForm.patchValue({
          shipmentNumber: shipment.shipmentNumber,
          orderNumber: shipment.orderNumber,
          customerName: shipment.customerName,
          method: Number(shipment.method),
          shipmentDate: shipment.shipmentDate ? shipment.shipmentDate.substring(0, 10) : '',
          carrierName: shipment.carrierName || '',
          trackingNumber: shipment.trackingNumber || '',
          recipientName: shipment.recipientName || '',
          deliveryAddress: shipment.deliveryAddress || '',
          vehicleNumber: shipment.vehicleNumber || '',
          notes: shipment.notes || ''
        });

        if (!this.canEditDraft()) {
          this.headerForm.disable();
        }

        // Fetch availability from order in background to power shortage and split recommendations
        if (shipment.salesOrderId && branch?.id) {
          this.shipmentService.getOrderDispatchAvailability(comp.id, branch.id, shipment.salesOrderId).subscribe({
            next: avail => {
              this.availability.set(avail);
              if (avail?.lines) {
                this.workingLines.update(currLines =>
                  currLines.map(line => {
                    const match = avail.lines.find(al => al.salesOrderItemId === line.salesOrderItemId);
                    if (!match) return line;
                    return {
                      ...line,
                      orderedQuantity: Number(match.orderedQuantity) || line.orderedQuantity,
                      alreadyDispatchedQuantity: Number(match.alreadyDispatchedQuantity) || 0,
                      outstandingQuantity: Number(match.remainingQuantity) || 0
                    };
                  })
                );
              }
              this.refreshAvailableQuantities();
            }
          });
        }

        // Populate working lines
        const lines: EditableShipmentLine[] = (shipment.items || []).map(item => {
          const prods = this.products();
          const p = prods.find(x => x.productId === item.productId || x.productCode === item.productCode);
          const trkMode = p ? Number(p.trackingMode) || TrackingMode.None : TrackingMode.None;

          if (item.warehouseId) {
            this.ensureLocationsLoaded(item.warehouseId);
          }

          const allocs: BatchSerialAllocation[] = [];
          if (item.batchNumber || item.serialNumber) {
            allocs.push({
              id: item.id,
              batchNumber: item.batchNumber || undefined,
              serialNumber: item.serialNumber || undefined,
              quantity: item.quantityDispatched
            });
          }

          return {
            lineId: item.id,
            id: item.id,
            salesOrderItemId: item.salesOrderItemId,
            productId: item.productId,
            productCode: item.productCode || '',
            productName: item.productName || '',
            unitOfMeasureId: item.unitOfMeasureId,
            unitOfMeasure: p?.unitOfMeasure || 'PCS',
            orderedQuantity: item.quantityDispatched,
            alreadyDispatchedQuantity: 0,
            outstandingQuantity: item.quantityDispatched,
            quantityDispatched: item.quantityDispatched,
            warehouseId: item.warehouseId,
            warehouseLocationId: item.warehouseLocationId,
            batchNumber: item.batchNumber,
            serialNumber: item.serialNumber,
            notes: item.notes,
            trackingMode: trkMode,
            trackingAllocations: allocs,
            availableQuantity: 0
          };
        });

        this.workingLines.set(lines);
        this.refreshAvailableQuantities();
      },
      error: err => {
        this.loading.set(false);
        this.error.set(err?.message || 'Failed to load dispatch note.');
      }
    });
  }

  private initManualDraft(): void {
    const rand = Math.floor(1000 + Math.random() * 9000);
    const year = new Date().getFullYear();
    this.headerForm.patchValue({
      shipmentNumber: `DSP-${year}-${rand}`,
      orderNumber: '',
      customerName: '',
      method: FulfillmentMethod.Delivery,
      shipmentDate: new Date().toISOString().substring(0, 10)
    });
    this.workingLines.set([]);
  }

  onTableCellValueChange(event: { row: EditableShipmentLine; column: DataTableColumn<EditableShipmentLine>; value: any }): void {
    const { row, column, value } = event;
    const field = String(column.field);

    if (field === 'warehouseId') {
      this.onLineWarehouseChange(row, value);
    } else if (field === 'warehouseLocationId') {
      this.onLineLocationChange(row, value);
    } else if (field === 'quantityDispatched') {
      this.onLineQuantityChange(row, Number(value) || 0);
    }
  }

  onLineWarehouseChange(row: EditableShipmentLine, warehouseId: string): void {
    row.warehouseId = warehouseId;
    row.warehouseLocationId = '';
    row.availableQuantity = 0;

    if (!warehouseId) {
      this.updateWorkingLine(row);
      return;
    }

    this.ensureLocationsLoaded(warehouseId, () => {
      const locs = this.getLocationsForWarehouse(warehouseId);
      if (locs.length > 0) {
        const avail = this.availability();
        const lineAvail = avail?.lines.find(l => l.salesOrderItemId === row.salesOrderItemId);
        const eligibleInWh = lineAvail?.eligibleLocations?.find(loc => loc.warehouseId === warehouseId && loc.availableQuantity > 0);

        row.warehouseLocationId = eligibleInWh?.warehouseLocationId
          ?? (locs[0].warehouseLocationId ?? locs[0].locationId ?? '');
        row.availableQuantity = this.getAvailableQuantityForRow(row);
      }
      this.updateWorkingLine(row);
    });
  }

  onLineLocationChange(row: EditableShipmentLine, locationId: string): void {
    row.warehouseLocationId = locationId;
    row.availableQuantity = this.getAvailableQuantityForRow(row);
    this.updateWorkingLine(row);
  }

  onLineQuantityChange(row: EditableShipmentLine, quantity: number): void {
    row.quantityDispatched = quantity;
    this.updateWorkingLine(row);
  }

  updateWorkingLine(row: EditableShipmentLine): void {
    this.workingLines.update(curr =>
      curr.map(l => l.lineId === row.lineId ? { ...l, ...row } : l)
    );
  }

  // Multi-Bin Split / Add Bin Action
  onAddBinSplit(row: EditableShipmentLine): void {
    if (!this.canEditDraft()) return;

    const linesForThisOrder = this.workingLines().filter(l => l.salesOrderItemId === row.salesOrderItemId);
    const totalAllocated = linesForThisOrder.reduce((sum, l) => sum + (Number(l.quantityDispatched) || 0), 0);
    const remainingShortage = Math.max(0, row.outstandingQuantity - totalAllocated);

    const avail = this.availability();
    const lineAvail = avail?.lines.find(l => l.salesOrderItemId === row.salesOrderItemId);
    const usedLocationIds = new Set(linesForThisOrder.map(l => l.warehouseLocationId));
    const nextEligible = lineAvail?.eligibleLocations?.find(loc => !usedLocationIds.has(loc.warehouseLocationId) && loc.availableQuantity > 0);

    const nextWhId = nextEligible?.warehouseId || row.warehouseId;
    const nextLocId = nextEligible?.warehouseLocationId || '';

    const newRow: EditableShipmentLine = {
      lineId: `split-${row.salesOrderItemId}-${Date.now()}-${Math.floor(Math.random() * 1000)}`,
      salesOrderItemId: row.salesOrderItemId,
      productId: row.productId,
      productCode: row.productCode,
      productName: row.productName,
      unitOfMeasureId: row.unitOfMeasureId,
      unitOfMeasure: row.unitOfMeasure,
      orderedQuantity: row.orderedQuantity,
      alreadyDispatchedQuantity: row.alreadyDispatchedQuantity,
      outstandingQuantity: row.outstandingQuantity,
      quantityDispatched: remainingShortage > 0 ? remainingShortage : 0,
      warehouseId: nextWhId,
      warehouseLocationId: nextLocId,
      availableQuantity: 0,
      isSuggestedSource: false,
      trackingMode: row.trackingMode,
      trackingAllocations: []
    };

    if (nextWhId) {
      this.ensureLocationsLoaded(nextWhId, () => {
        newRow.availableQuantity = this.getAvailableQuantityForRow(newRow);
        this.updateWorkingLine(newRow);
      });
    }

    this.workingLines.update(curr => {
      const idx = curr.findIndex(l => l.lineId === row.lineId);
      if (idx === -1) return [...curr, newRow];
      return [...curr.slice(0, idx + 1), newRow, ...curr.slice(idx + 1)];
    });

    this.showMessage(`Added new allocation bin row for ${row.productCode}.`, 'info');
  }

  onRemoveBinSplit(row: EditableShipmentLine): void {
    if (!this.canEditDraft()) return;

    const count = this.workingLines().filter(l => l.salesOrderItemId === row.salesOrderItemId).length;
    if (count <= 1) {
      this.showMessage('Cannot remove the primary allocation line for this order item.', 'warn');
      return;
    }

    this.workingLines.update(curr => curr.filter(l => l.lineId !== row.lineId));
    this.showMessage(`Removed allocation row for ${row.productCode}.`, 'info');
  }

  canRemoveRow(row: EditableShipmentLine): boolean {
    if (!this.canEditDraft()) return false;
    return this.workingLines().filter(l => l.salesOrderItemId === row.salesOrderItemId).length > 1;
  }

  hasSuggestedSplits(salesOrderItemId: string): boolean {
    const line = this.availability()?.lines.find(l => l.salesOrderItemId === salesOrderItemId);
    return (line?.suggestedSplits?.length || 0) > 1;
  }

  getSuggestedSplits(salesOrderItemId: string): SuggestedLocationSplitDto[] {
    const line = this.availability()?.lines.find(l => l.salesOrderItemId === salesOrderItemId);
    return line?.suggestedSplits || [];
  }

  applySuggestedSplit(salesOrderItemId: string): void {
    if (!this.canEditDraft()) return;
    const splits = this.getSuggestedSplits(salesOrderItemId);
    if (splits.length === 0) return;

    const existingRows = this.workingLines().filter(l => l.salesOrderItemId === salesOrderItemId);
    if (existingRows.length === 0) return;
    const base = existingRows[0];

    const newRows: EditableShipmentLine[] = splits.map((s, idx) => ({
      lineId: idx === 0 ? base.lineId : `split-${salesOrderItemId}-${idx}-${Date.now()}`,
      salesOrderItemId: base.salesOrderItemId,
      productId: base.productId,
      productCode: base.productCode,
      productName: base.productName,
      unitOfMeasureId: base.unitOfMeasureId,
      unitOfMeasure: base.unitOfMeasure,
      orderedQuantity: base.orderedQuantity,
      alreadyDispatchedQuantity: base.alreadyDispatchedQuantity,
      outstandingQuantity: base.outstandingQuantity,
      quantityDispatched: s.suggestedQuantity,
      warehouseId: s.warehouseId,
      warehouseLocationId: s.warehouseLocationId,
      availableQuantity: s.availableQuantity,
      isSuggestedSource: true,
      batchNumber: s.batchNumber || null,
      trackingMode: base.trackingMode,
      trackingAllocations: []
    }));

    for (const r of newRows) {
      if (r.warehouseId) {
        this.ensureLocationsLoaded(r.warehouseId);
      }
    }

    this.workingLines.update(curr => {
      const firstIdx = curr.findIndex(l => l.salesOrderItemId === salesOrderItemId);
      if (firstIdx === -1) return curr;
      const before = curr.slice(0, firstIdx);
      const after = curr.slice(firstIdx).filter(l => l.salesOrderItemId !== salesOrderItemId);
      return [...before, ...newRows, ...after];
    });

    this.showMessage(`Applied ${splits.length}-bin split suggestion for ${base.productCode}.`, 'success');
  }

  // Batch / Serial Tracking Wiring
  openTrackingDialog(row: EditableShipmentLine, event?: Event): void {
    if (event) {
      event.stopPropagation();
      event.preventDefault();
    }

    const mode = Number(row.trackingMode) || TrackingMode.None;
    if (mode === TrackingMode.None) return;

    const targetQty = Number(row.quantityDispatched) || 0;
    if (targetQty <= 0) {
      this.showMessage('Please enter a picking quantity before configuring tracking.', 'warn');
      return;
    }

    if (!row.warehouseId || !row.warehouseLocationId) {
      this.showMessage('Please select warehouse facility and location bin before tracking.', 'warn');
      return;
    }

    const dialogRef = this.matDialog.open<BatchSerialTrackingDialogComponent, BatchSerialDialogData, BatchSerialAllocation[]>(
      BatchSerialTrackingDialogComponent,
      {
        width: '920px',
        maxWidth: '96vw',
        disableClose: true,
        data: {
          productCode: row.productCode,
          productName: row.productName,
          unitOfMeasure: row.unitOfMeasure || 'PCS',
          trackingMode: mode,
          targetQuantity: targetQty,
          readOnly: !this.canEditDraft(),
          allocations: row.trackingAllocations || [],
          dialogMode: 'issue',
          transactionTitle: 'Sales Dispatch Note',
          productId: row.productId,
          warehouseId: row.warehouseId,
          warehouseLocationId: row.warehouseLocationId
        }
      }
    );

    dialogRef.afterClosed().subscribe(result => {
      if (result) {
        row.trackingAllocations = result;
        const singleBatch = result.length === 1 && result[0].batchNumber ? result[0] : null;
        if (singleBatch) {
          row.batchNumber = singleBatch.batchNumber;
        } else if (result.length === 0) {
          row.batchNumber = null;
          row.serialNumber = null;
        }

        this.workingLines.update(curr =>
          curr.map(l => l.lineId === row.lineId ? { ...l, ...row, trackingAllocations: result } : l)
        );
        this.showMessage(`Tracking details recorded for ${row.productCode}.`, 'success');
      }
    });
  }

  isLineTrackingComplete(row: EditableShipmentLine): boolean {
    const mode = Number(row.trackingMode) || TrackingMode.None;
    if (mode === TrackingMode.None) return true;

    const targetQty = Number(row.quantityDispatched) || 0;
    if (targetQty <= 0) return true;

    const allocs = row.trackingAllocations || [];
    if (allocs.length === 0) return false;

    const totalAlloc = allocs.reduce((sum, a) => sum + (Number(a.quantity) || 0), 0);
    if (Math.abs(totalAlloc - targetQty) > 0.0001) return false;

    for (const a of allocs) {
      if ((mode === TrackingMode.Batch || mode === TrackingMode.BatchAndSerial) && (!a.batchNumber || !a.batchNumber.trim())) {
        return false;
      }
      if ((mode === TrackingMode.Serial || mode === TrackingMode.BatchAndSerial)) {
        if (!a.serialNumber || !a.serialNumber.trim()) return false;
        if (Number(a.quantity) !== 1) return false;
      }
    }
    return true;
  }

  getLineTrackingSummary(row: EditableShipmentLine): string {
    const mode = Number(row.trackingMode) || TrackingMode.None;
    if (mode === TrackingMode.None) return 'Not Tracked';

    const allocs = row.trackingAllocations || [];
    const allocated = allocs.reduce((sum, a) => sum + (Number(a.quantity) || 0), 0);
    const target = Number(row.quantityDispatched) || 0;

    if (allocated === target && target > 0) {
      return mode === TrackingMode.Batch
        ? `Batches (${allocs.length}) ✓`
        : `Serials (${allocs.length}) ✓`;
    }
    return `Configure (${allocated}/${target})`;
  }

  // Replenishment Action Triggers
  openReplenishmentTransfer(line: SalesOrderDispatchLineAvailabilityDto): void {
    this.selectedTransferLine.set(line);
    const currentWhId = this.workingLines().find(l => l.salesOrderItemId === line.salesOrderItemId)?.warehouseId
      || this.warehouses()[0]?.warehouseId || '';
    const firstAltWhId = line.alternativeWarehouses && line.alternativeWarehouses.length > 0
      ? line.alternativeWarehouses[0].warehouseId
      : '';

    this.transferForm.patchValue({
      sourceWarehouseId: firstAltWhId,
      destinationWarehouseId: currentWhId,
      quantity: line.shortageQuantity > 0 ? line.shortageQuantity : 1,
      notes: `Replenishment transfer for Sales Order ${this.sourceOrder()?.orderNumber || this.availability()?.orderNumber || ''}`
    });
    this.transferDialogVisible.set(true);
  }

  submitReplenishmentTransfer(): void {
    const line = this.selectedTransferLine();
    const orderId = this.sourceOrderId() || this.currentShipment()?.salesOrderId;
    const comp = this.scopeService.selectedCompany();
    const branch = this.scopeService.selectedBranch();

    if (!line || !orderId || !comp?.id || !branch?.id) return;
    if (this.transferForm.invalid) {
      this.transferForm.markAllAsTouched();
      return;
    }

    const formVal = this.transferForm.getRawValue();
    this.actionInProgress.set(true);

    const req: CreateReplenishmentTransferRequest = {
      salesOrderId: orderId,
      salesOrderItemId: line.salesOrderItemId,
      productId: line.productId,
      sourceWarehouseId: formVal.sourceWarehouseId,
      destinationWarehouseId: formVal.destinationWarehouseId,
      quantity: formVal.quantity,
      notes: formVal.notes || null
    };

    this.shipmentService.createReplenishmentTransfer(comp.id, branch.id, orderId, req).subscribe({
      next: res => {
        this.actionInProgress.set(false);
        this.transferDialogVisible.set(false);
        this.showMessage(
          `Inter-warehouse transfer draft ${res.transferReferenceNumber} created successfully! Stock can be transferred via Inventory Transfers.`,
          'success'
        );
      },
      error: err => {
        this.actionInProgress.set(false);
        this.showMessage(err?.error?.message || err?.message || 'Failed to create replenishment transfer.', 'danger');
      }
    });
  }

  openReplenishmentRequisition(line: SalesOrderDispatchLineAvailabilityDto): void {
    this.selectedRequisitionLine.set(line);
    this.requisitionForm.patchValue({
      requiredDate: new Date(Date.now() + 3 * 86400000).toISOString().substring(0, 10),
      quantity: line.shortageQuantity > 0 ? line.shortageQuantity : 1,
      estimatedUnitPrice: null,
      notes: `Procurement requisition for Sales Order ${this.sourceOrder()?.orderNumber || this.availability()?.orderNumber || ''}`
    });
    this.requisitionDialogVisible.set(true);
  }

  submitReplenishmentRequisition(): void {
    const line = this.selectedRequisitionLine();
    const orderId = this.sourceOrderId() || this.currentShipment()?.salesOrderId;
    const comp = this.scopeService.selectedCompany();
    const branch = this.scopeService.selectedBranch();

    if (!line || !orderId || !comp?.id || !branch?.id) return;
    if (this.requisitionForm.invalid) {
      this.requisitionForm.markAllAsTouched();
      return;
    }

    const formVal = this.requisitionForm.getRawValue();
    this.actionInProgress.set(true);

    const req: CreateReplenishmentRequisitionRequest = {
      salesOrderId: orderId,
      requiredDate: formVal.requiredDate || null,
      notes: formVal.notes || null,
      items: [
        {
          productId: line.productId,
          unitOfMeasureId: line.unitOfMeasureId || null,
          quantity: formVal.quantity,
          estimatedUnitPrice: formVal.estimatedUnitPrice || null,
          remarks: `Replenishment for ${line.productCode}`
        }
      ]
    };

    this.shipmentService.createReplenishmentRequisition(comp.id, branch.id, orderId, req).subscribe({
      next: res => {
        this.actionInProgress.set(false);
        this.requisitionDialogVisible.set(false);
        this.showMessage(
          `Purchase Requisition draft ${res.requisitionNumber} created with ${res.itemCount} items!`,
          'success'
        );
      },
      error: err => {
        this.actionInProgress.set(false);
        this.showMessage(err?.error?.message || err?.message || 'Failed to create replenishment requisition.', 'danger');
      }
    });
  }

  private buildExplicitItems(): CreateSalesShipmentItemRequest[] {
    const explicitItems: CreateSalesShipmentItemRequest[] = [];
    for (const l of this.workingLines()) {
      if (l.quantityDispatched <= 0) continue;
      const mode = Number(l.trackingMode) || TrackingMode.None;
      if (mode !== TrackingMode.None && l.trackingAllocations && l.trackingAllocations.length > 0) {
        for (const a of l.trackingAllocations) {
          explicitItems.push({
            salesOrderItemId: l.salesOrderItemId,
            productId: l.productId,
            warehouseId: l.warehouseId,
            warehouseLocationId: l.warehouseLocationId,
            quantityDispatched: Number(a.quantity),
            unitOfMeasureId: l.unitOfMeasureId || null,
            batchNumber: a.batchNumber?.trim() || null,
            serialNumber: a.serialNumber?.trim() || null,
            notes: a.remarks || l.notes || null
          });
        }
      } else {
        explicitItems.push({
          salesOrderItemId: l.salesOrderItemId,
          productId: l.productId,
          warehouseId: l.warehouseId,
          warehouseLocationId: l.warehouseLocationId,
          quantityDispatched: Number(l.quantityDispatched),
          unitOfMeasureId: l.unitOfMeasureId || null,
          batchNumber: l.batchNumber || null,
          serialNumber: l.serialNumber || null,
          notes: l.notes || null
        });
      }
    }
    return explicitItems;
  }

  // Zero-Deduction Save Draft
  saveDraft(): void {
    const comp = this.scopeService.selectedCompany();
    const branch = this.scopeService.selectedBranch();
    if (!comp?.id || !branch?.id) return;

    const lines = this.workingLines();
    if (lines.length === 0) {
      this.showMessage('Cannot save draft without picking lines.', 'warn');
      return;
    }

    for (const l of lines) {
      if (l.quantityDispatched > 0 && (!l.warehouseId || !l.warehouseLocationId)) {
        this.showMessage(`Warehouse and storage location are required for ${l.productCode}.`, 'warn');
        return;
      }
    }

    // Zero-over-shipment validation
    const groupedTotals = new Map<string, number>();
    for (const l of lines) {
      const cur = groupedTotals.get(l.salesOrderItemId) || 0;
      groupedTotals.set(l.salesOrderItemId, cur + Number(l.quantityDispatched));
    }
    for (const [orderItemId, totalPicked] of groupedTotals.entries()) {
      const first = lines.find(l => l.salesOrderItemId === orderItemId);
      if (first && totalPicked > first.outstandingQuantity + 0.0001) {
        this.showMessage(
          `Total picked quantity (${totalPicked.toFixed(2)}) for ${first.productCode} exceeds outstanding order demand (${first.outstandingQuantity.toFixed(2)}).`,
          'danger'
        );
        return;
      }
    }

    const formVal = this.headerForm.getRawValue();

    if (this.isOrderFullyDispatched()) {
      this.showMessage(
        'All demand for this sales order has already been fulfilled by another dispatch note. Outstanding quantity is 0.',
        'warn'
      );
      return;
    }

    this.actionInProgress.set(true);
    const explicitItems = this.buildExplicitItems();

    if (this.mode() === 'create' && this.sourceOrderId()) {
      const req: CreateShipmentFromOrderRequest = {
        shipmentNumber: formVal.shipmentNumber,
        method: formVal.method,
        defaultWarehouseId: lines[0]?.warehouseId || null,
        defaultLocationId: lines[0]?.warehouseLocationId || null,
        shipmentDate: formVal.shipmentDate,
        carrierName: formVal.carrierName || null,
        trackingNumber: formVal.trackingNumber || null,
        recipientName: formVal.recipientName || null,
        deliveryAddress: formVal.deliveryAddress || null,
        vehicleNumber: formVal.vehicleNumber || null,
        notes: formVal.notes || null,
        items: explicitItems
      };

      this.shipmentService.createFromOrder(comp.id, branch.id, this.sourceOrderId()!, req).subscribe({
        next: (created: SalesShipmentDto) => {
          this.actionInProgress.set(false);
          this.showMessage(`Draft dispatch note ${created.shipmentNumber} saved successfully. Stock not deducted.`, 'success');
          this.router.navigate(['/app/sales/dispatches', created.id]);
        },
        error: (err: any) => {
          this.actionInProgress.set(false);
          this.showMessage(err?.error?.message || err?.message || 'Failed to save draft dispatch note.', 'danger');
        }
      });
    } else if (this.shipmentId()) {
      const req: UpdateSalesShipmentRequest = {
        method: formVal.method,
        shipmentDate: formVal.shipmentDate,
        carrierName: formVal.carrierName || null,
        trackingNumber: formVal.trackingNumber || null,
        recipientName: formVal.recipientName || null,
        deliveryAddress: formVal.deliveryAddress || null,
        vehicleNumber: formVal.vehicleNumber || null,
        notes: formVal.notes || null,
        items: explicitItems
      };

      this.shipmentService.updateDraft(comp.id, branch.id, this.shipmentId()!, req).subscribe({
        next: (updated: SalesShipmentDto) => {
          this.actionInProgress.set(false);
          this.currentShipment.set(updated);
          this.showMessage(`Draft dispatch note ${updated.shipmentNumber} updated successfully. Stock not deducted.`, 'success');
          this.loadShipment(updated.id);
        },
        error: (err: any) => {
          this.actionInProgress.set(false);
          this.showMessage(err?.error?.message || err?.message || 'Failed to update draft dispatch note.', 'danger');
        }
      });
    }
  }

  // Transactional Dispatch Posting
  executeDispatch(): void {
    const comp = this.scopeService.selectedCompany();
    const branch = this.scopeService.selectedBranch();
    if (!comp?.id || !branch?.id) return;

    if (this.isOrderFullyDispatched()) {
      this.showMessage(
        'All demand for this sales order has already been fulfilled by another dispatch note. Outstanding quantity is 0.',
        'warn'
      );
      return;
    }

    const lines = this.workingLines();
    if (lines.length === 0) {
      this.showMessage('No picking lines to dispatch.', 'warn');
      return;
    }

    for (const l of lines) {
      if (l.quantityDispatched > 0 && (!l.warehouseId || !l.warehouseLocationId)) {
        this.showMessage(`Warehouse and storage location are required for ${l.productCode}.`, 'warn');
        return;
      }
    }

    for (const l of lines) {
      if (l.quantityDispatched > 0 && (Number(l.trackingMode) || 0) !== TrackingMode.None) {
        if (!this.isLineTrackingComplete(l)) {
          this.showMessage(`Please complete batch/serial tracking allocations for ${l.productCode}.`, 'warn');
          return;
        }
      }
    }

    const groupedTotals = new Map<string, number>();
    for (const l of lines) {
      const cur = groupedTotals.get(l.salesOrderItemId) || 0;
      groupedTotals.set(l.salesOrderItemId, cur + Number(l.quantityDispatched));
    }
    for (const [orderItemId, totalPicked] of groupedTotals.entries()) {
      const first = lines.find(l => l.salesOrderItemId === orderItemId);
      if (first && totalPicked > first.outstandingQuantity + 0.0001) {
        this.showMessage(
          `Total picked quantity (${totalPicked.toFixed(2)}) for ${first.productCode} exceeds outstanding order demand (${first.outstandingQuantity.toFixed(2)}).`,
          'danger'
        );
        return;
      }
    }

    this.actionInProgress.set(true);

    if (this.shipmentId()) {
      const formVal = this.headerForm.getRawValue();
      const explicitItems = this.buildExplicitItems();
      const updateReq: UpdateSalesShipmentRequest = {
        method: formVal.method,
        shipmentDate: formVal.shipmentDate,
        carrierName: formVal.carrierName || null,
        trackingNumber: formVal.trackingNumber || null,
        recipientName: formVal.recipientName || null,
        deliveryAddress: formVal.deliveryAddress || null,
        vehicleNumber: formVal.vehicleNumber || null,
        notes: formVal.notes || null,
        items: explicitItems
      };

      this.shipmentService.updateDraft(comp.id, branch.id, this.shipmentId()!, updateReq).subscribe({
        next: () => {
          this.shipmentService.dispatchShipment(comp.id, branch.id, this.shipmentId()!).subscribe({
            next: (dispatched: SalesShipmentDto) => {
              this.actionInProgress.set(false);
              this.currentShipment.set(dispatched);
              this.showMessage(
                `Dispatch note ${dispatched.shipmentNumber} confirmed! Warehouse inventory deducted and posted transactionally.`,
                'success'
              );
              this.loadShipment(dispatched.id);
            },
            error: (err: any) => {
              this.actionInProgress.set(false);
              this.showMessage(err?.error?.message || err?.message || 'Dispatch posting rejected. Check stock availability.', 'danger');
            }
          });
        },
        error: (err: any) => {
          this.actionInProgress.set(false);
          this.showMessage(err?.error?.message || err?.message || 'Failed to prepare shipment draft for dispatch.', 'danger');
        }
      });
    } else if (this.mode() === 'create' && this.sourceOrderId()) {
      const formVal = this.headerForm.getRawValue();
      const explicitItems = this.buildExplicitItems();
      const firstLine = lines[0];
      const dispatchReq: DispatchOrderRequest = {
        warehouseId: firstLine.warehouseId,
        warehouseLocationId: firstLine.warehouseLocationId,
        shipmentNumber: formVal.shipmentNumber,
        method: formVal.method,
        shipmentDate: formVal.shipmentDate,
        carrierName: formVal.carrierName || undefined,
        trackingNumber: formVal.trackingNumber || undefined,
        recipientName: formVal.recipientName || undefined,
        deliveryAddress: formVal.deliveryAddress || undefined,
        vehicleNumber: formVal.vehicleNumber || undefined,
        notes: formVal.notes || undefined,
        items: explicitItems
      };

      this.shipmentService.dispatchFromOrder(comp.id, branch.id, this.sourceOrderId()!, dispatchReq).subscribe({
        next: (dispatched: SalesShipmentDto) => {
          this.actionInProgress.set(false);
          this.showMessage(
            `Dispatch note ${dispatched.shipmentNumber} created and posted! Warehouse inventory deducted transactionally.`,
            'success'
          );
          this.router.navigate(['/app/sales/dispatches', dispatched.id]);
        },
        error: (err: any) => {
          this.actionInProgress.set(false);
          this.showMessage(err?.error?.message || err?.message || 'Dispatch posting rejected. Check stock availability.', 'danger');
        }
      });
    }
  }

  deliverShipment(): void {
    const comp = this.scopeService.selectedCompany();
    const branch = this.scopeService.selectedBranch();
    const id = this.shipmentId();
    if (!comp?.id || !branch?.id || !id) return;

    this.actionInProgress.set(true);
    const formVal = this.headerForm.getRawValue();

    this.shipmentService
      .deliverShipment(comp.id, branch.id, id, {
        recipientName: formVal.recipientName || undefined,
        notes: formVal.notes || undefined
      })
      .subscribe({
        next: (updated: SalesShipmentDto) => {
          this.actionInProgress.set(false);
          this.currentShipment.set(updated);
          this.showMessage(
            `Shipment ${updated.shipmentNumber} delivered successfully. Warehouse inventory posted and Sales Invoice draft generated!`,
            'success'
          );
          if (updated.invoiceId) {
            this.router.navigate(['/app/sales/invoices', updated.invoiceId]);
          } else {
            this.mode.set('view');
            this.headerForm.disable();
          }
        },
        error: (err: any) => {
          this.actionInProgress.set(false);
          this.showMessage(err?.error?.message || err?.message || 'Failed to deliver shipment.', 'danger');
        }
      });
  }

  cancelDraft(): void {
    const comp = this.scopeService.selectedCompany();
    const branch = this.scopeService.selectedBranch();
    const id = this.shipmentId();
    if (!comp?.id || !branch?.id || !id) return;

    this.actionInProgress.set(true);
    this.shipmentService.cancelDraft(comp.id, branch.id, id).subscribe({
      next: (updated: SalesShipmentDto) => {
        this.actionInProgress.set(false);
        this.currentShipment.set(updated);
        this.showMessage('Dispatch note cancelled.', 'info');
        this.mode.set('view');
        this.headerForm.disable();
      },
      error: (err: any) => {
        this.actionInProgress.set(false);
        this.showMessage(err?.error?.message || err?.message || 'Failed to cancel dispatch note.', 'danger');
      }
    });
  }

  navigateToInvoice(): void {
    const invId = this.currentShipment()?.invoiceId;
    if (invId) {
      this.router.navigate(['/app/sales/invoices', invId]);
    }
  }

  navigateBack(): void {
    this.router.navigate(['/app/sales/dispatches']);
  }

  showMessage(text: string, severity: 'success' | 'info' | 'warn' | 'danger'): void {
    this.message.set({ text, severity });
  }
}
