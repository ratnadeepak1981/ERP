export enum PurchaseRequisitionStatus {
  Draft = 1,
  Submitted = 2,
  Approved = 3,
  Rejected = 4,
  Cancelled = 5
}

export type RequisitionPriority = 'Low' | 'Medium' | 'High' | 'Urgent';

export function getPurchaseRequisitionStatusName(
  status: PurchaseRequisitionStatus | number | string
): string {
  const num = Number(status);
  switch (num) {
    case PurchaseRequisitionStatus.Draft:
      return 'Draft';
    case PurchaseRequisitionStatus.Submitted:
      return 'Submitted';
    case PurchaseRequisitionStatus.Approved:
      return 'Approved';
    case PurchaseRequisitionStatus.Rejected:
      return 'Rejected';
    case PurchaseRequisitionStatus.Cancelled:
      return 'Cancelled';
    default:
      return String(status || 'Draft');
  }
}

export function getPurchaseRequisitionStatusSeverity(
  status: PurchaseRequisitionStatus | number | string
): 'success' | 'warning' | 'danger' | 'info' | 'neutral' {
  const name = getPurchaseRequisitionStatusName(status).toLowerCase();
  switch (name) {
    case 'draft':
      return 'info';
    case 'submitted':
      return 'warning';
    case 'approved':
      return 'success';
    case 'rejected':
    case 'cancelled':
      return 'danger';
    default:
      return 'neutral';
  }
}

export function getPrioritySeverity(
  priority: RequisitionPriority | string | null | undefined
): 'success' | 'warning' | 'danger' | 'info' | 'neutral' {
  const p = (priority || '').toLowerCase();
  switch (p) {
    case 'urgent':
      return 'danger';
    case 'high':
      return 'warning';
    case 'medium':
      return 'info';
    case 'low':
      return 'neutral';
    default:
      return 'neutral';
  }
}

export interface PurchaseRequisitionItemDto {
  id?: string;
  purchaseRequisitionId?: string;
  productId: string;
  productCode?: string;
  productName?: string;
  unitOfMeasureId?: string | null;
  unitOfMeasureCode?: string | null;
  quantity: number;
  estimatedUnitPrice: number;
  estimatedLineTotal: number;
  remarks?: string;
  isActive?: boolean;
  // PR-to-PO Fulfillment tracking
  orderedQuantity?: number;
  remainingQuantity?: number;
  fulfillmentStatus?: 'unfulfilled' | 'partial' | 'fulfilled';
}

export interface PurchaseRequisitionDto {
  id: string;
  tenantId: string;
  companyId: string;
  branchId: string;
  requisitionNumber: string;
  requisitionDate: string;
  requiredDate?: string | null;
  requestorUserId: string;
  requestorUserName?: string;
  status: PurchaseRequisitionStatus | number | string;
  totalAmount: number;
  notes?: string;
  isActive: boolean;
  items: PurchaseRequisitionItemDto[];
  // Optional metadata parsed from notes or payload
  priority?: RequisitionPriority;
  preferredSupplierId?: string | null;
  preferredSupplierName?: string | null;
  // Aggregate fulfillment tracking
  orderedQuantity?: number;
  remainingQuantity?: number;
  isFullyOrdered?: boolean;
}

export interface CreatePurchaseRequisitionItemRequest {
  productId: string;
  unitOfMeasureId?: string | null;
  quantity: number;
  estimatedUnitPrice?: number;
  remarks?: string;
}

export interface CreatePurchaseRequisitionRequest {
  requisitionNumber: string;
  requisitionDate?: string;
  requiredDate?: string;
  notes?: string;
  items: CreatePurchaseRequisitionItemRequest[];
}

export interface UpdatePurchaseRequisitionRequest {
  requisitionDate?: string;
  requiredDate?: string;
  notes?: string;
}

export interface UpdatePurchaseRequisitionItemRequest {
  productId: string;
  unitOfMeasureId?: string | null;
  quantity: number;
  estimatedUnitPrice?: number;
  remarks?: string;
}

export interface ApprovalDecisionRequest {
  remarks?: string;
}

export interface PurchaseRequisitionHistoryDto {
  id: string;
  documentType?: string;
  documentId?: string;
  actorUserId: string;
  actorUserName: string;
  decision: number | string;
  decisionName?: string;
  decisionDateUtc: string;
  isAutomated?: boolean;
  remarks?: string;
  snapshotAmount?: number | null;
}
