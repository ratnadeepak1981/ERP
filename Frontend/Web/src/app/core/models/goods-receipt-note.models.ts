export enum GoodsReceiptNoteStatus {
  Draft = 1,
  Confirmed = 2,
  Cancelled = 3
}

export function getGrnStatusName(status: GoodsReceiptNoteStatus | number | string): string {
  const num = Number(status);
  switch (num) {
    case GoodsReceiptNoteStatus.Draft:
      return 'Draft';
    case GoodsReceiptNoteStatus.Confirmed:
      return 'Confirmed';
    case GoodsReceiptNoteStatus.Cancelled:
      return 'Cancelled';
    default:
      return String(status || 'Draft');
  }
}

export function getGrnStatusSeverity(
  status: GoodsReceiptNoteStatus | number | string
): 'success' | 'warning' | 'danger' | 'info' | 'neutral' {
  const name = getGrnStatusName(status).toLowerCase();
  switch (name) {
    case 'draft':
      return 'info';
    case 'confirmed':
      return 'success';
    case 'cancelled':
      return 'danger';
    default:
      return 'neutral';
  }
}

export interface GoodsReceiptNoteLineDto {
  goodsReceiptNoteLineId?: string;
  goodsReceiptNoteId?: string;
  purchaseOrderItemId: string;
  productId: string;
  productCode?: string;
  productName?: string;
  warehouseId?: string;
  warehouseLocationId?: string;
  quantityReceived: number;
  unitCost: number; // Server-authoritative unit price (strictly read-only on client)
  lineTotal: number; // Server-authoritative line total
  unitOfMeasureId?: string | null;
  unitOfMeasure?: string;
  batchNumber?: string | null;
  serialNumber?: string | null;
  expiryDate?: string | null;
  isQuarantine?: boolean;
  remarks?: string | null;
}

export interface GoodsReceiptNoteDto {
  goodsReceiptNoteId: string;
  tenantId: string;
  companyId: string;
  branchId: string;
  purchaseOrderId: string;
  supplierId?: string | null;
  supplierName?: string;
  grnNumber: string;
  receiptDate: string;
  status: GoodsReceiptNoteStatus | number | string;
  deliveryNoteNumber?: string | null;
  remarks?: string | null;
  totalAmount: number;
  lines: GoodsReceiptNoteLineDto[];
}

export interface CreateGoodsReceiptNoteLineRequest {
  purchaseOrderItemId: string;
  productId: string;
  warehouseId?: string;
  warehouseLocationId?: string;
  quantityReceived: number;
  unitOfMeasureId?: string | null;
  batchNumber?: string | null;
  serialNumber?: string | null;
  expiryDate?: string | null;
  isQuarantine?: boolean;
  remarks?: string | null;
}

export interface CreateGoodsReceiptNoteRequest {
  purchaseOrderId: string;
  grnNumber?: string;
  receiptDate?: string;
  deliveryNoteNumber?: string | null;
  remarks?: string | null;
  lines: CreateGoodsReceiptNoteLineRequest[];
}

export interface UpdateGoodsReceiptNoteRequest {
  receiptDate?: string;
  deliveryNoteNumber?: string | null;
  remarks?: string | null;
  lines: CreateGoodsReceiptNoteLineRequest[];
}
