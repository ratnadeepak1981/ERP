import { BatchSerialAllocation } from './goods-receipt-note.models';

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


export interface EligibleLocationDto {
  warehouseId: string;
  warehouseName: string;
  warehouseCode: string;
  locationId: string;
  locationCode: string;
  locationName: string;
  isDefault: boolean;
  isStaging?: boolean;
  availableQuantity?: number;
}

export interface LocationStockAvailabilityDto {
  warehouseId: string;
  warehouseCode: string;
  warehouseName: string;
  warehouseLocationId: string;
  locationCode: string;
  locationName: string;
  isStaging: boolean;
  availableQuantity: number;
  canFulfil: boolean;
}

export interface SuggestedLocationSplitDto {
  warehouseId: string;
  warehouseCode: string;
  warehouseName: string;
  warehouseLocationId: string;
  locationCode: string;
  locationName: string;
  isStaging: boolean;
  availableQuantity: number;
  suggestedQuantity: number;
  batchNumber?: string | null;
  expiryDate?: string | null;
}

export interface WarehouseStockSummaryDto {
  warehouseId: string;
  warehouseCode: string;
  warehouseName: string;
  isPrimary: boolean;
  isSameBranch: boolean;
  totalAvailableQuantity: number;
  canFulfil: boolean;
}

export interface SalesOrderDispatchLineAvailabilityDto {
  salesOrderItemId: string;
  productId: string;
  productCode: string;
  productName: string;
  orderedQuantity: number;
  alreadyDispatchedQuantity: number;
  remainingQuantity: number;
  currentDraftAllocatedQuantity?: number;
  availableQuantity: number;
  shortageQuantity: number;
  canFulfil: boolean;
  unitOfMeasureId?: string | null;
  unitOfMeasure?: string;
  trackingMode?: number;
  defaultSuggestedWarehouseId?: string | null;
  defaultSuggestedWarehouseName?: string | null;
  defaultSuggestedLocationId?: string | null;
  defaultSuggestedLocationCode?: string | null;
  defaultSuggestedLocationName?: string | null;
  isDefaultStaging?: boolean;
  suggestedSplits?: SuggestedLocationSplitDto[];
  eligibleLocations?: LocationStockAvailabilityDto[];
  alternativeWarehouses?: WarehouseStockSummaryDto[];
}

export interface SalesOrderDispatchAvailabilityDto {
  salesOrderId: string;
  orderNumber: string;
  defaultWarehouseId?: string | null;
  defaultWarehouseName?: string | null;
  defaultLocationId?: string | null;
  defaultLocationCode?: string | null;
  defaultLocationName?: string | null;
  selectedWarehouseId?: string | null;
  selectedWarehouseName?: string | null;
  selectedLocationId?: string | null;
  selectedLocationCode?: string | null;
  selectedLocationName?: string | null;
  canDefaultLocationFulfil: boolean;
  canFulfil: boolean;
  hasShortages: boolean;
  totalShortageLines?: number;
  eligibleLocations: EligibleLocationDto[];
  eligibleWarehouses?: WarehouseStockSummaryDto[];
  lines: SalesOrderDispatchLineAvailabilityDto[];
}

export interface CreateReplenishmentTransferRequest {
  salesOrderId: string;
  salesOrderItemId: string;
  productId: string;
  sourceWarehouseId: string;
  destinationWarehouseId: string;
  quantity: number;
  notes?: string | null;
}

export interface ReplenishmentTransferResult {
  transferReferenceNumber: string;
  status: string;
  productId: string;
  sourceWarehouseId: string;
  destinationWarehouseId: string;
  quantity: number;
  message: string;
}

export interface CreateReplenishmentRequisitionRequest {
  salesOrderId: string;
  requiredDate?: string | null;
  notes?: string | null;
  items: CreateReplenishmentRequisitionItemRequest[];
}

export interface CreateReplenishmentRequisitionItemRequest {
  productId: string;
  unitOfMeasureId?: string | null;
  quantity: number;
  estimatedUnitPrice?: number | null;
  remarks?: string | null;
}

export interface ReplenishmentRequisitionResult {
  requisitionId: string;
  requisitionNumber: string;
  status: string;
  itemCount: number;
  message: string;
}

export interface EditableShipmentLine {
  lineId: string;
  id?: string;
  salesOrderItemId: string;
  productId: string;
  productCode: string;
  productName: string;
  unitOfMeasureId?: string | null;
  unitOfMeasure?: string;
  orderedQuantity: number;
  alreadyDispatchedQuantity: number;
  outstandingQuantity: number;
  quantityDispatched: number;
  warehouseId: string;
  warehouseLocationId: string;
  availableQuantity?: number;
  isSuggestedSource?: boolean;
  batchNumber?: string | null;
  serialNumber?: string | null;
  notes?: string | null;
  trackingMode?: number;
  trackingAllocations?: BatchSerialAllocation[];
}

export interface DispatchOrderRequest {
  warehouseId: string;
  warehouseLocationId: string;
  shipmentNumber?: string;
  method?: FulfillmentMethod;
  shipmentDate?: string;
  carrierName?: string;
  trackingNumber?: string;
  recipientName?: string;
  deliveryAddress?: string;
  vehicleNumber?: string;
  notes?: string;
  items?: CreateSalesShipmentItemRequest[];
}
