export interface CreateTenantRequest {
  name: string;
  subscriptionPlanId: string;
  subdomain?: string;
}

export interface TenantSummary {
  id: string;
  name: string;
  code: string;
  isActive: boolean;
}

export interface SubscriptionSummary {
  id: string;
  tenantId: string;
  subscriptionPlanId: string;
  subscriptionName: string;
  subscriptionType: number | string;
  storageMode: number | string;
  startDate: string;
  endDate: string | null;
  isActive: boolean;
}

export interface TenantDomainSummary {
  id: string;
  hostname: string;
  subdomain: string;
  domainType?: string;
  isPrimary: boolean;
  isVerified: boolean;
}

export interface TenantRegistrationResult {
  tenant: TenantSummary;
  subscription: SubscriptionSummary;
  domain?: TenantDomainSummary | null;
  hostname?: string | null;
  subdomain?: string | null;
}
