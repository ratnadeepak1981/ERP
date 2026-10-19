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
