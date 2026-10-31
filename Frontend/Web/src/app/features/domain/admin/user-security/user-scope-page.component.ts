import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { TooltipModule } from 'primeng/tooltip';
import { MenuItem } from 'primeng/api';
import { AppPageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { AppLoadingStateComponent } from '../../../../shared/components/loading-state/loading-state.component';
import { AppStatusBadgeComponent } from '../../../../shared/components/status-badge/status-badge.component';
import { UserSecurityService } from '../../../../core/services/user-security.service';
import { CompanyService } from '../../../../core/services/company.service';
import { Company, Branch } from '../../../../core/models/scope.models';
import { UserAccount, AssignScopeDto, UserScopeAssignment } from '../../../../core/models/security.models';

@Component({
  selector: 'app-user-scope-page',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterModule,
    ButtonModule,
    InputTextModule,
    TooltipModule,
    AppPageHeaderComponent,
    AppLoadingStateComponent,
    AppStatusBadgeComponent
  ],
  templateUrl: './user-scope-page.component.html',
  styleUrl: './user-scope-page.component.scss'
})
export class UserScopePageComponent implements OnInit {
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  readonly securityService = inject(UserSecurityService);
  readonly companyService = inject(CompanyService);

  readonly loading = signal<boolean>(true);
  readonly saving = signal<boolean>(false);
  readonly loadingUserScopes = signal<boolean>(false);
  readonly revokingScopeId = signal<string | null>(null);
  readonly errorMessage = signal<string | null>(null);
  readonly successMessage = signal<string | null>(null);

  readonly users = signal<UserAccount[]>([]);
  readonly companies = signal<Company[]>([]);
  readonly branches = signal<Branch[]>([]);
  readonly userScopes = signal<UserScopeAssignment[]>([]);

  // Search & Filter
  readonly userSearchTerm = signal<string>('');

  // Form state
  selectedUserId = '';
  selectedCompanyId = '';
  selectedBranchId = '';
  isAllBranches = false;
  scopeNotes = '';

  readonly breadcrumbs: MenuItem[] = [
    { label: 'Tenant Administration', routerLink: '/tenant-admin' },
    { label: 'User Operating Scopes', routerLink: '/tenant-admin/user-scope' },
    { label: 'Assign Operating Scope' }
  ];

  readonly filteredUsers = computed(() => {
    const term = this.userSearchTerm().trim().toLowerCase();
    const list = this.users();
    if (!term) return list;
    return list.filter(u =>
      (u.fullName && u.fullName.toLowerCase().includes(term)) ||
      (u.username && u.username.toLowerCase().includes(term)) ||
      (u.email && u.email.toLowerCase().includes(term)) ||
      (u.roleName && u.roleName.toLowerCase().includes(term))
    );
  });

  readonly selectedUser = computed(() =>
    this.users().find(u => u.id === this.selectedUserId) || null
  );

  readonly selectedCompany = computed(() =>
    this.companies().find(c => c.id === this.selectedCompanyId) || null
  );

  readonly selectedBranch = computed(() =>
    this.branches().find(b => b.id === this.selectedBranchId) || null
  );

  // Computes the status of the currently selected scope in the form relative to the selected user's existing scopes
  readonly currentSelectionStatus = computed<'NEW' | 'ACTIVE' | 'REVOKED'>(() => {
    if (!this.selectedUserId || !this.selectedCompanyId) return 'NEW';

    const scopes = this.userScopes();
    const targetBranchId = this.isAllBranches ? '' : this.selectedBranchId;

    const match = scopes.find(s => {
      const compMatch = s.companyId === this.selectedCompanyId;
      const branchMatch = this.isAllBranches ? s.isAllBranches : (!s.isAllBranches && s.branchId === targetBranchId);
      return compMatch && branchMatch;
    });

    if (!match) return 'NEW';
    return match.status === 'Authorized' ? 'ACTIVE' : 'REVOKED';
  });

  ngOnInit(): void {
    this.route.queryParams.subscribe(params => {
      if (params['userId']) {
        this.selectedUserId = params['userId'];
      }
    });

    this.loadInitialData();
  }

  loadInitialData(): void {
    this.loading.set(true);
    this.errorMessage.set(null);

    this.securityService.getUsers().subscribe({
      next: (userList) => {
        this.users.set(userList);
        if (!this.selectedUserId && userList.length > 0) {
          this.selectedUserId = userList[0].id;
        }

        // Immediately load scopes for the selected user
        if (this.selectedUserId) {
          this.loadUserScopes(this.selectedUserId);
        }

        this.companyService.getCompanies().subscribe({
          next: (compList) => {
            this.companies.set(compList);
            if (compList.length > 0) {
              this.selectedCompanyId = compList[0].id;
              this.onCompanyChange(this.selectedCompanyId);
            }
            this.loading.set(false);
          },
          error: (err) => {
            this.errorMessage.set(err?.error?.message || err?.message || 'Failed to load companies.');
            this.loading.set(false);
          }
        });
      },
      error: (err) => {
        this.errorMessage.set(err?.error?.message || err?.message || 'Failed to load user accounts.');
        this.loading.set(false);
      }
    });
  }

