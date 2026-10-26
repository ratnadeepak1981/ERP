import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { buildApiUrl } from '../utils/api-url.util';
import { ApiResponse } from '../models/auth.models';
import {
  ProductItem,
  CreateProductDto,
  UpdateProductDto,
  SupplierItem,
  CreateSupplierDto,
  CustomerItem,
  CreateCustomerDto,
  CategoryItem,
  UnitOfMeasureItem,
  UnitOfMeasureConversionItem,
  CurrencyItem,
  BrandItem,
  CreateBrandDto,
  UpdateBrandDto,
  PaymentTermItem,
  CreatePaymentTermDto,
  UpdatePaymentTermDto,
  AddressItem,
  AddressTypeItem,
  CustomerAddressItem,
  SupplierAddressItem,
  WarehouseItem,
  WarehouseZoneItem,
  WarehouseLocationItem,
  LocationTypeItem,
  CreateWarehouseDto,
  UpdateWarehouseDto,
  CreateWarehouseZoneDto,
  UpdateWarehouseZoneDto,
  CreateWarehouseLocationDto,
  UpdateWarehouseLocationDto,
  TaxCodeItem,
  CreateTaxCodeDto,
  UpdateTaxCodeDto
} from '../models/scope.models';

@Injectable({
  providedIn: 'root'
})
export class MasterDataService {
  private readonly http = inject(HttpClient);

  // --- Products ---
  getProducts(): Observable<ProductItem[]> {
    return this.http
      .get<ApiResponse<ProductItem[] | { items: ProductItem[] }>>(buildApiUrl('/products'))
      .pipe(
        map(res => {
          if (!res?.data) return [];
          if (Array.isArray(res.data)) return res.data;
          return (res.data as { items: ProductItem[] }).items || [];
        })
      );
  }

  getProduct(id: string): Observable<ProductItem | null> {
    return this.http
      .get<ApiResponse<ProductItem>>(buildApiUrl(`/products/${id}`))
      .pipe(map(res => res?.data ?? null));
  }

  createProduct(dto: CreateProductDto): Observable<ProductItem> {
    return this.http
      .post<ApiResponse<ProductItem>>(buildApiUrl('/products'), dto)
      .pipe(map(res => res.data));
  }

  updateProduct(id: string, dto: UpdateProductDto): Observable<ProductItem> {
    return this.http
      .put<ApiResponse<ProductItem>>(buildApiUrl(`/products/${id}`), dto)
      .pipe(map(res => res.data));
  }

  deactivateProduct(id: string): Observable<void> {
    return this.http.delete<void>(buildApiUrl(`/products/${id}`));
  }

  // --- Suppliers ---
  getSuppliers(): Observable<SupplierItem[]> {
    return this.http
      .get<ApiResponse<SupplierItem[] | { items: SupplierItem[] }>>(buildApiUrl('/suppliers'))
      .pipe(
        map(res => {
          if (!res?.data) return [];
          if (Array.isArray(res.data)) return res.data;
          return (res.data as { items: SupplierItem[] }).items || [];
        })
      );
  }

  getSupplier(id: string): Observable<SupplierItem | null> {
    return this.http
      .get<ApiResponse<SupplierItem>>(buildApiUrl(`/suppliers/${id}`))
      .pipe(map(res => res?.data ?? null));
  }

  createSupplier(dto: CreateSupplierDto): Observable<SupplierItem> {
    return this.http
      .post<ApiResponse<SupplierItem>>(buildApiUrl('/suppliers'), dto)
      .pipe(map(res => res.data));
  }

  updateSupplier(id: string, dto: Partial<CreateSupplierDto>): Observable<SupplierItem> {
    return this.http
      .put<ApiResponse<SupplierItem>>(buildApiUrl(`/suppliers/${id}`), dto)
      .pipe(map(res => res.data));
  }

  deactivateSupplier(id: string): Observable<void> {
    return this.http.delete<void>(buildApiUrl(`/suppliers/${id}`));
  }

  reactivateSupplier(id: string): Observable<SupplierItem> {
    return this.http
      .post<ApiResponse<SupplierItem>>(buildApiUrl(`/suppliers/${id}/reactivate`), null)
      .pipe(map(res => res.data));
  }

