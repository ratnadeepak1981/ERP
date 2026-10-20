import {
  Component,
  Input,
  Output,
  EventEmitter,
  OnInit,
  OnChanges,
  SimpleChanges,
  signal,
  computed,
  ChangeDetectionStrategy,
  ChangeDetectorRef
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { MaterialTableColumn } from '../models/material-data-table.models';
import { TableStateService } from './table-state.service';
import { TableEditingService } from './table-editing.service';
import { TableValidationService } from './table-validation.service';
import { TableNavigationService } from './table-navigation.service';
import { TableToolbarComponent } from '../subcomponents/table-toolbar/table-toolbar.component';
import { TableCellComponent } from '../subcomponents/table-cell/table-cell.component';

@Component({
  selector: 'app-material-editable-data-table',
  standalone: true,
  imports: [CommonModule, TableToolbarComponent, TableCellComponent],
  providers: [
    TableStateService,
    TableEditingService,
    TableValidationService,
    TableNavigationService
  ],
  template: `
    <div class="fluent-material-grid-container">
      <!-- Toolbar -->
      @if (showToolbar) {
        <app-mat-table-toolbar
          [allowAddRow]="allowAddRow"
          [dirtyCount]="stateService.dirtyCount()"
          [totalLines]="stateService.rowCount()"
          (addRow)="onAddRow()"
          (save)="onSave()"
          (cancel)="onReset()"
        ></app-mat-table-toolbar>
      }

      <!-- Status / Feedback Banner -->
      @if (feedbackBanner(); as banner) {
        <div class="fluent-feedback-banner" [class]="'banner-' + banner.type">
          <span>{{ banner.message }}</span>
          <button type="button" class="close-banner-btn" (click)="feedbackBanner.set(null)">✕</button>
        </div>
      }

      <!-- Table Body -->
      <div class="fluent-table-wrapper">
        <table class="fluent-grid-table" role="grid">
          <thead>
            <tr>
              @if (showRowNumbers) {
                <th style="width: 44px; text-align: center;">#</th>
              }
              @for (col of columns; track col.field) {
                <th
                  [style.width]="col.width || 'auto'"
                  [style.minWidth]="col.minWidth || 'auto'"
                  [style.maxWidth]="col.maxWidth || 'auto'"
                  [class.align-right]="col.align === 'right'"
                  [class.align-center]="col.align === 'center'"
                >
                  {{ col.header }}
                  @if (col.required) {
                    <span class="required-star">*</span>
                  }
                </th>
              }
            </tr>
          </thead>

          <tbody>
            @if (stateService.rows().length === 0) {
              <tr class="empty-table-row">
                <td [attr.colspan]="columns.length + (showRowNumbers ? 1 : 0)" style="text-align: center; padding: 24px;">
                  <span class="empty-text">No records available. Click "Add Line" to create a row.</span>
                </td>
              </tr>
            } @else {
              @for (row of stateService.rows(); track getRowKey(row, $index); let rIdx = $index) {
                <tr
                  class="fluent-grid-row"
                  [class.row-dirty]="stateService.dirtyRowIndices().has(rIdx)"
                >
                  @if (showRowNumbers) {
                    <td style="width: 44px; text-align: center; color: #605e5c; user-select: none;">
                      {{ rIdx + 1 }}
                    </td>
                  }

                  @for (col of columns; track col.field) {
                    <td
                      [style.width]="col.width || 'auto'"
                      [class.is-editing]="editingService.isEditing(rIdx, col.field)"
                      [class.cell-has-error]="!!validationService.getCellError(rIdx, col.field)"
                      [class.cell-readonly]="col.type === 'actions' || col.type === 'calculated' || col.editable === false"
                      [class.align-right]="col.align === 'right'"
                      [class.align-center]="col.align === 'center'"
                    >
                      <app-mat-table-cell
                        [rowIndex]="rIdx"
                        [column]="col"
                        [row]="row"
                        (cellClick)="onCellClick($event)"
                        (valueChange)="onCellValueChange(rIdx, col, $event)"
                        (lookupSelect)="onLookupItemSelect(rIdx, col, $event)"
                        (commit)="onCellCommit(rIdx, col)"
                        (cancel)="onCellCancel(rIdx, col)"
                        (tab)="onCellTab(rIdx, col, $event)"
                        (rowDelete)="onDeleteRow(rIdx)"
                      ></app-mat-table-cell>
                    </td>
                  }
                </tr>
              }
            }
          </tbody>

          @if (showFooter && stateService.rows().length > 0) {
            <tfoot>
              <tr>
                @if (showRowNumbers) {
                  <td></td>
                }
                @for (col of columns; track col.field; let cIdx = $index) {
                  <td
                    [class.align-right]="col.align === 'right'"
                    [class.align-center]="col.align === 'center'"
                  >
                    @if (cIdx === 0) {
                      <span>Total ({{ stateService.rowCount() }} lines)</span>
                    } @else if (col.field === 'quantity') {
                      <span>{{ getTotalQuantity() }}</span>
                    } @else if (col.field === 'lineTotal' || col.type === 'currency' && cIdx === columns.length - 2) {
                      <span>\${{ getTotalValuation().toFixed(2) }}</span>
                    }
                  </td>
                }
              </tr>
            </tfoot>
          }
        </table>
      </div>
    </div>
  `,
  styleUrls: ['../theme/material-overrides.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class AppMaterialEditableDataTableComponent<T = any> implements OnInit, OnChanges {
  @Input() data: T[] = [];
  @Input() columns: MaterialTableColumn<T>[] = [];
  @Input() rowKey: string = 'id';
  @Input() allowAddRow: boolean = true;
  @Input() allowDeleteRow: boolean = true;
  @Input() showToolbar: boolean = true;
  @Input() showRowNumbers: boolean = true;
  @Input() showFooter: boolean = true;
  @Input() defaultNewRow?: () => T;
  @Input() onRowCalculate?: (row: T) => void;

  @Output() dataChange = new EventEmitter<T[]>();
  @Output() save = new EventEmitter<{ updatedData: T[]; dirtyCount: number }>();
  @Output() cancelEdits = new EventEmitter<void>();
  @Output() rowDelete = new EventEmitter<T>();

  readonly feedbackBanner = signal<{ type: 'success' | 'error' | 'info'; message: string } | null>(null);

  constructor(
    public stateService: TableStateService<T>,
    public editingService: TableEditingService,
    public validationService: TableValidationService,
    public navigationService: TableNavigationService,
    public cdr?: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    this.stateService.init(this.data, this.columns, this.rowKey, this.onRowCalculate);
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['data'] && !changes['data'].firstChange) {
      if (this.stateService.dirtyCount() === 0 && !this.editingService.activeCell()) {
        this.stateService.setRows(this.data);
      }
    }
  }

  getRowKey(row: T, index: number): any {
    return (row as any)[this.rowKey] || index;
  }

  getTotalQuantity(): number {
    return this.stateService.rows().reduce((sum, r) => sum + (Number((r as any).quantity) || 0), 0);
  }

  getTotalValuation(): number {
    return this.stateService.rows().reduce((sum, r) => {
      const lineTotal = Number((r as any).lineTotal);
      if (!isNaN(lineTotal) && lineTotal > 0) return sum + lineTotal;
      const qty = Number((r as any).quantity) || 0;
      const cost = Number((r as any).unitCost) || 0;
      return sum + (qty * cost);
    }, 0);
  }

  onCellClick(event: { rowIndex: number; column: MaterialTableColumn<T> }): void {
    const active = this.editingService.activeCell();
    if (active) {
      // Validate previous cell before switching
      const prevRow = this.stateService.rows()[active.rowIndex];
      const prevCol = this.columns.find(c => c.field === active.colField);
      if (prevCol) {
        const prevVal = (prevRow as any)[prevCol.field];
        if (!this.validationService.isCellValid(active.rowIndex, prevCol, prevVal, prevRow)) {
          // Block cell click navigation to keep focus on invalid cell
          return;
        }
      }
      this.editingService.commitEditing();
    }

    const row = this.stateService.rows()[event.rowIndex];
    const currentVal = (row as any)[event.column.field];
    this.editingService.startEditing(event.rowIndex, event.column.field, currentVal);
  }

  onCellValueChange(rowIndex: number, col: MaterialTableColumn<T>, value: any): void {
    this.stateService.updateCellValue(rowIndex, col.field, value);
    const row = this.stateService.rows()[rowIndex];
    if (this.onRowCalculate) {
      this.onRowCalculate(row);
      this.stateService.updateRow(rowIndex, row);
    }
    this.validationService.validateCell(rowIndex, col, value, row);
    this.dataChange.emit(this.stateService.rows());
  }

  onLookupItemSelect(rowIndex: number, col: MaterialTableColumn<T>, item: any): void {
    const row = this.stateService.rows()[rowIndex];
    if (col.lookupConfig?.onSelect) {
      col.lookupConfig.onSelect(item, row);
      this.stateService.updateRow(rowIndex, row);
    } else {
      const val = item[col.lookupConfig?.valueField || 'id'];
      this.stateService.updateCellValue(rowIndex, col.field, val);
    }
    this.dataChange.emit(this.stateService.rows());
  }

  onCellCommit(rowIndex: number, col: MaterialTableColumn<T>): void {
    const row = this.stateService.rows()[rowIndex];
    const val = (row as any)[col.field];
    if (this.validationService.isCellValid(rowIndex, col, val, row)) {
      this.editingService.commitEditing();
    }
  }

  onCellCancel(rowIndex: number, col: MaterialTableColumn<T>): void {
    const result = this.editingService.cancelEditing();
    if (result) {
      this.stateService.updateCellValue(result.rowIndex, result.colField, result.originalValue);
      const row = this.stateService.rows()[result.rowIndex];
      if (this.onRowCalculate) {
        this.onRowCalculate(row);
        this.stateService.updateRow(result.rowIndex, row);
      }
    }
  }

  onCellTab(rowIndex: number, col: MaterialTableColumn<T>, event: KeyboardEvent): void {
    this.navigationService.handleTabNavigation(
      event,
      { rowIndex, colField: col.field },
      this.stateService.rows(),
      this.columns
    );
  }

  onAddRow(): void {
    const active = this.editingService.activeCell();
    if (active) {
      const prevRow = this.stateService.rows()[active.rowIndex];
      const prevCol = this.columns.find(c => c.field === active.colField);
      if (prevCol) {
        const prevVal = (prevRow as any)[prevCol.field];
        if (!this.validationService.isCellValid(active.rowIndex, prevCol, prevVal, prevRow)) {
          this.feedbackBanner.set({
            type: 'error',
            message: 'Please complete or correct the active cell before adding a new row.'
          });
          return;
        }
      }
      this.editingService.commitEditing();
    }

    const newRow = this.defaultNewRow ? this.defaultNewRow() : ({} as T);
    const newIdx = this.stateService.addRow(newRow, 'end');

    this.dataChange.emit(this.stateService.rows());

    // Auto-focus first editable cell of the new row
    const editableCols = this.navigationService.getEditableColumnsForRow(this.columns, newRow);
    if (editableCols.length > 0) {
      const firstCol = editableCols[0];
      this.editingService.startEditing(newIdx, firstCol.field, (newRow as any)[firstCol.field]);
    }

    this.feedbackBanner.set({
      type: 'info',
      message: `Added new row #${newIdx + 1}. Press Tab to navigate across cells.`
    });
  }

  onDeleteRow(rowIndex: number): void {
    const deleted = this.stateService.deleteRow(rowIndex);
    this.editingService.stopEditing();
    if (deleted) {
      this.rowDelete.emit(deleted);
      this.dataChange.emit(this.stateService.rows());
      this.feedbackBanner.set({
        type: 'info',
        message: `Line #${rowIndex + 1} was removed from in-memory draft.`
      });
    }
  }

  onSave(): void {
    const rows = this.stateService.rows();
    const isValid = this.validationService.validateAll(rows, this.columns);

    if (!isValid) {
      this.feedbackBanner.set({
        type: 'error',
        message: 'Cannot save: please fix highlighted validation errors in the table.'
      });
      return;
    }

    const dirtyCount = this.stateService.dirtyCount();
    const committed = this.stateService.commitAllAsBaseline();
    this.editingService.commitEditing();

    this.save.emit({ updatedData: committed, dirtyCount });
    this.feedbackBanner.set({
      type: 'success',
      message: `Successfully saved ${dirtyCount} line item(s) to in-memory demo data store.`
    });
  }

  onReset(): void {
    this.stateService.revertAll();
    this.editingService.stopEditing();
    this.validationService.clearErrors();
    this.cancelEdits.emit();
    this.feedbackBanner.set({
      type: 'info',
      message: 'All unsaved changes have been reverted to baseline demo state.'
    });
  }
}
