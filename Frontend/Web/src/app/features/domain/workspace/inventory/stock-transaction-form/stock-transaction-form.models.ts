import {
  StockIssueDto,
  StockIssuePurpose,
  StockReceiptDto,
  StockReceiptReason
} from '../../../../../core/models/inventory.models';

export type StockTransactionType = 'receipt' | 'issue';
export type StockTransactionMode = 'create' | 'view';

export interface StockTransactionLineItem {
  id: string;
  productId: string;
  productCode: string;
  productName: string;
  unitOfMeasure: string;
  warehouseId: string;
  warehouseLocationId: string;
  availableStock: number;
  loadingStock: boolean;
  quantity: number;
  unitCost?: number | null;
  batchNumber?: string;
  serialNumber?: string;
  remarks?: string;
}

export interface ReasonOrPurposeOption {
  label: string;
  value: number;
  description: string;
  badgeSeverity: 'success' | 'warning' | 'info' | 'danger' | 'neutral';
}
