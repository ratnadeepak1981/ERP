export enum PurchaseOrderStatus {
  Draft = 1,
  Submitted = 2,
  Approved = 3,
  Fulfilled = 4,
  Cancelled = 5,
  Rejected = 6
}

export function getPurchaseOrderStatusName(status: PurchaseOrderStatus | number | string): string {
  const num = Number(status);
  switch (num) {
    case PurchaseOrderStatus.Draft:
      return 'Draft';
    case PurchaseOrderStatus.Submitted:
      return 'Submitted';
    case PurchaseOrderStatus.Approved:
      return 'Approved';
    case PurchaseOrderStatus.Fulfilled:
      return 'Fulfilled';
    case PurchaseOrderStatus.Cancelled:
      return 'Cancelled';
    case PurchaseOrderStatus.Rejected:
      return 'Rejected';
    default:
      return String(status || 'Draft');
  }
}

export function getPurchaseOrderStatusSeverity(
  status: PurchaseOrderStatus | number | string
): 'success' | 'warning' | 'danger' | 'info' | 'neutral' {
  const name = getPurchaseOrderStatusName(status).toLowerCase();
  switch (name) {
    case 'draft':
      return 'info';
    case 'submitted':
      return 'warning';
    case 'approved':
    case 'fulfilled':
      return 'success';
    case 'cancelled':
    case 'rejected':
      return 'danger';
    default:
      return 'neutral';
  }
}

export interface PurchaseOrderItemDto {
  id?: string;
  purchaseOrderId?: string;
  productId: string;
  productCode?: string;
  productName?: string;
  unitOfMeasureId?: string | null;
  quantity: number;
  receivedQuantity?: number;
  unitPrice: number;
  lineTotal: number;
  isActive?: boolean;
}

export interface PurchaseOrderDto {
  id: string;
  tenantId: string;
  companyId: string;
  branchId: string;
  supplierId?: string | null;
  supplierName?: string;
  createdByUserId?: string | null;
  orderNumber: string;
  orderDate: string;
  status: PurchaseOrderStatus | number | string;
  totalAmount: number;
  isActive: boolean;
  items: PurchaseOrderItemDto[];
}

export interface CreatePurchaseOrderItemRequest {
  productId: string;
  unitOfMeasureId?: string | null;
  quantity: number;
  unitPrice?: number;
}

export interface CreatePurchaseOrderRequest {
  orderNumber: string;
  supplierId?: string | null;
  orderDate?: string;
  items: CreatePurchaseOrderItemRequest[];
}

export interface ApprovalDecisionRequest {
  remarks?: string;
}

export interface PurchaseOrderHistoryDto {
  id: string;
  purchaseOrderId: string;
  action: string;
  performedByUserId: string;
  performedByUserName?: string;
  performedAt: string;
  remarks?: string;
}
