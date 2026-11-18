import { Component, OnInit, signal, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { AppDataTableComponent } from '../../../../shared/components/data-table/data-table.component';
import { TableColumn } from '../../../../shared/models/table-column.model';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { TooltipModule } from 'primeng/tooltip';
import { TenantProvisioningService } from '../../../../core/services/tenant-provisioning.service';
import { TenantDirectoryRecord, TenantUserRecord } from '../../../../core/models/tenant-provisioning.model';

@Component({
  selector: 'app-tenants',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterLink,
    PageHeaderComponent,
    AppDataTableComponent,
    ButtonModule,
    DialogModule,
    InputTextModule,
    TooltipModule
  ],
  templateUrl: './tenants.component.html',
  styleUrl: './tenants.component.scss'
})
export class TenantsComponent implements OnInit {
  private readonly provisioningService = inject(TenantProvisioningService);

  // State signals
  readonly tenants = signal<TenantDirectoryRecord[]>([]);
  readonly loading = signal<boolean>(true);
  readonly errorMessage = signal<string | null>(null);

  // Filter signals
  readonly globalSearch = signal<string>('');
  readonly tenantStatusFilter = signal<string>('ALL');
  readonly planFilter = signal<string>('ALL');
  readonly storageFilter = signal<string>('ALL');
  readonly provisioningStatusFilter = signal<string>('ALL');
  readonly adminStatusFilter = signal<string>('ALL');

  // Modals signals
  readonly selectedTenant = signal<TenantDirectoryRecord | null>(null);
  readonly showDetailsModal = signal<boolean>(false);

  readonly usersModalTenant = signal<TenantDirectoryRecord | null>(null);
  readonly showUsersModal = signal<boolean>(false);
  readonly tenantUsers = signal<TenantUserRecord[]>([]);
  readonly loadingUsers = signal<boolean>(false);
  readonly userSearchQuery = signal<string>('');

  // Computed options
  readonly availablePlans = computed(() => {
    const set = new Set<string>();
    for (const t of this.tenants()) {
      if (t.planName) set.add(t.planName);
    }
    return Array.from(set).sort();
  });

  // KPI Metrics computed from verified database records
  readonly metrics = computed(() => {
    const list = this.tenants();
    const total = list.length;
    const active = list.filter(t => t.isActive).length;
    const completed = list.filter(t => t.isProvisioned).length;
    const totalUsers = list.reduce((sum, t) => sum + (t.totalUserCount || 0), 0);
    const totalAdmins = list.reduce((sum, t) => sum + (t.tenantAdminCount || 0), 0);
    return { total, active, completed, totalUsers, totalAdmins };
  });

  // Filtered dataset for enterprise data table
  readonly filteredTenants = computed(() => {
    const q = this.globalSearch().trim().toLowerCase();
    const statusF = this.tenantStatusFilter();
    const planF = this.planFilter();
    const storageF = this.storageFilter();
    const provF = this.provisioningStatusFilter();
    const adminF = this.adminStatusFilter();

    return this.tenants().filter(t => {
      // Global text search across code, name, legal name, plan, subdomain, admin emails
      if (q) {
        const matchCode = t.code?.toLowerCase().includes(q);
        const matchName = t.name?.toLowerCase().includes(q);
        const matchLegal = t.legalName?.toLowerCase().includes(q);
        const matchPlan = t.planName?.toLowerCase().includes(q);
        const matchSub = t.subdomain?.toLowerCase().includes(q);
        const matchAdmin = t.adminEmail?.toLowerCase().includes(q) ||
          t.tenantAdminEmails?.some(e => e.toLowerCase().includes(q)) ||
          t.tenantAdminUsernames?.some(u => u.toLowerCase().includes(q));
        if (!matchCode && !matchName && !matchLegal && !matchPlan && !matchSub && !matchAdmin) {
          return false;
        }
      }

      // Tenant Active status filter
      if (statusF === 'ACTIVE' && !t.isActive) return false;
      if (statusF === 'INACTIVE' && t.isActive) return false;

      // Plan filter
      if (planF !== 'ALL' && t.planName !== planF) return false;

      // Storage Mode filter
      if (storageF !== 'ALL' && t.storageMode !== storageF) return false;

      // Provisioning status filter
      if (provF === 'Completed' && (!t.isProvisioned || t.status !== 'Completed')) return false;
      if (provF === 'Pending' && (t.isProvisioned && t.status === 'Completed')) return false;

      // Admin Account Status filter
      if (adminF === 'Active' && t.tenantAdminAccountStatus !== 'Active') return false;
      if (adminF === 'Suspended' && t.tenantAdminAccountStatus !== 'Suspended' && t.tenantAdminAccountStatus !== 'Mixed/Suspended') return false;
      if (adminF === 'Unassigned' && (t.tenantAdminCount > 0)) return false;

      return true;
    });
  });

