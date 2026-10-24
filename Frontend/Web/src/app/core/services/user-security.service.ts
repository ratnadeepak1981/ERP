import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of, map, switchMap, catchError } from 'rxjs';
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
  AssignScopeDto,
  UpdateUserDto,
  UpdateRoleDto
} from '../models/security.models';

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
    { id: 'p-cust-view', category: 'Master Data', code: 'customers.view', name: 'View Customer Master', description: 'Browse enterprise customer accounts' },
    { id: 'p-cust-edit', category: 'Master Data', code: 'customers.edit', name: 'Manage Customer Master', description: 'Register and update customer accounts and credit limits' },
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
        'scope.switch', 'products.view', 'products.edit', 'customers.view', 'customers.edit', 'suppliers.view', 'warehouses.view',
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
        'scope.switch', 'products.view', 'customers.view', 'customers.edit', 'so.view', 'so.create', 'customers.manage', 'stock.view'
      ]
    },
    {
      id: 'tmpl-viewer',
      name: 'Read-Only Auditor',
      description: 'Inspect operational records, metrics, and export audit reports without editing rights.',
      permissionCodes: [
        'scope.switch', 'products.view', 'customers.view', 'suppliers.view', 'warehouses.view',
        'po.view', 'so.view', 'stock.view', 'finance.view', 'reports.export'
      ]
    }
  ];

  // Helper to resolve role permissions when backend only stores role headers
  private resolvePermissionsForRole(roleName: string, backendPermissions?: string[]): string[] {
    if (backendPermissions && backendPermissions.length > 0) {
      return backendPermissions;
    }
    const lower = (roleName || '').toLowerCase();
    if (lower.includes('admin')) {
      return this.systemPermissions.map(p => p.code);
    }
    const template = this.permissionTemplates.find(t => t.name.toLowerCase() === lower);
    if (template) {
      return template.permissionCodes;
    }
    return [
      'scope.switch', 'products.view', 'customers.view', 'suppliers.view', 'warehouses.view', 'po.view', 'so.view', 'stock.view'
    ];
  }

  // --- Roles Management ---
  getRoles(): Observable<RoleDefinition[]> {
    return this.http.get<ApiResponse<any[]>>(buildApiUrl('/Role')).pipe(
      map(res => {
        const list = res?.data || [];
        if (!Array.isArray(list)) return [];
        return list.map(r => ({
          id: r.id || r.roleId,
          tenantId: r.tenantId || this.getTenantStorageKey(),
          name: r.name,
          description: r.description || '',
          assignmentMode: 'simple' as const,
          permissions: this.resolvePermissionsForRole(r.name, r.permissions),
          usersCount: r.usersCount || 0,
          isSystem: !!r.isSystem,
          createdAt: r.createdAt || new Date().toISOString()
        }));
      }),
      catchError(() => of([]))
    );
  }

  createRole(dto: CreateRoleDto): Observable<RoleDefinition> {
    const payload = {
      name: dto.name.trim(),
      description: dto.description.trim()
    };

    return this.http.post<ApiResponse<any>>(buildApiUrl('/Role'), payload).pipe(
      map(res => {
        const r = res?.data;
        return {
          id: r?.roleId || r?.id || ('role-' + Date.now().toString(36)),
          tenantId: r?.tenantId || this.getTenantStorageKey(),
          name: r?.name || dto.name.trim(),
          description: r?.description || dto.description.trim(),
          assignmentMode: dto.assignmentMode,
          templateId: dto.templateId,
          permissions: dto.permissions,
          usersCount: 0,
          isSystem: false,
          createdAt: new Date().toISOString()
        };
      })
    );
  }

  // --- Users Management ---
  getUsers(): Observable<UserAccount[]> {
    return this.companyService.getCompanies().pipe(
      switchMap(companies => {
        const compMap = new Map<string, string>();
        for (const c of companies) {
          compMap.set(c.id, c.name);
        }

        return this.http.get<ApiResponse<any[]>>(buildApiUrl('/User')).pipe(
          map(res => {
            const list = res?.data || [];
            if (!Array.isArray(list)) return [];

            return list.map(u => {
              const primaryRole = u.roles?.[0];
              const primaryScope = primaryRole?.scopes?.[0];
              const compId = primaryScope?.companyId || '';
              const branchId = primaryScope?.branchId || '';
              const compName = compMap.get(compId) || (compId ? 'Primary Operating Company' : 'Assigned Company');
              const branchName = branchId ? 'Assigned Operating Branch' : 'All Facilities';

              return {
                id: u.id || u.userId,
                tenantId: u.tenantId || this.getTenantStorageKey(),
                firstName: u.firstName || u.username,
                lastName: u.lastName || '',
                fullName: u.fullName || u.username,
                email: u.email,
                username: u.username,
                phone: u.phone || '',
                accountStatus: ((u.accountStatus === 'Suspended' || u.accountStatus === 'Inactive') ? u.accountStatus : (u.isActive === false ? 'Suspended' : 'Active')) as 'Active' | 'Suspended' | 'Inactive',
                roleId: primaryRole?.roleId || '',
                roleName: primaryRole?.roleName || 'Authorized User',
                primaryCompanyId: compId,
                primaryCompanyName: compName,
                primaryBranchId: branchId,
                primaryBranchName: branchName,
                createdAt: u.createdAt || new Date().toISOString()
              };
            });
          })
        );
      }),
      catchError(() => of([]))
    );
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

    return this.http.post<ApiResponse<any>>(buildApiUrl('/User/tenant-user'), payload).pipe(
      map(res => {
        const data = res?.data;
        return {
          id: data?.userId || data?.id || ('usr-' + Date.now().toString(36)),
          tenantId: data?.tenantId || this.getTenantStorageKey(),
          firstName: dto.firstName.trim(),
          lastName: dto.lastName.trim(),
          fullName: `${dto.firstName.trim()} ${dto.lastName.trim()}`.trim() || dto.username || dto.email,
          email: dto.email.trim().toLowerCase(),
          username: dto.username?.trim() || dto.email.split('@')[0],
          phone: dto.phone?.trim() || '',
          accountStatus: dto.accountStatus,
          roleId: dto.roleId,
          roleName: data?.roleName || roleName,
          primaryCompanyId: dto.companyId,
          primaryCompanyName: companyName,
          primaryBranchId: dto.branchId,
          primaryBranchName: branchName,
          createdAt: new Date().toISOString()
        };
      })
    );
  }

  toggleUserStatus(userId: string): Observable<UserAccount | null> {
    return this.http.put<ApiResponse<any>>(buildApiUrl(`/User/${userId}/toggle-status`), {}).pipe(
      map(res => {
        const d = res?.data;
        if (!d) return null;
        return {
          id: d.userId || userId,
          accountStatus: ((d.accountStatus === 'Suspended' || d.accountStatus === 'Inactive') ? d.accountStatus : (d.isActive === false ? 'Suspended' : 'Active')) as 'Active' | 'Suspended' | 'Inactive'
        } as UserAccount;
      }),
      catchError(() => of(null))
    );
  }

  // --- User Scope Assignments (Company & Branch Assignment) ---
  getUserScopes(userId?: string, includeInactive: boolean = true): Observable<UserScopeAssignment[]> {
    let url = buildApiUrl('/User/scopes');
    const queryParams: string[] = [];
    if (userId) {
      queryParams.push(`userId=${encodeURIComponent(userId)}`);
    }
    if (includeInactive) {
      queryParams.push('includeInactive=true');
    }
    if (queryParams.length > 0) {
      url += '?' + queryParams.join('&');
    }

    return this.companyService.getCompanies().pipe(
      switchMap(companies => {
        const compMap = new Map<string, { name: string; code: string }>();
        for (const c of companies) {
          compMap.set(c.id, { name: c.name, code: c.code });
        }

        return this.http.get<ApiResponse<any[]>>(url).pipe(
          map(res => {
            const list = res?.data || [];
            if (!Array.isArray(list)) return [];

            let scopes: UserScopeAssignment[] = list.map(s => {
              const compInfo = compMap.get(s.companyId);
              const compName = s.companyName || compInfo?.name || (s.companyId ? 'Operating Company' : 'All Companies');
              const compCode = s.companyCode || compInfo?.code || '';
              const isAll = s.isAllBranches ?? !s.branchId;
              const branchName = s.branchName || (s.branchId ? 'Operating Branch' : 'All Facilities');
              const statusStr = (s.isActive === false || s.status === 'Revoked') ? 'Revoked' : 'Authorized';

              return {
                id: s.id || s.scopeId,
                userId: s.userId,
                userName: s.userName || 'Authorized User',
                userEmail: s.userEmail || '',
                companyId: s.companyId || '',
                companyName: compName,
                companyCode: compCode,
                branchId: s.branchId || '',
                branchName: isAll ? 'All Facilities' : branchName,
                branchCode: s.branchCode || '',
                isAllBranches: isAll,
                scopeLevel: s.scopeLevel || (isAll ? 'Company-wide' : 'Branch-specific'),
                isPrimary: false,
                status: statusStr,
                grantedAt: s.grantedAt || new Date().toISOString(),
                grantedBy: s.grantedBy || 'Tenant Admin',
                notes: s.notes || (isAll ? 'Company-wide organizational scope authorization.' : 'Operating facility scope authorization.')
              };
            });

            if (userId) {
              scopes = scopes.filter(s => s.userId.toLowerCase() === userId.toLowerCase());
            }
            return scopes;
          })
        );
      }),
      catchError(() => of([]))
    );
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
      branchId: dto.branchId === 'all' || !dto.branchId ? null : dto.branchId,
      isAllBranches: !!dto.isAllBranches,
      notes: dto.notes
    };

    return this.http.post<ApiResponse<any>>(buildApiUrl('/User/scope'), payload).pipe(
      map(res => {
        const data = res?.data;
        const isAll = data?.isAllBranches ?? !!dto.isAllBranches;
        return {
          id: data?.scopeId || ('scp-' + Date.now().toString(36)),
          userId: dto.userId,
          userName: userName,
          userEmail: userEmail,
          companyId: dto.companyId,
          companyName: data?.companyName || companyName,
          companyCode: data?.companyCode || '',
          branchId: dto.branchId,
          branchName: isAll ? 'All Facilities' : (data?.branchName || branchName),
          branchCode: data?.branchCode || '',
          isAllBranches: isAll,
          scopeLevel: data?.scopeLevel || (isAll ? 'Company-wide' : 'Branch-specific'),
          isPrimary: false,
          status: 'Authorized',
          grantedAt: new Date().toISOString(),
          grantedBy: 'Tenant Admin',
          notes: dto.notes?.trim() || 'Operating branch authorization.'
        };
      })
    );
  }

  revokeUserScope(scopeId: string): Observable<boolean> {
    return this.http.delete<ApiResponse<any>>(buildApiUrl('/User/scope/' + scopeId)).pipe(
      map(res => res?.succeeded ?? true),
      catchError(() => of(false))
    );
  }

  getRole(roleId: string): Observable<RoleDefinition> {
    return this.http.get<ApiResponse<any>>(buildApiUrl('/Role/' + roleId)).pipe(
      map(res => {
        const r = res?.data;
        if (!r) throw new Error('Role record not found or returned empty from server.');
        const perms: string[] = Array.isArray(r.permissions)
          ? r.permissions.map((p: any) => typeof p === 'string' ? p : p.code)
          : [];
        return {
          id: r.id || roleId,
          tenantId: r.tenantId || this.getTenantStorageKey(),
          name: r.name,
          description: r.description || '',
          assignmentMode: 'advanced' as const,
          permissions: perms,
          usersCount: r.usersCount || 0,
          isSystem: !!r.isSystem,
          isActive: r.isActive !== false,
          createdAt: r.createdAt || new Date().toISOString()
        };
      })
    );
  }

  updateRole(roleId: string, dto: UpdateRoleDto): Observable<RoleDefinition> {
    const payload = {
      name: dto.name.trim(),
      description: dto.description?.trim() || ''
    };
    return this.http.put<ApiResponse<any>>(buildApiUrl('/Role/' + roleId), payload).pipe(
      map(res => {
        const r = res?.data;
        return {
          id: r?.id || roleId,
          tenantId: r?.tenantId || this.getTenantStorageKey(),
          name: r?.name || dto.name.trim(),
          description: r?.description || dto.description?.trim() || '',
          assignmentMode: 'advanced' as const,
          permissions: [],
          usersCount: 0,
          isSystem: !!r?.isSystemRole,
          isActive: r?.isActive !== false,
          createdAt: new Date().toISOString()
        };
      })
    );
  }

  toggleRoleStatus(roleId: string): Observable<RoleDefinition | null> {
    return this.http.put<ApiResponse<any>>(buildApiUrl('/Role/' + roleId + '/toggle-status'), {}).pipe(
      map(res => {
        const r = res?.data;
        if (!r) return null;
        return {
          id: r.id || roleId,
          name: r.name,
          isActive: r.isActive !== false
        } as unknown as RoleDefinition;
      }),
      catchError(() => of(null))
    );
  }

  getPermissions(): Observable<PermissionItem[]> {
    return this.http.get<ApiResponse<any[]>>(buildApiUrl('/permission')).pipe(
      map(res => {
        const list = res?.data;
        if (!Array.isArray(list) || list.length === 0) {
          return this.systemPermissions;
        }
        return list.map(p => ({
          id: p.id,
          category: p.module || 'General',
          code: p.code,
          name: p.name,
          description: p.description || ''
        }));
      }),
      catchError(() => of(this.systemPermissions))
    );
  }

  getRolePermissions(roleId: string): Observable<PermissionItem[]> {
    return this.http.get<ApiResponse<any[]>>(buildApiUrl('/RolePermission/' + roleId)).pipe(
      map(res => {
        const list = res?.data;
        if (!Array.isArray(list)) return [];
        return list.map(p => ({
          id: p.id,
          category: p.module || 'General',
          code: p.code,
          name: p.name,
          description: p.description || ''
        }));
      }),
      catchError(() => of([]))
    );
  }

  syncRolePermissions(roleId: string, permissionCodes: string[], permissionIds?: string[]): Observable<ApiResponse<any>> {
    const payload = {
      roleId,
      permissionCodes: permissionCodes && permissionCodes.length > 0 ? permissionCodes : [],
      permissionIds: permissionIds && permissionIds.length > 0 ? permissionIds : []
    };
    return this.http.post<ApiResponse<any>>(buildApiUrl('/RolePermission/batch'), payload);
  }

  getUser(userId: string): Observable<UserAccount | null> {
    return this.companyService.getCompanies().pipe(
      switchMap(companies => {
        const compMap = new Map<string, string>();
        for (const c of companies) {
          compMap.set(c.id, c.name);
        }
        return this.http.get<ApiResponse<any>>(buildApiUrl('/User/' + userId)).pipe(
          map(res => {
            const u = res?.data;
            if (!u) return null;
            const primaryRole = u.roles?.[0];
            const primaryScope = primaryRole?.scopes?.[0];
            const compId = primaryScope?.companyId || '';
            const branchId = primaryScope?.branchId || '';
            const compName = compMap.get(compId) || (compId ? 'Primary Operating Company' : 'Assigned Company');
            const branchName = branchId ? 'Assigned Operating Branch' : 'All Facilities';

            return {
              id: u.id || u.userId || userId,
              tenantId: u.tenantId || this.getTenantStorageKey(),
              firstName: u.firstName || u.username,
              lastName: u.lastName || '',
              fullName: u.fullName || u.username,
              email: u.email,
              username: u.username,
              phone: u.phone || '',
              accountStatus: ((u.accountStatus === 'Suspended' || u.accountStatus === 'Inactive') ? u.accountStatus : (u.isActive === false ? 'Suspended' : 'Active')) as 'Active' | 'Suspended' | 'Inactive',
              roleId: primaryRole?.roleId || '',
              roleName: primaryRole?.roleName || 'Authorized User',
              primaryCompanyId: compId,
              primaryCompanyName: compName,
              primaryBranchId: branchId,
              primaryBranchName: branchName,
              createdAt: u.createdAt || new Date().toISOString()
            } as UserAccount;
          })
        );
      }),
      catchError(() => of(null))
    );
  }

  updateUser(userId: string, dto: UpdateUserDto): Observable<UserAccount> {
    const payload: any = {};
    if (dto.username) payload.username = dto.username;
    if (dto.email) payload.email = dto.email;
    if (dto.isActive !== undefined) payload.isActive = dto.isActive;
    if (dto.roleId) payload.roleId = dto.roleId;

    return this.http.put<ApiResponse<any>>(buildApiUrl('/User/' + userId), payload).pipe(
      map(res => {
        const d = res?.data;
        return {
          id: d?.userId || userId,
          username: d?.username || dto.username || '',
          email: d?.email || dto.email || '',
          accountStatus: d?.isActive ? 'Active' : 'Suspended'
        } as UserAccount;
      })
    );
  }

}
