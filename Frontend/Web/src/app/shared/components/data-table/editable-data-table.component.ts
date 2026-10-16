import {
  Component,
  Input,
  Output,
  EventEmitter,
  signal,
  computed,
  ChangeDetectionStrategy
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { DialogModule } from 'primeng/dialog';
import { TooltipModule } from 'primeng/tooltip';
import {
  DataTableColumn,
  DataTableAction,
  DataTableConfig,
  TableSortMeta,
  TableFilterMeta,
  TableQueryState
} from '../../models/data-table-config.model';
import { AppStatusBadgeComponent } from '../status-badge/status-badge.component';
import { AppLoadingStateComponent } from '../loading-state/loading-state.component';
import { AppEmptyStateComponent } from '../empty-state/empty-state.component';
import { AppCheckboxComponent } from '../checkbox/checkbox.component';
import { AppLookupDropdownComponent } from '../lookup-dropdown/lookup-dropdown.component';
import { LookupItem } from '../lookup-dropdown/lookup-item.model';
import { TableSearchComponent } from './subcomponents/table-search.component';
import { TableSortComponent } from './subcomponents/table-sort.component';
import { TableFilterComponent } from './subcomponents/table-filter.component';
import { TablePaginationComponent } from './subcomponents/table-pagination.component';
import { TableSelectionComponent } from './subcomponents/table-selection.component';
import { TableColumnChooserComponent } from './subcomponents/table-column-chooser.component';

@Component({
  selector: 'app-editable-data-table',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ButtonModule,
    InputTextModule,
    DialogModule,
    TooltipModule,
    AppStatusBadgeComponent,
    AppLoadingStateComponent,
    AppEmptyStateComponent,
    AppCheckboxComponent,
    AppLookupDropdownComponent,
    TableSearchComponent,
    TableSortComponent,
    TableFilterComponent,
    TablePaginationComponent,
    TableSelectionComponent,
    TableColumnChooserComponent
  ],
  templateUrl: './editable-data-table.component.html',
  styleUrl: './editable-data-table.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class AppEditableDataTableComponent<T extends Record<string, any> = Record<string, any>> {
  @Input() set config(cfg: DataTableConfig<T> | undefined) {
    if (!cfg) return;
    if (cfg.data) this.initData(cfg.data);
    if (cfg.columns) this.columnsSignal.set(cfg.columns);
    if (cfg.rowKey) this.rowKey = String(cfg.rowKey);
    if (cfg.showSearch !== undefined) this.showGlobalSearch = cfg.showSearch;
    if (cfg.showFilters !== undefined) this.showFilters = cfg.showFilters;
    if (cfg.showSorting !== undefined) this.showSorting = cfg.showSorting;
    if (cfg.showPagination !== undefined) this.paginator = cfg.showPagination;
    if (cfg.showSelection !== undefined) this.selectionMode = cfg.showSelection ? 'multiple' : null;
    if (cfg.showColumnChooser !== undefined) this.showColumnChooser = cfg.showColumnChooser;
    if (cfg.pageSize !== undefined) this.rows = cfg.pageSize;
    if (cfg.pageSizeOptions) this.rowsPerPageOptions = cfg.pageSizeOptions;
    if (cfg.rowActions) this.rowActions = cfg.rowActions;
    if (cfg.searchPlaceholder) this.searchPlaceholder = cfg.searchPlaceholder;
  }

  @Input() set data(val: T[]) {
    this.initData(val || []);
  }

  @Input() set columns(cols: DataTableColumn<T>[]) {
    this.columnsSignal.set(cols || []);
  }
  get columns(): DataTableColumn<T>[] {
    return this.columnsSignal();
  }

  @Input() rowKey: string = 'lineId';
  @Input() loading: boolean = false;
  @Input() paginator: boolean = true;
  @Input() rows: number = 10;
  @Input() rowsPerPageOptions: number[] = [5, 10, 25, 50];
  @Input() searchPlaceholder: string = 'Search items...';
  @Input() showGlobalSearch: boolean = true;
  @Input() showFilters: boolean = true;
  @Input() showSorting: boolean = true;
  @Input() showColumnChooser: boolean = true;
  @Input() showExport: boolean = true;
  @Input() showRowNumbers: boolean = true;
  @Input() selectionMode: 'single' | 'multiple' | null = 'multiple';
  @Input() allowAddRow: boolean = true;
  @Input() allowDeleteRow: boolean = true;
  @Input() defaultNewRow?: () => Partial<T>;
  @Input() rowActions?: DataTableAction<T>[];
  @Input() currencyCode: string = 'USD';
  @Input() currencyLocale: string = 'en-US';
  @Input() showSubtotal: boolean = true;
  @Input() subtotalLabel: string = 'Subtotal';
  @Input() subtotalField: string = 'lineTotal';
  @Input() onRowCalculate?: (row: T) => void;

  // Outputs
  @Output() dataChange = new EventEmitter<T[]>();
  @Output() save = new EventEmitter<{ updatedData: T[]; dirtyCount: number }>();
  @Output() cancelEdits = new EventEmitter<void>();
  @Output() rowDelete = new EventEmitter<T>();
  @Output() selectionChange = new EventEmitter<T[]>();
  @Output() cellValueChange = new EventEmitter<{ row: T; column: DataTableColumn<T>; value: any }>();

  // Computed subtotal of working rows
  readonly computedSubtotal = computed(() => {
    const field = this.subtotalField;
    return this.workingData().reduce((acc, row) => {
      const val = Number((row as any)[field]);
      return acc + (isNaN(val) ? 0 : val);
    }, 0);
  });

  getTotalColspan(): number {
    return this.visibleColumns().length
      + (this.selectionMode === 'multiple' ? 1 : 0)
      + (this.showRowNumbers ? 1 : 0)
      + (this.allowDeleteRow ? 1 : 0);
  }

  getSubtotalLeadingColspan(): number {
    const totalVisible = this.visibleColumns().length;
    const selectionOffset = this.selectionMode === 'multiple' ? 1 : 0;
    const rowNumOffset = this.showRowNumbers ? 1 : 0;
    return Math.max(1, totalVisible + selectionOffset + rowNumOffset - 2);
  }

  // Reactive state
  readonly baselineData = signal<T[]>([]);
  readonly workingData = signal<T[]>([]);
  readonly columnsSignal = signal<DataTableColumn<T>[]>([]);
  readonly dirtyRowKeys = signal<Set<string>>(new Set());
  readonly validationErrors = signal<Record<string, Record<string, string>>>({});
  readonly selection = signal<T[]>([]);

  // Click-to-edit state
  readonly activeEditCell = signal<{ rowKey: string; field: string } | null>(null);
  private readonly cellOriginalValues = new Map<string, any>();

  // Filter & sort state
  readonly globalSearchTerm = signal<string>('');
  readonly columnFilters = signal<Record<string, TableFilterMeta>>({});
  readonly activeSortField = signal<string | undefined>(undefined);
  readonly activeSortOrder = signal<1 | -1 | 0>(0);
  readonly pageOffset = signal<number>(0);

  // Status message for save / cancel feedback
  readonly feedbackMessage = signal<{ type: 'success' | 'info' | 'error'; text: string } | null>(null);

  // Delete modal state
  readonly deleteDialogVisible = signal<boolean>(false);
  readonly pendingDeleteRow = signal<T | null>(null);

  private initData(items: T[]): void {
    const copy = JSON.parse(JSON.stringify(items));
    this.baselineData.set(JSON.parse(JSON.stringify(items)));
    this.workingData.set(copy);
    this.dirtyRowKeys.set(new Set());
    this.validationErrors.set({});
    this.activeEditCell.set(null);
    this.cellOriginalValues.clear();
  }

  readonly visibleColumns = computed(() => {
    return this.columnsSignal().filter(c => c.visible !== false);
  });

  readonly dirtyCount = computed(() => this.dirtyRowKeys().size);

  readonly hasErrors = computed(() => {
    const errMap = this.validationErrors();
    return Object.keys(errMap).some(k => Object.keys(errMap[k]).length > 0);
  });

  // Client filtering & sorting engine
  readonly processedData = computed(() => {
    let result = [...this.workingData()];

    // Search
    const search = this.globalSearchTerm().trim().toLowerCase();
    if (search) {
      result = result.filter(item => {
        return this.columnsSignal().some(col => {
          if (col.type === 'actions') return false;
          const val = item[col.field as string];
          return val != null && String(val).toLowerCase().includes(search);
        });
      });
    }

    // Column Filters
    const filters = this.columnFilters();
    for (const field of Object.keys(filters)) {
      const filter = filters[field];
      if (filter.value == null || filter.value === '') continue;

      result = result.filter(item => {
        const itemVal = item[field];
        if (itemVal == null) return false;

        if (filter.matchMode === 'in' && Array.isArray(filter.value)) {
          return filter.value.length === 0 || filter.value.includes(itemVal);
        }

        const strItem = String(itemVal).toLowerCase();
        const strFilter = String(filter.value).toLowerCase();

        switch (filter.matchMode) {
          case 'startsWith': return strItem.startsWith(strFilter);
          case 'equals': return strItem === strFilter;
          case 'gt': return Number(itemVal) > Number(filter.value);
          case 'gte': return Number(itemVal) >= Number(filter.value);
          case 'lt': return Number(itemVal) < Number(filter.value);
          case 'lte': return Number(itemVal) <= Number(filter.value);
          default: return strItem.includes(strFilter);
        }
      });
    }

    // Sorting
    if (this.activeSortField() && this.activeSortOrder() !== 0) {
      const field = this.activeSortField()!;
      const order = this.activeSortOrder();
      result.sort((a, b) => {
        const valA = a[field];
        const valB = b[field];
        if (valA == null && valB == null) return 0;
        if (valA == null) return order * -1;
        if (valB == null) return order * 1;
        if (typeof valA === 'number' && typeof valB === 'number') {
          return order * (valA - valB);
        }
        return order * String(valA).localeCompare(String(valB), undefined, { numeric: true });
      });
    }

    return result;
  });

  readonly displayedRows = computed(() => {
    if (!this.paginator) return this.processedData();
    const offset = this.pageOffset();
    return this.processedData().slice(offset, offset + this.rows);
  });

  getRowKey(row: T): string {
    return String(row[this.rowKey] || (row as any).id || (row as any).lineId);
  }

  getFieldKey(col: DataTableColumn<T>): string {
    return String(col.field);
  }

  isRowDirty(row: T): boolean {
    return this.dirtyRowKeys().has(this.getRowKey(row));
  }

  getFieldError(row: T, field: string): string | null {
    const key = this.getRowKey(row);
    return this.validationErrors()[key]?.[field] || null;
  }

  // Click-to-edit management
  isCellEditing(row: T, col: DataTableColumn<T>): boolean {
    if (!col.editable) return false;
    const active = this.activeEditCell();
    if (!active) return false;
    return active.rowKey === this.getRowKey(row) && active.field === String(col.field);
  }

  startEditing(row: T, col: DataTableColumn<T>, event?: Event): void {
    if (!col.editable) return;
    if (event) {
      event.stopPropagation();
    }
    const rowKey = this.getRowKey(row);
    const field = String(col.field);
    const cellKey = `${rowKey}_${field}`;

    if (!this.cellOriginalValues.has(cellKey)) {
      this.cellOriginalValues.set(cellKey, row[col.field as keyof T]);
    }

    this.activeEditCell.set({ rowKey, field });

    setTimeout(() => {
      const el = document.getElementById(`editor-${rowKey}-${field}`);
      if (el) {
        if (el instanceof HTMLInputElement || el instanceof HTMLSelectElement) {
          el.focus();
          if (el instanceof HTMLInputElement && el.type !== 'number') {
            el.select();
          }
        } else {
          const innerInput = el.querySelector('input') || el.querySelector('button');
          innerInput?.focus();
        }
      }
    }, 15);
  }

  stopEditing(): void {
    this.activeEditCell.set(null);
  }

  onCellKeyDown(event: KeyboardEvent, row: T, col: DataTableColumn<T>, rowIndex: number): void {
    const rowKey = this.getRowKey(row);
    const cellKey = `${rowKey}_${String(col.field)}`;

    if (event.key === 'Escape') {
      event.preventDefault();
      if (this.cellOriginalValues.has(cellKey)) {
        const orig = this.cellOriginalValues.get(cellKey);
        row[col.field as keyof T] = orig;
        this.recalculateRowTotals(row);
      }
      this.stopEditing();
      return;
    }

    if (event.key === 'Enter' && !event.ctrlKey) {
      event.preventDefault();
      this.stopEditing();
      return;
    }

    // Insert row shortcut (Insert key or Ctrl+Enter)
    if (event.key === 'Insert' || (event.key === 'Enter' && event.ctrlKey)) {
      event.preventDefault();
      const currRowIdx = this.workingData().findIndex(r => this.getRowKey(r) === rowKey);
      this.insertRowAt(currRowIdx >= 0 ? currRowIdx : rowIndex);
      return;
    }

    // Arrow navigation across rows
    if (event.key === 'ArrowUp' && (event.altKey || col.editType === 'text' || col.editType === 'currency')) {
      const currRowIdx = this.workingData().findIndex(r => this.getRowKey(r) === rowKey);
      if (currRowIdx > 0) {
        event.preventDefault();
        const prevRow = this.workingData()[currRowIdx - 1];
        this.startEditing(prevRow, col);
        return;
      }
    }

    if (event.key === 'ArrowDown' && (event.altKey || col.editType === 'text' || col.editType === 'currency')) {
      const currRowIdx = this.workingData().findIndex(r => this.getRowKey(r) === rowKey);
      if (currRowIdx < this.workingData().length - 1) {
        event.preventDefault();
        const nextRow = this.workingData()[currRowIdx + 1];
        this.startEditing(nextRow, col);
        return;
      }
    }

    if (event.key === 'Tab') {
      event.preventDefault();
      const editableCols = this.visibleColumns().filter(c => c.editable);
      const colIdx = editableCols.findIndex(c => c.field === col.field);

      if (event.shiftKey) {
        // Shift + Tab: Move backwards
        if (colIdx > 0) {
          this.startEditing(row, editableCols[colIdx - 1]);
        } else {
          const currRowIdx = this.workingData().findIndex(r => this.getRowKey(r) === rowKey);
          if (currRowIdx > 0) {
            const prevRow = this.workingData()[currRowIdx - 1];
            this.startEditing(prevRow, editableCols[editableCols.length - 1]);
          } else {
            this.stopEditing();
          }
        }
      } else {
        // Tab: Move forward
        if (colIdx < editableCols.length - 1) {
          // Next editable column in same row
          this.startEditing(row, editableCols[colIdx + 1]);
        } else {
          // Last editable column of current row
          const currRowIdx = this.workingData().findIndex(r => this.getRowKey(r) === rowKey);
          if (currRowIdx < this.workingData().length - 1) {
            // Next row's first editable column
            const nextRow = this.workingData()[currRowIdx + 1];
            this.startEditing(nextRow, editableCols[0]);
          } else {
            // After last column tab also add blank row!
            this.addNewRow('end');
          }
        }
      }
    }
  }

  // Lookup integration
  onLookupSelect(row: T, col: DataTableColumn<T>, item: LookupItem): void {
    const val = item.code || item.id;
    this.onCellValueChange(row, col, val);
    if (col.onLookupSelect) {
      col.onLookupSelect(item, row);
      this.recalculateRowTotals(row);
      this.workingData.set([...this.workingData()]);
    }
  }

  // High performance value change and validation for fast typing
  onCellValueChange(row: T, col: DataTableColumn<T>, value: any): void {
    const key = this.getRowKey(row);
    const fieldName = String(col.field);

    // Cast numbers if needed
    let parsedVal = value;
    if (col.editType === 'number' || col.editType === 'currency') {
      parsedVal = value === '' || isNaN(Number(value)) ? value : Number(value);
    }
    row[fieldName as keyof T] = parsedVal;

    // Validate field
    this.validateField(row, col, parsedVal);

    // Recalculate row totals in place
    this.recalculateRowTotals(row);

    // Mark dirty
    if (!this.dirtyRowKeys().has(key)) {
      const dirties = new Set(this.dirtyRowKeys());
      dirties.add(key);
      this.dirtyRowKeys.set(dirties);
    }

    this.workingData.set([...this.workingData()]);

    this.cellValueChange.emit({ row, column: col, value: parsedVal });
  }

  private validateField(row: T, col: DataTableColumn<T>, val: any): void {
    const key = this.getRowKey(row);
    const fieldName = String(col.field);
    const errMap = { ...this.validationErrors() };
    if (!errMap[key]) {
      errMap[key] = {};
    }

    let errorMsg: string | null = null;

    if (col.validators && col.validators.length > 0) {
      for (const validator of col.validators) {
        errorMsg = validator(val, row);
        if (errorMsg) break;
      }
    } else {
      if (val === '' || val === null || val === undefined) {
        errorMsg = `${col.header} is required`;
      } else if ((col.editType === 'number' || col.editType === 'currency') && isNaN(Number(val))) {
        errorMsg = 'Must be a valid number';
      } else if ((col.editType === 'number' || col.editType === 'currency') && Number(val) < 0) {
        errorMsg = 'Cannot be negative';
      }
    }

    if (errorMsg) {
      errMap[key][fieldName] = errorMsg;
    } else {
      delete errMap[key][fieldName];
      if (Object.keys(errMap[key]).length === 0) {
        delete errMap[key];
      }
    }

    this.validationErrors.set(errMap);
  }

  private recalculateRowTotals(row: any): void {
    if (this.onRowCalculate) {
      this.onRowCalculate(row);
      return;
    }

    // Flexible transaction calculation supporting unitCost or unitPrice
    if ('quantity' in row) {
      const qty = Number(row.quantity) || 0;
      const rate = Number(row.unitCost !== undefined ? row.unitCost : (row.unitPrice !== undefined ? row.unitPrice : 0));
      const disc = Number(row.discountPercent) || 0;
      const tax = Number(row.taxPercent) || 0;
      const subtotal = qty * rate * (1 - disc / 100);
      const total = subtotal * (1 + tax / 100);
      row.lineTotal = Math.round(total * 100) / 100;
    }
  }

  // Row additions: creates a clean blank row at start or end
  addNewRow(position: 'start' | 'end' = 'start'): void {
    const newId = 'LINE-' + (100 + this.workingData().length + 1);
    let newRecord: any = {
      [this.rowKey]: newId,
      status: 'Draft'
    };

    if (this.defaultNewRow) {
      newRecord = { ...newRecord, ...this.defaultNewRow() };
    } else {
      for (const col of this.columnsSignal()) {
        const field = String(col.field);
        if (field === this.rowKey) continue;
        if (col.editType === 'number' || col.editType === 'currency' || col.type === 'number' || col.type === 'currency') {
          newRecord[field] = 0;
        } else {
          newRecord[field] = '';
        }
      }
    }

    const current = position === 'end'
      ? [...this.workingData(), newRecord]
      : [newRecord, ...this.workingData()];
    this.workingData.set(current);

    const dirties = new Set(this.dirtyRowKeys());
    dirties.add(newId);
    this.dirtyRowKeys.set(dirties);

    this.feedbackMessage.set({
      type: 'info',
      text: `Added new blank line item ${newId}. Click any cell to enter data.`
    });

    const editableCols = this.visibleColumns().filter(c => c.editable);
    if (editableCols.length > 0) {
      this.startEditing(newRecord, editableCols[0]);
    }
  }

  // Insert row in the middle of table right below targetIndex
  insertRowAt(targetIndex: number, event?: Event): void {
    if (event) event.stopPropagation();
    const newId = 'LINE-' + (100 + this.workingData().length + 1);
    let newRecord: any = {
      [this.rowKey]: newId,
      status: 'Draft'
    };

    if (this.defaultNewRow) {
      newRecord = { ...newRecord, ...this.defaultNewRow() };
    } else {
      for (const col of this.columnsSignal()) {
        const field = String(col.field);
        if (field === this.rowKey) continue;
        if (col.editType === 'number' || col.editType === 'currency' || col.type === 'number' || col.type === 'currency') {
          newRecord[field] = 0;
        } else {
          newRecord[field] = '';
        }
      }
    }

    const current = [...this.workingData()];
    const insertIdx = Math.min(Math.max(0, targetIndex + 1), current.length);
    current.splice(insertIdx, 0, newRecord);
    this.workingData.set(current);

    const dirties = new Set(this.dirtyRowKeys());
    dirties.add(newId);
    this.dirtyRowKeys.set(dirties);

    this.feedbackMessage.set({
      type: 'info',
      text: `Inserted blank row ${newId} into position ${insertIdx + 1}. Click any cell to enter data.`
    });

    const editableCols = this.visibleColumns().filter(c => c.editable);
    if (editableCols.length > 0) {
      this.startEditing(newRecord, editableCols[0]);
    }
  }

  // Row deletion with confirmation
  promptDelete(row: T, event?: Event): void {
    if (event) event.stopPropagation();
    this.pendingDeleteRow.set(row);
    this.deleteDialogVisible.set(true);
  }

  confirmDelete(): void {
    const row = this.pendingDeleteRow();
    if (!row) return;

    const key = this.getRowKey(row);
    const updated = this.workingData().filter(r => this.getRowKey(r) !== key);
    this.workingData.set(updated);

    const dirties = new Set(this.dirtyRowKeys());
    dirties.delete(key);
    this.dirtyRowKeys.set(dirties);

    const errMap = { ...this.validationErrors() };
    delete errMap[key];
    this.validationErrors.set(errMap);

    this.deleteDialogVisible.set(false);
    this.pendingDeleteRow.set(null);
    this.rowDelete.emit(row);

    this.feedbackMessage.set({
      type: 'info',
      text: `Line item ${key} removed from local draft.`
    });
  }

  // Save changes locally
  saveChanges(): void {
    if (this.hasErrors()) {
      this.feedbackMessage.set({
        type: 'error',
        text: 'Please resolve validation errors before saving.'
      });
      return;
    }

    const count = this.dirtyCount();
    const newBaseline = JSON.parse(JSON.stringify(this.workingData()));
    this.baselineData.set(newBaseline);
    this.dirtyRowKeys.set(new Set());
    this.validationErrors.set({});
    this.stopEditing();

    this.save.emit({ updatedData: newBaseline, dirtyCount: count });
    this.dataChange.emit(newBaseline);

    this.feedbackMessage.set({
      type: 'success',
      text: `Successfully saved ${count} record(s) to sample demonstration state.`
    });
  }

  // Discard changes
  cancelAllChanges(): void {
    const reverted = JSON.parse(JSON.stringify(this.baselineData()));
    this.workingData.set(reverted);
    this.dirtyRowKeys.set(new Set());
    this.validationErrors.set({});
    this.stopEditing();
    this.cancelEdits.emit();

    this.feedbackMessage.set({
      type: 'info',
      text: 'All unsaved edits have been discarded and reset to baseline.'
    });
  }

  // Selection
  isRowSelected(row: T): boolean {
    const key = this.getRowKey(row);
    return this.selection().some(s => this.getRowKey(s) === key);
  }

  readonly isAllPageSelected = computed(() => {
    const pageRows = this.displayedRows();
    if (pageRows.length === 0) return false;
    return pageRows.every(row => this.isRowSelected(row));
  });

  readonly isIndeterminatePageSelected = computed(() => {
    const pageRows = this.displayedRows();
    if (pageRows.length === 0) return false;
    const selectedInPage = pageRows.filter(row => this.isRowSelected(row)).length;
    return selectedInPage > 0 && selectedInPage < pageRows.length;
  });

  toggleRowSelection(row: T, selected: boolean, event?: Event): void {
    if (event) event.stopPropagation();
    const current = [...this.selection()];
    const key = this.getRowKey(row);

    if (this.selectionMode === 'single') {
      const next = selected ? [row] : [];
      this.selection.set(next);
      this.selectionChange.emit(next);
      return;
    }

    if (selected) {
      if (!current.some(s => this.getRowKey(s) === key)) current.push(row);
    } else {
      const idx = current.findIndex(s => this.getRowKey(s) === key);
      if (idx >= 0) current.splice(idx, 1);
    }
    this.selection.set(current);
    this.selectionChange.emit(current);
  }

  toggleSelectAllPage(checked: boolean): void {
    const pageRows = this.displayedRows();
    let current = [...this.selection()];

    if (checked) {
      for (const row of pageRows) {
        const key = this.getRowKey(row);
        if (!current.some(s => this.getRowKey(s) === key)) current.push(row);
      }
    } else {
      const pageKeys = new Set(pageRows.map(r => this.getRowKey(r)));
      current = current.filter(s => !pageKeys.has(this.getRowKey(s)));
    }
    this.selection.set(current);
    this.selectionChange.emit(current);
  }

  selectAllRecords(): void {
    const all = [...this.processedData()];
    this.selection.set(all);
    this.selectionChange.emit(all);
  }

  clearSelection(): void {
    this.selection.set([]);
    this.selectionChange.emit([]);
  }

  // Filter & sort
  onGlobalSearch(term: string): void {
    this.globalSearchTerm.set(term);
    this.pageOffset.set(0);
  }

  onColumnFilter(event: { field: string; value: any; matchMode: string }): void {
    const current = { ...this.columnFilters() };
    if (event.value == null || event.value === '') {
      delete current[event.field];
    } else {
      current[event.field] = { field: event.field, value: event.value, matchMode: event.matchMode as any };
    }
    this.columnFilters.set(current);
    this.pageOffset.set(0);
  }

  onColumnFilterClear(field: string): void {
    const current = { ...this.columnFilters() };
    delete current[field];
    this.columnFilters.set(current);
    this.pageOffset.set(0);
  }

  onColumnSort(event: { field: string; order: 1 | -1 | 0 }): void {
    this.activeSortField.set(event.order === 0 ? undefined : event.field);
    this.activeSortOrder.set(event.order);
  }

  onPageChange(event: { first: number; rows: number; page: number }): void {
    this.pageOffset.set(event.first);
    this.rows = event.rows;
  }

  onColumnsChange(newCols: DataTableColumn<T>[]): void {
    this.columnsSignal.set(newCols);
  }

  formatValue(col: DataTableColumn<T>, item: T): string {
    const val = item[col.field as string];
    if (col.format) return col.format(val, item);
    if (val === null || val === undefined || val === '') return '';
    if (col.type === 'currency' && typeof val === 'number') {
      return new Intl.NumberFormat(this.currencyLocale, {
        style: 'currency',
        currency: this.currencyCode
      }).format(val);
    }
    return String(val);
  }
}
