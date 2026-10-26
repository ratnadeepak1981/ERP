export enum SalesOrderStatus {
  Draft = 1,
  Submitted = 2,
  Confirmed = 3,
  Fulfilled = 4,
  Cancelled = 5,
  Rejected = 6
}

export enum SalesOrderType {
  Standard = 1,
  Direct = 2,
  Service = 3
}

export function getSalesOrderStatusName(status: SalesOrderStatus | number | string): string {
  const num = Number(status);
  switch (num) {
    case SalesOrderStatus.Draft:
      return 'Draft';
    case SalesOrderStatus.Submitted:
      return 'Pending Confirmation';
    case SalesOrderStatus.Confirmed:
      return 'Confirmed';
    case SalesOrderStatus.Fulfilled:
      return 'Fulfilled';
    case SalesOrderStatus.Cancelled:
      return 'Cancelled';
    case SalesOrderStatus.Rejected:
      return 'Rejected';
    default:
      return String(status || 'Draft');
  }
}

export function getSalesOrderStatusSeverity(
  status: SalesOrderStatus | number | string
): 'success' | 'warning' | 'danger' | 'info' | 'neutral' {
  const name = getSalesOrderStatusName(status).toLowerCase();
  switch (name) {
    case 'draft':
      return 'info';
    case 'pending confirmation':
    case 'submitted':
      return 'warning';
    case 'confirmed':
    case 'fulfilled':
      return 'success';
    case 'cancelled':
    case 'rejected':
      return 'danger';
    default:
      return 'neutral';
  }
}

export interface SalesOrderItemDto {
  id?: string;
  salesOrderId?: string;
  productId: string;
  productCode?: string;
  productName?: string;
  unitOfMeasureId?: string | null;
  quantity: number;
  unitPrice: number;
  discountRate?: number;
  discountAmount?: number;
  taxRate?: number;
  taxAmount?: number;
  subTotal?: number;
  taxCodeId?: string | null;
  isTaxInclusive?: boolean;
  lineTotal: number;
  notes?: string | null;
  isActive?: boolean;
}

export interface SalesOrderDto {
  id: string;
  tenantId: string;
  companyId: string;
  branchId: string;
  customerId: string;
  customerName?: string;
  orderNumber: string;
  documentType?: SalesOrderType | number;
  orderDate: string;
  deliveryDate?: string | null;
  currency?: string;
  paymentTerms?: string | null;
  status: SalesOrderStatus | number | string;
  subTotal?: number;
  discountAmount?: number;
  taxAmount?: number;
  totalAmount: number;
  deliveryAddressId?: string | null;
  deliveryAddressSnapshot?: string | null;
  notes?: string | null;
  createdByUserId?: string | null;
  opportunityId?: string | null;
  isActive: boolean;
  items: SalesOrderItemDto[];
}

export interface CreateSalesOrderItemRequest {
  productId: string;
  unitOfMeasureId?: string | null;
  quantity: number;
  unitPrice?: number;
  discountRate?: number;
  taxCodeId?: string | null;
  taxRate?: number;
  isTaxInclusive?: boolean;
  notes?: string | null;
}

export interface CreateSalesOrderRequest {
  orderNumber: string;
  documentType?: SalesOrderType;
  customerId: string;
  orderDate?: string;
  deliveryDate?: string | null;
  currency?: string;
  paymentTerms?: string | null;
  deliveryAddressId?: string | null;
  notes?: string | null;
  opportunityId?: string | null;
  items: CreateSalesOrderItemRequest[];
}

export interface SalesOrderDecisionRequest {
  remarks?: string;
}
