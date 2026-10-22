import { describe, it, expect, beforeEach, vi } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { of, throwError } from 'rxjs';
import { SalesShipmentRegisterComponent } from './sales-shipment-register.component';
import { SalesShipmentService } from '../../../../../core/services/sales-shipment.service';
import { MasterDataService } from '../../../../../core/services/master-data.service';
import { WorkspaceScopeService } from '../../../../../core/services/workspace-scope.service';
import { AuthService } from '../../../../../core/auth/auth.service';
import { SalesShipmentDto, SalesShipmentStatus, FulfillmentMethod } from '../../../../../core/models/sales-shipment.models';
import { CustomerItem, Company, Branch } from '../../../../../core/models/scope.models';

describe('SalesShipmentRegisterComponent', () => {
  let component: SalesShipmentRegisterComponent;
  let fixture: ComponentFixture<SalesShipmentRegisterComponent>;
  let shipmentService: SalesShipmentService;
  let masterDataService: MasterDataService;
  let scopeService: WorkspaceScopeService;
  let authService: AuthService;
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

  const mockCustomers: CustomerItem[] = [
    {
      customerId: 'cust-1',
      tenantId: 't1',
      customerCode: 'CUST-001',
      customerName: 'Acme Mega Corp',
      isActive: true
    }
  ];

  const mockShipments: SalesShipmentDto[] = [
    {
      id: 'ship-1',
      tenantId: 't1',
      companyId: mockCompany.id,
      branchId: mockBranch.id,
      salesOrderId: 'so-1',
      orderNumber: 'SO-2026-001',
      customerId: 'cust-1',
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
          warehouseId: 'wh-1',
          warehouseLocationId: 'loc-1',
          quantityDispatched: 5
        }
      ]
    },
    {
      id: 'ship-2',
      tenantId: 't1',
      companyId: mockCompany.id,
      branchId: mockBranch.id,
      salesOrderId: 'so-2',
      orderNumber: 'SO-2026-002',
      customerId: 'cust-1',
      shipmentNumber: 'DSP-2026-002',
      status: SalesShipmentStatus.Delivered,
      method: FulfillmentMethod.Delivery,
      shipmentDate: '2026-10-22',
      invoiceId: 'inv-1',
      items: []
    }
  ];

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SalesShipmentRegisterComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        SalesShipmentService,
        MasterDataService,
        WorkspaceScopeService,
        AuthService
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(SalesShipmentRegisterComponent);
    component = fixture.componentInstance;
    shipmentService = TestBed.inject(SalesShipmentService);
    masterDataService = TestBed.inject(MasterDataService);
    scopeService = TestBed.inject(WorkspaceScopeService);
    authService = TestBed.inject(AuthService);
    router = TestBed.inject(Router);

    vi.spyOn(scopeService, 'selectedCompany').mockReturnValue(mockCompany);
    vi.spyOn(scopeService, 'selectedBranch').mockReturnValue(mockBranch);
    vi.spyOn(masterDataService, 'getCustomers').mockReturnValue(of(mockCustomers));
    vi.spyOn(shipmentService, 'getShipments').mockReturnValue(of(mockShipments));
    vi.spyOn(router, 'navigate').mockResolvedValue(true);
  });

  it('should initialize and load dispatch notes with company scope', () => {
    fixture.detectChanges();
    expect(component.shipments().length).toBe(2);
    expect(component.draftCount()).toBe(1);
    expect(component.deliveredCount()).toBe(1);
  });

  it('should filter shipments by search term and status', () => {
    fixture.detectChanges();

    component.activeStatusFilter.set('draft');
    expect(component.filteredShipments().length).toBe(1);
    expect(component.filteredShipments()[0].shipmentNumber).toBe('DSP-2026-001');

    component.activeStatusFilter.set('all');
    component.searchTerm.set('DSP-2026-002');
    expect(component.filteredShipments().length).toBe(1);
    expect(component.filteredShipments()[0].orderNumber).toBe('SO-2026-002');
  });

  it('should navigate to view and edit pages correctly', () => {
    fixture.detectChanges();

    component.navigateToView(mockShipments[0]);
    expect(router.navigate).toHaveBeenCalledWith(['/app/sales/dispatches', 'ship-1']);

    component.navigateToEdit(mockShipments[0]);
    expect(router.navigate).toHaveBeenCalledWith(['/app/sales/dispatches', 'ship-1', 'edit']);
  });

  it('should handle 403 access denied gracefully', () => {
    vi.spyOn(shipmentService, 'getShipments').mockReturnValue(throwError(() => ({ status: 403 })));
    fixture.detectChanges();

    expect(component.isAccessDenied()).toBe(true);
    expect(component.shipments().length).toBe(0);
  });
});
