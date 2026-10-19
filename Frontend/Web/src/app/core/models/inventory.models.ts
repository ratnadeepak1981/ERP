export interface InventorySummaryDto {
  totalStockItems: number;
  totalWarehouses: number;
  totalLocations: number;
  lowStockItems: number;
  totalTransactions: number;
  totalOnHandQuantity: number;
  totalInventoryValue: number;
}

export interface InventoryRecentTransactionDto {
  inventoryTransactionId: string;
  transactionNumber: string;
  movementType: string;
  movementTypeCode: number;
  sourceDocumentType: string;
  sourceDocumentId: string;
  productId: string;
  productCode: string;
  productName: string;
  unitOfMeasure: string;
  warehouseId: string;
  warehouseCode: string;
  warehouseName: string;
  warehouseLocationId: string;
  locationCode: string;
  locationName: string;
  quantity: number;
  unitCost: number;
  totalCost: number;
  batchNumber?: string | null;
  serialNumber?: string | null;
  transactionDate: string;
  createdAt: string;
}

export enum StockIssuePurpose {
  InternalUse = 1,
  SalesDispatch = 2,
  Manufacturing = 3,
  Other = 4
}

export enum StockIssueStatus {
  Draft = 1,
  Posted = 2,
  Cancelled = 3
}

export interface StockIssueDto {
  stockIssueId: string;
  issueNumber: string;
  issueDate: string;
  purpose: StockIssuePurpose;
  purposeName: string;
  movementType: string;
  status: StockIssueStatus;
  statusName: string;
  warehouseId: string;
  warehouseCode: string;
  warehouseName: string;
  warehouseLocationId: string;
  locationCode: string;
  locationName: string;
  productId: string;
  productCode: string;
  productName: string;
  unitOfMeasure: string;
  quantity: number;
  unitCost: number;
  totalCost: number;
  batchNumber?: string | null;
  serialNumber?: string | null;
  sourceDocumentReference?: string | null;
  remarks?: string | null;
  createdAt: string;
}

export interface AvailableStockDto {
  productId: string;
  warehouseLocationId: string;
  quantityOnHand: number;
  quantityAllocated: number;
  quantityQuarantine: number;
  quantityAvailable: number;
}

export interface CreateStockIssueLineRequest {
  productId: string;
  warehouseLocationId: string;
  quantity: number;
  batchNumber?: string | null;
  serialNumber?: string | null;
  remarks?: string | null;
}

export interface CreateStockIssueRequest {
  issueDate?: string | null;
  purpose: StockIssuePurpose;
  warehouseId: string;
  sourceDocumentReference?: string | null;
  remarks?: string | null;
  lines: CreateStockIssueLineRequest[];
}

export function getStockIssuePurposeName(purpose: StockIssuePurpose | number): string {
  switch (Number(purpose)) {
    case StockIssuePurpose.SalesDispatch:
      return 'Sales / Customer Dispatch';
    case StockIssuePurpose.Manufacturing:
      return 'Manufacturing / Material Issue';
    case StockIssuePurpose.InternalUse:
      return 'Internal Use / Maintenance';
    case StockIssuePurpose.Other:
      return 'Other Authorized Purpose';
    default:
      return 'Internal Use';
  }
}

export function getStockIssuePurposeSeverity(
  purpose: StockIssuePurpose | number
): 'success' | 'warning' | 'danger' | 'info' | 'neutral' {
  switch (Number(purpose)) {
    case StockIssuePurpose.SalesDispatch:
      return 'info';
    case StockIssuePurpose.Manufacturing:
      return 'warning';
    case StockIssuePurpose.InternalUse:
      return 'success';
    default:
      return 'neutral';
  }
}

export function getStockIssueStatusSeverity(
  status: StockIssueStatus | number
): 'success' | 'warning' | 'danger' | 'info' | 'neutral' {
  switch (Number(status)) {
    case StockIssueStatus.Posted:
      return 'success';
    case StockIssueStatus.Draft:
      return 'info';
    case StockIssueStatus.Cancelled:
      return 'danger';
    default:
      return 'neutral';
  }
}

