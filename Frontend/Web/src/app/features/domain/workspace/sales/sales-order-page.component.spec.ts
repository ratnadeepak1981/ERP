import { describe, it, expect, beforeEach, vi } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router, ActivatedRoute } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { of, throwError } from 'rxjs';
import { SalesOrderPageComponent, EditableSOLine } from './sales-order-page.component';
import { SalesOrderService } from '../../../../core/services/sales-order.service';
import { MasterDataService } from '../../../../core/services/master-data.service';
import { WorkspaceScopeService } from '../../../../core/services/workspace-scope.service';
import { AuthService } from '../../../../core/auth/auth.service';
import { SalesOrderDto, SalesOrderStatus } from '../../../../core/models/sales-order.models';
import { CustomerItem, ProductItem, Company, Branch } from '../../../../core/models/scope.models';

describe('SalesOrderPageComponent', () => {
  let component: SalesOrderPageComponent;
  let fixture: ComponentFixture<SalesOrderPageComponent>;
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
    currency: 'USD',
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

  const mockProducts: ProductItem[] = [
    {
      productId: 'prod-1',
      tenantId: 't1',
      productCode: 'PROD-001',
      productName: 'Commercial Widget A',
      categoryId: 'cat-1',
      unitOfMeasure: 'PCS',
      isPurchasable: false,
      isSellable: true,
      isManufacturable: true,
      canConsumeInProduction: false,
      canConsumeInMaintenance: false,
      isStockTracked: true,
      productType: 1,
      trackingMode: 0,
      valuationMethod: 1,
      standardCost: 50,
      sellingPrice: 100,
      reorderLevel: 5,
      reorderQuantity: 20,
      isActive: true
    }
  ];

  const mockExistingOrder: SalesOrderDto = {
    id: 'so-100',
    tenantId: 't1',
    companyId: mockCompany.id,
    branchId: mockBranch.id,
    customerId: 'cust-1',
    orderNumber: 'SO-2026-100',
    orderDate: '2026-10-18T00:00:00Z',
    deliveryDate: '2026-10-25T00:00:00Z',
    currency: 'USD',
    paymentTerms: 'Net 30',
    status: SalesOrderStatus.Draft,
    subTotal: 200,
    discountAmount: 20,
    taxAmount: 18,
    totalAmount: 198,
    notes: 'Urgent delivery requested',
    isActive: true,
    items: [
      {
        id: 'soi-1',
        salesOrderId: 'so-100',
        productId: 'prod-1',
        productCode: 'PROD-001',
        productName: 'Commercial Widget A',
        quantity: 2,
        unitPrice: 100,
        discountRate: 10,
        discountAmount: 20,
        taxRate: 10,
        taxAmount: 18,
        subTotal: 200,
        lineTotal: 198,
        isActive: true
      }
    ]
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SalesOrderPageComponent],
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

    fixture = TestBed.createComponent(SalesOrderPageComponent);
    component = fixture.componentInstance;
    salesOrderService = TestBed.inject(SalesOrderService);
    masterDataService = TestBed.inject(MasterDataService);
    scopeService = TestBed.inject(WorkspaceScopeService);
    authService = TestBed.inject(AuthService);
    router = TestBed.inject(Router);

    vi.spyOn(authService, 'isTenantAdmin').mockReturnValue(true);
    vi.spyOn(router, 'navigate').mockResolvedValue(true);
    vi.spyOn(masterDataService, 'getCustomers').mockReturnValue(of(mockCustomers));
    vi.spyOn(masterDataService, 'getProducts').mockReturnValue(of(mockProducts));
  });

  it('should initialize create form with auto-generated order number and default row', () => {
    vi.spyOn(scopeService, 'selectedCompany').mockReturnValue(mockCompany);
    vi.spyOn(scopeService, 'selectedBranch').mockReturnValue(mockBranch);

    fixture.detectChanges();

    expect(component.mode()).toBe('create');
    expect(component.headerForm.get('orderNumber')?.value).toMatch(/^SO-\d{4}-\d{4}$/);
    expect(component.workingLines().length).toBe(1);
    expect(component.subtotalAmount()).toBe(0);
    expect(component.totalOrderAmount()).toBe(0);
  });

  it('should calculate row amounts accurately matching backend RecalculateTotals rules', () => {
    const testLine: EditableSOLine = {
      lineId: 'line-1',
      productId: 'prod-1',
      productCode: 'PROD-001',
      productName: 'Commercial Widget A',
      quantity: 5,
      unitPrice: 200,
      discountRate: 10, // 10% discount
      discountAmount: 0,
      taxRate: 15, // 15% tax
      taxAmount: 0,
      subTotal: 0,
      lineTotal: 0
    };

    component.calculateRow(testLine);

    // subtotal = 5 * 200 = 1000
    expect(testLine.subTotal).toBe(1000);
    // discount = 1000 * 0.10 = 100
    expect(testLine.discountAmount).toBe(100);
    // taxable = 1000 - 100 = 900
    // tax = 900 * 0.15 = 135
    expect(testLine.taxAmount).toBe(135);
    // lineTotal = 900 + 135 = 1035
    expect(testLine.lineTotal).toBe(1035);
  });

  it('should compute document totals from working lines', () => {
    component.workingLines.set([
      {
        lineId: 'line-1',
        productId: 'prod-1',
        productCode: 'P1',
        productName: 'W1',
        quantity: 2,
        unitPrice: 100,
        discountRate: 10,
        discountAmount: 20,
        taxRate: 10,
        taxAmount: 18,
        subTotal: 200,
        lineTotal: 198
      },
      {
        lineId: 'line-2',
        productId: 'prod-1',
        productCode: 'P2',
        productName: 'W2',
        quantity: 1,
        unitPrice: 50,
        discountRate: 0,
        discountAmount: 0,
        taxRate: 0,
        taxAmount: 0,
        subTotal: 50,
        lineTotal: 50
      }
    ]);

    expect(component.subtotalAmount()).toBe(250);
    expect(component.discountAmount()).toBe(20);
    expect(component.taxAmount()).toBe(18);
    expect(component.totalOrderAmount()).toBe(248);
  });

  it('should save draft sales order successfully and navigate', () => {
    vi.spyOn(scopeService, 'selectedCompany').mockReturnValue(mockCompany);
    vi.spyOn(scopeService, 'selectedBranch').mockReturnValue(mockBranch);
    vi.spyOn(scopeService, 'canOperateTransactions').mockReturnValue(true);

    component.headerForm.patchValue({
      orderNumber: 'SO-2026-999',
      customerId: 'cust-1'
    });

    component.workingLines.set([
      {
        lineId: 'line-1',
        productId: 'prod-1',
        productCode: 'PROD-001',
        productName: 'Widget A',
        quantity: 2,
        unitPrice: 100,
        discountRate: 0,
        discountAmount: 0,
        taxRate: 0,
        taxAmount: 0,
        subTotal: 200,
        lineTotal: 200
      }
    ]);

    vi.spyOn(salesOrderService, 'createOrder').mockReturnValue(
      of({ ...mockExistingOrder, id: 'so-new', orderNumber: 'SO-2026-999' })
    );

    component.saveOrder(false);

    expect(salesOrderService.createOrder).toHaveBeenCalledWith(
      mockCompany.id,
      mockBranch.id,
      expect.objectContaining({
        orderNumber: 'SO-2026-999',
        customerId: 'cust-1',
        items: expect.arrayContaining([
          expect.objectContaining({ productId: 'prod-1', quantity: 2, unitPrice: 100 })
        ])
      })
    );
    expect(router.navigate).toHaveBeenCalledWith(['/app/sales/orders', 'so-new']);
  });

  it('should save and submit sales order when submit is true', () => {
    vi.spyOn(scopeService, 'selectedCompany').mockReturnValue(mockCompany);
    vi.spyOn(scopeService, 'selectedBranch').mockReturnValue(mockBranch);
    vi.spyOn(scopeService, 'canOperateTransactions').mockReturnValue(true);

    component.headerForm.patchValue({
      orderNumber: 'SO-2026-999',
      customerId: 'cust-1'
    });

    component.workingLines.set([
      {
        lineId: 'line-1',
        productId: 'prod-1',
        productCode: 'PROD-001',
        productName: 'Widget A',
        quantity: 2,
        unitPrice: 100,
        discountRate: 0,
        discountAmount: 0,
        taxRate: 0,
        taxAmount: 0,
        subTotal: 200,
        lineTotal: 200
      }
    ]);

    vi.spyOn(salesOrderService, 'createOrder').mockReturnValue(
      of({ ...mockExistingOrder, id: 'so-new', orderNumber: 'SO-2026-999' })
    );
    vi.spyOn(salesOrderService, 'submitOrder').mockReturnValue(
      of({ ...mockExistingOrder, id: 'so-new', status: SalesOrderStatus.Submitted })
    );

    component.saveOrder(true);

    expect(salesOrderService.createOrder).toHaveBeenCalled();
    expect(salesOrderService.submitOrder).toHaveBeenCalledWith(mockCompany.id, mockBranch.id, 'so-new');
    expect(router.navigate).toHaveBeenCalledWith(['/app/sales/orders', 'so-new']);
  });

  it('should populate existing order in view mode and navigate to dispatch when confirmed', () => {
    vi.spyOn(scopeService, 'selectedCompany').mockReturnValue(mockCompany);
    vi.spyOn(scopeService, 'selectedBranch').mockReturnValue(mockBranch);
    vi.spyOn(salesOrderService, 'getOrder').mockReturnValue(
      of({ ...mockExistingOrder, status: SalesOrderStatus.Confirmed })
    );

    component.loadExistingOrder('so-100');

    expect(component.currentOrder()?.status).toBe(SalesOrderStatus.Confirmed);
    expect(component.orderStatusName()).toBe('Confirmed');

    component.navigateToDispatch();

    expect(router.navigate).toHaveBeenCalledWith(['/app/inventory/issue-stock/new'], {
      queryParams: {
        purpose: 'sales',
        referenceNumber: 'SO-2026-100',
        salesOrderId: 'so-100'
      }
    });
  });

  it('should confirm order with remarks in confirm mode', () => {
    vi.spyOn(scopeService, 'selectedCompany').mockReturnValue(mockCompany);
    vi.spyOn(scopeService, 'selectedBranch').mockReturnValue(mockBranch);
    component.currentOrder.set(mockExistingOrder);

    vi.spyOn(salesOrderService, 'confirmOrder').mockReturnValue(
      of({ ...mockExistingOrder, status: SalesOrderStatus.Confirmed })
    );

    component.openRemarksDialog('confirm');
    component.remarksText.set('Customer PO confirmed');
    component.submitRemarksAction();

    expect(salesOrderService.confirmOrder).toHaveBeenCalledWith(
      mockCompany.id,
      mockBranch.id,
      'so-100',
      'Customer PO confirmed'
    );
  });
});
