import {
  Component,
  Input,
  Output,
  EventEmitter,
  signal,
  computed,
  ViewChild,
  ContentChild,
  TemplateRef,
  ChangeDetectionStrategy
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Table, TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { TooltipModule } from 'primeng/tooltip';
import {
  DataTableColumn,
  DataTableAction,
  DataTableConfig,
  TableSortMeta,
  TableFilterMeta,
  TableQueryState
} from '../../models/data-table-config.model';
import { TableColumn } from '../../models/table-column.model';
import { AppStatusBadgeComponent } from '../status-badge/status-badge.component';
import { AppLoadingStateComponent } from '../loading-state/loading-state.component';
import { AppEmptyStateComponent } from '../empty-state/empty-state.component';
import { AppCheckboxComponent } from '../checkbox/checkbox.component';
import { TableSearchComponent } from './subcomponents/table-search.component';
import { TableSortComponent } from './subcomponents/table-sort.component';
import { TableFilterComponent } from './subcomponents/table-filter.component';
import { TablePaginationComponent } from './subcomponents/table-pagination.component';
import { TableSelectionComponent } from './subcomponents/table-selection.component';
import { TableColumnChooserComponent } from './subcomponents/table-column-chooser.component';
import { TableViewSwitcherComponent, TableViewMode } from './subcomponents/table-view-switcher.component';

@Component({
  selector: 'app-data-table',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    TableModule,
    ButtonModule,
    InputTextModule,
    TooltipModule,
    AppStatusBadgeComponent,
    AppLoadingStateComponent,
    AppEmptyStateComponent,
    AppCheckboxComponent,
    TableSearchComponent,
    TableSortComponent,
    TableFilterComponent,
    TablePaginationComponent,
    TableSelectionComponent,
    TableColumnChooserComponent,
    TableViewSwitcherComponent
  ],
  templateUrl: './data-table.component.html',
  styleUrl: './data-table.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class AppDataTableComponent<T extends Record<string, any> = Record<string, any>> {
  @ViewChild('dt') dtTable?: Table;
  @ContentChild('rowActions', { static: false }) rowActionsTemplate?: TemplateRef<{ $implicit: T }>;

  // Optional unified config input
  @Input() set config(cfg: DataTableConfig<T> | undefined) {
    if (!cfg) return;
    if (cfg.data) this.dataSignal.set(cfg.data);
    if (cfg.columns) this.columnsSignal.set(cfg.columns);
    if (cfg.rowKey) this.rowKey = String(cfg.rowKey);
    if (cfg.showSearch !== undefined) this.showGlobalSearch = cfg.showSearch;
    if (cfg.showFilters !== undefined) this.showFilters = cfg.showFilters;
    if (cfg.showSorting !== undefined) this.showSorting = cfg.showSorting;
    if (cfg.showPagination !== undefined) this.paginator = cfg.showPagination;
    if (cfg.showSelection !== undefined) this.selectionMode = cfg.showSelection ? 'multiple' : null;
    if (cfg.showColumnChooser !== undefined) this.showColumnChooser = cfg.showColumnChooser;
    if (cfg.showExport !== undefined) this.showExport = cfg.showExport;
    if (cfg.pageSize !== undefined) this.rows = cfg.pageSize;
    if (cfg.pageSizeOptions) this.rowsPerPageOptions = cfg.pageSizeOptions;
    if (cfg.sortingMode) this.sortMode = cfg.sortingMode;
    if (cfg.processingMode) this.processingMode = cfg.processingMode;
    if (cfg.rowActions) this.rowActions = cfg.rowActions;
    if (cfg.searchPlaceholder) this.searchPlaceholder = cfg.searchPlaceholder;
    if (cfg.emptyTitle) this.emptyTitle = cfg.emptyTitle;
    if (cfg.emptyDescription) this.emptyDescription = cfg.emptyDescription;
  }

  // Individual inputs (preserving full backward-compatibility)
  @Input() set data(val: T[]) {
    this.dataSignal.set(val || []);
  }
  get data(): T[] {
    return this.dataSignal();
  }

  @Input() set columns(cols: (DataTableColumn<T> | TableColumn<T>)[]) {
    this.columnsSignal.set(cols || []);
  }
  get columns(): (DataTableColumn<T> | TableColumn<T>)[] {
    return this.columnsSignal();
  }

  @Input() loading: boolean = false;
  @Input() paginator: boolean = true;
  @Input() rows: number = 10;
  @Input() rowsPerPageOptions: number[] = [5, 10, 25, 50];
  @Input() searchPlaceholder: string = 'Search records...';
  @Input() showGlobalSearch: boolean = true;
  @Input() showFilters: boolean = true;
  @Input() showSorting: boolean = true;
  @Input() showColumnChooser: boolean = true;
  @Input() showExport: boolean = true;
  @Input() emptyTitle: string = 'No records available';
  @Input() emptyDescription: string = 'There is no data matching your current filter.';
  @Input() sortMode: 'single' | 'multiple' = 'single';
  @Input() processingMode: 'client' | 'server' = 'client';
  @Input() currencyCode: string = 'USD';
  @Input() currencyLocale: string = 'en-US';
  @Input() selectionMode: 'single' | 'multiple' | null = null;
  @Input() rowKey: string = 'id';
  @Input() rowActions?: DataTableAction<T>[];
  @Input() actionTemplate?: TemplateRef<{ $implicit: T }> | null = null;

  // Selection model
  private readonly _selection = signal<T[]>([]);
  @Input() set selection(val: T | T[] | null) {
    if (val === null || val === undefined) {
      this._selection.set([]);
    } else if (Array.isArray(val)) {
      this._selection.set(val);
    } else {
      this._selection.set([val]);
    }
  }
  get selection(): T[] {
    return this._selection();
  }

  // View Mode: Table, List, Card
  @Input() showViewSwitcher: boolean = true;
  @Input() set viewMode(val: TableViewMode) {
    this.currentView.set(val || 'table');
  }
  get viewMode(): TableViewMode {
    return this.currentView();
  }
  @Input() cardTitleField?: string;
  @Input() cardSubtitleField?: string;
  @Input() cardIcon: string = 'pi pi-box';

  // Outputs
  @Output() viewModeChange = new EventEmitter<TableViewMode>();
  @Output() selectionChange = new EventEmitter<T[]>();
  @Output() rowClick = new EventEmitter<T>();
  @Output() queryChange = new EventEmitter<TableQueryState>();

  // Active view state
  readonly currentView = signal<TableViewMode>('table');

  // Reactive state signals
  readonly dataSignal = signal<T[]>([]);
  readonly columnsSignal = signal<DataTableColumn<T>[]>([]);
  readonly globalSearchTerm = signal<string>('');
  readonly columnFilters = signal<Record<string, TableFilterMeta>>({});
  readonly activeSortField = signal<string | undefined>(undefined);
  readonly activeSortOrder = signal<1 | -1 | 0>(0);
  readonly multiSortMeta = signal<TableSortMeta[]>([]);
  readonly pageOffset = signal<number>(0);

  // Extract searchable field names for global filter
  readonly globalFilterFields = computed(() => {
    return this.columnsSignal()
      .filter(c => c.type !== 'actions')
      .map(c => String(c.field));
  });

  // Visible columns computed from column chooser
  readonly visibleColumns = computed(() => {
    return this.columnsSignal().filter(c => c.visible !== false);
  });

  // Client-side filtering & sorting engine
  readonly processedData = computed(() => {
    let result = [...this.dataSignal()];
    if (this.processingMode === 'server') {
      return result;
    }

    // 1. Global Search
    const search = this.globalSearchTerm().trim().toLowerCase();
    if (search) {
      result = result.filter(item => {
        return this.columnsSignal().some(col => {
          if (col.type === 'actions') return false;
          const val = this.getFieldValue(item, col.field);
          return val != null && String(val).toLowerCase().includes(search);
        });
      });
    }

    // 2. Per-Column Filters
    const filters = this.columnFilters();
    for (const field of Object.keys(filters)) {
      const filter = filters[field];
      if (filter.value == null || filter.value === '') continue;

      result = result.filter(item => {
        const itemVal = this.getFieldValue(item, field);
        if (itemVal == null) return false;

        if (filter.matchMode === 'in' && Array.isArray(filter.value)) {
          return filter.value.length === 0 || filter.value.includes(itemVal);
        }

        const strItem = String(itemVal).toLowerCase();
        const strFilter = String(filter.value).toLowerCase();

        switch (filter.matchMode) {
          case 'startsWith':
            return strItem.startsWith(strFilter);
          case 'equals':
            return strItem === strFilter;
          case 'gt':
            return Number(itemVal) > Number(filter.value);
          case 'gte':
            return Number(itemVal) >= Number(filter.value);
          case 'lt':
            return Number(itemVal) < Number(filter.value);
          case 'lte':
            return Number(itemVal) <= Number(filter.value);
          case 'contains':
          default:
            return strItem.includes(strFilter);
        }
      });
    }

    // 3. Sorting
    if (this.sortMode === 'single' && this.activeSortField() && this.activeSortOrder() !== 0) {
      const field = this.activeSortField()!;
      const order = this.activeSortOrder() as 1 | -1;
      result.sort((a, b) => {
        const valA = this.getFieldValue(a, field);
        const valB = this.getFieldValue(b, field);
        return this.compareValues(valA, valB, order);
      });
    } else if (this.sortMode === 'multiple' && this.multiSortMeta().length > 0) {
      const metas = this.multiSortMeta();
      result.sort((a, b) => {
        for (const meta of metas) {
          const valA = this.getFieldValue(a, meta.field);
          const valB = this.getFieldValue(b, meta.field);
          const comp = this.compareValues(valA, valB, meta.order);
          if (comp !== 0) return comp;
        }
        return 0;
      });
    }

    return result;
  });

  // Current page sliced data for display
  readonly displayedRows = computed(() => {
    if (!this.paginator) {
      return this.processedData();
    }
    const offset = this.pageOffset();
    return this.processedData().slice(offset, offset + this.rows);
  });

  // Selection states
  readonly selectedCount = computed(() => this._selection().length);

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

  isRowSelected(row: T): boolean {
    const key = this.getRowKey(row);
    return this._selection().some(s => this.getRowKey(s) === key);
  }

  getRowKey(row: T): any {
    if (!row) return undefined;
    return row[this.rowKey] !== undefined ? row[this.rowKey] : (row as any).id;
  }

  toggleRowSelection(row: T, selected: boolean, event?: Event): void {
    if (event) event.stopPropagation();
    const current = [...this._selection()];
    const key = this.getRowKey(row);

    if (this.selectionMode === 'single') {
      const next = selected ? [row] : [];
      this._selection.set(next);
      this.selectionChange.emit(next);
      return;
    }

    if (selected) {
      if (!current.some(s => this.getRowKey(s) === key)) {
        current.push(row);
      }
    } else {
      const idx = current.findIndex(s => this.getRowKey(s) === key);
      if (idx >= 0) current.splice(idx, 1);
    }
    this._selection.set(current);
    this.selectionChange.emit(current);
  }

  toggleSelectAllPage(checked: boolean): void {
    const pageRows = this.displayedRows();
    let current = [...this._selection()];

    if (checked) {
      for (const row of pageRows) {
        const key = this.getRowKey(row);
        if (!current.some(s => this.getRowKey(s) === key)) {
          current.push(row);
        }
      }
    } else {
      const pageKeys = new Set(pageRows.map(r => this.getRowKey(r)));
      current = current.filter(s => !pageKeys.has(this.getRowKey(s)));
    }
    this._selection.set(current);
    this.selectionChange.emit(current);
  }

  selectAllRecords(): void {
    const all = [...this.processedData()];
    this._selection.set(all);
    this.selectionChange.emit(all);
  }

  clearSelection(): void {
    this._selection.set([]);
    this.selectionChange.emit([]);
  }

  // Filter & Search Handlers
  onGlobalSearch(term: string): void {
    this.globalSearchTerm.set(term);
    this.pageOffset.set(0);
    this.emitQueryState();
  }

  onColumnFilter(event: { field: string; value: any; matchMode: string }): void {
    const current = { ...this.columnFilters() };
    if (event.value == null || event.value === '') {
      delete current[event.field];
    } else {
      current[event.field] = {
        field: event.field,
        value: event.value,
        matchMode: event.matchMode as any
      };
    }
    this.columnFilters.set(current);
    this.pageOffset.set(0);
    this.emitQueryState();
  }

  onColumnFilterClear(field: string): void {
    const current = { ...this.columnFilters() };
    delete current[field];
    this.columnFilters.set(current);
    this.pageOffset.set(0);
    this.emitQueryState();
  }

  // Sorting Handlers
  onColumnSort(event: { field: string; order: 1 | -1 | 0 }): void {
    if (this.sortMode === 'single') {
      this.activeSortField.set(event.order === 0 ? undefined : event.field);
      this.activeSortOrder.set(event.order);
    } else {
      let metas = [...this.multiSortMeta()];
      const idx = metas.findIndex(m => m.field === event.field);
      if (event.order === 0) {
        if (idx >= 0) metas.splice(idx, 1);
      } else {
        if (idx >= 0) {
          metas[idx].order = event.order;
        } else {
          metas.push({ field: event.field, order: event.order });
        }
      }
      this.multiSortMeta.set(metas);
    }
    this.emitQueryState();
  }

  // Pagination Handler
  onPageChange(event: { first: number; rows: number; page: number }): void {
    this.pageOffset.set(event.first);
    this.rows = event.rows;
    this.emitQueryState();
  }

  // Column Chooser Handler
  onColumnsChange(newCols: DataTableColumn<T>[]): void {
    this.columnsSignal.set(newCols);
  }

  getFieldKey(col: DataTableColumn<T> | TableColumn<T>): string {
    return String(col.field);
  }

  getActionSeverity(severity: string | undefined): 'primary' | 'secondary' | 'success' | 'info' | 'warn' | 'danger' | 'contrast' {
    if (severity === 'warning') return 'warn';
    return (severity as any) || 'secondary';
  }

  // Value formatting & helpers
  getFieldValue(item: T, field: keyof T | string): any {
    if (!item || !field) return undefined;
    const fieldStr = String(field);
    if (!fieldStr.includes('.')) {
      return item[fieldStr];
    }
    return fieldStr.split('.').reduce((acc: any, part: string) => (acc != null ? acc[part] : undefined), item);
  }

  getBadgeSeverity(col: DataTableColumn<T> | TableColumn<T>, item: T): 'success' | 'warning' | 'danger' | 'info' | 'neutral' {
    const val = this.getFieldValue(item, col.field);
    if (col.badgeSeverityMap) {
      return col.badgeSeverityMap(val, item);
    }
    return 'neutral';
  }

  formatValue(col: DataTableColumn<T> | TableColumn<T>, item: T): string {
    const val = this.getFieldValue(item, col.field);
    if (col.format) {
      return col.format(val, item);
    }
    if (val === null || val === undefined) {
      return '-';
    }
    if (col.type === 'currency' && typeof val === 'number') {
      return new Intl.NumberFormat(this.currencyLocale, {
        style: 'currency',
        currency: this.currencyCode
      }).format(val);
    }
    if (col.type === 'number' && typeof val === 'number') {
      return new Intl.NumberFormat(this.currencyLocale).format(val);
    }
    return String(val);
  }

  private compareValues(valA: any, valB: any, order: 1 | -1): number {
    if (valA == null && valB == null) return 0;
    if (valA == null) return order * -1;
    if (valB == null) return order * 1;

    if (typeof valA === 'number' && typeof valB === 'number') {
      return order * (valA - valB);
    }
    return order * String(valA).localeCompare(String(valB), undefined, { numeric: true });
  }

  private emitQueryState(): void {
    this.queryChange.emit({
      first: this.pageOffset(),
      rows: this.rows,
      sortField: this.activeSortField(),
      sortOrder: this.activeSortOrder() || undefined,
      multiSortMeta: this.multiSortMeta(),
      filters: this.columnFilters(),
      globalFilter: this.globalSearchTerm() || undefined
    });
  }

  exportCSV(options?: { selectionOnly?: boolean }): void {
    const data = (options?.selectionOnly && this.selection.length) ? this.selection : this.processedData();
    if (!data.length) return;

    const visibleCols = this.visibleColumns().filter(c => c.type !== 'actions');
    const header = visibleCols.map(c => `"${c.header}"`).join(',');
    const rows = data.map(item => {
      return visibleCols.map(c => {
        const val = this.formatValue(c, item);
        return `"${String(val).replace(/"/g, '""')}"`;
      }).join(',');
    });

    const csvContent = 'data:text/csv;charset=utf-8,' + [header, ...rows].join('\n');
    const encodedUri = encodeURI(csvContent);
    const link = document.createElement('a');
    link.setAttribute('href', encodedUri);
    link.setAttribute('download', `apex_erp_export_${new Date().toISOString().slice(0, 10)}.csv`);
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
  }

  setViewMode(mode: TableViewMode): void {
    this.currentView.set(mode);
    this.viewModeChange.emit(mode);
  }

  getCardTitle(item: T): string {
    if (this.cardTitleField) {
      const val = this.getFieldValue(item, this.cardTitleField);
      if (val != null) return String(val);
    }
    const col = this.visibleColumns().find(c =>
      c.field === 'name' || c.field === 'productName' || c.field === 'title' || (c.type !== 'actions' && c.type !== 'badge')
    );
    return col ? this.formatValue(col, item) : 'Item';
  }

  getCardSubtitle(item: T): string {
    if (this.cardSubtitleField) {
      const val = this.getFieldValue(item, this.cardSubtitleField);
      if (val != null) return String(val);
    }
    const col = this.visibleColumns().find(c =>
      c.field === 'code' || c.field === 'productCode' || c.field === 'sku' || c.field === 'id'
    );
    return col ? String(this.getFieldValue(item, col.field) || '') : '';
  }

  getCardFields(item: T): { header: string; value: string }[] {
    const titleCol = this.columnsSignal().find(c =>
      c.field === this.cardTitleField || c.field === 'name' || c.field === 'productName' || c.field === 'title'
    );
    const subtitleCol = this.columnsSignal().find(c =>
      c.field === this.cardSubtitleField || c.field === 'code' || c.field === 'productCode' || c.field === 'sku' || c.field === 'id'
    );

    return this.visibleColumns()
      .filter(c => c.type !== 'actions' && c.type !== 'badge' && c !== titleCol && c !== subtitleCol)
      .map(c => ({
        header: c.header,
        value: this.formatValue(c, item)
      }));
  }

  getBadgeColumn(): DataTableColumn<T> | undefined {
    return this.visibleColumns().find(c => c.type === 'badge');
  }

  onRowClick(row: T): void {
    this.rowClick.emit(row);
  }
}

export { AppDataTableComponent as DataTableComponent };
