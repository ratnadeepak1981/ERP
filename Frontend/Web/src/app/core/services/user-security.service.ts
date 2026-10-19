import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of, map, catchError } from 'rxjs';
import { AuthService } from '../auth/auth.service';
import { CompanyService } from './company.service';
import { buildApiUrl } from '../utils/api-url.util';
import { ApiResponse } from '../models/auth.models';
import {
  UserAccount,
  CreateUserDto,
  RoleDefinition,
  CreateRoleDto,
  PermissionItem,
  PermissionTemplate,
  UserScopeAssignment,
  AssignScopeDto
} from '../models/security.models';

const USERS_STORAGE_KEY_PREFIX = 'apex_erp_tenant_users_';
const ROLES_STORAGE_KEY_PREFIX = 'apex_erp_tenant_roles_';
const SCOPES_STORAGE_KEY_PREFIX = 'apex_erp_tenant_user_scopes_';

@Injectable({
  providedIn: 'root'
})
export class UserSecurityService {
  private readonly http = inject(HttpClient);
  private readonly authService = inject(AuthService, { optional: true });
  private readonly companyService = inject(CompanyService);

  private getTenantStorageKey(): string {
    const user = this.authService?.currentUser?.();
    return user?.tenantId || user?.username || 'default_tenant';
  }

  // --- Granular Permissions Catalog ---
  readonly systemPermissions: PermissionItem[] = [
    // Scope & Entities
    { id: 'p-scope-switch', category: 'Scope & Organization', code: 'scope.switch', name: 'Switch Working Scope', description: 'Switch between assigned companies and operating branches' },
    { id: 'p-company-manage', category: 'Scope & Organization', code: 'company.manage', name: 'Manage Legal Companies', description: 'Create and configure tenant operating legal entities' },
    { id: 'p-branch-manage', category: 'Scope & Organization', code: 'branch.manage', name: 'Manage Operating Branches', description: 'Add and configure branch plants and facilities' },
    // Security & User Administration
    { id: 'p-users-view', category: 'Security Administration', code: 'users.view', name: 'View User Accounts', description: 'Inspect system user accounts, roles and status' },
    { id: 'p-users-create', category: 'Security Administration', code: 'users.create', name: 'Create User Accounts', description: 'Provision new tenant user credentials' },
    { id: 'p-users-edit', category: 'Security Administration', code: 'users.edit', name: 'Edit User Accounts', description: 'Update profile, role assignments and status' },
    { id: 'p-users-deact', category: 'Security Administration', code: 'users.deactivate', name: 'Activate/Deactivate Users', description: 'Suspend or activate user authentication access' },
    { id: 'p-roles-manage', category: 'Security Administration', code: 'roles.manage', name: 'Manage Roles & Policies', description: 'Create and configure custom permission roles' },
    { id: 'p-scope-assign', category: 'Security Administration', code: 'scope.assign', name: 'Assign User Company/Branch Scope', description: 'Grant additional branch and company authorizations' },
    // Master Data
    { id: 'p-prod-view', category: 'Master Data', code: 'products.view', name: 'View Product Masters', description: 'Browse item catalog and pricing' },
    { id: 'p-prod-edit', category: 'Master Data', code: 'products.edit', name: 'Manage Product Masters', description: 'Create and update products and specifications' },
    { id: 'p-supp-view', category: 'Master Data', code: 'suppliers.view', name: 'View Supplier Directory', description: 'Inspect approved vendor listings' },
    { id: 'p-supp-edit', category: 'Master Data', code: 'suppliers.edit', name: 'Manage Supplier Masters', description: 'Register and update supplier agreements' },
    { id: 'p-wh-view', category: 'Master Data', code: 'warehouses.view', name: 'View Warehouse Locations', description: 'Inspect storage bins and plant facilities' },
    { id: 'p-wh-manage', category: 'Master Data', code: 'warehouses.manage', name: 'Manage Warehouses', description: 'Add and maintain warehouses and storage zones' },
    // Procurement
    { id: 'p-po-view', category: 'Procurement', code: 'po.view', name: 'View Purchase Orders', description: 'Access procurement orders and requisitions' },
    { id: 'p-po-create', category: 'Procurement', code: 'po.create', name: 'Create Purchase Orders', description: 'Issue purchase orders to approved suppliers' },
    { id: 'p-po-approve', category: 'Procurement', code: 'po.approve', name: 'Approve Purchase Orders', description: 'Financial authorization for procurement commitments' },
    { id: 'p-grn-receive', category: 'Procurement', code: 'grn.receive', name: 'Process Goods Receipt (GRN)', description: 'Receive shipments and record inbound receipts' },
    // Sales & Customers
    { id: 'p-so-view', category: 'Sales & Orders', code: 'so.view', name: 'View Sales Orders', description: 'Browse customer orders and fulfillment status' },
    { id: 'p-so-create', category: 'Sales & Orders', code: 'so.create', name: 'Create Sales Orders', description: 'Enter sales orders and quotes' },
    { id: 'p-so-approve', category: 'Sales & Orders', code: 'so.approve', name: 'Approve Sales Orders', description: 'Commercial credit and order approval' },
    { id: 'p-cust-manage', category: 'Sales & Orders', code: 'customers.manage', name: 'Manage Customer Accounts', description: 'Maintain client directory and credit terms' },
    // Inventory
    { id: 'p-stock-view', category: 'Inventory', code: 'stock.view', name: 'View Stock Balances', description: 'Real-time on-hand stock and valuations' },
    { id: 'p-stock-adj', category: 'Inventory', code: 'stock.adjust', name: 'Perform Stock Adjustments', description: 'Reconcile physical counts and variances' },
    { id: 'p-stock-trans', category: 'Inventory', code: 'stock.transfer', name: 'Inter-Branch Stock Transfers', description: 'Initiate and receive transfers between facilities' },
    // Financials & Audit
    { id: 'p-fin-view', category: 'Financials & Reports', code: 'finance.view', name: 'View Financial Metrics', description: 'Operational revenue, margins and cost of goods' },
    { id: 'p-rep-export', category: 'Financials & Reports', code: 'reports.export', name: 'Export Audit Reports', description: 'Download compliance ledgers and transactional summaries' }
  ];

