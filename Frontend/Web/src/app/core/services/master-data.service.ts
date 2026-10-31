import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, map, of, tap } from 'rxjs';
import { buildApiUrl } from '../utils/api-url.util';
import { ApiResponse } from '../models/auth.models';
import {
  PriceCardItem,
  CreatePriceCardDto,
  UpdatePriceCardDto,
  PriceCardQueryParams
} from '../models/price-card.models';
import { ExchangeRateItem, ExchangeRateRequest } from '../models/exchange-rate.models';
import {
  ProductItem,
  CreateProductDto,
  UpdateProductDto,
  ProductPackagingItem,
  SupplierItem,
  CreateSupplierDto,
  CustomerItem,
  CreateCustomerDto,
  CategoryItem,
  CategoryTreeNode,
  CategoryNodeState,
  PagedResult,
  UnitOfMeasureItem,
  UnitOfMeasureConversionItem,
  CurrencyItem,
  CreateCurrencyDto,
  UpdateCurrencyDto,
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
      .pipe(
        map(res => res.data),
        tap(() => (this.cachedAllProducts = null))
      );
  }

  updateProduct(id: string, dto: UpdateProductDto): Observable<ProductItem> {
    return this.http
      .put<ApiResponse<ProductItem>>(buildApiUrl(`/products/${id}`), dto)
      .pipe(
        map(res => res.data),
        tap(() => (this.cachedAllProducts = null))
      );
  }

  deactivateProduct(id: string): Observable<void> {
    return this.http.delete<void>(buildApiUrl(`/products/${id}`)).pipe(
      tap(() => (this.cachedAllProducts = null))
    );
  }

  getProductPackagings(productId: string): Observable<ProductPackagingItem[]> {
    return this.http
      .get<ApiResponse<ProductPackagingItem[]>>(buildApiUrl(`/products/${productId}/packagings`))
      .pipe(map(res => res?.data ?? []));
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

  createCategory(dto: { categoryCode: string; categoryName: string; description?: string; parentCategoryId?: string | null }): Observable<CategoryItem> {
    return this.http
      .post<ApiResponse<CategoryItem>>(buildApiUrl('/categories'), dto)
      .pipe(
        map(res => res.data),
        tap(() => this.cachedAllCategories = null)
      );
  }

  updateCategory(id: string, dto: { categoryName: string; description?: string; parentCategoryId?: string | null }): Observable<CategoryItem> {
    return this.http
      .put<ApiResponse<CategoryItem>>(buildApiUrl(`/categories/${id}`), dto)
      .pipe(
        map(res => res.data),
        tap(() => this.cachedAllCategories = null)
      );
  }

  deactivateCategory(id: string): Observable<void> {
    return this.http.delete<void>(buildApiUrl(`/categories/${id}`)).pipe(
      tap(() => this.cachedAllCategories = null)
    );
  }

  reactivateCategory(id: string): Observable<CategoryItem> {
    return this.http
      .post<ApiResponse<CategoryItem>>(buildApiUrl(`/categories/${id}/reactivate`), null)
      .pipe(
        map(res => res.data),
        tap(() => this.cachedAllCategories = null)
      );
  }

  // --- Category Hierarchy & Lazy Loading ---
  private cachedAllCategories: CategoryItem[] | null = null;
  private cachedAllProducts: ProductItem[] | null = null;

  clearHierarchyCache(): void {
    this.cachedAllCategories = null;
    this.cachedAllProducts = null;
  }

  getRootCategories(page: number = 1, pageSize: number = 20): Observable<PagedResult<CategoryTreeNode>> {
    const fetch$ = this.cachedAllCategories
      ? of(this.cachedAllCategories)
      : this.getCategories().pipe(tap(cats => (this.cachedAllCategories = cats)));

    return fetch$.pipe(
      map(allCategories => {
        const parentIdsWithChildren = new Set(
          allCategories
            .filter(c => !!c.parentCategoryId)
            .map(c => c.parentCategoryId!)
        );

        const roots: CategoryTreeNode[] = allCategories
          .filter(c => !c.parentCategoryId)
          .map(c => ({
            ...c,
            level: 0,
            hasChildren: parentIdsWithChildren.has(c.categoryId)
          }));

        const totalCount = roots.length;
        const startIndex = (page - 1) * pageSize;
        const items = roots.slice(startIndex, startIndex + pageSize);
        const hasMore = startIndex + items.length < totalCount;

        return {
          items,
          totalCount,
          page,
          pageSize,
          hasMore
        };
      })
    );
  }

  getCategoryChildren(categoryId: string, page: number = 1, pageSize: number = 20): Observable<PagedResult<CategoryTreeNode>> {
    const fetch$ = this.cachedAllCategories
      ? of(this.cachedAllCategories)
      : this.getCategories().pipe(tap(cats => (this.cachedAllCategories = cats)));

    return fetch$.pipe(
      map(allCategories => {
        const parentIdsWithChildren = new Set(
          allCategories
            .filter(c => !!c.parentCategoryId)
            .map(c => c.parentCategoryId!)
        );

        const children: CategoryTreeNode[] = allCategories
          .filter(c => c.parentCategoryId === categoryId)
          .map(c => ({
            ...c,
            hasChildren: parentIdsWithChildren.has(c.categoryId)
          }));

        const totalCount = children.length;
        const startIndex = (page - 1) * pageSize;
        const items = children.slice(startIndex, startIndex + pageSize);
        const hasMore = startIndex + items.length < totalCount;

        return {
          items,
          totalCount,
          page,
          pageSize,
          hasMore
        };
      })
    );
  }

  getCategoryProducts(categoryId: string, page: number = 1, pageSize: number = 20): Observable<PagedResult<ProductItem>> {
    const fetch$ = this.cachedAllProducts
      ? of(this.cachedAllProducts)
      : this.getProducts().pipe(tap(prods => (this.cachedAllProducts = prods)));

    return fetch$.pipe(
      map(allProducts => {
        const categoryProducts = allProducts.filter(p => p.categoryId === categoryId);
        const totalCount = categoryProducts.length;
        const startIndex = (page - 1) * pageSize;
        const items = categoryProducts.slice(startIndex, startIndex + pageSize);
        const hasMore = startIndex + items.length < totalCount;

        return {
          items,
          totalCount,
          page,
          pageSize,
          hasMore
        };
      })
    );
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

  getCurrency(id: string): Observable<CurrencyItem | null> {
    return this.http
      .get<ApiResponse<CurrencyItem>>(buildApiUrl(`/currencies/${id}`))
      .pipe(map(res => res?.data ?? null));
  }

  createCurrency(data: CreateCurrencyDto): Observable<CurrencyItem> {
    return this.http
      .post<ApiResponse<CurrencyItem>>(buildApiUrl('/currencies'), data)
      .pipe(map(res => res.data));
  }

  updateCurrency(id: string, dto: UpdateCurrencyDto): Observable<CurrencyItem> {
    return this.http
      .put<ApiResponse<CurrencyItem>>(buildApiUrl(`/currencies/${id}`), dto)
      .pipe(map(res => res.data));
  }

  deactivateCurrency(id: string): Observable<void> {
    return this.http.delete<void>(buildApiUrl(`/currencies/${id}`));
  }

  activateCurrency(id: string): Observable<void> {
    return this.http.post<void>(buildApiUrl(`/currencies/${id}/activate`), {});
  }

  // --- Exchange Rates ---
  getExchangeRates(): Observable<ExchangeRateItem[]> {
    return this.http
      .get<ApiResponse<ExchangeRateItem[]>>(buildApiUrl('/exchange-rates'))
      .pipe(map(res => res?.data || []));
  }

  createExchangeRate(data: ExchangeRateRequest): Observable<ExchangeRateItem> {
    return this.http
      .post<ApiResponse<ExchangeRateItem>>(buildApiUrl('/exchange-rates'), data)
      .pipe(map(res => res.data));
  }

  updateExchangeRate(id: string, data: ExchangeRateRequest): Observable<ExchangeRateItem> {
    return this.http
      .put<ApiResponse<ExchangeRateItem>>(buildApiUrl(`/exchange-rates/${id}`), data)
      .pipe(map(res => res.data));
  }

  deactivateExchangeRate(id: string): Observable<void> {
    return this.http.delete<void>(buildApiUrl(`/exchange-rates/${id}`));
  }

  activateExchangeRate(id: string): Observable<void> {
    return this.http.post<void>(buildApiUrl(`/exchange-rates/${id}/activate`), {});
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

  activateWarehouse(id: string): Observable<WarehouseItem> {
    return this.http
      .post<ApiResponse<WarehouseItem>>(buildApiUrl(`/warehouses/${id}/activate`), {})
      .pipe(map(res => res.data));
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

  activateWarehouseZone(warehouseId: string, zoneId: string): Observable<WarehouseZoneItem> {
    return this.http
      .post<ApiResponse<WarehouseZoneItem>>(buildApiUrl(`/warehouses/${warehouseId}/zones/${zoneId}/activate`), {})
      .pipe(map(res => res.data));
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

  activateWarehouseLocation(locationId: string, warehouseId?: string | null): Observable<WarehouseLocationItem> {
    const url = warehouseId
      ? `/warehouses/${warehouseId}/locations/${locationId}/activate`
      : `/warehouses/locations/${locationId}/activate`;
    return this.http
      .post<ApiResponse<WarehouseLocationItem>>(buildApiUrl(url), {})
      .pipe(map(res => res.data));
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

  // --- Price Cards ---
  getPriceCards(params?: PriceCardQueryParams): Observable<PagedResult<PriceCardItem>> {
    let httpParams = new HttpParams();
    if (params) {
      if (params.type !== undefined && params.type !== null) {
        httpParams = httpParams.set('type', params.type.toString());
      }
      if (params.search) {
        httpParams = httpParams.set('search', params.search);
      }
      if (params.isActive !== undefined && params.isActive !== null) {
        httpParams = httpParams.set('isActive', params.isActive.toString());
      }
      if (params.page !== undefined && params.page !== null) {
        httpParams = httpParams.set('page', params.page.toString());
      }
      if (params.pageSize !== undefined && params.pageSize !== null) {
        httpParams = httpParams.set('pageSize', params.pageSize.toString());
      }
      if (params.sortBy) {
        httpParams = httpParams.set('sortBy', params.sortBy);
      }
      if (params.sortDirection) {
        httpParams = httpParams.set('sortDirection', params.sortDirection);
      }
    }

    return this.http
      .get<ApiResponse<PagedResult<PriceCardItem> | PriceCardItem[]>>(buildApiUrl('/master-data/price-cards'), { params: httpParams })
      .pipe(
        map(res => {
          if (!res?.data) {
            return { items: [], totalCount: 0, page: 1, pageSize: 10, hasMore: false };
          }
          if (Array.isArray(res.data)) {
            return {
              items: res.data,
              totalCount: res.data.length,
              page: 1,
              pageSize: res.data.length || 10,
              hasMore: false
            };
          }
          const paged = res.data as PagedResult<PriceCardItem>;
          return {
            items: paged.items || [],
            totalCount: paged.totalCount ?? (paged.items ? paged.items.length : 0),
            page: paged.page ?? 1,
            pageSize: paged.pageSize ?? 10,
            hasMore: paged.hasMore ?? false
          };
        })
      );
  }

  getPriceCard(id: string): Observable<PriceCardItem | null> {
    return this.http
      .get<ApiResponse<PriceCardItem>>(buildApiUrl(`/master-data/price-cards/${id}`))
      .pipe(map(res => res?.data ?? null));
  }

  createPriceCard(dto: CreatePriceCardDto): Observable<PriceCardItem> {
    return this.http
      .post<ApiResponse<PriceCardItem>>(buildApiUrl('/master-data/price-cards'), dto)
      .pipe(map(res => res.data));
  }

  updatePriceCard(id: string, dto: UpdatePriceCardDto): Observable<PriceCardItem> {
    return this.http
      .put<ApiResponse<PriceCardItem>>(buildApiUrl(`/master-data/price-cards/${id}`), dto)
      .pipe(map(res => res.data));
  }

  deactivatePriceCard(id: string): Observable<void> {
    return this.http.patch<void>(buildApiUrl(`/master-data/price-cards/${id}/deactivate`), {});
  }

  reactivatePriceCard(id: string): Observable<void> {
    return this.http.patch<void>(buildApiUrl(`/master-data/price-cards/${id}/reactivate`), {});
  }
}
