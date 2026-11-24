export enum CustomerSalesQuotationStatus {
  Draft = 1,
  Submitted = 2,
  Confirmed = 3,
  RejectedInternal = 4,
  Sent = 5,
  Accepted = 6,
  DeclinedByCustomer = 7,
  Converted = 8,
  Superseded = 9,
  Cancelled = 10
}

export function getCustomerQuotationStatusLabel(status: number): string {
  switch (status) {
    case CustomerSalesQuotationStatus.Draft:
      return 'Draft Request';
    case CustomerSalesQuotationStatus.Submitted:
      return 'Under Commercial Review';
    case CustomerSalesQuotationStatus.Confirmed:
      return 'Proposal Approved';
    case CustomerSalesQuotationStatus.RejectedInternal:
      return 'Declined by Supplier';
    case CustomerSalesQuotationStatus.Sent:
      return 'Formal Proposal Issued';
    case CustomerSalesQuotationStatus.Accepted:
      return 'Accepted by Customer';
    case CustomerSalesQuotationStatus.DeclinedByCustomer:
      return 'Declined by Customer';
    case CustomerSalesQuotationStatus.Converted:
      return 'Converted to Sales Order';
    case CustomerSalesQuotationStatus.Superseded:
      return 'Superseded by Revision';
    case CustomerSalesQuotationStatus.Cancelled:
      return 'Cancelled';
    default:
      return 'Pending Review';
  }
}

export function getCustomerQuotationStatusSeverity(status: number): 'active' | 'warning' | 'info' | 'success' | 'danger' {
  switch (status) {
    case CustomerSalesQuotationStatus.Draft:
      return 'info';
    case CustomerSalesQuotationStatus.Submitted:
      return 'warning';
    case CustomerSalesQuotationStatus.Confirmed:
    case CustomerSalesQuotationStatus.Sent:
      return 'active';
    case CustomerSalesQuotationStatus.Accepted:
    case CustomerSalesQuotationStatus.Converted:
      return 'success';
    case CustomerSalesQuotationStatus.RejectedInternal:
    case CustomerSalesQuotationStatus.DeclinedByCustomer:
    case CustomerSalesQuotationStatus.Cancelled:
      return 'danger';
    default:
      return 'info';
  }
}

export interface CustomerQuotationItemDto {
  id: string;
  salesQuotationId: string;
  productId: string;
  productCode?: string;
  productName?: string;
  unitOfMeasureId?: string | null;
  unitOfMeasureCode?: string | null;
  quantity: number;
  unitPrice: number;
  discountRate: number;
  discountAmount: number;
  taxCodeId?: string | null;
  taxCodeName?: string | null;
  taxRate: number;
  taxAmount: number;
  subTotal: number;
  lineTotal: number;
  isTaxInclusive: boolean;
  notes?: string | null;
  isActive: boolean;
}

export interface CustomerSalesQuotationDto {
  id: string;
  tenantId: string;
  companyId: string;
  branchId: string;
  customerId: string;
  customerName?: string;
  customerCode?: string;
  customerContactId?: string | null;
  contactPersonName?: string | null;
  quotationNumber: string;
  revisionNumber: number;
  documentType: number;
  quotationDate: string;
  validUntilDateUtc: string;
  isExpired: boolean;
  daysUntilExpiry: number;
  deliveryDate?: string | null;
  currency: string;
  paymentTerms?: string | null;
  deliveryTerms?: string | null;
  status: CustomerSalesQuotationStatus;
  subTotal: number;
  discountAmount: number;
  taxAmount: number;
  totalAmount: number;
  notes?: string | null;
  customerRemarks?: string | null;
  deliveryAddressId?: string | null;
  deliveryAddressSnapshot?: string | null;
  sentAtUtc?: string | null;
  acceptedAtUtc?: string | null;
  declinedAtUtc?: string | null;
  declinedRemarks?: string | null;
  convertedSalesOrderId?: string | null;
  convertedAtUtc?: string | null;
  items: CustomerQuotationItemDto[];
}

export interface CreateCustomerQuotationItemRequest {
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

export interface CreateCustomerQuotationRequest {
  customerId?: string;
  customerContactId?: string | null;
  customerRemarks?: string | null;
  requiredDeliveryDate?: string | null;
  currency: string;
  deliveryAddressId?: string | null;
  deliveryAddressSnapshot?: string | null;
  customerReference?: string | null;
  deliveryInstructions?: string | null;
  projectReference?: string | null;
  idempotencyKey?: string | null;
  isDraft: boolean;
  items: CreateCustomerQuotationItemRequest[];
}

export interface AcceptQuotationRequest {
  deliveryAddressId?: string | null;
  customerRemarks?: string | null;
  idempotencyKey?: string | null;
}

export interface DeclineQuotationRequest {
  remarks: string;
}