  // --- Predefined Permission Templates ---
  readonly permissionTemplates: PermissionTemplate[] = [
    {
      id: 'tmpl-admin',
      name: 'Tenant Administrator',
      description: 'Unrestricted administration, role assignment, and organization configuration.',
      permissionCodes: this.systemPermissions.map(p => p.code)
    },
    {
      id: 'tmpl-ops-mgr',
      name: 'Branch Operations Manager',
      description: 'Full operational control over procurement, inventory, sales, and facility masters.',
      permissionCodes: [
        'scope.switch', 'products.view', 'products.edit', 'suppliers.view', 'warehouses.view',
        'po.view', 'po.create', 'po.approve', 'grn.receive',
        'so.view', 'so.create', 'so.approve', 'customers.manage',
        'stock.view', 'stock.adjust', 'stock.transfer', 'finance.view', 'reports.export'
      ]
    },
    {
      id: 'tmpl-proc-officer',
      name: 'Procurement Officer',
      description: 'Issue purchase orders, manage approved vendors, and process inbound GRN receipts.',
      permissionCodes: [
        'scope.switch', 'products.view', 'suppliers.view', 'suppliers.edit',
        'po.view', 'po.create', 'grn.receive', 'stock.view', 'reports.export'
      ]
    },
    {
      id: 'tmpl-wh-super',
      name: 'Warehouse Supervisor',
      description: 'Manage warehouse facilities, execute stock transfers, adjustments, and receipts.',
      permissionCodes: [
        'scope.switch', 'products.view', 'warehouses.view', 'warehouses.manage',
        'grn.receive', 'stock.view', 'stock.adjust', 'stock.transfer'
      ]
    },
    {
      id: 'tmpl-sales-rep',
      name: 'Sales Representative',
      description: 'Draft and track customer sales orders, product catalog, and customer records.',
      permissionCodes: [
        'scope.switch', 'products.view', 'so.view', 'so.create', 'customers.manage', 'stock.view'
      ]
    },
    {
      id: 'tmpl-viewer',
      name: 'Read-Only Auditor',
      description: 'Inspect operational records, metrics, and export audit reports without editing rights.',
      permissionCodes: [
        'scope.switch', 'products.view', 'suppliers.view', 'warehouses.view',
        'po.view', 'so.view', 'stock.view', 'finance.view', 'reports.export'
      ]
    }
  ];

