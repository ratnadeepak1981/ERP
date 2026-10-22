import { describe, it, expect, beforeEach, vi } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router, ActivatedRoute } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { of } from 'rxjs';
import { SalesShipmentPageComponent } from './sales-shipment-page.component';
import { SalesShipmentService } from '../../../../../core/services/sales-shipment.service';
import { SalesOrderService } from '../../../../../core/services/sales-order.service';
import { MasterDataService } from '../../../../../core/services/master-data.service';
import { WorkspaceScopeService } from '../../../../../core/services/workspace-scope.service';
import { AuthService } from '../../../../../core/auth/auth.service';
import { SalesShipmentDto, SalesShipmentStatus, FulfillmentMethod } from '../../../../../core/models/sales-shipment.models';
import { SalesOrderDto, SalesOrderStatus } from '../../../../../core/models/sales-order.models';
import { Company, Branch } from '../../../../../core/models/scope.models';

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
    status: SalesShipmentStatus.Draft,
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
              queryParamMap: { get: (key: string) => key === 'orderId' ? 'so-1' : null }
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
    vi.spyOn(shipmentService, 'createFromOrder').mockReturnValue(of(mockShipment));
    vi.spyOn(router, 'navigate').mockResolvedValue(true);
  });

  it('should initialize draft from order query parameter with prefilled items', () => {
    fixture.detectChanges();

    expect(component.mode()).toBe('create');
    expect(component.sourceOrderId()).toBe('so-1');
    expect(component.workingLines().length).toBe(1);
    expect(component.workingLines()[0].productId).toBe('prod-1');
    expect(component.workingLines()[0].orderedQuantity).toBe(5);
    expect(component.isDraft()).toBe(true);
  });

  it('should save draft without stock deduction and navigate to dispatch view', () => {
    fixture.detectChanges();

    component.saveDraft();
    expect(shipmentService.createFromOrder).toHaveBeenCalled();
    expect(router.navigate).toHaveBeenCalledWith(['/app/sales/dispatches', 'ship-1']);
  });
});
