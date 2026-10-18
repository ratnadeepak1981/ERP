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
import { SupplierItem, ProductItem, Company, Branch } from '../../../../core/models/scope.models';

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
      orderDate: '2026-10-18'
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
      orderDate: '2026-10-18'
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
});