  // --- Roles Management ---
  getRoles(): Observable<RoleDefinition[]> {
    this.http.get<ApiResponse<any[]>>(buildApiUrl('/Role')).subscribe({
      next: res => {
        const list = res?.data;
        if (Array.isArray(list) && list.length > 0) {
          const apiRoles: RoleDefinition[] = list.map(r => ({
            id: r.id || r.roleId,
            tenantId: r.tenantId || this.getTenantStorageKey(),
            name: r.name,
            description: r.description || '',
            assignmentMode: 'simple',
            permissions: r.permissions || [],
            usersCount: r.usersCount || 0,
            isSystem: !!r.isSystem,
            createdAt: r.createdAt || new Date().toISOString()
          }));
          this.mergeWithLocalRoles(apiRoles);
        }
      },
      error: () => {}
    });

    return of(this.getLocalRoles());
  }

  private getLocalRoles(): RoleDefinition[] {
    const key = ROLES_STORAGE_KEY_PREFIX + this.getTenantStorageKey();
    const stored = localStorage.getItem(key);
    if (stored) {
      try {
        const roles = JSON.parse(stored);
        if (Array.isArray(roles) && roles.length > 0) {
          return roles;
        }
      } catch {
        // Fall through
      }
    }
    const initialRoles = this.buildInitialRoles();
    this.saveRolesLocally(initialRoles);
    return initialRoles;
  }

  private mergeWithLocalRoles(apiRoles: RoleDefinition[]): RoleDefinition[] {
    const local = this.getLocalRoles();
    const map = new Map<string, RoleDefinition>();
    for (const r of local) {
      map.set(r.name.toLowerCase(), r);
    }
    for (const r of apiRoles) {
      map.set(r.name.toLowerCase(), {
        ...(map.get(r.name.toLowerCase()) || {}),
        ...r
      });
    }
    const merged = Array.from(map.values());
    this.saveRolesLocally(merged);
    return merged;
  }

  private buildInitialRoles(): RoleDefinition[] {
    return [
      {
        id: 'role-admin',
        tenantId: this.getTenantStorageKey(),
        name: 'Tenant Administrator',
        description: 'Complete administrative access to security policies, scope configuration, and enterprise operations.',
        assignmentMode: 'simple',
        templateId: 'tmpl-admin',
        permissions: this.systemPermissions.map(p => p.code),
        usersCount: 1,
        isSystem: true,
        createdAt: '2026-01-15T08:00:00Z'
      },
      {
        id: 'role-ops-mgr',
        tenantId: this.getTenantStorageKey(),
        name: 'Branch Operations Manager',
        description: 'Oversees day-to-day procurement, sales order approvals, and warehouse management.',
        assignmentMode: 'simple',
        templateId: 'tmpl-ops-mgr',
        permissions: this.permissionTemplates.find(t => t.id === 'tmpl-ops-mgr')?.permissionCodes || [],
        usersCount: 1,
        isSystem: true,
        createdAt: '2026-01-16T09:30:00Z'
      },
      {
        id: 'role-procurement',
        tenantId: this.getTenantStorageKey(),
        name: 'Procurement Officer',
        description: 'Handles purchase orders, supplier interactions, and receiving validation.',
        assignmentMode: 'simple',
        templateId: 'tmpl-proc-officer',
        permissions: this.permissionTemplates.find(t => t.id === 'tmpl-proc-officer')?.permissionCodes || [],
        usersCount: 1,
        isSystem: false,
        createdAt: '2026-02-01T10:00:00Z'
      },
      {
        id: 'role-warehouse',
        tenantId: this.getTenantStorageKey(),
        name: 'Warehouse Supervisor',
        description: 'Manages physical storage locations, inventory bin transfers, and stock adjustments.',
        assignmentMode: 'simple',
        templateId: 'tmpl-wh-super',
        permissions: this.permissionTemplates.find(t => t.id === 'tmpl-wh-super')?.permissionCodes || [],
        usersCount: 1,
        isSystem: false,
        createdAt: '2026-02-10T11:15:00Z'
      },
      {
        id: 'role-sales',
        tenantId: this.getTenantStorageKey(),
        name: 'Sales Representative',
        description: 'Enters and follows up on customer quotations, orders, and shipment tracking.',
        assignmentMode: 'simple',
        templateId: 'tmpl-sales-rep',
        permissions: this.permissionTemplates.find(t => t.id === 'tmpl-sales-rep')?.permissionCodes || [],
        usersCount: 1,
        isSystem: false,
        createdAt: '2026-02-20T14:00:00Z'
      },
      {
        id: 'role-auditor',
        tenantId: this.getTenantStorageKey(),
        name: 'Read-Only Auditor',
        description: 'Read-only access for internal audits and compliance reporting.',
        assignmentMode: 'simple',
        templateId: 'tmpl-viewer',
        permissions: this.permissionTemplates.find(t => t.id === 'tmpl-viewer')?.permissionCodes || [],
        usersCount: 1,
        isSystem: false,
        createdAt: '2026-03-01T16:45:00Z'
      }
    ];
  }

