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
  StockReceiptReason,
  StockByLocationReportQuery,
  StockByLocationReportDto,
  StockLedgerReportQuery,
  StockLedgerReportDto
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

  /**
   * Retrieves the current stock by location report (storage, staging, or all)
   */
  getStockByLocationReport(params?: StockByLocationReportQuery): Observable<StockByLocationReportDto> {
    const url = buildApiUrl('/inventory/reports/stock-by-location');
    let httpParams = new HttpParams();
    if (params?.warehouseId) httpParams = httpParams.set('warehouseId', params.warehouseId);
    if (params?.companyId) httpParams = httpParams.set('companyId', params.companyId);
    if (params?.branchId) httpParams = httpParams.set('branchId', params.branchId);
    if (params?.locationId) httpParams = httpParams.set('locationId', params.locationId);
    if (params?.productId) httpParams = httpParams.set('productId', params.productId);
    if (params?.categoryId) httpParams = httpParams.set('categoryId', params.categoryId);
    if (params?.locationClassification) {
      httpParams = httpParams.set('locationClassification', params.locationClassification);
    }
    if (params?.search) httpParams = httpParams.set('search', params.search);
    if (params?.page !== undefined && params?.page !== null) {
      httpParams = httpParams.set('page', params.page.toString());
    }
    if (params?.pageSize !== undefined && params?.pageSize !== null) {
      httpParams = httpParams.set('pageSize', params.pageSize.toString());
    }
    if (params?.sortBy) httpParams = httpParams.set('sortBy', params.sortBy);
    if (params?.sortDirection) httpParams = httpParams.set('sortDirection', params.sortDirection);

    return this.http.get<ApiResponse<StockByLocationReportDto>>(url, { params: httpParams }).pipe(
      map(res => {
        if (!res?.data) {
          return {
            items: [],
            totalCount: 0,
            totalQuantityOnHand: 0,
            totalQuantityAvailable: 0,
            totalStockValue: 0,
            currency: 'LKR',
            page: 1,
            pageSize: 25
          };
        }
        return res.data;
      })
    );
  }

  /**
   * Retrieves the product-wise, location-wise stock ledger transaction history and valuation
   */
  getStockLedgerReport(params: StockLedgerReportQuery): Observable<StockLedgerReportDto> {
    const url = buildApiUrl('/inventory/reports/stock-ledger');
    let httpParams = new HttpParams()
      .set('productId', params.productId)
      .set('warehouseId', params.warehouseId);

    if (params.warehouseLocationId) {
      httpParams = httpParams.set('warehouseLocationId', params.warehouseLocationId);
    }

    if (params.companyId) httpParams = httpParams.set('companyId', params.companyId);
    if (params.branchId) httpParams = httpParams.set('branchId', params.branchId);
    if (params.dateFrom) httpParams = httpParams.set('dateFrom', params.dateFrom);
    if (params.dateTo) httpParams = httpParams.set('dateTo', params.dateTo);
    if (params.search) httpParams = httpParams.set('search', params.search);
    if (params.page !== undefined && params.page !== null) {
      httpParams = httpParams.set('page', params.page.toString());
    }
    if (params.pageSize !== undefined && params.pageSize !== null) {
      httpParams = httpParams.set('pageSize', params.pageSize.toString());
    }

    return this.http.get<ApiResponse<StockLedgerReportDto>>(url, { params: httpParams }).pipe(
      map(res => {
        if (!res?.data) {
          return {
            productId: params.productId,
            productCode: '',
            productName: '',
            unitOfMeasure: '',
            warehouseId: params.warehouseId,
            warehouseCode: '',
            warehouseName: '',
            warehouseLocationId: params.warehouseLocationId ?? null,
            locationCode: '',
            locationName: '',
            hierarchyPath: '',
            valuationMethod: 'FIFO',
            currency: 'LKR',
            openingQuantity: 0,
            openingValue: 0,
            totalQuantityIn: 0,
            totalInwardValue: 0,
            totalQuantityOut: 0,
            totalOutwardValue: 0,
            closingQuantity: 0,
            closingValue: 0,
            items: [],
            totalCount: 0,
            page: 1,
            pageSize: 25
          };
        }
        return res.data;
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
