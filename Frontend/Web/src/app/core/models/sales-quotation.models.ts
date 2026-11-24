export enum SalesQuotationStatus {
  Draft = 1,
  Submitted = 2,
  Confirmed = 3,
  RejectedInternal = 4,
  Sent = 5,
  Accepted = 6,
  DeclinedByCustomer = 7,
  Converted = 8,
  Superseded = 9,
  Expired = 10,
  Cancelled = 11
}

export enum SalesQuotationType {
  Standard = 1,
  CustomerRequest = 2,
  CustomB2B = 3
}

export function getSalesQuotationStatusName(status: SalesQuotationStatus | number | string): string {
  const num = Number(status);
  switch (num) {
    case SalesQuotationStatus.Draft:
      return 'Draft';
    case SalesQuotationStatus.Submitted:
      return 'Pending Approval';
    case SalesQuotationStatus.Confirmed:
      return 'Approved (Internal)';
    case SalesQuotationStatus.RejectedInternal:
      return 'Rejected (Internal)';
    case SalesQuotationStatus.Sent:
      return 'Issued to Customer';
    case SalesQuotationStatus.Accepted:
      return 'Accepted by Customer';
    case SalesQuotationStatus.DeclinedByCustomer:
      return 'Declined by Customer';
    case SalesQuotationStatus.Converted:
      return 'Converted to Order';
    case SalesQuotationStatus.Superseded:
      return 'Superseded';
    case SalesQuotationStatus.Expired:
      return 'Expired';
    case SalesQuotationStatus.Cancelled:
      return 'Cancelled';
    default:
      return String(status || 'Draft');
  }
}

export function getSalesQuotationStatusSeverity(
  status: SalesQuotationStatus | number | string
): 'success' | 'warning' | 'danger' | 'info' | 'neutral' {
  const num = Number(status);
  switch (num) {
    case SalesQuotationStatus.Draft:
      return 'info';
    case SalesQuotationStatus.Submitted:
      return 'warning';
    case SalesQuotationStatus.Confirmed:
    case SalesQuotationStatus.Sent:
    case SalesQuotationStatus.Accepted:
    case SalesQuotationStatus.Converted:
      return 'success';
    case SalesQuotationStatus.RejectedInternal:
    case SalesQuotationStatus.DeclinedByCustomer:
    case SalesQuotationStatus.Cancelled:
    case SalesQuotationStatus.Expired:
      return 'danger';
    case SalesQuotationStatus.Superseded:
    default:
      return 'neutral';
  }
}

export function getSalesQuotationEmailStatusSeverity(
  status?: string | null
): 'success' | 'warning' | 'danger' | 'info' | 'neutral' {
  if (!status) return 'neutral';
  const s = status.toLowerCase();
  switch (s) {
    case 'sent':
      return 'success';
    case 'pending':
      return 'warning';
    case 'failed':
      return 'danger';
    case 'notsent':
    default:
      return 'neutral';
  }
}

export interface OriginalCustomerRequestItemDto {
  productId: string;
  productCode?: string;
  productName?: string;
  requestedQuantity: number;
  targetUnitPrice?: number | null;
  unitOfMeasureCode?: string | null;
  notes?: string | null;
}

export interface OriginalCustomerRequestDto {
  customerRemarks?: string | null;
  deliveryAddressSnapshot?: string | null;
  requiredDeliveryDate?: string | null;
  items: OriginalCustomerRequestItemDto[];
}

export interface SalesQuotationItemDto {
  id?: string;
  salesQuotationId?: string;
  productId: string;
  productCode?: string;
  productName?: string;
  unitOfMeasureId?: string | null;
  unitOfMeasureCode?: string | null;
  quantity: number;
  unitPrice: number;
  unitCost?: number | null;
  grossMarginPercentage?: number | null;
  discountRate?: number;
  discountAmount?: number;
  taxCodeId?: string | null;
  taxCodeName?: string | null;
  taxRate?: number;
  taxAmount?: number;
  subTotal?: number;
  lineTotal: number;
  isTaxInclusive?: boolean;
  notes?: string | null;
  isActive?: boolean;
}

