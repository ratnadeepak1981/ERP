export type ColumnType = 'text' | 'number' | 'currency' | 'date' | 'badge' | 'actions';

export interface TableColumn<T = any> {
  field: keyof T | string;
  header: string;
  type?: ColumnType;
  sortable?: boolean;
  filterable?: boolean;
  width?: string;
  align?: 'left' | 'center' | 'right';
  badgeSeverityMap?: (value: any, item: T) => 'success' | 'warning' | 'danger' | 'info' | 'neutral';
  format?: (value: any, item: T) => string;
}
