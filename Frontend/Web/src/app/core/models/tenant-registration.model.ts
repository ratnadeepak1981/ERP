export interface CreateTenantRequest {
  name: string;
  subscriptionPlanId: string;
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

export interface TenantRegistrationResult {
  tenant: TenantSummary;
  subscription: SubscriptionSummary;
}
