import { Component, OnInit, signal, computed, inject, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';

import { AppPageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { AppDataTableComponent } from '../../../../shared/components/data-table/data-table.component';
import { AppLoadingStateComponent } from '../../../../shared/components/loading-state/loading-state.component';
import { AppErrorStateComponent } from '../../../../shared/components/error-state/error-state.component';
import { AppEmptyStateComponent } from '../../../../shared/components/empty-state/empty-state.component';
import { CustomerQuotationService } from '../../../../core/services/customer-quotation.service';
import { AuthService } from '../../../../core/auth/auth.service';
import { TenantContextService } from '../../../../core/services/tenant-context.service';
import {
  CustomerSalesQuotationDto,
  CustomerSalesQuotationStatus,
  getCustomerQuotationStatusLabel
} from '../../../../core/models/customer-quotation.models';
import { DataTableColumn, DataTableAction } from '../../../../shared/models/data-table-config.model';

@Component({
  selector: 'app-customer-quotations',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterModule,
    AppPageHeaderComponent,
    AppDataTableComponent,
    AppLoadingStateComponent,
    AppErrorStateComponent,
    AppEmptyStateComponent
  ],
  template: `
    <div class="customer-quotations-page">
      <app-page-header
        title="Sales Quotations &amp; Proposals"
        subtitle="Request customized price quotations, review formal commercial proposals, and track RFQ statuses."
        [breadcrumbs]="breadcrumbs"
      >
        <div actions class="page-header-actions">
          <button
            type="button"
            class="erp-button erp-button-secondary"
            [disabled]="loading()"
            (click)="loadQuotations()"
            title="Reload quotations list"
          >
            <i class="pi pi-refresh" [class.pi-spin]="loading()"></i>
            <span>Refresh</span>
          </button>

          <a
            routerLink="/customer-portal/quotations/new"
            class="erp-button erp-button-primary"
            title="Create a new quotation request"
          >
            <i class="pi pi-plus"></i>
            <span>Request Quotation</span>
          </a>
        </div>
      </app-page-header>

      @if (loading()) {
        <div class="erp-card state-card">
          <app-loading-state message="Loading sales quotations from server..."></app-loading-state>
        </div>
      } @else if (error()) {
        <div class="erp-card state-card">
          <app-error-state [message]="error()!" (retry)="loadQuotations()"></app-error-state>
        </div>
      } @else {
        <!-- Main Content Area: Search, Filter Pills & Data Table (Identical to ERP Quotations Reference) -->
        <div class="sq-table-card erp-card">
          <div class="table-controls-bar">
            <div class="search-and-filters">
              <!-- Quick Search -->
              <div class="search-input-box">
                <i class="pi pi-search search-icon"></i>
                <input
                  type="text"
                  class="p-inputtext p-inputtext-sm search-field"
                  placeholder="Search quote #, products or remarks..."
                  [ngModel]="searchTerm()"
                  (ngModelChange)="searchTerm.set($event)"
                />
              </div>

              <!-- Type Category Pills -->
              <div class="filter-pills category-pills">
                <button
                  type="button"
                  class="filter-pill"
                  [class.active]="activeTab() === 'all'"
                  (click)="activeTab.set('all')"
                >
                  All Quotes ({{ allCount() }})
                </button>
                <button
                  type="button"
                  class="filter-pill"
                  [class.active]="activeTab() === 'requests'"
                  (click)="activeTab.set('requests')"
                >
                  <i class="pi pi-inbox pill-icon"></i>
                  <span>Customer Requests ({{ requestsCount() }})</span>
                </button>
                <button
                  type="button"
                  class="filter-pill"
                  [class.active]="activeTab() === 'offers'"
                  (click)="activeTab.set('offers')"
                >
                  <i class="pi pi-file-edit pill-icon"></i>
                  <span>Commercial Offers ({{ offersCount() }})</span>
                </button>
              </div>

              <!-- Status Filter Pills -->
              <div class="filter-pills">
                <button
                  type="button"
                  class="filter-pill"
                  [class.active]="activeStatusFilter() === 'all'"
                  (click)="activeStatusFilter.set('all')"
                >
                  All Status
                </button>
                <button
                  type="button"
                  class="filter-pill"
                  [class.active]="activeStatusFilter() === 'draft'"
                  (click)="activeStatusFilter.set('draft')"
                >
                  <i class="pi pi-file-edit pill-icon"></i> Draft
                </button>
                <button
                  type="button"
                  class="filter-pill"
                  [class.active]="activeStatusFilter() === 'submitted'"
                  (click)="activeStatusFilter.set('submitted')"
                >
                  <i class="pi pi-clock pill-icon"></i> Pending Approval
                </button>
                <button
                  type="button"
                  class="filter-pill"
                  [class.active]="activeStatusFilter() === 'confirmed'"
                  (click)="activeStatusFilter.set('confirmed')"
                >
                  <i class="pi pi-check pill-icon"></i> Approved
                </button>
                <button
                  type="button"
                  class="filter-pill"
                  [class.active]="activeStatusFilter() === 'sent'"
                  (click)="activeStatusFilter.set('sent')"
                >
                  <i class="pi pi-envelope pill-icon"></i> Issued to Customer
                </button>
                <button
                  type="button"
                  class="filter-pill"
                  [class.active]="activeStatusFilter() === 'accepted'"
                  (click)="activeStatusFilter.set('accepted')"
                >
                  <i class="pi pi-check-circle pill-icon"></i> Accepted
                </button>
                <button
                  type="button"
                  class="filter-pill"
                  [class.active]="activeStatusFilter() === 'converted'"
                  (click)="activeStatusFilter.set('converted')"
                >
                  <i class="pi pi-shopping-bag pill-icon"></i> Converted
                </button>
                <button
                  type="button"
                  class="filter-pill"
                  [class.active]="activeStatusFilter() === 'declined'"
                  (click)="activeStatusFilter.set('declined')"
                >
                  <i class="pi pi-times-circle pill-icon"></i> Declined
                </button>
                <button
                  type="button"
                  class="filter-pill"
                  [class.active]="activeStatusFilter() === 'cancelled'"
                  (click)="activeStatusFilter.set('cancelled')"
                >
                  <i class="pi pi-ban pill-icon"></i> Cancelled
                </button>
              </div>
            </div>

            <!-- Corporate Scope Badge Indicator (Matches Second Screen) -->
            <div class="scope-info-pill" title="Current Corporate Scope">
              <i class="pi pi-building"></i>
              <span class="scope-text">{{ companyAccountLabel() }}</span>
            </div>
          </div>

          <!-- Main Sales Quotations Data Table (Matches Second Screen) -->
          <app-data-table
            [data]="filteredQuotations()"
            [columns]="tableColumns"
            [rowActions]="tableActions"
            rowKey="id"
            [loading]="loading()"
            emptyTitle="No sales quotations found"
            emptyDescription="No sales quotations match your current search or status filter."
            [showGlobalSearch]="false"
            [showColumnChooser]="true"
            [showExport]="true"
            [showViewSwitcher]="true"
            cardTitleField="quotationNumber"
            cardSubtitleField="customerRemarks"
            cardIcon="pi pi-file-edit"
            [paginator]="true"
            [rows]="10"
            [rowsPerPageOptions]="[5, 10, 25, 50]"
            (rowClick)="navigateToQuotation($event)"
          ></app-data-table>
        </div>
      }
    </div>
  `,
  styles: [`
    .customer-quotations-page {
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

    .state-card {
      padding: var(--erp-space-8, 32px);
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

        &.category-pills {
          padding-right: 8px;
          border-right: 1px solid var(--erp-border, #e2e8f0);
        }

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
export class CustomerQuotationsComponent implements OnInit {
  private readonly quotationService = inject(CustomerQuotationService);
  readonly authService = inject(AuthService);
  private readonly tenantContext = inject(TenantContextService);
  private readonly router = inject(Router);

  readonly loading = signal<boolean>(true);
  readonly error = signal<string | null>(null);
  readonly quotations = signal<CustomerSalesQuotationDto[]>([]);

  readonly activeTab = signal<'all' | 'requests' | 'offers'>('all');
  readonly activeStatusFilter = signal<string>('all');
  readonly searchTerm = signal<string>('');

  readonly breadcrumbs = [
    { label: 'Customer Portal', routerLink: '/customer-portal' },
    { label: 'Purchasing', routerLink: '/customer-portal/quotations' },
    { label: 'Quotations' }
  ];

  readonly companyAccountLabel = computed(() => {
    const tenant = this.tenantContext.currentTenant()?.name || 'Alpha Manufacturing Ltd';
    const user = this.authService.currentUser()?.username || this.authService.currentUser()?.email || 'Corporate Account';
    return `${tenant} / ${user}`;
  });

  readonly allCount = computed(() => this.quotations().length);
  readonly requestsCount = computed(() => this.quotations().filter(q => Number(q.documentType) !== 2).length);
  readonly offersCount = computed(() => this.quotations().filter(q => Number(q.documentType) === 2).length);

  readonly filteredQuotations = computed<CustomerSalesQuotationDto[]>(() => {
    let list = this.quotations();

    // Type category filter
    if (this.activeTab() === 'requests') {
      list = list.filter(q => Number(q.documentType) !== 2);
    } else if (this.activeTab() === 'offers') {
      list = list.filter(q => Number(q.documentType) === 2);
    }

    // Status filter
    const status = this.activeStatusFilter();
    if (status !== 'all') {
      list = list.filter(q => {
        const s = Number(q.status);
        switch (status) {
          case 'draft':
            return s === CustomerSalesQuotationStatus.Draft;
          case 'submitted':
            return s === CustomerSalesQuotationStatus.Submitted;
          case 'confirmed':
            return s === CustomerSalesQuotationStatus.Confirmed;
          case 'sent':
            return s === CustomerSalesQuotationStatus.Sent;
          case 'accepted':
            return s === CustomerSalesQuotationStatus.Accepted;
          case 'converted':
            return s === CustomerSalesQuotationStatus.Converted;
          case 'declined':
            return s === CustomerSalesQuotationStatus.DeclinedByCustomer || s === CustomerSalesQuotationStatus.RejectedInternal;
          case 'cancelled':
            return s === CustomerSalesQuotationStatus.Cancelled;
          default:
            return true;
        }
      });
    }

    // Quick search
    const term = this.searchTerm().trim().toLowerCase();
    if (term) {
      list = list.filter(q => {
        const matchNum = q.quotationNumber?.toLowerCase().includes(term);
        const matchNotes = q.notes?.toLowerCase().includes(term) || q.customerRemarks?.toLowerCase().includes(term);
        const matchItems = q.items?.some(i => i.productName?.toLowerCase().includes(term) || i.productCode?.toLowerCase().includes(term));
        return matchNum || matchNotes || matchItems;
      });
    }

    return list;
  });

  readonly tableColumns: DataTableColumn<CustomerSalesQuotationDto>[] = [
    {
      field: 'quotationNumber',
      header: 'Quotation #',
      sortable: true,
      width: '160px'
    },
    {
      field: 'documentType',
      header: 'Type',
      sortable: true,
      width: '150px',
      format: (val: any) => Number(val) === 2 ? 'Commercial Offer' : 'Customer Request'
    },
    {
      field: 'quotationDate',
      header: 'Date',
      sortable: true,
      width: '120px',
      format: (val: any) => {
        if (!val) return '-';
        const d = new Date(val);
        return isNaN(d.getTime()) ? String(val) : d.toLocaleDateString();
      }
    },
    {
      field: 'deliveryDate',
      header: 'Req. Delivery',
      sortable: true,
      width: '130px',
      format: (val: any) => {
        if (!val) return 'Immediate';
        const d = new Date(val);
        return isNaN(d.getTime()) ? String(val) : d.toLocaleDateString();
      }
    },
    {
      field: 'validUntilDateUtc',
      header: 'Valid Until',
      sortable: true,
      width: '130px',
      format: (val: any) => {
        if (!val) return '—';
        const d = new Date(val);
        return isNaN(d.getTime()) ? String(val) : d.toLocaleDateString();
      }
    },
    {
      field: 'totalAmount',
      header: 'Total Amount',
      sortable: true,
      align: 'right',
      width: '140px',
      type: 'currency'
    },
    {
      field: 'status',
      header: 'Status',
      sortable: true,
      width: '180px',
      type: 'badge',
      badgeSeverityMap: (val: any) => this.getStatusBadgeSeverity(val),
      format: (val: any) => getCustomerQuotationStatusLabel(val)
    },
    {
      field: 'id',
      header: 'Actions',
      width: '140px',
      type: 'actions'
    }
  ];

  readonly tableActions: DataTableAction<CustomerSalesQuotationDto>[] = [
    {
      id: 'view',
      label: 'View',
      icon: 'pi pi-eye',
      severity: 'secondary',
      action: (row: CustomerSalesQuotationDto) => this.navigateToQuotation(row)
    }
  ];

  ngOnInit(): void {
    this.loadQuotations();
  }

  loadQuotations(): void {
    this.loading.set(true);
    this.error.set(null);
    this.quotationService.getQuotations().subscribe({
      next: (data) => {
        this.loading.set(false);
        this.quotations.set(data || []);
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(err?.error?.message || err?.message || 'Unable to load quotations.');
        this.quotations.set([]);
      }
    });
  }

  navigateToQuotation(row: CustomerSalesQuotationDto): void {
    this.router.navigate(['/customer-portal/quotations', row.id]);
  }

  getStatusBadgeSeverity(status: number): 'success' | 'warning' | 'danger' | 'info' | 'neutral' {
    switch (status) {
      case CustomerSalesQuotationStatus.Draft:
        return 'neutral';
      case CustomerSalesQuotationStatus.Submitted:
        return 'warning';
      case CustomerSalesQuotationStatus.Confirmed:
      case CustomerSalesQuotationStatus.Sent:
        return 'info';
      case CustomerSalesQuotationStatus.Accepted:
      case CustomerSalesQuotationStatus.Converted:
        return 'success';
      case CustomerSalesQuotationStatus.RejectedInternal:
      case CustomerSalesQuotationStatus.DeclinedByCustomer:
      case CustomerSalesQuotationStatus.Cancelled:
        return 'danger';
      default:
        return 'neutral';
    }
  }
}
