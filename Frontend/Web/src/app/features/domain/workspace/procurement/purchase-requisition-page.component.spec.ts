import { describe, it, expect, beforeEach, vi } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router, ActivatedRoute, UrlSegment } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { of, throwError } from 'rxjs';
import { PurchaseRequisitionPageComponent } from './purchase-requisition-page.component';
import { PurchaseRequisitionService } from '../../../../core/services/purchase-requisition.service';
import { PurchaseOrderService } from '../../../../core/services/purchase-order.service';
import { MasterDataService } from '../../../../core/services/master-data.service';
import { WorkspaceScopeService } from '../../../../core/services/workspace-scope.service';
import { AuthService } from '../../../../core/auth/auth.service';
import {
  PurchaseRequisitionDto,
  PurchaseRequisitionStatus
} from '../../../../core/models/purchase-requisition.models';
import { Company, Branch, ProductItem, UnitOfMeasureItem, SupplierItem } from '../../../../core/models/scope.models';

describe('PurchaseRequisitionPageComponent', () => {
  let component: PurchaseRequisitionPageComponent;
  let fixture: ComponentFixture<PurchaseRequisitionPageComponent>;
  let prService: PurchaseRequisitionService;
  let poService: PurchaseOrderService;
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
    name: 'Colombo Plant',
    code: 'CMB',
    isActive: true
  };

  const mockProducts: ProductItem[] = [
    {
      productId: 'p1',
      tenantId: 't1',
      productCode: 'RAW-001',
      productName: 'Steel Sheet 2mm',
      categoryId: 'cat-1',
      unitOfMeasure: 'PCS',
      unitOfMeasureId: 'uom-1',
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
      sellingPrice: 30.0,
      reorderLevel: 10,
      reorderQuantity: 50,
      isActive: true
    }
  ];

  const mockUoms: UnitOfMeasureItem[] = [
    {
      unitOfMeasureId: 'uom-1',
      tenantId: 't1',
      code: 'PCS',
      name: 'Pieces',
      description: 'Individual units',
      isActive: true
    }
  ];

  const mockSuppliers: SupplierItem[] = [
    {
      supplierId: 'sup-1',
      supplierCode: 'SUP-001',
      supplierName: 'Global Steel Ltd',
      tenantId: 't1',
      isActive: true
    }
  ];

  const mockRequisition: PurchaseRequisitionDto = {
    id: 'pr-100',
    tenantId: 't1',
    companyId: mockCompany.id,
    branchId: mockBranch.id,
    requisitionNumber: 'PR-2026-0001',
    requisitionDate: '2026-10-18T00:00:00Z',
    requiredDate: '2026-10-25T00:00:00Z',
    requestorUserId: 'u1',
    status: PurchaseRequisitionStatus.Draft,
    totalAmount: 200.0,
    notes: 'Urgent stock for fabrication',
    isActive: true,
    items: [
      {
        id: 'item-1',
        purchaseRequisitionId: 'pr-100',
        productId: 'p1',
        productCode: 'RAW-001',
        productName: 'Steel Sheet 2mm',
        unitOfMeasureId: 'uom-1',
        unitOfMeasureCode: 'PCS',
        quantity: 10,
        estimatedUnitPrice: 20.0,
        estimatedLineTotal: 200.0,
        remarks: 'Batch delivery needed',
        isActive: true
      }
    ]
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [PurchaseRequisitionPageComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        PurchaseRequisitionService,
        PurchaseOrderService,
        MasterDataService,
        WorkspaceScopeService,
        AuthService,
        {
          provide: ActivatedRoute,
          useValue: {
            url: of([new UrlSegment('new', {})]),
            snapshot: { params: {} }
          }
        }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(PurchaseRequisitionPageComponent);
    component = fixture.componentInstance;
    prService = TestBed.inject(PurchaseRequisitionService);
    poService = TestBed.inject(PurchaseOrderService);
    masterDataService = TestBed.inject(MasterDataService);
    scopeService = TestBed.inject(WorkspaceScopeService);
    authService = TestBed.inject(AuthService);
    router = TestBed.inject(Router);

    vi.spyOn(scopeService, 'selectedCompany').mockReturnValue(mockCompany);
    vi.spyOn(scopeService, 'selectedBranch').mockReturnValue(mockBranch);
    vi.spyOn(scopeService, 'hasCompany').mockReturnValue(true);
    vi.spyOn(scopeService, 'companies').mockReturnValue([mockCompany]);
    vi.spyOn(scopeService, 'branches').mockReturnValue([mockBranch]);
    vi.spyOn(authService, 'hasPermission').mockReturnValue(true);
    vi.spyOn(authService, 'currentUser').mockReturnValue({
      userId: 'manager-user-99',
      username: 'Manager',
      email: 'manager@apex.com',
      roles: ['DEPARTMENT_MANAGER'],
      permissions: ['PURCHASE_REQUISITION.APPROVE'],
      tenantId: 't1',
      token: 'jwt'
    });

    vi.spyOn(masterDataService, 'getProducts').mockReturnValue(of(mockProducts));
    vi.spyOn(masterDataService, 'getUnitsOfMeasure').mockReturnValue(of(mockUoms));
    vi.spyOn(masterDataService, 'getSuppliers').mockReturnValue(of(mockSuppliers));
    vi.spyOn(prService, 'getRequisition').mockReturnValue(of(mockRequisition));
    vi.spyOn(prService, 'getApprovalHistory').mockReturnValue(of([]));
    vi.spyOn(poService, 'getOrders').mockReturnValue(of([]));
  });

  it('should initialize in create mode', () => {
    fixture.detectChanges();
    expect(component.mode()).toBe('create');
    expect(component.headerForm.controls.requisitionNumber.value).toMatch(/^PR-\d{4}-\d{4}$/);
  });

  it('should calculate row line totals correctly', () => {
    const line = {
      lineId: '1',
      productId: 'p1',
      productCode: 'RAW-001',
      productName: 'Steel',
      quantity: 5,
      estimatedUnitPrice: 25.5,
      estimatedLineTotal: 0
    };
    component.calculateRow(line);
    expect(line.estimatedLineTotal).toBe(127.5);
  });

  it('should populate form and lines when loading existing requisition', () => {
    component.loadExistingRequisition('pr-100');
    expect(component.currentRequisition()).toEqual(mockRequisition);
    expect(component.headerForm.controls.requisitionNumber.value).toBe('PR-2026-0001');
    expect(component.workingLines().length).toBe(1);
    expect(component.totalAmount()).toBe(200.0);
  });

  it('should save requisition as draft in create mode', () => {
    fixture.detectChanges();
    const createSpy = vi.spyOn(prService, 'createRequisition').mockReturnValue(of(mockRequisition));
    const navSpy = vi.spyOn(router, 'navigate');

    component.workingLines.set([
      {
        lineId: 'l1',
        productId: 'p1',
        productCode: 'RAW-001',
        productName: 'Steel Sheet 2mm',
        unitOfMeasureId: 'uom-1',
        unitOfMeasureCode: 'PCS',
        quantity: 10,
        estimatedUnitPrice: 20.0,
        estimatedLineTotal: 200.0
      }
    ]);

    component.saveRequisition(false);
    expect(createSpy).toHaveBeenCalled();
    expect(navSpy).toHaveBeenCalledWith(['/app/procurement/purchase-requisitions', 'pr-100']);
  });

  it('should allow creating and submitting with ONLY mandatory fields (Item, Quantity, UOM)', () => {
    fixture.detectChanges();
    const createSpy = vi.spyOn(prService, 'createRequisition').mockReturnValue(of(mockRequisition));
    const submitSpy = vi.spyOn(prService, 'submitRequisition').mockReturnValue(of({
      ...mockRequisition,
      status: PurchaseRequisitionStatus.Submitted
    }));
    const navSpy = vi.spyOn(router, 'navigate');

    // Only mandatory line details: product, quantity, UOM; price = 0, no remarks, no optional supplier or notes
    component.headerForm.controls.notes.setValue('');
    component.headerForm.controls.requiredDate.setValue('');
    component.headerForm.controls.preferredSupplierId.setValue('');

    component.workingLines.set([
      {
        lineId: 'l1',
        productId: 'p1',
        productCode: 'RAW-001',
        productName: 'Steel Sheet 2mm',
        unitOfMeasureId: 'uom-1',
        unitOfMeasureCode: 'PCS',
        quantity: 50,
        estimatedUnitPrice: 0,
        estimatedLineTotal: 0
      }
    ]);

    component.saveRequisition(true);
    expect(createSpy).toHaveBeenCalledWith(
      mockCompany.id,
      mockBranch.id,
      expect.objectContaining({
        items: [
          expect.objectContaining({
            productId: 'p1',
            quantity: 50,
            unitOfMeasureId: 'uom-1'
          })
        ]
      })
    );
    expect(submitSpy).toHaveBeenCalledWith(mockCompany.id, mockBranch.id, 'pr-100');
    expect(navSpy).toHaveBeenCalledWith(['/app/procurement/purchase-requisitions', 'pr-100']);
  });

  it('should enforce segregation of duties: submitter cannot approve own requisition', () => {
    // Current user is u1 (the requestor)
    vi.spyOn(authService, 'currentUser').mockReturnValue({
      userId: 'u1',
      username: 'Requester User',
      email: 'requester@apex.com',
      roles: ['DEPARTMENT_REQUESTER', 'DEPARTMENT_MANAGER'],
      permissions: ['PURCHASE_REQUISITION.APPROVE'],
      tenantId: 't1',
      token: 'jwt'
    });

    component.currentRequisition.set({
      ...mockRequisition,
      requestorUserId: 'u1',
      status: PurchaseRequisitionStatus.Submitted
    });

    expect(component.canApprove()).toBe(false);
    expect(component.isSelfApprovalRestricted()).toBe(true);

    // Another manager can approve
    vi.spyOn(authService, 'currentUser').mockReturnValue({
      userId: 'different-manager-id',
      username: 'Independent Manager',
      email: 'manager2@apex.com',
      roles: ['DEPARTMENT_MANAGER'],
      permissions: ['PURCHASE_REQUISITION.APPROVE'],
      tenantId: 't1',
      token: 'jwt'
    });
    component.currentRequisition.set({
      ...mockRequisition,
      requestorUserId: 'u1',
      status: PurchaseRequisitionStatus.Submitted
    });

    expect(component.canApprove()).toBe(true);
    expect(component.isSelfApprovalRestricted()).toBe(false);
  });

  it('should enable Create Purchase Order action on approved requisition with remaining quantity', () => {
    vi.spyOn(authService, 'hasPermission').mockReturnValue(true);

    component.currentRequisition.set({
      ...mockRequisition,
      status: PurchaseRequisitionStatus.Approved
    });

    component.workingLines.set([
      {
        lineId: 'l1',
        id: 'item-1',
        productId: 'p1',
        productCode: 'RAW-001',
        productName: 'Steel Sheet 2mm',
        unitOfMeasureId: 'uom-1',
        quantity: 100,
        estimatedUnitPrice: 20.0,
        estimatedLineTotal: 2000.0
      }
    ]);

    // 40 units ordered, 60 remaining
    component.lineFulfillments.set(
      new Map([['item-1', { ordered: 40, remaining: 60 }]])
    );

    expect(component.totalRemainingQuantity()).toBe(60);
    expect(component.totalOrderedQuantity()).toBe(40);
    expect(component.canCreatePurchaseOrder()).toBe(true);
    expect(component.isFullyOrdered()).toBe(false);

    const navSpy = vi.spyOn(router, 'navigate');
    component.navigateToCreatePurchaseOrder();
    expect(navSpy).toHaveBeenCalledWith(['/app/procurement/purchase-orders/new'], {
      queryParams: { purchaseRequisitionId: 'pr-100' }
    });
  });

  it('should disable Create Purchase Order action when requisition is fully ordered', () => {
    component.currentRequisition.set({
      ...mockRequisition,
      status: PurchaseRequisitionStatus.Approved
    });

    component.workingLines.set([
      {
        lineId: 'l1',
        id: 'item-1',
        productId: 'p1',
        productCode: 'RAW-001',
        productName: 'Steel Sheet 2mm',
        unitOfMeasureId: 'uom-1',
        quantity: 100,
        estimatedUnitPrice: 20.0,
        estimatedLineTotal: 2000.0
      }
    ]);

    // 100 units ordered, 0 remaining
    component.lineFulfillments.set(
      new Map([['item-1', { ordered: 100, remaining: 0 }]])
    );

    expect(component.totalRemainingQuantity()).toBe(0);
    expect(component.totalOrderedQuantity()).toBe(100);
    expect(component.canCreatePurchaseOrder()).toBe(false);
    expect(component.isFullyOrdered()).toBe(true);
  });

  it('should approve requisition from decision modal', () => {
    component.currentRequisition.set({
      ...mockRequisition,
      status: PurchaseRequisitionStatus.Submitted
    });
    const approveSpy = vi.spyOn(prService, 'approveRequisition').mockReturnValue(of({
      ...mockRequisition,
      status: PurchaseRequisitionStatus.Approved
    }));

    component.openDecision('approve');
    component.decisionRemarks.set('Approved as requested');
    component.executeDecision();

    expect(approveSpy).toHaveBeenCalledWith(
      mockCompany.id,
      mockBranch.id,
      'pr-100',
      'Approved as requested'
    );
    expect(component.decisionAction()).toBeNull();
  });

  it('should reject requisition with required remarks', () => {
    component.currentRequisition.set({
      ...mockRequisition,
      status: PurchaseRequisitionStatus.Submitted
    });
    const rejectSpy = vi.spyOn(prService, 'rejectRequisition').mockReturnValue(of({
      ...mockRequisition,
      status: PurchaseRequisitionStatus.Rejected
    }));

    component.openDecision('reject');
    component.executeDecision();
    expect(component.decisionError()).toBe('Remarks are required when rejecting a requisition.');
    expect(rejectSpy).not.toHaveBeenCalled();

    component.decisionRemarks.set('Exceeds current quarterly budget');
    component.executeDecision();
    expect(rejectSpy).toHaveBeenCalledWith(
      mockCompany.id,
      mockBranch.id,
      'pr-100',
      'Exceeds current quarterly budget'
    );
  });
});
