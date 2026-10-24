export interface UserAccount {
  id: string;
  tenantId: string;
  firstName: string;
  lastName: string;
  fullName: string;
  email: string;
  username: string;
  phone?: string;
  accountStatus: 'Active' | 'Suspended' | 'Inactive';
  roleId: string;
  roleName: string;
  primaryCompanyId: string;
  primaryCompanyName: string;
  primaryBranchId: string;
  primaryBranchName: string;
  createdAt: string;
  updatedAt?: string;
}

export interface CreateUserDto {
  firstName: string;
  lastName: string;
  email: string;
  username: string;
  phone?: string;
  accountStatus: 'Active' | 'Suspended' | 'Inactive';
  roleId: string;
  companyId: string;
  branchId: string;
}

export interface UpdateUserDto {
  username?: string;
  email?: string;
  isActive?: boolean;
  roleId?: string;
}

export interface PermissionItem {
  id: string;
  category: string;
  code: string;
  name: string;
  description: string;
}

export interface PermissionTemplate {
  id: string;
  name: string;
  description: string;
  permissionCodes: string[];
}

export interface RoleDefinition {
  id: string;
  tenantId: string;
  name: string;
  description: string;
  assignmentMode: 'simple' | 'advanced';
  templateId?: string;
  permissions: string[];
  usersCount: number;
  isSystem: boolean;
  isActive?: boolean;
  createdAt: string;
}

export interface CreateRoleDto {
  name: string;
  description: string;
  assignmentMode: 'simple' | 'advanced';
  templateId?: string;
  permissions: string[];
}

export interface UpdateRoleDto {
  name: string;
  description?: string;
}

export interface UserScopeAssignment {
  id: string;
  userId: string;
  userName: string;
  userEmail: string;
  companyId: string;
  companyName: string;
  companyCode?: string;
  branchId: string;
  branchName: string;
  branchCode?: string;
  isAllBranches: boolean;
  scopeLevel?: string;
  isPrimary: boolean;
  status: 'Authorized' | 'Revoked';
  grantedAt: string;
  grantedBy: string;
  notes?: string;
}

export interface AssignScopeDto {
  userId: string;
  companyId: string;
  branchId: string;
  isAllBranches?: boolean;
  notes?: string;
}
