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
    { label: 'Stores Putaway', routerLink: '/app/inventory/store-putaway' },
    { label: this.mode() === 'create' ? 'New Putaway' : (this.currentRecord()?.putawayNumber || 'Putaway Details') }
  ]);

  // Page Title & Subtitle
  readonly pageTitle = computed(() => {
    if (this.mode() === 'create') return 'New Stores Putaway';
    const num = this.currentRecord()?.putawayNumber;
    return num ? `Stores Putaway — ${num}` : 'Stores Putaway Details';
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

  // Destination Location Options for editable table
  readonly destinationLocationOptions = computed<SelectOption[]>(() => {
    return this.warehouseLocations().map(l => ({
      label: this.formatLocationHierarchy(l),
      value: l.warehouseLocationId ?? l.locationId
    }));
  });

  // Detail Columns
  readonly detailColumns = computed<DataTableColumn<EditablePutawayLine>[]>(() => {
    const isReadOnly = this.isReadOnlyMode();

    return [
      {
        field: 'productCode',
        header: 'Product / SKU',
        width: '260px',
        editable: false,
        format: (_val, row) => `${row.productCode} — ${row.productName}`
      },
      {
        field: 'unitOfMeasure',
        header: 'UOM',
        width: '80px',
        editable: false,
        align: 'center'
      },
      {
        field: 'sourceQuantity',
        header: 'Received Qty',
        width: '120px',
        editable: false,
        align: 'right',
        format: val => Number(val || 0).toLocaleString()
      },
      {
        field: 'alreadyPutawayQuantity',
        header: 'Already Put Away',
        width: '130px',
        editable: false,
        align: 'right',
        format: val => Number(val || 0).toLocaleString()
      },
      {
        field: 'outstandingQuantity',
        header: 'Outstanding Qty',
        width: '130px',
        editable: false,
        align: 'right',
        format: val => Number(val || 0).toLocaleString()
      },
      {
        field: 'putawayQuantity',
        header: 'Putaway Qty *',
        width: '130px',
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
        header: 'Destination Location (Zone → Rack → Bin) *',
        width: '320px',
        editable: !isReadOnly,
        editType: 'select',
        editOptions: this.destinationLocationOptions(),
        format: val => {
          const loc = this.warehouseLocations().find(
            l => (l.warehouseLocationId ?? l.locationId) === val
          );
          return loc ? this.formatLocationHierarchy(loc) : (val || 'Select bin...');
        },
        validators: [
          val => (val ? null : 'Destination storage bin is required')
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
          this.initNewRecord();
        }
      })
    );
  }

  ngOnDestroy(): void {
    this.subs.unsubscribe();
  }

  private initModeAndData(): void {
    if (!this.canView()) {
      this.isAccessDenied.set(true);
      return;
    }

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
        this.error.set(err?.message || 'Failed to load putaway details.');
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
        this.warehouseLocations.set(locs || []);
      },
      error: () => {
        this.warehouseLocations.set([]);
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
        // Pre-assign default destination location if available
        const defaultDest = this.warehouseLocations().find(l => !l.isReceivingLocation && l.isActive);
        const mapped = lines.map(l => ({
          ...l,
          destinationLocationId: defaultDest ? (defaultDest.warehouseLocationId ?? defaultDest.locationId) : ''
        }));
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
    if (loc.zoneName) {
      return `${loc.zoneName} → ${loc.locationCode} (${loc.locationName})`;
    }
    const segs = loc.locationCode.split('-');
    if (segs.length >= 3) {
      return `${loc.locationCode} (Rack ${segs[0]} → Level ${segs[1]} → Bin ${segs[2]})`;
    }
    return `${loc.locationCode} — ${loc.locationName}`;
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
          text: err?.error?.message || err?.message || 'Failed to confirm putaway transaction.'
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
          this.feedbackMessage.set({
            type: 'error',
            text: `Destination location is required for item ${line.productCode || line.productName}.`
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
          text: err?.error?.message || err?.message || 'Failed to process stores putaway.'
        });
      }
    });
  }

  canView(): boolean {
    if (this.authService.isTenantAdmin() || this.authService.isPlatformAdmin() || this.authService.isPlatformStaff()) {
      return true;
    }
    return this.authService.hasPermission('WAREHOUSE.VIEW') || this.authService.hasPermission('PRODUCT.VIEW');
  }

  canEdit(): boolean {
    if (this.isReadOnlyMode() && this.isCompleted()) return false;
    if (this.authService.isTenantAdmin() || this.authService.isPlatformAdmin()) {
      return true;
    }
    return this.authService.hasPermission('WAREHOUSE.MANAGE_LOCATIONS') || this.authService.hasPermission('PRODUCT.EDIT');
  }
}
