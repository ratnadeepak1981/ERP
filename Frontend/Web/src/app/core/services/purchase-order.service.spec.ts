import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { PurchaseOrderService } from './purchase-order.service';
import { environment } from '../../../environments/environment';
import { PurchaseOrderStatus, CreatePurchaseOrderRequest } from '../models/purchase-order.models';

describe('PurchaseOrderService', () => {
  let service: PurchaseOrderService;
  let httpMock: HttpTestingController;

  const companyId = 'c1111111-1111-1111-1111-111111111111';
  const branchId = 'b1111111-1111-1111-1111-111111111111';
  const basePath = `${environment.apiBaseUrl}/companies/${companyId}/branches/${branchId}/purchase-orders`;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [PurchaseOrderService, provideHttpClient(), provideHttpClientTesting()]
    });

    service = TestBed.inject(PurchaseOrderService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('should fetch purchase orders list', () => {
    service.getOrders(companyId, branchId).subscribe(orders => {
      expect(orders.length).toBe(1);
      expect(orders[0].orderNumber).toBe('PO-2026-001');
    });

    const req = httpMock.expectOne(basePath);
    expect(req.request.method).toBe('GET');
    req.flush({
      succeeded: true,
      data: [
        {
          id: 'po-1',
          tenantId: 't1',
          companyId,
          branchId,
          orderNumber: 'PO-2026-001',
          orderDate: '2026-10-18T00:00:00Z',
          status: PurchaseOrderStatus.Draft,
          totalAmount: 1250,
          isActive: true,
          items: []
        }
      ]
    });
  });

  it('should fetch single purchase order by id', () => {
    service.getOrder(companyId, branchId, 'po-1').subscribe(order => {
      expect(order).toBeTruthy();
      expect(order?.orderNumber).toBe('PO-2026-001');
    });

    const req = httpMock.expectOne(`${basePath}/po-1`);
    expect(req.request.method).toBe('GET');
    req.flush({
      succeeded: true,
      data: {
        id: 'po-1',
        tenantId: 't1',
        companyId,
        branchId,
        orderNumber: 'PO-2026-001',
        orderDate: '2026-10-18T00:00:00Z',
        status: PurchaseOrderStatus.Draft,
        totalAmount: 1250,
        isActive: true,
        items: []
      }
    });
  });

  it('should create a purchase order', () => {
    const request: CreatePurchaseOrderRequest = {
      orderNumber: 'PO-2026-002',
      items: [
        {
          productId: 'p1',
          quantity: 10,
          unitPrice: 25.5
        }
      ]
    };

    service.createOrder(companyId, branchId, request).subscribe(order => {
      expect(order.orderNumber).toBe('PO-2026-002');
      expect(order.status).toBe(PurchaseOrderStatus.Draft);
    });

    const req = httpMock.expectOne(basePath);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(request);
    req.flush({
      succeeded: true,
      data: {
        id: 'po-2',
        tenantId: 't1',
        companyId,
        branchId,
        orderNumber: 'PO-2026-002',
        orderDate: '2026-10-18T00:00:00Z',
        status: PurchaseOrderStatus.Draft,
        totalAmount: 255,
        isActive: true,
        items: []
      }
    });
  });

  it('should submit a purchase order', () => {
    service.submitOrder(companyId, branchId, 'po-1').subscribe(order => {
      expect(order.status).toBe(PurchaseOrderStatus.Submitted);
    });

    const req = httpMock.expectOne(`${basePath}/po-1/submit`);
    expect(req.request.method).toBe('POST');
    req.flush({
      succeeded: true,
      data: {
        id: 'po-1',
        tenantId: 't1',
        companyId,
        branchId,
        orderNumber: 'PO-2026-001',
        orderDate: '2026-10-18T00:00:00Z',
        status: PurchaseOrderStatus.Submitted,
        totalAmount: 1250,
        isActive: true,
        items: []
      }
    });
  });

  it('should cancel a purchase order with remarks', () => {
    service.cancelOrder(companyId, branchId, 'po-1', 'Duplicate entry').subscribe(order => {
      expect(order.status).toBe(PurchaseOrderStatus.Cancelled);
    });

    const req = httpMock.expectOne(`${basePath}/po-1/cancel`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ remarks: 'Duplicate entry' });
    req.flush({
      succeeded: true,
      data: {
        id: 'po-1',
        tenantId: 't1',
        companyId,
        branchId,
        orderNumber: 'PO-2026-001',
        orderDate: '2026-10-18T00:00:00Z',
        status: PurchaseOrderStatus.Cancelled,
        totalAmount: 1250,
        isActive: true,
        items: []
      }
    });
  });
});
