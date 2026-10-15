import {
  Component,
  input,
  output,
  model,
  computed,
  ViewChild,
  ContentChild,
  TemplateRef
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Table, TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { TableColumn } from '../../models/table-column.model';
import { AppStatusBadgeComponent } from '../status-badge/status-badge.component';
import { AppLoadingStateComponent } from '../loading-state/loading-state.component';
import { AppEmptyStateComponent } from '../empty-state/empty-state.component';

@Component({
  selector: 'app-data-table',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    TableModule,
    ButtonModule,
    InputTextModule,
    AppStatusBadgeComponent,
    AppLoadingStateComponent,
    AppEmptyStateComponent
  ],
  templateUrl: './data-table.component.html',
  styleUrl: './data-table.component.scss'
})
export class AppDataTableComponent<T extends Record<string, any> = Record<string, any>> {
  @ViewChild('dt') dtTable?: Table;
  @ContentChild('rowActions', { static: false }) rowActionsTemplate?: TemplateRef<{ $implicit: T }>;

  readonly data = input<T[]>([]);
  readonly columns = input<TableColumn<T>[]>([]);
  readonly loading = input<boolean>(false);
  readonly paginator = input<boolean>(true);
  readonly rows = input<number>(10);
  readonly rowsPerPageOptions = input<number[]>([10, 25, 50]);
  readonly searchPlaceholder = input<string>('Search records...');
  readonly showGlobalSearch = input<boolean>(true);
  readonly emptyTitle = input<string>('No records available');
  readonly emptyDescription = input<string>('There is no data matching your current filter.');
  readonly sortMode = input<'single' | 'multiple'>('single');
  readonly currencyCode = input<string>('USD');
  readonly currencyLocale = input<string>('en-US');
  readonly selectionMode = input<'single' | 'multiple' | null>(null);
  readonly actionTemplate = input<TemplateRef<{ $implicit: T }> | null>(null);

  readonly selection = model<T | T[] | null>(null);
  readonly rowClick = output<T>();

  searchValue: string = '';

  // Extract searchable field names for PrimeNG global filter
  readonly globalFilterFields = computed(() => {
    return this.columns()
      .filter(c => c.type !== 'actions')
      .map(c => String(c.field));
  });

  getFieldValue(item: T, field: keyof T | string): any {
    if (!item || !field) return undefined;
    const fieldStr = String(field);
    if (!fieldStr.includes('.')) {
      return item[fieldStr];
    }
    return fieldStr.split('.').reduce((acc: any, part: string) => (acc != null ? acc[part] : undefined), item);
  }

  getBadgeSeverity(col: TableColumn<T>, item: T): 'success' | 'warning' | 'danger' | 'info' | 'neutral' {
    const val = this.getFieldValue(item, col.field);
    if (col.badgeSeverityMap) {
      return col.badgeSeverityMap(val, item);
    }
    return 'neutral';
  }

  formatValue(col: TableColumn<T>, item: T): string {
    const val = this.getFieldValue(item, col.field);
    if (col.format) {
      return col.format(val, item);
    }
    if (val === null || val === undefined) {
      return '-';
    }
    if (col.type === 'currency' && typeof val === 'number') {
      return new Intl.NumberFormat(this.currencyLocale(), {
        style: 'currency',
        currency: this.currencyCode()
      }).format(val);
    }
    return String(val);
  }

  exportCSV(options?: any): void {
    this.dtTable?.exportCSV(options);
  }

  onRowClick(row: T): void {
    this.rowClick.emit(row);
  }
}

export { AppDataTableComponent as DataTableComponent };
