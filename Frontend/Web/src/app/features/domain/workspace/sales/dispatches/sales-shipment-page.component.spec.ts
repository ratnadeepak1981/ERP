import { describe, it, expect, beforeEach, vi } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router, ActivatedRoute } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { of } from 'rxjs';
import { MatDialog } from '@angular/material/dialog';
import { SalesShipmentPageComponent, DispatchLineWithTracking } from './sales-shipment-page.component';
import { SalesShipmentService } from '../../../../../core/services/sales-shipment.service';
import { SalesOrderService } from '../../../../../core/services/sales-order.service';
import { MasterDataService } from '../../../../../core/services/master-data.service';
import { WorkspaceScopeService } from '../../../../../core/services/workspace-scope.service';
import { AuthService } from '../../../../../core/auth/auth.service';
import {
  SalesShipmentDto,
  SalesShipmentStatus,
  FulfillmentMethod,
  SalesOrderDispatchAvailabilityDto
} from '../../../../../core/models/sales-shipment.models';
import { SalesOrderDto, SalesOrderStatus } from '../../../../../core/models/sales-order.models';
import { Company, Branch, TrackingMode } from '../../../../../core/models/scope.models';
import { BatchSerialAllocation } from '../../../../../core/models/goods-receipt-note.models';
import { BatchSerialTrackingDialogComponent } from '../../procurement/batch-serial-dialog/batch-serial-dialog.component';