  // --- Customers ---
  getCustomers(): Observable<CustomerItem[]> {
    return this.http
      .get<ApiResponse<CustomerItem[] | { items: CustomerItem[] }>>(buildApiUrl('/customers'))
      .pipe(
        map(res => {
          if (!res?.data) return [];
          if (Array.isArray(res.data)) return res.data;
          return (res.data as { items: CustomerItem[] }).items || [];
        })
      );
  }

  getCustomer(id: string): Observable<CustomerItem | null> {
    return this.http
      .get<ApiResponse<CustomerItem>>(buildApiUrl(`/customers/${id}`))
      .pipe(map(res => res?.data ?? null));
  }

  createCustomer(dto: CreateCustomerDto): Observable<CustomerItem> {
    return this.http
      .post<ApiResponse<CustomerItem>>(buildApiUrl('/customers'), dto)
      .pipe(map(res => res.data));
  }

  updateCustomer(id: string, dto: Partial<CreateCustomerDto>): Observable<CustomerItem> {
    return this.http
      .put<ApiResponse<CustomerItem>>(buildApiUrl(`/customers/${id}`), dto)
      .pipe(map(res => res.data));
  }

  deactivateCustomer(id: string): Observable<void> {
    return this.http.delete<void>(buildApiUrl(`/customers/${id}`));
  }

  reactivateCustomer(id: string): Observable<CustomerItem> {
    return this.http
      .post<ApiResponse<CustomerItem>>(buildApiUrl(`/customers/${id}/reactivate`), null)
      .pipe(map(res => res.data));
  }

  // --- Categories ---
  getCategories(): Observable<CategoryItem[]> {
    return this.http
      .get<ApiResponse<CategoryItem[] | { items: CategoryItem[] }>>(buildApiUrl('/categories'))
      .pipe(
        map(res => {
          if (!res?.data) return [];
          if (Array.isArray(res.data)) return res.data;
          return (res.data as { items: CategoryItem[] }).items || [];
        })
      );
  }

  getCategory(id: string): Observable<CategoryItem | null> {
    return this.http
      .get<ApiResponse<CategoryItem>>(buildApiUrl(`/categories/${id}`))
      .pipe(map(res => res?.data ?? null));
  }

  createCategory(dto: { categoryCode: string; categoryName: string; description?: string }): Observable<CategoryItem> {
    return this.http
      .post<ApiResponse<CategoryItem>>(buildApiUrl('/categories'), dto)
      .pipe(map(res => res.data));
  }

  updateCategory(id: string, dto: { categoryName: string; description?: string }): Observable<CategoryItem> {
    return this.http
      .put<ApiResponse<CategoryItem>>(buildApiUrl(`/categories/${id}`), dto)
      .pipe(map(res => res.data));
  }

  deactivateCategory(id: string): Observable<void> {
    return this.http.delete<void>(buildApiUrl(`/categories/${id}`));
  }

  reactivateCategory(id: string): Observable<CategoryItem> {
    return this.http
      .post<ApiResponse<CategoryItem>>(buildApiUrl(`/categories/${id}/reactivate`), null)
      .pipe(map(res => res.data));
  }

  // --- Brands ---
  getBrands(): Observable<BrandItem[]> {
    return this.http
      .get<ApiResponse<BrandItem[] | { items: BrandItem[] }>>(buildApiUrl('/brands'))
      .pipe(
        map(res => {
          if (!res?.data) return [];
          if (Array.isArray(res.data)) return res.data;
          return (res.data as { items: BrandItem[] }).items || [];
        })
      );
  }

  getBrand(id: string): Observable<BrandItem | null> {
    return this.http
      .get<ApiResponse<BrandItem>>(buildApiUrl(`/brands/${id}`))
      .pipe(map(res => res?.data ?? null));
  }

  createBrand(dto: CreateBrandDto): Observable<BrandItem> {
    return this.http
      .post<ApiResponse<BrandItem>>(buildApiUrl('/brands'), dto)
      .pipe(map(res => res.data));
  }

