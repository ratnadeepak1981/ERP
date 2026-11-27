import { describe, it, expect, beforeEach, vi } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { InventoryReplenishmentComponent } from './inventory-replenishment.component';
import { InventoryService } from '../../../../../core/services/inventory.service';
import { MasterDataService } from '../../../../../core/services/master-data.service';
import { WorkspaceScopeService } from '../../../../../core/services/workspace-scope.service';
import { AuthService } from '../../../../../core/auth/auth.service';
import { of, throwError } from 'rxjs';
import { Router } from '@angular/router';
import {
  ReplenishmentAssessmentResult,
  DraftPurchaseRequisitionResult
} from '../../../../../core/models/inventory.models';

describe('InventoryReplenishmentComponent', () => {
  let component: InventoryReplenishmentComponent;
  let fixture: ComponentFixture<InventoryReplenishmentComponent>;
  let mockInventoryService: any;
  let mockMasterDataService: any;
  let mockScopeService: any;
  let mockAuthService: any;
  let mockRouter: any;

  const mockWarehouse = {
    warehouseId: 'wh-1',
    warehouseCode: 'WH-MAIN',
    warehouseName: 'Main Facility',
    companyId: 'comp-1',
    isActive: true
  };

  const mockProduct = {
    productId: 'prod-1',
    productCode: 'ITEM-1',
    productName: 'Item One',
    unitOfMeasure: 'PCS',
    unitOfMeasureId: 'uom-1',
    isActive: true,
    isStockTracked: true
  };

  const mockAssessment: ReplenishmentAssessmentResult = {
    tenantId: 'tenant-1',
    companyId: 'comp-1',
    branchId: 'branch-1',
    warehouseId: 'wh-1',
    warehouseCode: 'WH-MAIN',
    warehouseName: 'Main Facility',
    assessedAt: new Date().toISOString(),
    totalLinesAssessed: 1,
    linesWithShortage: 1,
    hasAnyShortage: true,
    lines: [
      {
        productId: 'prod-1',
        productCode: 'ITEM-1',
        productName: 'Item One',
        requiredQuantity: 20,
        quantityOnHand: 5,
        expiredQuantity: 0,
        eligibleOnHandQuantity: 5,
        quantityAllocated: 2,
        quantityQuarantine: 0,
        quantityOnOrder: 0,
        netAvailableStock: 3,
        shortageQuantity: 17,
        hasShortage: true,
        unitOfMeasure: 'PCS',
        estimatedUnitPrice: 50
      }
    ]
  };

  const mockPrResult: DraftPurchaseRequisitionResult = {
    requisitionId: 'pr-1',
    requisitionNumber: 'PR-20261127-001',
    status: 'Draft',
    lineCount: 1,
    itemCount: 1,
    isReused: false,
    message: 'Draft Purchase Requisition created successfully'
  };

  beforeEach(async () => {
    mockInventoryService = {
      assessReplenishment: vi.fn().mockReturnValue(of(mockAssessment)),
      createReplenishmentPurchaseRequisition: vi.fn().mockReturnValue(of(mockPrResult))
    };
    mockMasterDataService = {
      getWarehouses: vi.fn().mockReturnValue(of([mockWarehouse as any])),
      getProducts: vi.fn().mockReturnValue(of([mockProduct as any]))
    };
    mockScopeService = {
      selectedCompany: vi.fn().mockReturnValue({ id: 'comp-1', name: 'Company Alpha' }),
      selectedBranch: vi.fn().mockReturnValue({ id: 'branch-1', name: 'Branch 1' }),
      scopeChange$: of()
    };
    mockAuthService = {
      isTenantAdmin: vi.fn().mockReturnValue(false),
      isPlatformAdmin: vi.fn().mockReturnValue(false),
      permissions: vi.fn().mockReturnValue(['INVENTORY.VIEW', 'INVENTORY.REPLENISH.CREATE'])
    };
    mockRouter = {
      navigate: vi.fn()
    };

    await TestBed.configureTestingModule({
      imports: [InventoryReplenishmentComponent],
      providers: [
        { provide: InventoryService, useValue: mockInventoryService },
        { provide: MasterDataService, useValue: mockMasterDataService },
        { provide: WorkspaceScopeService, useValue: mockScopeService },
        { provide: AuthService, useValue: mockAuthService },
        { provide: Router, useValue: mockRouter }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(InventoryReplenishmentComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should initialize and load warehouses for selected company', () => {
    expect(component).toBeTruthy();
    expect(mockMasterDataService.getWarehouses).toHaveBeenCalled();
    expect(component.selectedWarehouseId()).toBe('wh-1');
  });

  it('should run stock assessment with indeterminate progress and populate lines', () => {
    component.runAssessment();

    expect(mockInventoryService.assessReplenishment).toHaveBeenCalled();
    expect(component.assessmentResult()).toEqual(mockAssessment);
    expect(component.lineRows().length).toBe(1);
    expect(component.lineRows()[0].shortageQuantity).toBe(17);
    expect(component.lineRows()[0].selected).toBe(true);
  });

  it('should open confirmation dialog and generate stable idempotency key', () => {
    component.runAssessment();
    component.openConfirmDialog();

    expect(component.confirmDialogVisible()).toBe(true);
    expect(component.currentIdempotencyKey()).toContain('REPL-BATCH-wh-1-');
  });

  it('should submit batch and display persistent Draft PR result', () => {
    component.runAssessment();
    component.openConfirmDialog();
    component.submitBatchPurchaseRequisition();

    expect(mockInventoryService.createReplenishmentPurchaseRequisition).toHaveBeenCalled();
    expect(component.isSubmitting()).toBe(false);
    expect(component.createdRequisition()).toEqual(mockPrResult);
    expect(component.confirmDialogVisible()).toBe(false);
  });

  it('should preserve selections and idempotency key on failure to allow safe retry', () => {
    component.runAssessment();
    component.openConfirmDialog();

    const originalKey = component.currentIdempotencyKey();

    // Simulate network error
    mockInventoryService.createReplenishmentPurchaseRequisition = vi.fn().mockReturnValue(
      throwError(() => ({ status: 0, message: 'Network timeout' }))
    );

    component.submitBatchPurchaseRequisition();

    expect(component.isSubmitting()).toBe(false);
    expect(component.error()).toBeTruthy();
    expect(component.error()?.category).toBe('Network');
    expect(component.error()?.canRetry).toBe(true);

    // Verify key was preserved
    expect(component.currentIdempotencyKey()).toBe(originalKey);
    // Verify selections preserved
    expect(component.selectedLinesCount()).toBe(1);

    // Retry submission re-uses original key
    mockInventoryService.createReplenishmentPurchaseRequisition = vi.fn().mockReturnValue(of(mockPrResult));
    component.retrySubmission();

    expect(mockInventoryService.createReplenishmentPurchaseRequisition).toHaveBeenCalledWith(
      'comp-1',
      'branch-1',
      expect.objectContaining({ idempotencyKey: originalKey })
    );
  });

  it('should navigate to procurement requisition view', () => {
    component.inspectRequisition('pr-123');
    expect(mockRouter.navigate).toHaveBeenCalledWith(['/app/procurement/purchase-requisitions', 'pr-123']);
  });
});
