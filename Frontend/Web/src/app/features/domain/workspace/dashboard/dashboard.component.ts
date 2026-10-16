import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { AppPageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { AppDataTableComponent } from '../../../../shared/components/data-table/data-table.component';
import { AppStatusBadgeComponent } from '../../../../shared/components/status-badge/status-badge.component';
import { WorkspaceScopeService } from '../../../../core/services/workspace-scope.service';
import { MasterDataService } from '../../../../core/services/master-data.service';
import { CompanyService } from '../../../../core/services/company.service';
import { AuthService } from '../../../../core/auth/auth.service';
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
export class DashboardComponent implements OnInit {
  readonly scopeService = inject(WorkspaceScopeService);
  readonly masterDataService = inject(MasterDataService);
  readonly companyService = inject(CompanyService);
  readonly authService = inject(AuthService);

  readonly liveProductCount = signal<number>(0);
  readonly liveSupplierCount = signal<number>(0);
  readonly liveWarehouseCount = signal<number>(0);
  readonly liveCompanyCount = signal<number>(0);

  readonly breadcrumbs = [
    { label: 'Workspace', routerLink: '/app/dashboard' },
    { label: 'Operations Overview' }
  ];

  // Preserved for test compatibility and baseline operational layout
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

  // Transactions are strictly isolated to active operating company and branch
  readonly displayedOrders = computed(() => {
    // If no operating company is selected, do NOT display transactions
    if (!this.scopeService.hasCompany()) {
      return [];
    }
    const activeBranch = this.scopeService.selectedBranch();
    if (activeBranch) {
      return this.recentOrders.filter(o =>
        o.branch.toLowerCase().includes(activeBranch.name.toLowerCase()) ||
        activeBranch.name.toLowerCase().includes(o.branch.toLowerCase())
      );
    }
    return this.recentOrders;
  });

  // KPI cards reflect active scope status
  readonly displayedKpiCards = computed(() => {
    if (!this.scopeService.hasCompany()) {
      return [
        {
          title: 'Inventory Valuation',
          value: '$0.00',
          trend: 'Setup Required',
          trendPositive: false,
          subtext: 'Select an operating company',
          icon: 'pi pi-box',
          accentColor: '#64748b'
        },
        {
          title: 'Active Sales Orders',
          value: '0 Orders',
          trend: '$0.00',
          trendPositive: false,
          subtext: 'Select an operating company',
          icon: 'pi pi-shopping-bag',
          accentColor: '#64748b'
        },
        {
          title: 'Open Purchase Orders',
          value: '0 Orders',
          trend: '$0.00',
          trendPositive: false,
          subtext: 'Select an operating company',
          icon: 'pi pi-shopping-cart',
          accentColor: '#64748b'
        },
        {
          title: 'Replenishment Signals',
          value: '0 SKUs Low',
          trend: 'No Scope',
          trendPositive: false,
          subtext: 'Select an operating company',
          icon: 'pi pi-exclamation-circle',
          accentColor: '#64748b'
        }
      ];
    }
    return this.kpiCards;
  });

  ngOnInit(): void {
    this.loadRealCounts();
  }

  loadRealCounts(): void {
    this.masterDataService.getProducts().subscribe({
      next: (prods) => this.liveProductCount.set(prods.length),
      error: () => {}
    });

    this.masterDataService.getSuppliers().subscribe({
      next: (sups) => this.liveSupplierCount.set(sups.length),
      error: () => {}
    });

    this.masterDataService.getWarehouses().subscribe({
      next: (whs) => this.liveWarehouseCount.set(whs.length),
      error: () => {}
    });

    this.companyService.getCompanies().subscribe({
      next: (comps) => this.liveCompanyCount.set(comps.length),
      error: () => {}
    });
  }
}