  updateBrand(id: string, dto: UpdateBrandDto): Observable<BrandItem> {
    return this.http
      .put<ApiResponse<BrandItem>>(buildApiUrl(`/brands/${id}`), dto)
      .pipe(map(res => res.data));
  }

  deactivateBrand(id: string): Observable<void> {
    return this.http.delete<void>(buildApiUrl(`/brands/${id}`));
  }

  reactivateBrand(id: string): Observable<BrandItem> {
    return this.http
      .post<ApiResponse<BrandItem>>(buildApiUrl(`/brands/${id}/reactivate`), null)
      .pipe(map(res => res.data));
  }

  // --- Payment Terms ---
  getPaymentTerms(): Observable<PaymentTermItem[]> {
    return this.http
      .get<ApiResponse<PaymentTermItem[] | { items: PaymentTermItem[] }>>(buildApiUrl('/payment-terms'))
      .pipe(
        map(res => {
          if (!res?.data) return [];
          if (Array.isArray(res.data)) return res.data;
          return (res.data as { items: PaymentTermItem[] }).items || [];
        })
      );
  }

  getPaymentTerm(id: string): Observable<PaymentTermItem | null> {
    return this.http
      .get<ApiResponse<PaymentTermItem>>(buildApiUrl(`/payment-terms/${id}`))
      .pipe(map(res => res?.data ?? null));
  }

  createPaymentTerm(dto: CreatePaymentTermDto): Observable<PaymentTermItem> {
    return this.http
      .post<ApiResponse<PaymentTermItem>>(buildApiUrl('/payment-terms'), dto)
      .pipe(map(res => res.data));
  }

  updatePaymentTerm(id: string, dto: UpdatePaymentTermDto): Observable<PaymentTermItem> {
    return this.http
      .put<ApiResponse<PaymentTermItem>>(buildApiUrl(`/payment-terms/${id}`), dto)
      .pipe(map(res => res.data));
  }

  deactivatePaymentTerm(id: string): Observable<void> {
    return this.http.delete<void>(buildApiUrl(`/payment-terms/${id}`));
  }

  reactivatePaymentTerm(id: string): Observable<PaymentTermItem> {
    return this.http
      .post<ApiResponse<PaymentTermItem>>(buildApiUrl(`/payment-terms/${id}/reactivate`), null)
      .pipe(map(res => res.data));
  }

  // --- Addresses & Address Types ---
  getAddresses(): Observable<AddressItem[]> {
    return this.http
      .get<ApiResponse<AddressItem[] | { items: AddressItem[] }>>(buildApiUrl('/addresses'))
      .pipe(
        map(res => {
          if (!res?.data) return [];
          if (Array.isArray(res.data)) return res.data;
          return (res.data as { items: AddressItem[] }).items || [];
        })
      );
  }

  createAddress(dto: {
    addressName: string;
    addressLine1: string;
    addressLine2?: string;
    city: string;
    stateProvince?: string;
    postalCode?: string;
  }): Observable<AddressItem> {
    return this.http
      .post<ApiResponse<AddressItem>>(buildApiUrl('/addresses'), dto)
      .pipe(map(res => res.data));
  }

  getAddressTypes(): Observable<AddressTypeItem[]> {
    return this.http
      .get<ApiResponse<AddressTypeItem[]>>(buildApiUrl('/addresses/types'))
      .pipe(map(res => res?.data || []));
  }

  // --- Customer Addresses ---
  getCustomerAddresses(customerId: string): Observable<CustomerAddressItem[]> {
    return this.http
      .get<ApiResponse<CustomerAddressItem[]>>(buildApiUrl(`/customers/${customerId}/addresses`))
      .pipe(map(res => res?.data || []));
  }

  linkCustomerAddress(
    customerId: string,
    dto: { addressId: string; addressTypeId: string; isDefault?: boolean }
  ): Observable<CustomerAddressItem> {
    return this.http
      .post<ApiResponse<CustomerAddressItem>>(buildApiUrl(`/customers/${customerId}/addresses`), dto)
      .pipe(map(res => res.data));
  }

