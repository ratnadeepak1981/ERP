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
  CategoryItem,
  UnitOfMeasureItem,
  WarehouseItem,
  WarehouseZoneItem,
  WarehouseLocationItem,
  LocationTypeItem,
  CreateWarehouseDto,
  UpdateWarehouseDto,
  CreateWarehouseZoneDto,
  UpdateWarehouseZoneDto,
  CreateWarehouseLocationDto,
  UpdateWarehouseLocationDto
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
          if (Array.isArray(res.data)) return res.data;
          return (res.data as { items: WarehouseLocationItem[] }).items || [];
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
}
