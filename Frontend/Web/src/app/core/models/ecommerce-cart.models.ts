/**
 * Strongly typed models for the ApexERP E-commerce Shopping Cart subsystem.
 * Strictly aligned to backend contracts in ERP.Application.Features.Ecommerce.Cart.EcommerceCartDtos.
 */

export enum EcommerceCartStatus {
  Active = 1,
  CheckedOut = 2,
  Abandoned = 3
}

export interface CartItemDto {
  lineId: string;
  productId: string;
  productCode: string;
  productName: string;
  unitOfMeasureId?: string | null;
  unitOfMeasureCode?: string | null;
  unitOfMeasureName?: string | null;
  productPackagingId?: string | null;
  packagingName?: string | null;
  quantity: number;
  unitPrice: number;
  subTotal: number;
  discountRate: number;
  discountAmount: number;
  taxCodeId?: string | null;
  taxCode?: string | null;
  taxRate: number;
  taxAmount: number;
  lineTotal: number;
  isTaxInclusive: boolean;
  currency: string;
  isAvailable: boolean;
  validationMessage?: string | null;
}

export interface CartDto {
  cartId: string;
  tenantId: string;
  companyId: string;
  companyName: string;
  customerId?: string | null;
  customerName?: string | null;
  sessionId?: string | null;
  status: EcommerceCartStatus;
  currency: string;
  totalItems: number;
  subTotal: number;
  totalDiscount: number;
  totalTax: number;
  grandTotal: number;
  lines: CartItemDto[];
}

export interface AddToCartRequest {
  productId: string;
  quantity: number;
  unitOfMeasureId?: string | null;
  productPackagingId?: string | null;
}

export interface PendingAddToCartAction {
  productId: string;
  quantity: number;
  unitOfMeasureId?: string | null;
  productPackagingId?: string | null;
  companyId: string;
  timestamp: number;
}

export interface UpdateCartQuantityRequest {
  quantity: number;
}

export interface CheckoutRequest {
  sessionId?: string | null;
  idempotencyKey?: string | null;
  customerId?: string | null;
  deliveryAddressId?: string | null;
  customerNotes?: string | null;
  requestedDeliveryDate?: string | null;
}

export interface CheckoutResultDto {
  success: boolean;
  salesOrderId: string;
  orderNumber: string;
  orderStatus: number;
  cartId: string;
  totalAmount: number;
  currency: string;
  message?: string | null;
}
