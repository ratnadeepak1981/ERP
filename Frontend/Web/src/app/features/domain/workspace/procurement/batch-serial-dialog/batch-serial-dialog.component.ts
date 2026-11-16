import {
  Component,
  Inject,
  signal,
  computed,
  ChangeDetectionStrategy,
  OnInit,
  inject
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatDialogRef, MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { AppMaterialEditableDataTableComponent } from '../../../../../shared/components/material-editable-data-table/core/app-material-editable-data-table.component';
import { MaterialTableColumn, MaterialSelectOption } from '../../../../../shared/components/material-editable-data-table/models/material-data-table.models';
import { BatchSerialAllocation } from '../../../../../core/models/goods-receipt-note.models';
import { TrackingMode } from '../../../../../core/models/scope.models';
import { InventoryService } from '../../../../../core/services/inventory.service';
import { EligibleBatchDto } from '../../../../../core/models/inventory.models';

export interface BatchSerialDialogData {
  productCode: string;
  productName: string;
  unitOfMeasure: string;
  trackingMode: number;
  targetQuantity: number;
  readOnly: boolean;
  allocations: BatchSerialAllocation[];
  // Outbound / Issue mode fields
  dialogMode?: 'receipt' | 'issue';
  transactionTitle?: string;
  productId?: string;
  warehouseId?: string;
  warehouseLocationId?: string;
}

@Component({
  selector: 'app-batch-serial-dialog',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatDialogModule,
    MatButtonModule,
    MatIconModule,
    AppMaterialEditableDataTableComponent
  ],
  template: `
    <div class="erp-batch-serial-dialog">
      <!-- Standard Transaction Dialog Header -->
      <header class="erp-dialog-header">
        <div class="header-main">
          <div class="header-badge-row">
            <span class="dialog-type-pill">{{ getTransactionTypeName() }}</span>
            <span class="dialog-tracking-pill">{{ getTrackingModeName() }}</span>
          </div>
          <h2 mat-dialog-title class="dialog-title">Batch / Serial Tracking</h2>
          <div class="dialog-subcontext">
            <span class="product-id-badge">{{ data.productCode }}</span>
            <span class="product-name-text">{{ data.productName }}</span>
            <span class="requirement-bullet">•</span>
            <span class="required-qty-text">
              Required: <strong>{{ data.targetQuantity | number:'1.0-4' }} {{ data.unitOfMeasure }}</strong>
            </span>
          </div>
        </div>
        <button
          type="button"
          class="erp-dialog-close-btn"
          (click)="onDismiss()"
          aria-label="Close dialog"
          title="Close dialog"
        >
          ✕
        </button>
      </header>

      <!-- Quick Bulk Serial Addition (Only in Receipt Mode when Serial or BatchAndSerial tracking) -->
      @if (!data.readOnly && !isIssueMode() && (isSerialMode() || isBatchAndSerialMode()) && remaining() > 0) {
        <section class="bulk-serial-section">
          <div class="bulk-title">
            <mat-icon class="bulk-icon">flash_on</mat-icon>
            <span>Quick Serial Generator:</span>
          </div>
          <div class="bulk-inputs">
            <input
              type="text"
              class="erp-input bulk-text-input"
              placeholder="Prefix (e.g. SN-)"
              [(ngModel)]="bulkPrefix"
            />
            <input
              type="number"
              class="erp-input bulk-number-input"
              placeholder="Count"
              [min]="1"
              [max]="remaining()"
              [(ngModel)]="bulkCount"
            />
            @if (isBatchAndSerialMode()) {
              <input
                type="text"
                class="erp-input bulk-text-input"
                placeholder="Batch Number"
                [(ngModel)]="bulkBatchNumber"
              />
            }
            <button
              type="button"
              class="erp-btn erp-btn-secondary bulk-add-btn"
              (click)="generateSerials()"
              [disabled]="bulkCount <= 0"
            >
              Add {{ bulkCount }} {{ bulkCount === 1 ? 'Serial' : 'Serials' }}
            </button>
          </div>
        </section>
      }

      <!-- Outbound Issue Guidance / Discovery Banner -->
      @if (isIssueMode()) {
        <div class="issue-guidance-bar">
          <mat-icon class="info-icon">info</mat-icon>
          <div class="guidance-text">
            @if (isLoadingEligibleBatches()) {
              <span>Loading eligible batches for stock issue...</span>
            } @else if (isBatchMode() || isBatchAndSerialMode()) {
              @if (eligibleBatches().length === 0) {
                <span class="warn-text">No eligible batches found in stock for this product and location.</span>
              } @else {
                <span>
                  Select from <strong>{{ eligibleBatches().length }}</strong> eligible batches ordered by FEFO. Available quantity is capped per batch.
                </span>
              }
            } @else {
              <span>
                Enter eligible serial numbers (quantity 1 per serial). Discovery endpoint for on-hand serials is currently not supported by backend API.
              </span>
            }
          </div>
        </div>
      }

      <!-- Main Body: Editable Data Grid -->
      <mat-dialog-content class="dialog-content">
        <app-material-editable-data-table
          [data]="workingAllocations()"
          [columns]="columns"
          rowKey="id"
          [allowAddRow]="!data.readOnly"
          [addRowLabel]="getAddRowLabel()"
          [allowDeleteRow]="!data.readOnly"
          [showToolbar]="!data.readOnly"
          [showSaveActions]="false"
          [showRowNumbers]="true"
          [showFooter]="false"
          [defaultNewRow]="createNewRow"
          (dataChange)="onAllocationsChange($event)"
        ></app-material-editable-data-table>

        <!-- Inline Validation Errors -->
        @if (validationErrors().length > 0) {
          <div class="validation-error-panel" role="alert">
            <mat-icon class="err-icon">error_outline</mat-icon>
            <div class="err-messages">
              @for (err of validationErrors(); track err) {
                <div class="err-item">{{ err }}</div>
              }
            </div>
          </div>
        }
      </mat-dialog-content>

      <!-- Standard Transaction Footer (Status, Quantities & Action Buttons) -->
      <footer class="erp-dialog-footer">
        <div class="footer-quantities-bar">
          <div class="footer-stat">
            <span class="stat-label">Required Qty</span>
            <span class="stat-val required-val">{{ data.targetQuantity | number:'1.0-4' }} {{ data.unitOfMeasure }}</span>
          </div>
          <div class="footer-divider"></div>
          <div class="footer-stat">
            <span class="stat-label">Allocated Qty</span>
            <span class="stat-val" [class.alloc-complete]="isComplete()" [class.alloc-incomplete]="!isComplete()">
              {{ totalAllocated() | number:'1.0-4' }} {{ data.unitOfMeasure }}
            </span>
          </div>
          <div class="footer-divider"></div>
          <div class="footer-stat">
            <span class="stat-label">Remaining Qty</span>
            <span class="stat-val" [class.balance-zero]="remaining() === 0" [class.balance-pending]="remaining() !== 0">
              {{ remaining() | number:'1.0-4' }} {{ data.unitOfMeasure }}
            </span>
          </div>
        </div>

        <div class="footer-actions-group">
          <button
            type="button"
            class="erp-btn erp-btn-secondary"
            (click)="onDismiss()"
          >
            {{ data.readOnly ? 'Close' : 'Cancel' }}
          </button>

          @if (!data.readOnly) {
            <button
              type="button"
              class="erp-btn erp-btn-primary"
              (click)="onApply()"
              [disabled]="!canApply()"
            >
              Apply Tracking Details
            </button>
          }
        </div>
      </footer>
    </div>
  `,
  styleUrls: ['./batch-serial-dialog.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class BatchSerialTrackingDialogComponent implements OnInit {
  private readonly inventoryService = inject(InventoryService);

  readonly workingAllocations = signal<BatchSerialAllocation[]>([]);
  readonly eligibleBatches = signal<EligibleBatchDto[]>([]);
  readonly isLoadingEligibleBatches = signal<boolean>(false);

  bulkPrefix: string = 'SN-';
  bulkCount: number = 1;
  bulkBatchNumber: string = '';

  columns: MaterialTableColumn<BatchSerialAllocation>[] = [];

  constructor(
    public dialogRef: MatDialogRef<BatchSerialTrackingDialogComponent>,
    @Inject(MAT_DIALOG_DATA) public data: BatchSerialDialogData
  ) {}

  ngOnInit(): void {
    // Clone allocations to isolate dialog edits
    const initial = (this.data.allocations || []).map(a => ({
      ...a,
      id: a.id || 'alloc-' + Math.random().toString(36).substring(2, 9),
      quantity: Number(a.quantity) || 1,
      manufacturingDate: this.sanitizeDate(a.manufacturingDate),
      expiryDate: this.sanitizeDate(a.expiryDate)
    }));

    // Auto-seed initial batch allocation row in receipt mode if no allocations exist
    if (!this.data.readOnly && !this.isIssueMode() && this.isBatchMode() && initial.length === 0 && this.data.targetQuantity > 0) {
      initial.push({
        id: 'alloc-' + Math.random().toString(36).substring(2, 9),
        batchNumber: '',
        serialNumber: '',
        quantity: this.data.targetQuantity,
        manufacturingDate: '',
        expiryDate: '',
        isQuarantine: false,
        remarks: ''
      });
    } else if (!this.data.readOnly && !this.isIssueMode() && this.isBatchMode() && initial.length === 1 && (!initial[0].quantity || Number(initial[0].quantity) <= 0)) {
      initial[0].quantity = this.data.targetQuantity;
    }

    this.workingAllocations.set(initial);

    // Initial default count for bulk entry
    const rem = Math.max(0, this.data.targetQuantity - this.calculateTotal(initial));
    this.bulkCount = rem > 0 ? rem : 1;

    // Load eligible batches if in issue mode and tracked by batch
    if (this.isIssueMode() && (this.isBatchMode() || this.isBatchAndSerialMode())) {
      this.loadEligibleBatches();
    } else {
      this.setupColumns();
    }
  }

  isIssueMode(): boolean {
    return this.data.dialogMode === 'issue';
  }

  getTransactionTypeName(): string {
    if (this.data.transactionTitle) return this.data.transactionTitle;
    return this.isIssueMode() ? 'Stock Issue' : 'Stock Receipt';
  }

  getAddRowLabel(): string {
    if (this.isSerialMode()) return 'Add Serial';
    if (this.isBatchAndSerialMode()) return 'Add Batch + Serial Allocation';
    return 'Add Batch Allocation';
  }

  private sanitizeDate(val: any): string {
    if (!val) return '';
    if (typeof val === 'string') {
      if (val.length >= 10 && val[4] === '-' && val[7] === '-') {
        return val.substring(0, 10);
      }
      const d = new Date(val);
      if (!isNaN(d.getTime())) {
        return d.toISOString().substring(0, 10);
      }
    } else if (val instanceof Date && !isNaN(val.getTime())) {
      return val.toISOString().substring(0, 10);
    }
    return '';
  }

  private loadEligibleBatches(): void {
    if (!this.data.productId) {
      this.setupColumns();
      return;
    }

    this.isLoadingEligibleBatches.set(true);
    this.inventoryService
      .getEligibleBatches(
        this.data.productId,
        this.data.warehouseId,
        this.data.warehouseLocationId
      )
      .subscribe({
        next: batches => {
          this.eligibleBatches.set(batches || []);
          this.isLoadingEligibleBatches.set(false);
          this.setupColumns();
        },
        error: () => {
          this.eligibleBatches.set([]);
          this.isLoadingEligibleBatches.set(false);
          this.setupColumns();
        }
      });
  }

  isBatchMode(): boolean {
    return this.data.trackingMode === TrackingMode.Batch;
  }

  isSerialMode(): boolean {
    return this.data.trackingMode === TrackingMode.Serial;
  }

  isBatchAndSerialMode(): boolean {
    return this.data.trackingMode === TrackingMode.BatchAndSerial;
  }

  getTrackingModeName(): string {
    switch (this.data.trackingMode) {
      case TrackingMode.Batch:
        return 'Tracking: Batch';
      case TrackingMode.Serial:
        return 'Tracking: Serial';
      case TrackingMode.BatchAndSerial:
        return 'Tracking: Batch & Serial';
      default:
        return 'Untracked';
    }
  }

  private calculateTotal(allocs: BatchSerialAllocation[]): number {
    return allocs.reduce((sum, item) => sum + (Number(item.quantity) || 0), 0);
  }

  readonly totalAllocated = computed<number>(() => {
    return this.calculateTotal(this.workingAllocations());
  });

  readonly remaining = computed<number>(() => {
    const rem = Math.round((this.data.targetQuantity - this.totalAllocated()) * 10000) / 10000;
    return rem;
  });

  readonly isComplete = computed<boolean>(() => {
    return Math.abs(this.data.targetQuantity - this.totalAllocated()) < 0.0001;
  });

  readonly validationErrors = computed<string[]>(() => {
    const errors: string[] = [];
    const allocs = this.workingAllocations();
    const isIssue = this.isIssueMode();
    const eligible = this.eligibleBatches();

    if (allocs.length === 0 && this.data.targetQuantity > 0) {
      errors.push('At least one tracking line is required.');
      return errors;
    }

    if (!this.isComplete()) {
      errors.push(
        `Total allocated quantity (${this.totalAllocated()}) must equal required ${isIssue ? 'issued' : 'received'} quantity (${this.data.targetQuantity}). Difference: ${this.remaining()}.`
      );
    }

    const seenSerials = new Set<string>();
    const allocatedPerBatch = new Map<string, number>();

    for (let i = 0; i < allocs.length; i++) {
      const a = allocs[i];
      const lineNum = i + 1;
      const qty = Number(a.quantity) || 0;

      // Batch validation
      if (this.isBatchMode() || this.isBatchAndSerialMode()) {
        if (!a.batchNumber || !a.batchNumber.trim()) {
          errors.push(`Line #${lineNum}: Batch Number is required.`);
        } else if (isIssue && eligible.length > 0) {
          const normBatch = a.batchNumber.trim().toUpperCase();
          const match = eligible.find(b => b.batchNumber.trim().toUpperCase() === normBatch);
          if (!match) {
            errors.push(`Line #${lineNum}: Batch '${a.batchNumber}' is not an eligible active on-hand batch.`);
          } else {
            const currentTotal = (allocatedPerBatch.get(normBatch) || 0) + qty;
            allocatedPerBatch.set(normBatch, currentTotal);
            if (currentTotal > match.quantityAvailable) {
              errors.push(
                `Batch '${match.batchNumber}': Total allocated quantity (${currentTotal}) exceeds available stock (${match.quantityAvailable}).`
              );
            }
          }
        }
      }

      // Serial validation
      if (this.isSerialMode() || this.isBatchAndSerialMode()) {
        if (!a.serialNumber || !a.serialNumber.trim()) {
          errors.push(`Line #${lineNum}: Serial Number is required.`);
        } else {
          const normSn = a.serialNumber.trim().toUpperCase();
          if (seenSerials.has(normSn)) {
            errors.push(`Duplicate serial number '${a.serialNumber}' found on Line #${lineNum}.`);
          } else {
            seenSerials.add(normSn);
          }
        }

        if (Number(a.quantity) !== 1) {
          errors.push(`Line #${lineNum}: Quantity for serial-tracked item must be 1. Current: ${a.quantity}.`);
        }
      }

      // Quantity validation
      if (Number(a.quantity) <= 0) {
        errors.push(`Line #${lineNum}: Quantity must be greater than zero.`);
      }

      // Date validation
      if (a.manufacturingDate && a.expiryDate) {
        const mfg = new Date(a.manufacturingDate);
        const exp = new Date(a.expiryDate);
        if (!isNaN(mfg.getTime()) && !isNaN(exp.getTime()) && exp < mfg) {
          errors.push(`Line #${lineNum}: Expiry date cannot be earlier than manufacturing date.`);
        }
      }
    }

    return errors;
  });

  readonly canApply = computed<boolean>(() => {
    return this.isComplete() && this.validationErrors().length === 0;
  });

  private getBatchOptions(): MaterialSelectOption[] {
    const batches = this.eligibleBatches();
    if (!batches || batches.length === 0) {
      return [{ label: '-- No Eligible Batches Found --', value: '' }];
    }
    return batches.map(b => {
      const expStr = b.expiryDate ? new Date(b.expiryDate).toLocaleDateString() : 'No Exp';
      return {
        value: b.batchNumber,
        label: `${b.batchNumber} (Avail: ${b.quantityAvailable}, Exp: ${expStr})`,
        subtitle: `Location: ${b.locationCode || 'Default'} | On Hand: ${b.quantityOnHand}`,
        rawItem: b
      };
    });
  }

  private setupColumns(): void {
    const isView = this.data.readOnly;
    const isIssue = this.isIssueMode();
    const useBatchSelect = isIssue && !isView && this.eligibleBatches().length > 0;

    if (this.isBatchMode()) {
      if (isIssue) {
        // Issue Mode Table Structure
        this.columns = [
          {
            field: 'batchNumber',
            header: 'Batch Number *',
            type: useBatchSelect ? 'select' : 'text',
            options: useBatchSelect ? this.getBatchOptions() : undefined,
            width: '240px',
            required: true,
            editable: !isView,
            placeholder: 'Select eligible batch...'
          },
          {
            field: 'availableQuantity',
            header: 'Available Qty',
            type: 'number',
            width: '120px',
            align: 'right',
            editable: false,
            format: (_val, row) => {
              const match = this.eligibleBatches().find(b => b.batchNumber === row.batchNumber);
              return match ? `${match.quantityAvailable} ${this.data.unitOfMeasure}` : '—';
            }
          },
          {
            field: 'quantity',
            header: `Issue Qty (${this.data.unitOfMeasure}) *`,
            type: 'number',
            width: '130px',
            align: 'right',
            required: true,
            editable: !isView,
            min: 0.0001
          },
          {
            field: 'manufacturingDate',
            header: 'Mfg Date',
            type: 'date',
            width: '130px',
            editable: false,
            format: (val) => val ? String(val).substring(0, 10) : '—'
          },
          {
            field: 'expiryDate',
            header: 'Expiry Date',
            type: 'date',
            width: '130px',
            editable: false,
            format: (val) => val ? String(val).substring(0, 10) : '—'
          },
          {
            field: 'remarks',
            header: 'Remarks / Notes',
            type: 'text',
            width: '160px',
            editable: !isView,
            placeholder: 'Optional notes'
          }
        ];
      } else {
        // Receipt Mode Table Structure
        this.columns = [
          {
            field: 'batchNumber',
            header: 'Batch Number *',
            type: 'text',
            width: '200px',
            required: true,
            editable: !isView,
            placeholder: 'e.g. LOT-2026-001'
          },
          {
            field: 'quantity',
            header: `Quantity (${this.data.unitOfMeasure}) *`,
            type: 'number',
            width: '130px',
            align: 'right',
            required: true,
            editable: !isView,
            min: 0.0001
          },
          {
            field: 'manufacturingDate',
            header: 'Manufacturing Date',
            type: 'date',
            width: '160px',
            editable: !isView
          },
          {
            field: 'expiryDate',
            header: 'Expiry Date',
            type: 'date',
            width: '160px',
            editable: !isView
          },
          {
            field: 'remarks',
            header: 'Remarks / Notes',
            type: 'text',
            width: '180px',
            editable: !isView,
            placeholder: 'Optional notes'
          }
        ];
      }
    } else if (this.isSerialMode()) {
      this.columns = [
        {
          field: 'serialNumber',
          header: 'Serial Number *',
          type: 'text',
          width: '260px',
          required: true,
          editable: !isView,
          placeholder: 'e.g. SN-984021'
        },
        {
          field: 'quantity',
          header: 'Quantity',
          type: 'number',
          width: '100px',
          align: 'right',
          editable: false
        },
        {
          field: 'remarks',
          header: 'Remarks / Condition',
          type: 'text',
          width: '220px',
          editable: !isView,
          placeholder: 'Optional notes'
        }
      ];
    } else {
      // Batch and Serial
      this.columns = [
        {
          field: 'batchNumber',
          header: isIssue ? 'Eligible Batch *' : 'Batch Number *',
          type: useBatchSelect ? 'select' : 'text',
          options: useBatchSelect ? this.getBatchOptions() : undefined,
          width: isIssue ? '200px' : '170px',
          required: true,
          editable: !isView,
          placeholder: isIssue ? 'Select batch...' : 'e.g. LOT-2026-001'
        },
        {
          field: 'serialNumber',
          header: 'Serial Number *',
          type: 'text',
          width: '200px',
          required: true,
          editable: !isView,
          placeholder: 'e.g. SN-984021'
        },
        {
          field: 'quantity',
          header: 'Qty',
          type: 'number',
          width: '80px',
          align: 'right',
          editable: false
        },
        {
          field: 'manufacturingDate',
          header: 'Mfg Date',
          type: 'date',
          width: '130px',
          editable: !isView && !isIssue,
          format: (val) => val ? String(val).substring(0, 10) : '—'
        },
        {
          field: 'expiryDate',
          header: 'Expiry Date',
          type: 'date',
          width: '130px',
          editable: !isView && !isIssue,
          format: (val) => val ? String(val).substring(0, 10) : '—'
        },
        {
          field: 'remarks',
          header: 'Remarks',
          type: 'text',
          width: '160px',
          editable: !isView
        }
      ];
    }

    if (!isView) {
      this.columns.push({
        field: 'actions',
        header: 'Action',
        type: 'actions',
        width: '80px',
        align: 'center'
      });
    }
  }

  readonly createNewRow = (): BatchSerialAllocation => {
    const isSerial = this.isSerialMode() || this.isBatchAndSerialMode();
    const isIssue = this.isIssueMode();
    const rem = this.remaining();
    let initialBatch = '';
    let initialMfg = '';
    let initialExp = '';

    if (this.isBatchAndSerialMode() && this.bulkBatchNumber) {
      initialBatch = this.bulkBatchNumber;
    } else if (isIssue && this.eligibleBatches().length > 0) {
      // Pre-select first eligible batch
      const first = this.eligibleBatches()[0];
      initialBatch = first.batchNumber;
      initialMfg = this.sanitizeDate(first.manufacturingDate);
      initialExp = this.sanitizeDate(first.expiryDate);
    }

    return {
      id: 'alloc-' + Math.random().toString(36).substring(2, 9),
      batchNumber: initialBatch,
      serialNumber: '',
      quantity: isSerial ? 1 : (rem > 0 ? rem : 1),
      manufacturingDate: initialMfg,
      expiryDate: initialExp,
      isQuarantine: false,
      remarks: ''
    };
  };

  onAllocationsChange(allocs: BatchSerialAllocation[]): void {
    if (this.isIssueMode() && (this.isBatchMode() || this.isBatchAndSerialMode())) {
      // Ensure date synchronization when batch is selected/changed
      const eligible = this.eligibleBatches();
      for (const a of allocs) {
        if (a.batchNumber) {
          const match = eligible.find(b => b.batchNumber === a.batchNumber);
          if (match) {
            a.manufacturingDate = this.sanitizeDate(match.manufacturingDate);
            a.expiryDate = this.sanitizeDate(match.expiryDate);
          }
        }
      }
    }
    this.workingAllocations.set(allocs);
  }

  generateSerials(): void {
    const count = Math.min(this.bulkCount, Math.max(1, this.remaining()));
    if (count <= 0) return;

    const prefix = this.bulkPrefix.trim() || 'SN-';
    const current = [...this.workingAllocations()];
    const timestamp = Date.now().toString().slice(-4);

    for (let i = 1; i <= count; i++) {
      const padIndex = String(current.length + i).padStart(3, '0');
      current.push({
        id: 'alloc-' + Math.random().toString(36).substring(2, 9),
        batchNumber: this.bulkBatchNumber.trim() || (this.isBatchAndSerialMode() ? 'LOT-' + timestamp : ''),
        serialNumber: `${prefix}${padIndex}`,
        quantity: 1,
        manufacturingDate: '',
        expiryDate: '',
        isQuarantine: false,
        remarks: ''
      });
    }

    this.workingAllocations.set(current);
    const newRem = Math.max(0, this.data.targetQuantity - this.calculateTotal(current));
    this.bulkCount = newRem > 0 ? newRem : 1;
  }

  onDismiss(): void {
    this.dialogRef.close(null);
  }

  onApply(): void {
    if (!this.canApply()) return;
    this.dialogRef.close(this.workingAllocations());
  }
}
