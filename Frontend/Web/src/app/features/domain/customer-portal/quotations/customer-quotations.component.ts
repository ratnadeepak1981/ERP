import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { StatusBadgeComponent } from '../../../../shared/components/status-badge/status-badge.component';

export interface CustomerQuotationSummary {
  quotationNumber: string;
  submissionDate: string;
  validUntil: string;
  itemsCount: number;
  totalAmount: string;
  status: 'active' | 'warning' | 'info' | 'success';
  statusLabel: string;
}

@Component({
  selector: 'app-customer-quotations',
  standalone: true,
  imports: [CommonModule, StatusBadgeComponent],
  template: `
    <div class="customer-quotations-page">
      <div class="page-title-row">
        <div>
          <h1>Corporate Sales Quotations</h1>
          <p>Review supplier quotation proposals, technical terms, and contract Price Card agreements.</p>
        </div>
      </div>

      <div class="quotations-card erp-card">
        <div class="table-responsive">
          <table class="erp-table">
            <thead>
              <tr>
                <th>Quotation Ref</th>
                <th>Requested Date</th>
                <th>Valid Until</th>
                <th>Line Items</th>
                <th>Estimated Total</th>
                <th>Status</th>
                <th>Action</th>
              </tr>
            </thead>
            <tbody>
              @for (q of quotations(); track q.quotationNumber) {
                <tr>
                  <td class="font-mono text-primary font-bold">{{ q.quotationNumber }}</td>
                  <td>{{ q.submissionDate }}</td>
                  <td>{{ q.validUntil }}</td>
                  <td>{{ q.itemsCount }} items</td>
                  <td class="font-bold">{{ q.totalAmount }}</td>
                  <td>
                    <app-status-badge [status]="q.status" [label]="q.statusLabel"></app-status-badge>
                  </td>
                  <td>
                    <button type="button" class="erp-button erp-button-secondary erp-button-sm">
                      Review Proposal
                    </button>
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .customer-quotations-page {
      display: flex;
      flex-direction: column;
      gap: var(--erp-space-4);
    }

    .page-title-row {
      h1 {
        font-size: 1.4rem;
        font-weight: 700;
        margin: 0;
        color: var(--erp-text-main);
      }

      p {
        font-size: 0.88rem;
        color: var(--erp-text-secondary);
        margin: 2px 0 0 0;
      }
    }

    .quotations-card {
      padding: var(--erp-space-4);
    }

    .table-responsive {
      overflow-x: auto;
    }
  `]
})
export class CustomerQuotationsComponent {
  readonly quotations = signal<CustomerQuotationSummary[]>([
    {
      quotationNumber: 'QT-2026-0042',
      submissionDate: '15-Nov-2026',
      validUntil: '15-Dec-2026',
      itemsCount: 8,
      totalAmount: '$12,450.00',
      status: 'active',
      statusLabel: 'Issued / Under Review'
    },
    {
      quotationNumber: 'QT-2026-0038',
      submissionDate: '24-Oct-2026',
      validUntil: '24-Nov-2026',
      itemsCount: 3,
      totalAmount: '$4,800.00',
      status: 'success',
      statusLabel: 'Accepted & Converted'
    }
  ]);
}
