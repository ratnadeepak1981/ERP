export interface TenantProvisioningRequest {
  tenantId: string;
  adminUserName: string;
  adminEmail: string;
  adminPassword: string;
}

export interface TenantProvisioningResult {
  tenantId: string;
  databaseProvisioned: boolean;
  configurationProvisioned: boolean;
  isProvisioned: boolean;
  storageMode: string;
}

export interface ProvisioningQueueItem {
  id: string;
  name: string;
  code: string;
  planName?: string;
  isActive?: boolean;
  createdAt?: string;
  subscriptionStatus?: string;
  storageMode: string;
  status: 'Pending' | 'In Progress' | 'Completed' | 'Failed';
  isProvisioned?: boolean;
  databaseName?: string | null;
  adminUsername?: string | null;
  adminEmail?: string | null;
}

export interface ProvisioningMetrics {
  totalRegistered?: number | null;
  pending: number | null;
  inProgress: number | null;
  failed: number | null;
  completed: number | null;
  activeSubscriptions?: number | null;
  isLive: boolean;
}

export interface TenantDetails {
  id: string;
  name: string;
  code: string;
  isActive: boolean;
  createdAt: string;
  modifiedAt?: string | null;
  subscriptionId?: string | null;
  subscriptionPlanId?: string | null;
  subscriptionName?: string;
  subscriptionType?: string;
  subscriptionStatus?: string;
  storageMode: string;
  subscriptionStartDate?: string | null;
  subscriptionEndDate?: string | null;
  isProvisioned: boolean;
  databaseName?: string | null;
  databaseServer?: string | null;
  databaseIsActive?: boolean;
  configurationExists?: boolean;
}

export interface TenantLifecycleStatus {
  tenantId: string;
  isRegistered: boolean;
  hasActiveSubscription: boolean;
  subscriptionStatus: string;
  storageMode: string;
  databaseAllocated: boolean;
  databaseName?: string | null;
  databaseServer?: string | null;
  configurationAllocated: boolean;
  isProvisioned: boolean;
  overallStatus: 'Pending' | 'Completed' | 'Failed';
}

export interface BulkTenantItem {
  tenantId: string;
  name: string;
  code: string;
  adminUserName: string;
  adminEmail: string;
  adminPassword: string;
  status: 'Pending' | 'In Progress' | 'Completed' | 'Failed' | 'Skipped';
  error?: string;
  result?: TenantProvisioningResult;
}

export interface BulkProvisioningSummary {
  total: number;
  completed: number;
  failed: number;
  remaining: number;
  progressPercent: number;
  isProcessing: boolean;
  isFinished: boolean;
}

export interface TenantDirectoryRecord {
  id: string;
  code: string;
  name: string;
  legalName?: string | null;
  subdomain?: string | null;
  taxId?: string | null;
  billingEmail?: string | null;
  city?: string | null;
  countryCode?: string | null;
  isActive: boolean;
  status: string; // "Completed" | "Pending"
  tenantStatus?: 'Active' | 'Inactive';
  createdAt: string;
  modifiedAt?: string | null;

  // Subscription & Plan
  subscriptionId?: string | null;
  subscriptionPlanId?: string | null;
  subscriptionName?: string | null;
  planName?: string | null;
  planCode?: string | null;
  planIsActive?: boolean | null;
  subscriptionStatus: string;
  storageMode: string;
  subscriptionStartDate?: string | null;
  subscriptionEndDate?: string | null;
  nextBillingDate?: string | null;
  billingCycle?: string | null;
  billingPlanPriceSnapshot?: number | null;
  currency?: string | null;
  autoRenew?: boolean | null;

  // Database & Provisioning
  isProvisioned: boolean;
  databaseName?: string | null;
  databaseServer?: string | null;
  databaseIsActive?: boolean | null;
  provisioningDate?: string | null;
  configurationExists: boolean;
  companyEnabled: boolean;
  branchEnabled: boolean;
  warehouseZoneEnabled?: boolean;
  warehouseBinEnabled?: boolean;
  procurementApprovalRequired?: boolean;
  procurementApprovalMode?: number;
  specialUomConversionEnabled?: boolean;
  industryType?: string | null;
  companyStructure?: string | null;
  eCommerceEnabled?: boolean;

  // Administration & RBAC
  adminUsername?: string | null;
  adminEmail?: string | null;
  tenantAdminUsernames: string[];
  tenantAdminEmails: string[];
  tenantAdminCount: number;
  tenantAdminAccountStatus?: string | null;

  // Users
  totalUserCount: number;
  activeUserCount: number;
  inactiveUserCount: number;

  // Company & Branch Scope
  companyCount: number;
  branchCount: number;
  primaryCompanyName?: string | null;
  primaryCompanyStatus?: string | null;
}

export interface TenantUserRecord {
  id: string;
  tenantId?: string | null;
  username: string;
  email: string;
  fullName: string;
  accountStatus: 'Active' | 'Suspended';
  roles: {
    roleId: string;
    roleName: string;
    scopes: {
      scopeId: string;
      companyId?: string | null;
      branchId?: string | null;
    }[];
  }[];
  createdAt: string;
  lastActivityAt?: string | null;
}