  createRole(dto: CreateRoleDto): Observable<RoleDefinition> {
    const key = ROLES_STORAGE_KEY_PREFIX + this.getTenantStorageKey();
    const stored = localStorage.getItem(key);
    let roles: RoleDefinition[] = stored ? JSON.parse(stored) : this.buildInitialRoles();

    const newRole: RoleDefinition = {
      id: 'role-' + Date.now().toString(36),
      tenantId: this.getTenantStorageKey(),
      name: dto.name.trim(),
      description: dto.description.trim(),
      assignmentMode: dto.assignmentMode,
      templateId: dto.templateId,
      permissions: dto.permissions,
      usersCount: 0,
      isSystem: false,
      createdAt: new Date().toISOString()
    };

    roles = [newRole, ...roles];
    this.saveRolesLocally(roles);
    return of(newRole);
  }

  private saveRolesLocally(roles: RoleDefinition[]): void {
    const key = ROLES_STORAGE_KEY_PREFIX + this.getTenantStorageKey();
    localStorage.setItem(key, JSON.stringify(roles));
  }

  // --- Users Management ---
  getUsers(): Observable<UserAccount[]> {
    this.http.get<ApiResponse<any[]>>(buildApiUrl('/User')).subscribe({
      next: res => {
        const list = res?.data;
        if (Array.isArray(list) && list.length > 0) {
          const apiUsers: UserAccount[] = list.map(u => {
            const primaryRole = u.roles?.[0];
            const primaryScope = primaryRole?.scopes?.[0];
            return {
              id: u.id || u.userId,
              tenantId: u.tenantId || this.getTenantStorageKey(),
              firstName: u.firstName || u.username,
              lastName: u.lastName || '',
              fullName: u.fullName || u.username,
              email: u.email,
              username: u.username,
              phone: u.phone || '',
              accountStatus: u.accountStatus || (u.isActive ? 'Active' : 'Suspended'),
              roleId: primaryRole?.roleId || '',
              roleName: primaryRole?.roleName || 'Authorized User',
              primaryCompanyId: primaryScope?.companyId || 'comp_default',
              primaryCompanyName: primaryScope?.companyName || 'Primary Operating Company',
              primaryBranchId: primaryScope?.branchId || 'br_default',
              primaryBranchName: primaryScope?.branchName || 'Primary Operating Branch',
              createdAt: u.createdAt || new Date().toISOString()
            };
          });
          this.mergeWithLocalUsers(apiUsers);
        }
      },
      error: () => {}
    });

    return of(this.getLocalUsers());
  }

  private getLocalUsers(): UserAccount[] {
    const key = USERS_STORAGE_KEY_PREFIX + this.getTenantStorageKey();
    const stored = localStorage.getItem(key);
    if (stored) {
      try {
        const users = JSON.parse(stored);
        if (Array.isArray(users) && users.length > 0) {
          return users;
        }
      } catch {
        // Fall through
      }
    }
    const initialUsers = this.buildInitialUsers();
    this.saveUsersLocally(initialUsers);
    return initialUsers;
  }

  private mergeWithLocalUsers(apiUsers: UserAccount[]): UserAccount[] {
    const local = this.getLocalUsers();
    const map = new Map<string, UserAccount>();
    for (const u of local) {
      map.set(u.email.toLowerCase(), u);
    }
    for (const u of apiUsers) {
      map.set(u.email.toLowerCase(), {
        ...(map.get(u.email.toLowerCase()) || {}),
        ...u
      });
    }
    const merged = Array.from(map.values());
    this.saveUsersLocally(merged);
    return merged;
  }