  // Filtered scoped tenant users for View Users modal
  readonly filteredTenantUsers = computed(() => {
    const q = this.userSearchQuery().trim().toLowerCase();
    const users = this.tenantUsers();
    if (!q) return users;

    return users.filter(u =>
      u.username?.toLowerCase().includes(q) ||
      u.email?.toLowerCase().includes(q) ||
      u.roles?.some(r => r.roleName?.toLowerCase().includes(q))
    );
  });

  // Table Columns Definition
  readonly columns: TableColumn<TenantDirectoryRecord>[] = [
    { field: 'code', header: 'Tenant Code', width: '130px', sortable: true },
    { field: 'name', header: 'Organization Name', width: '220px', sortable: true },
    { field: 'planName', header: 'Subscription Plan', width: '170px', sortable: true },
    { field: 'storageMode', header: 'Storage Mode', width: '150px' },
    {
      field: 'status',
      header: 'Provisioning',
      type: 'badge',
      width: '130px',
      badgeSeverityMap: (val: any) => (val === 'Completed' ? 'success' : 'warning')
    },
    {
      field: 'tenantStatus',
      header: 'Tenant Status',
      type: 'badge',
      width: '120px',
      badgeSeverityMap: (val: any) => (val === 'Active' ? 'success' : 'danger')
    },
    { field: 'adminEmail', header: 'Primary Administrator', width: '200px' },
    { field: 'totalUserCount', header: 'Users', width: '90px', sortable: true, align: 'right' },
    { field: 'createdAt', header: 'Enrolled Date', type: 'date', width: '120px', sortable: true },
    { field: 'actions', header: 'Actions', type: 'actions', width: '200px', align: 'center' }
  ];

  ngOnInit(): void {
    this.loadTenants();
  }

  loadTenants(): void {
    this.loading.set(true);
    this.errorMessage.set(null);

    this.provisioningService.getTenantDirectory().subscribe({
      next: (data) => {
        // Normalize fields for presentation
        const normalized = data.map(item => ({
          ...item,
          tenantStatus: item.isActive ? 'Active' as const : 'Inactive' as const,
          adminEmail: item.adminEmail || item.tenantAdminEmails?.[0] || 'Unassigned',
          planName: item.planName || item.subscriptionName || 'Manufacturing Cloud'
        }));
        this.tenants.set(normalized);
        this.loading.set(false);
      },
      error: (err) => {
        this.errorMessage.set(err?.message || 'Failed to load live tenant directory from backend.');
        this.loading.set(false);
      }
    });
  }

  clearFilters(): void {
    this.globalSearch.set('');
    this.tenantStatusFilter.set('ALL');
    this.planFilter.set('ALL');
    this.storageFilter.set('ALL');
    this.provisioningStatusFilter.set('ALL');
    this.adminStatusFilter.set('ALL');
  }

  viewTenantDetails(tenant: TenantDirectoryRecord): void {
    this.selectedTenant.set(tenant);
    this.showDetailsModal.set(true);
  }

  closeDetailsModal(): void {
    this.showDetailsModal.set(false);
    this.selectedTenant.set(null);
  }

  viewTenantUsers(tenant: TenantDirectoryRecord): void {
    this.usersModalTenant.set(tenant);
    this.showUsersModal.set(true);
    this.loadingUsers.set(true);
    this.tenantUsers.set([]);
    this.userSearchQuery.set('');

    this.provisioningService.getTenantUsers(tenant.id).subscribe({
      next: (users) => {
        this.tenantUsers.set(users);
        this.loadingUsers.set(false);
      },
      error: () => {
        this.tenantUsers.set([]);
        this.loadingUsers.set(false);
      }
    });
  }

  closeUsersModal(): void {
    this.showUsersModal.set(false);
    this.usersModalTenant.set(null);
    this.tenantUsers.set([]);
  }
}
