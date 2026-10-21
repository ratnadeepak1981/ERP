import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, of, throwError, switchMap, map, catchError } from 'rxjs';
import { buildApiUrl } from '../utils/api-url.util';
import { ApiResponse } from '../models/auth.models';
import { WorkspaceScopeService } from './workspace-scope.service';
import {
  PutawayRecordDto,
  PutawayLineDto,
  PutawayStatus,
  CreatePutawayRequest,
  PutawaySourceDocType,
  AvailableReceiptOption,
  getPutawayStatusName
} from '../models/putaway.models';

const STORAGE_KEY = 'apex_erp_stores_putaways';

@Injectable({
  providedIn: 'root'
})
export class PutawayService {
  private readonly http = inject(HttpClient);
  private readonly scopeService = inject(WorkspaceScopeService);

  private getCompanyId(explicitCompanyId?: string): string {
    const id = explicitCompanyId || this.scopeService.selectedCompany()?.id;
    if (!id) {
      throw new Error('An operating company must be selected.');
    }
    return id;
  }

  private getBranchId(explicitBranchId?: string): string {
    const id = explicitBranchId || this.scopeService.selectedBranch()?.id;
    if (!id) {
      throw new Error('An operating branch must be selected.');
    }
    return id;
  }

  private buildPath(companyId?: string, branchId?: string, suffix = ''): string {
    const cId = this.getCompanyId(companyId);
    const bId = this.getBranchId(branchId);
    return buildApiUrl(`/companies/${cId}/branches/${bId}/putaways${suffix}`);
  }

  /**
   * Generates next sequential putaway number placeholder
   */
  getNextPutawayNumber(): string {
    return '(Auto-generated)';
  }

  /**
   * Retrieves putaway records for register view from backend API.
   * If local storage contains legacy test transactions (e.g., from prior audit),
   * includes them seamlessly as fallbacks.
   */
  getPutaways(
    warehouseId?: string,
    status?: PutawayStatus,
    companyId?: string,
    branchId?: string
  ): Observable<PutawayRecordDto[]> {
    let params = new HttpParams();
    if (warehouseId && warehouseId !== 'all') {
      params = params.set('warehouseId', warehouseId);
    }
    if (status !== undefined && status !== null) {
      params = params.set('status', status.toString());
    }

    try {
      const url = this.buildPath(companyId, branchId);
      return this.http.get<ApiResponse<any>>(url, { params }).pipe(
        map(res => {
          const items = Array.isArray(res?.data) ? res.data : (res?.data?.items || []);
          return items.map((raw: any) => this.mapToRecordDto(raw));
        }),
        catchError(err => {
          console.warn('Backend putaway list fetch failed, falling back to cached storage:', err);
          return of(this.loadLegacyStorageRecords(warehouseId, status));
        })
      );
    } catch {
      return of(this.loadLegacyStorageRecords(warehouseId, status));
    }
  }

  /**
   * Retrieves a single putaway record by ID
   */
  getPutawayById(
    id: string,
    companyId?: string,
    branchId?: string
  ): Observable<PutawayRecordDto | null> {
    try {
      const url = this.buildPath(companyId, branchId, `/${id}`);
      return this.http.get<ApiResponse<any>>(url).pipe(
        map(res => (res?.data ? this.mapToRecordDto(res.data) : null)),
        catchError(() => {
          const legacy = this.loadLegacyStorageRecords().find(r => r.putawayId === id);
          return of(legacy || null);
        })
      );
    } catch {
      const legacy = this.loadLegacyStorageRecords().find(r => r.putawayId === id);
      return of(legacy || null);
    }
  }

  /**
   * Loads confirmed Stock Receipts and GRNs available for putaway from the backend API.
   * Fully put away receipts are excluded by the backend query.
   */
  getAvailableSourceReceipts(
    warehouseId?: string,
    companyId?: string,
    branchId?: string
  ): Observable<AvailableReceiptOption[]> {
    let params = new HttpParams();
    if (warehouseId && warehouseId !== 'all') {
      params = params.set('warehouseId', warehouseId);
    }

    try {
      const url = this.buildPath(companyId, branchId, '/eligible-receipts');
      return this.http.get<ApiResponse<any[]>>(url, { params }).pipe(
        map(res => {
          const items = res?.data || [];
          return items.map(item => {
            const docType: PutawaySourceDocType = (item.type === 2 || item.typeName === 'GoodsReceipt') ? 'GoodsReceipt' : 'StockReceipt';
            return {
              id: item.id,
              number: item.number,
              type: docType,
              date: item.date ? new Date(item.date).toISOString().substring(0, 10) : '',
              warehouseId: item.warehouseId,
              warehouseName: item.warehouseName || item.warehouseCode,
              lineCount: item.totalLines || 0,
              outstandingQuantity: Number(item.outstandingQuantity) || 0,
              companyId: item.companyId,
              branchId: item.branchId
            };
          });
        }),
        catchError(err => {
          console.error('Failed to load eligible receipts:', err);
          return of([]);
        })
      );
    } catch {
      return of([]);
    }
  }

