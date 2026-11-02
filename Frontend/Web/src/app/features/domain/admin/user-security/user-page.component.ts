import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { TooltipModule } from 'primeng/tooltip';
import { MenuItem } from 'primeng/api';
import { AppPageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { AppStatusBadgeComponent } from '../../../../shared/components/status-badge/status-badge.component';
import { AppLoadingStateComponent } from '../../../../shared/components/loading-state/loading-state.component';
import { UserSecurityService } from '../../../../core/services/user-security.service';
import { CompanyService } from '../../../../core/services/company.service';
import {
  UserAccount,
  RoleDefinition,
  UserScopeAssignment,
  CreateUserDto,
  UpdateUserDto,
  PermissionItem
} from '../../../../core/models/security.models';
import { Company, Branch } from '../../../../core/models/scope.models';

export type UserPageMode = 'create' | 'edit' | 'view';

export interface PoDiagnosticItem {
  name: string;
  code: string;
  granted: boolean;
}

@Component({
  selector: 'app-user-page',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterModule,
    ButtonModule,
    InputTextModule,
    TooltipModule,
    AppPageHeaderComponent,
    AppStatusBadgeComponent,
    AppLoadingStateComponent
  ],
  templateUrl: './user-page.component.html',
  styleUrl: './user-page.component.scss'
})
export class UserPageComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  readonly securityService = inject(UserSecurityService);
  readonly companyService = inject(CompanyService);

  readonly mode = signal<UserPageMode>('create');
  readonly userId = signal<string | null>(null);
  readonly user = signal<UserAccount | null>(null);
  readonly userScopes = signal<UserScopeAssignment[]>([]);

  readonly loading = signal<boolean>(false);
  readonly saving = signal<boolean>(false);
  readonly errorMessage = signal<string | null>(null);
  readonly successMessage = signal<string | null>(null);

  // Form Fields
  firstName = '';
  lastName = '';
  email = '';
  username = '';
  phone = '';
  password = 'Password@123';
  selectedRoleId = '';
  accountStatus: 'Active' | 'Suspended' | 'Inactive' = 'Active';

  // Cascading Scope Fields (for create mode)
  selectedCompanyId = '';
  selectedBranchId = '';
  availableBranches = signal<Branch[]>([]);

  // Metadata catalogs
  roles = signal<RoleDefinition[]>([]);
  companies = signal<Company[]>([]);

  // Role & Permissions Inspection State
  assignedRole = signal<RoleDefinition | null>(null);
  rolePermissions = signal<PermissionItem[]>([]);
  editingRole = signal<boolean>(false);
  editRoleId = signal<string>('');
  savingRole = signal<boolean>(false);

  readonly otherPermissions = computed<PermissionItem[]>(() => {
    const poCodes = [
      'PURCHASE_ORDER.VIEW',
      'PURCHASE_ORDER.CREATE',
      'PURCHASE_ORDER.EDIT',
      'PURCHASE_ORDER.SUBMIT',
      'PO.VIEW',
      'PO.CREATE',
      'PO.EDIT',
      'PO.SUBMIT',
      'PO.APPROVE'
    ];
    return this.rolePermissions().filter(p => !poCodes.includes(p.code?.toUpperCase()));
  });

  readonly accessDiagnostics = computed(() => {
    const scopes = this.userScopes().filter(s => s.status === 'Authorized');
    const hasScope = scopes.length > 0 && scopes.some(s => !!s.companyId);
    const activeScope = scopes.find(s => !!s.companyId);
    const scopeDetail = activeScope
      ? `${activeScope.companyName}${activeScope.branchName && activeScope.branchName !== 'All Facilities' ? ' / ' + activeScope.branchName : ' (All Facilities)'}`
      : '';

    const permissions = this.rolePermissions().map(p => p.code?.toUpperCase() || '');

    const poViewGranted = permissions.includes('PURCHASE_ORDER.VIEW') || permissions.includes('PO.VIEW');
    const poCreateGranted = permissions.includes('PURCHASE_ORDER.CREATE') || permissions.includes('PO.CREATE');
    const poEditGranted = permissions.includes('PURCHASE_ORDER.EDIT') || permissions.includes('PO.EDIT');
    const poSubmitGranted =
      permissions.includes('PURCHASE_ORDER.SUBMIT') ||
      permissions.includes('PO.SUBMIT') ||
      permissions.includes('PO.APPROVE');

    const poAllGranted = poViewGranted && poCreateGranted && poEditGranted && poSubmitGranted;
    const poAnyGranted = poViewGranted || poCreateGranted || poEditGranted || poSubmitGranted;

    const poItems: PoDiagnosticItem[] = [
      { name: 'PO View', code: 'PURCHASE_ORDER.VIEW', granted: poViewGranted },
      { name: 'PO Create', code: 'PURCHASE_ORDER.CREATE', granted: poCreateGranted },
      { name: 'PO Edit', code: 'PURCHASE_ORDER.EDIT', granted: poEditGranted },
      { name: 'PO Submit', code: 'PURCHASE_ORDER.SUBMIT', granted: poSubmitGranted }
    ];

    return {
      scopeGranted: hasScope,
      scopeDetail,
      poAllGranted,
      poAnyGranted,
      poItems,
      isFullyReady: hasScope && poAllGranted,
      isScopeMissingOnly: !hasScope && poAllGranted
    };
  });

  readonly breadcrumbs = computed<MenuItem[]>(() => [
    { label: 'Workspace', routerLink: '/app/dashboard' },
    { label: 'Administration' },
    { label: 'User Accounts', routerLink: '/tenant-admin/users' },
    { label: this.mode() === 'create' ? 'New User' : (this.user()?.fullName || this.user()?.username || 'User Details') }
  ]);

  readonly pageTitle = computed(() => {
    if (this.mode() === 'create') return 'Create Tenant User Account';
    return `User Account: ${this.user()?.fullName || this.user()?.username || 'Details'}`;
  });

  readonly pageSubtitle = computed(() => {
    if (this.mode() === 'create') {
      return 'Provision an authorized user account, assign capability role, and configure primary organizational scope.';
    }
    return `Inspect user profile, manage assigned role, configure facility scopes, and govern authentication status.`;
  });

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    const isNew = this.route.snapshot.url.some(s => s.path === 'new') || !id;

    if (isNew) {
      this.mode.set('create');
      this.loadMetadata();
    } else {
      this.mode.set('edit');
      this.userId.set(id);
      this.loadMetadata();
      this.loadUserData(id!);
    }
  }

  loadMetadata(): void {
    this.securityService.getRoles().subscribe(r => {
      this.roles.set(r);
      if (this.mode() === 'create' && r.length > 0 && !this.selectedRoleId) {
        this.selectedRoleId = r[0].id;
      }
      if (this.selectedRoleId) {
        const found = r.find(x => x.id === this.selectedRoleId);
        if (found) this.assignedRole.set(found);
      }
    });

    this.companyService.getCompanies().subscribe(comps => {
      this.companies.set(comps);
      if (this.mode() === 'create' && comps.length > 0 && !this.selectedCompanyId) {
        this.selectedCompanyId = comps[0].id;
        this.onCompanyChange(comps[0].id);
      }
    });
  }

  loadUserData(id: string): void {
    this.loading.set(true);
    this.errorMessage.set(null);

    this.securityService.getUser(id).subscribe({
      next: userData => {
        if (!userData) {
          this.errorMessage.set('User account was not found or is inaccessible.');
          this.loading.set(false);
          return;
        }

        this.user.set(userData);
        this.firstName = userData.firstName || '';
        this.lastName = userData.lastName || '';
        this.email = userData.email || '';
        this.username = userData.username || '';
        this.phone = userData.phone || '';
        this.selectedRoleId = userData.roleId || '';
        this.editRoleId.set(userData.roleId || '');
        this.accountStatus = userData.accountStatus || 'Active';

        this.loadRolePermissions(userData.roleId);

        // Load scopes for this user
        this.securityService.getUserScopes(id).subscribe({
          next: scopes => {
            this.userScopes.set(scopes);
            this.loading.set(false);
          },
          error: () => {
            this.loading.set(false);
          }
        });
      },
      error: () => {
        this.errorMessage.set('Failed to load user account details.');
        this.loading.set(false);
      }
    });
  }

  loadRolePermissions(roleId: string): void {
    if (!roleId) {
      this.assignedRole.set(null);
      this.rolePermissions.set([]);
      return;
    }

    const role = this.roles().find(r => r.id === roleId);
    if (role) {
      this.assignedRole.set(role);
    } else {
      this.securityService.getRole(roleId).subscribe({
        next: r => this.assignedRole.set(r),
        error: () => {}
      });
    }

    this.securityService.getRolePermissions(roleId).subscribe({
      next: perms => {
        this.rolePermissions.set(perms);
      },
      error: () => {
        this.rolePermissions.set([]);
      }
    });
  }

  startEditingRole(): void {
    this.editRoleId.set(this.selectedRoleId);
    this.editingRole.set(true);
  }

  cancelEditingRole(): void {
    this.editingRole.set(false);
  }

  saveRoleQuick(): void {
    const newRoleId = this.editRoleId();
    if (!newRoleId || newRoleId === this.selectedRoleId) {
      this.editingRole.set(false);
      return;
    }

    this.savingRole.set(true);
    const updateDto: UpdateUserDto = {
      roleId: newRoleId,
      email: this.email.trim().toLowerCase(),
      username: this.username.trim() || undefined,
      isActive: this.accountStatus === 'Active'
    };

    this.securityService.updateUser(this.userId()!, updateDto).subscribe({
      next: () => {
        this.savingRole.set(false);
        this.editingRole.set(false);
        this.selectedRoleId = newRoleId;
        this.successMessage.set('Assigned role updated successfully.');
        this.loadRolePermissions(newRoleId);
        this.loadUserData(this.userId()!);
      },
      error: err => {
        this.savingRole.set(false);
        this.errorMessage.set(err?.error?.message || 'Failed to update assigned role.');
      }
    });
  }

  navigateToAssignScope(): void {
    this.router.navigate(['/tenant-admin/user-scope/new'], {
      queryParams: { userId: this.userId() }
    });
  }

  onCompanyChange(companyId: string): void {
    if (!companyId) {
      this.availableBranches.set([]);
      this.selectedBranchId = '';
      return;
    }
    this.companyService.getBranches(companyId).subscribe(brs => {
      this.availableBranches.set(brs);
      if (brs.length > 0) {
        this.selectedBranchId = brs[0].id;
      } else {
        this.selectedBranchId = '';
      }
    });
  }

  navigateBack(): void {
    this.router.navigate(['/tenant-admin/users']);
  }

  saveUser(): void {
    this.errorMessage.set(null);
    this.successMessage.set(null);

    if (this.mode() === 'create') {
      if (!this.firstName.trim() || !this.lastName.trim()) {
        this.errorMessage.set('First Name and Last Name are required.');
        return;
      }
      if (!this.email.trim() || !this.email.includes('@')) {
        this.errorMessage.set('A valid work email address is required.');
        return;
      }
      if (!this.selectedRoleId) {
        this.errorMessage.set('Please select an authorized role for this user.');
        return;
      }
      if (!this.selectedCompanyId) {
        this.errorMessage.set('Please select an authorized operating company.');
        return;
      }

      this.saving.set(true);
      const role = this.roles().find(r => r.id === this.selectedRoleId);
      const comp = this.companies().find(c => c.id === this.selectedCompanyId);
      const br = this.availableBranches().find(b => b.id === this.selectedBranchId);

      const dto: CreateUserDto = {
        firstName: this.firstName.trim(),
        lastName: this.lastName.trim(),
        email: this.email.trim().toLowerCase(),
        username: this.username.trim() || this.email.split('@')[0],
        phone: this.phone.trim(),
        accountStatus: this.accountStatus,
        roleId: this.selectedRoleId,
        companyId: this.selectedCompanyId,
        branchId: this.selectedBranchId
      };

      this.securityService.createUser(
        dto,
        role?.name || 'Authorized User',
        comp?.name || 'Primary Company',
        br?.name || 'Primary Facility'
      ).subscribe({
        next: () => {
          this.saving.set(false);
          this.successMessage.set('User account created and primary facility scope provisioned successfully.');
          setTimeout(() => this.router.navigate(['/tenant-admin/users']), 1000);
        },
        error: err => {
          this.saving.set(false);
          this.errorMessage.set(err?.error?.message || err?.error?.errors?.[0] || 'Failed to create user account.');
        }
      });
    } else {
      // Edit mode: update user profile & role
      if (!this.email.trim() || !this.email.includes('@')) {
        this.errorMessage.set('A valid email address is required.');
        return;
      }

      this.saving.set(true);
      const updateDto: UpdateUserDto = {
        username: this.username.trim() || undefined,
        email: this.email.trim().toLowerCase(),
        roleId: this.selectedRoleId || undefined,
        isActive: this.accountStatus === 'Active'
      };

      this.securityService.updateUser(this.userId()!, updateDto).subscribe({
        next: () => {
          this.saving.set(false);
          this.successMessage.set('User account details and role assignment updated successfully.');
          this.loadUserData(this.userId()!);
        },
        error: err => {
          this.saving.set(false);
          this.errorMessage.set(err?.error?.message || err?.error?.errors?.[0] || 'Failed to update user account.');
        }
      });
    }
  }

  toggleAccountStatus(): void {
    if (!this.userId()) return;
    this.securityService.toggleUserStatus(this.userId()!).subscribe({
      next: () => {
        const nextStatus = this.accountStatus === 'Active' ? 'Suspended' : 'Active';
        this.accountStatus = nextStatus;
        this.successMessage.set(`User account authentication access has been ${nextStatus === 'Active' ? 'activated' : 'suspended'}.`);
        this.loadUserData(this.userId()!);
      },
      error: () => {
        this.errorMessage.set('Failed to update user account authentication status.');
      }
    });
  }

  revokeScope(scope: UserScopeAssignment): void {
    if (scope.isPrimary) return;
    this.securityService.revokeUserScope(scope.id).subscribe({
      next: ok => {
        if (ok) {
          this.successMessage.set(`Facility scope for ${scope.branchName || scope.companyName} has been revoked.`);
          this.loadUserData(this.userId()!);
        } else {
          this.errorMessage.set('Failed to revoke facility scope.');
        }
      },
      error: () => {
        this.errorMessage.set('An error occurred while revoking facility scope.');
      }
    });
  }

  navigateToAddScope(): void {
    this.router.navigate(['/tenant-admin/user-scope/new'], {
      queryParams: { userId: this.userId() }
    });
  }
}
