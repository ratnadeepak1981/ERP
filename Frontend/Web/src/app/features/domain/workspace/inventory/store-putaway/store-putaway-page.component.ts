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
import { Subscription, of } from 'rxjs';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { DialogModule } from 'primeng/dialog';
import { TooltipModule } from 'primeng/tooltip';
import { SharedModule } from 'primeng/api';
import { AppPageHeaderComponent } from '../../../../../shared/components/page-header/page-header.component';
import { AppEditableDataTableComponent } from '../../../../../shared/components/data-table/editable-data-table.component';
import { AppStatusBadgeComponent } from '../../../../../shared/components/status-badge/status-badge.component';
import { AppLoadingStateComponent } from '../../../../../shared/components/loading-state/loading-state.component';
import { AppErrorStateComponent } from '../../../../../shared/components/error-state/error-state.component';
import { AccessDeniedComponent } from '../../../../../shared/components/access-denied/access-denied.component';
import { PutawayService } from '../../../../../core/services/putaway.service';
import { MasterDataService } from '../../../../../core/services/master-data.service';
import { WorkspaceScopeService } from '../../../../../core/services/workspace-scope.service';
import { AuthService } from '../../../../../core/auth/auth.service';
import {
  isForbiddenError,
  toUserFacingErrorMessage
} from '../../../../../core/utils/http-error.util';
import {
  PutawayRecordDto,
  PutawayLineDto,
  PutawayStatus,
  PutawaySourceDocType,
  AvailableReceiptOption,
  getPutawayStatusName,
  getPutawayStatusSeverity,
  CreatePutawayRequest
} from '../../../../../core/models/putaway.models';
import { WarehouseItem, WarehouseLocationItem } from '../../../../../core/models/scope.models';
import { DataTableColumn, SelectOption } from '../../../../../shared/models/data-table-config.model';

export type PutawayPageMode = 'create' | 'view' | 'edit';

export interface EditablePutawayLine {
  lineId: string;
  sourceLineId?: string;
  productId: string;
  productCode: string;
  productName: string;
  unitOfMeasure: string;
  sourceQuantity: number;
  alreadyPutawayQuantity: number;
  outstandingQuantity: number;
  putawayQuantity: number;
  sourceLocationId?: string;
  sourceLocationCode?: string;
  sourceLocationName?: string;
  destinationLocationId: string;
  destinationLocationCode: string;
  destinationLocationName?: string;
  batchNumber?: string | null;
  serialNumber?: string | null;
  notes?: string;
}

