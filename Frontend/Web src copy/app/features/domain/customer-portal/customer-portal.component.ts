import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { AuthService } from '../../core/auth/auth.service';

interface CustomerOrder {
  orderNumber: string;
  orderDate: string;
  itemsCount: number;
  totalAmount: string;
  status: 'active' | 'warning' | 'info' | 'success';
  statusLabel: string;
}

@Component({
  selector: 'app-customer-portal',
  standalone: true,
  imports: [CommonModule, RouterLink, ButtonModule, PageHeaderComponent, StatusBadgeComponent],
  template: `
    <div class="customer-portal-page">
      <header class="portal-header erp-card">
        <div class="header-left">
          <div class="portal-badge">
            <i class="pi pi-user"></i>
          </div>
          <div>
            <h2>Customer Private Portal</h2>
            <p>Track your manufacturing purchase orders, invoices, and shipment deliveries.</p>
          </div>
        </div>
        <div class="header-right">
          <span class="user-meta">Signed In: <strong>{{ authService.currentUser()?.email }}</strong></span>
          <button type="button" class="p-button p-button-sm p-button-outlined" (click)="authService.logout()">
            Sign Out
          </button>
        </div>
      </header>

      <div class="portal-grid">
        <!-- Recent Orders Card -->
        <div class="portal-card erp-card">
          <div class="card-head">
            <i class="pi pi-receipt"></i>
            <h3>Your Recent Purchase Orders</h3>
          </div>
          <p>Orders submitted to Alpha Manufacturing Ltd.</p>

          <div class="orders-list">
            @for (ord of recentOrders(); track ord.orderNumber) {
              <div class="order-item">
                <div class="order-main">
                  <strong class="order-no">{{ ord.orderNumber }}</strong>
                  <span class="order-date">{{ ord.orderDate }} &bull; {{ ord.itemsCount }} items</span>
                </div>
                <div class="order-amount">{{ ord.totalAmount }}</div>
                <app-status-badge [status]="ord.status" [label]="ord.statusLabel"></app-status-badge>
              </div>
            }
          </div>
        </div>

        <!-- Quick Inquiries & Actions -->
        <div class="portal-card erp-card">
          <div class="card-head">
            <i class="pi pi-compass"></i>
            <h3>Customer Actions</h3>
          </div>
          <div class="customer-actions">
            <a routerLink="/storefront" class="portal-btn">
              <i class="pi pi-shopping-bag"></i>
              <div>
                <strong>Browse Storefront Catalog</strong>
                <span>View latest stock and price sheets</span>
              </div>
            </a>
            <a routerLink="/" class="portal-btn">
              <i class="pi pi-home"></i>
              <div>
                <strong>Platform Home</strong>
                <span>Return to ApexERP public overview</span>
              </div>
            </a>
          </div>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .customer-portal-page {
      padding: var(--erp-space-5);
      display: flex;
      flex-direction: column;
      gap: var(--erp-space-5);
      background-color: var(--erp-bg-app);
      min-height: 100vh;
    }
    .portal-header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      flex-wrap: wrap;
      gap: var(--erp-space-3);
    }
    .header-left {
      display: flex;
      align-items: center;
      gap: var(--erp-space-3);
    }
    .portal-badge {
      width: 44px;
      height: 44px;
      border-radius: var(--erp-radius-md);
      background-color: var(--erp-primary-light);
      color: var(--erp-primary);
      display: flex;
      align-items: center;
      justify-content: center;
      font-size: 20px;
    }
    h2 {
      font-size: 1.4rem;
      margin: 0;
      color: var(--erp-text-main);
    }
    p {
      margin: 0;
      font-size: 13px;
      color: var(--erp-text-secondary);
    }
    .header-right {
      display: flex;
      align-items: center;
      gap: var(--erp-space-3);
      font-size: 13px;
    }
    .portal-grid {
      display: grid;
      grid-template-columns: 1.6fr 1fr;
      gap: var(--erp-space-4);
      @media (max-width: 860px) {
        grid-template-columns: 1fr;
      }
    }
    .portal-card {
      .card-head {
        display: flex;
        align-items: center;
        gap: var(--erp-space-2);
        margin-bottom: var(--erp-space-2);
        color: var(--erp-primary);
        h3 {
          margin: 0;
          font-size: 15px;
          color: var(--erp-text-main);
        }
      }
    }
    .orders-list {
      display: flex;
      flex-direction: column;
      gap: var(--erp-space-2);
      margin-top: var(--erp-space-3);
    }
    .order-item {
      display: flex;
      align-items: center;
      justify-content: space-between;
      padding: var(--erp-space-3);
      border: 1px solid var(--erp-border-subtle);
      border-radius: var(--erp-radius-sm);
      background-color: var(--erp-bg-surface);
      .order-main {
        display: flex;
        flex-direction: column;
        .order-no {
          font-size: 13px;
          color: var(--erp-primary);
          font-family: var(--erp-font-mono);
        }
        .order-date {
          font-size: 12px;
          color: var(--erp-text-muted);
        }
      }
      .order-amount {
        font-weight: 700;
        font-size: 14px;
        color: var(--erp-text-main);
      }
    }
    .customer-actions {
      display: flex;
      flex-direction: column;
      gap: var(--erp-space-2);
      margin-top: var(--erp-space-3);
    }
    .portal-btn {
      display: flex;
      align-items: center;
      gap: var(--erp-space-3);
      padding: var(--erp-space-3);
      border: 1px solid var(--erp-border);
      border-radius: var(--erp-radius-sm);
      text-decoration: none;
      transition: all 0.15s ease;
      i {
        font-size: 20px;
        color: var(--erp-primary);
      }
      strong {
        display: block;
        font-size: 13px;
        color: var(--erp-text-main);
      }
      span {
        font-size: 11.5px;
        color: var(--erp-text-muted);
      }
      &:hover {
        border-color: var(--erp-primary);
        background-color: var(--erp-primary-light);
      }
    }
  `]
})
export class CustomerPortalComponent {
  readonly authService = inject(AuthService);

  readonly recentOrders = signal<CustomerOrder[]>([
    {
      orderNumber: 'SO-2026-0891',
      orderDate: 'Oct 12, 2026',
      itemsCount: 4,
      totalAmount: '$6,420.00',
      status: 'active',
      statusLabel: 'In Production'
    },
    {
      orderNumber: 'SO-2026-0744',
      orderDate: 'Sep 28, 2026',
      itemsCount: 12,
      totalAmount: '$18,900.00',
      status: 'success',
      statusLabel: 'Shipped'
    },
    {
      orderNumber: 'SO-2026-0512',
      orderDate: 'Aug 14, 2026',
      itemsCount: 2,
      totalAmount: '$1,250.00',
      status: 'success',
      statusLabel: 'Delivered'
    }
  ]);
}
