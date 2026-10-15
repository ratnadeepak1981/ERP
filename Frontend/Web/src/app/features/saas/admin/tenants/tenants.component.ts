import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { AppDataTableComponent } from '../../../../shared/components/data-table/data-table.component';
import { TableColumn } from '../../../../shared/models/table-column.model';
import { ButtonModule } from 'primeng/button';

export interface TenantRecord {
  id: string;
  name: string;
  code: string;
  plan: string;
  storageMode: string;
  status: string;
  usersCount: number;
  createdAt: string;
}

@Component({
  selector: 'app-tenants',
  standalone: true,
  imports: [CommonModule, RouterLink, PageHeaderComponent, AppDataTableComponent, ButtonModule],
  templateUrl: './tenants.component.html',
  styleUrl: './tenants.component.scss'
})
export class TenantsComponent {
  readonly tenants = signal<TenantRecord[]>([
    {
      id: 'a1b2c3d4-1111-4444-8888-000000000001',
      name: 'Alpha Manufacturing Ltd',
      code: 'ALPHA',
      plan: 'Enterprise Pro',
      storageMode: 'SharedDatabase',
      status: 'Active',
      usersCount: 42,
      createdAt: '2026-01-15'
    },
    {
      id: 'b2c3d4e5-2222-4444-8888-000000000002',
      name: 'Beta Precision Machining',
      code: 'BETA',
      plan: 'Standard Growth',
      storageMode: 'SharedDatabase',
      status: 'Active',
      usersCount: 18,
      createdAt: '2026-03-20'
    },
    {
      id: 'c3d4e5f6-3333-4444-8888-000000000003',
      name: 'Gamma Automotive Components',
      code: 'GAMMA',
      plan: 'Enterprise Elite',
      storageMode: 'DedicatedDatabase',
      status: 'Active',
      usersCount: 110,
      createdAt: '2026-05-10'
    },
    {
      id: 'd4e5f6g7-4444-4444-8888-000000000004',
      name: 'Delta Plastics & Molding',
      code: 'DELTA',
      plan: 'Starter Free',
      storageMode: 'SharedDatabase',
      status: 'Pending Verification',
      usersCount: 5,
      createdAt: '2026-09-02'
    }
  ]);

  readonly columns: TableColumn<TenantRecord>[] = [
    { field: 'code', header: 'Code', width: '110px', sortable: true },
    { field: 'name', header: 'Organization Name', sortable: true },
    { field: 'plan', header: 'Subscription Plan', width: '160px', sortable: true },
    { field: 'storageMode', header: 'Storage Mode', width: '160px' },
    {
      field: 'status',
      header: 'Status',
      type: 'badge',
      width: '130px',
      badgeSeverityMap: (val) => (val === 'Active' ? 'success' : 'warning')
    },
    { field: 'usersCount', header: 'Active Users', width: '120px', sortable: true, align: 'right' },
    { field: 'createdAt', header: 'Enrolled Date', type: 'date', width: '130px' }
  ];
}