  onUserChange(newUserId: string): void {
    this.selectedUserId = newUserId;
    this.errorMessage.set(null);
    this.successMessage.set(null);

    if (newUserId) {
      this.loadUserScopes(newUserId);
    } else {
      this.userScopes.set([]);
    }
  }

  loadUserScopes(userId: string): void {
    // Clear previous user's scope data immediately so assignments are never mixed
    this.userScopes.set([]);
    this.loadingUserScopes.set(true);

    this.securityService.getUserScopes(userId, true).subscribe({
      next: (scopes) => {
        // Double check in case user changed before response returned
        if (this.selectedUserId === userId) {
          this.userScopes.set(scopes);
        }
        this.loadingUserScopes.set(false);
      },
      error: (err) => {
        this.loadingUserScopes.set(false);
        this.errorMessage.set(err?.error?.message || err?.message || 'Failed to retrieve user scopes from server.');
      }
    });
  }

  onCompanyChange(companyId: string): void {
    this.selectedCompanyId = companyId;
    this.selectedBranchId = '';
    this.branches.set([]);

    if (!companyId) return;

    this.companyService.getBranches(companyId).subscribe({
      next: (branchList) => {
        this.branches.set(branchList);
        if (branchList.length > 0) {
          this.selectedBranchId = branchList[0].id;
        }
      },
      error: () => {}
    });
  }

  saveScope(): void {
    if (!this.selectedUserId) {
      this.errorMessage.set('Please select a user account.');
      return;
    }

    if (!this.selectedCompanyId) {
      this.errorMessage.set('Please select an operating company.');
      return;
    }

    if (!this.isAllBranches && !this.selectedBranchId) {
      this.errorMessage.set('Please select an operating branch or check "Authorize All Facilities in Company".');
      return;
    }

    if (this.currentSelectionStatus() === 'ACTIVE') {
      this.errorMessage.set('This user already has this exact operating scope actively assigned.');
      return;
    }

    this.saving.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);

    const user = this.selectedUser();
    const company = this.selectedCompany();
    const branch = this.selectedBranch();

    const dto: AssignScopeDto = {
      userId: this.selectedUserId,
      companyId: this.selectedCompanyId,
      branchId: this.isAllBranches ? 'all' : this.selectedBranchId,
      isAllBranches: this.isAllBranches,
      notes: this.scopeNotes
    };

    const userName = user?.fullName || user?.username || 'Authorized User';
    const userEmail = user?.email || '';
    const compName = company?.name || 'Assigned Company';
    const branchName = this.isAllBranches ? 'All Facilities' : (branch?.name || 'Selected Facility');

    const isReactivation = this.currentSelectionStatus() === 'REVOKED';

    this.securityService.assignUserScope(dto, userName, userEmail, compName, branchName).subscribe({
      next: () => {
        this.saving.set(false);
        const actionVerb = isReactivation ? 'reactivated' : 'granted';
        this.successMessage.set(`Operating scope successfully ${actionVerb} for ${userName} on ${compName} (${branchName}).`);
        this.scopeNotes = '';

        // Immediately refresh the selected user's scope assignments without navigating away
        this.loadUserScopes(this.selectedUserId);
      },
      error: (err) => {
        this.saving.set(false);
        const errorMsg = err?.error?.message || err?.message || 'Failed to assign operating scope. Check server logs.';
        this.errorMessage.set(errorMsg);
      }
    });
  }

  revokeScope(scope: UserScopeAssignment): void {
    if (scope.status === 'Revoked') return;

    this.revokingScopeId.set(scope.id);
    this.errorMessage.set(null);
    this.successMessage.set(null);

    this.securityService.revokeUserScope(scope.id).subscribe({
      next: (success) => {
        this.revokingScopeId.set(null);
        if (success) {
          this.successMessage.set(`Operating scope on ${scope.companyName} (${scope.branchName}) was revoked.`);
          // Reload fresh state from backend
          this.loadUserScopes(this.selectedUserId);
        } else {
          this.errorMessage.set('Unable to revoke scope assignment.');
        }
      },
      error: (err) => {
        this.revokingScopeId.set(null);
        this.errorMessage.set(err?.error?.message || err?.message || 'Server error while revoking scope.');
      }
    });
  }

  navigateBack(): void {
    this.router.navigate(['/tenant-admin/user-scope']);
  }
}
