import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { InventoryService } from './inventory.service';
import {
  InventorySummaryDto,
  InventoryRecentTransactionDto,
  StockIssueDto,
  StockIssuePurpose,
  StockIssueStatus,
  AvailableStockDto,
  CreateStockIssueRequest
} from '../models/inventory.models';

describe('InventoryService', () => {
  let service: InventoryService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        InventoryService,
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    });
    service = TestBed.inject(InventoryService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('should fetch inventory summary from real API endpoint', () => {
    const mockSummary: InventorySummaryDto = {
      totalStockItems: 42,
      totalWarehouses: 3,
      totalLocations: 12,
      lowStockItems: 5,
      totalTransactions: 156,
      totalOnHandQuantity: 1250,
      totalInventoryValue: 75400.5
    };

    service.getSummary().subscribe(summary => {
      expect(summary).toEqual(mockSummary);
      expect(summary.totalStockItems).toBe(42);
      expect(summary.lowStockItems).toBe(5);
    });

    const req = httpMock.expectOne(r => r.url.includes('/inventory/summary'));
    expect(req.request.method).toBe('GET');
    req.flush({ success: true, data: mockSummary });
  });

  it('should fetch recent transactions with limit parameter', () => {
    const mockTransactions: InventoryRecentTransactionDto[] = [
      {
        inventoryTransactionId: 'tx-1',
        transactionNumber: 'TX-1001',
        movementType: 'GoodsReceipt',
        movementTypeCode: 1,
        sourceDocumentType: 'GoodsReceiptNote',
        sourceDocumentId: 'grn-1',
        productId: 'prod-1',
        productCode: 'RAW-001',
        productName: 'Steel Rod 10mm',
        unitOfMeasure: 'KG',
        warehouseId: 'wh-1',
        warehouseCode: 'MAIN',
        warehouseName: 'Main Warehouse',
        warehouseLocationId: 'loc-1',
        locationCode: 'A-01',
        locationName: 'Bin A1',
        quantity: 100,
        unitCost: 15.5,
        totalCost: 1550,
        batchNumber: 'B-2026-01',
        serialNumber: null,
        transactionDate: '2026-10-18T10:00:00Z',
        createdAt: '2026-10-18T10:00:00Z'
      }
    ];

    service.getRecentTransactions(5).subscribe(res => {
      expect(res.length).toBe(1);
      expect(res[0].transactionNumber).toBe('TX-1001');
      expect(res[0].movementType).toBe('GoodsReceipt');
    });

    const req = httpMock.expectOne(r => r.url.includes('/inventory/recent-transactions') && r.params.get('limit') === '5');
    expect(req.request.method).toBe('GET');
    req.flush({ success: true, data: mockTransactions });
  });

  it('should fetch stock issues with filters', () => {
    const mockIssues: StockIssueDto[] = [
      {
        stockIssueId: 'issue-1',
        issueNumber: 'ISS-2026-0001',
        issueDate: '2026-10-19T10:00:00Z',
        purpose: StockIssuePurpose.InternalUse,
        purposeName: 'Internal Use / Maintenance',
        movementType: 'StockAdjustmentOut',
        status: StockIssueStatus.Posted,
        statusName: 'Posted',
        warehouseId: 'wh-1',
        warehouseCode: 'MAIN',
        warehouseName: 'Main Warehouse',
        warehouseLocationId: 'loc-1',
        locationCode: 'A1-01',
        locationName: 'Bin A1',
        productId: 'prod-1',
        productCode: 'RAW-001',
        productName: 'Steel Rod 10mm',
        unitOfMeasure: 'KG',
        quantity: 20,
        unitCost: 15.5,
        totalCost: 310,
        batchNumber: null,
        serialNumber: null,
        sourceDocumentReference: 'MAINT-99',
        remarks: 'Workshop repair',
        createdAt: '2026-10-19T10:00:00Z'
      }
    ];

    service.getStockIssues({ purpose: StockIssuePurpose.InternalUse }).subscribe(res => {
      expect(res.length).toBe(1);
      expect(res[0].issueNumber).toBe('ISS-2026-0001');
      expect(res[0].quantity).toBe(20);
    });

    const req = httpMock.expectOne(r => r.url.includes('/inventory/stock-issues') && r.params.get('purpose') === '1');
    expect(req.request.method).toBe('GET');
    req.flush({ success: true, data: mockIssues });
  });

  it('should fetch available stock for a product and location', () => {
    const mockStock: AvailableStockDto = {
      productId: 'prod-1',
      warehouseLocationId: 'loc-1',
      quantityOnHand: 100,
      quantityAllocated: 10,
      quantityQuarantine: 5,
      quantityAvailable: 85
    };

    service.getAvailableStock('prod-1', 'loc-1').subscribe(res => {
      expect(res.quantityAvailable).toBe(85);
      expect(res.quantityOnHand).toBe(100);
    });

    const req = httpMock.expectOne(r => r.url.includes('/inventory/available-stock') && r.params.get('productId') === 'prod-1');
    expect(req.request.method).toBe('GET');
    req.flush({ success: true, data: mockStock });
  });

  it('should post create stock issue request', () => {
    const request: CreateStockIssueRequest = {
      purpose: StockIssuePurpose.InternalUse,
      warehouseId: 'wh-1',
      sourceDocumentReference: 'REQ-123',
      remarks: 'Internal workshop consumption',
      lines: [
        {
          productId: 'prod-1',
          warehouseLocationId: 'loc-1',
          quantity: 5
        }
      ]
    };

    const mockResponse: StockIssueDto[] = [
      {
        stockIssueId: 'tx-new',
        issueNumber: 'ISS-2026-0002',
        issueDate: '2026-10-19T11:00:00Z',
        purpose: StockIssuePurpose.InternalUse,
        purposeName: 'Internal Use / Maintenance',
        movementType: 'StockAdjustmentOut',
        status: StockIssueStatus.Posted,
        statusName: 'Posted',
        warehouseId: 'wh-1',
        warehouseCode: 'MAIN',
        warehouseName: 'Main Warehouse',
        warehouseLocationId: 'loc-1',
        locationCode: 'A1-01',
        locationName: 'Bin A1',
        productId: 'prod-1',
        productCode: 'RAW-001',
        productName: 'Steel Rod 10mm',
        unitOfMeasure: 'KG',
        quantity: 5,
        unitCost: 15.5,
        totalCost: 77.5,
        createdAt: '2026-10-19T11:00:00Z'
      }
    ];

    service.createStockIssue(request).subscribe(res => {
      expect(res.length).toBe(1);
      expect(res[0].issueNumber).toBe('ISS-2026-0002');
    });

    const req = httpMock.expectOne(r => r.url.includes('/inventory/stock-issues'));
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(request);
    req.flush({ success: true, data: mockResponse });
  });
});
