export type RequestForQuotationStatus = 'Draft' | 'Issued' | 'Closed' | 'Awarded' | 'Cancelled';
export type RFQSupplierInvitationStatus = 'Pending' | 'Sending' | 'Sent' | 'Failed' | 'Opened' | 'Submitted' | 'Declined';
export type SupplierQuotationStatus = 'Draft' | 'Submitted' | 'UnderReview' | 'Approved' | 'Rejected' | 'Withdrawn';

export function getRfqStatusName(status: any): RequestForQuotationStatus {
  if (typeof status === 'string') {
    const s = status.trim();
    if (s === 'Draft' || s === 'Issued' || s === 'Closed' || s === 'Awarded' || s === 'Cancelled') {
      return s as RequestForQuotationStatus;
    }
  }
  switch (Number(status)) {
    case 1: return 'Draft';
    case 2: return 'Issued';
    case 3: return 'Closed';
    case 4: return 'Awarded';
    case 5: return 'Cancelled';
    default: return 'Draft';
  }
}

export function getRfqStatusSeverity(status: any): 'neutral' | 'info' | 'warning' | 'success' | 'danger' {
  const name = getRfqStatusName(status);
  switch (name) {
    case 'Draft': return 'neutral';
    case 'Issued': return 'info';
    case 'Closed': return 'warning';
    case 'Awarded': return 'success';
    case 'Cancelled': return 'danger';
    default: return 'neutral';
  }
}

export function getSupplierQuotationStatusName(status: any): SupplierQuotationStatus {
  if (typeof status === 'string') {
    const s = status.trim();
    if (s === 'Draft' || s === 'Submitted' || s === 'UnderReview' || s === 'Approved' || s === 'Rejected' || s === 'Withdrawn') {
      return s as SupplierQuotationStatus;
    }
  }
  switch (Number(status)) {
    case 1: return 'Draft';
    case 2: return 'Submitted';
    case 3: return 'UnderReview';
    case 4: return 'Approved';
    case 5: return 'Rejected';
    case 6: return 'Withdrawn';
    default: return 'Draft';
  }
}

export function getSupplierInvitationStatusName(status: any): RFQSupplierInvitationStatus {
  if (typeof status === 'string') {
    const s = status.trim();
    if (s === 'Pending' || s === 'Sending' || s === 'Sent' || s === 'Failed' || s === 'Opened' || s === 'Submitted' || s === 'Declined') {
      return s as RFQSupplierInvitationStatus;
    }
  }
  switch (Number(status)) {
    case 1: return 'Pending';
    case 2: return 'Sent';
    case 3: return 'Opened';
    case 4: return 'Submitted';
    case 5: return 'Failed';
    case 6: return 'Declined';
    case 7: return 'Sending';
    default: return 'Pending';
  }
}

export interface DraftRfqLineItem {
  id?: string;
  lineNumber: number;
  productId: string;
  productCode: string;
  productName: string;
  quantity: number;
  unitOfMeasureId: string;
  unitOfMeasureCode: string;
  targetUnitPrice?: number | null;
  requiredDate?: string | null;
  technicalSpecifications?: string | null;
  notes?: string | null;
}

export interface RfqHeaderDto {
  id: string;
  tenantId: string;
  companyId: string;
  branchId: string;
  rfqNumber: string;
  title: string;
  status: RequestForQuotationStatus;
  closingDate: string;
  deliveryRequiredDate?: string;
  deliveryTerms?: string;
  instructions?: string;
  lineCount: number;
  invitedSupplierCount: number;
  quotationCount: number;
  createdAt: string;
  createdBy: string;
}

export interface RfqLineDto {
  id: string;
  tenantId: string;
  companyId: string;
  rfqId: string;
  lineNumber: number;
  productId: string;
  productCode: string;
  productName: string;
  quantity: number;
  unitOfMeasureId: string;
  unitOfMeasureCode: string;
  targetUnitPrice?: number;
  requiredDate?: string;
  technicalSpecifications?: string;
  notes?: string;
}

