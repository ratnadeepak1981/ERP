import { describe, it, expect, beforeEach, vi } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { of, throwError } from 'rxjs';
import { PurchaseOrderComponent } from './purchase-order.component';
import { PurchaseOrderService } from '../../../../core/services/purchase-order.service';
import { MasterDataService } from '../../../../core/services/master-data.service';
import { WorkspaceScopeService } from '../../../../core/services/workspace-scope.service';
import { AuthService } from '../../../../core/auth/auth.service';
import { PurchaseOrderDto, PurchaseOrderStatus } from '../../../../core/models/purchase-order.models';
import { SupplierItem, ProductItem, Company, Branch } from '../../../../core/models/scope.models';

describe('PurchaseOrderComponent', () => {
  let component: PurchaseOrderComponent;
  let fixture: ComponentFixture<PurchaseOrderComponent>;
  let poService: PurchaseOrderService;
  let masterDataService: MasterDataService;
  let scopeService: WorkspaceScopeService;
  let authService: AuthService;

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
      standardCost: 15.5,
      sellingPrice: 25,
      reorderLevel: 10,
      reorderQuantity: 50,
      isActive: true
    }
  ];

  const mockOrders: PurchaseOrderDto[] = [
    {
      id: 'po-1',
      tenantId: 't1',
      companyId: mockCompany.id,
      branchId: mockBranch.id,
      supplierId: 's1',
      supplierName: 'Industrial Metals Ltd',
      orderNumber: 'PO-2026-001',
      orderDate: '2026-10-18T00:00:00Z',
      status: PurchaseOrderStatus.Draft,
      totalAmount: 155.0,
      isActive: true,
      items: [
        {
          id: 'poi-1',
          purchaseOrderId: 'po-1',
          productId: 'p1',
          productCode: 'RAW-001',
          productName: 'Steel Sheet 2mm',
          quantity: 10,
          unitPrice: 15.5,
          lineTotal: 155.0,
          isActive: true
        }
      ]
    },
    {
      id: 'po-2',
      tenantId: 't1',
      companyId: mockCompany.id,
      branchId: mockBranch.id,
      supplierId: 's1',
      supplierName: 'Industrial Metals Ltd',
      orderNumber: 'PO-2026-002',
      orderDate: '2026-10-17T00:00:00Z',
      status: PurchaseOrderStatus.Submitted,
      totalAmount: 310.0,
      isActive: true,
      items: []
    }
  ];

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [PurchaseOrderComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        PurchaseOrderService,
        MasterDataService,
        WorkspaceScopeService,
        AuthService
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(PurchaseOrderComponent);
    component = fixture.componentInstance;
    poService = TestBed.inject(PurchaseOrderService);
    masterDataService = TestBed.inject(MasterDataService);
    scopeService = TestBed.inject(WorkspaceScopeService);
    authService = TestBed.inject(AuthService);

    // Default mock setup
    vi.spyOn(masterDataService, 'getSuppliers').mockReturnValue(of(mockSuppliers));
    vi.spyOn(masterDataService, 'getProducts').mockReturnValue(of(mockProducts));
    vi.spyOn(poService, 'getOrders').mockReturnValue(of(mockOrders));
  });

  it('should create component and render scope prompt when company or branch is unselected', () => {
    // No scope set
    scopeService.selectedCompany.set(null);
    scopeService.selectedBranch.set(null);
    scopeService.isAllBranches.set(true);

    fixture.detectChanges();

    expect(component).toBeTruthy();
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Operating Company and Branch Required');
  });

  it('should load purchase orders when company and branch are actively selected', () => {
    scopeService.selectedCompany.set(mockCompany);
    scopeService.selectedBranch.set(mockBranch);
    scopeService.isAllBranches.set(false);

    fixture.detectChanges();
    component.loadOrders();

    expect(component.orders().length).toBe(2);
    expect(component.filteredOrders().length).toBe(2);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('PO-2026-001');
    expect(compiled.textContent).toContain('Industrial Metals Ltd');
  });

  it('should filter purchase orders by search term and status pills', () => {
    scopeService.selectedCompany.set(mockCompany);
    scopeService.selectedBranch.set(mockBranch);
    scopeService.isAllBranches.set(false);
    fixture.detectChanges();
    component.orders.set(mockOrders);

    // Search by PO number
    component.searchTerm.set('002');
    expect(component.filteredOrders().length).toBe(1);
    expect(component.filteredOrders()[0].orderNumber).toBe('PO-2026-002');

    // Filter by Draft status
    component.searchTerm.set('');
    component.activeStatusFilter.set('draft');
    expect(component.filteredOrders().length).toBe(1);
    expect(component.filteredOrders()[0].orderNumber).toBe('PO-2026-001');
  });

  it('should open create dialog with initial default row and save draft order', () => {
    scopeService.selectedCompany.set(mockCompany);
    scopeService.selectedBranch.set(mockBranch);
    scopeService.isAllBranches.set(false);
    fixture.detectChanges();

    component.suppliers.set(mockSuppliers);
    component.products.set(mockProducts);
    component.openCreateDialog();

    expect(component.createDialogVisible()).toBe(true);
    expect(component.workingLines().length).toBe(1);

    // Populate line item
    component.workingLines.set([
      {
        lineId: 'line-1',
        productId: 'p1',
        productCode: 'RAW-001',
        productName: 'Steel Sheet 2mm',
        quantity: 5,
        unitCost: 15.5,
        lineTotal: 77.5
      }
    ]);

    const createdMock: PurchaseOrderDto = {
      id: 'po-new',
      tenantId: 't1',
      companyId: mockCompany.id,
      branchId: mockBranch.id,
      orderNumber: component.formOrderNumber(),
      orderDate: '2026-10-18T00:00:00Z',
      status: PurchaseOrderStatus.Draft,
      totalAmount: 77.5,
      isActive: true,
      items: []
    };

    const createSpy = vi.spyOn(poService, 'createOrder').mockReturnValue(of(createdMock));

    component.onSaveOrder(false);

    expect(createSpy).toHaveBeenCalledWith(
      mockCompany.id,
      mockBranch.id,
      expect.objectContaining({
        orderNumber: component.formOrderNumber(),
        items: expect.arrayContaining([
          expect.objectContaining({
            productId: 'p1',
            quantity: 5,
            unitPrice: 15.5
          })
        ])
      })
    );
    expect(component.createDialogVisible()).toBe(false);
  });

  it('should submit a draft order', () => {
    scopeService.selectedCompany.set(mockCompany);
    scopeService.selectedBranch.set(mockBranch);
    scopeService.isAllBranches.set(false);
    fixture.detectChanges();

    const submittedMock = { ...mockOrders[0], status: PurchaseOrderStatus.Submitted };
    const submitSpy = vi.spyOn(poService, 'submitOrder').mockReturnValue(of(submittedMock));

    component.onSubmitOrder(mockOrders[0]);

    expect(submitSpy).toHaveBeenCalledWith(mockCompany.id, mockBranch.id, mockOrders[0].id);
    expect(component.message()?.text).toContain('submitted successfully');
  });

  it('should handle approval via remarks dialog', () => {
    scopeService.selectedCompany.set(mockCompany);
    scopeService.selectedBranch.set(mockBranch);
    scopeService.isAllBranches.set(false);
    fixture.detectChanges();

    const approvedMock = { ...mockOrders[1], status: PurchaseOrderStatus.Approved };
    const approveSpy = vi.spyOn(poService, 'approveOrder').mockReturnValue(of(approvedMock));

    component.openRemarksDialog('approve', mockOrders[1]);
    expect(component.remarksDialogVisible()).toBe(true);

    component.remarksText.set('Approved for procurement');
    component.onConfirmRemarks();

    expect(approveSpy).toHaveBeenCalledWith(
      mockCompany.id,
      mockBranch.id,
      mockOrders[1].id,
      'Approved for procurement'
    );
    expect(component.remarksDialogVisible()).toBe(false);
    expect(component.message()?.text).toContain('approved successfully');
  });

  it('should render AccessDeniedComponent instead of raw URL error when API returns 403 Forbidden', () => {
    scopeService.selectedCompany.set(mockCompany);
    scopeService.selectedBranch.set(mockBranch);
    scopeService.isAllBranches.set(false);

    const forbiddenError = {
      status: 403,
      statusText: 'Forbidden',
      message: 'Http failure response for http://localhost:4200/api/companies/c1/branches/b1/purchase-orders: 403 Forbidden'
    };
    vi.spyOn(poService, 'getOrders').mockReturnValue(throwError(() => forbiddenError));

    fixture.detectChanges();
    component.loadOrders();
    fixture.detectChanges();

    expect(component.isAccessDenied()).toBe(true);
    expect(component.error()).toBeNull();

    const compiled = fixture.nativeElement as HTMLElement;
    // Must render the friendly AccessDeniedComponent
    expect(compiled.querySelector('.po-access-denied-wrapper')).toBeTruthy();
    expect(compiled.querySelector('.denied-title')?.textContent).toBe('Access restricted');
    // Must NOT leak raw localhost URLs, GUIDs or exception dumps
    expect(compiled.textContent).not.toContain('http://localhost:4200');
    expect(compiled.textContent).not.toContain('Http failure response');
  });

  it('should sanitize mutation errors when action returns 403 Forbidden', () => {
    scopeService.selectedCompany.set(mockCompany);
    scopeService.selectedBranch.set(mockBranch);
    scopeService.isAllBranches.set(false);
    fixture.detectChanges();

    const forbiddenError = {
      status: 403,
      message: 'Http failure response for http://localhost:4200/api/...: 403 Forbidden'
    };
    vi.spyOn(poService, 'submitOrder').mockReturnValue(throwError(() => forbiddenError));

    component.onSubmitOrder(mockOrders[0]);

    expect(component.message()?.severity).toBe('danger');
    expect(component.message()?.text).toContain("You don't have authorization to perform this action.");
    expect(component.message()?.text).not.toContain('http://localhost:4200');
  });

  it('should reload orders when scope changes to valid operating branch', () => {
    scopeService.selectedCompany.set(mockCompany);
    scopeService.selectedBranch.set(mockBranch);
    scopeService.isAllBranches.set(false);
    const getOrdersSpy = vi.spyOn(poService, 'getOrders').mockReturnValue(of(mockOrders));

    fixture.detectChanges();
    expect(getOrdersSpy).toHaveBeenCalled();

    // Trigger scopeChange subject
    (scopeService as any).scopeChangeSubject?.next(scopeService.activeScope());
    expect(getOrdersSpy).toHaveBeenCalledTimes(2);
  });

  describe('Standardized List Navigation & Actions', () => {
    it('should navigate to amend mode for draft purchase order', () => {
      const navSpy = vi.spyOn(component['router']!, 'navigate');
      component.navigateToAmend(mockOrders[0]);
      expect(navSpy).toHaveBeenCalledWith(['/app/procurement/purchase-orders', mockOrders[0].id, 'amend']);
    });

    it('should navigate to approval mode for purchase order approval', () => {
      const navSpy = vi.spyOn(component['router']!, 'navigate');
      component.navigateToApproval(mockOrders[1]);
      expect(navSpy).toHaveBeenCalledWith(['/app/procurement/purchase-orders', mockOrders[1].id, 'approve']);
    });

    it('should check permissions for amend action', () => {
      vi.spyOn(authService, 'hasPermission').mockReturnValue(false);
      vi.spyOn(authService, 'isTenantAdmin').mockReturnValue(false);
      vi.spyOn(authService, 'isPlatformAdmin').mockReturnValue(false);
      vi.spyOn(authService, 'hasRole').mockReturnValue(false);
      expect(component.canEditOrder()).toBe(false);

      vi.spyOn(authService, 'hasPermission').mockImplementation((p: string) => p === 'PURCHASE_ORDER.EDIT');
      expect(component.canEditOrder()).toBe(true);
    });
  });
});
