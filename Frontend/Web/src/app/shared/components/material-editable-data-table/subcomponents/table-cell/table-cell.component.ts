import {
  Component,
  Input,
  Output,
  EventEmitter,
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  computed
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { MaterialTableColumn } from '../../models/material-data-table.models';
import { TableEditingService } from '../../core/table-editing.service';
import { TableValidationService } from '../../core/table-validation.service';
import { TableTextEditorComponent } from '../table-text-editor/table-text-editor.component';
import { TableNumberEditorComponent } from '../table-number-editor/table-number-editor.component';
import { TableSelectEditorComponent } from '../table-select-editor/table-select-editor.component';
import { TableLookupEditorComponent } from '../table-lookup-editor/table-lookup-editor.component';
import { TableRowActionsComponent } from '../table-row-actions/table-row-actions.component';

@Component({
  selector: 'app-mat-table-cell',
  standalone: true,
  imports: [
    CommonModule,
    TableTextEditorComponent,
    TableNumberEditorComponent,
    TableSelectEditorComponent,
    TableLookupEditorComponent,
    TableRowActionsComponent
  ],
  template: `
    @if (isEditing()) {
      <div class="fluent-cell-editor" (click)="$event.stopPropagation()">
        @switch (column.type) {
          @case ('lookup') {
            <app-mat-table-lookup-editor
              [value]="value"
              [config]="column.lookupConfig!"
              [placeholder]="column.placeholder"
              (valueChange)="onValueChange($event)"
              (itemSelect)="onLookupItemSelect($event)"
              (commit)="onCommit()"
              (cancel)="onCancel()"
              (tab)="onTab($event)"
            ></app-mat-table-lookup-editor>
          }
          @case ('select') {
            <app-mat-table-select-editor
              [value]="value"
              [options]="getSelectOptions()"
              [placeholder]="column.placeholder"
              (valueChange)="onValueChange($event)"
              (commit)="onCommit()"
              (cancel)="onCancel()"
              (tab)="onTab($event)"
            ></app-mat-table-select-editor>
          }
          @case ('number') {
            <app-mat-table-number-editor
              [value]="value"
              [step]="column.step || 1"
              [min]="column.min"
              [max]="column.max"
              [placeholder]="column.placeholder"
              (valueChange)="onValueChange($event)"
              (commit)="onCommit()"
              (cancel)="onCancel()"
              (tab)="onTab($event)"
            ></app-mat-table-number-editor>
          }
          @case ('currency') {
            <app-mat-table-number-editor
              [value]="value"
              [step]="column.step || 0.01"
              [min]="column.min"
              [max]="column.max"
              [placeholder]="column.placeholder"
              (valueChange)="onValueChange($event)"
              (commit)="onCommit()"
              (cancel)="onCancel()"
              (tab)="onTab($event)"
            ></app-mat-table-number-editor>
          }
          @default {
            <app-mat-table-text-editor
              [value]="value"
              [placeholder]="column.placeholder"
              (valueChange)="onValueChange($event)"
              (commit)="onCommit()"
              (cancel)="onCancel()"
              (tab)="onTab($event)"
            ></app-mat-table-text-editor>
          }
        }

        <!-- Error callout tooltip if invalid -->
        @if (errorMessage(); as err) {
          <div class="cell-error-banner" [title]="err">
            ⚠ {{ err }}
          </div>
        }
      </div>
    } @else {
      @if (column.type === 'actions') {
        <app-mat-table-row-actions
          (delete)="rowDelete.emit()"
        ></app-mat-table-row-actions>
      } @else {
        <div
          class="fluent-cell-display"
          [class.align-right]="column.align === 'right'"
          [class.align-center]="column.align === 'center'"
          [class.is-editable]="isEditable()"
          (click)="onCellClick($event)"
        >
          @if (getDisplayValue(); as text) {
            <span class="cell-value-text">{{ text }}</span>
          } @else {
            <span class="placeholder-text">{{ isEditable() ? '—' : '' }}</span>
          }

          @if (errorMessage(); as err) {
            <span class="cell-error-icon" [title]="err">⚠</span>
          }
        </div>
      }
    }
  `,
  styles: [`
    :host {
      display: block;
      width: 100%;
      height: 100%;
      min-height: var(--fluent-row-height, 36px);
      position: relative;
    }
    .fluent-cell-display {
      width: 100%;
      height: 100%;
      min-height: var(--fluent-row-height, 36px);
      display: flex;
      align-items: center;
      padding: 0 10px;
      box-sizing: border-box;
      font-size: 13px;
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
      user-select: none;
      &.is-editable {
        cursor: pointer;
      }
      &.align-right { justify-content: flex-end; }
      &.align-center { justify-content: center; }
    }
    .placeholder-text {
      color: #a19f9d;
    }
    .cell-error-banner {
      position: absolute;
      top: 100%;
      left: 0;
      background: #a4262c;
      color: #ffffff;
      font-size: 11px;
      padding: 3px 6px;
      border-radius: 2px;
      white-space: nowrap;
      z-index: 1001;
      box-shadow: 0 2px 6px rgba(0,0,0,0.2);
    }
    .cell-error-icon {
      color: #a4262c;
      margin-left: 6px;
      font-size: 12px;
    }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class TableCellComponent<T = any> {
  @Input({ required: true }) rowIndex!: number;
  @Input({ required: true }) column!: MaterialTableColumn<T>;
  @Input({ required: true }) row!: T;

  @Output() cellClick = new EventEmitter<{ rowIndex: number; column: MaterialTableColumn<T> }>();
  @Output() valueChange = new EventEmitter<any>();
  @Output() lookupSelect = new EventEmitter<any>();
  @Output() commit = new EventEmitter<void>();
  @Output() cancel = new EventEmitter<void>();
  @Output() tab = new EventEmitter<KeyboardEvent>();
  @Output() rowDelete = new EventEmitter<void>();

  constructor(
    private editingService: TableEditingService,
    private validationService: TableValidationService,
    private cdr: ChangeDetectorRef
  ) {}

  get value(): any {
    return (this.row as any)[this.column.field];
  }

  readonly isEditing = computed(() => this.editingService.isEditing(this.rowIndex, this.column.field));

  isEditable(): boolean {
    if (this.column.type === 'actions' || this.column.type === 'calculated') return false;
    if (typeof this.column.editable === 'function') {
      return this.column.editable(this.row);
    }
    return this.column.editable !== false;
  }

  readonly errorMessage = computed(() => this.validationService.getCellError(this.rowIndex, this.column.field));

  getDisplayValue(): string {
    const val = this.value;
    if (this.column.format) {
      return this.column.format(val, this.row);
    }
    if (val === null || val === undefined || val === '') {
      return '';
    }
    if (this.column.type === 'currency') {
      return `$${Number(val).toFixed(2)}`;
    }
    return String(val);
  }

  getSelectOptions(): any[] {
    const opts = this.column.options;
    if (typeof opts === 'function') {
      return opts(this.row);
    }
    return opts || [];
  }

  onCellClick(event: Event): void {
    if (!this.isEditable()) return;
    this.cellClick.emit({ rowIndex: this.rowIndex, column: this.column });
  }

  onValueChange(newVal: any): void {
    this.valueChange.emit(newVal);
  }

  onLookupItemSelect(item: any): void {
    this.lookupSelect.emit(item);
  }

  onCommit(): void {
    this.commit.emit();
    this.cdr.markForCheck();
  }

  onCancel(): void {
    this.cancel.emit();
    this.cdr.markForCheck();
  }

  onTab(event: KeyboardEvent): void {
    this.tab.emit(event);
  }
}
