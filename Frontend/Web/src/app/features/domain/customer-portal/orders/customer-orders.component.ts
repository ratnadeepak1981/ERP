import { Component, signal, computed, inject, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';

import { AppPageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { AppDataTableComponent } from '../../../../shared/components/data-table/data-table.component';
import { AuthService } from '../../../../core/auth/auth.service';
import { TenantContextService } from '../../../../core/services/tenant-context.service';
import { DataTableColumn, DataTableAction } from '../../../../shared/models/data-table-config.model';

export interface CustomerOrderSummary {
  id: string;
  orderNumber: string;
  orderDate: string;
  itemsCount: number;
  totalAmount: string;
  status: 'active' | 'warning' | 'info' | 'success';
  statusLabel: string;
  deliveryAddress: string;
}

@Component({
  selector: 'app-customer-orders',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterModule,
    AppPageHeaderComponent,
    AppDataTableComponent
  ],
  template: `
    <div class="customer-orders-page">
      <app-page-header
        title="Order History &amp; Requisitions"
        subtitle="Track order fulfillment, shipment waybills, and historical sales orders."
        [breadcrumbs]="breadcrumbs"
      >
        <div actions class="page-header-actions">
          <a routerLink="/customer-portal/catalog" class="erp-button erp-button-primary">
            <i class="pi pi-plus"></i>
            <span>New Requisition</span>
          </a>
        </div>
      </app-page-header>

      <!-- Main Content Area: Standardized Data Table Layout -->
      <div class="orders-table-card erp-card">
        <div class="table-controls-bar">
          <div class="search-and-filters">
            <!-- Quick Search -->
            <div class="search-input-box">
              <i class="pi pi-search search-icon"></i>
              <input
                type="text"
                class="p-inputtext p-inputtext-sm search-field"
                placeholder="Search order ref, delivery address..."
                [ngModel]="searchTerm()"
                (ngModelChange)="searchTerm.set($event)"
              />
            </div>

            <!-- Status Filter Pills -->
            <div class="filter-pills">
              <button
                type="button"
                class="filter-pill"
                [class.active]="activeStatusFilter() === 'all'"
                (click)="activeStatusFilter.set('all')"
              >
                All Orders ({{ orders().length }})
              </button>
              <button
                type="button"
                class="filter-pill"
                [class.active]="activeStatusFilter() === 'In Production'"
                (click)="activeStatusFilter.set('In Production')"
              >
                <i class="pi pi-cog pill-icon"></i> In Production
              </button>
              <button
                type="button"
                class="filter-pill"
                [class.active]="activeStatusFilter() === 'Shipped'"
                (click)="activeStatusFilter.set('Shipped')"
              >
                <i class="pi pi-truck pill-icon"></i> Shipped
              </button>
              <button
                type="button"
                class="filter-pill"
                [class.active]="activeStatusFilter() === 'Delivered'"
                (click)="activeStatusFilter.set('Delivered')"
              >
                <i class="pi pi-check-circle pill-icon"></i> Delivered
              </button>
            </div>
          </div>

          <!-- Corporate Scope Badge Indicator -->
          <div class="scope-info-pill" title="Current Corporate Scope">
            <i class="pi pi-building"></i>
            <span class="scope-text">{{ companyAccountLabel() }}</span>
          </div>
        </div>

        <!-- Main Orders Data Table -->
        <app-data-table
          [data]="filteredOrders()"
          [columns]="tableColumns"
          [rowActions]="tableActions"
          rowKey="id"
          emptyTitle="No orders found"
          emptyDescription="No customer orders match your current search or filter."
          [showGlobalSearch]="false"
          [showColumnChooser]="true"
          [showExport]="true"
          [showViewSwitcher]="true"
          cardTitleField="orderNumber"
          cardSubtitleField="deliveryAddress"
          cardIcon="pi pi-receipt"
          [paginator]="true"
          [rows]="10"
          [rowsPerPageOptions]="[5, 10, 25, 50]"
        ></app-data-table>
      </div>
    </div>
  `,
  styles: [`
    .customer-orders-page {
      padding: var(--erp-space-4, 16px);
      display: flex;
      flex-direction: column;
      gap: var(--erp-space-4, 16px);
      max-width: 1400px;
      margin: 0 auto;
    }

    .page-header-actions {
      display: flex;
      align-items: center;
      gap: var(--erp-space-2, 8px);
    }

    .erp-card {
      background: var(--erp-bg-surface, #ffffff);
      border: 1px solid var(--erp-border, #e2e8f0);
      border-radius: var(--erp-radius-md, 8px);
      padding: var(--erp-space-4, 16px);
      box-shadow: 0 1px 3px rgba(0, 0, 0, 0.05);
    }

    .table-controls-bar {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: var(--erp-space-3, 12px);
      margin-bottom: var(--erp-space-4, 16px);
      flex-wrap: wrap;

      .search-and-filters {
        display: flex;
        align-items: center;
        gap: var(--erp-space-3, 12px);
        flex-wrap: wrap;
        flex: 1;
      }

      .search-input-box {
        position: relative;
        width: 280px;

        .search-icon {
          position: absolute;
          left: 10px;
          top: 50%;
          transform: translateY(-50%);
          color: var(--erp-text-secondary, #64748b);
          font-size: 13px;
          pointer-events: none;
        }

        .search-field {
          width: 100%;
          padding-left: 32px;
          height: 34px;
          border-radius: var(--erp-radius-sm, 6px);
          font-size: 13px;
        }
      }

      .filter-pills {
        display: flex;
        align-items: center;
        gap: 6px;
        flex-wrap: wrap;

        .filter-pill {
          display: inline-flex;
          align-items: center;
          gap: 6px;
          padding: 5px 12px;
          border-radius: 16px;
          font-size: 12px;
          font-weight: 500;
          cursor: pointer;
          border: 1px solid var(--erp-border, #e2e8f0);
          background: var(--erp-bg-surface, #ffffff);
          color: var(--erp-text-secondary, #64748b);
          transition: all 0.15s ease;

          .pill-icon {
            font-size: 11px;
          }

          &:hover {
            background: var(--erp-bg-hover, #f8fafc);
            color: var(--erp-text-main, #1e293b);
          }

          &.active {
            background: var(--erp-primary-subtle, #eff6ff);
            border-color: var(--erp-primary, #0f6cbd);
            color: var(--erp-primary, #0f6cbd);
            font-weight: 600;
          }
        }
      }

      .scope-info-pill {
        display: inline-flex;
        align-items: center;
        gap: 6px;
        padding: 6px 12px;
        border-radius: var(--erp-radius-md, 6px);
        background: var(--erp-bg-subtle, #f8fafc);
        border: 1px solid var(--erp-border, #e2e8f0);
        font-size: 12px;
        color: var(--erp-text-secondary, #64748b);

        i {
          font-size: 12px;
          color: var(--erp-primary, #0f6cbd);
        }
      }
    }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class CustomerOrdersComponent {
  private readonly authService = inject(AuthService);
  private readonly tenantContext = inject(TenantContextService);

  readonly searchTerm = signal<string>('');
  readonly activeStatusFilter = signal<string>('all');

  readonly breadcrumbs = [
    { label: 'Customer Portal', routerLink: '/customer-portal' },
    { label: 'Purchasing', routerLink: '/customer-portal/orders' },
    { label: 'Order History' }
  ];

  readonly companyAccountLabel = computed(() => {
    const tenant = this.tenantContext.currentTenant()?.name || 'Alpha Manufacturing Ltd';
    const user = this.authService.currentUser()?.username || this.authService.currentUser()?.email || 'Corporate Account';
    return `${tenant} / ${user}`;
  });

  readonly orders = signal<CustomerOrderSummary[]>([
    {
      id: 'SO-0891',
      orderNumber: 'SO-2026-0891',
      orderDate: '12-Oct-2026',
      itemsCount: 4,
      totalAmount: '$6,420.00',
      status: 'active',
      statusLabel: 'In Production',
      deliveryAddress: 'Dock 4, Industrial Zone, Colombo'
    },
    {
      id: 'SO-0744',
      orderNumber: 'SO-2026-0744',
      orderDate: '28-Sep-2026',
      itemsCount: 12,
      totalAmount: '$18,900.00',
      status: 'success',
      statusLabel: 'Shipped',
      deliveryAddress: 'Plant 2 Warehouse, Kandy Rd'
    },
    {
      id: 'SO-0512',
      orderNumber: 'SO-2026-0512',
      orderDate: '14-Aug-2026',
      itemsCount: 2,
      totalAmount: '$1,250.00',
      status: 'success',
      statusLabel: 'Delivered',
      deliveryAddress: 'Dock 4, Industrial Zone, Colombo'
    }
  ]);

  readonly filteredOrders = computed<CustomerOrderSummary[]>(() => {
    let list = this.orders();
    const filter = this.activeStatusFilter();
    if (filter !== 'all') {
      list = list.filter(o => o.statusLabel === filter);
    }

    const term = this.searchTerm().trim().toLowerCase();
    if (term) {
      list = list.filter(
        o =>
          o.orderNumber.toLowerCase().includes(term) ||
          o.deliveryAddress.toLowerCase().includes(term) ||
          o.statusLabel.toLowerCase().includes(term)
      );
    }
    return list;
  });

  readonly tableColumns: DataTableColumn<CustomerOrderSummary>[] = [
    {
      field: 'orderNumber',
      header: 'Order Ref',
      sortable: true,
      width: '160px'
    },
    {
      field: 'orderDate',
      header: 'Order Date',
      sortable: true,
      width: '140px'
    },
    {
      field: 'itemsCount',
      header: 'Items Count',
      sortable: true,
      align: 'right',
      width: '130px',
      format: (val: any) => `${val} items`
    },
    {
      field: 'totalAmount',
      header: 'Total Value',
      sortable: true,
      align: 'right',
      width: '140px'
    },
    {
      field: 'deliveryAddress',
      header: 'Delivery Address',
      sortable: true,
      width: '280px'
    },
    {
      field: 'status',
      header: 'Status',
      sortable: true,
      width: '160px',
      type: 'badge',
      badgeSeverityMap: (val: any) =>
        val === 'active' ? 'info' : val === 'success' ? 'success' : val === 'warning' ? 'warning' : 'neutral',
      format: (val: any, row?: CustomerOrderSummary) => row?.statusLabel || String(val)
    },
    {
      field: 'id',
      header: 'Actions',
      width: '140px',
      type: 'actions'
    }
  ];

  readonly tableActions: DataTableAction<CustomerOrderSummary>[] = [
    {
      id: 'view',
      label: 'View Details',
      icon: 'pi pi-eye',
      severity: 'secondary',
      action: (row: CustomerOrderSummary) => this.viewOrder(row)
    }
  ];

  viewOrder(row: CustomerOrderSummary): void {
    // Navigate or view details
  }
}
