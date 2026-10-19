import { describe, it, expect, beforeEach, vi } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter, Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import { ProcurementOverviewComponent } from './procurement-overview.component';
import { PurchaseOrderDto, PurchaseOrderStatus } from '../../../../core/models/purchase-order.models';
import { Company, Branch } from '../../../../core/models/scope.models';
import { AuthService } from '../../../../core/auth/auth.service';

describe('ProcurementOverviewComponent', () => {
  let component: ProcurementOverviewComponent;
  let fixture: ComponentFixture<ProcurementOverviewComponent>;
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

  const mockOrders: PurchaseOrderDto[] = [
    {
      id: 'po-1',
      tenantId: 'tenant-uuid-1',
      companyId: 'company-uuid-1',
      branchId: 'branch-uuid-1',
      orderNumber: 'PO-1048',
      supplierId: 'sup-1',
      supplierName: 'Northern Supplies',
      orderDate: '2026-10-18',
      status: PurchaseOrderStatus.Submitted, // Pending
      totalAmount: 245000,
      isActive: true,
      items: []
    },
    {
      id: 'po-2',
      tenantId: 'tenant-uuid-1',
      companyId: 'company-uuid-1',
      branchId: 'branch-uuid-1',
      orderNumber: 'PO-1047',
      supplierId: 'sup-2',
      supplierName: 'City Wholesale',
      orderDate: '2026-10-17',
      status: PurchaseOrderStatus.Approved,
      totalAmount: 186500,
      isActive: true,
      items: []
    },
    {
      id: 'po-3',
      tenantId: 'tenant-uuid-1',
      companyId: 'company-uuid-1',
      branchId: 'branch-uuid-1',
      orderNumber: 'PO-1046',
      supplierId: 'sup-3',
      supplierName: 'Premier Industrial',
      orderDate: '2026-10-16',
      status: PurchaseOrderStatus.Draft,
      totalAmount: 92000,
      isActive: true,
      items: []
    },
    {
      id: 'po-4',
      tenantId: 'tenant-uuid-1',
      companyId: 'company-uuid-1',
      branchId: 'branch-uuid-1',
      orderNumber: 'PO-1045',
      supplierId: 'sup-1',
      supplierName: 'Northern Supplies',
      orderDate: '2026-10-15',
      status: PurchaseOrderStatus.Approved,
      totalAmount: 318750,
      isActive: true,
      items: []
    }
  ];

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ProcurementOverviewComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(ProcurementOverviewComponent);
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
    vi.spyOn(component.poService, 'getOrders').mockReturnValue(of(mockOrders));

    fixture.detectChanges();

    expect(component.orders().length).toBe(4);
    expect(component.draftCount()).toBe(1);
    expect(component.pendingCount()).toBe(1);
    expect(component.approvedCount()).toBe(2);
    expect(component.totalOrderValue()).toBe(842250);
    expect(component.formattedTotalOrderValue()).toContain('LKR');

    const draftCard = fixture.nativeElement.querySelector('[data-testid="dashboard-card-draft"] .card-metric-value');
    expect(draftCard?.textContent?.trim()).toBe('1');

    const pendingCard = fixture.nativeElement.querySelector('[data-testid="dashboard-card-pending"] .card-metric-value');
    expect(pendingCard?.textContent?.trim()).toBe('1');

    const approvedCard = fixture.nativeElement.querySelector('[data-testid="dashboard-card-approved"] .card-metric-value');
    expect(approvedCard?.textContent?.trim()).toBe('2');

    // Verify Process Flow is rendered
    const processFlow = fixture.nativeElement.querySelector('app-process-flow');
    expect(processFlow).toBeTruthy();
  });

  it('should default to recentLimit = 10 and respect custom configured limit', () => {
    expect(component.recentLimit).toBe(10);

    const generatedOrders: PurchaseOrderDto[] = Array.from({ length: 15 }, (_, i) => ({
      id: `po-${i + 1}`,
      tenantId: 'tenant-uuid-1',
      companyId: 'company-uuid-1',
      branchId: 'branch-uuid-1',
      orderNumber: `PO-${1000 + i}`,
      orderDate: '2026-10-18',
      supplierName: `Vendor ${i + 1}`,
      status: PurchaseOrderStatus.Draft,
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

  it('should navigate to dedicated screen with corresponding status query param when summary card is clicked', () => {
    vi.spyOn(component.scopeService, 'selectedCompany').mockReturnValue(mockCompany);
    vi.spyOn(component.scopeService, 'selectedBranch').mockReturnValue(mockBranch);
    vi.spyOn(component.scopeService, 'canOperateTransactions').mockReturnValue(true);
    vi.spyOn(component.poService, 'getOrders').mockReturnValue(of(mockOrders));

    fixture.detectChanges();

    component.navigateToStatus('draft');
    expect(router.navigate).toHaveBeenCalledWith(['/app/procurement/purchase-orders'], {
      queryParams: { status: 'draft' }
    });

    component.navigateToStatus('submitted');
    expect(router.navigate).toHaveBeenCalledWith(['/app/procurement/purchase-orders'], {
      queryParams: { status: 'submitted' }
    });

    component.navigateToStatus('approved');
    expect(router.navigate).toHaveBeenCalledWith(['/app/procurement/purchase-orders'], {
      queryParams: { status: 'approved' }
    });
  });

  it('should navigate to dedicated screen on "View all Purchase Orders" and "+ Create PO"', () => {
    vi.spyOn(component.scopeService, 'selectedCompany').mockReturnValue(mockCompany);
    vi.spyOn(component.scopeService, 'selectedBranch').mockReturnValue(mockBranch);
    vi.spyOn(component.scopeService, 'canOperateTransactions').mockReturnValue(true);
    vi.spyOn(component.poService, 'getOrders').mockReturnValue(of(mockOrders));

    fixture.detectChanges();

    component.navigateToAllOrders();
    expect(router.navigate).toHaveBeenCalledWith(['/app/procurement/purchase-orders']);

    component.navigateToCreate();
    expect(router.navigate).toHaveBeenCalledWith(['/app/procurement/purchase-orders'], {
      queryParams: { action: 'create' }
    });
  });

  it('should handle 403 Forbidden with AccessDeniedComponent and without leaking technical urls', () => {
    vi.spyOn(component.scopeService, 'selectedCompany').mockReturnValue(mockCompany);
    vi.spyOn(component.scopeService, 'selectedBranch').mockReturnValue(mockBranch);
    vi.spyOn(component.scopeService, 'canOperateTransactions').mockReturnValue(true);
    vi.spyOn(component.poService, 'getOrders').mockReturnValue(
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