  private buildInitialUsers(): UserAccount[] {
    return [
      {
        id: 'usr-admin-01',
        tenantId: this.getTenantStorageKey(),
        firstName: 'John',
        lastName: 'Doe',
        fullName: 'John Doe',
        email: 'admin@alphamfg.com',
        username: 'johndoe',
        phone: '+1 (555) 019-2831',
        accountStatus: 'Active',
        roleId: 'role-admin',
        roleName: 'Tenant Administrator',
        primaryCompanyId: 'comp_default',
        primaryCompanyName: 'Apex Industrial Manufacturing Ltd',
        primaryBranchId: 'br_default',
        primaryBranchName: 'Colombo Headquarters Plant',
        createdAt: '2026-01-15T09:00:00Z'
      },
      {
        id: 'usr-proc-02',
        tenantId: this.getTenantStorageKey(),
        firstName: 'Sarah',
        lastName: 'Connor',
        fullName: 'Sarah Connor',
        email: 's.connor@alphamfg.com',
        username: 'sconnor',
        phone: '+1 (555) 014-9921',
        accountStatus: 'Active',
        roleId: 'role-procurement',
        roleName: 'Procurement Officer',
        primaryCompanyId: 'comp_default',
        primaryCompanyName: 'Apex Industrial Manufacturing Ltd',
        primaryBranchId: 'br_default',
        primaryBranchName: 'Colombo Headquarters Plant',
        createdAt: '2026-02-05T10:30:00Z'
      },
      {
        id: 'usr-wh-03',
        tenantId: this.getTenantStorageKey(),
        firstName: 'Alex',
        lastName: 'Mercer',
        fullName: 'Alex Mercer',
        email: 'a.mercer@alphamfg.com',
        username: 'amercer',
        phone: '+1 (555) 018-4432',
        accountStatus: 'Active',
        roleId: 'role-warehouse',
        roleName: 'Warehouse Supervisor',
        primaryCompanyId: 'comp_default',
        primaryCompanyName: 'Apex Industrial Manufacturing Ltd',
        primaryBranchId: 'br_default',
        primaryBranchName: 'Colombo Headquarters Plant',
        createdAt: '2026-02-12T11:45:00Z'
      },
      {
        id: 'usr-sales-04',
        tenantId: this.getTenantStorageKey(),
        firstName: 'Elena',
        lastName: 'Rostova',
        fullName: 'Elena Rostova',
        email: 'e.rostova@alphamfg.com',
        username: 'erostova',
        phone: '+1 (555) 017-7781',
        accountStatus: 'Active',
        roleId: 'role-sales',
        roleName: 'Sales Representative',
        primaryCompanyId: 'comp_default',
        primaryCompanyName: 'Apex Industrial Manufacturing Ltd',
        primaryBranchId: 'br_default',
        primaryBranchName: 'Colombo Headquarters Plant',
        createdAt: '2026-02-22T13:20:00Z'
      },
      {
        id: 'usr-audit-05',
        tenantId: this.getTenantStorageKey(),
        firstName: 'David',
        lastName: 'Silva',
        fullName: 'David Silva',
        email: 'd.silva@alphamfg.com',
        username: 'dsilva',
        phone: '+1 (555) 012-3349',
        accountStatus: 'Suspended',
        roleId: 'role-auditor',
        roleName: 'Read-Only Auditor',
        primaryCompanyId: 'comp_default',
        primaryCompanyName: 'Apex Industrial Manufacturing Ltd',
        primaryBranchId: 'br_default',
        primaryBranchName: 'Colombo Headquarters Plant',
        createdAt: '2026-03-02T15:10:00Z'
      }
    ];
  }

