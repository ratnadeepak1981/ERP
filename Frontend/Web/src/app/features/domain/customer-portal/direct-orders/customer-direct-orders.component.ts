import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { StatusBadgeComponent } from '../../../../shared/components/status-badge/status-badge.component';

export interface CustomerDirectOrderSummary {
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
  imports: [CommonModule, StatusBadgeComponent],
  template: `
    <div class="customer-direct-orders-page">
      <div class="page-title-row">
        <div>
          <h1>Direct Contract Orders</h1>
          <p>Authorised commercial orders placed directly against active Sales Price Cards.</p>
        </div>
      </div>

      <div class="direct-orders-card erp-card">
        <div class="table-responsive">
          <table class="erp-table">
            <thead>
              <tr>
                <th>Order Ref</th>
                <th>Order Date</th>
                <th>Contract Price Card</th>
                <th>Items Count</th>
                <th>Gross Total</th>
                <th>Status</th>
                <th>Action</th>
              </tr>
            </thead>
            <tbody>
              @for (ord of directOrders(); track ord.directOrderRef) {
                <tr>
                  <td class="font-mono text-primary font-bold">{{ ord.directOrderRef }}</td>
                  <td>{{ ord.orderDate }}</td>
                  <td><code>{{ ord.contractCardCode }}</code></td>
                  <td>{{ ord.itemsCount }} items</td>
                  <td class="font-bold">{{ ord.totalAmount }}</td>
                  <td>
                    <app-status-badge [status]="ord.status" [label]="ord.statusLabel"></app-status-badge>
                  </td>
                  <td>
                    <button type="button" class="erp-button erp-button-secondary erp-button-sm">
                      View Dispatch
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
    .customer-direct-orders-page {
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

    .direct-orders-card {
      padding: var(--erp-space-4);
    }

    .table-responsive {
      overflow-x: auto;
    }
  `]
})
export class CustomerDirectOrdersComponent {
  readonly directOrders = signal<CustomerDirectOrderSummary[]>([
    {
      directOrderRef: 'DIR-2026-0012',
      orderDate: '08-Nov-2026',
      contractCardCode: 'PC-ACME-2026-A',
      itemsCount: 5,
      totalAmount: '$8,200.00',
      status: 'active',
      statusLabel: 'Confirmed'
    }
  ]);
}
