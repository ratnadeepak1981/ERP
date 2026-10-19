import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { TooltipModule } from 'primeng/tooltip';
import { MenuItem, SharedModule } from 'primeng/api';
import { AppPageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { AppDataTableComponent } from '../../../../shared/components/data-table/data-table.component';
import { DataTableColumn } from '../../../../shared/models/data-table-config.model';
import { AppStatusBadgeComponent } from '../../../../shared/components/status-badge/status-badge.component';
import { UserSecurityService } from '../../../../core/services/user-security.service';
import { CompanyService } from '../../../../core/services/company.service';
import { WorkspaceScopeService } from '../../../../core/services/workspace-scope.service';
import {
  UserAccount,
  CreateUserDto,
  RoleDefinition,
  CreateRoleDto,
  PermissionItem,
  PermissionTemplate,
  UserScopeAssignment,
  AssignScopeDto
} from '../../../../core/models/security.models';
import { Company, Branch } from '../../../../core/models/scope.models';

export type SecurityNavigationTab = 'users' | 'roles' | 'permissions' | 'user-scope';

@Component({
  selector: 'app-user-security',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ButtonModule,
    DialogModule,
    InputTextModule,
    TooltipModule,
    SharedModule,
    AppPageHeaderComponent,
    AppDataTableComponent,
    AppStatusBadgeComponent
  ],
  templateUrl: './user-security.component.html',
  styleUrl: './user-security.component.scss'
})
export class UserSecurityComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  readonly securityService = inject(UserSecurityService);
  readonly companyService = inject(CompanyService);
  readonly scopeService = inject(WorkspaceScopeService);

  readonly breadcrumbs: MenuItem[] = [
    { label: 'Workspace', routerLink: '/app/dashboard' },
    { label: 'Administration' },
    { label: 'Users, Roles & Permissions' }
  ];

  // --- Active Tab Navigation ---
  readonly activeTab = signal<SecurityNavigationTab>('users');

  // --- Data State ---
  readonly users = signal<UserAccount[]>([]);
  readonly roles = signal<RoleDefinition[]>([]);
  readonly userScopes = signal<UserScopeAssignment[]>([]);
  readonly companies = signal<Company[]>([]);
  readonly branches = signal<Branch[]>([]);

  // Selected entities for master-detail views
  readonly selectedRole = signal<RoleDefinition | null>(null);
  readonly selectedScopeUserFilter = signal<string>('all');

  // Permissions Catalog & Templates
  readonly permissions = signal<PermissionItem[]>(this.securityService.systemPermissions);
  readonly templates = signal<PermissionTemplate[]>(this.securityService.permissionTemplates);

  // Search & Filtering
  readonly userSearchQuery = signal<string>('');
  readonly roleSearchQuery = signal<string>('');
  readonly permissionSearchQuery = signal<string>('');
  readonly scopeSearchQuery = signal<string>('');

  // --- Modals State ---
  readonly showCreateUserModal = signal<boolean>(false);
  readonly showCreateRoleModal = signal<boolean>(false);
  readonly showAssignScopeModal = signal<boolean>(false);
  readonly formError = signal<string | null>(null);

  // --- Form State: Create User ---
  newUserFirstName = '';
  newUserLastName = '';
  newUserEmail = '';
  newUserUsername = '';
  newUserPhone = '';
  newUserAccountStatus: 'Active' | 'Suspended' | 'Inactive' = 'Active';
  newUserRoleId = '';
  newUserCompanyId = '';
  newUserBranchId = '';
  newUserAvailableBranches = signal<Branch[]>([]);

  // --- Form State: Create Role ---
  newRoleName = '';
  newRoleDescription = '';
  newRoleAssignmentMode: 'simple' | 'advanced' = 'simple';
  newRoleTemplateId = '';
  
  // Advanced Dual-List / Transfer Picklist State
  availablePermissionsList = signal<PermissionItem[]>([]);
  assignedPermissionsList = signal<PermissionItem[]>([]);
  selectedAvailableCodes = signal<Set<string>>(new Set());
  selectedAssignedCodes = signal<Set<string>>(new Set());
  availableSearchQuery = signal<string>('');
  assignedSearchQuery = signal<string>('');

  // --- Form State: Assign Additional Scope ---
  assignScopeUserId = '';
  assignScopeCompanyId = '';
  assignScopeBranchId = '';
  assignScopeIsAllBranches = false;
  assignScopeNotes = '';
  assignScopeAvailableBranches = signal<Branch[]>([]);

  // --- Data Table Columns ---
  readonly userColumns: DataTableColumn[] = [
    { field: 'fullName', header: 'User Name', sortable: true, filterable: true },
    { field: 'email', header: 'Email Address', sortable: true, filterable: true },
    { field: 'roleName', header: 'Assigned Role', sortable: true, filterable: true },
    { field: 'primaryScope', header: 'Primary Facility Scope', sortable: true },
    { field: 'accountStatus', header: 'Account Status', sortable: true, filterable: true }
  ];

  readonly scopeColumns: DataTableColumn[] = [
    { field: 'userName', header: 'Authorized User', sortable: true, filterable: true },
    { field: 'companyName', header: 'Operating Company', sortable: true, filterable: true },
    { field: 'branchName', header: 'Authorized Facility / Branch', sortable: true, filterable: true },
    { field: 'scopeType', header: 'Scope Jurisdiction', sortable: true },
    { field: 'status', header: 'Authorization Status', sortable: true },
    { field: 'grantedAt', header: 'Granted Date', sortable: true }
  ];

  ngOnInit(): void {
    // Check route parameters or default tab
    this.route.data.subscribe(data => {
      if (data && data['defaultTab']) {
        this.activeTab.set(data['defaultTab']);
      }
    });

    this.route.queryParams.subscribe(params => {
      if (params['tab']) {
        this.activeTab.set(params['tab'] as SecurityNavigationTab);
      }
      if (params['userId']) {
        this.selectedScopeUserFilter.set(params['userId']);
      }
    });

    this.loadAllData();
  }

  loadAllData(): void {
    // 1. Load Companies & Branches
    this.companyService.getCompanies().subscribe(comps => {
      this.companies.set(comps);
      if (comps.length > 0) {
        this.companyService.getBranches(comps[0].id).subscribe(brs => {
          this.branches.set(brs);
        });
      }
    });

    // 2. Load Roles
    this.securityService.getRoles().subscribe(roles => {
      this.roles.set(roles);
      if (roles.length > 0 && !this.selectedRole()) {
        this.selectedRole.set(roles[0]);
      }
    });

    // 3. Load Users
    this.securityService.getUsers().subscribe(users => {
      this.users.set(users);
    });

    // 4. Load User Scopes
    this.securityService.getUserScopes().subscribe(scopes => {
      this.userScopes.set(scopes);
    });
  }

  // --- Tab Switching ---
  switchTab(tab: SecurityNavigationTab): void {
    this.activeTab.set(tab);
    this.formError.set(null);
  }

  // --- Users Actions ---
  openCreateUserModal(): void {
    this.formError.set(null);
    this.newUserFirstName = '';
    this.newUserLastName = '';
    this.newUserEmail = '';
    this.newUserUsername = '';
    this.newUserPhone = '';
    this.newUserAccountStatus = 'Active';

    // Default to first role and company
    const currentRoles = this.roles();
    this.newUserRoleId = currentRoles.length > 0 ? currentRoles[0].id : '';

    const currentCompanies = this.companies();
    if (currentCompanies.length > 0) {
      this.newUserCompanyId = currentCompanies[0].id;
      this.onUserCompanyChange(this.newUserCompanyId);
    } else {
      this.newUserCompanyId = '';
      this.newUserAvailableBranches.set([]);
      this.newUserBranchId = '';
    }

    this.showCreateUserModal.set(true);
  }

  onUserCompanyChange(companyId: string): void {
    if (!companyId) {
      this.newUserAvailableBranches.set([]);
      this.newUserBranchId = '';
      return;
    }
    this.companyService.getBranches(companyId).subscribe(brs => {
      this.newUserAvailableBranches.set(brs);
      if (brs.length > 0) {
        this.newUserBranchId = brs[0].id;
      } else {
        this.newUserBranchId = '';
      }
    });
  }

  saveUser(): void {
    this.formError.set(null);
    if (!this.newUserFirstName.trim() || !this.newUserLastName.trim()) {
      this.formError.set('First Name and Last Name are required.');
      return;
    }
    if (!this.newUserEmail.trim() || !this.newUserEmail.includes('@')) {
      this.formError.set('A valid work email address is required.');
      return;
    }
    if (!this.newUserRoleId) {
      this.formError.set('Please select an available role for this user.');
      return;
    }
    if (!this.newUserCompanyId) {
      this.formError.set('Please select an authorized operating company.');
      return;
    }
    if (!this.newUserBranchId) {
      this.formError.set('Please select an authorized operating branch.');
      return;
    }

    const role = this.roles().find(r => r.id === this.newUserRoleId);
    const company = this.companies().find(c => c.id === this.newUserCompanyId);
    const branch = this.newUserAvailableBranches().find(b => b.id === this.newUserBranchId);

    const dto: CreateUserDto = {
      firstName: this.newUserFirstName,
      lastName: this.newUserLastName,
      email: this.newUserEmail,
      username: this.newUserUsername || this.newUserEmail.split('@')[0],
      phone: this.newUserPhone,
      accountStatus: this.newUserAccountStatus,
      roleId: this.newUserRoleId,
      companyId: this.newUserCompanyId,
      branchId: this.newUserBranchId
    };

    this.securityService.createUser(
      dto,
      role?.name || 'Authorized User',
      company?.name || 'Primary Company',
      branch?.name || 'Primary Branch'
    ).subscribe(newUser => {
      this.showCreateUserModal.set(false);
      this.loadAllData();
    });
  }

  toggleUserStatus(user: UserAccount): void {
    this.securityService.toggleUserStatus(user.id).subscribe(() => {
      this.loadAllData();
    });
  }

  // --- Navigate to User Scope for a Specific User ---
  manageUserScope(user: UserAccount): void {
    this.selectedScopeUserFilter.set(user.id);
    this.activeTab.set('user-scope');
  }

  // --- Roles & Dual-List Advanced Mode ---
  openCreateRoleModal(): void {
    this.formError.set(null);
    this.newRoleName = '';
    this.newRoleDescription = '';
    this.newRoleAssignmentMode = 'simple';
    this.newRoleTemplateId = '';

    // Initialize Dual-List for Advanced mode
    this.initDualList();

    this.showCreateRoleModal.set(true);
  }

  initDualList(): void {
    const allPerms = [...this.securityService.systemPermissions];
    this.availablePermissionsList.set(allPerms);
    this.assignedPermissionsList.set([]);
    this.selectedAvailableCodes.set(new Set());
    this.selectedAssignedCodes.set(new Set());
    this.availableSearchQuery.set('');
    this.assignedSearchQuery.set('');
  }

  onRoleAssignmentModeChange(mode: 'simple' | 'advanced'): void {
    this.newRoleAssignmentMode = mode;
    if (mode === 'advanced') {
      this.initDualList();
    }
  }

  // Toggle selection in Available Box
  toggleSelectAvailable(code: string): void {
    const set = new Set(this.selectedAvailableCodes());
    if (set.has(code)) {
      set.delete(code);
    } else {
      set.add(code);
    }
    this.selectedAvailableCodes.set(set);
  }

  // Toggle selection in Assigned Box
  toggleSelectAssigned(code: string): void {
    const set = new Set(this.selectedAssignedCodes());
    if (set.has(code)) {
      set.delete(code);
    } else {
      set.add(code);
    }
    this.selectedAssignedCodes.set(set);
  }

  // Move selected from Available -> Assigned (>)
  moveSelectedToAssigned(): void {
    const toMove = this.selectedAvailableCodes();
    if (toMove.size === 0) return;

    const currentAvail = this.availablePermissionsList();
    const currentAssigned = this.assignedPermissionsList();

    const movingItems = currentAvail.filter(p => toMove.has(p.code));
    const remainingAvail = currentAvail.filter(p => !toMove.has(p.code));

    this.availablePermissionsList.set(remainingAvail);
    this.assignedPermissionsList.set([...currentAssigned, ...movingItems]);
    this.selectedAvailableCodes.set(new Set());
  }

  // Move ALL from Available -> Assigned (>>)
  moveAllToAssigned(): void {
    const currentAvail = this.availablePermissionsList();
    if (currentAvail.length === 0) return;

    const currentAssigned = this.assignedPermissionsList();
    this.assignedPermissionsList.set([...currentAssigned, ...currentAvail]);
    this.availablePermissionsList.set([]);
    this.selectedAvailableCodes.set(new Set());
  }

  // Move selected from Assigned -> Available (<)
  moveSelectedToAvailable(): void {
    const toMove = this.selectedAssignedCodes();
    if (toMove.size === 0) return;

    const currentAssigned = this.assignedPermissionsList();
    const currentAvail = this.availablePermissionsList();

    const movingItems = currentAssigned.filter(p => toMove.has(p.code));
    const remainingAssigned = currentAssigned.filter(p => !toMove.has(p.code));

    this.assignedPermissionsList.set(remainingAssigned);
    this.availablePermissionsList.set([...currentAvail, ...movingItems]);
    this.selectedAssignedCodes.set(new Set());
  }

  // Move ALL from Assigned -> Available (<<)
  moveAllToAvailable(): void {
    const currentAssigned = this.assignedPermissionsList();
    if (currentAssigned.length === 0) return;

    const currentAvail = this.availablePermissionsList();
    this.availablePermissionsList.set([...currentAvail, ...currentAssigned]);
    this.assignedPermissionsList.set([]);
    this.selectedAssignedCodes.set(new Set());
  }

  // Filtered lists for dual-list search
  filteredAvailablePermissions = computed(() => {
    const q = this.availableSearchQuery().toLowerCase().trim();
    const list = this.availablePermissionsList();
    if (!q) return list;
    return list.filter(p => p.name.toLowerCase().includes(q) || p.code.toLowerCase().includes(q) || p.category.toLowerCase().includes(q));
  });

  filteredAssignedPermissions = computed(() => {
    const q = this.assignedSearchQuery().toLowerCase().trim();
    const list = this.assignedPermissionsList();
    if (!q) return list;
    return list.filter(p => p.name.toLowerCase().includes(q) || p.code.toLowerCase().includes(q) || p.category.toLowerCase().includes(q));
  });

  saveRole(): void {
    this.formError.set(null);
    if (!this.newRoleName.trim()) {
      this.formError.set('Role Name is required.');
      return;
    }

    let permissionCodes: string[] = [];
    if (this.newRoleAssignmentMode === 'simple') {
      const tmpl = this.templates().find(t => t.id === this.newRoleTemplateId);
      if (!tmpl) {
        this.formError.set('Please select a permission template.');
        return;
      }
      permissionCodes = tmpl.permissionCodes;
    } else {
      permissionCodes = this.assignedPermissionsList().map(p => p.code);
      if (permissionCodes.length === 0) {
        this.formError.set('Advanced Mode: Please assign at least one permission to this role.');
        return;
      }
    }

    const dto: CreateRoleDto = {
      name: this.newRoleName,
      description: this.newRoleDescription,
      assignmentMode: this.newRoleAssignmentMode,
      templateId: this.newRoleAssignmentMode === 'simple' ? this.newRoleTemplateId : undefined,
      permissions: permissionCodes
    };

    this.securityService.createRole(dto).subscribe(createdRole => {
      this.showCreateRoleModal.set(false);
      this.loadAllData();
      this.selectedRole.set(createdRole);
    });
  }

  selectRole(role: RoleDefinition): void {
    this.selectedRole.set(role);
  }

  // --- User Scope View & Modal ---
  openAssignScopeModal(forUserId?: string): void {
    this.formError.set(null);
    const usersList = this.users();
    this.assignScopeUserId = forUserId || (usersList.length > 0 ? usersList[0].id : '');
    this.assignScopeIsAllBranches = false;
    this.assignScopeNotes = '';

    const currentCompanies = this.companies();
    if (currentCompanies.length > 0) {
      this.assignScopeCompanyId = currentCompanies[0].id;
      this.onScopeCompanyChange(this.assignScopeCompanyId);
    } else {
      this.assignScopeCompanyId = '';
      this.assignScopeAvailableBranches.set([]);
      this.assignScopeBranchId = '';
    }

    this.showAssignScopeModal.set(true);
  }

  onScopeCompanyChange(companyId: string): void {
    if (!companyId) {
      this.assignScopeAvailableBranches.set([]);
      this.assignScopeBranchId = '';
      return;
    }
    this.companyService.getBranches(companyId).subscribe(brs => {
      this.assignScopeAvailableBranches.set(brs);
      if (brs.length > 0) {
        this.assignScopeBranchId = brs[0].id;
      } else {
        this.assignScopeBranchId = '';
      }
    });
  }

  saveScopeAssignment(): void {
    this.formError.set(null);
    if (!this.assignScopeUserId) {
      this.formError.set('Please select an authorized user.');
      return;
    }
    if (!this.assignScopeCompanyId) {
      this.formError.set('Please select an operating legal entity.');
      return;
    }
    if (!this.assignScopeIsAllBranches && !this.assignScopeBranchId) {
      this.formError.set('Please select an operating branch facility.');
      return;
    }

    const user = this.users().find(u => u.id === this.assignScopeUserId);
    const company = this.companies().find(c => c.id === this.assignScopeCompanyId);
    const branch = this.assignScopeAvailableBranches().find(b => b.id === this.assignScopeBranchId);

    const dto: AssignScopeDto = {
      userId: this.assignScopeUserId,
      companyId: this.assignScopeCompanyId,
      branchId: this.assignScopeIsAllBranches ? 'all' : (this.assignScopeBranchId || 'all'),
      isAllBranches: this.assignScopeIsAllBranches,
      notes: this.assignScopeNotes
    };

    this.securityService.assignUserScope(
      dto,
      user?.fullName || 'User',
      user?.email || '',
      company?.name || 'Company',
      this.assignScopeIsAllBranches ? 'All Operating Branches' : (branch?.name || 'Branch')
    ).subscribe(() => {
      this.showAssignScopeModal.set(false);
      this.loadAllData();
    });
  }

  revokeScope(scope: UserScopeAssignment): void {
    if (scope.isPrimary) return;
    this.securityService.revokeUserScope(scope.id).subscribe(() => {
      this.loadAllData();
    });
  }

  // --- Computed Display Data ---
  displayedUsers = computed(() => {
    const q = this.userSearchQuery().toLowerCase().trim();
    const list = this.users();
    if (!q) return list;
    return list.filter(u =>
      u.fullName.toLowerCase().includes(q) ||
      u.email.toLowerCase().includes(q) ||
      u.username.toLowerCase().includes(q) ||
      u.roleName.toLowerCase().includes(q)
    );
  });

  displayedUserScopes = computed(() => {
    const list = this.userScopes();
    const userFilter = this.selectedScopeUserFilter();
    const q = this.scopeSearchQuery().toLowerCase().trim();

    let filtered = list;
    if (userFilter && userFilter !== 'all') {
      filtered = filtered.filter(s => s.userId === userFilter);
    }
    if (q) {
      filtered = filtered.filter(s =>
        s.userName.toLowerCase().includes(q) ||
        s.userEmail.toLowerCase().includes(q) ||
        s.companyName.toLowerCase().includes(q) ||
        s.branchName.toLowerCase().includes(q)
      );
    }
    return filtered;
  });

  // Filtered Roles
  filteredRoles = computed(() => {
    const q = this.roleSearchQuery().toLowerCase().trim();
    const list = this.roles();
    if (!q) return list;
    return list.filter(r => r.name.toLowerCase().includes(q) || r.description.toLowerCase().includes(q));
  });

  // Permissions in selected role
  selectedRolePermissions = computed(() => {
    const role = this.selectedRole();
    if (!role) return [];
    return this.securityService.systemPermissions.filter(p => role.permissions.includes(p.code));
  });

  // Permission categories grouped
  groupedPermissions = computed(() => {
    const q = this.permissionSearchQuery().toLowerCase().trim();
    const all = this.securityService.systemPermissions;
    const filtered = q ? all.filter(p => p.name.toLowerCase().includes(q) || p.code.toLowerCase().includes(q) || p.description.toLowerCase().includes(q)) : all;

    const map = new Map<string, PermissionItem[]>();
    for (const p of filtered) {
      if (!map.has(p.category)) {
        map.set(p.category, []);
      }
      map.get(p.category)!.push(p);
    }
    return Array.from(map.entries()).map(([category, items]) => ({ category, items }));
  });

  // Selected Template in Simple Mode
  currentTemplate = computed(() => {
    return this.templates().find(t => t.id === this.newRoleTemplateId);
  });
}