  createUser(dto: CreateUserDto, roleName: string, companyName: string, branchName: string): Observable<UserAccount> {
    const payload = {
      username: dto.username?.trim() || dto.email.split('@')[0],
      email: dto.email.trim().toLowerCase(),
      password: 'Password@123',
      firstName: dto.firstName?.trim() || '',
      lastName: dto.lastName?.trim() || '',
      phone: dto.phone?.trim() || '',
      accountStatus: dto.accountStatus || 'Active',
      roleId: dto.roleId,
      companyId: dto.companyId,
      branchId: dto.branchId
    };

    const localUser = this.createLocalUser(dto, roleName, companyName, branchName);

    this.http.post<ApiResponse<any>>(buildApiUrl('/User/tenant-user'), payload).subscribe({
      next: res => {
        if (res?.data) {
          localUser.id = res.data.userId || res.data.id || localUser.id;
          localUser.tenantId = res.data.tenantId || localUser.tenantId;
          localUser.roleName = res.data.roleName || localUser.roleName;
          this.saveSingleUserLocally(localUser);
        }
      },
      error: () => {}
    });

    return of(localUser);
  }

  private createLocalUser(dto: CreateUserDto, roleName: string, companyName: string, branchName: string): UserAccount {
    const newUser: UserAccount = {
      id: 'usr-' + Date.now().toString(36),
      tenantId: this.getTenantStorageKey(),
      firstName: dto.firstName.trim(),
      lastName: dto.lastName.trim(),
      fullName: `${dto.firstName.trim()} ${dto.lastName.trim()}`,
      email: dto.email.trim().toLowerCase(),
      username: dto.username?.trim() || dto.email.split('@')[0],
      phone: dto.phone?.trim() || '',
      accountStatus: dto.accountStatus,
      roleId: dto.roleId,
      roleName: roleName,
      primaryCompanyId: dto.companyId,
      primaryCompanyName: companyName,
      primaryBranchId: dto.branchId,
      primaryBranchName: branchName,
      createdAt: new Date().toISOString()
    };

    this.saveSingleUserLocally(newUser);

    this.assignUserScopeInternal({
      id: 'scp-' + Date.now().toString(36),
      userId: newUser.id,
      userName: newUser.fullName,
      userEmail: newUser.email,
      companyId: newUser.primaryCompanyId,
      companyName: newUser.primaryCompanyName,
      branchId: newUser.primaryBranchId,
      branchName: newUser.primaryBranchName,
      isAllBranches: false,
      isPrimary: true,
      status: 'Authorized',
      grantedAt: new Date().toISOString(),
      grantedBy: 'Tenant Admin',
      notes: 'Initial primary facility scope assignment on onboarding.'
    });

    return newUser;
  }

  private saveSingleUserLocally(newUser: UserAccount): void {
    const users = this.getLocalUsers();
    const filtered = users.filter(u => u.id !== newUser.id && u.email.toLowerCase() !== newUser.email.toLowerCase());
    this.saveUsersLocally([newUser, ...filtered]);
  }

  toggleUserStatus(userId: string): Observable<UserAccount | null> {
    const updated = this.toggleLocalUserStatus(userId);
    this.http.put<ApiResponse<any>>(buildApiUrl(`/User/${userId}/toggle-status`), {}).subscribe({
      error: () => {}
    });
    return of(updated);
  }

  private toggleLocalUserStatus(userId: string): UserAccount | null {
    const users = this.getLocalUsers();
    const idx = users.findIndex(u => u.id === userId);
    if (idx === -1) return null;

    const currentStatus = users[idx].accountStatus;
    users[idx].accountStatus = currentStatus === 'Active' ? 'Suspended' : 'Active';
    users[idx].updatedAt = new Date().toISOString();

    this.saveUsersLocally(users);
    return users[idx];
  }

  private saveUsersLocally(users: UserAccount[]): void {
    const key = USERS_STORAGE_KEY_PREFIX + this.getTenantStorageKey();
    localStorage.setItem(key, JSON.stringify(users));
  }

