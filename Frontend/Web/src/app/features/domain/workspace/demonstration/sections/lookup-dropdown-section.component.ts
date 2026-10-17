import { Component, signal, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { AppLookupDropdownComponent } from '../../../../../shared/components/lookup-dropdown/lookup-dropdown.component';
import { LookupItem } from '../../../../../shared/components/lookup-dropdown/lookup-item.model';
import { AppCheckboxComponent } from '../../../../../shared/components/checkbox/checkbox.component';

@Component({
  selector: 'app-lookup-dropdown-section',
  standalone: true,
  imports: [CommonModule, FormsModule, ButtonModule, AppLookupDropdownComponent, AppCheckboxComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="lookup-section-container">
      <div class="section-intro erp-card">
        <h3>Section D: Reusable Lookup Dropdown Component</h3>
        <p class="text-sm text-secondary">
          One configurable searchable dropdown, reusable for products, suppliers, warehouses, and master data.
          Supports debounced search, category filtering, typed eligibility criteria, keyboard navigation, and seamless cell editor integration.
        </p>
      </div>

      <div class="lookup-demo-grid">
        <!-- Configuration & Controls Panel -->
        <div class="demo-card erp-card">
          <div class="card-header">
            <h4>1. Lookup Filter Controls</h4>
            <span class="badge-tag">Interactive Controls</span>
          </div>

          <div class="filter-controls-group">
            <div class="control-row">
              <label class="control-label">Category Filter:</label>
              <div class="category-pills">
                @for (cat of availableCategories; track cat) {
                  <button
                    type="button"
                    class="cat-pill-btn"
                    [class.active]="selectedCategory() === cat"
                    (click)="selectedCategory.set(cat)"
                  >
                    {{ cat }}
                  </button>
                }
              </div>
            </div>

            <div class="control-row">
              <label class="control-label">Eligibility Filter:</label>
              <app-checkbox
                [checked]="onlyEligible()"
                (checkedChange)="onlyEligible.set($event)"
                label="Show only eligible / active items"
                size="small"
              ></app-checkbox>
            </div>
          </div>

          <div class="dropdown-mount-box">
            <label class="control-label">Product Lookup Dropdown:</label>
            <app-lookup-dropdown
              [items]="sampleCatalog"
              [categoryFilter]="selectedCategory() === 'ALL' ? null : selectedCategory()"
              [onlyEligible]="onlyEligible()"
              [ngModel]="selectedProductId()"
              (ngModelChange)="selectedProductId.set($event)"
              (selectItem)="onItemSelected($event)"
              placeholder="Search or pick an enterprise product..."
              searchPlaceholder="Type product code or name..."
              size="normal"
            ></app-lookup-dropdown>
          </div>
        </div>

        <!-- Selected Result Card -->
        <div class="demo-card erp-card">
          <div class="card-header">
            <h4>2. Selected Item Details</h4>
            <span class="badge-tag">Model Binding</span>
          </div>

          @if (activeItem(); as item) {
            <div class="item-detail-view">
              <div class="detail-header">
                <span class="code-badge">{{ item.code }}</span>
                <span class="status-pill" [class.eligible]="item.isEligible !== false" [class.ineligible]="item.isEligible === false">
                  {{ item.isEligible !== false ? 'Active & Eligible' : 'Ineligible' }}
                </span>
              </div>

              <h4 class="product-title">{{ item.name }}</h4>
              <p class="product-desc text-sm text-secondary">{{ item.description }}</p>

              <div class="detail-props-grid">
                <div class="prop-item">
                  <span class="prop-label">Internal ID</span>
                  <span class="prop-value font-mono">{{ item.id }}</span>
                </div>
                <div class="prop-item">
                  <span class="prop-label">Category</span>
                  <span class="prop-value">{{ item.category }}</span>
                </div>
                <div class="prop-item">
                  <span class="prop-label">Standard Price</span>
                  <span class="prop-value font-mono">{{ (item.meta?.price || item.meta?.cost) | currency:'USD' }}</span>
                </div>
                <div class="prop-item">
                  <span class="prop-label">Stock Status</span>
                  <span class="prop-value">{{ item.meta?.stock || 'In Stock' }}</span>
                </div>
              </div>

              <div class="detail-actions">
                <p-button
                  label="Clear Selection"
                  icon="pi pi-times"
                  [outlined]="true"
                  severity="secondary"
                  size="small"
                  (onClick)="clearSelection()"
                ></p-button>
              </div>
            </div>
          } @else {
            <div class="empty-selection-placeholder">
              <i class="pi pi-search placeholder-icon"></i>
              <p class="placeholder-title">No Product Selected</p>
              <p class="placeholder-text text-sm text-secondary">
                Select an item in the lookup dropdown on the left to inspect its model data and eligibility properties.
              </p>
            </div>
          }
        </div>
      </div>
    </div>
  `,
  styles: [`
    .lookup-section-container {
      display: flex;
      flex-direction: column;
      gap: 16px;

      .section-intro {
        padding: 16px 20px;
        background: #ffffff;
        border: 1px solid #e2e8f0;
        border-radius: 6px;

        h3 {
          margin: 0 0 6px;
          font-size: 16px;
          font-weight: 700;
          color: #0f172a;
        }

        p {
          margin: 0;
          line-height: 1.5;
        }
      }

      .lookup-demo-grid {
        display: grid;
        grid-template-columns: 1fr 1fr;
        gap: 16px;

        @media (max-width: 900px) {
          grid-template-columns: 1fr;
        }
      }

      .demo-card {
        padding: 20px;
        background: #ffffff;
        border: 1px solid #e2e8f0;
        border-radius: 6px;
        display: flex;
        flex-direction: column;
        gap: 16px;

        .card-header {
          display: flex;
          align-items: center;
          justify-content: space-between;
          border-bottom: 1px solid #f1f5f9;
          padding-bottom: 12px;

          h4 {
            margin: 0;
            font-size: 14px;
            font-weight: 600;
            color: #1e293b;
          }

          .badge-tag {
            font-size: 11px;
            font-weight: 600;
            color: #0f6cbd;
            background: #eff6fc;
            padding: 2px 8px;
            border-radius: 4px;
          }
        }

        .filter-controls-group {
          display: flex;
          flex-direction: column;
          gap: 12px;

          .control-row {
            display: flex;
            flex-direction: column;
            gap: 6px;

            .control-label {
              font-size: 12px;
              font-weight: 600;
              color: #475569;
            }

            .category-pills {
              display: flex;
              gap: 6px;
              flex-wrap: wrap;

              .cat-pill-btn {
                background: #f8fafc;
                border: 1px solid #cbd5e1;
                color: #475569;
                font-size: 11.5px;
                padding: 4px 10px;
                border-radius: 4px;
                cursor: pointer;
                transition: all 0.15s ease;

                &:hover {
                  background: #f1f5f9;
                  color: #0f172a;
                }

                &.active {
                  background: #0f6cbd;
                  border-color: #0f6cbd;
                  color: #ffffff;
                  font-weight: 600;
                }
              }
            }
          }
        }

        .dropdown-mount-box {
          display: flex;
          flex-direction: column;
          gap: 6px;

          .control-label {
            font-size: 12px;
            font-weight: 600;
            color: #475569;
          }
        }

        .item-detail-view {
          display: flex;
          flex-direction: column;
          gap: 12px;

          .detail-header {
            display: flex;
            align-items: center;
            justify-content: space-between;

            .code-badge {
              font-family: monospace;
              font-size: 13px;
              font-weight: 700;
              color: #0f6cbd;
              background: #eff6fc;
              padding: 3px 8px;
              border-radius: 4px;
            }

            .status-pill {
              font-size: 11px;
              font-weight: 600;
              padding: 2px 8px;
              border-radius: 12px;

              &.eligible {
                background: #dff6dd;
                color: #107c41;
              }

              &.ineligible {
                background: #fde7e9;
                color: #c42b1c;
              }
            }
          }

          .product-title {
            margin: 0;
            font-size: 16px;
            font-weight: 600;
            color: #0f172a;
          }

          .product-desc {
            margin: 0;
          }

          .detail-props-grid {
            display: grid;
            grid-template-columns: 1fr 1fr;
            gap: 10px;
            background: #f8fafc;
            padding: 12px;
            border-radius: 6px;
            border: 1px solid #e2e8f0;

            .prop-item {
              display: flex;
              flex-direction: column;
              gap: 2px;

              .prop-label {
                font-size: 11px;
                color: #64748b;
                text-transform: uppercase;
                letter-spacing: 0.5px;
              }

              .prop-value {
                font-size: 13px;
                font-weight: 600;
                color: #1e293b;
              }
            }
          }

          .detail-actions {
            margin-top: 8px;
          }
        }

        .empty-selection-placeholder {
          display: flex;
          flex-direction: column;
          align-items: center;
          justify-content: center;
          text-align: center;
          padding: 32px 16px;
          border: 2px dashed #e2e8f0;
          border-radius: 6px;

          .placeholder-icon {
            font-size: 28px;
            color: #94a3b8;
            margin-bottom: 8px;
          }

          .placeholder-title {
            font-weight: 600;
            color: #475569;
            margin: 0 0 4px;
          }

          .placeholder-text {
            margin: 0;
            max-width: 320px;
          }
        }
      }
    }
  `]
})
export class AppLookupDropdownSectionComponent {
  readonly availableCategories = ['ALL', 'Raw Materials', 'Hardware', 'Finished Goods', 'Electrical'];
  readonly selectedCategory = signal<string>('ALL');
  readonly onlyEligible = signal<boolean>(false);
  readonly selectedProductId = signal<string | null>('RAW-001');
  readonly activeItem = signal<LookupItem | null>(null);

  readonly sampleCatalog: LookupItem[] = [
    {
      id: 'RAW-001',
      code: 'RAW-001',
      name: 'Cold Rolled Steel Sheet 2mm',
      category: 'Raw Materials',
      description: 'Standard Grade 1018 carbon steel sheet, suitable for precision pressing and structural enclosures.',
      isEligible: true,
      meta: { cost: 12.50, stock: '4,500 units' }
    },
    {
      id: 'RAW-002',
      code: 'RAW-002',
      name: 'Extruded Aluminium Rod 20mm',
      category: 'Raw Materials',
      description: 'Aircraft-grade 6061-T6 cylindrical bar for CNC lathe machining operations.',
      isEligible: true,
      meta: { cost: 8.75, stock: '1,200 units' }
    },
    {
      id: 'RAW-003',
      code: 'RAW-003',
      name: 'Pure Copper Grounding Bar 15mm',
      category: 'Raw Materials',
      description: 'High conductivity electrolytic tough pitch copper for heavy electrical busbars.',
      isEligible: true,
      meta: { cost: 24.00, stock: '320 units' }
    },
    {
      id: 'HDW-004',
      code: 'HDW-004',
      name: 'M10 Hex Head Stainless Bolt',
      category: 'Hardware',
      description: 'Marine-grade A4 stainless steel fastener with zinc passivated coating.',
      isEligible: true,
      meta: { cost: 1.45, stock: '15,000 units' }
    },
    {
      id: 'HDW-005',
      code: 'HDW-005',
      name: 'Obsolete Brass Fastener Batch',
      category: 'Hardware',
      description: 'Discontinued legacy brass bushing with non-standard threading.',
      isEligible: false,
      meta: { cost: 0.90, stock: '0 units (Deprecated)' }
    },
    {
      id: 'ELE-006',
      code: 'ELE-006',
      name: 'Industrial Din-Rail Power Supply 24V',
      category: 'Electrical',
      description: 'Single-phase 120W Din rail mounted switching power supply for automation racks.',
      isEligible: true,
      meta: { price: 65.00, stock: '85 units' }
    },
    {
      id: 'FIN-007',
      code: 'FIN-007',
      name: 'IP66 Weatherproof Control Enclosure',
      category: 'Finished Goods',
      description: 'Fully certified powder-coated NEMA 4X steel cabinet with dual door interlock.',
      isEligible: true,
      meta: { price: 295.00, stock: '18 units' }
    }
  ];

  constructor() {
    this.activeItem.set(this.sampleCatalog[0]);
  }

  onItemSelected(item: LookupItem): void {
    this.activeItem.set(item);
  }

  clearSelection(): void {
    this.selectedProductId.set(null);
    this.activeItem.set(null);
  }
}