export interface RfqInvitationAccessOptionsDto {
  standardLocalhostUrl?: string;
  tenantLocalhostUrl?: string;
  tenantLocalhostUnavailableReason?: string;
  externalTenantUrl?: string;
  externalTenantUnavailableReason?: string;
  lanIpUrl?: string;
  lanIpUnavailableReason?: string;
}

export interface RfqSupplierDto {
  id: string;
  tenantId: string;
  companyId: string;
  rfqId: string;
  supplierId: string;
  supplierCode: string;
  supplierName: string;
  contactEmail: string;
  invitationStatus: RFQSupplierInvitationStatus;
  invitedAt?: string;
  hasToken: boolean;
  tokenExpiresAt?: string;
  responseReceivedAt?: string;
  standardLocalhostUrl?: string;
  tenantLocalhostUrl?: string;
  tenantLocalhostUnavailableReason?: string;
  externalTenantUrl?: string;
  externalTenantUnavailableReason?: string;
  lanIpUrl?: string;
  lanIpUnavailableReason?: string;
  accessOptions?: RfqInvitationAccessOptionsDto;
}

export interface RfqDetailsDto extends RfqHeaderDto {
  lines: RfqLineDto[];
  invitedSuppliers: RfqSupplierDto[];
  quotations: SupplierQuotationDto[];
}

export interface SupplierQuotationLineDto {
  id: string;
  tenantId: string;
  companyId: string;
  supplierQuotationId: string;
  rfqLineId?: string;
  productId: string;
  productCode: string;
  productName: string;
  quotedQuantity: number;
  unitOfMeasureId: string;
  unitOfMeasureCode: string;
  unitPrice: number;
  baseUnitPrice: number;
  discountPercentage: number;
  taxCodeId?: string;
  taxRate: number;
  lineTotal: number;
  baseLineTotal: number;
  leadTimeDays?: number;
  technicalRemarks?: string;
}

export interface SupplierQuotationDto {
  id: string;
  tenantId: string;
  companyId: string;
  branchId: string;
  rfqId: string;
  rfqNumber: string;
  supplierId: string;
  supplierCode: string;
  supplierName: string;
  quotationReference: string;
  quotationDate: string;
  validUntil: string;
  isExpired: boolean;
  currencyId: string;
  currencyCode: string;
  exchangeRate: number;
  status: SupplierQuotationStatus;
  subTotal: number;
  discountAmount: number;
  taxAmount: number;
  totalAmount: number;
  baseTotalAmount: number;
  notes?: string;
  isManualEntry: boolean;
  approvedAt?: string;
  approvedByUserId?: string;
  approvalRemarks?: string;
  rejectedAt?: string;
  rejectedByUserId?: string;
  rejectionRemarks?: string;
  lines: SupplierQuotationLineDto[];
}

export interface CreateRfqLineRequest {
  productId: string;
  quantity: number;
  unitOfMeasureId: string;
  targetUnitPrice?: number;
  requiredDate?: string;
  technicalSpecifications?: string;
  notes?: string;
}

export interface CreateRfqRequest {
  title: string;
  closingDate: string;
  deliveryRequiredDate?: string;
  deliveryTerms?: string;
  instructions?: string;
  lines?: CreateRfqLineRequest[];
  supplierIds?: string[];
}

export interface UpdateRfqRequest {
  title: string;
  closingDate: string;
  deliveryRequiredDate?: string;
  deliveryTerms?: string;
  instructions?: string;
}

export interface AddRfqSupplierRequest {
  supplierId: string;
  customEmail?: string;
}

export interface SendRfqInvitationsRequest {
  supplierIds?: string[];
  customEmailNote?: string;
  portalBaseUrl?: string;
  tenantSubdomain?: string;
  externalBaseUrl?: string;
  lanBaseUrl?: string;
}

export interface RfqInvitationResultDto {
  sentCount: number;
  failedCount: number;
  messages: string[];
  standardLocalhostUrl?: string;
  tenantLocalhostUrl?: string;
  tenantLocalhostUnavailableReason?: string;
  externalTenantUrl?: string;
  externalTenantUnavailableReason?: string;
  lanIpUrl?: string;
  lanIpUnavailableReason?: string;
  accessOptions?: RfqInvitationAccessOptionsDto;
}