export interface SalesQuotationDto {
  id: string;
  tenantId: string;
  companyId: string;
  branchId: string;
  customerId: string;
  customerName?: string;
  customerCode?: string;
  customerContactId?: string | null;
  contactPersonName?: string | null;
  contactEmail?: string | null;
  quotationNumber: string;
  revisionNumber: number;
  documentType: SalesQuotationType | number;
  quotationDate: string;
  validUntilDateUtc: string;
  daysUntilExpiry: number;
  isExpired: boolean;
  deliveryDate?: string | null;
  currency: string;
  transactionCurrencyId?: string | null;
  baseCurrencyId?: string | null;
  baseCurrencyCode?: string | null;
  baseCurrencyDecimalPlaces?: number | null;
  appliedExchangeRate?: number | null;
  priceCardId?: string | null;
  paymentTerms?: string | null;
  deliveryTerms?: string | null;
  status: SalesQuotationStatus | number | string;
  subTotal: number;
  discountAmount: number;
  taxAmount: number;
  totalAmount: number;
  baseSubTotal?: number | null;
  baseDiscountAmount?: number | null;
  baseTaxAmount?: number | null;
  baseTotalAmount?: number | null;
  notes?: string | null;
  customerRemarks?: string | null;
  internalNotes?: string | null;
  deliveryAddressId?: string | null;
  deliveryAddressSnapshot?: string | null;
  submittedByUserId?: string | null;
  submittedAtUtc?: string | null;
  approvedByUserId?: string | null;
  approvedAtUtc?: string | null;
  rejectedByUserId?: string | null;
  rejectedAtUtc?: string | null;
  rejectionRemarks?: string | null;
  sentAtUtc?: string | null;
  acceptedAtUtc?: string | null;
  declinedAtUtc?: string | null;
  declinedRemarks?: string | null;
  convertedSalesOrderId?: string | null;
  convertedAtUtc?: string | null;
  supersededByQuotationId?: string | null;

  originalRequest?: OriginalCustomerRequestDto | null;
  emailStatus?: string | null;
  emailSentAtUtc?: string | null;
  emailRecipient?: string | null;
  emailFailureReason?: string | null;
  totalCost?: number | null;
  grossMarginPercentage?: number | null;

  items: SalesQuotationItemDto[];
}

export interface CreateSalesQuotationItemRequest {
  productId: string;
  unitOfMeasureId?: string | null;
  quantity: number;
  unitPrice: number;
  discountRate?: number;
  taxCodeId?: string | null;
  taxRate?: number;
  isTaxInclusive?: boolean;
  notes?: string | null;
}

export interface CreateSalesQuotationDraftRequest {
  customerId: string;
  customerContactId?: string | null;
  quotationDate?: string | null;
  validUntilDateUtc?: string | null;
  deliveryDate?: string | null;
  currency?: string;
  paymentTerms?: string | null;
  deliveryTerms?: string | null;
  notes?: string | null;
  internalNotes?: string | null;
  deliveryAddressId?: string | null;
  deliveryAddressSnapshot?: string | null;
  priceCardId?: string | null;
  items?: CreateSalesQuotationItemRequest[];
}

export interface UpdateSalesQuotationDraftRequest {
  quotationDate?: string | null;
  validUntilDateUtc?: string | null;
  deliveryDate?: string | null;
  currency?: string | null;
  paymentTerms?: string | null;
  deliveryTerms?: string | null;
  notes?: string | null;
  internalNotes?: string | null;
  deliveryAddressId?: string | null;
  deliveryAddressSnapshot?: string | null;
  items?: CreateSalesQuotationItemRequest[];
}

export interface ReturnToDraftRequest {
  remarks?: string | null;
}

export interface QuotationDecisionRequest {
  remarks?: string | null;
}

export interface ApprovalAuditRecord {
  id: string;
  entityType: string;
  entityId: string;
  action: string;
  actorUserId: string;
  actorUserName: string;
  amount?: number | null;
  remarks?: string | null;
  createdAt: string;
}
