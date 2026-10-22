export enum SalesShipmentStatus {
  Draft = 1,
  Dispatched = 2,
  Delivered = 3,
  Cancelled = 4
}

export enum FulfillmentMethod {
  Delivery = 1,
  CustomerPickup = 2,
  StoreHandover = 3
}

export function getSalesShipmentStatusName(status: SalesShipmentStatus | number): string {
  switch (Number(status)) {
    case SalesShipmentStatus.Draft:
      return 'Draft';
    case SalesShipmentStatus.Dispatched:
      return 'Dispatched';
    case SalesShipmentStatus.Delivered:
      return 'Delivered';
    case SalesShipmentStatus.Cancelled:
      return 'Cancelled';
    default:
      return 'Unknown';
  }
}

export function getSalesShipmentStatusSeverity(
  status: SalesShipmentStatus | number
): 'info' | 'success' | 'warning' | 'danger' | 'neutral' {
  switch (Number(status)) {
    case SalesShipmentStatus.Draft:
      return 'info';
    case SalesShipmentStatus.Dispatched:
      return 'warning';
    case SalesShipmentStatus.Delivered:
      return 'success';
    case SalesShipmentStatus.Cancelled:
      return 'danger';
    default:
      return 'neutral';
  }
}

export interface SalesShipmentItemDto {
  id: string;
  salesShipmentId: string;
  salesOrderItemId: string;
  productId: string;
  productCode?: string;
  productName?: string;
  warehouseId: string;
  warehouseName?: string;
  warehouseLocationId: string;
  locationCode?: string;
  unitOfMeasureId?: string | null;
  quantityDispatched: number;
  batchNumber?: string | null;
  serialNumber?: string | null;
  notes?: string | null;
}

export interface SalesShipmentDto {
  id: string;
  tenantId: string;
  companyId: string;
  branchId: string;
  salesOrderId: string;
  orderNumber: string;
  customerId: string;
  customerName?: string;
  warehouseId?: string | null;
  shipmentNumber: string;
  status: SalesShipmentStatus | number;
  method: FulfillmentMethod | number;
  shipmentDate: string;
  dispatchedAt?: string | null;
  deliveredAt?: string | null;
  carrierName?: string | null;
  trackingNumber?: string | null;
  recipientName?: string | null;
  deliveryAddress?: string | null;
  vehicleNumber?: string | null;
  notes?: string | null;
  invoiceId?: string | null;
  items: SalesShipmentItemDto[];
}

export interface CreateSalesShipmentItemRequest {
  salesOrderItemId: string;
  productId: string;
  warehouseId: string;
  warehouseLocationId: string;
  quantityDispatched: number;
  unitOfMeasureId?: string | null;
  batchNumber?: string | null;
  serialNumber?: string | null;
  notes?: string | null;
}

export interface CreateShipmentFromOrderRequest {
  shipmentNumber: string;
  method?: FulfillmentMethod;
  defaultWarehouseId?: string | null;
  defaultLocationId?: string | null;
  shipmentDate?: string | null;
  carrierName?: string | null;
  trackingNumber?: string | null;
  recipientName?: string | null;
  deliveryAddress?: string | null;
  vehicleNumber?: string | null;
  notes?: string | null;
  items?: CreateSalesShipmentItemRequest[];
}

export interface UpdateSalesShipmentRequest {
  method?: FulfillmentMethod;
  shipmentDate?: string | null;
  carrierName?: string | null;
  trackingNumber?: string | null;
  recipientName?: string | null;
  deliveryAddress?: string | null;
  vehicleNumber?: string | null;
  notes?: string | null;
  items?: CreateSalesShipmentItemRequest[];
}

export interface DeliverShipmentRequest {
  deliveredAt?: string | null;
  recipientName?: string | null;
  notes?: string | null;
}
