import { Component, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { StatusBadgeComponent } from '../../../../shared/components/status-badge/status-badge.component';
import { AuthService } from '../../../../core/auth/auth.service';
import { TenantContextService } from '../../../../core/services/tenant-context.service';

export interface RecentOrderRow {
  orderNumber: string;
  orderDate: string;
  itemsCount: number;
  totalAmount: string;
  status: 'active' | 'warning' | 'info' | 'success';
  statusLabel: string;
}

@Component({
  selector: 'app-customer-dashboard',
  standalone: true,
  imports: [CommonModule, RouterLink, StatusBadgeComponent],
  template: `
    <div class="customer-dashboard">
      <!-- Welcome Header -->
      <section class="welcome-banner erp-card">
        <div class="banner-left">
          <div class="user-avatar-badge">
            <i [class]="isOrganisation() ? 'pi pi-building' : 'pi pi-user'"></i>
          </div>
          <div>
            <div class="badge-row">
              <span class="account-badge" [class.b2b]="isOrganisation()">
                {{ isOrganisation() ? 'Corporate B2B Account' : 'Individual Retail Account' }}
              </span>
              <span class="tenant-tag">{{ tenantName() }}</span>
            </div>
            <h1>Welcome, {{ userEmail() }}</h1>
            <p>Access your product catalogues, track active orders, and manage requisitions.</p>
          </div>
        </div>

        <div class="banner-right">
          <a routerLink="/customer-portal/catalog" class="erp-button erp-button-primary">
            <i class="pi pi-shopping-bag"></i>
            <span>Browse Products</span>
          </a>
        </div>
      </section>

      <!-- KPI Summary Cards -->
      <section class="kpi-grid">
        <div class="kpi-card erp-card">
          <div class="kpi-icon blue">
            <i class="pi pi-receipt"></i>
          </div>
          <div class="kpi-info">
            <span class="kpi-label">Active Orders</span>
            <span class="kpi-value">3</span>
            <span class="kpi-sub">Fulfillment in progress</span>
          </div>
        </div>

        <div class="kpi-card erp-card">
          <div class="kpi-icon green">
            <i class="pi pi-check-circle"></i>
          </div>
          <div class="kpi-info">
            <span class="kpi-label">Delivered Orders</span>
            <span class="kpi-value">12</span>
            <span class="kpi-sub">Past 90 days</span>
          </div>
        </div>

        @if (isOrganisation()) {
          <div class="kpi-card erp-card">
            <div class="kpi-icon purple">
              <i class="pi pi-file-edit"></i>
            </div>
            <div class="kpi-info">
              <span class="kpi-label">Open Quotations</span>
              <span class="kpi-value">2</span>
              <span class="kpi-sub">Awaiting commercial review</span>
            </div>
          </div>
        } @else {
          <div class="kpi-card erp-card">
            <div class="kpi-icon purple">
              <i class="pi pi-map-marker"></i>
            </div>
            <div class="kpi-info">
              <span class="kpi-label">Saved Addresses</span>
              <span class="kpi-value">1</span>
              <span class="kpi-sub">Default delivery location</span>
            </div>
          </div>
        }

        <div class="kpi-card erp-card">
          <div class="kpi-icon orange">
            <i class="pi pi-shield"></i>
          </div>
          <div class="kpi-info">
            <span class="kpi-label">Account Status</span>
            <span class="kpi-value status-active">Verified</span>
            <span class="kpi-sub">Ready for ordering</span>
          </div>
        </div>
      </section>

      <!-- Main Dashboard Grid -->
      <div class="dashboard-columns">
        <!-- Recent Orders Section -->
        <section class="orders-section erp-card">
          <div class="section-head">
            <div class="head-title">
              <i class="pi pi-clock"></i>
              <h2>Recent Orders</h2>
            </div>
            <a routerLink="/customer-portal/orders" class="erp-button erp-button-secondary erp-button-sm">
              View All Orders
            </a>
          </div>

          <div class="orders-table-wrapper">
            <table class="erp-table">
              <thead>
                <tr>
                  <th>Order Ref</th>
                  <th>Date</th>
                  <th>Items</th>
                  <th>Amount</th>
                  <th>Status</th>
                  <th>Action</th>
                </tr>
              </thead>
              <tbody>
                @for (ord of recentOrders(); track ord.orderNumber) {
                  <tr>
                    <td class="font-mono text-primary font-bold">{{ ord.orderNumber }}</td>
                    <td>{{ ord.orderDate }}</td>
                    <td>{{ ord.itemsCount }} items</td>
                    <td class="font-bold">{{ ord.totalAmount }}</td>
                    <td>
                      <app-status-badge [status]="ord.status" [label]="ord.statusLabel"></app-status-badge>
                    </td>
                    <td>
                      <a routerLink="/customer-portal/orders" class="erp-button erp-button-secondary erp-button-sm">
                        Details
                      </a>
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        </section>

        <!-- Quick Access & Highlights -->
        <section class="actions-section erp-card">
          <div class="section-head">
            <div class="head-title">
              <i class="pi pi-bolt"></i>
              <h2>Quick Actions</h2>
            </div>
          </div>

          <div class="quick-links-list">
            <a routerLink="/customer-portal/catalog" class="quick-link-item">
              <div class="link-icon">
                <i class="pi pi-shopping-bag"></i>
              </div>
              <div class="link-text">
                <strong>Explore Product Catalogue</strong>
                <span>Browse live stock, prices, and specifications</span>
              </div>
              <i class="pi pi-chevron-right chevron"></i>
            </a>

            <a routerLink="/customer-portal/cart" class="quick-link-item">
              <div class="link-icon">
                <i class="pi pi-shopping-cart"></i>
              </div>
              <div class="link-text">
                <strong>View Shopping Cart</strong>
                <span>Review staged items and commercial totals</span>
              </div>
              <i class="pi pi-chevron-right chevron"></i>
            </a>

            @if (isOrganisation()) {
              <a routerLink="/customer-portal/quotations" class="quick-link-item">
                <div class="link-icon">
                  <i class="pi pi-file-edit"></i>
                </div>
                <div class="link-text">
                  <strong>Corporate Quotations</strong>
                  <span>Track RFQs and formal sales quotations</span>
                </div>
                <i class="pi pi-chevron-right chevron"></i>
              </a>
            }

            <a routerLink="/customer-portal/addresses" class="quick-link-item">
              <div class="link-icon">
                <i class="pi pi-map-marker"></i>
              </div>
              <div class="link-text">
                <strong>Delivery Addresses</strong>
                <span>Manage shipping destinations and contacts</span>
              </div>
              <i class="pi pi-chevron-right chevron"></i>
            </a>
          </div>
        </section>
      </div>
    </div>
  `,
  styles: [`
    .customer-dashboard {
      display: flex;
      flex-direction: column;
      gap: var(--erp-space-5);
    }

    .welcome-banner {
      display: flex;
      align-items: center;
      justify-content: space-between;
      flex-wrap: wrap;
      gap: var(--erp-space-4);
      padding: var(--erp-space-5);
      border-left: 4px solid var(--erp-primary);

      .banner-left {
        display: flex;
        align-items: center;
        gap: var(--erp-space-4);

        .user-avatar-badge {
          width: 52px;
          height: 52px;
          border-radius: var(--erp-radius-md);
          background-color: var(--erp-primary-light);
          color: var(--erp-primary);
          display: flex;
          align-items: center;
          justify-content: center;
          font-size: 24px;
        }

        .badge-row {
          display: flex;
          align-items: center;
          gap: var(--erp-space-2);
          margin-bottom: 4px;

          .account-badge {
            font-size: 0.72rem;
            font-weight: 700;
            text-transform: uppercase;
            padding: 2px 8px;
            border-radius: 12px;
            background-color: var(--erp-primary-light);
            color: var(--erp-primary);

            &.b2b {
              background-color: rgba(139, 92, 246, 0.15);
              color: #7c3aed;
            }
          }

          .tenant-tag {
            font-size: 0.78rem;
            color: var(--erp-text-secondary);
          }
        }

        h1 {
          font-size: 1.35rem;
          font-weight: 700;
          margin: 0 0 2px 0;
          color: var(--erp-text-main);
        }

        p {
          font-size: 0.88rem;
          color: var(--erp-text-secondary);
          margin: 0;
        }
      }
    }

    .kpi-grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
      gap: var(--erp-space-4);

      .kpi-card {
        display: flex;
        align-items: center;
        gap: var(--erp-space-3);
        padding: var(--erp-space-4);

        .kpi-icon {
          width: 48px;
          height: 48px;
          border-radius: var(--erp-radius-md);
          display: flex;
          align-items: center;
          justify-content: center;
          font-size: 20px;

          &.blue {
            background-color: rgba(0, 120, 212, 0.12);
            color: #0078d4;
          }
          &.green {
            background-color: rgba(16, 185, 129, 0.12);
            color: #10b981;
          }
          &.purple {
            background-color: rgba(139, 92, 246, 0.12);
            color: #8b5cf6;
          }
          &.orange {
            background-color: rgba(245, 158, 11, 0.12);
            color: #f59e0b;
          }
        }

        .kpi-info {
          display: flex;
          flex-direction: column;

          .kpi-label {
            font-size: 0.78rem;
            color: var(--erp-text-secondary);
            font-weight: 500;
          }

          .kpi-value {
            font-size: 1.4rem;
            font-weight: 700;
            color: var(--erp-text-main);
            line-height: 1.2;

            &.status-active {
              color: #10b981;
              font-size: 1.1rem;
            }
          }

          .kpi-sub {
            font-size: 0.72rem;
            color: var(--erp-text-muted);
          }
        }
      }
    }

    .dashboard-columns {
      display: grid;
      grid-template-columns: 1.7fr 1fr;
      gap: var(--erp-space-4);

      @media (max-width: 960px) {
        grid-template-columns: 1fr;
      }
    }

    .section-head {
      display: flex;
      align-items: center;
      justify-content: space-between;
      margin-bottom: var(--erp-space-4);
      padding-bottom: var(--erp-space-3);
      border-bottom: 1px solid var(--erp-border-subtle);

      .head-title {
        display: flex;
        align-items: center;
        gap: var(--erp-space-2);
        color: var(--erp-primary);

        h2 {
          font-size: 1.05rem;
          font-weight: 600;
          margin: 0;
          color: var(--erp-text-main);
        }
      }
    }

    .orders-table-wrapper {
      overflow-x: auto;
    }

    .quick-links-list {
      display: flex;
      flex-direction: column;
      gap: var(--erp-space-2);

      .quick-link-item {
        display: flex;
        align-items: center;
        gap: var(--erp-space-3);
        padding: var(--erp-space-3);
        border: 1px solid var(--erp-border);
        border-radius: var(--erp-radius-md);
        text-decoration: none;
        color: inherit;
        transition: all 0.15s ease;

        .link-icon {
          width: 38px;
          height: 38px;
          border-radius: var(--erp-radius-sm);
          background-color: var(--erp-bg-subtle);
          color: var(--erp-primary);
          display: flex;
          align-items: center;
          justify-content: center;
          font-size: 1.1rem;
        }

        .link-text {
          flex: 1;
          display: flex;
          flex-direction: column;

          strong {
            font-size: 0.88rem;
            color: var(--erp-text-main);
          }

          span {
            font-size: 0.75rem;
            color: var(--erp-text-secondary);
          }
        }

        .chevron {
          color: var(--erp-text-muted);
          font-size: 0.8rem;
        }

        &:hover {
          border-color: var(--erp-primary);
          background-color: var(--erp-bg-subtle);

          .chevron {
            color: var(--erp-primary);
          }
        }
      }
    }
  `]
})
export class CustomerDashboardComponent {
  readonly authService = inject(AuthService);
  readonly tenantContext = inject(TenantContextService);

  readonly userEmail = computed(() => {
    return this.authService.currentUser()?.email || 'Customer';
  });

  readonly isOrganisation = computed(() => {
    return this.authService.isCustomerCompany();
  });

  readonly tenantName = computed(() => {
    return this.tenantContext.currentTenant()?.name || this.tenantContext.subdomain() || 'Alpha Manufacturing Ltd';
  });

  readonly recentOrders = signal<RecentOrderRow[]>([
    {
      orderNumber: 'SO-2026-0891',
      orderDate: '12-Oct-2026',
      itemsCount: 4,
      totalAmount: '$6,420.00',
      status: 'active',
      statusLabel: 'In Production'
    },
    {
      orderNumber: 'SO-2026-0744',
      orderDate: '28-Sep-2026',
      itemsCount: 12,
      totalAmount: '$18,900.00',
      status: 'success',
      statusLabel: 'Shipped'
    },
    {
      orderNumber: 'SO-2026-0512',
      orderDate: '14-Aug-2026',
      itemsCount: 2,
      totalAmount: '$1,250.00',
      status: 'success',
      statusLabel: 'Delivered'
    }
  ]);
}
