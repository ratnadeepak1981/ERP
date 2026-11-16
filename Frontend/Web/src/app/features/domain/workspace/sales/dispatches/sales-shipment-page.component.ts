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
import { FormsModule, ReactiveFormsModule, FormGroup, FormControl, Validators } from '@angular/forms';
import { Router, ActivatedRoute, RouterModule } from '@angular/router';
import { Subscription, forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { MatDialog } from '@angular/material/dialog';
import { AppPageHeaderComponent } from '../../../../../shared/components/page-header/page-header.component';
import { AppStatusBadgeComponent } from '../../../../../shared/components/status-badge/status-badge.component';
import { AppLoadingStateComponent } from '../../../../../shared/components/loading-state/loading-state.component';
import { AppErrorStateComponent } from '../../../../../shared/components/error-state/error-state.component';
import { AccessDeniedComponent } from '../../../../../shared/components/access-denied/access-denied.component';
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
  EligibleLocationDto,
  DispatchOrderRequest,
  CreateSalesShipmentItemRequest
} from '../../../../../core/models/sales-shipment.models';
import { SalesOrderDto } from '../../../../../core/models/sales-order.models';
import { CustomerItem, ProductItem, WarehouseItem, WarehouseLocationItem, TrackingMode } from '../../../../../core/models/scope.models';
import { BatchSerialAllocation } from '../../../../../core/models/goods-receipt-note.models';
import {
  BatchSerialTrackingDialogComponent,
  BatchSerialDialogData
} from '../../procurement/batch-serial-dialog/batch-serial-dialog.component';

export interface DispatchLineWithTracking extends SalesOrderDispatchLineAvailabilityDto {
  trackingAllocations?: BatchSerialAllocation[];
}

export interface EditableShipmentLine {
  lineId: string;
  salesOrderItemId: string;
  productId: string;
  productCode: string;
  productName: string;
  unitOfMeasureId?: string | null;
  unitOfMeasure?: string;
  orderedQuantity: number;
  quantityDispatched: number;
  warehouseId: string;
  warehouseLocationId: string;
  batchNumber?: string | null;
  serialNumber?: string | null;
  notes?: string | null;
  trackingMode?: number;
  trackingAllocations?: BatchSerialAllocation[];
}

