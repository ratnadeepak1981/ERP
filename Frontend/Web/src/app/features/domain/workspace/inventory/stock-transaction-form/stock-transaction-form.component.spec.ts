import { describe, it, expect, beforeEach, vi } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter, Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import { StockTransactionFormComponent } from './stock-transaction-form.component';
import { InventoryService } from '../../../../../core/services/inventory.service';
import { MasterDataService } from '../../../../../core/services/master-data.service';
import { AuthService } from '../../../../../core/auth/auth.service';
import {
  StockIssueDto,
  StockIssuePurpose,
  StockIssueStatus,
  StockReceiptDto,
  StockReceiptReason,
  StockReceiptStatus
} from '../../../../../core/models/inventory.models';
import {
  ProductItem,
  WarehouseItem,
  WarehouseLocationItem
} from '../../../../../core/models/scope.models';
import { StockTransactionLineItem } from './stock-transaction-form.models';

describe('StockTransactionFormComponent', () => {
  let component: StockTransactionFormComponent;
  let fixture: ComponentFixture<StockTransactionFormComponent>;
  let inventoryService: InventoryService;
  let masterDataService: MasterDataService;
  let router: Router;
  let authService: AuthService;

  const mockWarehouses: WarehouseItem[] = [
    {
      warehouseId: 'wh-1',
      warehouseCode: 'MAIN',
      warehouseName: 'Central Logistics Hub',
      tenantId: 'tenant-1',
      isActive: true
    }
  ];

  const mockLocations: WarehouseLocationItem[] = [
    {
      locationId: 'loc-1',
      warehouseId: 'wh-1',
      locationCode: 'R01-L02-B03',
      locationName: 'Rack 1 > Level 2 > Bin 3',
      isActive: true
    }
  ];

  const mockProducts: ProductItem[] = [
    {
      productId: 'prod-1',
      productCode: 'STL-001',
      productName: 'Galvanized Steel Sheet',
      unitOfMeasure: 'SHT',
      tenantId: 'tenant-1',
      categoryId: 'cat-1',
      isManufacturable: false,
      isPurchasable: true,
      isSellable: true,
      canConsumeInProduction: true,
      canConsumeInMaintenance: true,
      isStockTracked: true,
      productType: 1,
      trackingMode: 1,
      valuationMethod: 1,
      standardCost: 120,
      sellingPrice: 150,
      reorderLevel: 10,
      reorderQuantity: 50,
      isActive: true
    }
  ];

  const mockReceiptResult: StockReceiptDto[] = [
    {
      stockReceiptId: 'receipt-123',
      receiptNumber: 'REC-2026-0001',
      receiptDate: '2026-10-19T10:00:00Z',
      reason: StockReceiptReason.OpeningBalance,
      reasonName: 'Opening / Initial Balance',
      movementType: 'StockAdjustmentIn',
      status: StockReceiptStatus.Posted,
      statusName: 'Posted',
      warehouseId: 'wh-1',
      warehouseCode: 'MAIN',
      warehouseName: 'Central Logistics Hub',
      warehouseLocationId: 'loc-1',
      locationCode: 'R01-L02-B03',
      locationName: 'Rack 1 > Level 2 > Bin 3',
      productId: 'prod-1',
      productCode: 'STL-001',
      productName: 'Galvanized Steel Sheet',
      unitOfMeasure: 'SHT',
      quantity: 25,
      unitCost: 120,
      totalCost: 3000,
      createdAt: '2026-10-19T10:00:00Z'
    }
  ];

  const mockIssueResult: StockIssueDto[] = [
    {
      stockIssueId: 'issue-123',
      issueNumber: 'ISS-2026-0001',
      issueDate: '2026-10-19T10:00:00Z',
      purpose: StockIssuePurpose.InternalUse,
      purposeName: 'Internal Use / Maintenance',
      movementType: 'StockAdjustmentOut',
      status: StockIssueStatus.Posted,
      statusName: 'Posted',
      warehouseId: 'wh-1',
      warehouseCode: 'MAIN',
      warehouseName: 'Central Logistics Hub',
      warehouseLocationId: 'loc-1',
      locationCode: 'R01-L02-B03',
      locationName: 'Rack 1 > Level 2 > Bin 3',
      productId: 'prod-1',
      productCode: 'STL-001',
      productName: 'Galvanized Steel Sheet',
      unitOfMeasure: 'SHT',
      quantity: 5,
      unitCost: 120,
      totalCost: 600,
      createdAt: '2026-10-19T10:00:00Z'
    }
  ];

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [StockTransactionFormComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(StockTransactionFormComponent);
    component = fixture.componentInstance;
    inventoryService = TestBed.inject(InventoryService);
    masterDataService = TestBed.inject(MasterDataService);
    router = TestBed.inject(Router);
    authService = TestBed.inject(AuthService);

    vi.spyOn(authService, 'isTenantAdmin').mockReturnValue(true);
    vi.spyOn(router, 'navigate').mockResolvedValue(true);
    vi.spyOn(masterDataService, 'getWarehouses').mockReturnValue(of(mockWarehouses));
    vi.spyOn(masterDataService, 'getWarehouseLocations').mockReturnValue(of(mockLocations));
    vi.spyOn(masterDataService, 'getProducts').mockReturnValue(of(mockProducts));
    vi.spyOn(inventoryService, 'getAvailableStock').mockReturnValue(
      of({
        productId: 'prod-1',
        warehouseLocationId: 'loc-1',
        quantityOnHand: 50,
        quantityReserved: 10,
        quantityAllocated: 0,
        quantityQuarantine: 0,
        quantityAvailable: 40
      })
    );
    vi.spyOn(inventoryService, 'createStockReceipt').mockReturnValue(of(mockReceiptResult));
    vi.spyOn(inventoryService, 'createStockIssue').mockReturnValue(of(mockIssueResult));
  });

  describe('Receipt Mode Configuration', () => {
    beforeEach(() => {
      component.transactionType = 'receipt';
      fixture.detectChanges();
    });

    it('should configure form for Stock Receipt with inbound reason options', () => {
      expect(component.isReceipt()).toBe(true);
      expect(component.isIssue()).toBe(false);
      expect(component.reasonOrPurposeOptions().length).toBe(4);
      expect(component.reasonOrPurposeOptions()[0].label).toContain('Opening / Initial Balance');
    });

    it('should format Rack → Level → Bin hierarchy correctly', () => {
      const formatted = component.formatLocationHierarchy(mockLocations[0]);
      expect(formatted).toContain('Rack 1 → Level 2 → Bin 3');
    });

    it('should validate and post stock receipt through inventory service', () => {
      component.lines.update(lines => [
        {
          id: 'line-1',
          productId: 'prod-1',
          productCode: 'STL-001',
          productName: 'Galvanized Steel Sheet',
          unitOfMeasure: 'SHT',
          warehouseId: 'wh-1',
          warehouseLocationId: 'loc-1',
          availableStock: 0,
          loadingStock: false,
          quantity: 25,
          unitCost: 120
        }
      ]);

      expect(component.isValid()).toBe(true);

      const postSpy = vi.spyOn(inventoryService, 'createStockReceipt');
      const routerSpy = vi.spyOn(router, 'navigate');

      component.postTransaction();

      expect(postSpy).toHaveBeenCalled();
      expect(routerSpy).toHaveBeenCalledWith(['/app/inventory/stock-receipts', 'receipt-123']);
    });
  });

  describe('Issue Mode Configuration', () => {
    beforeEach(() => {
      component.transactionType = 'issue';
      fixture.detectChanges();
    });

    it('should configure form for Stock Issue with outbound purpose options', () => {
      expect(component.isIssue()).toBe(true);
      expect(component.isReceipt()).toBe(false);
      expect(component.reasonOrPurposeOptions().length).toBe(4);
      expect(component.reasonOrPurposeOptions()[0].label).toContain('Internal Use');
    });

    it('should prevent negative stock when issue quantity exceeds available stock', () => {
      component.lines.update(lines => [
        {
          id: 'line-1',
          productId: 'prod-1',
          productCode: 'STL-001',
          productName: 'Galvanized Steel Sheet',
          unitOfMeasure: 'SHT',
          warehouseId: 'wh-1',
          warehouseLocationId: 'loc-1',
          availableStock: 10,
          loadingStock: false,
          quantity: 15 // exceeds 10
        }
      ]);

      expect(component.isValid()).toBe(false);
    });

    it('should allow posting when issue quantity is within available stock', () => {
      component.lines.update(lines => [
        {
          id: 'line-1',
          productId: 'prod-1',
          productCode: 'STL-001',
          productName: 'Galvanized Steel Sheet',
          unitOfMeasure: 'SHT',
          warehouseId: 'wh-1',
          warehouseLocationId: 'loc-1',
          availableStock: 40,
          loadingStock: false,
          quantity: 5 // within 40
        }
      ]);

      expect(component.isValid()).toBe(true);

      const postSpy = vi.spyOn(inventoryService, 'createStockIssue');
      const routerSpy = vi.spyOn(router, 'navigate');

      component.postTransaction();

      expect(postSpy).toHaveBeenCalled();
      expect(routerSpy).toHaveBeenCalledWith(['/app/inventory/issue-stock', 'issue-123']);
    });
  });

  describe('View Mode Configuration', () => {
    it('should load stock receipt details in view mode', () => {
      vi.spyOn(inventoryService, 'getStockReceipt').mockReturnValue(of(mockReceiptResult[0]));
      component.transactionType = 'receipt';
      component.mode = 'view';
      component.recordId = 'receipt-123';

      fixture.detectChanges();

      expect(component.viewReceipt()).toBeTruthy();
      expect(component.viewReceipt()?.receiptNumber).toBe('REC-2026-0001');
    });

    it('should load stock issue details in view mode', () => {
      vi.spyOn(inventoryService, 'getStockIssue').mockReturnValue(of(mockIssueResult[0]));
      component.transactionType = 'issue';
      component.mode = 'view';
      component.recordId = 'issue-123';

      fixture.detectChanges();

      expect(component.viewIssue()).toBeTruthy();
      expect(component.viewIssue()?.issueNumber).toBe('ISS-2026-0001');
    });
  });

  describe('Editable Table and Action Buttons Configuration', () => {
    it('should configure receipt columns with lookup, facility, location, unit cost and quantity', () => {
      component.transactionType = 'receipt';
      fixture.detectChanges();

      const cols = component.columns();
      const fields = cols.map(c => c.field);

      expect(fields).toContain('productCode');
      expect(fields).toContain('warehouseId');
      expect(fields).toContain('warehouseLocationId');
      expect(fields).toContain('unitCost');
      expect(fields).toContain('quantity');
      expect(fields).toContain('unitOfMeasure');
      expect(fields).not.toContain('availableStock');

      const productCol = cols.find(c => c.field === 'productCode');
      expect(productCol?.editType).toBe('lookup');
      expect(productCol?.editable).toBe(true);
      expect(productCol?.lookupOptions?.length).toBe(1);

      const locCol = cols.find(c => c.field === 'warehouseLocationId');
      expect(locCol?.header).toContain('Destination Location');
      expect(locCol?.editType).toBe('select');
    });

    it('should configure issue columns with available stock and negative stock validation', () => {
      component.transactionType = 'issue';
      fixture.detectChanges();

      const cols = component.columns();
      const fields = cols.map(c => c.field);

      expect(fields).toContain('productCode');
      expect(fields).toContain('warehouseId');
      expect(fields).toContain('warehouseLocationId');
      expect(fields).toContain('availableStock');
      expect(fields).toContain('quantity');
      expect(fields).not.toContain('unitCost');

      const locCol = cols.find(c => c.field === 'warehouseLocationId');
      expect(locCol?.header).toContain('Source Location');

      const qtyCol = cols.find(c => c.field === 'quantity');
      expect(qtyCol?.validators?.length).toBeGreaterThan(0);

      // Test negative stock validator
      const validator = qtyCol?.validators![0];
      expect(validator).toBeDefined();

      const testRow: StockTransactionLineItem = {
        id: 'line-test',
        productId: 'prod-1',
        productCode: 'STL-001',
        productName: 'Galvanized Steel Sheet',
        unitOfMeasure: 'SHT',
        warehouseId: 'wh-1',
        warehouseLocationId: 'loc-1',
        availableStock: 10,
        loadingStock: false,
        quantity: 20
      };

      const errorMsg = validator!(20, testRow);
      expect(errorMsg).toContain('Exceeds available stock (10)');

      const validMsg = validator!(5, testRow);
      expect(validMsg).toBeNull();
    });

    it('should generate correct default new row and recalculate line totals', () => {
      component.transactionType = 'receipt';
      fixture.detectChanges();

      const newRow = component.defaultNewRow();
      expect(newRow.id).toContain('line-');
      expect(newRow.quantity).toBe(1);
      expect(newRow.unitOfMeasure).toBe('PCS');

      const rowToCalc: StockTransactionLineItem = {
        id: 'row-1',
        productId: 'prod-1',
        productCode: 'STL-001',
        productName: 'Steel',
        unitOfMeasure: 'SHT',
        warehouseId: 'wh-1',
        warehouseLocationId: 'loc-1',
        availableStock: 0,
        loadingStock: false,
        quantity: 10,
        unitCost: 15.5
      };

      component.calculateRow(rowToCalc);
      expect((rowToCalc as any).lineTotal).toBe(155);
    });

    it('should calculate total quantity and total receipt cost correctly', () => {
      component.transactionType = 'receipt';
      component.lines.set([
        {
          id: '1',
          productId: 'p1',
          productCode: 'C1',
          productName: 'P1',
          unitOfMeasure: 'PCS',
          warehouseId: 'w1',
          warehouseLocationId: 'l1',
          availableStock: 0,
          loadingStock: false,
          quantity: 10,
          unitCost: 20
        },
        {
          id: '2',
          productId: 'p2',
          productCode: 'C2',
          productName: 'P2',
          unitOfMeasure: 'PCS',
          warehouseId: 'w1',
          warehouseLocationId: 'l1',
          availableStock: 0,
          loadingStock: false,
          quantity: 5,
          unitCost: 10
        }
      ]);

      expect(component.totalQuantity()).toBe(15);
      expect(component.totalReceiptCost()).toBe(250);
    });

    it('should synchronize lines on dataChange and cellValueChange events', () => {
      component.transactionType = 'receipt';
      fixture.detectChanges();

      const row: StockTransactionLineItem = {
        id: '1',
        productId: '',
        productCode: '',
        productName: '',
        unitOfMeasure: 'PCS',
        warehouseId: 'wh-1',
        warehouseLocationId: 'loc-1',
        availableStock: 0,
        loadingStock: false,
        quantity: 1
      };
      component.lines.set([row]);

      // Simulate cell change of product
      component.onTableCellValueChange({
        row,
        column: { field: 'productId', header: 'Product' },
        value: 'prod-1'
      });

      expect(component.lines()[0].productId).toBe('prod-1');
      expect(component.lines()[0].productName).toBe('Galvanized Steel Sheet');
      expect(component.lines()[0].unitCost).toBe(120);
    });

    it('should preserve user-entered unit cost when selecting product in receipt mode', () => {
      component.transactionType = 'receipt';
      fixture.detectChanges();

      const row: StockTransactionLineItem = {
        id: '1',
        productId: '',
        productCode: '',
        productName: '',
        unitOfMeasure: 'PCS',
        warehouseId: 'wh-1',
        warehouseLocationId: 'loc-1',
        availableStock: 0,
        loadingStock: false,
        quantity: 1,
        unitCost: 85.5
      };
      component.lines.set([row]);

      component.onTableCellValueChange({
        row,
        column: { field: 'productId', header: 'Product' },
        value: 'prod-1'
      });

      expect(component.lines()[0].productId).toBe('prod-1');
      expect(component.lines()[0].unitCost).toBe(85.5);
    });

    it('should preserve user-entered unit cost when selecting product in receipt mode', () => {
      component.transactionType = 'receipt';
      fixture.detectChanges();

      const row: StockTransactionLineItem = {
        id: '1',
        productId: '',
        productCode: '',
        productName: '',
        unitOfMeasure: 'PCS',
        warehouseId: 'wh-1',
        warehouseLocationId: 'loc-1',
        availableStock: 0,
        loadingStock: false,
        quantity: 1,
        unitCost: 85.5
      };
      component.lines.set([row]);

      component.onTableCellValueChange({
        row,
        column: { field: 'productId', header: 'Product' },
        value: 'prod-1'
      });

      expect(component.lines()[0].productId).toBe('prod-1');
      expect(component.lines()[0].unitCost).toBe(85.5);
    });

    it('should trigger cancel navigation when onCancel is called', () => {
      const cancelSpy = vi.spyOn(component.cancel, 'emit');
      const routerSpy = vi.spyOn(router, 'navigate');

      component.transactionType = 'receipt';
      component.onCancel();

      expect(cancelSpy).toHaveBeenCalled();
      expect(routerSpy).toHaveBeenCalledWith(['/app/inventory/stock-receipts']);
    });
  });
});
