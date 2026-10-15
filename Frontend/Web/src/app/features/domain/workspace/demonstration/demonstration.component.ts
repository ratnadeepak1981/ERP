import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { AppPageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { AppDataTableComponent } from '../../../../shared/components/data-table/data-table.component';
import { AppStatusBadgeComponent } from '../../../../shared/components/status-badge/status-badge.component';
import { AppErrorStateComponent } from '../../../../shared/components/error-state/error-state.component';
import { TableColumn } from '../../../../shared/models/table-column.model';
import { DemoSalesOrder } from './demonstration.model';

@Component({
  selector: 'app-demonstration',
  standalone: true,
  imports: [
    CommonModule,
    ButtonModule,
    DialogModule,
    AppPageHeaderComponent,
    AppDataTableComponent,
    AppStatusBadgeComponent,
    AppErrorStateComponent
  ],
  templateUrl: './demonstration.component.html',
  styleUrl: './demonstration.component.scss'
})
export class DemonstrationComponent {
  readonly breadcrumbs = [
    { label: 'Workspace', routerLink: '/app/dashboard' },
    { label: 'UI Component Showcase' }
  ];

  readonly isSimulatingLoading = signal<boolean>(false);
  readonly isSimulatingEmpty = signal<boolean>(false);
  readonly isSimulatingError = signal<boolean>(false);

  readonly selectedOrder = signal<DemoSalesOrder | null>(null);
  readonly detailsDialogVisible = signal<boolean>(false);

  readonly columns: TableColumn<DemoSalesOrder>[] = [
    { field: 'orderNumber', header: 'Order Code', width: '130px', sortable: true },
    { field: 'customerName', header: 'Customer Organization', sortable: true },
    { field: 'branch', header: 'Fulfillment Branch', width: '140px' },
    { field: 'orderDate', header: 'Placement Date', width: '120px', sortable: true },
    { field: 'itemCount', header: 'Qty', width: '70px', align: 'center' },
    { field: 'totalAmount', header: 'Total Value (USD)', type: 'currency', align: 'right', sortable: true },
    {
      field: 'status',
      header: 'Workflow State',
      type: 'badge',
      width: '130px',
      badgeSeverityMap: (status: string) => {
        switch (status) {
          case 'Fulfilled': return 'success';
          case 'Shipped': return 'info';
          case 'Confirmed':
          case 'Processing': return 'warning';
          case 'Cancelled': return 'danger';
          default: return 'neutral';
        }
      }
    }
  ];

  readonly dataset: DemoSalesOrder[] = [
    { id: '1', orderNumber: 'SO-2026-101', customerName: 'Colombo Metal Fabricators', orderDate: '2026-10-14', totalAmount: 4850.0, status: 'Confirmed', itemCount: 12, branch: 'Colombo Main' },
    { id: '2', orderNumber: 'SO-2026-102', customerName: 'Ceylon Polymer Works PLC', orderDate: '2026-10-14', totalAmount: 12300.5, status: 'Processing', itemCount: 45, branch: 'Colombo Main' },
    { id: '3', orderNumber: 'SO-2026-103', customerName: 'Lanka Electrical Assemblies', orderDate: '2026-10-13', totalAmount: 2150.0, status: 'Shipped', itemCount: 5, branch: 'Kandy WH' },
    { id: '4', orderNumber: 'SO-2026-104', customerName: 'Kelani Rolling Mills Ltd', orderDate: '2026-10-12', totalAmount: 9400.0, status: 'Fulfilled', itemCount: 30, branch: 'Colombo Main' },
    { id: '5', orderNumber: 'SO-2026-105', customerName: 'Standard Packaging Solutions', orderDate: '2026-10-11', totalAmount: 1650.0, status: 'Draft', itemCount: 8, branch: 'Biyagama FTZ' },
    { id: '6', orderNumber: 'SO-2026-106', customerName: 'Global Garment Accessories', orderDate: '2026-10-10', totalAmount: 7800.0, status: 'Confirmed', itemCount: 18, branch: 'Katunayake' },
    { id: '7', orderNumber: 'SO-2026-107', customerName: 'Southern Steel Traders', orderDate: '2026-10-09', totalAmount: 5120.0, status: 'Cancelled', itemCount: 14, branch: 'Galle Depot' },
    { id: '8', orderNumber: 'SO-2026-108', customerName: 'Highland Chemicals Corp', orderDate: '2026-10-08', totalAmount: 14200.0, status: 'Fulfilled', itemCount: 60, branch: 'Colombo Main' }
  ];

  get displayedData(): DemoSalesOrder[] {
    if (this.isSimulatingEmpty()) return [];
    return this.dataset;
  }

  onRowClick(row: DemoSalesOrder): void {
    this.selectedOrder.set(row);
    this.detailsDialogVisible.set(true);
  }

  toggleLoadingSimulation(): void {
    this.isSimulatingLoading.set(true);
    setTimeout(() => this.isSimulatingLoading.set(false), 1200);
  }

  toggleEmptySimulation(): void {
    this.isSimulatingEmpty.update(empty => !empty);
  }

  toggleErrorSimulation(): void {
    this.isSimulatingError.update(err => !err);
  }

  onRetry(): void {
    this.isSimulatingError.set(false);
  }
}
