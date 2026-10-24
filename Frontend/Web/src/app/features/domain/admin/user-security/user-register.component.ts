import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { TooltipModule } from 'primeng/tooltip';
import { MenuItem } from 'primeng/api';
import { AppPageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { AppDataTableComponent } from '../../../../shared/components/data-table/data-table.component';
import { DataTableColumn } from '../../../../shared/models/data-table-config.model';
import { AppStatusBadgeComponent } from '../../../../shared/components/status-badge/status-badge.component';
import { AppLoadingStateComponent } from '../../../../shared/components/loading-state/loading-state.component';
import { AppErrorStateComponent } from '../../../../shared/components/error-state/error-state.component';
import { UserSecurityService } from '../../../../core/services/user-security.service';
import { WorkspaceScopeService } from '../../../../core/services/workspace-scope.service';
import { UserAccount } from '../../../../core/models/security.models';

@Component({
  selector: 'app-user-register',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterModule,
    ButtonModule,
    InputTextModule,
    TooltipModule,
    AppPageHeaderComponent,
    AppDataTableComponent,
    AppStatusBadgeComponent,
    AppLoadingStateComponent,
    AppErrorStateComponent
  ],
  templateUrl: './user-register.component.html',
  styleUrl: './user-register.component.scss'
})
export class UserRegisterComponent implements OnInit {
  private readonly router = inject(Router);
  readonly securityService = inject(UserSecurityService);
  readonly scopeService = inject(WorkspaceScopeService);

  readonly breadcrumbs: MenuItem[] = [
    { label: 'Workspace', routerLink: '/app/dashboard' },
    { label: 'Administration' },
    { label: 'User Accounts' }
  ];

  readonly loading = signal<boolean>(true);
  readonly error = signal<string | null>(null);
  readonly actionMessage = signal<{ severity: 'success' | 'danger' | 'info'; text: string } | null>(null);

  readonly users = signal<UserAccount[]>([]);
  readonly searchQuery = signal<string>('');
  readonly statusFilter = signal<'all' | 'Active' | 'Suspended'>('all');

  readonly userColumns: DataTableColumn[] = [
    { field: 'fullName', header: 'User Name', sortable: true, filterable: true },
    { field: 'email', header: 'Email Address', sortable: true, filterable: true },
    { field: 'roleName', header: 'Assigned Role', sortable: true, filterable: true },
    { field: 'primaryScope', header: 'Primary Facility Scope', sortable: true },
    { field: 'accountStatus', header: 'Account Status', sortable: true, filterable: true }
  ];

  readonly activeUsersCount = computed(() => this.users().filter(u => u.accountStatus === 'Active').length);
  readonly suspendedUsersCount = computed(() => this.users().filter(u => u.accountStatus !== 'Active').length);

  readonly displayedUsers = computed(() => {
    const q = this.searchQuery().toLowerCase().trim();
    const status = this.statusFilter();
    let list = this.users();

    if (status !== 'all') {
      list = list.filter(u => status === 'Active' ? u.accountStatus === 'Active' : u.accountStatus !== 'Active');
    }

    if (!q) return list;

    return list.filter(u =>
      (u.fullName || '').toLowerCase().includes(q) ||
      (u.email || '').toLowerCase().includes(q) ||
      (u.username || '').toLowerCase().includes(q) ||
      (u.roleName || '').toLowerCase().includes(q) ||
      (u.primaryCompanyName || '').toLowerCase().includes(q) ||
      (u.primaryBranchName || '').toLowerCase().includes(q)
    );
  });

  ngOnInit(): void {
    this.loadUsers();
  }

  loadUsers(): void {
    this.loading.set(true);
    this.error.set(null);

    this.securityService.getUsers().subscribe({
      next: data => {
        this.users.set(data);
        this.loading.set(false);
      },
      error: err => {
        this.error.set('Failed to load user accounts from server.');
        this.loading.set(false);
      }
    });
  }

  navigateToCreateUser(): void {
    this.router.navigate(['/tenant-admin/users/new']);
  }

  navigateToUserDetails(user: UserAccount): void {
    this.router.navigate(['/tenant-admin/users', user.id]);
  }

  manageUserScope(user: UserAccount): void {
    this.router.navigate(['/tenant-admin/user-scope'], {
      queryParams: { userId: user.id }
    });
  }

  toggleUserStatus(user: UserAccount): void {
    this.securityService.toggleUserStatus(user.id).subscribe({
      next: updated => {
        const newStatus = user.accountStatus === 'Active' ? 'Suspended' : 'Active';
        this.actionMessage.set({
          severity: 'success',
          text: `User ${user.username} account has been ${newStatus === 'Active' ? 'activated' : 'suspended'}.`
        });
        this.loadUsers();
      },
      error: () => {
        this.actionMessage.set({
          severity: 'danger',
          text: `Failed to update status for user ${user.username}.`
        });
      }
    });
  }
}
