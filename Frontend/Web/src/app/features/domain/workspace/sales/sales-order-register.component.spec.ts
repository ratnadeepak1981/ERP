import { describe, it, expect, beforeEach, vi } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { of, throwError } from 'rxjs';
import { SalesOrderRegisterComponent } from './sales-order-register.component';
import { SalesOrderService } from '../../../../core/services/sales-order.service';
import { MasterDataService } from '../../../../core/services/master-data.service';
import { WorkspaceScopeService } from '../../../../core/services/workspace-scope.service';
import { AuthService } from '../../../../core/auth/auth.service';
import { SalesOrderDto, SalesOrderStatus } from '../../../../core/models/sales-order.models';
import { CustomerItem, Company, Branch } from '../../../../core/models/scope.models';

describe('SalesOrderRegisterComponent', () => {
  let component: SalesOrderRegisterComponent;
  let fixture: ComponentFixture<SalesOrderRegisterComponent>;
  let salesOrderService: SalesOrderService;
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
    },
    {
      customerId: 'cust-2',
      tenantId: 't1',
      customerCode: 'CUST-002',
      customerName: 'Global Logistics PLC',
      isActive: true
    }
  ];

  const mockOrders: SalesOrderDto[] = [
    {
      id: 'so-1',
      tenantId: 't1',
      companyId: mockCompany.id,
      branchId: mockBranch.id,
      customerId: 'cust-1',
      orderNumber: 'SO-2026-001',
      orderDate: '2026-10-18T00:00:00Z',
      status: SalesOrderStatus.Draft,
      subTotal: 100,
      discountAmount: 10,
      taxAmount: 13.5,
      totalAmount: 103.5,
      isActive: true,
      items: []
    },
    {
      id: 'so-2',
      tenantId: 't1',
      companyId: mockCompany.id,
      branchId: mockBranch.id,
      customerId: 'cust-2',
      orderNumber: 'SO-2026-002',
      orderDate: '2026-10-19T00:00:00Z',
      status: SalesOrderStatus.Submitted,
      subTotal: 200,
      discountAmount: 0,
      taxAmount: 30,
      totalAmount: 230,
      isActive: true,
      items: []
    },
    {
      id: 'so-3',
      tenantId: 't1',
      companyId: mockCompany.id,
      branchId: mockBranch.id,
      customerId: 'cust-1',
      orderNumber: 'SO-2026-003',
      orderDate: '2026-10-20T00:00:00Z',
      status: SalesOrderStatus.Confirmed,
      subTotal: 500,
      discountAmount: 25,
      taxAmount: 71.25,
      totalAmount: 546.25,
      isActive: true,
      items: []
    }
  ];

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SalesOrderRegisterComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        SalesOrderService,
        MasterDataService,
        WorkspaceScopeService,
        AuthService
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(SalesOrderRegisterComponent);
    component = fixture.componentInstance;
    salesOrderService = TestBed.inject(SalesOrderService);
    masterDataService = TestBed.inject(MasterDataService);
    scopeService = TestBed.inject(WorkspaceScopeService);
    authService = TestBed.inject(AuthService);
    router = TestBed.inject(Router);

    vi.spyOn(authService, 'isTenantAdmin').mockReturnValue(true);
    vi.spyOn(router, 'navigate').mockResolvedValue(true);
  });

  it('should render empty state when scope is not selected', () => {
    vi.spyOn(scopeService, 'selectedCompany').mockReturnValue(null);
    vi.spyOn(scopeService, 'selectedBranch').mockReturnValue(null);
    vi.spyOn(scopeService, 'canOperateTransactions').mockReturnValue(false);

    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('app-empty-state')).toBeTruthy();
  });

  it('should load orders and enrich customer names from master data', () => {
    vi.spyOn(scopeService, 'selectedCompany').mockReturnValue(mockCompany);
    vi.spyOn(scopeService, 'selectedBranch').mockReturnValue(mockBranch);
    vi.spyOn(scopeService, 'canOperateTransactions').mockReturnValue(true);
    vi.spyOn(salesOrderService, 'getOrders').mockReturnValue(of(mockOrders));
    vi.spyOn(masterDataService, 'getCustomers').mockReturnValue(of(mockCustomers));

    fixture.detectChanges();

    expect(component.orders().length).toBe(3);
    const order1 = component.orders().find(o => o.id === 'so-1');
    expect(order1?.customerName).toBe('Acme Mega Corp');
    const order2 = component.orders().find(o => o.id === 'so-2');
    expect(order2?.customerName).toBe('Global Logistics PLC');
  });

  it('should filter orders by status pill and search term', () => {
    vi.spyOn(scopeService, 'selectedCompany').mockReturnValue(mockCompany);
    vi.spyOn(scopeService, 'selectedBranch').mockReturnValue(mockBranch);
    vi.spyOn(scopeService, 'canOperateTransactions').mockReturnValue(true);
    vi.spyOn(salesOrderService, 'getOrders').mockReturnValue(of(mockOrders));
    vi.spyOn(masterDataService, 'getCustomers').mockReturnValue(of(mockCustomers));

    fixture.detectChanges();

    // Filter by draft
    component.activeStatusFilter.set('draft');
    expect(component.filteredOrders().length).toBe(1);
    expect(component.filteredOrders()[0].orderNumber).toBe('SO-2026-001');

    // Filter by submitted
    component.activeStatusFilter.set('submitted');
    expect(component.filteredOrders().length).toBe(1);
    expect(component.filteredOrders()[0].orderNumber).toBe('SO-2026-002');

    // Filter by search term
    component.activeStatusFilter.set('all');
    component.searchTerm.set('Global');
    expect(component.filteredOrders().length).toBe(1);
    expect(component.filteredOrders()[0].orderNumber).toBe('SO-2026-002');
  });

  it('should navigate to new sales order page on navigateToCreate', () => {
    component.navigateToCreate();
    expect(router.navigate).toHaveBeenCalledWith(['/app/sales/orders/new']);
  });

  it('should navigate to transaction view page on navigateToView', () => {
    component.navigateToView(mockOrders[0]);
    expect(router.navigate).toHaveBeenCalledWith(['/app/sales/orders', 'so-1']);
  });

  it('should navigate to dispatch inventory page on navigateToDispatch for confirmed order', () => {
    component.navigateToDispatch(mockOrders[2]);
    expect(router.navigate).toHaveBeenCalledWith(['/app/inventory/issue-stock/new'], {
      queryParams: {
        purpose: 'sales',
        referenceNumber: 'SO-2026-003',
        salesOrderId: 'so-3'
      }
    });
  });

  it('should execute submit order transition on onSubmitOrder', () => {
    vi.spyOn(scopeService, 'selectedCompany').mockReturnValue(mockCompany);
    vi.spyOn(scopeService, 'selectedBranch').mockReturnValue(mockBranch);
    vi.spyOn(salesOrderService, 'submitOrder').mockReturnValue(of({ ...mockOrders[0], status: SalesOrderStatus.Submitted }));
    const loadSpy = vi.spyOn(component, 'loadOrders');

    component.onSubmitOrder(mockOrders[0]);

    expect(salesOrderService.submitOrder).toHaveBeenCalledWith(mockCompany.id, mockBranch.id, 'so-1');
    expect(loadSpy).toHaveBeenCalled();
  });

  it('should handle confirm with remarks dialog', () => {
    vi.spyOn(scopeService, 'selectedCompany').mockReturnValue(mockCompany);
    vi.spyOn(scopeService, 'selectedBranch').mockReturnValue(mockBranch);
    vi.spyOn(salesOrderService, 'confirmOrder').mockReturnValue(of({ ...mockOrders[1], status: SalesOrderStatus.Confirmed }));

    component.openRemarksDialog('confirm', mockOrders[1]);
    expect(component.remarksDialogVisible()).toBe(true);

    component.remarksText.set('Approved by Sales VP');
    component.submitRemarksAction();

    expect(salesOrderService.confirmOrder).toHaveBeenCalledWith(mockCompany.id, mockBranch.id, 'so-2', 'Approved by Sales VP');
    expect(component.remarksDialogVisible()).toBe(false);
  });

  it('should require remarks when rejecting an order', () => {
    vi.spyOn(scopeService, 'selectedCompany').mockReturnValue(mockCompany);
    vi.spyOn(scopeService, 'selectedBranch').mockReturnValue(mockBranch);
    const rejectSpy = vi.spyOn(salesOrderService, 'rejectOrder');

    component.openRemarksDialog('reject', mockOrders[1]);
    component.remarksText.set('');
    component.submitRemarksAction();

    expect(rejectSpy).not.toHaveBeenCalled();
    expect(component.remarksError()).toBe('Rejection remarks are required.');
  });

  it('should handle 403 Forbidden with AccessDeniedComponent', () => {
    vi.spyOn(scopeService, 'selectedCompany').mockReturnValue(mockCompany);
    vi.spyOn(scopeService, 'selectedBranch').mockReturnValue(mockBranch);
    vi.spyOn(scopeService, 'canOperateTransactions').mockReturnValue(true);
    vi.spyOn(salesOrderService, 'getOrders').mockReturnValue(
      throwError(() => ({ status: 403, statusText: 'Forbidden' }))
    );

    fixture.detectChanges();

    expect(component.isAccessDenied()).toBe(true);
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('app-access-denied')).toBeTruthy();
  });
});