  setDefaultCustomerAddress(customerId: string, customerAddressId: string): Observable<void> {
    return this.http.put<void>(
      buildApiUrl(`/customers/${customerId}/addresses/${customerAddressId}/default`),
      null
    );
  }

  removeCustomerAddress(customerId: string, customerAddressId: string): Observable<void> {
    return this.http.delete<void>(buildApiUrl(`/customers/${customerId}/addresses/${customerAddressId}`));
  }

  // --- Supplier Addresses ---
  getSupplierAddresses(supplierId: string): Observable<SupplierAddressItem[]> {
    return this.http
      .get<ApiResponse<SupplierAddressItem[]>>(buildApiUrl(`/suppliers/${supplierId}/addresses`))
      .pipe(map(res => res?.data || []));
  }

  linkSupplierAddress(
    supplierId: string,
    dto: { addressId: string; addressTypeId: string; isDefault?: boolean }
  ): Observable<SupplierAddressItem> {
    return this.http
      .post<ApiResponse<SupplierAddressItem>>(buildApiUrl(`/suppliers/${supplierId}/addresses`), dto)
      .pipe(map(res => res.data));
  }

  setDefaultSupplierAddress(supplierId: string, supplierAddressId: string): Observable<void> {
    return this.http.put<void>(
      buildApiUrl(`/suppliers/${supplierId}/addresses/${supplierAddressId}/default`),
      null
    );
  }

  removeSupplierAddress(supplierId: string, supplierAddressId: string): Observable<void> {
    return this.http.delete<void>(buildApiUrl(`/suppliers/${supplierId}/addresses/${supplierAddressId}`));
  }

  // --- Units of Measure ---
  getUnitsOfMeasure(): Observable<UnitOfMeasureItem[]> {
    return this.http
      .get<ApiResponse<UnitOfMeasureItem[] | { items: UnitOfMeasureItem[] }>>(buildApiUrl('/uom'))
      .pipe(
        map(res => {
          if (!res?.data) return [];
          if (Array.isArray(res.data)) return res.data;
          return (res.data as { items: UnitOfMeasureItem[] }).items || [];
        })
      );
  }

  createUnitOfMeasure(data: { code: string; name: string; description?: string }): Observable<UnitOfMeasureItem> {
    return this.http
      .post<ApiResponse<UnitOfMeasureItem>>(buildApiUrl('/uom'), data)
      .pipe(map(res => res.data));
  }

  updateUnitOfMeasure(id: string, data: { name: string; description?: string }): Observable<UnitOfMeasureItem> {
    return this.http
      .put<ApiResponse<UnitOfMeasureItem>>(buildApiUrl(`/uom/${id}`), data)
      .pipe(map(res => res.data));
  }

  deactivateUnitOfMeasure(id: string): Observable<void> {
    return this.http
      .delete<void>(buildApiUrl(`/uom/${id}`));
  }

  getUomConversions(): Observable<UnitOfMeasureConversionItem[]> {
    return this.http
      .get<ApiResponse<UnitOfMeasureConversionItem[]>>(buildApiUrl('/uom/conversions'))
      .pipe(map(res => res?.data || []));
  }

  createUomConversion(data: { fromUnitId: string; toUnitId: string; conversionFactor: number }): Observable<UnitOfMeasureConversionItem> {
    return this.http
      .post<ApiResponse<UnitOfMeasureConversionItem>>(buildApiUrl('/uom/conversions'), data)
      .pipe(map(res => res.data));
  }

  convertQuantity(fromUnitId: string, toUnitId: string, quantity: number): Observable<{ fromUnitId: string; toUnitId: string; originalQuantity: number; convertedQuantity: number; effectiveFactor: number; isInverse: boolean }> {
    return this.http
      .get<ApiResponse<{ fromUnitId: string; toUnitId: string; originalQuantity: number; convertedQuantity: number; effectiveFactor: number; isInverse: boolean }>>(
        buildApiUrl(`/uom/convert?fromUnitId=${fromUnitId}&toUnitId=${toUnitId}&quantity=${quantity}`)
      )
      .pipe(map(res => res.data));
  }

