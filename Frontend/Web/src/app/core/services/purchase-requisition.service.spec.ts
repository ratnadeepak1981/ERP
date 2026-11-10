import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { PurchaseRequisitionService } from './purchase-requisition.service';
import { environment } from '../../../environments/environment';
import {
  PurchaseRequisitionStatus,
  CreatePurchaseRequisitionRequest,
  UpdatePurchaseRequisitionRequest,
  CreatePurchaseRequisitionItemRequest,
  UpdatePurchaseRequisitionItemRequest
} from '../models/purchase-requisition.models';

describe('PurchaseRequisitionService', () => {
  let service: PurchaseRequisitionService;
  let httpMock: HttpTestingController;

  const companyId = 'c1111111-1111-1111-1111-111111111111';
  const branchId = 'b1111111-1111-1111-1111-111111111111';
  const basePath = `${environment.apiBaseUrl}/companies/${companyId}/branches/${branchId}/purchase-requisitions`;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [PurchaseRequisitionService, provideHttpClient(), provideHttpClientTesting()]
    });

    service = TestBed.inject(PurchaseRequisitionService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('should fetch purchase requisitions list', () => {
    service.getRequisitions(companyId, branchId).subscribe(reqs => {
      expect(reqs.length).toBe(1);
      expect(reqs[0].requisitionNumber).toBe('PR-2026-001');
    });

    const req = httpMock.expectOne(basePath);
    expect(req.request.method).toBe('GET');
    req.flush({
      succeeded: true,
      data: [
        {
          id: 'pr-1',
          tenantId: 't1',
          companyId,
          branchId,
          requisitionNumber: 'PR-2026-001',
          requisitionDate: '2026-10-18T00:00:00Z',
          requestorUserId: 'u1',
          status: PurchaseRequisitionStatus.Draft,
          totalAmount: 500,
          isActive: true,
          items: []
        }
      ]
    });
  });

  it('should fetch single purchase requisition by id', () => {
    service.getRequisition(companyId, branchId, 'pr-1').subscribe(req => {
      expect(req).toBeTruthy();
      expect(req?.requisitionNumber).toBe('PR-2026-001');
    });

    const req = httpMock.expectOne(`${basePath}/pr-1`);
    expect(req.request.method).toBe('GET');
    req.flush({
      succeeded: true,
      data: {
        id: 'pr-1',
        tenantId: 't1',
        companyId,
        branchId,
        requisitionNumber: 'PR-2026-001',
        requisitionDate: '2026-10-18T00:00:00Z',
        requestorUserId: 'u1',
        status: PurchaseRequisitionStatus.Draft,
        totalAmount: 500,
        isActive: true,
        items: []
      }
    });
  });

  it('should create a purchase requisition', () => {
    const request: CreatePurchaseRequisitionRequest = {
      requisitionNumber: 'PR-2026-002',
      items: [
        {
          productId: 'p1',
          quantity: 10,
          estimatedUnitPrice: 50
        }
      ]
    };

    service.createRequisition(companyId, branchId, request).subscribe(res => {
      expect(res.requisitionNumber).toBe('PR-2026-002');
      expect(res.status).toBe(PurchaseRequisitionStatus.Draft);
    });

    const req = httpMock.expectOne(basePath);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(request);
    req.flush({
      succeeded: true,
      data: {
        id: 'pr-2',
        tenantId: 't1',
        companyId,
        branchId,
        requisitionNumber: 'PR-2026-002',
        requisitionDate: '2026-10-18T00:00:00Z',
        requestorUserId: 'u1',
        status: PurchaseRequisitionStatus.Draft,
        totalAmount: 500,
        isActive: true,
        items: []
      }
    });
  });

  it('should update a purchase requisition draft header', () => {
    const request: UpdatePurchaseRequisitionRequest = {
      notes: 'Updated notes'
    };

    service.updateRequisition(companyId, branchId, 'pr-1', request).subscribe(res => {
      expect(res.notes).toBe('Updated notes');
    });

    const req = httpMock.expectOne(`${basePath}/pr-1`);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual(request);
    req.flush({
      succeeded: true,
      data: {
        id: 'pr-1',
        tenantId: 't1',
        companyId,
        branchId,
        requisitionNumber: 'PR-2026-001',
        requisitionDate: '2026-10-18T00:00:00Z',
        requestorUserId: 'u1',
        status: PurchaseRequisitionStatus.Draft,
        totalAmount: 500,
        notes: 'Updated notes',
        isActive: true,
        items: []
      }
    });
  });

  it('should delete a purchase requisition draft', () => {
    service.deleteRequisition(companyId, branchId, 'pr-1').subscribe();

    const req = httpMock.expectOne(`${basePath}/pr-1`);
    expect(req.request.method).toBe('DELETE');
    req.flush({ succeeded: true, data: null });
  });

  it('should add item to purchase requisition', () => {
    const request: CreatePurchaseRequisitionItemRequest = {
      productId: 'p2',
      quantity: 5,
      estimatedUnitPrice: 30
    };

    service.addItem(companyId, branchId, 'pr-1', request).subscribe(item => {
      expect(item.productId).toBe('p2');
      expect(item.quantity).toBe(5);
    });

    const req = httpMock.expectOne(`${basePath}/pr-1/items`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(request);
    req.flush({
      succeeded: true,
      data: {
        id: 'item-2',
        purchaseRequisitionId: 'pr-1',
        productId: 'p2',
        quantity: 5,
        estimatedUnitPrice: 30,
        estimatedLineTotal: 150,
        isActive: true
      }
    });
  });

  it('should update item on purchase requisition', () => {
    const request: UpdatePurchaseRequisitionItemRequest = {
      productId: 'p2',
      quantity: 8,
      estimatedUnitPrice: 30
    };

    service.updateItem(companyId, branchId, 'pr-1', 'item-2', request).subscribe(item => {
      expect(item.quantity).toBe(8);
    });

    const req = httpMock.expectOne(`${basePath}/pr-1/items/item-2`);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual(request);
    req.flush({
      succeeded: true,
      data: {
        id: 'item-2',
        purchaseRequisitionId: 'pr-1',
        productId: 'p2',
        quantity: 8,
        estimatedUnitPrice: 30,
        estimatedLineTotal: 240,
        isActive: true
      }
    });
  });

  it('should delete item on purchase requisition', () => {
    service.deleteItem(companyId, branchId, 'pr-1', 'item-2').subscribe();

    const req = httpMock.expectOne(`${basePath}/pr-1/items/item-2`);
    expect(req.request.method).toBe('DELETE');
    req.flush({ succeeded: true, data: null });
  });

  it('should submit requisition', () => {
    service.submitRequisition(companyId, branchId, 'pr-1').subscribe(res => {
      expect(res.status).toBe(PurchaseRequisitionStatus.Submitted);
    });

    const req = httpMock.expectOne(`${basePath}/pr-1/submit`);
    expect(req.request.method).toBe('POST');
    req.flush({
      succeeded: true,
      data: {
        id: 'pr-1',
        tenantId: 't1',
        companyId,
        branchId,
        requisitionNumber: 'PR-2026-001',
        requisitionDate: '2026-10-18T00:00:00Z',
        requestorUserId: 'u1',
        status: PurchaseRequisitionStatus.Submitted,
        totalAmount: 500,
        isActive: true,
        items: []
      }
    });
  });

  it('should approve requisition', () => {
    service.approveRequisition(companyId, branchId, 'pr-1', 'Approved for procurement').subscribe(res => {
      expect(res.status).toBe(PurchaseRequisitionStatus.Approved);
    });

    const req = httpMock.expectOne(`${basePath}/pr-1/approve`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ remarks: 'Approved for procurement' });
    req.flush({
      succeeded: true,
      data: {
        id: 'pr-1',
        tenantId: 't1',
        companyId,
        branchId,
        requisitionNumber: 'PR-2026-001',
        requisitionDate: '2026-10-18T00:00:00Z',
        requestorUserId: 'u1',
        status: PurchaseRequisitionStatus.Approved,
        totalAmount: 500,
        isActive: true,
        items: []
      }
    });
  });

  it('should reject requisition', () => {
    service.rejectRequisition(companyId, branchId, 'pr-1', 'Budget exceeded').subscribe(res => {
      expect(res.status).toBe(PurchaseRequisitionStatus.Rejected);
    });

    const req = httpMock.expectOne(`${basePath}/pr-1/reject`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ remarks: 'Budget exceeded' });
    req.flush({
      succeeded: true,
      data: {
        id: 'pr-1',
        tenantId: 't1',
        companyId,
        branchId,
        requisitionNumber: 'PR-2026-001',
        requisitionDate: '2026-10-18T00:00:00Z',
        requestorUserId: 'u1',
        status: PurchaseRequisitionStatus.Rejected,
        totalAmount: 500,
        isActive: true,
        items: []
      }
    });
  });

  it('should cancel requisition', () => {
    service.cancelRequisition(companyId, branchId, 'pr-1', 'No longer required').subscribe(res => {
      expect(res.status).toBe(PurchaseRequisitionStatus.Cancelled);
    });

    const req = httpMock.expectOne(`${basePath}/pr-1/cancel`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ remarks: 'No longer required' });
    req.flush({
      succeeded: true,
      data: {
        id: 'pr-1',
        tenantId: 't1',
        companyId,
        branchId,
        requisitionNumber: 'PR-2026-001',
        requisitionDate: '2026-10-18T00:00:00Z',
        requestorUserId: 'u1',
        status: PurchaseRequisitionStatus.Cancelled,
        totalAmount: 500,
        isActive: true,
        items: []
      }
    });
  });

  it('should fetch approval history', () => {
    service.getApprovalHistory(companyId, branchId, 'pr-1').subscribe(history => {
      expect(history.length).toBe(1);
      expect(history[0].actorUserName).toBe('Manager');
    });

    const req = httpMock.expectOne(`${basePath}/pr-1/approval-history`);
    expect(req.request.method).toBe('GET');
    req.flush({
      succeeded: true,
      data: [
        {
          id: 'hist-1',
          documentType: 'PurchaseRequisition',
          documentId: 'pr-1',
          actorUserId: 'u2',
          actorUserName: 'Manager',
          decision: 2,
          decisionDateUtc: '2026-10-19T00:00:00Z',
          remarks: 'Approved'
        }
      ]
    });
  });
});