export function getMovementTypeDisplayName(movementType: string): string {
  switch (movementType) {
    case 'GoodsReceipt':
      return 'Goods Receipt';
    case 'SupplierReturn':
      return 'Supplier Return';
    case 'InternalTransferIn':
      return 'Transfer In';
    case 'InternalTransferOut':
      return 'Transfer Out';
    case 'StockAdjustmentIn':
      return 'Adjustment In';
    case 'StockAdjustmentOut':
      return 'Adjustment Out';
    case 'ProductionReceipt':
      return 'Production Receipt';
    case 'ProductionIssue':
      return 'Production Issue';
    case 'ShipmentDispatch':
      return 'Shipment Dispatch';
    case 'Reversal':
      return 'Reversal';
    default:
      return movementType || 'Movement';
  }
}

export function getMovementTypeBadgeSeverity(
  movementType: string
): 'success' | 'warning' | 'danger' | 'info' | 'neutral' {
  switch (movementType) {
    case 'GoodsReceipt':
    case 'ProductionReceipt':
    case 'StockAdjustmentIn':
    case 'InternalTransferIn':
      return 'success';
    case 'ShipmentDispatch':
    case 'ProductionIssue':
    case 'StockAdjustmentOut':
      return 'info';
    case 'SupplierReturn':
      return 'warning';
    case 'Reversal':
      return 'danger';
    default:
      return 'neutral';
  }
}

export enum StockReceiptReason {
  OpeningBalance = 1,
  CustomerReturn = 2,
  InternalProduction = 3,
  Miscellaneous = 4
}

export enum StockReceiptStatus {
  Draft = 1,
  Posted = 2,
  Cancelled = 3
}

export interface StockReceiptDto {
  stockReceiptId: string;
  receiptNumber: string;
  receiptDate: string;
  reason: StockReceiptReason;
  reasonName: string;
  movementType: string;
  status: StockReceiptStatus;
  statusName: string;
  warehouseId: string;
  warehouseCode: string;
  warehouseName: string;
  warehouseLocationId: string;
  locationCode: string;
  locationName: string;
  productId: string;
  productCode: string;
  productName: string;
  unitOfMeasure: string;
  quantity: number;
  unitCost: number;
  totalCost: number;
  batchNumber?: string | null;
  serialNumber?: string | null;
  sourceDocumentReference?: string | null;
  remarks?: string | null;
  createdAt: string;
}

export interface CreateStockReceiptRequest {
  receiptDate?: string | null;
  reason: StockReceiptReason;
  warehouseId: string;
  sourceDocumentReference?: string | null;
  remarks?: string | null;
  lines: CreateStockReceiptLineRequest[];
}

export interface CreateStockReceiptLineRequest {
  productId: string;
  warehouseLocationId: string;
  quantity: number;
  unitCost?: number | null;
  batchNumber?: string | null;
  serialNumber?: string | null;
  remarks?: string | null;
}

export function getStockReceiptReasonName(reason: StockReceiptReason | number): string {
  switch (Number(reason)) {
    case StockReceiptReason.OpeningBalance:
      return 'Opening / Initial Balance';
    case StockReceiptReason.CustomerReturn:
      return 'Customer Sales Return';
    case StockReceiptReason.InternalProduction:
      return 'Internal Production / Assembly';
    case StockReceiptReason.Miscellaneous:
      return 'Miscellaneous Inbound';
    default:
      return 'General Receipt';
  }
}

export function getStockReceiptReasonSeverity(
  reason: StockReceiptReason | number
): 'success' | 'warning' | 'danger' | 'info' | 'neutral' {
  switch (Number(reason)) {
    case StockReceiptReason.OpeningBalance:
      return 'info';
    case StockReceiptReason.CustomerReturn:
      return 'warning';
    case StockReceiptReason.InternalProduction:
      return 'success';
    case StockReceiptReason.Miscellaneous:
      return 'neutral';
    default:
      return 'info';
  }
}

export function getStockReceiptStatusSeverity(
  status: StockReceiptStatus | number
): 'success' | 'warning' | 'danger' | 'info' | 'neutral' {
  switch (Number(status)) {
    case StockReceiptStatus.Posted:
      return 'success';
    case StockReceiptStatus.Draft:
      return 'warning';
    case StockReceiptStatus.Cancelled:
      return 'danger';
    default:
      return 'neutral';
  }
}
