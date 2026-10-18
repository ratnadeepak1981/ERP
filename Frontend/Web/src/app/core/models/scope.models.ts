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
  warehouseId: string;
  locationCode: string;
  locationName: string;
  zoneId?: string | null;
  locationType?: string;
  isPickingLocation?: boolean;
  isReceivingLocation?: boolean;
  isActive: boolean;
}

export interface WarehouseItem {
  warehouseId: string;
  tenantId: string;
  warehouseCode: string;
  warehouseName: string;
  branchId?: string | null;
  city?: string;
  isActive: boolean;
  locations?: WarehouseLocationItem[];
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
