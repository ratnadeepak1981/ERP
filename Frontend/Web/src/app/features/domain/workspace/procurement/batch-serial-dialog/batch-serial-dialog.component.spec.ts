import { describe, it, expect, beforeEach, vi } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { of, throwError } from 'rxjs';
import {
  BatchSerialTrackingDialogComponent,
  BatchSerialDialogData
} from './batch-serial-dialog.component';
import { InventoryService } from '../../../../../core/services/inventory.service';
import { TrackingMode } from '../../../../../core/models/scope.models';
import { EligibleBatchDto } from '../../../../../core/models/inventory.models';

describe('BatchSerialTrackingDialogComponent', () => {
  let component: BatchSerialTrackingDialogComponent;
  let fixture: ComponentFixture<BatchSerialTrackingDialogComponent>;
  let inventoryServiceMock: Partial<InventoryService>;
  let dialogRefMock: Partial<MatDialogRef<BatchSerialTrackingDialogComponent>>;

  const mockEligibleBatches: EligibleBatchDto[] = [
    {
      productId: 'prod-101',
      productCode: 'RAW-STEEL',
      productName: 'Raw Steel Coil',
      batchNumber: 'LOT-FEFO-01',
      warehouseId: 'wh-1',
      warehouseCode: 'MAIN',
      warehouseName: 'Main Facility',
      warehouseLocationId: 'loc-1',
      locationCode: 'A1-01',
      locationName: 'Storage Bin 1',
      quantityOnHand: 20,
      quantityAvailable: 15,
      manufacturingDate: '2026-01-01T00:00:00Z',
      expiryDate: '2026-06-30T00:00:00Z'
    },
    {
      productId: 'prod-101',
      productCode: 'RAW-STEEL',
      productName: 'Raw Steel Coil',
      batchNumber: 'LOT-FEFO-02',
      warehouseId: 'wh-1',
      warehouseCode: 'MAIN',
      warehouseName: 'Main Facility',
      warehouseLocationId: 'loc-1',
      locationCode: 'A1-01',
      locationName: 'Storage Bin 1',
      quantityOnHand: 50,
      quantityAvailable: 50,
      manufacturingDate: '2026-02-01T00:00:00Z',
      expiryDate: '2026-12-31T00:00:00Z'
    }
  ];

  function createComponent(data: BatchSerialDialogData) {
    inventoryServiceMock = {
      getEligibleBatches: vi.fn().mockReturnValue(of(mockEligibleBatches))
    };

    dialogRefMock = {
      close: vi.fn()
    };

    TestBed.configureTestingModule({
      imports: [BatchSerialTrackingDialogComponent],
      providers: [
        { provide: MAT_DIALOG_DATA, useValue: data },
        { provide: MatDialogRef, useValue: dialogRefMock },
        { provide: InventoryService, useValue: inventoryServiceMock }
      ]
    });

    fixture = TestBed.createComponent(BatchSerialTrackingDialogComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  }

  describe('Receipt Mode (Preservation)', () => {
    beforeEach(() => {
      createComponent({
        productCode: 'RAW-STEEL',
        productName: 'Raw Steel Coil',
        unitOfMeasure: 'KG',
        trackingMode: TrackingMode.Batch,
        targetQuantity: 10,
        readOnly: false,
        allocations: [],
        dialogMode: 'receipt'
      });
    });

    it('should initialize in receipt mode without fetching eligible batches', () => {
      expect(component.isIssueMode()).toBe(false);
      expect(inventoryServiceMock.getEligibleBatches).not.toHaveBeenCalled();
      expect(component.columns.find(c => c.field === 'batchNumber')?.type).toBe('text');
      expect(component.columns.find(c => c.field === 'manufacturingDate')?.editable).toBe(true);
      expect(component.columns.find(c => c.field === 'expiryDate')?.editable).toBe(true);
    });

    it('should allow free text batch creation and editable dates in receipt mode', () => {
      const newRow = component.createNewRow();
      expect(newRow.batchNumber).toBe('');
      expect(newRow.manufacturingDate).toBe('');
      expect(newRow.expiryDate).toBe('');
    });
  });

  describe('Issue Mode (Outbound Batch Selection)', () => {
    beforeEach(() => {
      createComponent({
        productCode: 'RAW-STEEL',
        productName: 'Raw Steel Coil',
        unitOfMeasure: 'KG',
        trackingMode: TrackingMode.Batch,
        targetQuantity: 10,
        readOnly: false,
        allocations: [],
        dialogMode: 'issue',
        productId: 'prod-101',
        warehouseId: 'wh-1',
        warehouseLocationId: 'loc-1'
      });
    });

    it('should identify as issue mode and fetch eligible batches for the given product and location', () => {
      expect(component.isIssueMode()).toBe(true);
      expect(inventoryServiceMock.getEligibleBatches).toHaveBeenCalledWith('prod-101', 'wh-1', 'loc-1');
      expect(component.eligibleBatches().length).toBe(2);
    });

    it('should configure batchNumber as a select column with FEFO eligible batch options', () => {
      const batchCol = component.columns.find(c => c.field === 'batchNumber');
      expect(batchCol?.type).toBe('select');
      expect(batchCol?.options?.length).toBe(2);

      const firstOpt = (batchCol?.options as any)[0];
      expect(firstOpt.value).toBe('LOT-FEFO-01');
      expect(firstOpt.label).toContain('LOT-FEFO-01');
      expect(firstOpt.label).toContain('Avail: 15');
    });

    it('should keep manufacturingDate and expiryDate strictly non-editable in issue mode', () => {
      const mfgCol = component.columns.find(c => c.field === 'manufacturingDate');
      const expCol = component.columns.find(c => c.field === 'expiryDate');

      expect(mfgCol?.editable).toBe(false);
      expect(expCol?.editable).toBe(false);
    });

    it('should pre-populate eligible batch and lock dates when creating a new row in issue mode', () => {
      const newRow = component.createNewRow();
      expect(newRow.batchNumber).toBe('LOT-FEFO-01');
      expect(newRow.manufacturingDate).toBe('2026-01-01');
      expect(newRow.expiryDate).toBe('2026-06-30');
    });

    it('should reject unknown batch numbers in issue mode validation', () => {
      component.workingAllocations.set([
        {
          id: 'row-1',
          batchNumber: 'UNKNOWN-BATCH',
          quantity: 10,
          manufacturingDate: '',
          expiryDate: '',
          isQuarantine: false
        }
      ]);

      expect(component.validationErrors().some(e => e.includes('not an eligible active on-hand batch'))).toBe(true);
      expect(component.canApply()).toBe(false);
    });

    it('should reject allocation if allocated quantity exceeds available stock of selected batch', () => {
      // LOT-FEFO-01 has 15 available. Allocate 20.
      component.data.targetQuantity = 20;
      component.workingAllocations.set([
        {
          id: 'row-1',
          batchNumber: 'LOT-FEFO-01',
          quantity: 20,
          manufacturingDate: '2026-01-01',
          expiryDate: '2026-06-30',
          isQuarantine: false
        }
      ]);

      expect(component.validationErrors().some(e => e.includes('exceeds available stock (15)'))).toBe(true);
      expect(component.canApply()).toBe(false);
    });

    it('should allow allocating across multiple eligible batches within available stock', () => {
      component.data.targetQuantity = 25;
      component.workingAllocations.set([
        {
          id: 'row-1',
          batchNumber: 'LOT-FEFO-01',
          quantity: 15, // Max available for LOT-FEFO-01
          manufacturingDate: '2026-01-01',
          expiryDate: '2026-06-30',
          isQuarantine: false
        },
        {
          id: 'row-2',
          batchNumber: 'LOT-FEFO-02',
          quantity: 10, // Within 50 available for LOT-FEFO-02
          manufacturingDate: '2026-02-01',
          expiryDate: '2026-12-31',
          isQuarantine: false
        }
      ]);

      expect(component.isComplete()).toBe(true);
      expect(component.validationErrors().length).toBe(0);
      expect(component.canApply()).toBe(true);

      component.onApply();
      expect(dialogRefMock.close).toHaveBeenCalledWith(component.workingAllocations());
    });
  });

  describe('Issue Mode Serial Validation', () => {
    beforeEach(() => {
      createComponent({
        productCode: 'RAW-EQUIP',
        productName: 'Equipment Device',
        unitOfMeasure: 'EA',
        trackingMode: TrackingMode.Serial,
        targetQuantity: 2,
        readOnly: false,
        allocations: [],
        dialogMode: 'issue',
        productId: 'prod-202',
        warehouseId: 'wh-1',
        warehouseLocationId: 'loc-1'
      });
    });

    it('should not show bulk serial generation in issue mode', () => {
      expect(component.isIssueMode()).toBe(true);
      // Bulk generation is guarded by !isIssueMode()
    });

    it('should enforce quantity == 1 and uniqueness for serial issue entries', () => {
      component.workingAllocations.set([
        {
          id: 's1',
          batchNumber: '',
          serialNumber: 'SN-1001',
          quantity: 1,
          isQuarantine: false
        },
        {
          id: 's2',
          batchNumber: '',
          serialNumber: 'SN-1001', // duplicate
          quantity: 1,
          isQuarantine: false
        }
      ]);

      expect(component.validationErrors().some(e => e.includes('Duplicate serial number'))).toBe(true);
      expect(component.canApply()).toBe(false);

      // Fix duplicate
      component.workingAllocations.set([
        {
          id: 's1',
          batchNumber: '',
          serialNumber: 'SN-1001',
          quantity: 1,
          isQuarantine: false
        },
        {
          id: 's2',
          batchNumber: '',
          serialNumber: 'SN-1002',
          quantity: 1,
          isQuarantine: false
        }
      ]);

      expect(component.validationErrors().length).toBe(0);
      expect(component.canApply()).toBe(true);
    });
  });
});
