import { Component, signal, computed, inject, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';

import { AppPageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { AppDataTableComponent } from '../../../../shared/components/data-table/data-table.component';
import { AuthService } from '../../../../core/auth/auth.service';
import { TenantContextService } from '../../../../core/services/tenant-context.service';
import { DataTableColumn, DataTableAction } from '../../../../shared/models/data-table-config.model';

export interface CustomerDirectOrderSummary {
  id: string;
  directOrderRef: string;
  orderDate: string;
  contractCardCode: string;
  itemsCount: number;
  totalAmount: string;
  status: 'active' | 'warning' | 'info' | 'success';
  statusLabel: string;
}

@Component({
  selector: 'app-customer-direct-orders',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterModule,
    AppPageHeaderComponent,
    AppDataTableComponent
  ],
  template: `
    <div class="customer-direct-orders-page">
      <app-page-header
        title="Direct Contract Orders"
        subtitle="Authorised commercial orders placed directly against active Sales Price Cards."
        [breadcrumbs]="breadcrumbs"
      >
        <div actions class="page-header-actions">
          <a routerLink="/customer-portal/catalog" class="erp-button erp-button-primary">
            <i class="pi pi-shopping-bag"></i>
            <span>Browse Products</span>
          </a>
        </div>
      </app-page-header>

      <!-- Main Content Area: Standardized Data Table Layout -->
      <div class="direct-orders-table-card erp-card">
        <div class="table-controls-bar">
          <div class="search-and-filters">
            <!-- Quick Search -->
            <div class="search-input-box">
              <i class="pi pi-search search-icon"></i>
              <input
                type="text"
                class="p-inputtext p-inputtext-sm search-field"
                placeholder="Search direct order ref, price card..."
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
                All Orders ({{ directOrders().length }})
              </button>
              <button
                type="button"
                class="filter-pill"
                [class.active]="activeStatusFilter() === 'In Fulfilment'"
                (click)="activeStatusFilter.set('In Fulfilment')"
              >
                <i class="pi pi-cog pill-icon"></i> In Fulfilment
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

        <!-- Main Direct Orders Data Table -->
        <app-data-table
          [data]="filteredDirectOrders()"
          [columns]="tableColumns"
          [rowActions]="tableActions"
          rowKey="id"
          emptyTitle="No direct contract orders found"
          emptyDescription="No direct orders match your current search or filter."
          [showGlobalSearch]="false"
          [showColumnChooser]="true"
          [showExport]="true"
          [showViewSwitcher]="true"
          cardTitleField="directOrderRef"
          cardSubtitleField="contractCardCode"
          cardIcon="pi pi-send"
          [paginator]="true"
          [rows]="10"
          [rowsPerPageOptions]="[5, 10, 25, 50]"
        ></app-data-table>
      </div>
    </div>
  `,
  styles: [`
    .customer-direct-orders-page {
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
export class CustomerDirectOrdersComponent {
  private readonly authService = inject(AuthService);
  private readonly tenantContext = inject(TenantContextService);

  readonly searchTerm = signal<string>('');
  readonly activeStatusFilter = signal<string>('all');

  readonly breadcrumbs = [
    { label: 'Customer Portal', routerLink: '/customer-portal' },
    { label: 'Purchasing', routerLink: '/customer-portal/direct-orders' },
    { label: 'Direct Orders' }
  ];

  readonly companyAccountLabel = computed(() => {
    const tenant = this.tenantContext.currentTenant()?.name || 'Alpha Manufacturing Ltd';
    const user = this.authService.currentUser()?.username || this.authService.currentUser()?.email || 'Corporate Account';
    return `${tenant} / ${user}`;
  });

  readonly directOrders = signal<CustomerDirectOrderSummary[]>([
    {
      id: 'DIR-0012',
      directOrderRef: 'DIR-2026-0012',
      orderDate: '08-Nov-2026',
      contractCardCode: 'PC-CORP-2026-A',
      itemsCount: 5,
      totalAmount: '$4,100.00',
      status: 'active',
      statusLabel: 'In Fulfilment'
    },
    {
      id: 'DIR-0008',
      directOrderRef: 'DIR-2026-0008',
      orderDate: '19-Oct-2026',
      contractCardCode: 'PC-CORP-2026-A',
      itemsCount: 8,
      totalAmount: '$12,450.00',
      status: 'success',
      statusLabel: 'Delivered'
    }
  ]);

  readonly filteredDirectOrders = computed<CustomerDirectOrderSummary[]>(() => {
    let list = this.directOrders();
    const filter = this.activeStatusFilter();
    if (filter !== 'all') {
      list = list.filter(o => o.statusLabel === filter);
    }

    const term = this.searchTerm().trim().toLowerCase();
    if (term) {
      list = list.filter(
        o =>
          o.directOrderRef.toLowerCase().includes(term) ||
          o.contractCardCode.toLowerCase().includes(term) ||
          o.statusLabel.toLowerCase().includes(term)
      );
    }
    return list;
  });

  readonly tableColumns: DataTableColumn<CustomerDirectOrderSummary>[] = [
    {
      field: 'directOrderRef',
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
      field: 'contractCardCode',
      header: 'Price Card',
      sortable: true,
      width: '160px'
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
      header: 'Gross Total',
      sortable: true,
      align: 'right',
      width: '140px'
    },
    {
      field: 'status',
      header: 'Status',
      sortable: true,
      width: '160px',
      type: 'badge',
      badgeSeverityMap: (val: any) =>
        val === 'active' ? 'info' : val === 'success' ? 'success' : val === 'warning' ? 'warning' : 'neutral',
      format: (val: any, row?: CustomerDirectOrderSummary) => row?.statusLabel || String(val)
    },
    {
      field: 'id',
      header: 'Actions',
      width: '140px',
      type: 'actions'
    }
  ];

  readonly tableActions: DataTableAction<CustomerDirectOrderSummary>[] = [
    {
      id: 'view',
      label: 'View Dispatch',
      icon: 'pi pi-truck',
      severity: 'secondary',
      action: (row: CustomerDirectOrderSummary) => this.viewDispatch(row)
    }
  ];

  viewDispatch(row: CustomerDirectOrderSummary): void {
    // Navigate or view dispatch details
  }
}
