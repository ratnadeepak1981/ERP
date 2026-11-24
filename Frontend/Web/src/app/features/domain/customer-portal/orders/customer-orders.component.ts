import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { StatusBadgeComponent } from '../../../../shared/components/status-badge/status-badge.component';
import { AppPageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';

export interface CustomerOrderSummary {
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
  imports: [CommonModule, RouterLink, StatusBadgeComponent, AppPageHeaderComponent],
  template: `
    <div class="customer-orders-page">
      <app-page-header
        title="Order History &amp; Requisitions"
        subtitle="Track order fulfillment, shipment waybills, and historical sales orders."
      >
        <div actions class="page-header-actions">
          <a routerLink="/customer-portal/catalog" class="erp-button erp-button-primary">
            <i class="pi pi-plus"></i>
            <span>New Requisition</span>
          </a>
        </div>
      </app-page-header>

      <div class="orders-card erp-card">
        <div class="table-responsive">
          <table class="erp-table">
            <thead>
              <tr>
                <th>Order Ref</th>
                <th>Order Date</th>
                <th>Items Count</th>
                <th>Total Value</th>
                <th>Delivery Address</th>
                <th>Status</th>
                <th>Action</th>
              </tr>
            </thead>
            <tbody>
              @for (ord of orders(); track ord.orderNumber) {
                <tr>
                  <td class="font-mono text-primary font-bold">{{ ord.orderNumber }}</td>
                  <td>{{ ord.orderDate }}</td>
                  <td>{{ ord.itemsCount }} items</td>
                  <td class="font-bold">{{ ord.totalAmount }}</td>
                  <td>{{ ord.deliveryAddress }}</td>
                  <td>
                    <app-status-badge [status]="ord.status" [label]="ord.statusLabel"></app-status-badge>
                  </td>
                  <td>
                    <button type="button" class="erp-button erp-button-secondary erp-button-sm">
                      <i class="pi pi-eye"></i>
                      <span>View Details</span>
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
    .customer-orders-page {
      display: flex;
      flex-direction: column;
      gap: var(--erp-space-4);
    }

    .page-header-actions {
      display: flex;
      align-items: center;
      gap: var(--erp-space-2, 8px);
    }

    .orders-card {
      padding: var(--erp-space-4);
    }

    .table-responsive {
      overflow-x: auto;
    }
  `]
})
export class CustomerOrdersComponent {
  readonly orders = signal<CustomerOrderSummary[]>([
    {
      orderNumber: 'SO-2026-0891',
      orderDate: '12-Oct-2026',
      itemsCount: 4,
      totalAmount: '$6,420.00',
      status: 'active',
      statusLabel: 'In Production',
      deliveryAddress: 'Dock 4, Industrial Zone, Colombo'
    },
    {
      orderNumber: 'SO-2026-0744',
      orderDate: '28-Sep-2026',
      itemsCount: 12,
      totalAmount: '$18,900.00',
      status: 'success',
      statusLabel: 'Shipped',
      deliveryAddress: 'Plant 2 Warehouse, Kandy Rd'
    },
    {
      orderNumber: 'SO-2026-0512',
      orderDate: '14-Aug-2026',
      itemsCount: 2,
      totalAmount: '$1,250.00',
      status: 'success',
      statusLabel: 'Delivered',
      deliveryAddress: 'Dock 4, Industrial Zone, Colombo'
    }
  ]);
}