  // --- User Scope Assignments (Company & Branch Assignment) ---
  getUserScopes(userId?: string): Observable<UserScopeAssignment[]> {
    this.http.get<ApiResponse<any[]>>(buildApiUrl('/User/scopes')).subscribe({
      next: res => {
        const list = res?.data;
        if (Array.isArray(list) && list.length > 0) {
          const apiScopes: UserScopeAssignment[] = list.map(s => ({
            id: s.id || s.scopeId,
            userId: s.userId,
            userName: s.userName || 'Authorized User',
            userEmail: s.userEmail || '',
            companyId: s.companyId || 'comp_default',
            companyName: s.companyName || 'Primary Operating Company',
            branchId: s.branchId || 'br_default',
            branchName: s.branchName || 'Primary Branch',
            isAllBranches: !!s.isAllBranches,
            isPrimary: !!s.isPrimary,
            status: s.isActive ? 'Authorized' : 'Revoked',
            grantedAt: s.grantedAt || new Date().toISOString(),
            grantedBy: s.grantedBy || 'Tenant Admin',
            notes: s.notes || 'Operating facility scope authorization.'
          }));
          this.mergeWithLocalScopes(apiScopes);
        }
      },
      error: () => {}
    });

    return of(this.getLocalScopes(userId));
  }

  private getLocalScopes(userId?: string): UserScopeAssignment[] {
    const key = SCOPES_STORAGE_KEY_PREFIX + this.getTenantStorageKey();
    const stored = localStorage.getItem(key);
    let scopes: UserScopeAssignment[] = [];
    if (stored) {
      try {
        scopes = JSON.parse(stored);
      } catch {
        scopes = [];
      }
    }
    if (!scopes || scopes.length === 0) {
      scopes = this.buildInitialScopes();
      this.saveScopesLocally(scopes);
    }
    if (userId) {
      return scopes.filter(s => s.userId === userId);
    }
    return scopes;
  }

  private mergeWithLocalScopes(apiScopes: UserScopeAssignment[]): UserScopeAssignment[] {
    const local = this.getLocalScopes();
    const map = new Map<string, UserScopeAssignment>();
    for (const s of local) {
      map.set(`${s.userId}_${s.companyId}_${s.branchId}`, s);
    }
    for (const s of apiScopes) {
      map.set(`${s.userId}_${s.companyId}_${s.branchId}`, {
        ...(map.get(`${s.userId}_${s.companyId}_${s.branchId}`) || {}),
        ...s
      });
    }
    const merged = Array.from(map.values());
    this.saveScopesLocally(merged);
    return merged;
  }

  private buildInitialScopes(): UserScopeAssignment[] {
    return [
      {
        id: 'scp-init-01',
        userId: 'usr-admin-01',
        userName: 'John Doe',
        userEmail: 'admin@alphamfg.com',
        companyId: 'comp_default',
        companyName: 'Apex Industrial Manufacturing Ltd',
        branchId: 'br_default',
        branchName: 'Colombo Headquarters Plant',
        isAllBranches: true,
        isPrimary: true,
        status: 'Authorized',
        grantedAt: '2026-01-15T09:00:00Z',
        grantedBy: 'System Bootstrap',
        notes: 'Full administrative access across all operating companies & branches.'
      },
      {
        id: 'scp-init-02',
        userId: 'usr-proc-02',
        userName: 'Sarah Connor',
        userEmail: 's.connor@alphamfg.com',
        companyId: 'comp_default',
        companyName: 'Apex Industrial Manufacturing Ltd',
        branchId: 'br_default',
        branchName: 'Colombo Headquarters Plant',
        isAllBranches: false,
        isPrimary: true,
        status: 'Authorized',
        grantedAt: '2026-02-05T10:30:00Z',
        grantedBy: 'John Doe',
        notes: 'Primary purchasing jurisdiction for Colombo HQ facility.'
      },
      {
        id: 'scp-init-03',
        userId: 'usr-proc-02',
        userName: 'Sarah Connor',
        userEmail: 's.connor@alphamfg.com',
        companyId: 'comp_default',
        companyName: 'Apex Industrial Manufacturing Ltd',
        branchId: 'br_kandy',
        branchName: 'Kandy Manufacturing Plant',
        isAllBranches: false,
        isPrimary: false,
        status: 'Authorized',
        grantedAt: '2026-02-15T14:00:00Z',
        grantedBy: 'John Doe',
        notes: 'Secondary authorized scope for Central Province procurement.'
      },
      {
        id: 'scp-init-04',
        userId: 'usr-wh-03',
        userName: 'Alex Mercer',
        userEmail: 'a.mercer@alphamfg.com',
        companyId: 'comp_default',
        companyName: 'Apex Industrial Manufacturing Ltd',
        branchId: 'br_default',
        branchName: 'Colombo Headquarters Plant',
        isAllBranches: false,
        isPrimary: true,
        status: 'Authorized',
        grantedAt: '2026-02-12T11:45:00Z',
        grantedBy: 'John Doe',
        notes: 'Main distribution warehouse supervisor.'
      },
      {
        id: 'scp-init-05',
        userId: 'usr-sales-04',
        userName: 'Elena Rostova',
        userEmail: 'e.rostova@alphamfg.com',
        companyId: 'comp_default',
        companyName: 'Apex Industrial Manufacturing Ltd',
        branchId: 'br_default',
        branchName: 'Colombo Headquarters Plant',
        isAllBranches: false,
        isPrimary: true,
        status: 'Authorized',
        grantedAt: '2026-02-22T13:20:00Z',
        grantedBy: 'John Doe',
        notes: 'Commercial distribution scope.'
      }
    ];
  }

