export interface Company {
  id: string;
  tenantId: string;
  name: string;
  code: string;
  isActive: boolean;
  currency?: string;
}

export interface Branch {
  id: string;
  companyId: string;
  name: string;
  code: string;
  isActive: boolean;
}

export interface WorkspaceScope {
  tenantId: string | null;
  tenantName: string;
  company: Company | null;
  branch: Branch | null;
  isAllBranches: boolean;
}

export interface CategoryItem {
  categoryId: string;
  tenantId: string;
  categoryCode: string;
  categoryName: string;
  description?: string;
  parentCategoryId?: string | null;
  isActive: boolean;
}

export interface UnitOfMeasureItem {
  unitOfMeasureId: string;
  tenantId: string;
  code: string;
  name: string;
  description?: string;
  isActive: boolean;
}

export interface WarehouseLocationItem {
  locationId: string;
  warehouseLocationId?: string;
  tenantId?: string;
  warehouseId: string;
  warehouseZoneId?: string | null;
  locationTypeId?: string | null;
  parentLocationId?: string | null;
  locationCode: string;
  locationName: string;
  zoneId?: string | null;
  zoneCode?: string | null;
  zoneName?: string | null;
  parentLocationCode?: string | null;
  parentLocationName?: string | null;
  locationTypeCode?: string | null;
  locationTypeName?: string | null;
  canStoreInventory?: boolean;
  canPick?: boolean;
  canPutAway?: boolean;
  canContainChildren?: boolean;
  hierarchyPath?: string;
  hierarchyLevel?: number;
  isLeaf?: boolean;
  isPickingLocation?: boolean;
  isReceivingLocation?: boolean;
  isActive: boolean;
}

export interface WarehouseZoneItem {
  warehouseZoneId: string;
  tenantId?: string;
  warehouseId: string;
  zoneCode: string;
  zoneName: string;
  description?: string | null;
  storageLocationsCount?: number;
  isActive: boolean;
}

export interface LocationTypeItem {
  locationTypeId: string;
  tenantId?: string | null;
  code: string;
  name: string;
  description?: string | null;
  canStoreInventory: boolean;
  canPick: boolean;
  canPutAway: boolean;
  canContainChildren: boolean;
  isActive: boolean;
}

export interface WarehouseItem {
  warehouseId: string;
  tenantId: string;
  companyId?: string | null;
  branchId?: string | null;
  warehouseCode: string;
  warehouseName: string;
  description?: string | null;
  addressLine1?: string | null;
  addressLine2?: string | null;
  city?: string;
  postalCode?: string | null;
  country?: string | null;
  isActive: boolean;
  zonesCount?: number;
  locationsCount?: number;
  locations?: WarehouseLocationItem[];
}

export interface CreateWarehouseDto {
  warehouseCode: string;
  warehouseName: string;
  description?: string;
  companyId?: string | null;
  branchId?: string | null;
  addressLine1?: string;
  addressLine2?: string;
  city: string;
  postalCode?: string;
  country?: string;
}

export interface UpdateWarehouseDto {
  warehouseName: string;
  description?: string;
  companyId?: string | null;
  branchId?: string | null;
  addressLine1?: string;
  addressLine2?: string;
  city: string;
  postalCode?: string;
  country?: string;
}

export interface CreateWarehouseZoneDto {
  zoneCode: string;
  zoneName: string;
  description?: string;
}

export interface UpdateWarehouseZoneDto {
  zoneName: string;
  description?: string;
}

export interface CreateWarehouseLocationDto {
  warehouseZoneId?: string | null;
  locationCode: string;
  locationName: string;
  locationTypeId?: string | null;
  parentLocationId?: string | null;
}

export interface UpdateWarehouseLocationDto {
  locationName: string;
  locationTypeId?: string | null;
  parentLocationId?: string | null;
}

export interface ProductItem {
  productId: string;
  tenantId: string;
  productCode: string;
  productName: string;
  categoryId: string;
  categoryName?: string;
  description?: string;
  unitOfMeasure: string;
  unitOfMeasureId?: string | null;
  isManufacturable: boolean;
  isPurchasable: boolean;
  isSellable: boolean;
  canConsumeInProduction: boolean;
  canConsumeInMaintenance: boolean;
  isStockTracked: boolean;
  productType: number | string;
  trackingMode: number | string;
  valuationMethod: number | string;
  standardCost: number;
  sellingPrice: number;
  reorderLevel: number;
  reorderQuantity: number;
  isActive: boolean;
  createdAt?: string;
}

export interface CreateProductDto {
  productCode: string;
  productName: string;
  categoryId: string;
  description?: string;
  unitOfMeasure: string;
  standardCost: number;
  sellingPrice: number;
  reorderLevel: number;
  reorderQuantity: number;
  isManufacturable: boolean;
  isPurchasable: boolean;
  isSellable: boolean;
  canConsumeInProduction: boolean;
  canConsumeInMaintenance?: boolean;
  isStockTracked: boolean;
  productType?: number;
  trackingMode?: number;
  unitOfMeasureId?: string | null;
}

export interface UpdateProductDto extends CreateProductDto {
  isActive?: boolean;
}

export interface SupplierItem {
  supplierId: string;
  tenantId: string;
  supplierCode: string;
  supplierName: string;
  contactName?: string;
  email?: string;
  phone?: string;
  city?: string;
  country?: string;
  paymentTerms?: string;
  leadTimeDays?: number;
  isActive: boolean;
  createdAt?: string;
}

export interface CreateSupplierDto {
  supplierCode: string;
  supplierName: string;
  contactName?: string;
  email?: string;
  phone?: string;
  city?: string;
  country?: string;
  paymentTerms?: string;
  leadTimeDays?: number;
  isActive?: boolean;
}


export interface CustomerItem {
  customerId: string;
  tenantId?: string;
  customerCode: string;
  customerName: string;
  contactPerson?: string;
  email?: string;
  phone?: string;
  addressLine1?: string;
  addressLine2?: string;
  city?: string;
  postalCode?: string;
  country?: string;
  taxNumber?: string;
  creditLimit?: number;
  paymentTerms?: string;
  isActive: boolean;
  createdAt?: string;
}

export interface CreateCustomerDto {
  customerCode: string;
  customerName: string;
  contactPerson?: string;
  email?: string;
  phone?: string;
  addressLine1?: string;
  addressLine2?: string;
  city: string;
  postalCode?: string;
  country?: string;
  taxNumber?: string;
  creditLimit?: number;
  paymentTerms?: string;
}
