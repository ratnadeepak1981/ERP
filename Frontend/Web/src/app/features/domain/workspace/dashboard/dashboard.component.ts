import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { AppPageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { AppDataTableComponent } from '../../../../shared/components/data-table/data-table.component';
import { AppStatusBadgeComponent } from '../../../../shared/components/status-badge/status-badge.component';
import { TableColumn } from '../../../../shared/models/table-column.model';
import { DemoSalesOrder } from '../demonstration/demonstration.model';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    ButtonModule,
    CardModule,
    AppPageHeaderComponent,
    AppDataTableComponent,
    AppStatusBadgeComponent
  ],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.scss'
})
export class DashboardComponent {
  readonly breadcrumbs = [
    { label: 'Workspace', routerLink: '/app/dashboard' },
    { label: 'Operations Overview' }
  ];

  readonly kpiCards = [
    {
      title: 'Inventory Valuation',
      value: '$245,890.00',
      trend: '+12.5%',
      trendPositive: true,
      subtext: 'Across 3 warehouse zones',
      icon: 'pi pi-box',
      accentColor: '#0f6cbd'
    },
    {
      title: 'Active Sales Orders',
      value: '24 Orders',
      trend: '$38,450.00',
      trendPositive: true,
      subtext: '4 awaiting shipment dispatch',
      icon: 'pi pi-shopping-bag',
      accentColor: '#107c41'
    },
    {
      title: 'Open Purchase Orders',
      value: '9 Orders',
      trend: '$15,200.00',
      trendPositive: false,
      subtext: '2 pending GRN inspection',
      icon: 'pi pi-shopping-cart',
      accentColor: '#b74700'
    },
    {
      title: 'Replenishment Signals',
      value: '3 SKUs Low',
      trend: 'Action Required',
      trendPositive: false,
      subtext: 'Reorder points triggered',
      icon: 'pi pi-exclamation-circle',
      accentColor: '#c42b1c'
    }
  ];

  readonly recentOrdersColumns: TableColumn<DemoSalesOrder>[] = [
    { field: 'orderNumber', header: 'Order #', width: '130px', sortable: true },
    { field: 'customerName', header: 'Customer', sortable: true },
    { field: 'orderDate', header: 'Date', width: '110px' },
    { field: 'itemCount', header: 'Items', width: '80px', align: 'center' },
    { field: 'totalAmount', header: 'Total Value', type: 'currency', align: 'right', sortable: true },
    {
      field: 'status',
      header: 'Status',
      type: 'badge',
      width: '120px',
      badgeSeverityMap: (val: string) => {
        switch (val) {
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

  readonly recentOrders: DemoSalesOrder[] = [
    { id: '1', orderNumber: 'SO-2026-001', customerName: 'Apex Industrial Fab', orderDate: '2026-10-14', totalAmount: 4850.0, status: 'Confirmed', itemCount: 12, branch: 'Colombo' },
    { id: '2', orderNumber: 'SO-2026-002', customerName: 'Ceylon Polymer Works', orderDate: '2026-10-14', totalAmount: 12300.5, status: 'Processing', itemCount: 45, branch: 'Colombo' },
    { id: '3', orderNumber: 'SO-2026-003', customerName: 'Lanka Electrical Tech', orderDate: '2026-10-13', totalAmount: 2150.0, status: 'Shipped', itemCount: 5, branch: 'Colombo' },
    { id: '4', orderNumber: 'SO-2026-004', customerName: 'Kelani Metal Products', orderDate: '2026-10-12', totalAmount: 9400.0, status: 'Fulfilled', itemCount: 30, branch: 'Colombo' },
    { id: '5', orderNumber: 'SO-2026-005', customerName: 'Standard Packaging Ltd', orderDate: '2026-10-11', totalAmount: 1650.0, status: 'Draft', itemCount: 8, branch: 'Colombo' }
  ];
}
