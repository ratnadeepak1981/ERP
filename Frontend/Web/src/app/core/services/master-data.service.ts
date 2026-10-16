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
  WarehouseLocationItem
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

  getWarehouseLocations(warehouseId: string): Observable<WarehouseLocationItem[]> {
    return this.http
      .get<ApiResponse<WarehouseLocationItem[] | { items: WarehouseLocationItem[] }>>(
        buildApiUrl(`/warehouses/${warehouseId}/locations`)
      )
      .pipe(
        map(res => {
          if (!res?.data) return [];
          if (Array.isArray(res.data)) return res.data;
          return (res.data as { items: WarehouseLocationItem[] }).items || [];
        })
      );
  }
}
