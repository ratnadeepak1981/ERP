import { Component, ChangeDetectionStrategy, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ButtonModule } from 'primeng/button';
import { AppEditableDataTableComponent } from '../../../../../shared/components/data-table/editable-data-table.component';
import { DataTableColumn } from '../../../../../shared/models/data-table-config.model';
import {
  PurchaseOrderItem,
  SAMPLE_PURCHASE_ORDER_ITEMS,
  PO_LOOKUP_PRODUCTS
} from '../purchase-order.model';
import {
  SalesOrderItem,
  SAMPLE_SALES_ORDER_ITEMS,
  SO_LOOKUP_PRODUCTS
} from '../sales-order.model';

type EditableExampleMode = 'purchase-order' | 'sales-order';

@Component({
  selector: 'app-editable-table-section',
  standalone: true,
  imports: [CommonModule, ButtonModule, AppEditableDataTableComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="editable-section-container">
      <div class="section-intro erp-card">
        <div class="intro-header">
          <div>
            <h3>Reusable Editable Data Table: Transaction Entry</h3>
            <p class="text-sm text-secondary">
              Demonstrates click-to-edit, blank row creation, tabbing to auto-add rows, and embedded product lookup across
              <strong>Procurement (Purchase Order)</strong> and <strong>Commercial (Sales Order)</strong>.
            </p>
          </div>
          <div class="example-switcher-group">
            <button
              type="button"
              class="example-tab-btn"
              [class.active]="selectedExample() === 'purchase-order'"
              (click)="selectedExample.set('purchase-order')"
            >
              <i class="pi pi-shopping-cart"></i>
              <span>Example A: Purchase Order</span>
            </button>
            <button
              type="button"
              class="example-tab-btn"
              [class.active]="selectedExample() === 'sales-order'"
              (click)="selectedExample.set('sales-order')"
            >
              <i class="pi pi-tag"></i>
              <span>Example B: Sales Order</span>
            </button>
          </div>
        </div>
      </div>

      <!-- EXAMPLE A: PURCHASE ORDER ITEMS (Procurement) -->
      @if (selectedExample() === 'purchase-order') {
        <div class="example-wrapper">
          <div class="example-meta-bar erp-card">
            <div class="meta-info">
              <span class="order-badge po-badge"><i class="pi pi-file"></i> PO-2026-089</span>
              <span class="vendor-name">Vendor: Industrial Steel &amp; Metal Supplies Ltd.</span>
            </div>
            <div class="meta-stats">
              <span class="calc-formula">Formula: <code>Quantity &times; Unit Cost = Line Total</code> &bull; Click any cell to edit &bull; Press Tab at the last column to add a blank row</span>
            </div>
          </div>

          <app-editable-data-table
            [data]="poData"
            [columns]="poColumns"
            [rows]="5"
            [rowsPerPageOptions]="[5, 10, 20]"
            rowKey="lineId"
            searchPlaceholder="Search product code or name..."
            [defaultNewRow]="defaultNewPORow"
            [onRowCalculate]="calculatePORow"
            subtotalLabel="Purchase Order Subtotal"
            subtotalField="lineTotal"
            (save)="onSaveEdits('Purchase Order', $event)"
            (cancelEdits)="onCancelEdits('Purchase Order')"
          ></app-editable-data-table>
        </div>
      }

      <!-- EXAMPLE B: SALES ORDER ITEMS (Commercial) -->
      @if (selectedExample() === 'sales-order') {
        <div class="example-wrapper">
          <div class="example-meta-bar erp-card">
            <div class="meta-info">
              <span class="order-badge so-badge"><i class="pi pi-file"></i> SO-2026-541</span>
              <span class="vendor-name">Customer: Global Logistics Equipment Corp.</span>
            </div>
            <div class="meta-stats">
              <span class="calc-formula">Formula: <code>Quantity &times; Unit Price = Line Total</code> &bull; Click any cell to edit &bull; Press Tab at the last column to add a blank row</span>
            </div>
          </div>

          <app-editable-data-table
            [data]="soData"
            [columns]="soColumns"
            [rows]="5"
            [rowsPerPageOptions]="[5, 10, 20]"
            rowKey="lineId"
            searchPlaceholder="Search product code or name..."
            [defaultNewRow]="defaultNewSORow"
            [onRowCalculate]="calculateSORow"
            subtotalLabel="Sales Order Subtotal"
            subtotalField="lineTotal"
            (save)="onSaveEdits('Sales Order', $event)"
            (cancelEdits)="onCancelEdits('Sales Order')"
          ></app-editable-data-table>
        </div>
      }
    </div>
  `,
  styles: [`
    .editable-section-container {
      display: flex;
      flex-direction: column;
      gap: 16px;

      .section-intro {
        padding: 16px 20px;
        background: #ffffff;
        border: 1px solid var(--erp-border, #e2e8f0);
        border-radius: 6px;

        .intro-header {
          display: flex;
          justify-content: space-between;
          align-items: center;
          gap: 16px;
          flex-wrap: wrap;

          h3 {
            margin: 0 0 4px;
            font-size: 16px;
            font-weight: 700;
            color: var(--erp-text-main, #0f172a);
          }

          p {
            margin: 0;
            max-width: 650px;
          }

          .example-switcher-group {
            display: inline-flex;
            gap: 4px;
            padding: 3px;
            background-color: var(--erp-bg-secondary, #f1f5f9);
            border-radius: var(--erp-radius-md, 6px);
            border: 1px solid var(--erp-border, #e2e8f0);

            .example-tab-btn {
              display: inline-flex;
              align-items: center;
              gap: 8px;
              padding: 6px 14px;
              border: none;
              background: transparent;
              color: var(--erp-text-secondary, #64748b);
              font-size: 12.5px;
              font-weight: 500;
              border-radius: var(--erp-radius-sm, 4px);
              cursor: pointer;
              transition: all 0.15s ease-in-out;

              i {
                font-size: 13px;
              }

              &:hover:not(.active) {
                color: var(--erp-text-main, #0f172a);
              }

              &.active {
                background: #ffffff;
                color: var(--erp-primary, #0f6cbd);
                font-weight: 600;
                box-shadow: 0 1px 3px rgba(0, 0, 0, 0.08);
              }
            }
          }
        }
      }

      .example-wrapper {
        display: flex;
        flex-direction: column;
        gap: 12px;

        .example-meta-bar {
          display: flex;
          align-items: center;
          justify-content: space-between;
          padding: 10px 16px;
          background: #ffffff;
          border: 1px solid var(--erp-border, #e2e8f0);
          border-radius: 6px;
          flex-wrap: wrap;
          gap: 12px;

          .meta-info {
            display: flex;
            align-items: center;
            gap: 12px;

            .order-badge {
              display: inline-flex;
              align-items: center;
              gap: 6px;
              padding: 4px 10px;
              font-size: 12.5px;
              font-weight: 700;
              border-radius: 4px;

              &.po-badge {
                background: #eff6fc;
                color: #0f6cbd;
                border: 1px solid #c7e0f4;
              }

              &.so-badge {
                background: #fdf3e7;
                color: #b74700;
                border: 1px solid #f9d9b6;
              }
            }

            .vendor-name {
              font-size: 13px;
              font-weight: 500;
              color: var(--erp-text-main, #0f172a);
            }
          }

          .meta-stats {
            .calc-formula {
              font-size: 12px;
              color: var(--erp-text-secondary, #64748b);

              code {
                background: #f1f5f9;
                padding: 2px 6px;
                border-radius: 3px;
                font-family: var(--erp-font-mono, monospace);
                color: #0f172a;
              }
            }
          }
        }
      }
    }
  `]
})
export class AppEditableTableSectionComponent {
  readonly selectedExample = signal<EditableExampleMode>('purchase-order');

  readonly poData: PurchaseOrderItem[] = SAMPLE_PURCHASE_ORDER_ITEMS;
  readonly soData: SalesOrderItem[] = SAMPLE_SALES_ORDER_ITEMS;

  // Example A: Purchase Order Columns
  readonly poColumns: DataTableColumn<PurchaseOrderItem>[] = [
    {
      field: 'productCode',
      header: 'Product',
      sortable: true,
      width: '240px',
      editable: true,
      editType: 'lookup',
      lookupOptions: PO_LOOKUP_PRODUCTS,
      onLookupSelect: (item, row) => {
        row.productName = item.name;
        if (item.meta?.cost) {
          row.unitCost = item.meta.cost;
        }
      },
      validators: [(val: any) => (val ? null : 'Product is required')]
    },
    {
      field: 'productName',
      header: 'Description',
      sortable: true,
      width: '200px',
      editable: true,
      editType: 'text',
      validators: [(val: any) => (val && String(val).trim().length > 0 ? null : 'Description is required')]
    },
    {
      field: 'quantity',
      header: 'Quantity',
      sortable: true,
      align: 'right',
      width: '110px',
      editable: true,
      editType: 'number',
      validators: [
        (val: any) => {
          const num = Number(val);
          if (isNaN(num)) return 'Must be a number';
          if (num <= 0) return 'Must be > 0';
          return null;
        }
      ]
    },
    {
      field: 'unitCost',
      header: 'Unit Cost',
      sortable: true,
      align: 'right',
      width: '120px',
      type: 'currency',
      editable: true,
      editType: 'currency',
      validators: [
        (val: any) => {
          const num = Number(val);
          if (isNaN(num)) return 'Must be a valid cost';
          if (num < 0) return 'Cannot be negative';
          return null;
        }
      ]
    },
    {
      field: 'lineTotal',
      header: 'Line Total',
      sortable: true,
      align: 'right',
      width: '130px',
      type: 'currency',
      editable: false
    }
  ];

  // Example B: Sales Order Columns
  readonly soColumns: DataTableColumn<SalesOrderItem>[] = [
    {
      field: 'productCode',
      header: 'Product',
      sortable: true,
      width: '240px',
      editable: true,
      editType: 'lookup',
      lookupOptions: SO_LOOKUP_PRODUCTS,
      onLookupSelect: (item, row) => {
        row.productName = item.name;
        if (item.meta?.price) {
          row.unitPrice = item.meta.price;
        }
      },
      validators: [(val: any) => (val ? null : 'Product is required')]
    },
    {
      field: 'productName',
      header: 'Description',
      sortable: true,
      width: '200px',
      editable: true,
      editType: 'text',
      validators: [(val: any) => (val && String(val).trim().length > 0 ? null : 'Description is required')]
    },
    {
      field: 'quantity',
      header: 'Quantity',
      sortable: true,
      align: 'right',
      width: '110px',
      editable: true,
      editType: 'number',
      validators: [
        (val: any) => {
          const num = Number(val);
          if (isNaN(num)) return 'Must be a number';
          if (num <= 0) return 'Must be > 0';
          return null;
        }
      ]
    },
    {
      field: 'unitPrice',
      header: 'Unit Price',
      sortable: true,
      align: 'right',
      width: '120px',
      type: 'currency',
      editable: true,
      editType: 'currency',
      validators: [
        (val: any) => {
          const num = Number(val);
          if (isNaN(num)) return 'Must be a valid price';
          if (num < 0) return 'Cannot be negative';
          return null;
        }
      ]
    },
    {
      field: 'lineTotal',
      header: 'Line Total',
      sortable: true,
      align: 'right',
      width: '130px',
      type: 'currency',
      editable: false
    }
  ];

  // Calculation callback for Purchase Order
  readonly calculatePORow = (row: PurchaseOrderItem): void => {
    const qty = Number(row.quantity) || 0;
    const cost = Number(row.unitCost) || 0;
    row.lineTotal = Math.round(qty * cost * 100) / 100;
  };

  // Calculation callback for Sales Order
  readonly calculateSORow = (row: SalesOrderItem): void => {
    const qty = Number(row.quantity) || 0;
    const price = Number(row.unitPrice) || 0;
    row.lineTotal = Math.round(qty * price * 100) / 100;
  };

  // Default blank row generators (creates empty blank lines)
  readonly defaultNewPORow = (): Partial<PurchaseOrderItem> => {
    return {
      productCode: '',
      productName: '',
      quantity: 1,
      unitCost: 0,
      lineTotal: 0,
      status: 'Draft'
    };
  };

  readonly defaultNewSORow = (): Partial<SalesOrderItem> => {
    return {
      productCode: '',
      productName: '',
      quantity: 1,
      unitPrice: 0,
      lineTotal: 0,
      status: 'Processing'
    };
  };

  onSaveEdits(context: string, event: { updatedData: any[]; dirtyCount: number }): void {
    console.log(`[${context}] Successfully committed ${event.dirtyCount} modified row(s).`);
  }

  onCancelEdits(context: string): void {
    console.log(`[${context}] Edits reverted to baseline.`);
  }
}

export { AppEditableTableSectionComponent as EditableTableSectionComponent };

