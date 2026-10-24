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
import { UserSecurityService } from '../../../../core/services/user-security.service';
import { UserScopeAssignment, UserAccount } from '../../../../core/models/security.models';

@Component({
  selector: 'app-user-scope-register',
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
    AppLoadingStateComponent
  ],
  templateUrl: './user-scope-register.component.html',
  styleUrl: './user-scope-register.component.scss'
})
export class UserScopeRegisterComponent implements OnInit {
  private readonly router = inject(Router);
  readonly securityService = inject(UserSecurityService);

  readonly scopes = signal<UserScopeAssignment[]>([]);
  readonly users = signal<UserAccount[]>([]);
  readonly loading = signal<boolean>(true);
  readonly errorMessage = signal<string | null>(null);
  readonly successMessage = signal<string | null>(null);

  // Filters
  readonly searchQuery = signal<string>('');
  readonly selectedUserId = signal<string>('all');
  readonly statusFilter = signal<'ALL' | 'Authorized' | 'Revoked'>('ALL');

  readonly breadcrumbs: MenuItem[] = [
    { label: 'Tenant Administration', routerLink: '/tenant-admin' },
    { label: 'User Operating Scopes' }
  ];

  readonly columns: DataTableColumn[] = [
    { field: 'userName', header: 'Authorized User', sortable: true, filterable: true, width: '220px' },
    { field: 'scopeLevel', header: 'Scope Level', sortable: true, width: '130px' },
    { field: 'companyName', header: 'Company Jurisdiction', sortable: true, filterable: true, width: '200px' },
    { field: 'branchName', header: 'Branch / Facility', sortable: true, filterable: true, width: '180px' },
    { field: 'status', header: 'Status', sortable: true, width: '120px' },
    { field: 'grantedAt', header: 'Granted', sortable: true, width: '140px' },
    { field: 'actions', header: 'Actions', width: '120px' }
  ];

  // Computed metrics
  readonly totalScopesCount = computed(() => this.scopes().length);
  readonly companyLevelCount = computed(() => this.scopes().filter(s => s.isAllBranches && s.status === 'Authorized').length);
  readonly branchLevelCount = computed(() => this.scopes().filter(s => !s.isAllBranches && s.status === 'Authorized').length);
  readonly revokedCount = computed(() => this.scopes().filter(s => s.status === 'Revoked').length);

  readonly filteredScopes = computed(() => {
    let list = this.scopes();

    if (this.selectedUserId() !== 'all') {
      list = list.filter(s => s.userId === this.selectedUserId());
    }

    if (this.statusFilter() !== 'ALL') {
      list = list.filter(s => s.status === this.statusFilter());
    }

    const q = this.searchQuery().trim().toLowerCase();
    if (q) {
      list = list.filter(s =>
        s.userName.toLowerCase().includes(q) ||
        s.userEmail.toLowerCase().includes(q) ||
        s.companyName.toLowerCase().includes(q) ||
        s.branchName.toLowerCase().includes(q) ||
        (s.notes && s.notes.toLowerCase().includes(q))
      );
    }

    return list;
  });

  ngOnInit(): void {
    this.loadData();
  }

  loadData(): void {
    this.loading.set(true);
    this.errorMessage.set(null);

    this.securityService.getUserScopes().subscribe({
      next: (data) => {
        this.scopes.set(data);
        this.loading.set(false);
      },
      error: (err) => {
        this.errorMessage.set(err?.message || 'Failed to load user scope assignments from server.');
        this.loading.set(false);
      }
    });

    this.securityService.getUsers().subscribe({
      next: (userList) => this.users.set(userList),
      error: () => {}
    });
  }

  navigateToNewScope(): void {
    this.router.navigate(['/tenant-admin/user-scope/new']);
  }

  revokeScope(scope: UserScopeAssignment): void {
    if (scope.status === 'Revoked') return;

    this.securityService.revokeUserScope(scope.id).subscribe({
      next: (success) => {
        if (success) {
          this.successMessage.set(`Scope for ${scope.userName} on ${scope.companyName} (${scope.branchName}) was revoked.`);
          // Optimistically update local signal
          this.scopes.update(current =>
            current.map(s => s.id === scope.id ? { ...s, status: 'Revoked' as const } : s)
          );
        } else {
          this.errorMessage.set(`Unable to revoke scope assignment.`);
        }
      },
      error: (err) => {
        this.errorMessage.set(err?.message || 'Server error while revoking scope.');
      }
    });
  }
}
