import { Component, ChangeDetectionStrategy, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { AppMaterialEditableDataTableComponent } from '../../../../../shared/components/material-editable-data-table/core/app-material-editable-data-table.component';
import {
  MaterialTableColumn,
  MaterialLookupConfig,
  MaterialSelectOption
} from '../../../../../shared/components/material-editable-data-table/models/material-data-table.models';

export interface DemoStockReceiptLine {
  id: string;
  productId: string;
  productCode: string;
  productName: string;
  locationCode: string;
  quantity: number;
  unitOfMeasure: string;
  unitCost: number;
  lineTotal: number;
}

export interface DemoProductItem {
  id: string;
  code: string;
  name: string;
  category: string;
  unitOfMeasure: string;
  standardCost: number;
  description: string;
}

export const DEMO_STOCK_PRODUCTS: DemoProductItem[] = [
  {
    id: 'prod-001',
    code: 'XYZ-P001',
    name: 'XYZ Industrial Product',
    category: 'Industrial',
    unitOfMeasure: 'EA',
    standardCost: 200.0,
    description: 'Heavy-duty industrial sub-assembly'
  },
  {
    id: 'prod-002',
    code: 'CBL-001',
    name: 'Shielded Copper Cable 10m',
    category: 'Electrical',
    unitOfMeasure: 'MTR',
    standardCost: 15.5,
    description: 'Category 6A shielded bulk cable'
  },
  {
    id: 'prod-003',
    code: 'ADP-002',
    name: 'Fast Charging Adapter 65W',
    category: 'Electronics',
    unitOfMeasure: 'PCS',
    standardCost: 32.0,
    description: 'Universal USB-C power delivery adapter'
  },
  {
    id: 'prod-004',
    code: 'WID-003',
    name: 'Precision Steel Widget M8',
    category: 'Hardware',
    unitOfMeasure: 'EA',
    standardCost: 4.75,
    description: 'High-tensile zinc plated fastener'
  },
  {
    id: 'prod-005',
    code: 'SN-9900',
    name: 'Industrial Sensor Array X1',
    category: 'Automation',
    unitOfMeasure: 'UNIT',
    standardCost: 450.0,
    description: 'Multi-channel optical and thermal sensor'
  }
];

export const INITIAL_DEMO_STOCK_RECEIPT_LINES: DemoStockReceiptLine[] = [
  {
    id: 'rec-line-1',
    productId: 'prod-001',
    productCode: 'XYZ-P001',
    productName: 'XYZ Industrial Product',
    locationCode: 'WH-DIST / Zone A / RACK-01',
    quantity: 10,
    unitOfMeasure: 'EA',
    unitCost: 200.0,
    lineTotal: 2000.0
  },
  {
    id: 'rec-line-2',
    productId: 'prod-002',
    productCode: 'CBL-001',
    productName: 'Shielded Copper Cable 10m',
    locationCode: 'WH-DIST / Zone B / RACK-02',
    quantity: 50,
    unitOfMeasure: 'MTR',
    unitCost: 15.5,
    lineTotal: 775.0
  },
  {
    id: 'rec-line-3',
    productId: 'prod-003',
    productCode: 'ADP-002',
    productName: 'Fast Charging Adapter 65W',
    locationCode: 'WH-MAIN / General / RACK-03',
    quantity: 25,
    unitOfMeasure: 'PCS',
    unitCost: 32.0,
    lineTotal: 800.0
  }
];

@Component({
  selector: 'app-material-editable-table-section',
  standalone: true,
  imports: [CommonModule, MatButtonModule, MatIconModule, AppMaterialEditableDataTableComponent],
  template: `
    <div class="material-showcase-section">
      <!-- Section Intro Card -->
      <div class="intro-card">
        <div class="intro-badge">Angular Material + Fluent Theme POC</div>
        <h2 class="section-title">Dynamics 365-Inspired Editable Transaction Grid</h2>
        <p class="section-desc">
          High-performance multi-row ERP transaction entry for <strong>Stock Receipts</strong>.
          Demonstrates uninterrupted Tab-driven keyboard workflows, searchable product lookup with distinct
          highlight states, adaptive popup positioning, and in-memory persistence.
        </p>

        <!-- Feature Tags -->
        <div class="feature-pills">
          <span class="pill">✓ Zero PrimeNG Components</span>
          <span class="pill">✓ D365 Keyboard & Focus Contract</span>
          <span class="pill">✓ ArrowUp/Down + Enter Commit</span>
          <span class="pill">✓ 2-Stage Escape Cancellation</span>
          <span class="pill">✓ Validation Navigation Guards</span>
          <span class="pill">✓ In-Memory Demo Storage</span>
        </div>
      </div>

      <!-- Live Transaction Table Card -->
      <div class="grid-card">
        <div class="grid-card-header">
          <div class="header-titles">
            <h3>Direct Stock Receipt Lines (Proof of Concept)</h3>
            <span class="subtext">
              Acceptance sequence: <code>Product → Quantity → Unit Cost → [Tab] Next Row</code>
            </span>
          </div>
          <div class="header-badges">
            <span class="in-memory-tag">In-Memory Demo State</span>
          </div>
        </div>

        <app-material-editable-data-table
          [data]="receiptLines()"
          [columns]="columns"
          rowKey="id"
          [allowAddRow]="true"
          [allowDeleteRow]="true"
          [showToolbar]="true"
          [showRowNumbers]="true"
          [showFooter]="true"
          [defaultNewRow]="createNewReceiptLine"
          [onRowCalculate]="calculateReceiptLine"
          (save)="onSaveTransaction($event)"
          (cancelEdits)="onCancelEdits()"
          (dataChange)="onDataChange($event)"
        ></app-material-editable-data-table>
      </div>

      <!-- In-Memory Saved State Inspector -->
      @if (lastSavedPayload(); as payload) {
        <div class="saved-payload-card">
          <div class="payload-header">
            <h4>💾 In-Memory Saved Payload ({{ payload.dirtyCount }} line items updated)</h4>
            <span class="timestamp">{{ payload.timestamp }}</span>
          </div>
          <pre class="json-dump">{{ payload.json }}</pre>
        </div>
      }
    </div>
  `,
  styles: [`
    .material-showcase-section {
      display: flex;
      flex-direction: column;
      gap: 20px;
      font-family: var(--fluent-font-family, 'Segoe UI', sans-serif);
    }
    .intro-card {
      background: #ffffff;
      border: 1px solid #edebe9;
      border-radius: 4px;
      padding: 20px 24px;
      box-shadow: 0 1.6px 3.6px 0 rgba(0, 0, 0, 0.13), 0 0.3px 0.9px 0 rgba(0, 0, 0, 0.11);
      position: relative;
    }
    .intro-badge {
      display: inline-block;
      background: #eff6fc;
      color: #0078d4;
      font-size: 11px;
      font-weight: 600;
      padding: 3px 8px;
      border-radius: 3px;
      margin-bottom: 8px;
      text-transform: uppercase;
      letter-spacing: 0.5px;
    }
    .section-title {
      font-size: 20px;
      font-weight: 600;
      color: #201f1e;
      margin: 0 0 8px 0;
    }
    .section-desc {
      font-size: 13px;
      line-height: 1.5;
      color: #605e5c;
      margin: 0 0 14px 0;
      max-width: 900px;
    }
    .feature-pills {
      display: flex;
      flex-wrap: wrap;
      gap: 8px;
    }
    .pill {
      background: #f3f2f1;
      color: #323130;
      font-size: 11px;
      font-weight: 500;
      padding: 3px 10px;
      border-radius: 12px;
      border: 1px solid #e1dfdd;
    }
    .grid-card {
      background: #ffffff;
      border: 1px solid #edebe9;
      border-radius: 4px;
      padding: 16px 20px 24px;
      box-shadow: 0 1.6px 3.6px 0 rgba(0, 0, 0, 0.13), 0 0.3px 0.9px 0 rgba(0, 0, 0, 0.11);
    }
    .grid-card-header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      margin-bottom: 14px;
      h3 {
        font-size: 15px;
        font-weight: 600;
        color: #201f1e;
        margin: 0 0 4px 0;
      }
      .subtext {
        font-size: 12px;
        color: #605e5c;
        code {
          background: #f3f2f1;
          padding: 2px 6px;
          border-radius: 3px;
          color: #0078d4;
          font-weight: 600;
        }
      }
    }
    .in-memory-tag {
      background: #fff4ce;
      color: #797673;
      border: 1px solid #fde386;
      font-size: 11px;
      font-weight: 600;
      padding: 3px 8px;
      border-radius: 3px;
    }
    .saved-payload-card {
      background: #ffffff;
      border: 1px solid #107c41;
      border-radius: 4px;
      padding: 16px;
      .payload-header {
        display: flex;
        align-items: center;
        justify-content: space-between;
        margin-bottom: 8px;
        h4 {
          margin: 0;
          color: #107c41;
          font-size: 14px;
        }
        .timestamp {
          font-size: 11px;
          color: #605e5c;
        }
      }
      .json-dump {
        background: #f3f2f1;
        padding: 12px;
        border-radius: 4px;
        font-size: 12px;
        font-family: Consolas, monospace;
        margin: 0;
        max-height: 220px;
        overflow-y: auto;
      }
    }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class MaterialEditableTableSectionComponent {
  readonly receiptLines = signal<DemoStockReceiptLine[]>(INITIAL_DEMO_STOCK_RECEIPT_LINES);
  readonly lastSavedPayload = signal<{ json: string; dirtyCount: number; timestamp: string } | null>(null);

  // Storage Location Options
  readonly locationOptions: MaterialSelectOption[] = [
    { label: 'WH-DIST / Zone A / RACK-01', value: 'WH-DIST / Zone A / RACK-01', subtitle: 'Fast Moving Items' },
    { label: 'WH-DIST / Zone B / RACK-02', value: 'WH-DIST / Zone B / RACK-02', subtitle: 'Bulk Storage' },
    { label: 'WH-MAIN / General / RACK-03', value: 'WH-MAIN / General / RACK-03', subtitle: 'Ambient Storage' },
    { label: 'WH-RES / Inspection / BIN-01', value: 'WH-RES / Inspection / BIN-01', subtitle: 'Quality Quarantine' }
  ];

  // Product Lookup Configuration
  readonly productLookupConfig: MaterialLookupConfig<DemoProductItem> = {
    title: 'Select Stock Receipt Product',
    searchPlaceholder: 'Search by SKU code, name, category...',
    displayField: 'code',
    valueField: 'code',
    items: DEMO_STOCK_PRODUCTS,
    columns: [
      { field: 'code', header: 'Product Code', width: '130px' },
      { field: 'name', header: 'Product Name', width: '220px' },
      { field: 'category', header: 'Category', width: '110px' },
      { field: 'unitOfMeasure', header: 'UOM', width: '80px' },
      { field: 'standardCost', header: 'Std Cost', width: '100px' }
    ],
    onSelect: (product: DemoProductItem, row: DemoStockReceiptLine) => {
      row.productId = product.id;
      row.productCode = product.code;
      row.productName = product.name;
      row.unitOfMeasure = product.unitOfMeasure;
      // Default standard cost only if not yet set or zero
      if (!row.unitCost || row.unitCost === 0) {
        row.unitCost = product.standardCost;
      }
      this.calculateReceiptLine(row);
    }
  };

  // Column Definitions
  readonly columns: MaterialTableColumn<DemoStockReceiptLine>[] = [
    {
      field: 'productCode',
      header: 'Product / SKU',
      type: 'lookup',
      required: true,
      lookupConfig: this.productLookupConfig,
      width: '260px',
      placeholder: 'Search product SKU...',
      format: (_val, row) => (row.productCode && row.productName ? `${row.productCode} — ${row.productName}` : row.productCode || '')
    },
    {
      field: 'locationCode',
      header: 'Destination Location',
      type: 'select',
      required: true,
      options: this.locationOptions,
      width: '240px',
      placeholder: 'Select storage location...'
    },
    {
      field: 'quantity',
      header: 'Receipt Qty',
      type: 'number',
      required: true,
      min: 1,
      width: '120px',
      align: 'right',
      placeholder: '1'
    },
    {
      field: 'unitOfMeasure',
      header: 'UOM',
      type: 'text',
      editable: false,
      width: '80px',
      align: 'center'
    },
    {
      field: 'unitCost',
      header: 'Unit Cost ($)',
      type: 'currency',
      required: true,
      min: 0,
      width: '130px',
      align: 'right',
      placeholder: '0.00'
    },
    {
      field: 'lineTotal',
      header: 'Total Valuation ($)',
      type: 'currency',
      editable: false,
      width: '140px',
      align: 'right'
    },
    {
      field: 'actions',
      header: '',
      type: 'actions',
      width: '50px',
      align: 'center'
    }
  ];

  readonly createNewReceiptLine = (): DemoStockReceiptLine => {
    return {
      id: `rec-line-${Date.now()}-${Math.random().toString(36).substring(2, 6)}`,
      productId: '',
      productCode: '',
      productName: '',
      locationCode: 'WH-DIST / Zone A / RACK-01',
      quantity: 1,
      unitOfMeasure: 'EA',
      unitCost: 0,
      lineTotal: 0
    };
  };

  readonly calculateReceiptLine = (row: DemoStockReceiptLine): void => {
    const qty = Number(row.quantity) || 0;
    const cost = Number(row.unitCost) || 0;
    row.lineTotal = Math.round(qty * cost * 100) / 100;
  };

  onDataChange(updated: DemoStockReceiptLine[]): void {
    this.receiptLines.set(updated);
  }

  onSaveTransaction(event: { updatedData: DemoStockReceiptLine[]; dirtyCount: number }): void {
    this.receiptLines.set([...event.updatedData]);
    this.lastSavedPayload.set({
      json: JSON.stringify(event.updatedData, null, 2),
      dirtyCount: event.dirtyCount,
      timestamp: new Date().toLocaleTimeString()
    });
  }

  onCancelEdits(): void {
    this.lastSavedPayload.set(null);
  }
}