  /**
   * Retrieves lines for a selected source receipt to pre-populate Putaway lines.
   * Returns authoritative server-calculated received, already put away, and outstanding quantities.
   */
  getLinesForSourceReceipt(
    receiptId: string,
    receiptNumber: string,
    docType: PutawaySourceDocType,
    companyId?: string,
    branchId?: string
  ): Observable<PutawayLineDto[]> {
    try {
      const numericType = docType === 'GoodsReceipt' ? 2 : 1;
      const url = this.buildPath(companyId, branchId, `/eligible-receipts/${numericType}/${receiptId}/lines`);
      return this.http.get<ApiResponse<any[]>>(url).pipe(
        map(res => {
          const items = res?.data || [];
          return items.map((l, idx) => ({
            lineId: l.sourceDocumentLineId || `pal-${idx + 1}-${l.productId}`,
            sourceLineId: l.sourceDocumentLineId,
            productId: l.productId,
            productCode: l.productCode || '',
            productName: l.productName || '',
            unitOfMeasure: l.unitOfMeasure || 'EA',
            sourceQuantity: Number(l.receivedQuantity) || 0,
            alreadyPutawayQuantity: Number(l.previouslyPutawayQuantity) || 0,
            outstandingQuantity: Number(l.outstandingQuantity) || 0,
            putawayQuantity: Number(l.outstandingQuantity) || 0,
            sourceLocationId: l.sourceWarehouseLocationId,
            sourceLocationCode: l.sourceLocationCode || '',
            destinationLocationId: '',
            destinationLocationCode: '',
            batchNumber: l.batchNumber,
            serialNumber: l.serialNumber,
            expiryDate: l.expiryDate,
            notes: '',
            allocations: [],
            selected: true
          }));
        }),
        catchError(err => {
          console.error('Failed to load eligible receipt lines:', err);
          return of([]);
        })
      );
    } catch {
      return of([]);
    }
  }

  /**
   * Saves a new putaway draft, and if confirm is requested, immediately executes
   * backend inventory posting to transition the transaction to Completed.
   */
  createPutaway(request: CreatePutawayRequest, isDraft: boolean = false): Observable<PutawayRecordDto> {
    const cId = this.getCompanyId(request.companyId);
    const bId = this.getBranchId(request.branchId);

    const payload = {
      putawayDate: request.putawayDate,
      sourceDocumentType: request.sourceDocumentType === 'GoodsReceipt' ? 2 : 1,
      sourceDocumentId: request.sourceDocumentId,
      sourceDocumentNumber: request.sourceDocumentNumber,
      warehouseId: request.warehouseId,
      notes: request.notes,
      lines: request.lines.map(l => {
        const destId = l.destinationWarehouseLocationId || l.destinationLocationId;
        const allocations = (l.allocations && l.allocations.length > 0)
          ? l.allocations
          : (destId ? [{ destinationWarehouseLocationId: destId, allocatedQuantity: Number(l.putawayQuantity) || 0 }] : []);

        return {
          sourceDocumentLineId: l.sourceLineId,
          productId: l.productId,
          sourceWarehouseLocationId: l.sourceLocationId,
          destinationWarehouseLocationId: destId,
          receivedQuantity: Number(l.sourceQuantity) || 0,
          putawayQuantity: Number(l.putawayQuantity) || 0,
          batchNumber: l.batchNumber,
          serialNumber: l.serialNumber,
          expiryDate: l.expiryDate,
          notes: l.notes,
          allocations
        };
      })
    };

    const url = buildApiUrl(`/companies/${cId}/branches/${bId}/putaways`);
    return this.http.post<ApiResponse<any>>(url, payload).pipe(
      map(res => this.mapToRecordDto(res.data)),
      switchMap(created => {
        if (!isDraft) {
          return this.confirmPutaway(created.putawayId, cId, bId);
        }
        return of(created);
      })
    );
  }

  /**
   * Confirms a draft putaway record and posts the inventory movements to the transaction ledger.
   */
  confirmPutaway(id: string, companyId?: string, branchId?: string): Observable<PutawayRecordDto> {
    const cId = this.getCompanyId(companyId);
    const bId = this.getBranchId(branchId);
    const url = buildApiUrl(`/companies/${cId}/branches/${bId}/putaways/${id}/confirm`);

    return this.http.post<ApiResponse<any>>(url, {}).pipe(
      map(res => this.mapToRecordDto(res.data))
    );
  }