export interface RecordQuotationLineRequest {
  rfqLineId?: string;
  productId: string;
  quotedQuantity: number;
  unitOfMeasureId: string;
  unitPrice: number;
  discountPercentage?: number;
  taxCodeId?: string;
  taxRate?: number;
  leadTimeDays?: number;
  technicalRemarks?: string;
}

export interface RecordManualQuotationRequest {
  supplierId: string;
  quotationReference: string;
  quotationDate: string;
  validUntil: string;
  currencyId: string;
  exchangeRate?: number;
  notes?: string;
  lines: RecordQuotationLineRequest[];
}

export interface ApproveQuotationRequest {
  remarks?: string;
}

export interface RejectQuotationRequest {
  remarks?: string;
}

export interface CancelRfqRequest {
  reason?: string;
}

export interface RfqComparisonSupplierBidDto {
  quotationId: string;
  supplierId: string;
  supplierCode: string;
  supplierName: string;
  quotationReference: string;
  quotationDate: string;
  submissionDate?: string;
  validUntil: string;
  isExpired: boolean;
  currencyCode: string;
  exchangeRate: number;
  hasValidExchangeRate: boolean;
  currencyConversionError?: string;
  totalAmount: number;
  baseTotalAmount: number;
  status: SupplierQuotationStatus;
  isManualEntry: boolean;
}

export interface RfqComparisonLineBidDto {
  quotationId: string;
  supplierId: string;
  quotedQuantity: number;
  quotedUomCode: string;
  unitPrice: number;
  baseUnitPrice: number;
  discountPercentage: number;
  discountAmount: number;
  baseDiscountAmount: number;
  taxRate: number;
  taxAmount: number;
  baseTaxAmount: number;
  lineTotal: number;
  baseLineTotal: number;
  leadTimeDays?: number;
  technicalRemarks?: string;
}

export interface RfqComparisonLineDto {
  rfqLineId: string;
  lineNumber: number;
  productCode: string;
  productName: string;
  requestedQuantity: number;
  requestedUomCode: string;
  targetUnitPrice?: number;
  bids: RfqComparisonLineBidDto[];
}

export interface RfqComparisonMatrixDto {
  rfqId: string;
  rfqNumber: string;
  title: string;
  baseCurrencyCode: string;
  suppliers: RfqComparisonSupplierBidDto[];
  lines: RfqComparisonLineDto[];
}

export interface PublicRfqLineDto {
  rfqLineId: string;
  lineNumber: number;
  productId: string;
  productCode: string;
  productName: string;
  quantity: number;
  unitOfMeasureId: string;
  unitOfMeasureCode: string;
  requiredDate?: string;
  technicalSpecifications?: string;
  notes?: string;
}

export interface PublicRfqDetailsDto {
  rfqId: string;
  rfqNumber: string;
  title: string;
  companyName?: string;
  supplierId?: string;
  supplierName: string;
  supplierCode: string;
  alreadySubmitted?: boolean;
  contactEmail?: string;
  closingDate: string;
  deliveryRequiredDate?: string;
  deliveryTerms?: string;
  instructions?: string;
  currencyCode?: string;
  currencyId?: string;
  lines: PublicRfqLineDto[];
}

export interface SubmitPublicQuotationLineRequest {
  rfqLineId: string;
  quotedQuantity: number;
  unitOfMeasureId: string;
  unitPrice: number;
  discountPercentage?: number;
  taxCodeId?: string;
  taxRate?: number;
  leadTimeDays?: number;
  technicalRemarks?: string;
}

export interface SubmitPublicQuotationRequest {
  quotationReference: string;
  quotationDate: string;
  validUntil: string;
  currencyId?: string;
  notes?: string;
  lines: SubmitPublicQuotationLineRequest[];
}

export interface SubmitQuotationResultDto {
  success: boolean;
  message: string;
  quotationId: string;
  quotationReference: string;
}
