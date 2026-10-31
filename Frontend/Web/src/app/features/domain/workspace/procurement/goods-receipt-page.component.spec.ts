import { describe, it, expect, beforeEach, vi } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router, ActivatedRoute } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { of, throwError } from 'rxjs';
import { GoodsReceiptPageComponent } from './goods-receipt-page.component';
import { GoodsReceiptNoteService } from '../../../../core/services/goods-receipt-note.service';
import { PurchaseOrderService } from '../../../../core/services/purchase-order.service';
import { WorkspaceScopeService } from '../../../../core/services/workspace-scope.service';
import { AuthService } from '../../../../core/auth/auth.service';
import {
  GoodsReceiptNoteDto,
  GoodsReceiptNoteStatus
} from '../../../../core/models/goods-receipt-note.models';
import { PurchaseOrderDto, PurchaseOrderStatus } from '../../../../core/models/purchase-order.models';
import { Company, Branch } from '../../../../core/models/scope.models';

describe('GoodsReceiptPageComponent', () => {
  let component: GoodsReceiptPageComponent;
  let fixture: ComponentFixture<GoodsReceiptPageComponent>;
  let grnService: GoodsReceiptNoteService;
  let poService: PurchaseOrderService;
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
    name: 'Colombo Plant',
    code: 'CMB',
    isActive: true
  };

  const mockApprovedPo: PurchaseOrderDto = {
    id: 'po-101',
    tenantId: 't1',
    companyId: mockCompany.id,
    branchId: mockBranch.id,
    supplierId: 'sup-1',
    supplierName: 'Industrial Metals Ltd',
    orderNumber: 'PO-2026-101',
    orderDate: '2026-10-18T00:00:00Z',
    status: PurchaseOrderStatus.Approved,
    totalAmount: 500.0,
    isActive: true,
    items: [
      {
        id: 'poi-1',
        purchaseOrderId: 'po-101',
        productId: 'prod-1',
        productCode: 'RAW-001',
        productName: 'Steel Sheet 2mm',
        unitOfMeasureId: 'uom-kg',
        unitOfMeasureCode: 'KG',
        quantity: 20,
        receivedQuantity: 5, // 5 previously received
        unitPrice: 25.0,
        lineTotal: 500.0,
        isActive: true
      }
    ]
  };

  const mockSavedGrn: GoodsReceiptNoteDto = {
    goodsReceiptNoteId: 'grn-201',
    tenantId: 't1',
    companyId: mockCompany.id,
    branchId: mockBranch.id,
    purchaseOrderId: 'po-101',
    supplierId: 'sup-1',
    supplierName: 'Industrial Metals Ltd',
    grnNumber: 'GRN-2026-001',
    receiptDate: '2026-10-18T10:00:00Z',
    status: GoodsReceiptNoteStatus.Draft,
    deliveryNoteNumber: 'DN-9988',
    totalAmount: 375.0,
    lines: [
      {
        goodsReceiptNoteLineId: 'grnl-1',
        goodsReceiptNoteId: 'grn-201',
        purchaseOrderItemId: 'poi-1',
        productId: 'prod-1',
        productCode: 'RAW-001',
        productName: 'Steel Sheet 2mm',
        quantityReceived: 15,
        unitCost: 25.0, // Strictly read-only
        lineTotal: 375.0
      }
    ]
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [GoodsReceiptPageComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        GoodsReceiptNoteService,
        PurchaseOrderService,
        WorkspaceScopeService,
        AuthService,
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: {
                get: (key: string) => (key === 'id' ? null : null)
              },
              queryParamMap: {
                get: (key: string) => (key === 'purchaseOrderId' ? 'po-101' : null)
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

    fixture = TestBed.createComponent(GoodsReceiptPageComponent);
    component = fixture.componentInstance;
    grnService = TestBed.inject(GoodsReceiptNoteService);
    poService = TestBed.inject(PurchaseOrderService);
    scopeService = TestBed.inject(WorkspaceScopeService);
    router = TestBed.inject(Router);

    scopeService.companies.set([mockCompany]);
    scopeService.branches.set([mockBranch]);
    scopeService.selectedCompany.set(mockCompany);
    scopeService.selectedBranch.set(mockBranch);
    scopeService.isAllBranches.set(false);
  });

  it('should initialize and load approved PO, pre-populating outstanding quantities and locking prices', () => {
    vi.spyOn(poService, 'getOrder').mockReturnValue(of(mockApprovedPo));

    fixture.detectChanges();

    expect(component).toBeTruthy();
    expect(component.mode()).toBe('create');
    expect(component.sourcePo()?.orderNumber).toBe('PO-2026-101');
    expect(component.headerForm.get('purchaseOrderNumber')?.value).toBe('PO-2026-101');
    expect(component.headerForm.get('supplierName')?.value).toBe('Industrial Metals Ltd');

    // Working line assertions
    const lines = component.workingLines();
    expect(lines.length).toBe(1);
    expect(lines[0].orderedQuantity).toBe(20);
    expect(lines[0].previouslyReceivedQuantity).toBe(5);
    expect(lines[0].outstandingQuantity).toBe(15);
    expect(lines[0].receivedQuantityNow).toBe(15);
    expect(lines[0].unitOfMeasure).toBe('KG');
    expect(lines[0].unitOfMeasureId).toBe('uom-kg');
    expect(lines[0].unitCost).toBe(25.0); // PO unit price
    expect(lines[0].lineTotal).toBe(375.0);

    // Verify price column configuration is strictly read-only
    const cols = component.itemColumns();
    const unitPriceCol = cols.find(c => c.field === 'unitCost');
    const lineTotalCol = cols.find(c => c.field === 'lineTotal');
    const orderedQtyCol = cols.find(c => c.field === 'orderedQuantity');
    const prevRecCol = cols.find(c => c.field === 'previouslyReceivedQuantity');
    const outstandingCol = cols.find(c => c.field === 'outstandingQuantity');
    const receivedNowCol = cols.find(c => c.field === 'receivedQuantityNow');

    expect(unitPriceCol?.editable).toBe(false);
    expect(lineTotalCol?.editable).toBe(false);
    expect(orderedQtyCol?.editable).toBe(false);
    expect(prevRecCol?.editable).toBe(false);
    expect(outstandingCol?.editable).toBe(false);
    expect(receivedNowCol?.editable).toBe(true);
  });

  it('should reject unapproved PO and display an eligibility warning', () => {
    const draftPo = { ...mockApprovedPo, status: PurchaseOrderStatus.Draft };
    vi.spyOn(poService, 'getOrder').mockReturnValue(of(draftPo));

    fixture.detectChanges();

    expect(component.poIneligibleError()).toContain('Only Approved Purchase Orders can be received');
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('.ineligible-card')).toBeTruthy();
  });

  it('should calculate the GRN preview with the PO discount', () => {
    const discountedPo: PurchaseOrderDto = {
      ...mockApprovedPo,
      items: mockApprovedPo.items.map(item => ({
        ...item,
        discountRate: 10,
        subTotal: 500,
        discountAmount: 50
      }))
    };
    vi.spyOn(poService, 'getOrder').mockReturnValue(of(discountedPo));

    fixture.detectChanges();

    expect(component.workingLines()[0].lineTotal).toBe(337.5);
    component.workingLines()[0].receivedQuantityNow = 10;
    component.calculateRow(component.workingLines()[0]);
    expect(component.workingLines()[0].lineTotal).toBe(225);
  });

  it('should prevent over-receipt where receivedQuantityNow exceeds outstandingQuantity', () => {
    vi.spyOn(poService, 'getOrder').mockReturnValue(of(mockApprovedPo));
    fixture.detectChanges();

    // Line outstanding is 15. Attempting to receive 16:
    component.workingLines()[0].receivedQuantityNow = 16;
    component.saveReceipt(false);

    expect(component.message()?.severity).toBe('danger');
    expect(component.message()?.text).toContain('Over-receipt rejected');
  });

  it('should save draft GRN and navigate to view mode', () => {
    vi.spyOn(poService, 'getOrder').mockReturnValue(of(mockApprovedPo));
    const createSpy = vi.spyOn(grnService, 'createManualDraft').mockReturnValue(of(mockSavedGrn));
    const navSpy = vi.spyOn(router, 'navigate');

    fixture.detectChanges();

    // Partial receipt: receive 10 instead of 15
    component.workingLines()[0].receivedQuantityNow = 10;
    component.calculateRow(component.workingLines()[0]);

    component.saveReceipt(false);

    expect(createSpy).toHaveBeenCalledWith(
      mockCompany.id,
      mockBranch.id,
      expect.objectContaining({
        purchaseOrderId: 'po-101',
        lines: expect.arrayContaining([
          expect.objectContaining({
            purchaseOrderItemId: 'poi-1',
            productId: 'prod-1',
            quantityReceived: 10
          })
        ])
      })
    );
    expect(navSpy).toHaveBeenCalledWith(['/app/procurement/goods-receipts', 'grn-201']);
  });

  it('should save and confirm GRN when andConfirm flag is true', () => {
    vi.spyOn(poService, 'getOrder').mockReturnValue(of(mockApprovedPo));
    vi.spyOn(grnService, 'createManualDraft').mockReturnValue(of(mockSavedGrn));
    const confirmSpy = vi.spyOn(grnService, 'confirmGrn').mockReturnValue(
      of({ ...mockSavedGrn, status: GoodsReceiptNoteStatus.Confirmed })
    );
    const navSpy = vi.spyOn(router, 'navigate');

    fixture.detectChanges();

    component.saveReceipt(true);

    expect(confirmSpy).toHaveBeenCalledWith(mockCompany.id, mockBranch.id, 'grn-201');
    expect(navSpy).toHaveBeenCalledWith(['/app/procurement/goods-receipts', 'grn-201']);
  });

  it('should reopen saved GRN in view mode with all fields locked', () => {
    const route = TestBed.inject(ActivatedRoute);
    vi.spyOn(route.snapshot.paramMap, 'get').mockImplementation((k: string) => (k === 'id' ? 'grn-201' : null));
    vi.spyOn(route.snapshot.queryParamMap, 'get').mockReturnValue(null);
    vi.spyOn(grnService, 'getGrn').mockReturnValue(of(mockSavedGrn));
    vi.spyOn(poService, 'getOrder').mockReturnValue(of(mockApprovedPo));

    fixture.detectChanges();

    expect(component.mode()).toBe('view');
    expect(component.currentGrn()).toEqual(mockSavedGrn);
    expect(component.headerForm.get('grnNumber')?.value).toBe('GRN-2026-001');
    expect(component.grnStatusName()).toBe('Draft');
    expect(component.workingLines()[0].unitOfMeasure).toBe('KG');
    expect(component.workingLines()[0].unitOfMeasureId).toBe('uom-kg');

    // All columns in view mode must be read-only
    const cols = component.itemColumns();
    const receivedNowCol = cols.find(c => c.field === 'receivedQuantityNow');
    expect(receivedNowCol?.editable).toBe(false);
  });

  it('should edit and save an existing draft using the update endpoint', () => {
    const route = TestBed.inject(ActivatedRoute);
    vi.spyOn(route.snapshot.paramMap, 'get').mockImplementation((k: string) => (k === 'id' ? 'grn-201' : null));
    vi.spyOn(route.snapshot.queryParamMap, 'get').mockReturnValue(null);
    vi.spyOn(grnService, 'getGrn').mockReturnValue(of(mockSavedGrn));
    vi.spyOn(poService, 'getOrder').mockReturnValue(of(mockApprovedPo));
    const updateSpy = vi.spyOn(grnService, 'updateDraft').mockReturnValue(of(mockSavedGrn));

    fixture.detectChanges();
    component.editCurrentDraft();
    component.workingLines()[0].receivedQuantityNow = 12;
    component.calculateRow(component.workingLines()[0]);
    component.saveReceipt(false);

    expect(updateSpy).toHaveBeenCalledWith(
      mockCompany.id,
      mockBranch.id,
      'grn-201',
      expect.objectContaining({
        lines: expect.arrayContaining([
          expect.objectContaining({
            purchaseOrderItemId: 'poi-1',
            quantityReceived: 12
          })
        ])
      })
    );
    expect(component.editingDraft()).toBe(false);
  });

  it('should render AccessDeniedComponent when PO fetch returns 403 Forbidden', () => {
    const forbiddenError = {
      status: 403,
      statusText: 'Forbidden',
      message: 'Http failure response for http://localhost:4200/api/companies/c1/branches/b1/purchase-orders/po-101: 403 Forbidden'
    };
    vi.spyOn(poService, 'getOrder').mockReturnValue(throwError(() => forbiddenError));

    fixture.detectChanges();

    expect(component.isAccessDenied()).toBe(true);
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('.grn-access-denied-wrapper')).toBeTruthy();
    expect(compiled.textContent).not.toContain('http://localhost:4200');
  });
});
