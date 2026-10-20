export type MaterialColumnType =
  | 'text'
  | 'number'
  | 'currency'
  | 'select'
  | 'lookup'
  | 'date'
  | 'boolean'
  | 'badge'
  | 'calculated'
  | 'actions';

export interface MaterialSelectOption<TValue = any> {
  value: TValue;
  label: string;
  subtitle?: string;
  badge?: string;
  badgeColor?: string;
  rawItem?: any;
}

export interface MaterialLookupColumn {
  field: string;
  header: string;
  width?: string;
}

export interface MaterialLookupConfig<TItem = any> {
  title?: string;
  searchPlaceholder?: string;
  displayField: string;
  valueField: string;
  items: TItem[] | (() => TItem[]);
  columns: MaterialLookupColumn[];
  filterFn?: (item: TItem, query: string) => boolean;
  onSelect?: (item: TItem, row: any) => void;
  enableDialog?: boolean;
}

export interface MaterialTableColumn<T = any> {
  field: string;
  header: string;
  type?: MaterialColumnType;
  width?: string;
  minWidth?: string;
  maxWidth?: string;
  align?: 'left' | 'center' | 'right';
  editable?: boolean | ((row: T) => boolean);
  required?: boolean;
  placeholder?: string;
  options?: MaterialSelectOption[] | ((row: T) => MaterialSelectOption[]);
  lookupConfig?: MaterialLookupConfig<any>;
  validators?: Array<(value: any, row: T) => string | null>;
  format?: (value: any, row: T) => string;
  step?: number | string;
  min?: number;
  max?: number;
  allowFreeText?: boolean;
}

export interface CellCoord {
  rowIndex: number;
  colField: string;
}

export type TableValidationErrors = Record<number, Record<string, string>>;

export interface MaterialTableAction<T = any> {
  id: string;
  label: string;
  icon?: string;
  color?: 'primary' | 'accent' | 'warn';
  disabled?: (row: T) => boolean;
  action: (row: T, index: number) => void;
}
