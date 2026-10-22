import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { SalesOrderService } from './sales-order.service';
import { environment } from '../../../environments/environment';
import { SalesOrderStatus, CreateSalesOrderRequest } from '../models/sales-order.models';

describe('SalesOrderService', () => {
  let service: SalesOrderService;
  let httpMock: HttpTestingController;

  const companyId = 'c1111111-1111-1111-1111-111111111111';
  const branchId = 'b1111111-1111-1111-1111-111111111111';
  const basePath = `${environment.apiBaseUrl}/companies/${companyId}/branches/${branchId}/sales-orders`;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [SalesOrderService, provideHttpClient(), provideHttpClientTesting()]
    });

    service = TestBed.inject(SalesOrderService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('should fetch sales orders list', () => {
    service.getOrders(companyId, branchId).subscribe(orders => {
      expect(orders.length).toBe(1);
      expect(orders[0].orderNumber).toBe('SO-2026-001');
    });

    const req = httpMock.expectOne(basePath);
    expect(req.request.method).toBe('GET');
    req.flush({
      succeeded: true,
      data: [
        {
          id: 'so-1',
          tenantId: 't1',
          companyId,
          branchId,
          customerId: 'cust-1',
          orderNumber: 'SO-2026-001',
          orderDate: '2026-10-18T00:00:00Z',
          status: SalesOrderStatus.Draft,
          totalAmount: 3500,
          isActive: true,
          items: []
        }
      ]
    });
  });

  it('should fetch single sales order by id', () => {
    service.getOrder(companyId, branchId, 'so-1').subscribe(order => {
      expect(order).toBeTruthy();
      expect(order?.orderNumber).toBe('SO-2026-001');
    });

    const req = httpMock.expectOne(`${basePath}/so-1`);
    expect(req.request.method).toBe('GET');
    req.flush({
      succeeded: true,
      data: {
        id: 'so-1',
        tenantId: 't1',
        companyId,
        branchId,
        customerId: 'cust-1',
        orderNumber: 'SO-2026-001',
        orderDate: '2026-10-18T00:00:00Z',
        status: SalesOrderStatus.Draft,
        totalAmount: 3500,
        isActive: true,
        items: []
      }
    });
  });

  it('should create a sales order', () => {
    const request: CreateSalesOrderRequest = {
      orderNumber: 'SO-2026-002',
      customerId: 'cust-1',
      items: [
        {
          productId: 'p1',
          quantity: 5,
          unitPrice: 150
        }
      ]
    };

    service.createOrder(companyId, branchId, request).subscribe(order => {
      expect(order.orderNumber).toBe('SO-2026-002');
      expect(order.status).toBe(SalesOrderStatus.Draft);
    });

    const req = httpMock.expectOne(basePath);
    expect(req.request.method).toBe('POST');
    req.flush({
      succeeded: true,
      data: {
        id: 'so-2',
        tenantId: 't1',
        companyId,
        branchId,
        customerId: 'cust-1',
        orderNumber: 'SO-2026-002',
        orderDate: '2026-10-22T00:00:00Z',
        status: SalesOrderStatus.Draft,
        totalAmount: 750,
        isActive: true,
        items: []
      }
    });
  });

  it('should submit a sales order for confirmation', () => {
    service.submitOrder(companyId, branchId, 'so-1').subscribe(order => {
      expect(order.status).toBe(SalesOrderStatus.Submitted);
    });

    const req = httpMock.expectOne(`${basePath}/so-1/submit`);
    expect(req.request.method).toBe('POST');
    req.flush({
      succeeded: true,
      data: {
        id: 'so-1',
        orderNumber: 'SO-2026-001',
        status: SalesOrderStatus.Submitted
      }
    });
  });

  it('should confirm a sales order', () => {
    service.confirmOrder(companyId, branchId, 'so-1', 'Approved for dispatch').subscribe(order => {
      expect(order.status).toBe(SalesOrderStatus.Confirmed);
    });

    const req = httpMock.expectOne(`${basePath}/so-1/confirm`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ remarks: 'Approved for dispatch' });
    req.flush({
      succeeded: true,
      data: {
        id: 'so-1',
        orderNumber: 'SO-2026-001',
        status: SalesOrderStatus.Confirmed
      }
    });
  });

  it('should cancel a sales order', () => {
    service.cancelOrder(companyId, branchId, 'so-1', 'Customer cancelled').subscribe(order => {
      expect(order.status).toBe(SalesOrderStatus.Cancelled);
    });

    const req = httpMock.expectOne(`${basePath}/so-1/cancel`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ remarks: 'Customer cancelled' });
    req.flush({
      succeeded: true,
      data: {
        id: 'so-1',
        orderNumber: 'SO-2026-001',
        status: SalesOrderStatus.Cancelled
      }
    });
  });
});
