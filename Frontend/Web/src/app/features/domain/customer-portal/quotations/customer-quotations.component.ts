import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { StatusBadgeComponent } from '../../../../shared/components/status-badge/status-badge.component';
import { AppPageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { CustomerQuotationService } from '../../../../core/services/customer-quotation.service';
import {
  CustomerSalesQuotationDto,
  getCustomerQuotationStatusLabel,
  getCustomerQuotationStatusSeverity
} from '../../../../core/models/customer-quotation.models';

@Component({
  selector: 'app-customer-quotations',
  standalone: true,
  imports: [CommonModule, RouterModule, StatusBadgeComponent, AppPageHeaderComponent],
  template: `
    <div class="customer-quotations-page">
      <app-page-header
        title="Corporate Sales Quotations"
        subtitle="Request customized price quotations, review supplier commercial proposals, and track RFQ statuses."
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
            <span>Request Sales Quotation</span>
          </a>
        </div>
      </app-page-header>

      @if (loading()) {
        <div class="quotations-card erp-card">
          <div class="loading-state">
            <i class="pi pi-spin pi-spinner text-primary" style="font-size: 2rem;"></i>
            <p>Loading quotations history...</p>
          </div>
        </div>
      } @else if (quotations().length === 0) {
        <div class="quotations-card erp-card empty-card">
          <div class="empty-state">
            <div class="empty-icon-wrap">
              <i class="pi pi-file-edit"></i>
            </div>
            <h3>No Quotation Requests Found</h3>
            <p>You have not submitted any sales quotation requests yet. Request tailored commercial pricing for bulk parts and assemblies.</p>
            <a routerLink="/customer-portal/quotations/new" class="erp-button erp-button-primary">
              <i class="pi pi-plus"></i>
              <span>Create First Quotation Request</span>
            </a>
          </div>
        </div>
      } @else {
        <div class="quotations-card erp-card">
          <div class="table-responsive">
            <table class="erp-table">
              <thead>
                <tr>
                  <th>Quotation Ref</th>
                  <th>Request Date</th>
                  <th>Req. Delivery</th>
                  <th>Valid Until</th>
                  <th>Line Items</th>
                  <th>Total Amount</th>
                  <th>Status</th>
                  <th>Action</th>
                </tr>
              </thead>
              <tbody>
                @for (q of quotations(); track q.id) {
                  <tr>
                    <td class="font-mono text-primary font-bold">
                      <a [routerLink]="['/customer-portal/quotations', q.id]" class="ref-link">
                        {{ q.quotationNumber }}
                      </a>
                    </td>
                    <td>{{ q.quotationDate | date:'mediumDate' }}</td>
                    <td>{{ q.deliveryDate ? (q.deliveryDate | date:'mediumDate') : 'Immediate' }}</td>
                    <td>{{ q.validUntilDateUtc | date:'mediumDate' }}</td>
                    <td>{{ q.items ? q.items.length : 0 }} items</td>
                    <td class="font-bold">{{ q.totalAmount | currency:q.currency:'symbol':'1.2-2' }}</td>
                    <td>
                      <app-status-badge
                        [status]="getStatusSeverity(q.status)"
                        [label]="getStatusLabel(q.status)"
                      ></app-status-badge>
                    </td>
                    <td>
                      <a [routerLink]="['/customer-portal/quotations', q.id]" class="erp-button erp-button-secondary erp-button-sm">
                        <i class="pi pi-eye"></i>
                        <span>View Details</span>
                      </a>
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        </div>
      }
    </div>
  `,
  styles: [`
    .customer-quotations-page {
      display: flex;
      flex-direction: column;
      gap: var(--erp-space-4);
    }

    .page-header-actions {
      display: flex;
      align-items: center;
      gap: var(--erp-space-2, 8px);
    }

    .quotations-card {
      padding: var(--erp-space-4);
    }

    .table-responsive {
      overflow-x: auto;
    }

    .ref-link {
      color: var(--erp-primary);
      text-decoration: none;

      &:hover {
        text-decoration: underline;
      }
    }

    .empty-card {
      padding: var(--erp-space-6) var(--erp-space-4);
    }

    .empty-state {
      display: flex;
      flex-direction: column;
      align-items: center;
      text-align: center;
      gap: var(--erp-space-3);
      max-width: 460px;
      margin: 0 auto;

      .empty-icon-wrap {
        width: 60px;
        height: 60px;
        border-radius: 50%;
        background: var(--erp-primary-subtle, #eff6ff);
        color: var(--erp-primary, #0f6cbd);
        display: flex;
        align-items: center;
        justify-content: center;
        font-size: 26px;
      }

      h3 {
        margin: 0;
        font-size: 1.15rem;
        color: var(--erp-text-main);
      }

      p {
        margin: 0;
        font-size: 0.88rem;
        color: var(--erp-text-secondary);
        line-height: 1.4;
      }
    }

    .loading-state {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: var(--erp-space-3);
      padding: var(--erp-space-6);
      color: var(--erp-text-secondary);
    }
  `]
})
export class CustomerQuotationsComponent implements OnInit {
  private readonly quotationService = inject(CustomerQuotationService);

  readonly loading = signal<boolean>(true);
  readonly quotations = signal<CustomerSalesQuotationDto[]>([]);

  ngOnInit(): void {
    this.loadQuotations();
  }

  loadQuotations(): void {
    this.loading.set(true);
    this.quotationService.getQuotations().subscribe({
      next: (data) => {
        this.loading.set(false);
        this.quotations.set(data || []);
      },
      error: () => {
        this.loading.set(false);
        this.quotations.set([]);
      }
    });
  }

  getStatusLabel(status: number): string {
    return getCustomerQuotationStatusLabel(status);
  }

  getStatusSeverity(status: number): 'active' | 'warning' | 'info' | 'success' | 'danger' {
    return getCustomerQuotationStatusSeverity(status);
  }
}
