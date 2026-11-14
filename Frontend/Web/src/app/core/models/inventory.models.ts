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
  warehouseLocationId?: string | null;
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
  stockIssueLineId?: string | null;
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
  warehouseLocationId?: string | null;
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

export interface StockByLocationReportQuery {
  warehouseId?: string | null;
  companyId?: string | null;
  branchId?: string | null;
  locationId?: string | null;
  productId?: string | null;
  categoryId?: string | null;
  locationClassification?: 'storage' | 'staging' | string | null;
  search?: string | null;
  page?: number | null;
  pageSize?: number | null;
  sortBy?: string | null;
  sortDirection?: 'asc' | 'desc' | string | null;
}

export interface StockByLocationRowDto {
  inventoryBalanceId: string;
  productId: string;
  productCode: string;
  productName: string;
  sku: string;
  categoryId?: string | null;
  categoryName?: string | null;
  unitOfMeasure: string;

  warehouseId: string;
  warehouseCode: string;
  warehouseName: string;

  warehouseLocationId: string;
  locationCode: string;
  locationName: string;
  zoneCode?: string | null;
  zoneName?: string | null;
  locationTypeCode?: string | null;
  locationTypeName?: string | null;
  hierarchyPath: string;
  locationClassification: string; // 'Storage' | 'Staging'

  batchNumber: string;
  serialNumber: string;

  quantityOnHand: number;
  quantityAllocated: number;
  quantityQuarantine: number;
  quantityAvailable: number;

  unitCost: number;
  totalCostValue: number;
  valuationMethod: string;
  currency: string;
}

export interface StockByLocationReportDto {
  items: StockByLocationRowDto[];
  totalCount: number;
  totalQuantityOnHand: number;
  totalQuantityAvailable: number;
  totalStockValue: number;
  currency: string;
  page: number;
  pageSize: number;
}

export interface StockLedgerReportQuery {
  productId: string;
  warehouseId: string;
  warehouseLocationId?: string | null;
  companyId?: string | null;
  branchId?: string | null;
  dateFrom?: string | null;
  dateTo?: string | null;
  page?: number | null;
  pageSize?: number | null;
  search?: string | null;
}

export interface StockLedgerRowDto {
  inventoryTransactionId: string;
  transactionDate: string;
  postingDate: string;
  transactionType: string;
  movementTypeCode: number;
  documentReference: string;
  sourceDocumentType: string;
  sourceDocumentId: string;
  sourceDocumentLineId?: string | null;
  productCode: string;
  productName: string;
  unitOfMeasure: string;
  warehouseCode: string;
  warehouseName: string;
  locationCode: string;
  locationName: string;
  sourceLocationCode?: string | null;
  destinationLocationCode?: string | null;
  batchNumber?: string | null;
  serialNumber?: string | null;

  quantityIn: number;
  quantityOut: number;
  costRate: number;
  inwardValue: number;
  outwardValue: number;
  runningQuantity: number;
  runningValue: number;
  valuationMethod: string;
  currency: string;
}

export interface StockLedgerReportDto {
  productId: string;
  productCode: string;
  productName: string;
  unitOfMeasure: string;
  warehouseId: string;
  warehouseCode: string;
  warehouseName: string;
  warehouseLocationId?: string | null;
  locationCode: string;
  locationName: string;
  hierarchyPath: string;
  valuationMethod: string;
  currency: string;

  // Opening Balance
  openingQuantity: number;
  openingValue: number;

  // Period Totals
  totalQuantityIn: number;
  totalInwardValue: number;
  totalQuantityOut: number;
  totalOutwardValue: number;

  // Closing Balance
  closingQuantity: number;
  closingValue: number;

  // Detail Rows
  items: StockLedgerRowDto[];
  totalCount: number;
  page: number;
  pageSize: number;
}



export function getMovementTypeDisplayName(movementType: string): string {
  switch (movementType) {
    case 'GoodsReceipt':
    case 'Goods Receipt (GRN)':
      return 'Goods Receipt (GRN)';
    case 'SupplierReturn':
    case 'Supplier Return':
      return 'Supplier Return';
    case 'InternalTransferIn':
    case 'Transfer In':
      return 'Transfer In';
    case 'Put Away In':
      return 'Put Away In';
    case 'InternalTransferOut':
    case 'Transfer Out':
      return 'Transfer Out';
    case 'Put Away Out':
      return 'Put Away Out';
    case 'StockAdjustmentIn':
    case 'Stock Adjustment (+)':
      return 'Adjustment In';
    case 'StockAdjustmentOut':
    case 'Stock Adjustment (-)':
      return 'Adjustment Out';
    case 'ProductionReceipt':
    case 'Production Receipt':
      return 'Production Receipt';
    case 'ProductionIssue':
    case 'Production Issue':
      return 'Production Issue';
    case 'ShipmentDispatch':
    case 'Sales Dispatch':
      return 'Sales Dispatch';
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
    case 'Goods Receipt (GRN)':
    case 'ProductionReceipt':
    case 'Production Receipt':
    case 'StockAdjustmentIn':
    case 'Stock Adjustment (+)':
    case 'InternalTransferIn':
    case 'Transfer In':
    case 'Put Away In':
      return 'success';
    case 'ShipmentDispatch':
    case 'Sales Dispatch':
    case 'ProductionIssue':
    case 'Production Issue':
    case 'StockAdjustmentOut':
    case 'Stock Adjustment (-)':
    case 'InternalTransferOut':
    case 'Transfer Out':
    case 'Put Away Out':
      return 'info';
    case 'SupplierReturn':
    case 'Supplier Return':
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
  stockReceiptLineId?: string | null;
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
  warehouseLocationId?: string | null;
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
  manufacturingDate?: string | null;
  expiryDate?: string | null;
  sourceDocumentReference?: string | null;
  remarks?: string | null;
  createdAt: string;
}

export interface CreateStockReceiptRequest {
  stockReceiptId?: string | null;
  receiptDate?: string | null;
  reason: StockReceiptReason;
  warehouseId: string;
  sourceDocumentReference?: string | null;
  remarks?: string | null;
  lines: CreateStockReceiptLineRequest[];
}

export interface CreateStockReceiptLineRequest {
  stockReceiptLineId?: string | null;
  productId: string;
  warehouseLocationId: string;
  quantity: number;
  unitCost?: number | null;
  batchNumber?: string | null;
  serialNumber?: string | null;
  manufacturingDate?: string | null;
  expiryDate?: string | null;
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

