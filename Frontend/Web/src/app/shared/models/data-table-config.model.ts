import { TemplateRef } from '@angular/core';

export type ColumnType =
  | 'text'
  | 'number'
  | 'currency'
  | 'date'
  | 'boolean'
  | 'badge'
  | 'actions';

export type FilterType = 'text' | 'numeric' | 'date' | 'boolean' | 'select';

import { LookupItem } from '../components/lookup-dropdown/lookup-item.model';

export type EditType = 'text' | 'number' | 'currency' | 'select' | 'lookup' | 'date';

export interface SelectOption<V = any> {
  label: string;
  value: V;
}

export interface DataTableColumn<T = any> {
  field: keyof T | string;
  header: string;
  type?: ColumnType;
  sortable?: boolean;
  filterable?: boolean;
  filterType?: FilterType;
  filterOptions?: SelectOption[];
  editable?: boolean;
  editType?: EditType;
  editOptions?: SelectOption[];
  lookupOptions?: LookupItem[];
  onLookupSelect?: (selectedItem: LookupItem, row: T) => void;
  validators?: ((value: any, row: T) => string | null)[];
  width?: string;
  minWidth?: string;
  maxWidth?: string;
  align?: 'left' | 'center' | 'right';
  visible?: boolean;
  badgeSeverityMap?: (value: any, item: T) => 'success' | 'warning' | 'danger' | 'info' | 'neutral';
  format?: (value: any, item: T) => string;
  cellTemplate?: TemplateRef<{ $implicit: any; row: T; col: DataTableColumn<T> }>;
}

export interface DataTableAction<T = any> {
  id: string;
  label: string;
  icon?: string;
  severity?: 'primary' | 'secondary' | 'success' | 'info' | 'warning' | 'danger';
  outlined?: boolean;
  disabled?: (row: T) => boolean;
  visible?: (row: T) => boolean;
  action: (row: T) => void;
}

export interface TableSortMeta {
  field: string;
  order: 1 | -1;
}

export interface TableFilterMeta {
  field: string;
  value: any;
  matchMode?: 'contains' | 'startsWith' | 'endsWith' | 'equals' | 'notEquals' | 'gt' | 'lt' | 'gte' | 'lte' | 'in';
}

export interface TableQueryState {
  first: number;
  rows: number;
  sortField?: string;
  sortOrder?: 1 | -1;
  multiSortMeta?: TableSortMeta[];
  filters?: Record<string, TableFilterMeta>;
  globalFilter?: string;
}

export interface DataTableConfig<T = any> {
  data: T[];
  columns: DataTableColumn<T>[];
  rowKey: keyof T | string;

  mode?: 'readonly' | 'editable';

  showSearch?: boolean;
  showSorting?: boolean;
  showFilters?: boolean;
  showPagination?: boolean;
  showSelection?: boolean;
  showColumnChooser?: boolean;
  showExport?: boolean;
  showViewSwitcher?: boolean;
  availableViews?: ('table' | 'list' | 'card')[];

  filterMode?: 'menu' | 'inline';
  sortingMode?: 'single' | 'multiple';
  processingMode?: 'client' | 'server';

  pageSize?: number;
  pageSizeOptions?: number[];

  rowActions?: DataTableAction<T>[];
  searchPlaceholder?: string;
  emptyTitle?: string;
  emptyDescription?: string;
}
