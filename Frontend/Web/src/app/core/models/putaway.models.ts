export enum PutawayStatus {
  Draft = 1,
  Completed = 2,
  Cancelled = 3
}

export function getPutawayStatusName(status: PutawayStatus | number | string): string {
  const s = Number(status);
  switch (s) {
    case PutawayStatus.Draft:
      return 'Draft';
    case PutawayStatus.Completed:
      return 'Completed';
    case PutawayStatus.Cancelled:
      return 'Cancelled';
    default:
      return 'Draft';
  }
}

export function getPutawayStatusSeverity(status: PutawayStatus | number | string): 'info' | 'success' | 'warning' | 'danger' | 'neutral' {
  const s = Number(status);
  switch (s) {
    case PutawayStatus.Draft:
      return 'warning';
    case PutawayStatus.Completed:
      return 'success';
    case PutawayStatus.Cancelled:
      return 'danger';
    default:
      return 'neutral';
  }
}

export type PutawaySourceDocType = 'StockReceipt' | 'GoodsReceipt' | 'Manual';

export interface AvailableReceiptOption {
  id: string;
  number: string;
  type: PutawaySourceDocType;
  date: string;
  warehouseId: string;
  warehouseName: string;
  lineCount: number;
  outstandingQuantity: number;
  companyId?: string;
  branchId?: string;
}

export interface PutawayLineAllocationDto {
  allocationId?: string;
  putawayLineId?: string;
  destinationLocationId: string;
  destinationLocationCode?: string;
  destinationLocationName?: string;
  allocatedQuantity: number;
  movementSequence?: number;
  inventoryTransactionId?: string;
}

export interface PutawayLineDto {
  lineId: string;
  sourceLineId?: string;
  productId: string;
  productCode: string;
  productName: string;
  unitOfMeasure: string;
  sourceQuantity: number;
  alreadyPutawayQuantity: number;
  outstandingQuantity: number;
  putawayQuantity: number;
  sourceLocationId?: string;
  sourceLocationCode?: string;
  destinationLocationId: string;
  destinationLocationCode: string;
  destinationLocationName?: string;
  batchNumber?: string | null;
  serialNumber?: string | null;
  expiryDate?: string | null;
  notes?: string;
  allocations?: PutawayLineAllocationDto[];
  selected?: boolean;
}

export interface PutawayRecordDto {
  putawayId: string;
  putawayNumber: string;
  putawayDate: string;
  sourceDocumentType: PutawaySourceDocType;
  sourceDocumentId?: string;
  sourceDocumentNumber?: string;
  warehouseId: string;
  warehouseCode: string;
  warehouseName: string;
  companyId?: string;
  branchId?: string;
  status: PutawayStatus;
  statusName: string;
  totalLines: number;
  totalQuantity: number;
  notes?: string;
  lines: PutawayLineDto[];
  createdAt: string;
}

export interface CreatePutawayAllocationInput {
  destinationWarehouseLocationId: string;
  allocatedQuantity: number;
}

export interface CreatePutawayLineInput {
  sourceLineId?: string;
  productId: string;
  productCode?: string;
  productName?: string;
  unitOfMeasure?: string;
  sourceQuantity: number;
  alreadyPutawayQuantity: number;
  outstandingQuantity: number;
  putawayQuantity: number;
  sourceLocationId?: string;
  sourceLocationCode?: string;
  destinationLocationId?: string;
  destinationWarehouseLocationId?: string;
  batchNumber?: string | null;
  serialNumber?: string | null;
  expiryDate?: string | null;
  notes?: string;
  allocations?: CreatePutawayAllocationInput[];
}

export interface CreatePutawayRequest {
  putawayNumber?: string;
  putawayDate: string;
  sourceDocumentType: PutawaySourceDocType;
  sourceDocumentId?: string;
  sourceDocumentNumber?: string;
  warehouseId: string;
  companyId?: string;
  branchId?: string;
  notes?: string;
  lines: CreatePutawayLineInput[];
}
