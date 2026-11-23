import { describe, it, expect, beforeEach } from 'vitest';
import { TestBed, getTestBed } from '@angular/core/testing';
import {
  BrowserTestingModule,
  platformBrowserTesting
} from '@angular/platform-browser/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { GoodsReceiptNoteService } from './goods-receipt-note.service';
import {
  GoodsReceiptNoteDto,
  GoodsReceiptNoteStatus,
  CreateGoodsReceiptNoteRequest,
  UpdateGoodsReceiptNoteRequest
} from '../models/goods-receipt-note.models';

if (typeof (globalThis as any).document === 'undefined') {
  (globalThis as any).document = {
    createElement: () => ({ style: {} }),
    querySelector: () => null,
    querySelectorAll: () => [],
    body: {
      querySelector: () => null,
      querySelectorAll: () => [],
      appendChild: () => {},
      removeChild: () => {}
    },
    location: { href: 'http://localhost' }
  };
  (globalThis as any).window = globalThis;
}

try {
  getTestBed().initTestEnvironment(
    BrowserTestingModule,
    platformBrowserTesting()
  );
} catch {
  // Already initialized
}

describe('GoodsReceiptNoteService', () => {
  let service: GoodsReceiptNoteService;
  let httpTesting: HttpTestingController;

  const companyId = 'c1111111-1111-1111-1111-111111111111';
  const branchId = 'b1111111-1111-1111-1111-111111111111';
  const grnId = 'grn-101';
  const poId = 'po-101';

  const mockGrn: GoodsReceiptNoteDto = {
    goodsReceiptNoteId: grnId,
    tenantId: 't1',
    companyId,
    branchId,
    purchaseOrderId: poId,
    grnNumber: 'GRN-2026-001',
    receiptDate: '2026-10-18T10:00:00Z',
    status: GoodsReceiptNoteStatus.Draft,
    totalAmount: 250.0,
    lines: [
      {
        goodsReceiptNoteLineId: 'line-1',
        purchaseOrderItemId: 'poi-1',
        productId: 'prod-1',
        productCode: 'RAW-001',
        productName: 'Steel Sheet 2mm',
        quantityReceived: 10,
        unitCost: 25.0,
        lineTotal: 250.0
      }
    ]
  };

  beforeEach(() => {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        GoodsReceiptNoteService,
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    });
    service = TestBed.inject(GoodsReceiptNoteService);
    httpTesting = TestBed.inject(HttpTestingController);
  });

  it('should fetch goods receipts for authorized branch', () => {
    service.getGrns(companyId, branchId).subscribe(data => {
      expect(data.length).toBe(1);
      expect(data[0].grnNumber).toBe('GRN-2026-001');
    });

    const req = httpTesting.expectOne(
      r => r.url.includes(`/companies/${companyId}/branches/${branchId}/goods-receipt-notes`) && r.method === 'GET'
    );
    req.flush({ success: true, data: [mockGrn] });
  });

  it('should fetch single goods receipt by id', () => {
    service.getGrn(companyId, branchId, grnId).subscribe(data => {
      expect(data).toBeTruthy();
      expect(data?.goodsReceiptNoteId).toBe(grnId);
      expect(data?.lines[0].unitCost).toBe(25.0);
    });

    const req = httpTesting.expectOne(
      r => r.url.includes(`/companies/${companyId}/branches/${branchId}/goods-receipt-notes/${grnId}`) && r.method === 'GET'
    );
    req.flush({ success: true, data: mockGrn });
  });

  it('should create manual draft GRN with server response', () => {
    const createReq: CreateGoodsReceiptNoteRequest = {
      purchaseOrderId: poId,
      grnNumber: 'GRN-2026-001',
      lines: [
        {
          purchaseOrderItemId: 'poi-1',
          productId: 'prod-1',
          quantityReceived: 10
        }
      ]
    };

    service.createManualDraft(companyId, branchId, createReq).subscribe(data => {
      expect(data.status).toBe(GoodsReceiptNoteStatus.Draft);
    });

    const req = httpTesting.expectOne(
      r => r.url.includes(`/companies/${companyId}/branches/${branchId}/goods-receipt-notes`) && r.method === 'POST'
    );
    expect(req.request.body).toEqual(createReq);
    req.flush({ success: true, data: mockGrn });
  });

  it('should confirm goods receipt note and update status', () => {
    const confirmedGrn = { ...mockGrn, status: GoodsReceiptNoteStatus.Confirmed };

    service.confirmGrn(companyId, branchId, grnId).subscribe(data => {
      expect(data.status).toBe(GoodsReceiptNoteStatus.Confirmed);
    });

    const req = httpTesting.expectOne(
      r => r.url.includes(`/companies/${companyId}/branches/${branchId}/goods-receipt-notes/${grnId}/confirm`) && r.method === 'POST'
    );
    req.flush({ success: true, data: confirmedGrn });
  });

  it('should fetch active draft for PO if exists', () => {
    service.getActiveDraftForPo(companyId, branchId, poId).subscribe(data => {
      expect(data?.goodsReceiptNoteId).toBe(grnId);
      expect(data?.status).toBe(GoodsReceiptNoteStatus.Draft);
    });

    const req = httpTesting.expectOne(
      r => r.url.includes(`/companies/${companyId}/branches/${branchId}/goods-receipt-notes/from-po/${poId}/draft`) && r.method === 'GET'
    );
    req.flush({ success: true, data: mockGrn });
  });

  it('should confirm goods receipt note with direct post body', () => {
    const updateReq: UpdateGoodsReceiptNoteRequest = {
      deliveryNoteNumber: 'DN-DIRECT',
      lines: []
    };
    const confirmedGrn = { ...mockGrn, status: GoodsReceiptNoteStatus.Confirmed, deliveryNoteNumber: 'DN-DIRECT' };

    service.confirmGrn(companyId, branchId, grnId, updateReq).subscribe(data => {
      expect(data.status).toBe(GoodsReceiptNoteStatus.Confirmed);
      expect(data.deliveryNoteNumber).toBe('DN-DIRECT');
    });

    const req = httpTesting.expectOne(
      r => r.url.includes(`/companies/${companyId}/branches/${branchId}/goods-receipt-notes/${grnId}/confirm`) && r.method === 'POST'
    );
    expect(req.request.body).toEqual(updateReq);
    req.flush({ success: true, data: confirmedGrn });
  });

  it('should cancel goods receipt note draft', () => {
    const cancelledGrn = { ...mockGrn, status: GoodsReceiptNoteStatus.Cancelled };

    service.cancelGrn(companyId, branchId, grnId).subscribe(data => {
      expect(data.status).toBe(GoodsReceiptNoteStatus.Cancelled);
    });

    const req = httpTesting.expectOne(
      r => r.url.includes(`/companies/${companyId}/branches/${branchId}/goods-receipt-notes/${grnId}/cancel`) && r.method === 'POST'
    );
    req.flush({ success: true, data: cancelledGrn });
  });
});
