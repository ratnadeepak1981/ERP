import { Component, signal, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { AppDataTableComponent } from '../../../../../shared/components/data-table/data-table.component';
import { AppStatusBadgeComponent } from '../../../../../shared/components/status-badge/status-badge.component';
import { DataTableColumn, DataTableAction } from '../../../../../shared/models/data-table-config.model';

export interface ProductMasterItem {
  id: string;
  productCode: string;
  productName: string;
  category: string;
  stock: number;
  price: number;
  uom: string;
  status: 'Active' | 'Low Stock' | 'Critical';
}

export const SAMPLE_PRODUCTS: ProductMasterItem[] = [
  {
    id: 'PRD-1',
    productCode: 'P001',
    productName: 'Steel Sheet',
    category: 'Raw Materials',
    stock: 250,
    price: 125.50,
    uom: 'SHEET',
    status: 'Active'
  },
  {
    id: 'PRD-2',
    productCode: 'P002',
    productName: 'Finished Panel',
    category: 'Finished Goods',
    stock: 120,
    price: 89.50,
    uom: 'PCS',
    status: 'Active'
  },
  {
    id: 'PRD-3',
    productCode: 'P003',
    productName: 'Aluminium Rod',
    category: 'Raw Materials',
    stock: 410,
    price: 45.00,
    uom: 'MTR',
    status: 'Active'
  },
  {
    id: 'PRD-4',
    productCode: 'P004',
    productName: 'Copper Wiring',
    category: 'Components',
    stock: 85,
    price: 210.00,
    uom: 'ROLL',
    status: 'Low Stock'
  },
  {
    id: 'PRD-5',
    productCode: 'P005',
    productName: 'Assembly Bracket',
    category: 'Components',
    stock: 650,
    price: 14.25,
    uom: 'PCS',
    status: 'Active'
  },
  {
    id: 'PRD-6',
    productCode: 'P006',
    productName: 'Control Module',
    category: 'Electronics',
    stock: 18,
    price: 540.00,
    uom: 'UNIT',
    status: 'Critical'
  },
  {
    id: 'PRD-7',
    productCode: 'P007',
    productName: 'Fastener Bolts M8',
    category: 'Hardware',
    stock: 2400,
    price: 0.85,
    uom: 'BOX',
    status: 'Active'
  },
  {
    id: 'PRD-8',
    productCode: 'P008',
    productName: 'Industrial Sealant',
    category: 'Consumables',
    stock: 45,
    price: 32.00,
    uom: 'CAN',
    status: 'Low Stock'
  }
];

@Component({
  selector: 'app-readonly-table-section',
  standalone: true,
  imports: [CommonModule, ButtonModule, DialogModule, AppDataTableComponent, AppStatusBadgeComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="readonly-section-container">
      <div class="section-intro erp-card">
        <div class="intro-header">
          <div>
            <div class="title-meta">
              <h3 class="product-master-title">Product Master</h3>
              <span class="product-master-subtitle">Inventory / Products</span>
            </div>
            <p class="text-sm text-secondary">
              Demonstrates <strong>Table</strong>, <strong>List</strong>, and <strong>Card</strong> views powered by the exact same underlying dataset.
              Switching views preserves active search terms, column sorting, pagination, and selected records.
            </p>
          </div>
          <div class="intro-actions">
            <span class="view-hint">
              <i class="pi pi-info-circle"></i>
              Use the 3 Windows File Explorer-style icons at the top-right of the table to switch views.
            </span>
          </div>
        </div>
      </div>

      <app-data-table
        #table
        [data]="productsData"
        [columns]="columns"
        [sortMode]="'single'"
        [selectionMode]="'multiple'"
        [rows]="4"
        [rowsPerPageOptions]="[4, 8, 16]"
        rowKey="productCode"
        searchPlaceholder="Search product code, name, category..."
        cardTitleField="productName"
        cardSubtitleField="productCode"
        cardIcon="pi pi-box"
        (rowClick)="onRowInspect($event)"
      >
        <div toolbar>
          <p-button
            label="Selected ({{ table.selectedCount() }})"
            icon="pi pi-check"
            size="small"
            severity="secondary"
            [disabled]="table.selectedCount() === 0"
            (onClick)="onBulkAction(table.selection)"
          ></p-button>
        </div>
      </app-data-table>

      <!-- Inspection Dialog -->
      <p-dialog
        header="Product Master Details"
        [(visible)]="inspectModalVisible"
        [modal]="true"
        [style]="{ width: '480px' }"
        [draggable]="false"
        [resizable]="false"
      >
        @if (inspectedItem(); as item) {
          <div class="inspect-details">
            <div class="detail-row">
              <span class="label">Product Code:</span>
              <strong class="font-mono">{{ item.productCode }}</strong>
            </div>
            <div class="detail-row">
              <span class="label">Product Name:</span>
              <strong>{{ item.productName }}</strong>
            </div>
            <div class="detail-row">
              <span class="label">Category:</span>
              <span>{{ item.category }}</span>
            </div>
            <div class="detail-row">
              <span class="label">Stock Level:</span>
              <span>{{ item.stock }} {{ item.uom }}</span>
            </div>
            <div class="detail-row">
              <span class="label">Unit Price:</span>
              <span>{{ item.price | currency }}</span>
            </div>
            <div class="detail-row">
              <span class="label">Lifecycle Status:</span>
              <app-status-badge
                [label]="item.status"
                [severity]="item.status === 'Active' ? 'success' : (item.status === 'Low Stock' ? 'warning' : 'danger')"
              ></app-status-badge>
            </div>
          </div>
        }
      </p-dialog>
    </div>
  `,
  styles: [`
    .readonly-section-container {
      display: flex;
      flex-direction: column;
      gap: var(--erp-space-3, 12px);

      .section-intro {
        .intro-header {
          display: flex;
          justify-content: space-between;
          align-items: flex-start;
          gap: 16px;
          flex-wrap: wrap;

          .title-meta {
            margin-bottom: 6px;

            .product-master-title {
              margin: 0;
              font-size: 18px;
              font-weight: 700;
              color: var(--erp-text-main, #0f172a);
            }

            .product-master-subtitle {
              font-size: 12px;
              color: var(--erp-text-secondary, #64748b);
            }
          }

          p {
            margin: 0;
            max-width: 680px;
          }

          .view-hint {
            display: inline-flex;
            align-items: center;
            gap: 6px;
            font-size: 12px;
            color: var(--erp-primary, #0f6cbd);
            background: #eff6fc;
            border: 1px solid #cbe2f7;
            padding: 6px 12px;
            border-radius: var(--erp-radius-md, 6px);
          }
        }
      }

      .inspect-details {
        display: flex;
        flex-direction: column;
        gap: 12px;
        padding: 8px 0;

        .detail-row {
          display: flex;
          justify-content: space-between;
          align-items: center;
          padding: 6px 0;
          border-bottom: 1px solid var(--erp-border-subtle, #f1f5f9);

          .label {
            font-size: 13px;
            color: var(--erp-text-secondary, #64748b);
          }
        }
      }
    }
  `]
})
export class ReadonlyTableSectionComponent {
  readonly productsData: ProductMasterItem[] = SAMPLE_PRODUCTS;

  readonly inspectModalVisible = signal<boolean>(false);
  readonly inspectedItem = signal<ProductMasterItem | null>(null);

  readonly columns: DataTableColumn<ProductMasterItem>[] = [
    { field: 'productCode', header: 'Product Code', sortable: true, width: '130px' },
    { field: 'productName', header: 'Product Name', sortable: true },
    { field: 'category', header: 'Category', sortable: true, width: '150px' },
    { field: 'stock', header: 'Stock', sortable: true, type: 'number', align: 'right', width: '100px' },
    { field: 'price', header: 'Price', sortable: true, type: 'currency', align: 'right', width: '120px' },
    {
      field: 'status',
      header: 'Status',
      sortable: true,
      type: 'badge',
      width: '120px',
      badgeSeverityMap: (val: any) => {
        if (val === 'Active') return 'success';
        if (val === 'Low Stock') return 'warning';
        return 'danger';
      }
    }
  ];

  onRowInspect(item: ProductMasterItem): void {
    this.inspectedItem.set(item);
    this.inspectModalVisible.set(true);
  }

  onBulkAction(selected: ProductMasterItem[]): void {
    alert(`Selected ${selected.length} products: ${selected.map(p => p.productCode).join(', ')}`);
  }
}