  // --- Currencies ---
  getCurrencies(): Observable<CurrencyItem[]> {
    return this.http
      .get<ApiResponse<CurrencyItem[]>>(buildApiUrl('/currencies'))
      .pipe(map(res => res?.data || []));
  }

  createCurrency(data: { currencyCode: string; currencyName: string; symbol: string; decimalPlaces: number }): Observable<CurrencyItem> {
    return this.http
      .post<ApiResponse<CurrencyItem>>(buildApiUrl('/currencies'), data)
      .pipe(map(res => res.data));
  }

  // --- Warehouses ---
  getWarehouses(): Observable<WarehouseItem[]> {
    return this.http
      .get<ApiResponse<WarehouseItem[] | { items: WarehouseItem[] }>>(buildApiUrl('/warehouses'))
      .pipe(
        map(res => {
          if (!res?.data) return [];
          if (Array.isArray(res.data)) return res.data;
          return (res.data as { items: WarehouseItem[] }).items || [];
        })
      );
  }

  getWarehouse(id: string): Observable<WarehouseItem | null> {
    return this.http
      .get<ApiResponse<WarehouseItem>>(buildApiUrl(`/warehouses/${id}`))
      .pipe(map(res => res?.data ?? null));
  }

  createWarehouse(dto: CreateWarehouseDto): Observable<WarehouseItem> {
    return this.http
      .post<ApiResponse<WarehouseItem>>(buildApiUrl('/warehouses'), dto)
      .pipe(map(res => res.data));
  }

  updateWarehouse(id: string, dto: UpdateWarehouseDto): Observable<WarehouseItem> {
    return this.http
      .put<ApiResponse<WarehouseItem>>(buildApiUrl(`/warehouses/${id}`), dto)
      .pipe(map(res => res.data));
  }

  deactivateWarehouse(id: string): Observable<void> {
    return this.http.delete<void>(buildApiUrl(`/warehouses/${id}`));
  }

  // --- Warehouse Zones ---
  getWarehouseZones(warehouseId: string): Observable<WarehouseZoneItem[]> {
    return this.http
      .get<ApiResponse<WarehouseZoneItem[] | { items: WarehouseZoneItem[] }>>(
        buildApiUrl(`/warehouses/${warehouseId}/zones`)
      )
      .pipe(
        map(res => {
          if (!res?.data) return [];
          if (Array.isArray(res.data)) return res.data;
          return (res.data as { items: WarehouseZoneItem[] }).items || [];
        })
      );
  }

  getWarehouseZone(warehouseId: string, zoneId: string): Observable<WarehouseZoneItem | null> {
    return this.http
      .get<ApiResponse<WarehouseZoneItem>>(buildApiUrl(`/warehouses/${warehouseId}/zones/${zoneId}`))
      .pipe(map(res => res?.data ?? null));
  }

  createWarehouseZone(warehouseId: string, dto: CreateWarehouseZoneDto): Observable<WarehouseZoneItem> {
    return this.http
      .post<ApiResponse<WarehouseZoneItem>>(buildApiUrl(`/warehouses/${warehouseId}/zones`), dto)
      .pipe(map(res => res.data));
  }

  updateWarehouseZone(warehouseId: string, zoneId: string, dto: UpdateWarehouseZoneDto): Observable<WarehouseZoneItem> {
    return this.http
      .put<ApiResponse<WarehouseZoneItem>>(buildApiUrl(`/warehouses/${warehouseId}/zones/${zoneId}`), dto)
      .pipe(map(res => res.data));
  }

  deactivateWarehouseZone(warehouseId: string, zoneId: string): Observable<void> {
    return this.http.delete<void>(buildApiUrl(`/warehouses/${warehouseId}/zones/${zoneId}`));
  }

