import { describe, it, expect, beforeEach, vi } from 'vitest';
import { TestBed, getTestBed } from '@angular/core/testing';
import {
  BrowserTestingModule,
  platformBrowserTesting
} from '@angular/platform-browser/testing';
import { provideRouter, Router, ActivatedRoute } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { of, throwError } from 'rxjs';

if (typeof (globalThis as any).document === 'undefined') {
  (globalThis as any).document = {
    createElement: () => ({ style: {} }),
    querySelector: () => null,
    querySelectorAll: () => [],
    body: {
      querySelector: () => null,
      querySelectorAll: () => [],
      appendChild: () => {},
      removeChild: () => {}
    },
    location: { href: 'http://localhost' }
  };
  (globalThis as any).window = globalThis;
}

try {
  getTestBed().initTestEnvironment(
    BrowserTestingModule,
    platformBrowserTesting()
  );
} catch {
  // Already initialized in test environment
}

import { GoodsReceiptPageComponent } from './goods-receipt-page.component';
import { GoodsReceiptNoteService } from '../../../../core/services/goods-receipt-note.service';
import { PurchaseOrderService } from '../../../../core/services/purchase-order.service';
import { MasterDataService } from '../../../../core/services/master-data.service';
import { WorkspaceScopeService } from '../../../../core/services/workspace-scope.service';
import { AuthService } from '../../../../core/auth/auth.service';
import {
  GoodsReceiptNoteDto,
  GoodsReceiptNoteStatus
} from '../../../../core/models/goods-receipt-note.models';
import { PurchaseOrderDto, PurchaseOrderStatus } from '../../../../core/models/purchase-order.models';
import { Company, Branch, ProductItem } from '../../../../core/models/scope.models';

