import { Component, ChangeDetectionStrategy, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ButtonModule } from 'primeng/button';
import { TableSearchComponent } from '../../../../../shared/components/data-table/subcomponents/table-search.component';
import { TableSortComponent } from '../../../../../shared/components/data-table/subcomponents/table-sort.component';
import { TableFilterComponent } from '../../../../../shared/components/data-table/subcomponents/table-filter.component';
import { TablePaginationComponent } from '../../../../../shared/components/data-table/subcomponents/table-pagination.component';
import { TableSelectionComponent } from '../../../../../shared/components/data-table/subcomponents/table-selection.component';
import { TableColumnChooserComponent } from '../../../../../shared/components/data-table/subcomponents/table-column-chooser.component';
import { DataTableColumn, TableSortMeta } from '../../../../../shared/models/data-table-config.model';

@Component({
  selector: 'app-modular-controls-section',
  standalone: true,
  imports: [
    CommonModule,
    ButtonModule,
    TableSearchComponent,
    TableSortComponent,
    TableFilterComponent,
    TablePaginationComponent,
    TableSelectionComponent,
    TableColumnChooserComponent
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="modular-controls-container">
      <div class="section-intro erp-card">
        <h3>Independent Reusable Table Subcomponents</h3>
        <p class="text-sm text-secondary">
          Demonstrates search, sorting, filtering, pagination, selection, and column chooser as decoupled,
          independently testable components. Each component emits typed events and accepts modular inputs.
        </p>
      </div>

      <div class="controls-grid">
        <!-- 1. TableSearchComponent -->
        <div class="control-showcase-card erp-card">
          <div class="card-header">
            <h4>1. TableSearchComponent</h4>
            <span class="badge-tag">app-table-search</span>
          </div>
          <p class="text-xs text-muted">Supports debounced typing, search icon, clear button, and Esc shortcut.</p>

          <div class="widget-box">
            <app-table-search
              placeholder="Search sample procurement ledger..."
              [debounceTimeMs]="200"
              (searchChange)="onSearchChange($event)"
              (searchClear)="onSearchClear()"
            ></app-table-search>
          </div>

          <div class="event-log">
            <span>Last Emitted Event: <code>{{ lastSearchEvent() || '(none)' }}</code></span>
          </div>
        </div>

        <!-- 2. TableSortComponent -->
        <div class="control-showcase-card erp-card">
          <div class="card-header">
            <h4>2. TableSortComponent</h4>
            <span class="badge-tag">app-table-sort</span>
          </div>
          <p class="text-xs text-muted">Supports single & multi-column sorting with priority badge indicator.</p>

          <div class="widget-box sort-demo-row">
            <div class="sort-item">
              <span class="field-title">Unit Price (Single):</span>
              <app-table-sort
                field="unitPrice"
                sortMode="single"
                [sortField]="activeSortField()"
                [sortOrder]="activeSortOrder()"
                (sortChange)="onSortChange($event)"
              ></app-table-sort>
            </div>

            <div class="sort-item">
              <span class="field-title">Fulfillment (Multi #1):</span>
              <app-table-sort
                field="fulfillmentStatus"
                sortMode="multiple"
                [multiSortMeta]="multiSortMeta()"
                (sortChange)="onMultiSortChange($event)"
              ></app-table-sort>
            </div>

            <div class="sort-item">
              <span class="field-title">Total Value (Multi #2):</span>
              <app-table-sort
                field="totalValue"
                sortMode="multiple"
                [multiSortMeta]="multiSortMeta()"
                (sortChange)="onMultiSortChange($event)"
              ></app-table-sort>
            </div>
          </div>

          <div class="event-log">
            <span>Sort State: <code>{{ lastSortEvent() || '(none)' }}</code></span>
          </div>
        </div>

        <!-- 3. TableFilterComponent -->
        <div class="control-showcase-card erp-card">
          <div class="card-header">
            <h4>3. TableFilterComponent (Excel-Style Popovers)</h4>
            <span class="badge-tag">app-table-filter</span>
          </div>
          <p class="text-xs text-muted">Excel-style filter overlays for Text, Numeric, Date, and Select options.</p>

          <div class="widget-box filter-demo-row">
            <div class="filter-demo-item">
              <span class="field-title">Product Code (Text):</span>
              <app-table-filter
                [column]="textCol"
                (filterChange)="onFilterChange($event)"
                (filterClear)="onFilterClear($event)"
              ></app-table-filter>
            </div>

            <div class="filter-demo-item">
              <span class="field-title">Quantity (Numeric):</span>
              <app-table-filter
                [column]="numCol"
                (filterChange)="onFilterChange($event)"
                (filterClear)="onFilterClear($event)"
              ></app-table-filter>
            </div>

            <div class="filter-demo-item">
              <span class="field-title">Workflow State (Select):</span>
              <app-table-filter
                [column]="selectCol"
                (filterChange)="onFilterChange($event)"
                (filterClear)="onFilterClear($event)"
              ></app-table-filter>
            </div>
          </div>

          <div class="event-log">
            <span>Last Filter Applied: <code>{{ lastFilterEvent() || '(none)' }}</code></span>
          </div>
        </div>

        <!-- 4. TableColumnChooserComponent -->
        <div class="control-showcase-card erp-card">
          <div class="card-header">
            <h4>4. TableColumnChooserComponent</h4>
            <span class="badge-tag">app-table-column-chooser</span>
          </div>
          <p class="text-xs text-muted">Toggle visibility of specific table columns dynamically.</p>

          <div class="widget-box">
            <app-table-column-chooser
              [columns]="demoColumns"
              (columnsChange)="onColumnsChange($event)"
            ></app-table-column-chooser>
          </div>

          <div class="event-log">
            <span>Visible Columns: <code>{{ getVisibleColumnNames() }}</code></span>
          </div>
        </div>

        <!-- 5. TableSelectionComponent -->
        <div class="control-showcase-card erp-card full-width">
          <div class="card-header">
            <h4>5. TableSelectionComponent (Bulk Toolbar)</h4>
            <span class="badge-tag">app-table-selection</span>
          </div>
          <p class="text-xs text-muted">High-visibility bar indicating selected records and bulk operations.</p>

          <div class="widget-box">
            <div class="selection-simulator-btns">
              <p-button label="Simulate 3 Selected" size="small" [outlined]="true" severity="secondary" (onClick)="selectedCount.set(3)"></p-button>
              <p-button label="Simulate 12 Selected" size="small" [outlined]="true" severity="secondary" (onClick)="selectedCount.set(12)"></p-button>
              <p-button label="Reset to 0" size="small" [text]="true" severity="secondary" (onClick)="selectedCount.set(0)"></p-button>
            </div>

            <app-table-selection
              [selectedCount]="selectedCount()"
              [totalCount]="48"
              (selectAll)="selectedCount.set(48)"
              (clearSelection)="selectedCount.set(0)"
            >
              <div actions>
                <p-button label="Batch Export" icon="pi pi-download" size="small" severity="secondary"></p-button>
                <p-button label="Batch Archive" icon="pi pi-archive" size="small" severity="secondary"></p-button>
              </div>
            </app-table-selection>
          </div>
        </div>

        <!-- 6. TablePaginationComponent -->
        <div class="control-showcase-card erp-card full-width">
          <div class="card-header">
            <h4>6. TablePaginationComponent (Standalone)</h4>
            <span class="badge-tag">app-table-pagination</span>
          </div>
          <p class="text-xs text-muted">Decoupled pagination bar with page-size options and record bounds calculation.</p>

          <div class="widget-box">
            <app-table-pagination
              [first]="pagOffset()"
              [rows]="pagRows()"
              [totalRecords]="85"
              [rowsPerPageOptions]="[5, 10, 25, 50]"
              (pageChange)="onPagChange($event)"
            ></app-table-pagination>
          </div>

          <div class="event-log">
            <span>Pagination Event: <code>Offset: {{ pagOffset() }}, Rows: {{ pagRows() }}, Page: {{ (pagOffset() / pagRows()) + 1 }} of 9</code></span>
          </div>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .modular-controls-container {
      display: flex;
      flex-direction: column;
      gap: var(--erp-space-4, 16px);

      .controls-grid {
        display: grid;
        grid-template-columns: 1fr 1fr;
        gap: 16px;

        @media (max-width: 900px) {
          grid-template-columns: 1fr;
        }

        .control-showcase-card {
          display: flex;
          flex-direction: column;
          gap: 10px;

          &.full-width {
            grid-column: 1 / -1;
          }

          .card-header {
            display: flex;
            align-items: center;
            justify-content: space-between;

            h4 {
              margin: 0;
              font-size: 14px;
            }

            .badge-tag {
              font-family: var(--erp-font-mono);
              font-size: 11px;
              color: var(--erp-primary, #0f6cbd);
              background: var(--erp-primary-light, #ebf3fc);
              padding: 2px 6px;
              border-radius: var(--erp-radius-sm, 4px);
            }
          }

          .widget-box {
            padding: 12px;
            background: var(--erp-bg-secondary, #f1f5f9);
            border-radius: var(--erp-radius-sm, 4px);
            border: 1px solid var(--erp-border, #e2e8f0);

            .sort-demo-row, .filter-demo-row {
              display: flex;
              gap: 16px;
              flex-wrap: wrap;

              .sort-item, .filter-demo-item {
                display: flex;
                align-items: center;
                gap: 6px;

                .field-title {
                  font-size: 12.5px;
                  color: var(--erp-text-secondary, #475569);
                }
              }
            }

            .selection-simulator-btns {
              display: flex;
              gap: 8px;
              margin-bottom: 8px;
            }
          }

          .event-log {
            font-size: 12px;
            color: var(--erp-text-secondary, #475569);

            code {
              font-family: var(--erp-font-mono);
              color: var(--erp-primary, #0f6cbd);
            }
          }
        }
      }
    }
  `]
})
export class ModularControlsSectionComponent {
  readonly lastSearchEvent = signal<string>('');
  readonly lastSortEvent = signal<string>('');
  readonly lastFilterEvent = signal<string>('');

  readonly activeSortField = signal<string | undefined>('unitPrice');
  readonly activeSortOrder = signal<1 | -1 | 0>(1);
  readonly multiSortMeta = signal<TableSortMeta[]>([
    { field: 'fulfillmentStatus', order: 1 },
    { field: 'totalValue', order: -1 }
  ]);

  readonly textCol: DataTableColumn = { field: 'productCode', header: 'Product Code', filterType: 'text' };
  readonly numCol: DataTableColumn = { field: 'quantity', header: 'Quantity', filterType: 'numeric' };
  readonly selectCol: DataTableColumn = {
    field: 'status',
    header: 'Status',
    filterType: 'select',
    filterOptions: [
      { label: 'Approved', value: 'Approved' },
      { label: 'Pending', value: 'Pending' },
      { label: 'Draft', value: 'Draft' }
    ]
  };

  demoColumns: DataTableColumn[] = [
    { field: 'code', header: 'Code', visible: true },
    { field: 'name', header: 'Product Name', visible: true },
    { field: 'category', header: 'Category', visible: true },
    { field: 'price', header: 'Unit Price', visible: false },
    { field: 'stock', header: 'Stock Level', visible: true }
  ];

  readonly selectedCount = signal<number>(3);
  readonly pagOffset = signal<number>(0);
  readonly pagRows = signal<number>(10);

  onSearchChange(term: string): void {
    this.lastSearchEvent.set(`searchChange: "${term}"`);
  }

  onSearchClear(): void {
    this.lastSearchEvent.set('searchClear() invoked');
  }

  onSortChange(event: { field: string; order: 1 | -1 | 0 }): void {
    this.activeSortField.set(event.order === 0 ? undefined : event.field);
    this.activeSortOrder.set(event.order);
    this.lastSortEvent.set(`field: ${event.field}, order: ${event.order}`);
  }

  onMultiSortChange(event: { field: string; order: 1 | -1 | 0 }): void {
    let metas = [...this.multiSortMeta()];
    const idx = metas.findIndex(m => m.field === event.field);
    if (event.order === 0) {
      if (idx >= 0) metas.splice(idx, 1);
    } else {
      if (idx >= 0) metas[idx].order = event.order;
      else metas.push({ field: event.field, order: event.order });
    }
    this.multiSortMeta.set(metas);
    this.lastSortEvent.set(JSON.stringify(metas));
  }

  onFilterChange(event: any): void {
    this.lastFilterEvent.set(JSON.stringify(event));
  }

  onFilterClear(field: string): void {
    this.lastFilterEvent.set(`Cleared filter on ${field}`);
  }

  onColumnsChange(newCols: DataTableColumn[]): void {
    this.demoColumns = newCols;
  }

  getVisibleColumnNames(): string {
    return this.demoColumns.filter(c => c.visible !== false).map(c => c.header).join(', ');
  }

  onPagChange(event: any): void {
    this.pagOffset.set(event.first);
    this.pagRows.set(event.rows);
  }
}