@Component({
  selector: 'app-store-putaway-page',
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
    SharedModule,
    AppPageHeaderComponent,
    AppEditableDataTableComponent,
    AppStatusBadgeComponent,
    AppLoadingStateComponent,
    AppErrorStateComponent,
    AccessDeniedComponent
  ],
  templateUrl: './store-putaway-page.component.html',
  styleUrl: './store-putaway-page.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class StorePutawayPageComponent implements OnInit, OnDestroy {
  readonly putawayService = inject(PutawayService);
  readonly masterDataService = inject(MasterDataService);
  readonly scopeService = inject(WorkspaceScopeService);
  readonly authService = inject(AuthService);
  readonly router = inject(Router);
  readonly route = inject(ActivatedRoute);

  private readonly subs = new Subscription();

  // Mode & Route State
  readonly mode = signal<PutawayPageMode>('create');
  readonly putawayId = signal<string | null>(null);
  readonly currentRecord = signal<PutawayRecordDto | null>(null);

  // Loading & State
  readonly loading = signal<boolean>(false);
  readonly saving = signal<boolean>(false);
  readonly error = signal<string | null>(null);
  readonly isAccessDenied = signal<boolean>(false);
  readonly feedbackMessage = signal<{ type: 'success' | 'error'; text: string } | null>(null);

  // Master Data
  readonly warehouses = signal<WarehouseItem[]>([]);
  readonly warehouseLocations = signal<WarehouseLocationItem[]>([]);
  readonly availableReceipts = signal<AvailableReceiptOption[]>([]);
  readonly selectedDocType = signal<string>('StockReceipt');

  readonly filteredAvailableReceipts = computed(() => {
    const dt = this.selectedDocType();
    const list = this.availableReceipts();
    if (!dt || dt === 'All') return list;
    return list.filter(r => r.type === dt);
  });

  // Form lines
  readonly workingLines = signal<EditablePutawayLine[]>([]);

  // Header form
  readonly headerForm = new FormGroup({
    putawayNumber: new FormControl<string>('', { nonNullable: true, validators: [Validators.required] }),
    putawayDate: new FormControl<string>(new Date().toISOString().substring(0, 10), { nonNullable: true, validators: [Validators.required] }),
    sourceDocumentType: new FormControl<PutawaySourceDocType>('StockReceipt', { nonNullable: true }),
    sourceReceiptId: new FormControl<string>('', { nonNullable: true }),
    warehouseId: new FormControl<string>('', { nonNullable: true, validators: [Validators.required] }),
    notes: new FormControl<string>('', { nonNullable: true })
  });

  // Breadcrumbs
  readonly breadcrumbs = computed(() => [
    { label: 'Workspace', routerLink: '/app/dashboard' },
    { label: 'Inventory', routerLink: '/app/inventory' },
    { label: 'Storage & Putaway', routerLink: '/app/inventory/store-putaway' },
    { label: this.mode() === 'create' ? 'New Putaway' : (this.currentRecord()?.putawayNumber || 'Putaway Details') }
  ]);

  // Page Title & Subtitle
  readonly pageTitle = computed(() => {
    if (this.mode() === 'create') return 'New Storage & Putaway';
    const num = this.currentRecord()?.putawayNumber;
    return num ? `Storage & Putaway — ${num}` : 'Storage & Putaway Details';
  });

  readonly isReadOnlyMode = computed(() => this.mode() === 'view');
  readonly isCompleted = computed(() => this.currentRecord()?.status === PutawayStatus.Completed);
  readonly isDraftRecord = computed(() => this.currentRecord()?.status === PutawayStatus.Draft);
  readonly getPutawayStatusSeverity = getPutawayStatusSeverity;

  // Summary Totals
  readonly totalLinesCount = computed(() => this.workingLines().length);
  readonly totalPutawayQuantity = computed(() =>
    this.workingLines().reduce((acc, l) => acc + (Number(l.putawayQuantity) || 0), 0)
  );

  // Helper to get destination options for a line, strictly excluding the line's own source location
  getDestinationOptionsForLine(row?: EditablePutawayLine): SelectOption[] {
    const whId = this.headerForm.controls.warehouseId.value;
    const srcId = row?.sourceLocationId;
    return this.warehouseLocations()
      .filter(l => {
        if (!l.isActive) return false;
        if (whId && l.warehouseId && l.warehouseId !== whId) return false;
        if (l.isReceivingLocation) return false;
        if (l.canPutAway === false) return false;
        if (l.canStoreInventory === false) return false;
        const locId = l.warehouseLocationId ?? l.locationId;
        if (srcId && locId === srcId) return false;
        return true;
      })
      .map(l => ({
        label: this.formatLocationHierarchy(l),
        value: l.warehouseLocationId ?? l.locationId
      }));
  }

  // Default Destination Location Options for editable table:
  // Must be active, belong to the selected warehouse, not a receiving dock, and allow putaway & storage.
  readonly destinationLocationOptions = computed<SelectOption[]>(() => {
    const firstLine = this.workingLines()[0];
    return this.getDestinationOptionsForLine(firstLine);
  });

  // Determines whether any line has no eligible destination bins in the current warehouse
  readonly hasNoEligibleDestinations = computed<boolean>(() => {
    const lines = this.workingLines();
    if (lines.length === 0) return false;
    return lines.some(l => this.getDestinationOptionsForLine(l).length === 0);
  });

  // Current warehouse display name for warnings and headers
  readonly currentWarehouseName = computed<string>(() => {
    const whId = this.headerForm.controls.warehouseId.value;
    const wh = this.warehouses().find(w => w.warehouseId === whId);
    return wh ? `${wh.warehouseCode} — ${wh.warehouseName}` : (whId || 'Warehouse');
  });

  // Formats label for physical movement summary
  getDestinationLocationLabel(destId?: string): string {
    if (!destId) return '(Select Destination Bin)';
    const loc = this.warehouseLocations().find(
      l => (l.warehouseLocationId ?? l.locationId) === destId
    );
    if (!loc) return destId;
    return loc.locationName && loc.locationName !== loc.locationCode
      ? `${loc.locationCode} (${loc.locationName})`
      : loc.locationCode;
  }

  // Authoritative Original Receipt Location details (READ ONLY)
  readonly sourceReceiptLocationDetails = computed(() => {
    const lines = this.workingLines();
    if (lines.length === 0) return null;

    const firstLine = lines[0];
    const srcLocId = firstLine?.sourceLocationId;
    const whId = this.headerForm.controls.warehouseId.value;
    const wh = this.warehouses().find(w => w.warehouseId === whId);

    const loc = srcLocId
      ? this.warehouseLocations().find(l => (l.warehouseLocationId ?? l.locationId) === srcLocId)
      : null;

    return {
      warehouseCode: wh?.warehouseCode || loc?.zoneCode || 'WH',
      warehouseName: wh?.warehouseName || 'Warehouse Facility',
      zoneCode: loc?.zoneCode || '',
      zoneName: loc?.zoneName || (loc?.zoneCode ? `Zone ${loc.zoneCode}` : 'General Receiving Area'),
      locationCode: loc?.locationCode || firstLine?.sourceLocationCode || srcLocId || 'RECEIVING',
      locationName: loc?.locationName || firstLine?.sourceLocationName || 'Receiving Area',
      locationTypeCode: loc?.locationTypeCode || '',
      locationTypeName: loc?.locationTypeName || loc?.locationTypeCode || 'Receiving Staging Area',
      hierarchyPath: loc ? this.formatLocationHierarchy(loc) : `${wh?.warehouseCode || 'WH'} / ${firstLine?.sourceLocationCode || 'RECEIVING'}`
    };
  });

  // Detail Columns
  readonly detailColumns = computed<DataTableColumn<EditablePutawayLine>[]>(() => {
    const isReadOnly = this.isReadOnlyMode();

    return [
      {
        field: 'productCode',
        header: 'Product / SKU',
        width: '240px',
        editable: false,
        format: (_val, row) => `${row.productCode} — ${row.productName}`
      },
      {
        field: 'unitOfMeasure',
        header: 'UOM',
        width: '70px',
        editable: false,
        align: 'center'
      },
      {
        field: 'sourceLocationCode',
        header: 'Source Staging (FROM)',
        width: '180px',
        editable: false,
        format: (_val, row) => {
          if (!row.sourceLocationCode && !row.sourceLocationName) return 'Receiving Dock';
          return row.sourceLocationName && row.sourceLocationName !== row.sourceLocationCode
            ? `${row.sourceLocationCode} (${row.sourceLocationName})`
            : (row.sourceLocationCode || 'Receiving Dock');
        }
      },
      {
        field: 'sourceQuantity',
        header: 'Received Qty',
        width: '110px',
        editable: false,
        align: 'right',
        format: val => Number(val || 0).toLocaleString()
      },
      {
        field: 'alreadyPutawayQuantity',
        header: 'Already Put Away',
        width: '120px',
        editable: false,
        align: 'right',
        format: val => Number(val || 0).toLocaleString()
      },
      {
        field: 'outstandingQuantity',
        header: 'Outstanding Qty',
        width: '120px',
        editable: false,
        align: 'right',
        format: val => Number(val || 0).toLocaleString()
      },
      {
        field: 'putawayQuantity',
        header: 'Putaway Qty *',
        width: '120px',
        editable: !isReadOnly,
        editType: 'number',
        align: 'right',
        validators: [
          (val, row) => {
            const num = Number(val);
            if (isNaN(num) || num <= 0) return 'Quantity must be greater than 0';
            if (row.outstandingQuantity !== undefined && num > row.outstandingQuantity) {
              return `Cannot exceed outstanding quantity (${row.outstandingQuantity})`;
            }
            return null;
          }
        ]
      },
      {
        field: 'destinationLocationId',
        header: 'Destination Location (TO) *',
        width: '340px',
        editable: !isReadOnly,
        editType: 'select',
        editOptions: this.destinationLocationOptions(),
        getEditOptions: row => this.getDestinationOptionsForLine(row),
        format: (val, row) => {
          if (!val) {
            const avail = this.getDestinationOptionsForLine(row);
            return avail.length === 0 ? 'No eligible storage bins available' : 'Select storage bin...';
          }
          const loc = this.warehouseLocations().find(
            l => (l.warehouseLocationId ?? l.locationId) === val
          );
          return loc ? this.formatLocationHierarchy(loc) : (val || 'Select storage bin...');
        },
        validators: [
          (val, row) => {
            if (!val) return 'Destination storage bin is required';
            if (row?.sourceLocationId && val === row.sourceLocationId) {
              return 'Destination cannot be identical to source receipt location';
            }
            return null;
          }
        ]
      }
    ];
  });

  ngOnInit(): void {
    this.initModeAndData();
    this.subs.add(
      this.route.paramMap.subscribe(params => {
        const id = params.get('id');
        if (id && id !== 'new') {
          this.mode.set('view');
          this.putawayId.set(id);
          this.loadExistingRecord(id);
        } else {
          this.mode.set('create');
          this.putawayId.set(null);
          if (!this.canEdit()) {
            this.isAccessDenied.set(true);
            return;
          }
          this.initNewRecord();
        }
      })
    );
  }

  ngOnDestroy(): void {
    this.subs.unsubscribe();
  }

  initModeAndData(): void {
    if (!this.canView()) {
      this.isAccessDenied.set(true);
      return;
    }
    if (this.mode() === 'create' && !this.canEdit()) {
      this.isAccessDenied.set(true);
      return;
    }

    this.error.set(null);
    this.isAccessDenied.set(false);
    this.loadWarehouses();
    this.loadAvailableReceipts();
  }

  private initNewRecord(): void {
    const nextNum = this.putawayService.getNextPutawayNumber();
    const qp = this.route.snapshot?.queryParamMap;
    const preselectedType = (qp?.get('sourceType') || 'StockReceipt') as PutawaySourceDocType;
    const preselectedId = qp?.get('sourceId');

    this.selectedDocType.set(preselectedType);
    this.headerForm.patchValue({
      putawayNumber: nextNum,
      putawayDate: new Date().toISOString().substring(0, 10),
      sourceDocumentType: preselectedType,
      sourceReceiptId: preselectedId || '',
      warehouseId: '',
      notes: ''
    });
    this.workingLines.set([]);

    if (preselectedId) {
      this.loadAvailableReceipts(preselectedId, preselectedType);
    }
  }

  private loadExistingRecord(id: string): void {
    this.loading.set(true);
    this.error.set(null);

    this.putawayService.getPutawayById(id).subscribe({
      next: rec => {
        if (!rec) {
          this.error.set('Stores Putaway record not found.');
          this.loading.set(false);
          return;
        }

        this.currentRecord.set(rec);
        this.selectedDocType.set(rec.sourceDocumentType);
        this.headerForm.patchValue({
          putawayNumber: rec.putawayNumber,
          putawayDate: rec.putawayDate ? new Date(rec.putawayDate).toISOString().substring(0, 10) : '',
          sourceDocumentType: rec.sourceDocumentType,
          sourceReceiptId: rec.sourceDocumentId || '',
          warehouseId: rec.warehouseId,
          notes: rec.notes || ''
        });

        this.workingLines.set(rec.lines.map(l => ({ ...l })));

        if (rec.warehouseId) {
          this.loadWarehouseLocations(rec.warehouseId);
        }
        this.loading.set(false);
      },
      error: err => {
        this.loading.set(false);
        if (isForbiddenError(err)) {
          this.isAccessDenied.set(true);
        } else {
          this.error.set(toUserFacingErrorMessage(err, 'Failed to load putaway details.'));
        }
      }
    });
  }

  private loadWarehouses(): void {
    this.masterDataService.getWarehouses().subscribe({
      next: whs => {
        const active = (whs || []).filter(w => w.isActive);
        this.warehouses.set(active);
        if (active.length > 0 && !this.headerForm.controls.warehouseId.value) {
          this.headerForm.patchValue({ warehouseId: active[0].warehouseId });
          this.loadWarehouseLocations(active[0].warehouseId);
        }
      },
      error: err => {
        this.warehouses.set([]);
        if (isForbiddenError(err)) {
          this.isAccessDenied.set(true);
        } else {
          this.error.set(toUserFacingErrorMessage(err, 'Failed to load warehouse facilities.'));
        }
      }
    });
  }

  private loadWarehouseLocations(whId: string): void {
    if (!whId) {
      this.warehouseLocations.set([]);
      return;
    }

    this.masterDataService.getWarehouseLocations(whId).subscribe({
      next: locs => {
        const loaded = locs || [];
        this.warehouseLocations.set(loaded);

        // If lines are already loaded, ensure destination IDs are valid and not identical to source
        const lines = this.workingLines();
        if (lines.length > 0) {
          const updated = lines.map(l => {
            if (!l.destinationLocationId || l.destinationLocationId === l.sourceLocationId) {
              const defaultDest = loaded.find(
                loc => loc.isActive &&
                       (loc.warehouseLocationId ?? loc.locationId) !== l.sourceLocationId &&
                       !loc.isReceivingLocation &&
                       loc.canPutAway !== false &&
                       loc.canStoreInventory !== false
              );
              return {
                ...l,
                destinationLocationId: defaultDest ? (defaultDest.warehouseLocationId ?? defaultDest.locationId) : ''
              };
            }
            return l;
          });
          this.workingLines.set(updated);
        }
      },
      error: err => {
        this.warehouseLocations.set([]);
        if (isForbiddenError(err)) {
          this.isAccessDenied.set(true);
        }
      }
    });
  }

  private loadAvailableReceipts(preselectedId?: string, preselectedType?: PutawaySourceDocType): void {
    this.putawayService.getAvailableSourceReceipts().subscribe({
      next: list => {
        this.availableReceipts.set(list || []);
        if (preselectedId) {
          this.onSourceReceiptSelect(preselectedId, preselectedType);
        }
      },
      error: err => {
        this.availableReceipts.set([]);
        if (isForbiddenError(err)) {
          this.isAccessDenied.set(true);
        } else {
          this.error.set(toUserFacingErrorMessage(err, 'Failed to load available source receipts.'));
        }
      }
    });
  }

  onSourceDocTypeChange(type: string): void {
    this.selectedDocType.set(type);
    this.headerForm.controls.sourceDocumentType.setValue(type as PutawaySourceDocType, { emitEvent: false });
    this.headerForm.controls.sourceReceiptId.setValue('', { emitEvent: false });
    this.workingLines.set([]);
  }

  onWarehouseSelectChange(event: Event): void {
    const target = event.target as HTMLSelectElement;
    if (target) {
      this.onWarehouseChange(target.value);
    }
  }

  onWarehouseChange(whId: string): void {
    this.headerForm.controls.warehouseId.setValue(whId, { emitEvent: false });
    this.loadWarehouseLocations(whId);
  }

  onSourceReceiptChange(event: Event): void {
    const target = event.target as HTMLSelectElement;
    if (target) {
      this.onSourceReceiptSelect(target.value);
    }
  }

  onSourceReceiptSelect(receiptId: string, docTypeFallback?: PutawaySourceDocType): void {
    this.headerForm.controls.sourceReceiptId.setValue(receiptId, { emitEvent: false });
    if (!receiptId) {
      this.workingLines.set([]);
      return;
    }

    const match = this.availableReceipts().find(r => r.id === receiptId);
    const docType = match ? match.type : (docTypeFallback || this.selectedDocType() as PutawaySourceDocType);
    const receiptNumber = match ? match.number : receiptId;

    if (match?.warehouseId) {
      this.headerForm.controls.warehouseId.setValue(match.warehouseId, { emitEvent: false });
      this.loadWarehouseLocations(match.warehouseId);
    }

    this.loading.set(true);
    this.putawayService.getLinesForSourceReceipt(
      receiptId,
      receiptNumber,
      docType,
      match?.companyId,
      match?.branchId
    ).subscribe({
      next: lines => {
        this.loading.set(false);
        // Pre-assign default destination location if available:
        // Must be active, not receiving location, allow putaway & storage, AND NOT be identical to source
        const mapped = lines.map(l => {
          const defaultDest = this.warehouseLocations().find(
            loc => loc.isActive &&
                   (loc.warehouseLocationId ?? loc.locationId) !== l.sourceLocationId &&
                   !loc.isReceivingLocation &&
                   loc.canPutAway !== false &&
                   loc.canStoreInventory !== false
          );
          return {
            ...l,
            sourceLocationName: l.sourceLocationName || '',
            destinationLocationId: defaultDest ? (defaultDest.warehouseLocationId ?? defaultDest.locationId) : ''
          };
        });
        this.workingLines.set(mapped);
      },
      error: () => {
        this.loading.set(false);
      }
    });
  }

  onLinesChange(updated: EditablePutawayLine[]): void {
    this.workingLines.set([...updated]);
  }

  formatLocationHierarchy(loc: WarehouseLocationItem): string {
    if (!loc) return '';
    if (loc.hierarchyPath) {
      return `${loc.hierarchyPath} (${loc.locationName || loc.locationCode})`;
    }
    if (loc.zoneName) {
      return `${loc.zoneName} / ${loc.locationCode} (${loc.locationName})`;
    }
    if (loc.locationName && loc.locationName !== loc.locationCode) {
      return `${loc.locationCode} — ${loc.locationName}`;
    }
    return loc.locationCode;
  }

  splitLine(targetLineId?: string): void {
    const lines = [...this.workingLines()];
    if (lines.length === 0) return;

    let targetIdx = 0;
    if (targetLineId) {
      targetIdx = lines.findIndex(l => l.lineId === targetLineId);
      if (targetIdx === -1) targetIdx = 0;
    }

    const target = lines[targetIdx];
    const key = target.sourceLineId || target.productId;
    const sameLines = lines.filter(l => (l.sourceLineId || l.productId) === key);
    const usedQty = sameLines.reduce((sum, l) => sum + (Number(l.putawayQuantity) || 0), 0);
    const remaining = Math.max(0, target.outstandingQuantity - usedQty);

    const splitQuantity = remaining > 0 ? remaining : Math.max(1, Math.floor(Number(target.putawayQuantity) / 2));
    if (remaining === 0 && Number(target.putawayQuantity) > 1) {
      target.putawayQuantity = Number(target.putawayQuantity) - splitQuantity;
    }

    const newLine: EditablePutawayLine = {
      ...target,
      lineId: `pal-split-${Date.now()}-${Math.random().toString(36).substring(2, 5)}`,
      putawayQuantity: splitQuantity,
      destinationLocationId: '',
      destinationLocationCode: '',
      destinationLocationName: ''
    };

    lines.splice(targetIdx + 1, 0, newLine);
    this.workingLines.set(lines);

    this.feedbackMessage.set({
      type: 'success',
      text: `Added second bin allocation line for ${target.productCode}. Please select the destination location.`
    });
  }

  confirmExistingDraft(): void {
    const id = this.putawayId();
    if (!id) return;

    this.saving.set(true);
    this.feedbackMessage.set(null);

    this.putawayService.confirmPutaway(id).subscribe({
      next: updated => {
        this.saving.set(false);
        this.currentRecord.set(updated);
        this.feedbackMessage.set({
          type: 'success',
          text: `Stores Putaway ${updated.putawayNumber} confirmed and inventory movements posted successfully.`
        });
        setTimeout(() => {
          this.router.navigate(['/app/inventory/store-putaway']);
        }, 1200);
      },
      error: err => {
        this.saving.set(false);
        this.feedbackMessage.set({
          type: 'error',
          text: toUserFacingErrorMessage(err, 'Failed to confirm putaway transaction.')
        });
      }
    });
  }

  saveDraft(): void {
    this.savePutaway(true);
  }

  confirmPutaway(): void {
    this.savePutaway(false);
  }

  private savePutaway(asDraft: boolean): void {
    const raw = this.headerForm.getRawValue();
    const lines = this.workingLines();

    if (!raw.warehouseId) {
      this.feedbackMessage.set({ type: 'error', text: 'Please select a warehouse facility.' });
      return;
    }

    if (lines.length === 0) {
      this.feedbackMessage.set({ type: 'error', text: 'At least one product line is required for putaway.' });
      return;
    }

    // Validate quantities per product across split rows
    const qtyBySource = new Map<string, number>();
    for (const line of lines) {
      const key = line.sourceLineId || line.productId;
      qtyBySource.set(key, (qtyBySource.get(key) || 0) + (Number(line.putawayQuantity) || 0));
    }

    for (const line of lines) {
      const key = line.sourceLineId || line.productId;
      const totalQty = qtyBySource.get(key) || 0;
      if (totalQty > line.outstandingQuantity + 0.0001) {
        this.feedbackMessage.set({
          type: 'error',
          text: `Total putaway quantity (${totalQty}) exceeds outstanding quantity (${line.outstandingQuantity}) for item ${line.productCode || line.productName}.`
        });
        return;
      }
    }

    // Validation for confirm mode
    if (!asDraft) {
      for (const line of lines) {
        if (!line.destinationLocationId) {
          const avail = this.getDestinationOptionsForLine(line);
          const reason = avail.length === 0
            ? 'No eligible destination storage bins exist in this warehouse distinct from the source location. Destination location is required.'
            : 'Destination location is required.';
          this.feedbackMessage.set({
            type: 'error',
            text: `${reason} Please select a valid destination storage bin for item ${line.productCode || line.productName}.`
          });
          return;
        }
        if (line.sourceLocationId && line.destinationLocationId && line.sourceLocationId === line.destinationLocationId) {
          this.feedbackMessage.set({
            type: 'error',
            text: `Source and destination locations cannot be identical for item ${line.productCode || line.productName}. Please select a different destination storage bin.`
          });
          return;
        }
        if (line.putawayQuantity <= 0) {
          this.feedbackMessage.set({
            type: 'error',
            text: `Putaway quantity must be greater than zero for item ${line.productCode}.`
          });
          return;
        }
      }
    }

    this.saving.set(true);
    this.feedbackMessage.set(null);

    const matchReceipt = this.availableReceipts().find(r => r.id === raw.sourceReceiptId);

    const request: CreatePutawayRequest = {
      putawayNumber: raw.putawayNumber === '(Auto-generated)' ? undefined : raw.putawayNumber,
      putawayDate: raw.putawayDate,
      sourceDocumentType: raw.sourceDocumentType,
      sourceDocumentId: raw.sourceReceiptId || undefined,
      sourceDocumentNumber: matchReceipt?.number || undefined,
      warehouseId: raw.warehouseId,
      companyId: this.scopeService.selectedCompany()?.id,
      branchId: this.scopeService.selectedBranch()?.id,
      notes: raw.notes,
      lines: lines.map(l => ({
        sourceLineId: l.sourceLineId,
        productId: l.productId,
        productCode: l.productCode,
        productName: l.productName,
        unitOfMeasure: l.unitOfMeasure,
        sourceQuantity: l.sourceQuantity,
        alreadyPutawayQuantity: l.alreadyPutawayQuantity,
        outstandingQuantity: l.outstandingQuantity,
        putawayQuantity: l.putawayQuantity,
        sourceLocationId: l.sourceLocationId,
        sourceLocationCode: l.sourceLocationCode,
        destinationLocationId: l.destinationLocationId,
        destinationWarehouseLocationId: l.destinationLocationId,
        batchNumber: l.batchNumber,
        serialNumber: l.serialNumber,
        notes: l.notes
      }))
    };

    this.putawayService.createPutaway(request, asDraft).subscribe({
      next: saved => {
        this.saving.set(false);
        this.currentRecord.set(saved);
        const actionVerb = asDraft ? 'saved as draft' : 'confirmed and stock relocated';
        this.feedbackMessage.set({
          type: 'success',
          text: `Stores Putaway ${saved.putawayNumber} ${actionVerb} successfully.`
        });

        setTimeout(() => {
          this.router.navigate(['/app/inventory/store-putaway']);
        }, 1200);
      },
      error: err => {
        this.saving.set(false);
        this.feedbackMessage.set({
          type: 'error',
          text: toUserFacingErrorMessage(err, 'Failed to process stores putaway.')
        });
      }
    });
  }

  canView(): boolean {
    if (this.authService.isTenantAdmin() || this.authService.isPlatformAdmin() || this.authService.isPlatformStaff()) {
      return true;
    }
    return this.authService.hasPermission('INVENTORY.VIEW');
  }

  canEdit(): boolean {
    if (this.isReadOnlyMode() && this.isCompleted()) return false;
    if (this.authService.isTenantAdmin() || this.authService.isPlatformAdmin()) {
      return true;
    }
    return this.authService.hasPermission('PUTAWAY.POST');
  }
}