describe('GoodsReceiptPageComponent', () => {
  let component: GoodsReceiptPageComponent;
  let grnService: GoodsReceiptNoteService;
  let poService: PurchaseOrderService;
  let masterDataService: MasterDataService;
  let scopeService: WorkspaceScopeService;
  let router: Router;

  const mockProducts: ProductItem[] = [
    {
      productId: 'prod-1',
      tenantId: 't1',
      categoryId: 'cat-1',
      productCode: 'RAW-001',
      productName: 'Steel Sheet 2mm',
      unitOfMeasure: 'KG',
      unitOfMeasureId: 'uom-kg',
      isActive: true,
      productType: 1,
      trackingMode: 0,
      valuationMethod: 1,
      standardCost: 25.0,
      sellingPrice: 35.0,
      reorderLevel: 10,
      reorderQuantity: 50,
      isPurchasable: true,
      isSellable: true,
      isManufacturable: false,
      canConsumeInProduction: true,
      canConsumeInMaintenance: false,
      isStockTracked: true
    },
    {
      productId: 'prod-2',
      tenantId: 't1',
      categoryId: 'cat-1',
      productCode: 'RET-001',
      productName: 'Samba Rice 5kg Pack',
      unitOfMeasure: 'BAG',
      unitOfMeasureId: 'uom-bag',
      isActive: true,
      productType: 1,
      trackingMode: 0,
      valuationMethod: 1,
      standardCost: 15.0,
      sellingPrice: 20.0,
      reorderLevel: 5,
      reorderQuantity: 20,
      isPurchasable: true,
      isSellable: true,
      isManufacturable: false,
      canConsumeInProduction: false,
      canConsumeInMaintenance: false,
      isStockTracked: true
    }
  ];

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

  beforeEach(() => {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        GoodsReceiptNoteService,
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
    });

    grnService = TestBed.inject(GoodsReceiptNoteService);
    poService = TestBed.inject(PurchaseOrderService);
    masterDataService = TestBed.inject(MasterDataService);
    scopeService = TestBed.inject(WorkspaceScopeService);
    router = TestBed.inject(Router);

    component = TestBed.runInInjectionContext(() => new GoodsReceiptPageComponent());

    vi.spyOn(masterDataService, 'getProducts').mockReturnValue(of(mockProducts));

    scopeService.companies.set([mockCompany]);
    scopeService.branches.set([mockBranch]);
    scopeService.selectedCompany.set(mockCompany);
    scopeService.selectedBranch.set(mockBranch);
    scopeService.isAllBranches.set(false);
  });

  it('should initialize and load approved PO, pre-populating outstanding quantities and locking prices', () => {
    vi.spyOn(poService, 'getOrder').mockReturnValue(of(mockApprovedPo));

    component.ngOnInit();

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

    component.ngOnInit();

    expect(component.poIneligibleError()).toContain('Only Approved Purchase Orders can be received');
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

    component.ngOnInit();

    expect(component.workingLines()[0].lineTotal).toBe(337.5);
    component.workingLines()[0].receivedQuantityNow = 10;
    component.calculateRow(component.workingLines()[0]);
    expect(component.workingLines()[0].lineTotal).toBe(225);
  });

  it('should prevent over-receipt where receivedQuantityNow exceeds outstandingQuantity', () => {
    vi.spyOn(poService, 'getOrder').mockReturnValue(of(mockApprovedPo));
    component.ngOnInit();

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

    component.ngOnInit();

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

    component.ngOnInit();

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

    component.ngOnInit();

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

    component.ngOnInit();
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

    component.ngOnInit();

    expect(component.isAccessDenied()).toBe(true);
  });

  describe('Batch 2: Defect 1 - Independent PO Line Item Population', () => {
    it('should populate all lines when a PO contains multiple different products', () => {
      const multiProductPo: PurchaseOrderDto = {
        ...mockApprovedPo,
        items: [
          mockApprovedPo.items[0],
          {
            id: 'poi-2',
            purchaseOrderId: 'po-101',
            productId: 'prod-2',
            productCode: 'RET-001',
            productName: 'Samba Rice 5kg Pack',
            unitOfMeasureId: 'uom-bag',
            unitOfMeasureCode: 'BAG',
            quantity: 30,
            receivedQuantity: 10,
            unitPrice: 15.0,
            lineTotal: 450.0,
            isActive: true
          }
        ]
      };
      vi.spyOn(poService, 'getOrder').mockReturnValue(of(multiProductPo));

      component.ngOnInit();

      const lines = component.workingLines();
      expect(lines.length).toBe(2);
      expect(lines[0].productId).toBe('prod-1');
      expect(lines[0].outstandingQuantity).toBe(15);
      expect(lines[1].productId).toBe('prod-2');
      expect(lines[1].orderedQuantity).toBe(30);
      expect(lines[1].previouslyReceivedQuantity).toBe(10);
      expect(lines[1].outstandingQuantity).toBe(20);
      expect(lines[1].receivedQuantityNow).toBe(20);
      expect(lines[1].unitOfMeasure).toBe('BAG');
    });

    it('should retain separate lines with independent identity when a PO contains duplicate product IDs', () => {
      const duplicateProductPo: PurchaseOrderDto = {
        ...mockApprovedPo,
        items: [
          {
            id: 'poi-dup-1',
            purchaseOrderId: 'po-101',
            productId: 'prod-1',
            productCode: 'RAW-001',
            productName: 'Steel Sheet 2mm',
            unitOfMeasureId: 'uom-kg',
            unitOfMeasureCode: 'KG',
            quantity: 50,
            receivedQuantity: 10,
            unitPrice: 25.0,
            lineTotal: 1250.0,
            isActive: true
          },
          {
            id: 'poi-dup-2',
            purchaseOrderId: 'po-101',
            productId: 'prod-1',
            productCode: 'RAW-001',
            productName: 'Steel Sheet 2mm',
            unitOfMeasureId: 'uom-kg',
            unitOfMeasureCode: 'KG',
            quantity: 30,
            receivedQuantity: 0,
            unitPrice: 25.0,
            lineTotal: 750.0,
            isActive: true
          }
        ]
      };
      vi.spyOn(poService, 'getOrder').mockReturnValue(of(duplicateProductPo));

      component.ngOnInit();

      const lines = component.workingLines();
      expect(lines.length).toBe(2);

      // Verify Line 1
      expect(lines[0].purchaseOrderItemId).toBe('poi-dup-1');
      expect(lines[0].productId).toBe('prod-1');
      expect(lines[0].orderedQuantity).toBe(50);
      expect(lines[0].previouslyReceivedQuantity).toBe(10);
      expect(lines[0].outstandingQuantity).toBe(40);
      expect(lines[0].receivedQuantityNow).toBe(40);

      // Verify Line 2 is completely independent
      expect(lines[1].purchaseOrderItemId).toBe('poi-dup-2');
      expect(lines[1].productId).toBe('prod-1');
      expect(lines[1].orderedQuantity).toBe(30);
      expect(lines[1].previouslyReceivedQuantity).toBe(0);
      expect(lines[1].outstandingQuantity).toBe(30);
      expect(lines[1].receivedQuantityNow).toBe(30);
    });

    it('should ensure outstanding quantity is never negative even if server receivedQuantity exceeds quantity', () => {
      const overReceivedOnServerPo: PurchaseOrderDto = {
        ...mockApprovedPo,
        items: [
          {
            id: 'poi-over',
            purchaseOrderId: 'po-101',
            productId: 'prod-1',
            quantity: 10,
            receivedQuantity: 15, // Server data anomaly
            unitPrice: 25.0,
            lineTotal: 250.0,
            isActive: true
          }
        ]
      };
      vi.spyOn(poService, 'getOrder').mockReturnValue(of(overReceivedOnServerPo));

      component.ngOnInit();

      const lines = component.workingLines();
      expect(lines.length).toBe(1);
      expect(lines[0].outstandingQuantity).toBe(0);
      expect(lines[0].receivedQuantityNow).toBe(0);
    });
  });

  describe('Batch 2: Defect 2 - Product Code and Name Display & Enrichment', () => {
    it('should enrich productCode, productName, and unitOfMeasure from product catalogue when PO item details are generic', () => {
      const poWithGenericItems: PurchaseOrderDto = {
        ...mockApprovedPo,
        items: [
          {
            id: 'poi-gen-1',
            purchaseOrderId: 'po-101',
            productId: 'prod-2',
            productCode: '', // Missing
            productName: 'Material Item', // Generic
            unitOfMeasureCode: '', // Missing
            quantity: 15,
            receivedQuantity: 0,
            unitPrice: 20.0,
            lineTotal: 300.0,
            isActive: true
          }
        ]
      };
      vi.spyOn(poService, 'getOrder').mockReturnValue(of(poWithGenericItems));

      component.ngOnInit();

      const lines = component.workingLines();
      expect(lines.length).toBe(1);
      expect(lines[0].productCode).toBe('RET-001');
      expect(lines[0].productName).toBe('Samba Rice 5kg Pack');
      expect(lines[0].unitOfMeasure).toBe('BAG');
    });

    it('should preserve valid custom PO-provided product details and not overwrite them with standard catalogue names', () => {
      const poWithCustomDetails: PurchaseOrderDto = {
        ...mockApprovedPo,
        items: [
          {
            id: 'poi-custom-1',
            purchaseOrderId: 'po-101',
            productId: 'prod-1',
            productCode: 'CUSTOM-STEEL-01',
            productName: 'Custom Cut Steel Plate 2mm',
            unitOfMeasureCode: 'SHEET',
            quantity: 10,
            receivedQuantity: 0,
            unitPrice: 30.0,
            lineTotal: 300.0,
            isActive: true
          }
        ]
      };
      vi.spyOn(poService, 'getOrder').mockReturnValue(of(poWithCustomDetails));

      component.ngOnInit();

      const lines = component.workingLines();
      expect(lines.length).toBe(1);
      expect(lines[0].productCode).toBe('CUSTOM-STEEL-01');
      expect(lines[0].productName).toBe('Custom Cut Steel Plate 2mm');
      expect(lines[0].unitOfMeasure).toBe('SHEET');
    });

    it('should provide clear fallback when a product is not found in the catalogue', () => {
      const poWithUnknownProduct: PurchaseOrderDto = {
        ...mockApprovedPo,
        items: [
          {
            id: 'poi-unknown-1',
            purchaseOrderId: 'po-101',
            productId: 'unknown-uuid-999',
            productCode: '',
            productName: '',
            quantity: 5,
            receivedQuantity: 0,
            unitPrice: 50.0,
            lineTotal: 250.0,
            isActive: true
          }
        ]
      };
      vi.spyOn(poService, 'getOrder').mockReturnValue(of(poWithUnknownProduct));

      component.ngOnInit();

      const lines = component.workingLines();
      expect(lines.length).toBe(1);
      expect(lines[0].productCode).toBe('unknown-uuid-999');
      expect(lines[0].productName).toBe('Product (unknown-uuid-999)');
    });

    it('should reactively enrich lines when product master data resolves after PO lines load', () => {
      const poWithEmptyDetails: PurchaseOrderDto = {
        ...mockApprovedPo,
        items: [
          {
            id: 'poi-async-1',
            purchaseOrderId: 'po-101',
            productId: 'prod-2',
            productCode: 'ITEM',
            productName: 'Material Item',
            quantity: 10,
            receivedQuantity: 0,
            unitPrice: 15.0,
            lineTotal: 150.0,
            isActive: true
          }
        ]
      };
      vi.spyOn(poService, 'getOrder').mockReturnValue(of(poWithEmptyDetails));
      vi.spyOn(masterDataService, 'getProducts').mockReturnValue(of([])); // Initially empty

      component.ngOnInit();

      // Trigger asynchronous arrival of catalogue products
      component['enrichWorkingLines'](mockProducts);

      const lines = component.workingLines();
      expect(lines[0].productCode).toBe('RET-001');
      expect(lines[0].productName).toBe('Samba Rice 5kg Pack');
      expect(lines[0].unitOfMeasure).toBe('BAG');
    });

    it('should prevent stale asynchronous catalogue response from overwriting a newer loaded PO', () => {
      const po1: PurchaseOrderDto = { ...mockApprovedPo, id: 'po-1', orderNumber: 'PO-1' };
      const po2: PurchaseOrderDto = {
        ...mockApprovedPo,
        id: 'po-2',
        orderNumber: 'PO-2',
        items: [
          {
            id: 'poi-p2',
            purchaseOrderId: 'po-2',
            productId: 'prod-2',
            productCode: 'RET-001',
            productName: 'Samba Rice 5kg Pack',
            quantity: 5,
            receivedQuantity: 0,
            unitPrice: 15.0,
            lineTotal: 75.0,
            isActive: true
          }
        ]
      };

      vi.spyOn(poService, 'getOrder').mockReturnValue(of(po1));
      component.ngOnInit();

      // Switch to po2
      component.purchaseOrderId.set('po-2');
      vi.spyOn(poService, 'getOrder').mockReturnValue(of(po2));
      component.loadSourcePurchaseOrder('po-2');

      expect(component.sourcePo()?.id).toBe('po-2');
      expect(component.workingLines()[0].purchaseOrderItemId).toBe('poi-p2');
    });
  });

  describe('Batch 2: Defect 3 - Fully Received PO History & Protection', () => {
    it('should retain complete line-item history in read-only mode when PO is fully received', () => {
      const fullyReceivedPo: PurchaseOrderDto = {
        ...mockApprovedPo,
        orderNumber: 'PO-2026-FULL',
        items: [
          {
            id: 'poi-full-1',
            purchaseOrderId: 'po-101',
            productId: 'prod-1',
            productCode: 'RAW-001',
            productName: 'Steel Sheet 2mm',
            unitOfMeasureId: 'uom-kg',
            unitOfMeasureCode: 'KG',
            quantity: 20,
            receivedQuantity: 20, // Fully received
            unitPrice: 25.0,
            lineTotal: 500.0,
            isActive: true
          }
        ]
      };
      vi.spyOn(poService, 'getOrder').mockReturnValue(of(fullyReceivedPo));

      component.ngOnInit();

      // Ineligible error must NOT be set
      expect(component.poIneligibleError()).toBeNull();
      expect(component.isFullyReceived()).toBe(true);
      expect(component.canEditDraft()).toBe(false);

      // Lines must be visible
      const lines = component.workingLines();
      expect(lines.length).toBe(1);
      expect(lines[0].orderedQuantity).toBe(20);
      expect(lines[0].previouslyReceivedQuantity).toBe(20);
      expect(lines[0].outstandingQuantity).toBe(0);
      expect(lines[0].receivedQuantityNow).toBe(0);

      // Verify fully received state and read-only protection
      expect(component.isFullyReceived()).toBe(true);
      expect(component.canEditDraft()).toBe(false);
      const fields = component.headerFormFields();
      expect(fields.find(f => f.name === 'deliveryNoteNumber')?.disabled).toBe(true);
      expect(fields.find(f => f.name === 'remarks')?.disabled).toBe(true);

      // Columns must be read-only
      const cols = component.itemColumns();
      const receivedNowCol = cols.find(c => c.field === 'receivedQuantityNow');
      expect(receivedNowCol?.editable).toBe(false);
    });

    it('should reject receipt saving when PO is fully received', () => {
      const fullyReceivedPo: PurchaseOrderDto = {
        ...mockApprovedPo,
        items: [
          {
            id: 'poi-full-1',
            purchaseOrderId: 'po-101',
            productId: 'prod-1',
            quantity: 20,
            receivedQuantity: 20,
            unitPrice: 25.0,
            lineTotal: 500.0,
            isActive: true
          }
        ]
      };
      vi.spyOn(poService, 'getOrder').mockReturnValue(of(fullyReceivedPo));
      const createSpy = vi.spyOn(grnService, 'createManualDraft');

      component.ngOnInit();

      component.saveReceipt(false);

      expect(createSpy).not.toHaveBeenCalled();
      expect(component.message()?.severity).toBe('danger');
      expect(component.message()?.text).toContain('All items on Purchase Order');
    });

    it('should keep partially received PO eligible for receipt while completed lines are non-receivable', () => {
      const partiallyReceivedPo: PurchaseOrderDto = {
        ...mockApprovedPo,
        items: [
          {
            id: 'poi-done',
            purchaseOrderId: 'po-101',
            productId: 'prod-1',
            productCode: 'RAW-001',
            productName: 'Steel Sheet 2mm',
            quantity: 10,
            receivedQuantity: 10, // Line 1 completed
            unitPrice: 25.0,
            lineTotal: 250.0,
            isActive: true
          },
          {
            id: 'poi-pending',
            purchaseOrderId: 'po-101',
            productId: 'prod-2',
            productCode: 'RET-001',
            productName: 'Samba Rice 5kg Pack',
            quantity: 10,
            receivedQuantity: 4, // Line 2 has 6 outstanding
            unitPrice: 15.0,
            lineTotal: 150.0,
            isActive: true
          }
        ]
      };
      vi.spyOn(poService, 'getOrder').mockReturnValue(of(partiallyReceivedPo));

      component.ngOnInit();

      expect(component.isFullyReceived()).toBe(false);
      expect(component.canEditDraft()).toBe(true);

      const lines = component.workingLines();
      expect(lines.length).toBe(2);
      expect(lines[0].outstandingQuantity).toBe(0);
      expect(lines[0].receivedQuantityNow).toBe(0);
      expect(lines[1].outstandingQuantity).toBe(6);
      expect(lines[1].receivedQuantityNow).toBe(6);

      // Line 1 validator rejects over-receipt
      const cols = component.itemColumns();
      const receivedNowCol = cols.find(c => c.field === 'receivedQuantityNow');
      const validator = receivedNowCol?.validators?.[0];
      expect(validator?.(1, lines[0])).toBe('Cannot exceed outstanding (0)');
      expect(validator?.(5, lines[1])).toBeNull();
    });
  });
});
