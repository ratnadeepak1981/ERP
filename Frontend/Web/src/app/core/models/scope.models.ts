export interface Company {
  id: string;
  tenantId: string;
  name: string;
  code: string;
  isActive: boolean;
  baseCurrencyId?: string;
  baseCurrencyCode?: string;
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

export interface CategoryTreeNode extends CategoryItem {
  level?: number;
  hasChildren?: boolean;
}

export interface CategoryNodeState {
  expanded: boolean;
  children: CategoryTreeNode[];
  childrenLoading: boolean;
  childrenLoaded: boolean;
  hasMoreChildren: boolean;
  childrenPage: number;
  childrenError?: string | null;
  showProducts: boolean;
  products: ProductItem[];
  productsLoading: boolean;
  productsLoaded: boolean;
  hasMoreProducts: boolean;
  productsPage: number;
  productsError?: string | null;
  error?: string | null;
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  hasMore: boolean;
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
  city?: string;
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
  city?: string;
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

export enum ProductType {
  StockItem = 1,
  NonStockItem = 2,
  Service = 3
}

export enum TrackingMode {
  None = 0,
  Batch = 1,
  Serial = 2,
  BatchAndSerial = 3
}

export enum ValuationMethod {
  FIFO = 1,
  WeightedAverage = 2,
  StandardCost = 3,
  LIFO = 4
}

export interface ProductItem {
  productId: string;
  tenantId: string;
  productCode: string;
  productName: string;
  categoryId: string;
  categoryName?: string;
  brandId?: string | null;
  brandName?: string | null;
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
  minStockQuantity?: number | null;
  maxStockQuantity?: number | null;
  safetyStockQuantity?: number | null;
  leadTimeDays?: number | null;
  isActive: boolean;
  createdAt?: string;
}

export interface CreateProductDto {
  productCode: string;
  productName: string;
  categoryId: string;
  brandId?: string | null;
  description?: string;
  unitOfMeasure: string;
  unitOfMeasureId?: string | null;
  standardCost: number;
  sellingPrice: number;
  reorderLevel: number;
  reorderQuantity: number;
  minStockQuantity?: number | null;
  maxStockQuantity?: number | null;
  safetyStockQuantity?: number | null;
  leadTimeDays?: number | null;
  isManufacturable: boolean;
  isPurchasable: boolean;
  isSellable: boolean;
  canConsumeInProduction: boolean;
  canConsumeInMaintenance?: boolean;
  isStockTracked: boolean;
  productType?: number;
  trackingMode?: number;
  valuationMethod?: number;
}

export interface UpdateProductDto extends CreateProductDto {
  isActive?: boolean;
}

export interface ProductPackagingItem {
  productPackagingId: string;
  tenantId: string;
  companyId?: string | null;
  productId: string;
  packagingCode: string;
  packagingName: string;
  packagingUomId: string;
  packagingUomCode?: string;
  baseQuantity: number;
  purchaseAllowed: boolean;
  salesAllowed: boolean;
  isActive: boolean;
}

export interface CreateProductPackagingDto {
  packagingCode: string;
  packagingName: string;
  packagingUomId: string;
  baseQuantity: number;
  purchaseAllowed?: boolean;
  salesAllowed?: boolean;
  companyId?: string | null;
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
  city?: string;
  postalCode?: string;
  country?: string;
  taxNumber?: string;
  creditLimit?: number;
  paymentTerms?: string;
}

export interface UnitOfMeasureConversionItem {
  conversionId: string;
  tenantId: string;
  fromUnitId: string;
  toUnitId: string;
  conversionFactor: number;
  isActive: boolean;
}

export interface CurrencyItem {
  currencyId: string;
  tenantId: string;
  currencyCode: string;
  currencyName: string;
  symbol: string;
  decimalPlaces: number;
  isActive: boolean;
}

export interface BrandItem {
  brandId: string;
  tenantId: string;
  brandCode: string;
  brandName: string;
  description?: string;
  isActive: boolean;
}

export interface CreateBrandDto {
  brandCode: string;
  brandName: string;
  description?: string;
}

export interface UpdateBrandDto {
  brandName: string;
  description?: string;
}

export interface PaymentTermItem {
  paymentTermId: string;
  tenantId: string;
  termCode: string;
  termName: string;
  days: number;
  description?: string;
  isActive: boolean;
}

export interface CreatePaymentTermDto {
  termCode: string;
  termName: string;
  days: number;
  description?: string;
}

export interface UpdatePaymentTermDto {
  termName: string;
  days: number;
  description?: string;
}

export interface AddressTypeItem {
  addressTypeId: string;
  code: string;
  name: string;
  description?: string;
  isActive: boolean;
}

export interface AddressItem {
  addressId: string;
  tenantId: string;
  addressName: string;
  addressLine1: string;
  addressLine2?: string;
  city: string;
  stateProvince?: string;
  postalCode?: string;
  countryId?: string;
  isActive: boolean;
}

export interface CustomerAddressItem {
  customerAddressId: string;
  customerId: string;
  addressId: string;
  addressTypeId: string;
  isDefault: boolean;
  isActive: boolean;
  address?: AddressItem;
  addressTypeName?: string;
}

export interface SupplierAddressItem {
  supplierAddressId: string;
  supplierId: string;
  addressId: string;
  addressTypeId: string;
  isDefault: boolean;
  isActive: boolean;
  address?: AddressItem;
  addressTypeName?: string;
}

export type TaxApplicabilityType = 'Both' | 'Purchase' | 'Sales' | 1 | 2 | 3;

export interface TaxCodeItem {
  taxCodeId: string;
  tenantId: string;
  code: string;
  name: string;
  rate: number;
  applicability: number | string;
  applicabilityName?: string;
  effectiveFrom?: string | null;
  effectiveTo?: string | null;
  description?: string | null;
  isActive: boolean;
}

export interface CreateTaxCodeDto {
  code: string;
  name: string;
  rate: number;
  applicability: number;
  effectiveFrom?: string | null;
  effectiveTo?: string | null;
  description?: string | null;
}

export interface UpdateTaxCodeDto {
  name: string;
  rate: number;
  applicability: number;
  effectiveFrom?: string | null;
  effectiveTo?: string | null;
  description?: string | null;
}
