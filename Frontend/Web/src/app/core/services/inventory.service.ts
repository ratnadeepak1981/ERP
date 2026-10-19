import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { buildApiUrl } from '../utils/api-url.util';
import { ApiResponse } from '../models/auth.models';
import {
  InventorySummaryDto,
  InventoryRecentTransactionDto,
  StockIssueDto,
  AvailableStockDto,
  CreateStockIssueRequest,
  StockReceiptDto,
  CreateStockReceiptRequest,
  StockReceiptReason
} from '../models/inventory.models';

export interface StockIssueQueryParams {
  warehouseId?: string;
  purpose?: number;
  page?: number;
  pageSize?: number;
  sortBy?: string;
  sortDirection?: string;
}

@Injectable({
  providedIn: 'root'
})
export class InventoryService {
  private readonly http = inject(HttpClient);

  /**
   * Retrieves high-level inventory metrics from the persistent database
   */
  getSummary(): Observable<InventorySummaryDto> {
    const url = buildApiUrl('/inventory/summary');
    return this.http.get<ApiResponse<InventorySummaryDto>>(url).pipe(
      map(res => {
        if (!res?.data) {
          return {
            totalStockItems: 0,
            totalWarehouses: 0,
            totalLocations: 0,
            lowStockItems: 0,
            totalTransactions: 0,
            totalOnHandQuantity: 0,
            totalInventoryValue: 0
          };
        }
        return res.data;
      })
    );
  }

  /**
   * Retrieves the most recent inventory transactions from the persistent ledger
   */
  getRecentTransactions(limit = 10): Observable<InventoryRecentTransactionDto[]> {
    const url = buildApiUrl('/inventory/recent-transactions');
    let params = new HttpParams();
    if (limit) {
      params = params.set('limit', limit.toString());
    }

    return this.http.get<ApiResponse<InventoryRecentTransactionDto[]>>(url, { params }).pipe(
      map(res => {
        if (!res?.data) return [];
        return Array.isArray(res.data) ? res.data : [];
      })
    );
  }

  /**
   * Retrieves stock issue records from the ledger with optional filters
   */
  getStockIssues(params?: StockIssueQueryParams): Observable<StockIssueDto[]> {
    const url = buildApiUrl('/inventory/stock-issues');
    let httpParams = new HttpParams();
    if (params?.warehouseId) httpParams = httpParams.set('warehouseId', params.warehouseId);
    if (params?.purpose !== undefined && params.purpose !== null) {
      httpParams = httpParams.set('purpose', params.purpose.toString());
    }
    if (params?.page !== undefined) httpParams = httpParams.set('page', params.page.toString());
    if (params?.pageSize !== undefined) httpParams = httpParams.set('pageSize', params.pageSize.toString());
    if (params?.sortBy) httpParams = httpParams.set('sortBy', params.sortBy);
    if (params?.sortDirection) httpParams = httpParams.set('sortDirection', params.sortDirection);

    return this.http.get<ApiResponse<StockIssueDto[] | { items: StockIssueDto[]; totalCount: number }>>(url, { params: httpParams }).pipe(
      map(res => {
        if (!res?.data) return [];
        if (Array.isArray(res.data)) return res.data;
        return (res.data as { items: StockIssueDto[] }).items || [];
      })
    );
  }

  /**
   * Retrieves a single stock issue record by its transaction ID
   */
  getStockIssue(id: string): Observable<StockIssueDto | null> {
    const url = buildApiUrl(`/inventory/stock-issues/${id}`);
    return this.http.get<ApiResponse<StockIssueDto>>(url).pipe(
      map(res => res?.data ?? null)
    );
  }

  /**
   * Retrieves available stock for a product at a specific warehouse location
   */
  getAvailableStock(
    productId: string,
    warehouseLocationId: string,
    batchNumber?: string | null,
    serialNumber?: string | null
  ): Observable<AvailableStockDto> {
    const url = buildApiUrl('/inventory/available-stock');
    let params = new HttpParams()
      .set('productId', productId)
      .set('warehouseLocationId', warehouseLocationId);

    if (batchNumber) params = params.set('batchNumber', batchNumber);
    if (serialNumber) params = params.set('serialNumber', serialNumber);

    return this.http.get<ApiResponse<AvailableStockDto>>(url, { params }).pipe(
      map(res => {
        if (!res?.data) {
          return {
            productId,
            warehouseLocationId,
            quantityOnHand: 0,
            quantityAllocated: 0,
            quantityQuarantine: 0,
            quantityAvailable: 0
          };
        }
        return res.data;
      })
    );
  }

  /**
   * Posts an authorized stock issue movement against the persistent ledger
   */
  createStockIssue(request: CreateStockIssueRequest): Observable<StockIssueDto[]> {
    const url = buildApiUrl('/inventory/stock-issues');
    return this.http.post<ApiResponse<StockIssueDto[]>>(url, request).pipe(
      map(res => {
        if (!res?.data) return [];
        return Array.isArray(res.data) ? res.data : [res.data];
      })
    );
  }

  /**
   * Retrieves stock receipt records from the ledger with optional filters
   */
  getStockReceipts(params?: StockReceiptQueryParams): Observable<StockReceiptDto[]> {
    const url = buildApiUrl('/inventory/stock-receipts');
    let httpParams = new HttpParams();
    if (params?.warehouseId) httpParams = httpParams.set('warehouseId', params.warehouseId);
    if (params?.reason !== undefined && params.reason !== null) {
      httpParams = httpParams.set('reason', params.reason.toString());
    }
    if (params?.page !== undefined) httpParams = httpParams.set('page', params.page.toString());
    if (params?.pageSize !== undefined) httpParams = httpParams.set('pageSize', params.pageSize.toString());
    if (params?.sortBy) httpParams = httpParams.set('sortBy', params.sortBy);
    if (params?.sortDirection) httpParams = httpParams.set('sortDirection', params.sortDirection);

    return this.http.get<ApiResponse<StockReceiptDto[] | { items: StockReceiptDto[]; totalCount: number }>>(url, { params: httpParams }).pipe(
      map(res => {
        if (!res?.data) return [];
        if (Array.isArray(res.data)) return res.data;
        return (res.data as { items: StockReceiptDto[] }).items || [];
      })
    );
  }

  /**
   * Retrieves a single stock receipt record by its transaction ID
   */
  getStockReceipt(id: string): Observable<StockReceiptDto | null> {
    const url = buildApiUrl(`/inventory/stock-receipts/${id}`);
    return this.http.get<ApiResponse<StockReceiptDto>>(url).pipe(
      map(res => res?.data ?? null)
    );
  }

  /**
   * Posts an authorized stock receipt movement against the persistent ledger
   */
  createStockReceipt(request: CreateStockReceiptRequest): Observable<StockReceiptDto[]> {
    const url = buildApiUrl('/inventory/stock-receipts');
    return this.http.post<ApiResponse<StockReceiptDto[]>>(url, request).pipe(
      map(res => {
        if (!res?.data) return [];
        return Array.isArray(res.data) ? res.data : [res.data];
      })
    );
  }
}

export interface StockReceiptQueryParams {
  warehouseId?: string;
  reason?: StockReceiptReason | number;
  page?: number;
  pageSize?: number;
  sortBy?: string;
  sortDirection?: 'asc' | 'desc';
}
