export enum SalesInvoiceStatus {
  Draft = 1,
  Confirmed = 2,
  Cancelled = 3,
  Paid = 4
}

export function getSalesInvoiceStatusName(status: SalesInvoiceStatus | number): string {
  switch (Number(status)) {
    case SalesInvoiceStatus.Draft:
      return 'Draft';
    case SalesInvoiceStatus.Confirmed:
      return 'Confirmed';
    case SalesInvoiceStatus.Cancelled:
      return 'Cancelled';
    case SalesInvoiceStatus.Paid:
      return 'Paid';
    default:
      return 'Unknown';
  }
}

export function getSalesInvoiceStatusSeverity(
  status: SalesInvoiceStatus | number
): 'info' | 'success' | 'warning' | 'danger' | 'neutral' {
  switch (Number(status)) {
    case SalesInvoiceStatus.Draft:
      return 'info';
    case SalesInvoiceStatus.Confirmed:
      return 'success';
    case SalesInvoiceStatus.Cancelled:
      return 'danger';
    case SalesInvoiceStatus.Paid:
      return 'success';
    default:
      return 'neutral';
  }
}

export interface SalesInvoiceItemDto {
  id: string;
  salesInvoiceId: string;
  salesOrderItemId?: string | null;
  salesShipmentItemId?: string | null;
  productId: string;
  productCode: string;
  productName: string;
  quantity: number;
  unitPrice: number;
  discountRate: number;
  discountAmount: number;
  taxRate: number;
  taxAmount: number;
  subtotal: number;
  lineTotal: number;
  unitOfMeasureId?: string | null;
  notes?: string | null;
}

export interface SalesInvoiceDto {
  id: string;
  tenantId: string;
  companyId: string;
  branchId: string;
  salesOrderId: string;
  orderNumber: string;
  salesShipmentId?: string | null;
  shipmentNumber?: string | null;
  customerId: string;
  customerName: string;
  invoiceNumber: string;
  invoiceDate: string;
  dueDate?: string | null;
  status: SalesInvoiceStatus | number;
  currency: string;
  paymentTerms?: string | null;
  subtotal: number;
  discountAmount: number;
  taxAmount: number;
  totalAmount: number;
  notes?: string | null;
  confirmedAt?: string | null;
  confirmedBy?: string | null;
  items: SalesInvoiceItemDto[];
}

export interface CreateSalesInvoiceItemRequest {
  productId: string;
  quantity: number;
  unitPrice: number;
  discountRate: number;
  taxRate: number;
  salesOrderItemId?: string | null;
  salesShipmentItemId?: string | null;
  unitOfMeasureId?: string | null;
  notes?: string | null;
}

export interface UpdateSalesInvoiceRequest {
  invoiceDate?: string | null;
  dueDate?: string | null;
  paymentTerms?: string | null;
  notes?: string | null;
  items?: CreateSalesInvoiceItemRequest[];
}

export interface ConfirmSalesInvoiceRequest {
  notes?: string | null;
}
