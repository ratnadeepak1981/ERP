export interface SubscriptionPlan {
  id: string;
  name: string;
  code: string;
  description: string;
  price: number;
  billingCycle: number; // 1 = Daily, 2 = Monthly, 3 = Quarterly, 4 = Annual, 5 = Lifetime
  isActive: boolean;
  createdAt?: string;
  createdBy?: string;
  modifiedAt?: string;
  modifiedBy?: string;
}

export function formatBillingCycle(cycle: number): string {
  switch (cycle) {
    case 1:
      return 'Daily';
    case 2:
      return 'Monthly';
    case 3:
      return 'Quarterly';
    case 4:
      return 'Annual';
    case 5:
      return 'Lifetime';
    default:
      return 'Monthly';
  }
}