@Component({
  selector: 'app-sales-shipment-page',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    RouterModule,
    AppPageHeaderComponent,
    AppStatusBadgeComponent,
    AppLoadingStateComponent,
    AppErrorStateComponent,
    AccessDeniedComponent
  ],
  templateUrl: './sales-shipment-page.component.html',
  styleUrl: './sales-shipment-page.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class SalesShipmentPageComponent implements OnInit, OnDestroy {
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

  // Simplified Location Selection & Availability State (Round 1)
  readonly availability = signal<SalesOrderDispatchAvailabilityDto | null>(null);
  readonly selectedLocationId = signal<string | null>(null);
  readonly selectedWarehouseId = signal<string | null>(null);
  readonly showAllLines = signal<boolean>(false);
  readonly revalidatingStock = signal<boolean>(false);

  // Loading & Action States
  readonly loading = signal<boolean>(false);
  readonly actionInProgress = signal<boolean>(false);
  readonly error = signal<string | null>(null);
  readonly isAccessDenied = signal<boolean>(false);
  readonly message = signal<{ text: string; severity: 'success' | 'info' | 'warn' | 'danger' } | null>(null);

  // Master Data
  readonly customers = signal<CustomerItem[]>([]);
  readonly products = signal<ProductItem[]>([]);
  readonly warehouses = signal<WarehouseItem[]>([]);
  readonly locations = signal<WarehouseLocationItem[]>([]);

  // Working Lines (for existing saved shipment view)
  readonly workingLines = signal<EditableShipmentLine[]>([]);

  // Fulfillment Method Options
  readonly methodOptions = [
    { label: 'Delivery', value: FulfillmentMethod.Delivery },
    { label: 'Customer Pickup', value: FulfillmentMethod.CustomerPickup },
    { label: 'Store Handover', value: FulfillmentMethod.StoreHandover }
  ];

  // Header Form
  readonly headerForm = new FormGroup({
    shipmentNumber: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    orderNumber: new FormControl({ value: '', disabled: true }),
    customerName: new FormControl({ value: '', disabled: true }),
    method: new FormControl<number>(FulfillmentMethod.Delivery, { nonNullable: true, validators: [Validators.required] }),
    shipmentDate: new FormControl(new Date().toISOString().substring(0, 10), { nonNullable: true, validators: [Validators.required] }),
    carrierName: new FormControl(''),
    trackingNumber: new FormControl(''),
    recipientName: new FormControl(''),
    deliveryAddress: new FormControl(''),
    vehicleNumber: new FormControl(''),
    notes: new FormControl('')
  });

  // Breadcrumbs
  readonly breadcrumbs = computed(() => [
    { label: 'Workspace', routerLink: '/app/dashboard' },
    { label: 'Sales', routerLink: '/app/sales' },
    { label: 'Dispatch Notes', routerLink: '/app/sales/dispatches' },
    { label: this.mode() === 'create' ? 'New Dispatch Note' : (this.headerForm.get('shipmentNumber')?.value || 'Dispatch Details') }
  ]);

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

  readonly canDeliver = computed<boolean>(() => {
    return (this.authService.hasPermission('SALES_SHIPMENT.DELIVER') || this.authService.isTenantAdmin() || this.authService.isPlatformAdmin()) && this.isDraft();
  });

  // Computed displayed lines for Dispatch Availability
  readonly displayedLines = computed<DispatchLineWithTracking[]>(() => {
    const avail = this.availability();
    if (!avail || !avail.lines) return [];
    if (this.showAllLines()) {
      return avail.lines as DispatchLineWithTracking[];
    }
    const shortages = (avail.lines as DispatchLineWithTracking[]).filter(l => l.shortageQuantity > 0 || !l.canFulfil);
    return shortages.length > 0 ? shortages : (avail.lines as DispatchLineWithTracking[]);
  });

  readonly shortageCount = computed<number>(() => {
    const avail = this.availability();
    if (!avail || !avail.lines) return 0;
    return avail.lines.filter(l => l.shortageQuantity > 0 || !l.canFulfil).length;
  });

  readonly totalLineCount = computed<number>(() => {
    const avail = this.availability();
    return avail?.lines ? avail.lines.length : 0;
  });

  // Tracking Helpers & Validation
  isLineTrackingComplete(line: DispatchLineWithTracking): boolean {
    const mode = Number(line.trackingMode) || TrackingMode.None;
    if (mode === TrackingMode.None) return true;

    const targetQty = Number(line.remainingQuantity) || 0;
    if (targetQty <= 0) return true;

    const allocs = line.trackingAllocations || [];
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

  getLineTrackingSummary(line: DispatchLineWithTracking): string {
    const mode = Number(line.trackingMode) || TrackingMode.None;
    if (mode === TrackingMode.None) return 'Not Tracked';

    const allocs = line.trackingAllocations || [];
    const allocated = allocs.reduce((sum, a) => sum + (Number(a.quantity) || 0), 0);
    const target = Number(line.remainingQuantity) || 0;

    if (allocated === target && target > 0) {
      return mode === TrackingMode.Batch
        ? `Batches (${allocs.length}) ✓`
        : `Serials (${allocs.length}) ✓`;
    }
    return `Configure (${allocated}/${target})`;
  }

  openTrackingDialog(line: DispatchLineWithTracking, event?: Event): void {
    if (event) {
      event.stopPropagation();
      event.preventDefault();
    }

    const mode = Number(line.trackingMode) || TrackingMode.None;
    if (mode === TrackingMode.None) return;

    const targetQty = Number(line.remainingQuantity) || 0;
    if (targetQty <= 0) {
      this.message.set({ text: 'No remaining quantity required for dispatch.', severity: 'warn' });
      return;
    }

    const whId = this.selectedWarehouseId() || (this.warehouses().length > 0 ? this.warehouses()[0].warehouseId : '');
    const locId = this.selectedLocationId() || '';

    const dialogRef = this.matDialog.open<BatchSerialTrackingDialogComponent, BatchSerialDialogData, BatchSerialAllocation[]>(
      BatchSerialTrackingDialogComponent,
      {
        width: '920px',
        maxWidth: '96vw',
        disableClose: true,
        data: {
          productCode: line.productCode,
          productName: line.productName,
          unitOfMeasure: line.unitOfMeasure || 'PCS',
          trackingMode: mode,
          targetQuantity: targetQty,
          readOnly: this.mode() === 'view',
          allocations: line.trackingAllocations || [],
          dialogMode: 'issue',
          transactionTitle: 'Dispatch Note',
          productId: line.productId,
          warehouseId: whId,
          warehouseLocationId: locId
        }
      }
    );

    dialogRef.afterClosed().subscribe(result => {
      if (result) {
        line.trackingAllocations = result;
        // Trigger signal reactivity on availability
        const currentAvail = this.availability();
        if (currentAvail) {
          this.availability.set({
            ...currentAvail,
            lines: [...currentAvail.lines]
          });
        }
      }
    });
  }

  // Can Dispatch boolean flag: enforces stock fulfillment & tracking completeness
  readonly canDispatch = computed<boolean>(() => {
    if (
      this.mode() !== 'create' ||
      !this.sourceOrderId() ||
      !this.selectedLocationId() ||
      !this.selectedWarehouseId() ||
      this.availability()?.canFulfil !== true ||
      this.actionInProgress() ||
      this.revalidatingStock()
    ) {
      return false;
    }

    // Validate tracking completeness for all tracked lines with remaining demand
    const lines = (this.availability()?.lines || []) as DispatchLineWithTracking[];
    for (const l of lines) {
      if (l.remainingQuantity > 0 && (Number(l.trackingMode) || 0) !== TrackingMode.None) {
        if (!this.isLineTrackingComplete(l)) {
          return false;
        }
      }
    }

    return true;
  });

  ngOnInit(): void {
    this.loadMasterData();
    this.detectRoute();
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
      if (url.endsWith('/edit')) {
        this.mode.set('edit');
      } else {
        this.mode.set('view');
      }
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
              this.locations.set(locArrays.flat());
            }
          });
        }

        // Synchronize tracking mode on current availability lines if already loaded
        this.enrichAvailabilityLines(this.availability());
      }
    });
  }

  private enrichAvailabilityLines(avail: SalesOrderDispatchAvailabilityDto | null): void {
    if (!avail || !avail.lines) return;
    const prods = this.products();
    for (const l of avail.lines as DispatchLineWithTracking[]) {
      const p = prods.find(x => x.productId === l.productId || x.productCode === l.productCode);
      if (p) {
        l.trackingMode = Number(p.trackingMode) || TrackingMode.None;
        l.unitOfMeasure = p.unitOfMeasure || 'PCS';
      }
      if (!l.trackingAllocations) {
        l.trackingAllocations = [];
      }
    }
  }

  private initFromOrder(orderId: string): void {
    const comp = this.scopeService.selectedCompany();
    const branch = this.scopeService.selectedBranch();
    if (!comp?.id || !branch?.id) return;

    this.loading.set(true);
    forkJoin({
      order: this.salesOrderService.getOrder(comp.id, branch.id, orderId).pipe(catchError(() => of(null))),
      avail: this.shipmentService.getOrderDispatchAvailability(comp.id, branch.id, orderId).pipe(catchError(() => of(null)))
    }).subscribe({
      next: ({ order, avail }) => {
        this.loading.set(false);
        if (!order) {
          this.error.set('Associated sales order not found.');
          return;
        }

        this.sourceOrder.set(order);
        this.enrichAvailabilityLines(avail);
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

        if (avail) {
          if (avail.canDefaultLocationFulfil) {
            this.selectedLocationId.set(avail.defaultLocationId || null);
            this.selectedWarehouseId.set(avail.defaultWarehouseId || null);
          } else if (avail.eligibleLocations && avail.eligibleLocations.length > 0) {
            // Default location insufficient, but alternative is available
            const firstAlt = avail.eligibleLocations[0];
            this.selectedLocationId.set(firstAlt.locationId);
            this.selectedWarehouseId.set(firstAlt.warehouseId);
            this.onSelectLocation(firstAlt.locationId);
          } else {
            // Shortages across all locations
            this.selectedLocationId.set(avail.defaultLocationId || null);
            this.selectedWarehouseId.set(avail.defaultWarehouseId || null);
          }
        }
      },
      error: (err: any) => {
        this.loading.set(false);
        this.error.set(err?.message || 'Failed to initialize dispatch availability.');
      }
    });
  }

  onSelectLocation(locationId: string): void {
    const comp = this.scopeService.selectedCompany();
    const branch = this.scopeService.selectedBranch();
    const orderId = this.sourceOrderId();
    if (!comp?.id || !branch?.id || !orderId) return;

    this.selectedLocationId.set(locationId);

    // Resolve matching warehouse for selected location
    const matched = this.availability()?.eligibleLocations.find(l => l.locationId === locationId);
    if (matched) {
      this.selectedWarehouseId.set(matched.warehouseId);
    } else {
      const locInMaster = this.locations().find(l => l.locationId === locationId);
      if (locInMaster) {
        this.selectedWarehouseId.set(locInMaster.warehouseId);
      }
    }

    this.revalidatingStock.set(true);
    this.shipmentService.getOrderDispatchAvailability(comp.id, branch.id, orderId, locationId).subscribe({
      next: (updatedAvail: SalesOrderDispatchAvailabilityDto) => {
        this.revalidatingStock.set(false);
        this.enrichAvailabilityLines(updatedAvail);
        this.availability.set(updatedAvail);
        if (updatedAvail.selectedWarehouseId) {
          this.selectedWarehouseId.set(updatedAvail.selectedWarehouseId);
        }
      },
      error: (err: any) => {
        this.revalidatingStock.set(false);
        this.message.set({
          text: err?.error?.message || err?.message || 'Failed to recheck stock for selected location.',
          severity: 'danger'
        });
      }
    });
  }

  toggleShowAllLines(): void {
    this.showAllLines.update(v => !v);
  }

  executeDirectDispatch(): void {
    const comp = this.scopeService.selectedCompany();
    const branch = this.scopeService.selectedBranch();
    const orderId = this.sourceOrderId();
    const locId = this.selectedLocationId();
    const whId = this.selectedWarehouseId();

    if (!comp?.id || !branch?.id || !orderId) {
      this.message.set({ text: 'Company, branch, and sales order context are required.', severity: 'warn' });
      return;
    }

    if (!locId || !whId) {
      this.message.set({ text: 'Please select an authorized issue location with sufficient stock.', severity: 'warn' });
      return;
    }

    if (!this.availability()?.canFulfil) {
      this.message.set({ text: 'Cannot dispatch order with insufficient stock.', severity: 'danger' });
      return;
    }

    if (this.headerForm.invalid) {
      this.headerForm.markAllAsTouched();
      this.message.set({ text: 'Please complete all required fields.', severity: 'warn' });
      return;
    }

    const formVal = this.headerForm.getRawValue();
    this.actionInProgress.set(true);

    // Expand tracked allocations into individual CreateSalesShipmentItemRequest lines
    const explicitItems: CreateSalesShipmentItemRequest[] = [];
    const lines = (this.availability()?.lines || []) as DispatchLineWithTracking[];
    let hasExplicitTracking = false;

    for (const l of lines) {
      if (l.remainingQuantity <= 0) continue;
      const mode = Number(l.trackingMode) || TrackingMode.None;

      if (mode !== TrackingMode.None && l.trackingAllocations && l.trackingAllocations.length > 0) {
        hasExplicitTracking = true;
        for (const a of l.trackingAllocations) {
          explicitItems.push({
            salesOrderItemId: l.salesOrderItemId,
            productId: l.productId,
            warehouseId: whId,
            warehouseLocationId: locId,
            quantityDispatched: Number(a.quantity),
            unitOfMeasureId: l.unitOfMeasureId || null,
            batchNumber: a.batchNumber?.trim() || null,
            serialNumber: a.serialNumber?.trim() || null,
            notes: a.remarks || formVal.notes || null
          });
        }
      } else {
        // Products with TrackingMode.None or untracked
        explicitItems.push({
          salesOrderItemId: l.salesOrderItemId,
          productId: l.productId,
          warehouseId: whId,
          warehouseLocationId: locId,
          quantityDispatched: l.remainingQuantity,
          unitOfMeasureId: l.unitOfMeasureId || null,
          batchNumber: null,
          serialNumber: null,
          notes: formVal.notes || null
        });
      }
    }

    const req: DispatchOrderRequest = {
      warehouseId: whId,
      warehouseLocationId: locId,
      shipmentNumber: formVal.shipmentNumber,
      method: formVal.method,
      shipmentDate: formVal.shipmentDate,
      carrierName: formVal.carrierName || undefined,
      trackingNumber: formVal.trackingNumber || undefined,
      recipientName: formVal.recipientName || undefined,
      deliveryAddress: formVal.deliveryAddress || undefined,
      vehicleNumber: formVal.vehicleNumber || undefined,
      notes: formVal.notes || undefined,
      items: hasExplicitTracking ? explicitItems : undefined
    };

    this.shipmentService.dispatchFromOrder(comp.id, branch.id, orderId, req).subscribe({
      next: (created: SalesShipmentDto) => {
        this.actionInProgress.set(false);
        this.message.set({
          text: `Dispatch note ${created.shipmentNumber} created and stock issued successfully.`,
          severity: 'success'
        });
        if (created.invoiceId) {
          this.router.navigate(['/app/sales/invoices', created.invoiceId]);
        } else {
          this.router.navigate(['/app/sales/dispatches', created.id]);
        }
      },
      error: (err: any) => {
        this.actionInProgress.set(false);
        this.message.set({
          text: err?.error?.message || err?.message || 'Dispatch submission rejected. Please check stock availability.',
          severity: 'danger'
        });
        // Re-evaluate availability upon error to reflect live inventory
        this.onSelectLocation(locId);
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

  loadShipment(id: string): void {
    const comp = this.scopeService.selectedCompany();
    const branch = this.scopeService.selectedBranch();
    if (!comp?.id) return;

    this.loading.set(true);
    this.shipmentService.getShipment(comp.id, branch?.id || null, id).subscribe({
      next: (shipment: SalesShipmentDto | null) => {
        this.loading.set(false);
        if (!shipment) {
          this.error.set('Dispatch note not found.');
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

        if (this.mode() === 'view') {
          this.headerForm.disable();
        }

        const lines: EditableShipmentLine[] = (shipment.items || []).map(item => ({
          lineId: item.id,
          salesOrderItemId: item.salesOrderItemId,
          productId: item.productId,
          productCode: item.productCode || '',
          productName: item.productName || '',
          unitOfMeasureId: item.unitOfMeasureId,
          orderedQuantity: item.quantityDispatched,
          quantityDispatched: item.quantityDispatched,
          warehouseId: item.warehouseId,
          warehouseLocationId: item.warehouseLocationId,
          batchNumber: item.batchNumber,
          serialNumber: item.serialNumber,
          notes: item.notes
        }));

        this.workingLines.set(lines);
      },
      error: (err: any) => {
        this.loading.set(false);
        this.error.set(err?.message || 'Failed to load dispatch note.');
      }
    });
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
          this.message.set({
            text: `Shipment ${updated.shipmentNumber} delivered successfully. Warehouse inventory posted and Sales Invoice draft generated!`,
            severity: 'success'
          });

          if (updated.invoiceId) {
            this.router.navigate(['/app/sales/invoices', updated.invoiceId]);
          } else {
            this.mode.set('view');
            this.headerForm.disable();
          }
        },
        error: (err: any) => {
          this.actionInProgress.set(false);
          this.message.set({
            text: err?.error?.message || err?.message || 'Failed to deliver shipment.',
            severity: 'danger'
          });
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
        this.message.set({ text: 'Dispatch note cancelled.', severity: 'info' });
        this.mode.set('view');
        this.headerForm.disable();
      },
      error: (err: any) => {
        this.actionInProgress.set(false);
        this.message.set({
          text: err?.error?.message || err?.message || 'Failed to cancel dispatch note.',
          severity: 'danger'
        });
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
}
