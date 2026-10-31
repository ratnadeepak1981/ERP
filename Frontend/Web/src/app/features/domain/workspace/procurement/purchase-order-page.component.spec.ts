import { describe, it, expect, beforeEach, vi } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router, ActivatedRoute } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { of, throwError } from 'rxjs';
import { PurchaseOrderPageComponent } from './purchase-order-page.component';
import { PurchaseOrderService } from '../../../../core/services/purchase-order.service';
import { MasterDataService } from '../../../../core/services/master-data.service';
import { WorkspaceScopeService } from '../../../../core/services/workspace-scope.service';
import { AuthService } from '../../../../core/auth/auth.service';
import { PurchaseOrderDto, PurchaseOrderStatus } from '../../../../core/models/purchase-order.models';
import { SupplierItem, ProductItem, Company, Branch, CurrencyItem } from '../../../../core/models/scope.models';
import { PriceCardItem, PriceCardType } from '../../../../core/models/price-card.models';

describe('PurchaseOrderPageComponent', () => {
  let component: PurchaseOrderPageComponent;
  let fixture: ComponentFixture<PurchaseOrderPageComponent>;
  let poService: PurchaseOrderService;
  let masterDataService: MasterDataService;
  let scopeService: WorkspaceScopeService;
  let authService: AuthService;
  let router: Router;

  const mockCompanyA: Company = {
    id: 'c1111111-1111-1111-1111-111111111111',
    tenantId: 't1',
    name: 'Alpha Industries',
    code: 'ALPHA',
    isActive: true
  };

  const mockCompanyB: Company = {
    id: 'c2222222-2222-2222-2222-222222222222',
    tenantId: 't1',
    name: 'Beta Manufacturing',
    code: 'BETA',
    isActive: true
  };

  const mockBranchA: Branch = {
    id: 'b1111111-1111-1111-1111-111111111111',
    companyId: mockCompanyA.id,
    name: 'Colombo Plant',
    code: 'CMB',
    isActive: true
  };

  const mockSuppliers: SupplierItem[] = [
    {
      supplierId: 's1',
      tenantId: 't1',
      supplierCode: 'SUP-001',
      supplierName: 'Industrial Metals Ltd',
      isActive: true
    }
  ];

  const mockCurrencies: CurrencyItem[] = [
    {
      currencyId: 'currency-usd',
      tenantId: 't1',
      currencyCode: 'USD',
      currencyName: 'US Dollar',
      symbol: '$',
      decimalPlaces: 2,
      isActive: true
    },
    {
      currencyId: 'currency-eur',
      tenantId: 't1',
      currencyCode: 'EUR',
      currencyName: 'Euro',
      symbol: '€',
      decimalPlaces: 2,
      isActive: true
    }
  ];

  const mockPurchasePriceCard: PriceCardItem = {
    priceCardId: 'pc-eur',
    tenantId: 't1',
    priceCardCode: 'PUR-EUR',
    priceCardName: 'Euro Purchase Card',
    priceCardType: PriceCardType.Purchase,
    currency: 'EUR',
    effectiveFrom: '2026-01-01',
    isActive: true,
    lineCount: 0,
    lines: []
  };

  const mockProducts: ProductItem[] = [
    {
      productId: 'p1',
      tenantId: 't1',
      productCode: 'RAW-001',
      productName: 'Steel Sheet 2mm',
      categoryId: 'cat-1',
      unitOfMeasure: 'PCS',
      isPurchasable: true,
      isSellable: true,
      isManufacturable: false,
      canConsumeInProduction: true,
      canConsumeInMaintenance: false,
      isStockTracked: true,
      productType: 1,
      trackingMode: 0,
      valuationMethod: 1,
      standardCost: 20.0,
      sellingPrice: 35.0,
      reorderLevel: 10,
      reorderQuantity: 50,
      isActive: true
    }
  ];

  const mockOrder: PurchaseOrderDto = {
    id: 'po-101',
    tenantId: 't1',
    companyId: mockCompanyA.id,
    branchId: mockBranchA.id,
    supplierId: 's1',
    supplierName: 'Industrial Metals Ltd',
    orderNumber: 'PO-2026-101',
    orderDate: '2026-10-18T00:00:00Z',
    status: PurchaseOrderStatus.Draft,
    totalAmount: 200.0,
    isActive: true,
    items: [
      {
        id: 'poi-1',
        purchaseOrderId: 'po-101',
        productId: 'p1',
        productCode: 'RAW-001',
        productName: 'Steel Sheet 2mm',
        quantity: 10,
        unitPrice: 20.0,
        lineTotal: 200.0,
        isActive: true
      }
    ]
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [PurchaseOrderPageComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        PurchaseOrderService,
        MasterDataService,
        WorkspaceScopeService,
        AuthService,
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: {
                get: (key: string) => (key === 'id' ? null : null)
              },
              queryParams: {}
            },
            queryParams: of({}),
            paramMap: of({
              get: (key: string) => (key === 'id' ? null : null)
            })
          }
        }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(PurchaseOrderPageComponent);
    component = fixture.componentInstance;
    poService = TestBed.inject(PurchaseOrderService);
    masterDataService = TestBed.inject(MasterDataService);
    scopeService = TestBed.inject(WorkspaceScopeService);
    authService = TestBed.inject(AuthService);
    router = TestBed.inject(Router);

    vi.spyOn(masterDataService, 'getSuppliers').mockReturnValue(of(mockSuppliers));
    vi.spyOn(masterDataService, 'getProducts').mockReturnValue(of(mockProducts));
    vi.spyOn(masterDataService, 'getCurrencies').mockReturnValue(of(mockCurrencies));
    vi.spyOn(masterDataService, 'getPriceCards').mockReturnValue(of({
      items: [mockPurchasePriceCard],
      totalCount: 1,
      page: 1,
      pageSize: 100,
      hasMore: false
    }));
    scopeService.companies.set([mockCompanyA, mockCompanyB]);
    scopeService.branches.set([mockBranchA]);
    scopeService.selectedCompany.set(mockCompanyA);
    scopeService.selectedBranch.set(mockBranchA);
    scopeService.isAllBranches.set(false);
  });

  it('should initialize in create mode with default row and generated order number', () => {
    fixture.detectChanges();

    expect(component).toBeTruthy();
    expect(component.mode()).toBe('create');
    expect(component.pageTitle()).toBe('New Purchase Order');
    expect(component.headerForm.get('orderNumber')?.value).toMatch(/^PO-\d{4}-\d{4}$/);
    expect(component.workingLines().length).toBe(1);
    expect(component.subtotalAmount()).toBe(0);
  });

  it('should calculate row line total and subtotal reactively', () => {
    fixture.detectChanges();

    const line = component.workingLines()[0];
    line.quantity = 5;
    line.unitCost = 25.5;
    component.calculateRow(line);

    expect(line.lineTotal).toBe(127.5);
    expect(component.subtotalAmount()).toBe(127.5);
    expect(component.totalOrderAmount()).toBe(127.5);
  });

  it('should validate form and show error if saving with invalid fields', () => {
    fixture.detectChanges();

    // Supplier is empty initially
    component.headerForm.patchValue({ supplierId: '' });
    component.saveOrder(false);

    expect(component.message()?.severity).toBe('danger');
    expect(component.message()?.text).toContain('Please correct required fields');
  });

  it('uses the supplier default Price Card currency when the supplier is selected', () => {
    vi.spyOn(masterDataService, 'getSuppliers').mockReturnValue(of([
      { ...mockSuppliers[0], defaultPurchasePriceCardId: mockPurchasePriceCard.priceCardId }
    ]));
    fixture.detectChanges();

    component.headerForm.controls.supplierId.setValue(mockSuppliers[0].supplierId);

    expect(component.headerForm.controls.priceCardId.value).toBe(mockPurchasePriceCard.priceCardId);
    expect(component.headerForm.controls.currency.value).toBe('EUR');
  });

  it('uses the supplier default currency and permits a manual currency override without a Price Card', () => {
    vi.spyOn(masterDataService, 'getSuppliers').mockReturnValue(of([
      { ...mockSuppliers[0], currencyCode: 'EUR' }
    ]));
    const createOrder = vi.spyOn(poService, 'createOrder').mockReturnValue(of(mockOrder));
    vi.spyOn(router, 'navigate').mockResolvedValue(true);
    fixture.detectChanges();

    component.headerForm.patchValue({
      supplierId: mockSuppliers[0].supplierId,
      currency: 'USD'
    });
    component.workingLines.set([{
      lineId: 'new-line',
      productId: 'p1',
      productCode: 'RAW-001',
      productName: 'Steel Sheet 2mm',
      quantity: 1,
      unitCost: 20,
      lineTotal: 20
    }]);

    component.saveOrder(false);

    expect(createOrder).toHaveBeenCalledWith(
      mockCompanyA.id,
      mockBranchA.id,
      expect.objectContaining({ currency: 'USD', priceCardId: null })
    );
  });

  it('rejects a PO currency that differs from its selected Price Card currency', () => {
    const createOrder = vi.spyOn(poService, 'createOrder');
    fixture.detectChanges();
    component.headerForm.patchValue({
      supplierId: mockSuppliers[0].supplierId,
      currency: 'USD',
      priceCardId: mockPurchasePriceCard.priceCardId
    });
    component.workingLines.set([{
      lineId: 'new-line',
      productId: 'p1',
      productCode: 'RAW-001',
      productName: 'Steel Sheet 2mm',
      quantity: 1,
      unitCost: 20,
      lineTotal: 20
    }]);

    component.saveOrder(false);

    expect(component.message()?.text).toContain('PO currency must match');
    expect(createOrder).not.toHaveBeenCalled();
  });

  it('should prompt confirmation when user changes company with dirty line items', () => {
    fixture.detectChanges();

    // Mark line with a product
    component.workingLines.set([
      {
        lineId: 'line-1',
        productId: 'p1',
        productCode: 'RAW-001',
        productName: 'Steel Sheet 2mm',
        quantity: 5,
        unitCost: 20,
        lineTotal: 100
      }
    ]);

    component.onCompanyChange(mockCompanyB.id);

    expect(component.confirmScopeChangeDialog()).toBe(true);
    expect(component.pendingCompanyChange()?.id).toBe(mockCompanyB.id);

    // Cancel change
    component.cancelCompanyChange();
    expect(component.confirmScopeChangeDialog()).toBe(false);
    expect(component.selectedCompanyId()).toBe(mockCompanyA.id);

    // Now confirm change
    component.onCompanyChange(mockCompanyB.id);
    component.confirmCompanyChange();
    expect(component.confirmScopeChangeDialog()).toBe(false);
    expect(component.selectedCompanyId()).toBe(mockCompanyB.id);
    expect(component.workingLines()[0].productId).toBe('');
  });

  it('should save draft order and navigate to purchase orders register', () => {
    fixture.detectChanges();

    component.headerForm.patchValue({
      orderNumber: 'PO-2026-999',
      supplierId: 's1',
      orderDate: '2026-10-18',
      currency: 'USD'
    });

    component.workingLines.set([
      {
        lineId: 'line-1',
        productId: 'p1',
        productCode: 'RAW-001',
        productName: 'Steel Sheet 2mm',
        quantity: 3,
        unitCost: 20,
        lineTotal: 60
      }
    ]);

    const createSpy = vi.spyOn(poService, 'createOrder').mockReturnValue(of(mockOrder));
    const navSpy = vi.spyOn(router, 'navigate');

    component.saveOrder(false);

    expect(createSpy).toHaveBeenCalledWith(
      mockCompanyA.id,
      mockBranchA.id,
      expect.objectContaining({
        orderNumber: 'PO-2026-999',
        supplierId: 's1',
        items: expect.arrayContaining([
          expect.objectContaining({
            productId: 'p1',
            quantity: 3,
            unitPrice: 20
          })
        ])
      })
    );
    expect(navSpy).toHaveBeenCalledWith(['/app/procurement/purchase-orders']);
  });

  it('should save and submit order when submit flag is true', () => {
    fixture.detectChanges();

    component.headerForm.patchValue({
      orderNumber: 'PO-2026-999',
      supplierId: 's1',
      orderDate: '2026-10-18',
      currency: 'USD'
    });

    component.workingLines.set([
      {
        lineId: 'line-1',
        productId: 'p1',
        productCode: 'RAW-001',
        productName: 'Steel Sheet 2mm',
        quantity: 3,
        unitCost: 20,
        lineTotal: 60
      }
    ]);

    vi.spyOn(poService, 'createOrder').mockReturnValue(of(mockOrder));
    const submitSpy = vi.spyOn(poService, 'submitOrder').mockReturnValue(of(mockOrder));
    const navSpy = vi.spyOn(router, 'navigate');

    component.saveOrder(true);

    expect(submitSpy).toHaveBeenCalledWith(mockCompanyA.id, mockBranchA.id, mockOrder.id);
    expect(navSpy).toHaveBeenCalledWith(['/app/procurement/purchase-orders']);
  });

  it('should handle remarks dialog rejection requiring mandatory remarks', () => {
    fixture.detectChanges();
    component.currentOrder.set({ ...mockOrder, status: PurchaseOrderStatus.Submitted });

    component.openRemarksDialog('reject');
    expect(component.remarksDialogVisible()).toBe(true);

    // Empty remarks
    component.remarksText.set('');
    component.confirmRemarksDecision();
    expect(component.remarksError()?.toLowerCase()).toContain('mandatory');

    // Valid remarks
    component.remarksText.set('Over budget requisition');
    const rejectSpy = vi.spyOn(poService, 'rejectOrder').mockReturnValue(
      of({ ...mockOrder, status: PurchaseOrderStatus.Rejected })
    );

    component.confirmRemarksDecision();
    expect(rejectSpy).toHaveBeenCalledWith(
      mockCompanyA.id,
      mockBranchA.id,
      mockOrder.id,
      'Over budget requisition'
    );
    expect(component.remarksDialogVisible()).toBe(false);
  });

  it('should render AccessDeniedComponent when order fetch returns 403 Forbidden', () => {
    fixture.detectChanges();

    const forbiddenError = {
      status: 403,
      statusText: 'Forbidden',
      message: 'Http failure response for http://localhost:4200/api/companies/c1/branches/b1/purchase-orders/po-101: 403 Forbidden'
    };
    vi.spyOn(poService, 'getOrder').mockReturnValue(throwError(() => forbiddenError));

    component.loadExistingOrder('po-101');
    fixture.detectChanges();

    expect(component.isAccessDenied()).toBe(true);
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('.po-access-denied-wrapper')).toBeTruthy();
    expect(compiled.textContent).not.toContain('http://localhost:4200');
  });

  describe('Multi-line PO Data Loss & Synchronisation Regression Tests', () => {
    it('should update workingLines signal when onLinesChange is triggered by table dataChange event', () => {
      fixture.detectChanges();

      const multiLines = [
        {
          lineId: 'line-1',
          productId: 'p1',
          productCode: 'RAW-001',
          productName: 'Steel Sheet 2mm',
          quantity: 2,
          unitCost: 10,
          lineTotal: 20
        },
        {
          lineId: 'line-2',
          productId: 'p2',
          productCode: 'RAW-002',
          productName: 'Aluminium Plate',
          quantity: 5,
          unitCost: 30,
          lineTotal: 150
        },
        {
          lineId: 'line-3',
          productId: 'p3',
          productCode: 'RAW-003',
          productName: 'Copper Wire',
          quantity: 10,
          unitCost: 5,
          lineTotal: 50
        }
      ];

      component.onLinesChange(multiLines);

      expect(component.workingLines().length).toBe(3);
      expect(component.workingLines()[0].productId).toBe('p1');
      expect(component.workingLines()[1].productId).toBe('p2');
      expect(component.workingLines()[2].productId).toBe('p3');
      expect(component.subtotalAmount()).toBe(220);
    });

    it('should preserve all lines and calculate subtotal reactively when calculateRow is called', () => {
      fixture.detectChanges();

      const initialLines = [
        {
          lineId: 'line-1',
          productId: 'p1',
          productCode: 'RAW-001',
          productName: 'Steel Sheet 2mm',
          quantity: 2,
          unitCost: 10,
          lineTotal: 20
        },
        {
          lineId: 'line-2',
          productId: 'p2',
          productCode: 'RAW-002',
          productName: 'Aluminium Plate',
          quantity: 5,
          unitCost: 30,
          lineTotal: 150
        },
        {
          lineId: 'line-3',
          productId: 'p3',
          productCode: 'RAW-003',
          productName: 'Copper Wire',
          quantity: 10,
          unitCost: 5,
          lineTotal: 50
        }
      ];

      component.workingLines.set(initialLines);

      // Modify middle line (line-2)
      const lineToEdit = component.workingLines()[1];
      lineToEdit.quantity = 8;
      lineToEdit.unitCost = 35;

      component.calculateRow(lineToEdit);

      expect(component.workingLines().length).toBe(3);
      // Line 1 intact
      expect(component.workingLines()[0].productId).toBe('p1');
      expect(component.workingLines()[0].lineTotal).toBe(20);

      // Line 2 recalculated (8 * 35 = 280)
      expect(component.workingLines()[1].productId).toBe('p2');
      expect(component.workingLines()[1].lineTotal).toBe(280);

      // Line 3 intact
      expect(component.workingLines()[2].productId).toBe('p3');
      expect(component.workingLines()[2].lineTotal).toBe(50);

      // Total order subtotal updated
      expect(component.subtotalAmount()).toBe(20 + 280 + 50);
    });

    it('should include all multiple lines when saving purchase order', () => {
      fixture.detectChanges();

      component.headerForm.patchValue({
        orderNumber: 'PO-2026-MULTI',
        supplierId: 's1',
        orderDate: '2026-10-18',
        currency: 'USD'
      });

      const threeLines = [
        {
          lineId: 'line-1',
          productId: 'p1',
          productCode: 'RAW-001',
          productName: 'Steel Sheet 2mm',
          quantity: 2,
          unitCost: 10,
          lineTotal: 20
        },
        {
          lineId: 'line-2',
          productId: 'p2',
          productCode: 'RAW-002',
          productName: 'Aluminium Plate',
          quantity: 4,
          unitCost: 25,
          lineTotal: 100
        },
        {
          lineId: 'line-3',
          productId: 'p3',
          productCode: 'RAW-003',
          productName: 'Copper Wire',
          quantity: 1,
          unitCost: 50,
          lineTotal: 50
        }
      ];

      component.workingLines.set(threeLines);

      const createSpy = vi.spyOn(poService, 'createOrder').mockReturnValue(of(mockOrder));
      const navSpy = vi.spyOn(router, 'navigate');

      component.saveOrder(false);

      expect(createSpy).toHaveBeenCalledWith(
        mockCompanyA.id,
        mockBranchA.id,
        expect.objectContaining({
          items: [
            expect.objectContaining({ productId: 'p1', quantity: 2, unitPrice: 10 }),
            expect.objectContaining({ productId: 'p2', quantity: 4, unitPrice: 25 }),
            expect.objectContaining({ productId: 'p3', quantity: 1, unitPrice: 50 })
          ]
        })
      );
    });
  });

  describe('Standardized 4-Mode & Master Data Enrichment Tests', () => {
    it('should support view mode with read-only properties and title', () => {
      fixture.detectChanges();
      component.mode.set('view');
      component.currentOrder.set(mockOrder);

      expect(component.isReadOnlyMode()).toBe(true);
      expect(component.pageTitle()).toBe('Purchase Order Details - PO-2026-101');
      expect(component.editableItemColumns().every(col => col.editable === false || col.field === 'productName' || col.field === 'lineTotal')).toBe(true);
    });

    it('should support approval mode with read-only properties and approval title', () => {
      fixture.detectChanges();
      component.mode.set('approval');
      component.currentOrder.set({ ...mockOrder, status: PurchaseOrderStatus.Submitted });

      expect(component.isReadOnlyMode()).toBe(true);
      expect(component.pageTitle()).toBe('Purchase Order Approval - PO-2026-101');
      expect(component.canApprove()).toBe(false); // No approver role yet

      vi.spyOn(authService, 'hasRole').mockImplementation((r: string) => r === 'Approver');
      expect(component.canApprove()).toBe(true);
    });

    it('should support amend mode for draft order and allow editing', () => {
      vi.spyOn(authService, 'isTenantAdmin').mockReturnValue(true);
      fixture.detectChanges();
      component.mode.set('amend');
      component.currentOrder.set(mockOrder);

      expect(component.isReadOnlyMode()).toBe(false);
      expect(component.pageTitle()).toBe('Amend Purchase Order - PO-2026-101');
      expect(component.canEdit()).toBe(true);
      expect(component.editableItemColumns().some(col => col.field === 'productCode' && col.editable === true)).toBe(true);
    });

    it('should enforce read-only fallback when attempting to amend an approved purchase order', () => {
      fixture.detectChanges();
      component.mode.set('amend');

      const approvedOrder: PurchaseOrderDto = {
        ...mockOrder,
        status: PurchaseOrderStatus.Approved
      };
      vi.spyOn(poService, 'getOrder').mockReturnValue(of(approvedOrder));

      component.loadExistingOrder(approvedOrder.id);

      expect(component.mode()).toBe('view');
      expect(component.isReadOnlyMode()).toBe(true);
      expect(component.message()?.severity).toBe('warn');
      expect(component.message()?.text).toContain('cannot be amended');
    });

    it('should enrich line items with product master code and name when loaded without them', () => {
      fixture.detectChanges();
      // Backend returns order items with productId only, missing productCode and productName
      const orderWithBareItems: PurchaseOrderDto = {
        ...mockOrder,
        items: [
          {
            id: 'poi-bare',
            purchaseOrderId: 'po-101',
            productId: 'p1',
            quantity: 5,
            unitPrice: 20.0,
            lineTotal: 100.0,
            isActive: true
          }
        ]
      };

      vi.spyOn(poService, 'getOrder').mockReturnValue(of(orderWithBareItems));
      component.loadExistingOrder(orderWithBareItems.id);

      const lines = component.workingLines();
      expect(lines.length).toBe(1);
      expect(lines[0].productId).toBe('p1');
      expect(lines[0].productCode).toBe('RAW-001');
      expect(lines[0].productName).toBe('Steel Sheet 2mm');
    });

    it('should open approval dialog and successfully confirm approval with optional remarks', () => {
      fixture.detectChanges();
      component.currentOrder.set({ ...mockOrder, status: PurchaseOrderStatus.Submitted });

      component.openApproveDialog();
      expect(component.approveDialogVisible()).toBe(true);
      expect(component.approveRemarks()).toBe('');

      component.approveRemarks.set('Approved as per budget plan Q4');
      const approveSpy = vi.spyOn(poService, 'approveOrder').mockReturnValue(
        of({ ...mockOrder, status: PurchaseOrderStatus.Approved })
      );

      component.confirmApprove();
      expect(approveSpy).toHaveBeenCalledWith(
        mockCompanyA.id,
        mockBranchA.id,
        mockOrder.id,
        'Approved as per budget plan Q4'
      );
      expect(component.approveDialogVisible()).toBe(false);
      expect(component.currentOrder()?.status).toBe(PurchaseOrderStatus.Approved);
    });

    it('should retain approval dialog open and show error feedback if approval API fails', () => {
      fixture.detectChanges();
      component.currentOrder.set({ ...mockOrder, status: PurchaseOrderStatus.Submitted });

      component.openApproveDialog();
      expect(component.approveDialogVisible()).toBe(true);

      const errorResponse = {
        status: 400,
        error: { message: 'Order has already been approved by another manager.' }
      };
      vi.spyOn(poService, 'approveOrder').mockReturnValue(throwError(() => errorResponse));

      component.confirmApprove();
      expect(component.approveDialogVisible()).toBe(true);
      expect(component.approveError()).toContain('Order has already been approved');
      expect(component.actionInProgress()).toBe(false);
    });

    it('should enforce mandatory remarks for rejection dialog', () => {
      fixture.detectChanges();
      component.currentOrder.set({ ...mockOrder, status: PurchaseOrderStatus.Submitted });

      component.openRejectDialog();
      expect(component.rejectDialogVisible()).toBe(true);

      // Attempt confirm without remarks
      component.rejectRemarks.set('   ');
      component.confirmReject();
      expect(component.rejectError()).toContain('Remarks are mandatory');
      expect(component.rejectDialogVisible()).toBe(true);

      // Enter valid rejection remarks
      component.rejectRemarks.set('Pricing exceeds approved rate contract');
      const rejectSpy = vi.spyOn(poService, 'rejectOrder').mockReturnValue(
        of({ ...mockOrder, status: PurchaseOrderStatus.Rejected })
      );

      component.confirmReject();
      expect(rejectSpy).toHaveBeenCalledWith(
        mockCompanyA.id,
        mockBranchA.id,
        mockOrder.id,
        'Pricing exceeds approved rate contract'
      );
      expect(component.rejectDialogVisible()).toBe(false);
      expect(component.currentOrder()?.status).toBe(PurchaseOrderStatus.Rejected);
    });
  });
});
