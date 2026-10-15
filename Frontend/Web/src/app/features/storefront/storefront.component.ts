import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';

export interface StoreProduct {
  id: string;
  sku: string;
  name: string;
  category: string;
  unitPrice: number;
  currency: string;
  inStock: boolean;
  leadTime: string;
}

@Component({
  selector: 'app-storefront',
  standalone: true,
  imports: [CommonModule, RouterLink, ButtonModule, PageHeaderComponent, StatusBadgeComponent],
  template: `
    <div class="storefront-page">
      <header class="storefront-header erp-container">
        <div class="brand-bar">
          <div class="brand">
            <i class="pi pi-box"></i>
            <div>
              <h1>Alpha Manufacturing Storefront</h1>
              <span class="sub">Precision Components &amp; Industrial Assembly Parts</span>
            </div>
          </div>
          <div class="store-actions">
            <a routerLink="/login" class="p-button p-button-outlined p-button-sm">
              <i class="pi pi-user"></i> Customer Sign In
            </a>
            <a routerLink="/register" class="p-button p-button-sm">
              Request B2B Account
            </a>
          </div>
        </div>
      </header>

      <main class="storefront-main erp-container">
        <app-page-header
          title="Product Catalog &amp; Materials"
          subtitle="Explore industrial-grade manufactured products, raw materials, and assemblies."
        ></app-page-header>

        <div class="catalog-grid">
          @for (prod of products(); track prod.id) {
            <div class="product-card erp-card">
              <div class="product-badge-row">
                <span class="category-tag">{{ prod.category }}</span>
                <app-status-badge
                  [status]="prod.inStock ? 'active' : 'warning'"
                  [label]="prod.inStock ? 'Available' : 'Made to Order'"
                ></app-status-badge>
              </div>

              <div class="product-info">
                <span class="sku-tag">{{ prod.sku }}</span>
                <h3 class="product-name">{{ prod.name }}</h3>
                <div class="product-lead">Lead Time: <strong>{{ prod.leadTime }}</strong></div>
              </div>

              <div class="product-footer">
                <div class="price-box">
                  <span class="price-val">{{ prod.currency }} \${{ prod.unitPrice.toFixed(2) }}</span>
                  <span class="price-unit">/ unit</span>
                </div>
                <a routerLink="/login" class="p-button p-button-sm p-button-outlined">
                  <i class="pi pi-shopping-cart"></i> Order
                </a>
              </div>
            </div>
          }
        </div>
      </main>
    </div>
  `,
  styles: [`
    .storefront-page {
      background-color: var(--erp-bg-app);
      min-height: 100vh;
      display: flex;
      flex-direction: column;
    }
    .storefront-header {
      padding: var(--erp-space-4) var(--erp-space-4);
      background-color: var(--erp-bg-surface);
      border-bottom: 1px solid var(--erp-border);
      width: 100%;
    }
    .brand-bar {
      display: flex;
      align-items: center;
      justify-content: space-between;
      flex-wrap: wrap;
      gap: var(--erp-space-3);
    }
    .brand {
      display: flex;
      align-items: center;
      gap: var(--erp-space-3);
      i {
        font-size: 28px;
        color: var(--erp-primary);
      }
      h1 {
        font-size: 1.3rem;
        margin: 0;
        color: var(--erp-text-main);
      }
      .sub {
        font-size: 12.5px;
        color: var(--erp-text-muted);
      }
    }
    .store-actions {
      display: flex;
      gap: var(--erp-space-2);
    }
    .storefront-main {
      padding: var(--erp-space-6) var(--erp-space-4);
      display: flex;
      flex-direction: column;
      gap: var(--erp-space-5);
      flex: 1;
      width: 100%;
    }
    .catalog-grid {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
      gap: var(--erp-space-4);
    }
    .product-card {
      display: flex;
      flex-direction: column;
      justify-content: space-between;
      gap: var(--erp-space-3);
      padding: var(--erp-space-4);
      transition: all 0.15s ease;
      &:hover {
        border-color: var(--erp-primary);
        box-shadow: var(--erp-shadow-sm);
      }
    }
    .product-badge-row {
      display: flex;
      justify-content: space-between;
      align-items: center;
      .category-tag {
        font-size: 11px;
        font-weight: 700;
        text-transform: uppercase;
        color: var(--erp-text-muted);
      }
    }
    .product-info {
      .sku-tag {
        font-family: var(--erp-font-mono);
        font-size: 11px;
        color: var(--erp-primary);
        display: block;
        margin-bottom: 2px;
      }
      .product-name {
        font-size: 15px;
        color: var(--erp-text-main);
        margin: 0 0 var(--erp-space-2) 0;
      }
      .product-lead {
        font-size: 12px;
        color: var(--erp-text-secondary);
      }
    }
    .product-footer {
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding-top: var(--erp-space-3);
      border-top: 1px solid var(--erp-border-subtle);
      .price-box {
        .price-val {
          font-size: 18px;
          font-weight: 700;
          color: var(--erp-text-main);
        }
        .price-unit {
          font-size: 12px;
          color: var(--erp-text-muted);
        }
      }
    }
  `]
})
export class StorefrontComponent {
  readonly products = signal<StoreProduct[]>([
    {
      id: 'p1',
      sku: 'PROD-AL-001',
      name: 'High Precision CNC Milling Spindle',
      category: 'Machinery Components',
      unitPrice: 1250.00,
      currency: 'USD',
      inStock: true,
      leadTime: '3-5 Business Days'
    },
    {
      id: 'p2',
      sku: 'PROD-AL-002',
      name: 'Titanium Grade 5 Hex Fasteners (Pack of 100)',
      category: 'Raw Hardware',
      unitPrice: 185.50,
      currency: 'USD',
      inStock: true,
      leadTime: 'Immediate Dispatch'
    },
    {
      id: 'p3',
      sku: 'PROD-AL-003',
      name: 'Hydraulic Pressure Regulator Valve 350 Bar',
      category: 'Hydraulics',
      unitPrice: 620.00,
      currency: 'USD',
      inStock: false,
      leadTime: '10-14 Business Days'
    },
    {
      id: 'p4',
      sku: 'PROD-AL-004',
      name: 'Automated Conveyor Roller Assembly 2m',
      category: 'Assembly Systems',
      unitPrice: 480.00,
      currency: 'USD',
      inStock: true,
      leadTime: '5-7 Business Days'
    }
  ]);
}
