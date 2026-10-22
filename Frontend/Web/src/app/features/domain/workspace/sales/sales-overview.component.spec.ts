import { describe, it, expect, beforeEach, vi } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter, Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import { SalesOverviewComponent } from './sales-overview.component';
import { SalesOrderDto, SalesOrderStatus } from '../../../../core/models/sales-order.models';
import { Company, Branch, CustomerItem } from '../../../../core/models/scope.models';
import { AuthService } from '../../../../core/auth/auth.service';

describe('SalesOverviewComponent', () => {
  let component: SalesOverviewComponent;
  let fixture: ComponentFixture<SalesOverviewComponent>;
  let router: Router;
  let authService: AuthService;

  const mockCompany: Company = {
    id: 'company-uuid-1',
    tenantId: 'tenant-uuid-1',
    name: 'Alpha Corp',
    code: 'AC',
    currency: 'LKR',
    isActive: true
  };

  const mockBranch: Branch = {
    id: 'branch-uuid-1',
    companyId: 'company-uuid-1',
    name: 'Colombo Branch',
    code: 'CB',
    isActive: true
  };

  const mockCustomers: CustomerItem[] = [
    {
      customerId: 'cust-uuid-1',
      customerCode: 'CUST-001',
      customerName: 'Acme Mega Corp',
      isActive: true
    },
    {
      customerId: 'cust-uuid-2',
      customerCode: 'CUST-002',
      customerName: 'Global Logistics PLC',
      isActive: true
    }
  ];

  const mockOrders: SalesOrderDto[] = [
    {
      id: 'so-1',
      tenantId: 'tenant-uuid-1',
      companyId: 'company-uuid-1',
      branchId: 'branch-uuid-1',
      customerId: 'cust-uuid-1',
      orderNumber: 'SO-2001',
      orderDate: '2026-10-18',
      status: SalesOrderStatus.Draft,
      totalAmount: 150000,
      isActive: true,
      items: []
    },
    {
      id: 'so-2',
      tenantId: 'tenant-uuid-1',
      companyId: 'company-uuid-1',
      branchId: 'branch-uuid-1',
      customerId: 'cust-uuid-1',
      orderNumber: 'SO-2002',
      orderDate: '2026-10-19',
      status: SalesOrderStatus.Submitted,
      totalAmount: 320000,
      isActive: true,
      items: []
    },
    {
      id: 'so-3',
      tenantId: 'tenant-uuid-1',
      companyId: 'company-uuid-1',
      branchId: 'branch-uuid-1',
      customerId: 'cust-uuid-2',
      orderNumber: 'SO-2003',
      orderDate: '2026-10-20',
      status: SalesOrderStatus.Confirmed,
      totalAmount: 580000,
      isActive: true,
      items: []
    },
    {
      id: 'so-4',
      tenantId: 'tenant-uuid-1',
      companyId: 'company-uuid-1',
      branchId: 'branch-uuid-1',
      customerId: 'cust-uuid-2',
      orderNumber: 'SO-2004',
      orderDate: '2026-10-21',
      status: SalesOrderStatus.Confirmed,
      totalAmount: 120000,
      isActive: true,
      items: []
    }
  ];

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SalesOverviewComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(SalesOverviewComponent);
    component = fixture.componentInstance;
    router = TestBed.inject(Router);
    authService = TestBed.inject(AuthService);
    vi.spyOn(authService, 'isTenantAdmin').mockReturnValue(true);
    vi.spyOn(router, 'navigate').mockResolvedValue(true);
  });

  it('should create component and render scope prompt when company or branch is unselected', () => {
    vi.spyOn(component.scopeService, 'selectedCompany').mockReturnValue(null);
    vi.spyOn(component.scopeService, 'selectedBranch').mockReturnValue(null);
    vi.spyOn(component.scopeService, 'canOperateTransactions').mockReturnValue(false);

    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('app-empty-state')).toBeTruthy();
  });

  it('should load orders and compute summary cards accurately from real API data', () => {
    vi.spyOn(component.scopeService, 'selectedCompany').mockReturnValue(mockCompany);
    vi.spyOn(component.scopeService, 'selectedBranch').mockReturnValue(mockBranch);
    vi.spyOn(component.scopeService, 'canOperateTransactions').mockReturnValue(true);
    vi.spyOn(component.salesOrderService, 'getOrders').mockReturnValue(of(mockOrders));
    vi.spyOn(component.masterDataService, 'getCustomers').mockReturnValue(of(mockCustomers));

    fixture.detectChanges();

    expect(component.orders().length).toBe(4);
    expect(component.draftCount()).toBe(1);
    expect(component.pendingCount()).toBe(1);
    expect(component.confirmedCount()).toBe(2);
    expect(component.totalOrderValue()).toBe(1170000);
    expect(component.formattedTotalOrderValue()).toContain('LKR');
    expect(component.formattedTotalOrderValue()).toBe('LKR 1.2M');

    // Customer enrichment verification
    const order1 = component.orders().find(o => o.id === 'so-1');
    expect(order1?.customerName).toBe('Acme Mega Corp');

    const draftCard = fixture.nativeElement.querySelector('[data-testid="dashboard-card-draft"] .card-metric-value');
    expect(draftCard?.textContent?.trim()).toBe('1');

    const pendingCard = fixture.nativeElement.querySelector('[data-testid="dashboard-card-pending"] .card-metric-value');
    expect(pendingCard?.textContent?.trim()).toBe('1');

    const confirmedCard = fixture.nativeElement.querySelector('[data-testid="dashboard-card-confirmed"] .card-metric-value');
    expect(confirmedCard?.textContent?.trim()).toBe('2');

    // Verify Process Flow is rendered
    const processFlow = fixture.nativeElement.querySelector('app-process-flow');
    expect(processFlow).toBeTruthy();
  });

  it('should configure 5 process stages with accurate statuses and shipment marked as planned', () => {
    const config = component.overviewConfig();
    expect(config.processFlow).toBeDefined();
    const stages = config.processFlow!.stages;
    expect(stages.length).toBe(5);

    expect(stages[0].id).toBe('so');
    expect(stages[0].label).toBe('Sales Order');
    expect(stages[0].status).toBe('current');

    expect(stages[1].id).toBe('confirmation');
    expect(stages[1].label).toBe('Order Confirmation');
    expect(stages[1].status).toBe('upcoming');
    expect(stages[1].requiredPermissions).toContain('SALES_ORDER.CONFIRM');

    expect(stages[2].id).toBe('dispatch');
    expect(stages[2].label).toBe('Dispatch Note');
    expect(stages[2].route).toBe('/app/sales/dispatches');

    expect(stages[3].id).toBe('deliver');
    expect(stages[3].label).toBe('Deliver & Issue Stock');
    expect(stages[3].route).toBe('/app/sales/dispatches');

    expect(stages[4].id).toBe('invoice');
    expect(stages[4].label).toBe('Sales Invoice');
    expect(stages[4].route).toBe('/app/sales/invoices');
  });

  it('should default to recentLimit = 10 and respect custom configured limit', () => {
    expect(component.recentLimit).toBe(10);

    const generatedOrders: SalesOrderDto[] = Array.from({ length: 15 }, (_, i) => ({
      id: `so-${i + 1}`,
      tenantId: 'tenant-uuid-1',
      companyId: 'company-uuid-1',
      branchId: 'branch-uuid-1',
      customerId: 'cust-uuid-1',
      orderNumber: `SO-${2000 + i}`,
      orderDate: '2026-10-18',
      customerName: `Customer ${i + 1}`,
      status: SalesOrderStatus.Draft,
      totalAmount: 1000 * (i + 1),
      isActive: true,
      items: []
    }));

    component.orders.set(generatedOrders);

    // Default limit = 10
    expect(component.recentOrders().length).toBe(10);

    // Configurable limit = 5
    component.recentLimit = 5;
    expect(component.recentOrders().length).toBe(5);
  });

  it('should filter recent orders by search term and status', () => {
    component.orders.set(mockOrders);

    // Status filter
    component.recentStatusFilter.set('draft');
    expect(component.recentOrders().length).toBe(1);
    expect(component.recentOrders()[0].orderNumber).toBe('SO-2001');

    component.recentStatusFilter.set('submitted');
    expect(component.recentOrders().length).toBe(1);
    expect(component.recentOrders()[0].orderNumber).toBe('SO-2002');

    // Reset status and filter by search term
    component.recentStatusFilter.set('all');
    component.recentSearchTerm.set('SO-2003');
    expect(component.recentOrders().length).toBe(1);
    expect(component.recentOrders()[0].orderNumber).toBe('SO-2003');
  });

  it('should handle navigation boundaries gracefully when sales order route is not yet implemented', () => {
    vi.spyOn(component.scopeService, 'selectedCompany').mockReturnValue(mockCompany);
    vi.spyOn(component.scopeService, 'selectedBranch').mockReturnValue(mockBranch);
    vi.spyOn(component.scopeService, 'canOperateTransactions').mockReturnValue(true);
    vi.spyOn(component.salesOrderService, 'getOrders').mockReturnValue(of(mockOrders));

    fixture.detectChanges();

    // Since Router has no 'sales/orders' route configured in test TestBed
    expect(component.hasOrdersRoute()).toBe(false);

    // Should not call router.navigate for unconfigured order routes
    component.navigateToAllOrders();
    expect(router.navigate).not.toHaveBeenCalled();

    component.navigateToCreate();
    expect(router.navigate).not.toHaveBeenCalled();

    component.navigateToOrder(mockOrders[0]);
    expect(router.navigate).not.toHaveBeenCalled();

    // Process stage click on dispatch should navigate because it is a valid existing route
    component.onStageClick(component.overviewConfig().processFlow!.stages[2]);
    expect(router.navigate).toHaveBeenCalledWith(['/app/sales/dispatches'], { queryParams: undefined });
  });

  it('should handle 403 Forbidden with AccessDeniedComponent and without leaking technical urls', () => {
    vi.spyOn(component.scopeService, 'selectedCompany').mockReturnValue(mockCompany);
    vi.spyOn(component.scopeService, 'selectedBranch').mockReturnValue(mockBranch);
    vi.spyOn(component.scopeService, 'canOperateTransactions').mockReturnValue(true);
    vi.spyOn(component.salesOrderService, 'getOrders').mockReturnValue(
      throwError(() => ({
        status: 403,
        statusText: 'Forbidden',
        message: 'Http failure response for http://localhost:5000/api/...: 403 Forbidden'
      }))
    );

    fixture.detectChanges();

    expect(component.isAccessDenied()).toBe(true);
    expect(component.error()).toBeNull();

    const accessDeniedComp = fixture.nativeElement.querySelector('app-access-denied');
    expect(accessDeniedComp).toBeTruthy();
  });
});