  /**
   * Cancels a draft putaway record
   */
  cancelPutaway(id: string, companyId?: string, branchId?: string): Observable<PutawayRecordDto> {
    const cId = this.getCompanyId(companyId);
    const bId = this.getBranchId(branchId);
    const url = buildApiUrl(`/companies/${cId}/branches/${bId}/putaways/${id}/cancel`);

    return this.http.post<ApiResponse<any>>(url, {}).pipe(
      map(res => this.mapToRecordDto(res.data))
    );
  }

  private mapToRecordDto(raw: any): PutawayRecordDto {
    const lines: PutawayLineDto[] = (raw.lines || []).map((l: any, idx: number) => {
      const allocations = (l.allocations || []).map((a: any) => ({
        allocationId: a.putawayLineAllocationId,
        putawayLineId: a.putawayLineId,
        destinationLocationId: a.destinationWarehouseLocationId,
        destinationLocationCode: a.destinationLocationCode || '',
        destinationLocationName: a.destinationLocationName || '',
        allocatedQuantity: Number(a.allocatedQuantity) || 0,
        movementSequence: a.movementSequence,
        inventoryTransactionId: a.inventoryTransactionId
      }));

      const primaryDest = allocations.length > 0 ? allocations[0].destinationLocationId : (l.destinationWarehouseLocationId || '');
      const primaryDestCode = allocations.length > 0 ? allocations[0].destinationLocationCode : (l.destinationLocationCode || '');

      return {
        lineId: l.putawayLineId || `line-${idx + 1}`,
        sourceLineId: l.sourceDocumentLineId,
        productId: l.productId,
        productCode: l.productCode || '',
        productName: l.productName || '',
        unitOfMeasure: l.unitOfMeasure || 'EA',
        sourceQuantity: Number(l.receivedQuantity) || 0,
        alreadyPutawayQuantity: Number(l.previouslyPutawayQuantity) || 0,
        outstandingQuantity: Number(l.outstandingQuantity) || 0,
        putawayQuantity: Number(l.putawayQuantity) || 0,
        sourceLocationId: l.sourceWarehouseLocationId,
        sourceLocationCode: l.sourceLocationCode || '',
        destinationLocationId: primaryDest,
        destinationLocationCode: primaryDestCode,
        destinationLocationName: allocations.length > 0 ? allocations[0].destinationLocationName : '',
        batchNumber: l.batchNumber,
        serialNumber: l.serialNumber,
        expiryDate: l.expiryDate,
        notes: l.notes,
        allocations,
        selected: true
      };
    });

    const docType: PutawaySourceDocType = (raw.sourceDocumentType === 2 || raw.sourceDocumentTypeName === 'GoodsReceipt')
      ? 'GoodsReceipt'
      : 'StockReceipt';

    const status: PutawayStatus = typeof raw.status === 'number' ? raw.status : PutawayStatus.Draft;

    return {
      putawayId: raw.putawayId,
      putawayNumber: raw.putawayNumber || '',
      putawayDate: raw.putawayDate ? new Date(raw.putawayDate).toISOString().substring(0, 10) : '',
      sourceDocumentType: docType,
      sourceDocumentId: raw.sourceDocumentId,
      sourceDocumentNumber: raw.sourceDocumentNumber || '',
      warehouseId: raw.warehouseId,
      warehouseCode: raw.warehouseCode || '',
      warehouseName: raw.warehouseName || '',
      companyId: raw.companyId,
      branchId: raw.branchId,
      status,
      statusName: raw.statusName || getPutawayStatusName(status),
      totalLines: raw.totalLines || lines.length,
      totalQuantity: Number(raw.totalQuantity) || lines.reduce((acc, l) => acc + l.putawayQuantity, 0),
      notes: raw.notes || '',
      lines,
      createdAt: raw.createdAt || new Date().toISOString()
    };
  }

  private loadLegacyStorageRecords(warehouseId?: string, status?: PutawayStatus): PutawayRecordDto[] {
    try {
      const tenantId = this.scopeService.selectedCompany()?.tenantId || 'default';
      const raw = localStorage.getItem(`${STORAGE_KEY}_${tenantId}`) || localStorage.getItem(STORAGE_KEY);
      if (!raw) return [];
      let records: PutawayRecordDto[] = JSON.parse(raw);
      if (warehouseId && warehouseId !== 'all') {
        records = records.filter(r => r.warehouseId === warehouseId);
      }
      if (status !== undefined && status !== null) {
        records = records.filter(r => r.status === status);
      }
      return records;
    } catch {
      return [];
    }
  }
}
