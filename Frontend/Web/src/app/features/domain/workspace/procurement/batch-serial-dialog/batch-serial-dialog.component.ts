import {
  Component,
  Inject,
  signal,
  computed,
  ChangeDetectionStrategy,
  OnInit
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatDialogRef, MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { AppMaterialEditableDataTableComponent } from '../../../../../shared/components/material-editable-data-table/core/app-material-editable-data-table.component';
import { MaterialTableColumn } from '../../../../../shared/components/material-editable-data-table/models/material-data-table.models';
import { BatchSerialAllocation } from '../../../../../core/models/goods-receipt-note.models';
import { TrackingMode } from '../../../../../core/models/scope.models';

export interface BatchSerialDialogData {
  productCode: string;
  productName: string;
  unitOfMeasure: string;
  trackingMode: number;
  targetQuantity: number;
  readOnly: boolean;
  allocations: BatchSerialAllocation[];
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
    <div class="fluent-batch-serial-dialog">
      <!-- Header -->
      <div class="dialog-header">
        <div class="title-wrap">
          <mat-icon class="header-icon">{{ getTrackingIcon() }}</mat-icon>
          <div class="title-text-group">
            <h2 mat-dialog-title class="dialog-title">
              {{ getTrackingTitle() }}
            </h2>
            <span class="dialog-subtitle">
              <strong>{{ data.productCode }}</strong> — {{ data.productName }}
            </span>
          </div>
        </div>
        <button mat-icon-button (click)="onDismiss()" aria-label="Close dialog" class="close-btn">
          <mat-icon>close</mat-icon>
        </button>
      </div>

      <!-- Scope / Requirement Overview Card -->
      <div class="dialog-metric-bar">
        <div class="metric-item">
          <span class="metric-label">Tracking Mode</span>
          <span class="metric-val mode-pill">{{ getTrackingModeName() }}</span>
        </div>
        <div class="metric-divider"></div>
        <div class="metric-item">
          <span class="metric-label">Target Quantity</span>
          <span class="metric-val target-val">{{ data.targetQuantity | number:'1.0-4' }} {{ data.unitOfMeasure }}</span>
        </div>
        <div class="metric-divider"></div>
        <div class="metric-item">
          <span class="metric-label">Total Allocated</span>
          <span class="metric-val" [class.valid-alloc]="isComplete()" [class.invalid-alloc]="!isComplete()">
            {{ totalAllocated() | number:'1.0-4' }} {{ data.unitOfMeasure }}
          </span>
        </div>
        <div class="metric-divider"></div>
        <div class="metric-item">
          <span class="metric-label">Remaining Required</span>
          <span class="metric-val" [class.balance-zero]="remaining() === 0" [class.balance-pending]="remaining() !== 0">
            {{ remaining() | number:'1.0-4' }} {{ data.unitOfMeasure }}
          </span>
        </div>
      </div>

      <!-- Quick Bulk Serial Addition (When Serial or BatchAndSerial tracking) -->
      @if (!data.readOnly && (isSerialMode() || isBatchAndSerialMode()) && remaining() > 0) {
        <div class="bulk-serial-box">
          <span class="bulk-title">
            <mat-icon>flash_on</mat-icon> Quick Serial Entry:
          </span>
          <div class="bulk-inputs">
            <input
              type="text"
              class="bulk-text-input"
              placeholder="Serial Prefix (e.g. SN-)"
              [(ngModel)]="bulkPrefix"
            />
            <input
              type="number"
              class="bulk-number-input"
              placeholder="Count"
              [min]="1"
              [max]="remaining()"
              [(ngModel)]="bulkCount"
            />
            @if (isBatchAndSerialMode()) {
              <input
                type="text"
                class="bulk-text-input"
                placeholder="Batch Number"
                [(ngModel)]="bulkBatchNumber"
              />
            }
            <button
              mat-stroked-button
              type="button"
              class="bulk-add-btn"
              (click)="generateSerials()"
              [disabled]="bulkCount <= 0"
            >
              Add {{ bulkCount }} {{ bulkCount === 1 ? 'Serial' : 'Serials' }}
            </button>
          </div>
        </div>
      }

      <!-- Editable Data Table Container -->
      <mat-dialog-content class="dialog-content">
        <app-material-editable-data-table
          [data]="workingAllocations()"
          [columns]="columns"
          rowKey="id"
          [allowAddRow]="!data.readOnly"
          [allowDeleteRow]="!data.readOnly"
          [showToolbar]="!data.readOnly"
          [showRowNumbers]="true"
          [showFooter]="true"
          [defaultNewRow]="createNewRow"
          (dataChange)="onAllocationsChange($event)"
        ></app-material-editable-data-table>

        <!-- Validation Errors Summary -->
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

      <!-- Dialog Actions Footer -->
      <div mat-dialog-actions class="dialog-actions">
        <button mat-button type="button" (click)="onDismiss()">
          {{ data.readOnly ? 'Close' : 'Cancel' }}
        </button>

        @if (!data.readOnly) {
          <button
            mat-flat-button
            color="primary"
            type="button"
            (click)="onApply()"
            [disabled]="!canApply()"
          >
            Apply Tracking Details
          </button>
        }
      </div>
    </div>
  `,
  styleUrls: ['./batch-serial-dialog.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class BatchSerialTrackingDialogComponent implements OnInit {
  readonly workingAllocations = signal<BatchSerialAllocation[]>([]);
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
      quantity: Number(a.quantity) || 1
    }));
    this.workingAllocations.set(initial);

    // Initial default count for bulk entry
    const rem = Math.max(0, this.data.targetQuantity - this.calculateTotal(initial));
    this.bulkCount = rem > 0 ? rem : 1;

    this.setupColumns();
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

  getTrackingIcon(): string {
    if (this.isSerialMode()) return 'qr_code';
    if (this.isBatchAndSerialMode()) return 'layers';
    return 'inventory_2';
  }

  getTrackingTitle(): string {
    if (this.isSerialMode()) return 'Serial Number Tracking';
    if (this.isBatchAndSerialMode()) return 'Batch & Serial Number Tracking';
    return 'Batch / Lot Number Tracking';
  }

  getTrackingModeName(): string {
    switch (this.data.trackingMode) {
      case TrackingMode.Batch:
        return 'Batch Tracked';
      case TrackingMode.Serial:
        return 'Serial Tracked';
      case TrackingMode.BatchAndSerial:
        return 'Batch & Serial Tracked';
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

    if (allocs.length === 0 && this.data.targetQuantity > 0) {
      errors.push('At least one tracking line is required.');
      return errors;
    }

    if (!this.isComplete()) {
      errors.push(
        `Total allocated quantity (${this.totalAllocated()}) must equal required received quantity (${this.data.targetQuantity}). Difference: ${this.remaining()}.`
      );
    }

    const seenSerials = new Set<string>();

    for (let i = 0; i < allocs.length; i++) {
      const a = allocs[i];
      const lineNum = i + 1;

      // Batch validation
      if (this.isBatchMode() || this.isBatchAndSerialMode()) {
        if (!a.batchNumber || !a.batchNumber.trim()) {
          errors.push(`Line #${lineNum}: Batch Number is required.`);
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
        if (exp < mfg) {
          errors.push(`Line #${lineNum}: Expiry date cannot be earlier than manufacturing date.`);
        }
      }
    }

    return errors;
  });

  readonly canApply = computed<boolean>(() => {
    return this.isComplete() && this.validationErrors().length === 0;
  });

  private setupColumns(): void {
    const isView = this.data.readOnly;

    if (this.isBatchMode()) {
      this.columns = [
        {
          field: 'batchNumber',
          header: 'Batch / Lot Number',
          type: 'text',
          width: '200px',
          required: true,
          editable: !isView,
          placeholder: 'e.g. LOT-2026-001'
        },
        {
          field: 'quantity',
          header: `Quantity (${this.data.unitOfMeasure})`,
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
          width: '150px',
          editable: !isView
        },
        {
          field: 'expiryDate',
          header: 'Expiry Date',
          type: 'date',
          width: '150px',
          editable: !isView
        },
        {
          field: 'remarks',
          header: 'Remarks / Notes',
          type: 'text',
          width: '200px',
          editable: !isView,
          placeholder: 'Optional notes'
        }
      ];
    } else if (this.isSerialMode()) {
      this.columns = [
        {
          field: 'serialNumber',
          header: 'Serial Number',
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
          width: '200px',
          editable: !isView,
          placeholder: 'Optional notes'
        }
      ];
    } else {
      // Batch and Serial
      this.columns = [
        {
          field: 'batchNumber',
          header: 'Batch Number',
          type: 'text',
          width: '180px',
          required: true,
          editable: !isView,
          placeholder: 'e.g. LOT-2026-001'
        },
        {
          field: 'serialNumber',
          header: 'Serial Number',
          type: 'text',
          width: '220px',
          required: true,
          editable: !isView,
          placeholder: 'e.g. SN-984021'
        },
        {
          field: 'quantity',
          header: 'Qty',
          type: 'number',
          width: '90px',
          align: 'right',
          editable: false
        },
        {
          field: 'manufacturingDate',
          header: 'Mfg Date',
          type: 'date',
          width: '140px',
          editable: !isView
        },
        {
          field: 'expiryDate',
          header: 'Expiry Date',
          type: 'date',
          width: '140px',
          editable: !isView
        },
        {
          field: 'remarks',
          header: 'Remarks',
          type: 'text',
          width: '180px',
          editable: !isView
        }
      ];
    }

    if (!isView) {
      this.columns.push({
        field: 'actions',
        header: 'Actions',
        type: 'actions',
        width: '90px',
        align: 'center'
      });
    }
  }

  readonly createNewRow = (): BatchSerialAllocation => {
    const isSerial = this.isSerialMode() || this.isBatchAndSerialMode();
    const rem = this.remaining();
    return {
      id: 'alloc-' + Math.random().toString(36).substring(2, 9),
      batchNumber: this.isBatchAndSerialMode() && this.bulkBatchNumber ? this.bulkBatchNumber : '',
      serialNumber: '',
      quantity: isSerial ? 1 : (rem > 0 ? rem : 1),
      manufacturingDate: '',
      expiryDate: '',
      isQuarantine: false,
      remarks: ''
    };
  };

  onAllocationsChange(allocs: BatchSerialAllocation[]): void {
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