  // --- Warehouse Locations ---
  getWarehouseLocations(
    warehouseId: string,
    zoneId?: string | null,
    includeDetails: boolean = true
  ): Observable<WarehouseLocationItem[]> {
    let url = `/warehouses/${warehouseId}/locations?includeDetails=${includeDetails}`;
    if (zoneId) {
      url += `&zoneId=${zoneId}`;
    }
    return this.http
      .get<ApiResponse<WarehouseLocationItem[] | { items: WarehouseLocationItem[] }>>(buildApiUrl(url))
      .pipe(
        map(res => {
          if (!res?.data) return [];
          const rawItems = Array.isArray(res.data)
            ? res.data
            : (res.data as { items: WarehouseLocationItem[] }).items || [];
          return rawItems.map(item => ({
            ...item,
            locationId: item.warehouseLocationId ?? item.locationId ?? '',
            warehouseLocationId: item.warehouseLocationId ?? item.locationId ?? ''
          }));
        })
      );
  }

  getWarehouseLocation(locationId: string, warehouseId?: string | null): Observable<WarehouseLocationItem | null> {
    const url = warehouseId
      ? `/warehouses/${warehouseId}/locations/${locationId}`
      : `/warehouses/locations/${locationId}`;
    return this.http
      .get<ApiResponse<WarehouseLocationItem>>(buildApiUrl(url))
      .pipe(map(res => res?.data ?? null));
  }

  createWarehouseLocation(warehouseId: string, dto: CreateWarehouseLocationDto): Observable<WarehouseLocationItem> {
    return this.http
      .post<ApiResponse<WarehouseLocationItem>>(buildApiUrl(`/warehouses/${warehouseId}/locations`), dto)
      .pipe(map(res => res.data));
  }

  updateWarehouseLocation(locationId: string, dto: UpdateWarehouseLocationDto): Observable<WarehouseLocationItem> {
    return this.http
      .put<ApiResponse<WarehouseLocationItem>>(buildApiUrl(`/warehouses/locations/${locationId}`), dto)
      .pipe(map(res => res.data));
  }

  deactivateWarehouseLocation(locationId: string): Observable<void> {
    return this.http.delete<void>(buildApiUrl(`/warehouses/locations/${locationId}`));
  }

  // --- Location Types ---
  getLocationTypes(): Observable<LocationTypeItem[]> {
    return this.http
      .get<ApiResponse<LocationTypeItem[] | { items: LocationTypeItem[] }>>(buildApiUrl('/warehouses/location-types'))
      .pipe(
        map(res => {
          if (!res?.data) return [];
          if (Array.isArray(res.data)) return res.data;
          return (res.data as { items: LocationTypeItem[] }).items || [];
        })
      );
  }

  // --- Tax Codes ---
  getTaxCodes(applicability?: string, includeInactive: boolean = false): Observable<TaxCodeItem[]> {
    let url = `/tax-codes?includeInactive=${includeInactive}`;
    if (applicability) {
      url += `&applicability=${applicability}`;
    }
    return this.http
      .get<ApiResponse<TaxCodeItem[] | { items: TaxCodeItem[] }>>(buildApiUrl(url))
      .pipe(
        map(res => {
          if (!res?.data) return [];
          if (Array.isArray(res.data)) return res.data;
          return (res.data as { items: TaxCodeItem[] }).items || [];
        })
      );
  }

  getTaxCode(id: string): Observable<TaxCodeItem | null> {
    return this.http
      .get<ApiResponse<TaxCodeItem>>(buildApiUrl(`/tax-codes/${id}`))
      .pipe(map(res => res?.data ?? null));
  }

  createTaxCode(dto: CreateTaxCodeDto): Observable<TaxCodeItem> {
    return this.http
      .post<ApiResponse<TaxCodeItem>>(buildApiUrl('/tax-codes'), dto)
      .pipe(map(res => res.data));
  }

  updateTaxCode(id: string, dto: UpdateTaxCodeDto): Observable<TaxCodeItem> {
    return this.http
      .put<ApiResponse<TaxCodeItem>>(buildApiUrl(`/tax-codes/${id}`), dto)
      .pipe(map(res => res.data));
  }

  deactivateTaxCode(id: string): Observable<void> {
    return this.http.delete<void>(buildApiUrl(`/tax-codes/${id}`));
  }

  reactivateTaxCode(id: string): Observable<TaxCodeItem> {
    return this.http
      .post<ApiResponse<TaxCodeItem>>(buildApiUrl(`/tax-codes/${id}/reactivate`), {})
      .pipe(map(res => res.data));
  }
}