  assignUserScope(
    dto: AssignScopeDto,
    userName: string,
    userEmail: string,
    companyName: string,
    branchName: string
  ): Observable<UserScopeAssignment> {
    const payload = {
      userId: dto.userId,
      companyId: dto.companyId,
      branchId: dto.branchId,
      isAllBranches: !!dto.isAllBranches,
      notes: dto.notes
    };

    const newScope = this.createLocalScope(dto, userName, userEmail, companyName, branchName);

    this.http.post<ApiResponse<any>>(buildApiUrl('/User/scope'), payload).subscribe({
      next: res => {
        if (res?.data?.scopeId) {
          newScope.id = res.data.scopeId;
          this.assignUserScopeInternal(newScope);
        }
      },
      error: () => {}
    });

    return of(newScope);
  }

  private createLocalScope(
    dto: AssignScopeDto,
    userName: string,
    userEmail: string,
    companyName: string,
    branchName: string,
    scopeId?: string
  ): UserScopeAssignment {
    const newScope: UserScopeAssignment = {
      id: scopeId || ('scp-' + Date.now().toString(36)),
      userId: dto.userId,
      userName: userName,
      userEmail: userEmail,
      companyId: dto.companyId,
      companyName: companyName,
      branchId: dto.branchId,
      branchName: branchName,
      isAllBranches: !!dto.isAllBranches,
      isPrimary: false,
      status: 'Authorized',
      grantedAt: new Date().toISOString(),
      grantedBy: 'Tenant Admin',
      notes: dto.notes?.trim() || 'Additional operating branch authorization granted by administrator.'
    };

    this.assignUserScopeInternal(newScope);
    return newScope;
  }

  private assignUserScopeInternal(assignment: UserScopeAssignment): void {
    const key = SCOPES_STORAGE_KEY_PREFIX + this.getTenantStorageKey();
    const stored = localStorage.getItem(key);
    let scopes: UserScopeAssignment[] = stored ? JSON.parse(stored) : this.buildInitialScopes();

    // Check if duplicate assignment exists
    const existingIdx = scopes.findIndex(
      s => s.userId === assignment.userId && s.companyId === assignment.companyId && s.branchId === assignment.branchId
    );

    if (existingIdx !== -1) {
      scopes[existingIdx].status = 'Authorized';
      scopes[existingIdx].grantedAt = new Date().toISOString();
      if (assignment.notes) scopes[existingIdx].notes = assignment.notes;
    } else {
      scopes = [assignment, ...scopes];
    }

    this.saveScopesLocally(scopes);
  }

  revokeUserScope(scopeId: string): Observable<boolean> {
    const key = SCOPES_STORAGE_KEY_PREFIX + this.getTenantStorageKey();
    const stored = localStorage.getItem(key);
    if (!stored) return of(false);
    let scopes: UserScopeAssignment[] = JSON.parse(stored);

    const idx = scopes.findIndex(s => s.id === scopeId);
    if (idx === -1) return of(false);

    // If primary, do not remove, mark as revoked or preserve
    if (scopes[idx].isPrimary) {
      return of(false);
    }

    scopes.splice(idx, 1);
    this.saveScopesLocally(scopes);
    return of(true);
  }

  private saveScopesLocally(scopes: UserScopeAssignment[]): void {
    const key = SCOPES_STORAGE_KEY_PREFIX + this.getTenantStorageKey();
    localStorage.setItem(key, JSON.stringify(scopes));
  }
}