describe('SalesShipmentPageComponent', () => {
  let component: SalesShipmentPageComponent;
  let fixture: ComponentFixture<SalesShipmentPageComponent>;
  let shipmentService: SalesShipmentService;
  let salesOrderService: SalesOrderService;
  let scopeService: WorkspaceScopeService;
  let router: Router;

  const mockCompany: Company = {
    id: 'c1111111-1111-1111-1111-111111111111',
    tenantId: 't1',
    name: 'Alpha Industries',
    code: 'ALPHA',
    isActive: true
  };

  const mockBranch: Branch = {
    id: 'b1111111-1111-1111-1111-111111111111',
    companyId: mockCompany.id,
    name: 'Colombo Branch',
    code: 'CMB',
    isActive: true
  };

  const mockOrder: SalesOrderDto = {
    id: 'so-1',
    tenantId: 't1',
    companyId: mockCompany.id,
    branchId: mockBranch.id,
    customerId: 'cust-1',
    customerName: 'Acme Mega Corp',
    orderNumber: 'SO-2026-001',
    orderDate: '2026-10-18T00:00:00Z',
    status: SalesOrderStatus.Confirmed,
    subTotal: 100,
    discountAmount: 10,
    taxAmount: 13.5,
    totalAmount: 103.5,
    isActive: true,
    items: [
      {
        id: 'so-item-1',
        salesOrderId: 'so-1',
        productId: 'prod-1',
        productCode: 'ITEM-001',
        productName: 'Solar Panel 400W',
        quantity: 5,
        unitPrice: 20,
        subTotal: 100,
        lineTotal: 100
      }
    ]
  };

  const mockAvailabilitySufficient: SalesOrderDispatchAvailabilityDto = {
    salesOrderId: 'so-1',
    orderNumber: 'SO-2026-001',
    defaultWarehouseId: 'wh-1',
    defaultWarehouseName: 'Main Warehouse',
    defaultLocationId: 'loc-1',
    defaultLocationName: 'Default Bin A1',
    canDefaultLocationFulfil: true,
    canFulfil: true,
    hasShortages: false,
    eligibleLocations: [
      {
        warehouseId: 'wh-1',
        warehouseCode: 'MAIN',
        warehouseName: 'Main Warehouse',
        locationId: 'loc-1',
        locationCode: 'A1-01',
        locationName: 'Default Bin A1',
        isDefault: true
      }
    ],
    lines: [
      {
        salesOrderItemId: 'so-item-1',
        productId: 'prod-1',
        productCode: 'ITEM-001',
        productName: 'Solar Panel 400W',
        orderedQuantity: 5,
        alreadyDispatchedQuantity: 0,
        remainingQuantity: 5,
        availableQuantity: 10,
        shortageQuantity: 0,
        canFulfil: true,
        unitOfMeasure: 'PCS',
        trackingMode: 0
      }
    ]
  };

  const mockAvailabilityShortage: SalesOrderDispatchAvailabilityDto = {
    salesOrderId: 'so-1',
    orderNumber: 'SO-2026-001',
    defaultWarehouseId: 'wh-1',
    defaultWarehouseName: 'Main Warehouse',
    defaultLocationId: 'loc-1',
    defaultLocationName: 'Default Bin A1',
    canDefaultLocationFulfil: false,
    canFulfil: false,
    hasShortages: true,
    eligibleLocations: [],
    lines: [
      {
        salesOrderItemId: 'so-item-1',
        productId: 'prod-1',
        productCode: 'ITEM-001',
        productName: 'Solar Panel 400W',
        orderedQuantity: 5,
        alreadyDispatchedQuantity: 0,
        remainingQuantity: 5,
        availableQuantity: 2,
        shortageQuantity: 3,
        canFulfil: false,
        unitOfMeasure: 'PCS',
        trackingMode: 0
      }
    ]
  };

  const mockShipment: SalesShipmentDto = {
    id: 'ship-1',
    tenantId: 't1',
    companyId: mockCompany.id,
    branchId: mockBranch.id,
    salesOrderId: 'so-1',
    orderNumber: 'SO-2026-001',
    customerId: 'cust-1',
    customerName: 'Acme Mega Corp',
    shipmentNumber: 'DSP-2026-001',
    status: SalesShipmentStatus.Dispatched,
    method: FulfillmentMethod.Delivery,
    shipmentDate: '2026-10-22',
    items: [
      {
        id: 'item-1',
        salesShipmentId: 'ship-1',
        salesOrderItemId: 'so-item-1',
        productId: 'prod-1',
        productCode: 'ITEM-001',
        productName: 'Solar Panel 400W',
        warehouseId: 'wh-1',
        warehouseLocationId: 'loc-1',
        quantityDispatched: 5
      }
    ]
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SalesShipmentPageComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        SalesShipmentService,
        SalesOrderService,
        MasterDataService,
        WorkspaceScopeService,
        AuthService,
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: { get: () => null },
              queryParamMap: { get: (key: string) => (key === 'orderId' ? 'so-1' : null) }
            }
          }
        }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(SalesShipmentPageComponent);
    component = fixture.componentInstance;
    shipmentService = TestBed.inject(SalesShipmentService);
    salesOrderService = TestBed.inject(SalesOrderService);
    scopeService = TestBed.inject(WorkspaceScopeService);
    router = TestBed.inject(Router);

    vi.spyOn(scopeService, 'selectedCompany').mockReturnValue(mockCompany);
    vi.spyOn(scopeService, 'selectedBranch').mockReturnValue(mockBranch);
    vi.spyOn(salesOrderService, 'getOrder').mockReturnValue(of(mockOrder));
    vi.spyOn(shipmentService, 'getOrderDispatchAvailability').mockReturnValue(of(mockAvailabilitySufficient));
    vi.spyOn(shipmentService, 'dispatchFromOrder').mockReturnValue(of(mockShipment));
    vi.spyOn(router, 'navigate').mockResolvedValue(true);
  });

  it('should initialize dispatch from order with default location and enable dispatch when sufficient', () => {
    fixture.detectChanges();

    expect(component.mode()).toBe('create');
    expect(component.sourceOrderId()).toBe('so-1');
    expect(component.availability()).toBeDefined();
    expect(component.selectedLocationId()).toBe('loc-1');
    expect(component.canDispatch()).toBe(true);
  });

  it('should disable dispatch and show shortage lines when no single location can fulfil', () => {
    vi.spyOn(shipmentService, 'getOrderDispatchAvailability').mockReturnValue(of(mockAvailabilityShortage));

    fixture.detectChanges();

    expect(component.canDispatch()).toBe(false);
    expect(component.displayedLines().length).toBe(1);
    expect(component.displayedLines()[0].shortageQuantity).toBe(3);
  });

  it('should execute direct dispatch and navigate to dispatch view on success', () => {
    fixture.detectChanges();

    component.executeDirectDispatch();

    expect(shipmentService.dispatchFromOrder).toHaveBeenCalledWith(
      mockCompany.id,
      mockBranch.id,
      'so-1',
      expect.objectContaining({
        warehouseLocationId: 'loc-1'
      })
    );
    expect(router.navigate).toHaveBeenCalledWith(['/app/sales/dispatches', 'ship-1']);
  });

  it('should display tracking summary correctly for untracked, pending, and configured lines', () => {
    fixture.detectChanges();

    const untrackedLine: DispatchLineWithTracking = {
      ...mockAvailabilitySufficient.lines[0],
      trackingMode: TrackingMode.None,
      remainingQuantity: 5,
      trackingAllocations: []
    };
    expect(component.getLineTrackingSummary(untrackedLine)).toBe('Not Tracked');

    const pendingBatchLine: DispatchLineWithTracking = {
      ...mockAvailabilitySufficient.lines[0],
      trackingMode: TrackingMode.Batch,
      remainingQuantity: 5,
      trackingAllocations: []
    };
    expect(component.getLineTrackingSummary(pendingBatchLine)).toBe('Configure (0/5)');

    const partialBatchLine: DispatchLineWithTracking = {
      ...mockAvailabilitySufficient.lines[0],
      trackingMode: TrackingMode.Batch,
      remainingQuantity: 5,
      trackingAllocations: [
        { batchNumber: 'LOT-01', quantity: 3 }
      ]
    };
    expect(component.getLineTrackingSummary(partialBatchLine)).toBe('Configure (3/5)');

    const configuredBatchLine: DispatchLineWithTracking = {
      ...mockAvailabilitySufficient.lines[0],
      trackingMode: TrackingMode.Batch,
      remainingQuantity: 5,
      trackingAllocations: [
        { batchNumber: 'LOT-01', quantity: 3 },
        { batchNumber: 'LOT-02', quantity: 2 }
      ]
    };
    expect(component.getLineTrackingSummary(configuredBatchLine)).toBe('Batches (2) ✓');

    const configuredSerialLine: DispatchLineWithTracking = {
      ...mockAvailabilitySufficient.lines[0],
      trackingMode: TrackingMode.Serial,
      remainingQuantity: 2,
      trackingAllocations: [
        { serialNumber: 'SN-001', quantity: 1 },
        { serialNumber: 'SN-002', quantity: 1 }
      ]
    };
    expect(component.getLineTrackingSummary(configuredSerialLine)).toBe('Serials (2) ✓');
  });

  it('should open BatchSerialTrackingDialogComponent in issue mode with correct product and location context', () => {
    fixture.detectChanges();

    const matDialog = TestBed.inject(MatDialog);
    const mockAllocations: BatchSerialAllocation[] = [
      { batchNumber: 'LOT-FEFO-01', quantity: 5 }
    ];
    const openSpy = vi.spyOn(matDialog, 'open').mockReturnValue({
      afterClosed: () => of(mockAllocations)
    } as any);

    const trackedLine: DispatchLineWithTracking = {
      ...mockAvailabilitySufficient.lines[0],
      trackingMode: TrackingMode.Batch,
      remainingQuantity: 5,
      trackingAllocations: []
    };

    component.openTrackingDialog(trackedLine);

    expect(openSpy).toHaveBeenCalledWith(
      BatchSerialTrackingDialogComponent,
      expect.objectContaining({
        width: '920px',
        disableClose: true,
        data: expect.objectContaining({
          dialogMode: 'issue',
          productId: 'prod-1',
          productCode: 'ITEM-001',
          productName: 'Solar Panel 400W',
          unitOfMeasure: 'PCS',
          trackingMode: TrackingMode.Batch,
          targetQuantity: 5,
          warehouseId: 'wh-1',
          warehouseLocationId: 'loc-1',
          readOnly: false,
          allocations: []
        })
      })
    );

    expect(trackedLine.trackingAllocations).toEqual(mockAllocations);
  });

  it('should enforce tracking completeness in canDispatch for batch and serial tracked lines', () => {
    const batchLine: DispatchLineWithTracking = {
      ...mockAvailabilitySufficient.lines[0],
      trackingMode: TrackingMode.Batch,
      remainingQuantity: 5,
      trackingAllocations: []
    };

    const availabilityWithBatch: SalesOrderDispatchAvailabilityDto = {
      ...mockAvailabilitySufficient,
      lines: [batchLine]
    };

    vi.spyOn(shipmentService, 'getOrderDispatchAvailability').mockReturnValue(of(availabilityWithBatch));
    fixture.detectChanges();

    // 0/5 allocations -> cannot dispatch
    expect(component.canDispatch()).toBe(false);

    // Partial 3/5 allocations -> cannot dispatch
    batchLine.trackingAllocations = [{ batchNumber: 'LOT-1', quantity: 3 }];
    component.availability.set({
      ...availabilityWithBatch,
      lines: [batchLine]
    });
    expect(component.canDispatch()).toBe(false);

    // Full 5/5 allocations -> can dispatch
    batchLine.trackingAllocations = [
      { batchNumber: 'LOT-1', quantity: 3 },
      { batchNumber: 'LOT-2', quantity: 2 }
    ];
    component.availability.set({
      ...availabilityWithBatch,
      lines: [batchLine]
    });
    expect(component.canDispatch()).toBe(true);

    // Serial tracking completeness check
    const serialLine: DispatchLineWithTracking = {
      ...mockAvailabilitySufficient.lines[0],
      trackingMode: TrackingMode.Serial,
      remainingQuantity: 2,
      trackingAllocations: [{ serialNumber: 'SN-01', quantity: 1 }]
    };
    component.availability.set({
      ...mockAvailabilitySufficient,
      lines: [serialLine]
    });
    // 1/2 serials -> cannot dispatch
    expect(component.canDispatch()).toBe(false);

    // 2/2 serials -> can dispatch
    serialLine.trackingAllocations = [
      { serialNumber: 'SN-01', quantity: 1 },
      { serialNumber: 'SN-02', quantity: 1 }
    ];
    component.availability.set({
      ...mockAvailabilitySufficient,
      lines: [serialLine]
    });
    expect(component.canDispatch()).toBe(true);
  });

  it('should expand explicit multi-batch allocations into items array during direct dispatch', () => {
    const batchLine: DispatchLineWithTracking = {
      ...mockAvailabilitySufficient.lines[0],
      trackingMode: TrackingMode.Batch,
      remainingQuantity: 5,
      trackingAllocations: [
        { batchNumber: 'LOT-A', quantity: 3 },
        { batchNumber: 'LOT-B', quantity: 2 }
      ]
    };

    const availabilityWithBatch: SalesOrderDispatchAvailabilityDto = {
      ...mockAvailabilitySufficient,
      lines: [batchLine]
    };

    vi.spyOn(shipmentService, 'getOrderDispatchAvailability').mockReturnValue(of(availabilityWithBatch));
    fixture.detectChanges();

    component.executeDirectDispatch();

    expect(shipmentService.dispatchFromOrder).toHaveBeenCalledWith(
      mockCompany.id,
      mockBranch.id,
      'so-1',
      expect.objectContaining({
        warehouseLocationId: 'loc-1',
        items: [
          expect.objectContaining({
            salesOrderItemId: 'so-item-1',
            productId: 'prod-1',
            warehouseId: 'wh-1',
            warehouseLocationId: 'loc-1',
            quantityDispatched: 3,
            batchNumber: 'LOT-A'
          }),
          expect.objectContaining({
            salesOrderItemId: 'so-item-1',
            productId: 'prod-1',
            warehouseId: 'wh-1',
            warehouseLocationId: 'loc-1',
            quantityDispatched: 2,
            batchNumber: 'LOT-B'
          })
        ]
      })
    );
  });

  it('should omit items array when products are untracked to allow backend fallback auto-allocation', () => {
    const untrackedLine: DispatchLineWithTracking = {
      ...mockAvailabilitySufficient.lines[0],
      trackingMode: TrackingMode.None,
      remainingQuantity: 5,
      trackingAllocations: []
    };

    const availabilityUntracked: SalesOrderDispatchAvailabilityDto = {
      ...mockAvailabilitySufficient,
      lines: [untrackedLine]
    };

    vi.spyOn(shipmentService, 'getOrderDispatchAvailability').mockReturnValue(of(availabilityUntracked));
    fixture.detectChanges();

    component.executeDirectDispatch();

    expect(shipmentService.dispatchFromOrder).toHaveBeenCalledWith(
      mockCompany.id,
      mockBranch.id,
      'so-1',
      expect.objectContaining({
        warehouseLocationId: 'loc-1',
        items: undefined
      })
    );
  });
});
