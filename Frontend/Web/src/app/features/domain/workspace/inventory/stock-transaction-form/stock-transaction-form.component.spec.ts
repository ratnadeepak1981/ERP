import { describe, it, expect, beforeEach, vi } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient, HttpErrorResponse } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter, Router } from '@angular/router';
import { MatDialog } from '@angular/material/dialog';
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
  StockReceiptStatus,
  CreateStockReceiptRequest,
  CreateStockIssueRequest
} from '../../../../../core/models/inventory.models';
import {
  ProductItem,
  WarehouseItem,
  WarehouseLocationItem,
  TrackingMode
} from '../../../../../core/models/scope.models';
import { BatchSerialAllocation } from '../../../../../core/models/goods-receipt-note.models';
import { BatchSerialTrackingDialogComponent } from '../../procurement/batch-serial-dialog/batch-serial-dialog.component';
import { StockTransactionLineItem } from './stock-transaction-form.models';

describe('StockTransactionFormComponent', () => {
  let component: StockTransactionFormComponent;
  let fixture: ComponentFixture<StockTransactionFormComponent>;
  let inventoryService: InventoryService;
  let masterDataService: MasterDataService;
  let router: Router;
  let authService: AuthService;
  let matDialog: MatDialog;

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
      warehouseLocationId: 'loc-1',
      locationId: '',
      warehouseId: 'wh-1',
      locationCode: 'R01-L02-B03',
      locationName: 'Rack 1 > Level 2 > Bin 3',
      isActive: true
    },
    {
      warehouseLocationId: 'loc-2',
      locationId: '',
      warehouseId: 'wh-1',
      locationCode: 'R01-L02-B04',
      locationName: 'Rack 1 > Level 2 > Bin 4',
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

  const mockUntrackedProduct: ProductItem = {
    productId: 'prod-2',
    productCode: 'BLT-001',
    productName: 'M8 Hex Bolt',
    unitOfMeasure: 'PCS',
    tenantId: 'tenant-1',
    categoryId: 'cat-1',
    isManufacturable: false,
    isPurchasable: true,
    isSellable: true,
    canConsumeInProduction: true,
    canConsumeInMaintenance: true,
    isStockTracked: true,
    productType: 1,
    trackingMode: 0,
    valuationMethod: 1,
    standardCost: 2,
    sellingPrice: 5,
    reorderLevel: 100,
    reorderQuantity: 500,
    isActive: true
  };

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
    matDialog = TestBed.inject(MatDialog);

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

  describe('Storage Location Mapping and Validation Regression Tests', () => {
    beforeEach(() => {
      component.transactionType = 'receipt';
      fixture.detectChanges();
    });

    it('should load location options using backend DTO warehouseLocationId and clear validation error', () => {
      const line: StockTransactionLineItem = {
        id: 'line-test',
        productId: 'prod-1',
        productCode: 'STL-001',
        productName: 'Galvanized Steel Sheet',
        unitOfMeasure: 'SHT',
        warehouseId: 'wh-1',
        warehouseLocationId: '',
        availableStock: 0,
        loadingStock: false,
        quantity: 10,
        unitCost: 120
      };
      component.lines.set([line]);

      const options = component.getLocationOptionsForRow(line);
      expect(options.length).toBe(2);
      expect(options[0].value).toBe('loc-1');
      expect(options[1].value).toBe('loc-2');

      const locCol = component.columns().find(c => c.field === 'warehouseLocationId');
      expect(locCol?.validators).toBeDefined();
      const validator = locCol!.validators![0];
      expect(validator('', line)).toBe('Storage location is required');

      component.onTableCellValueChange({
        row: line,
        column: locCol!,
        value: 'loc-1'
      });

      expect(component.lines()[0].warehouseLocationId).toBe('loc-1');
      expect(validator('loc-1', line)).toBeNull();
      expect(component.isValid()).toBe(true);
    });

    it('should reject empty or missing location in form validation', () => {
      component.lines.set([
        {
          id: 'line-empty-loc',
          productId: 'prod-1',
          productCode: 'STL-001',
          productName: 'Galvanized Steel Sheet',
          unitOfMeasure: 'SHT',
          warehouseId: 'wh-1',
          warehouseLocationId: '',
          availableStock: 0,
          loadingStock: false,
          quantity: 10,
          unitCost: 120
        }
      ]);

      expect(component.isValid()).toBe(false);
    });

    it('should support multiple receipt lines with distinct storage locations and post to ledger', () => {
      component.lines.set([
        {
          id: 'line-1',
          productId: 'prod-1',
          productCode: 'STL-001',
          productName: 'Steel',
          unitOfMeasure: 'SHT',
          warehouseId: 'wh-1',
          warehouseLocationId: 'loc-1',
          availableStock: 0,
          loadingStock: false,
          quantity: 10,
          unitCost: 100
        },
        {
          id: 'line-2',
          productId: 'prod-1',
          productCode: 'STL-001',
          productName: 'Steel',
          unitOfMeasure: 'SHT',
          warehouseId: 'wh-1',
          warehouseLocationId: 'loc-2',
          availableStock: 0,
          loadingStock: false,
          quantity: 20,
          unitCost: 100
        }
      ]);

      expect(component.isValid()).toBe(true);
      expect(component.totalQuantity()).toBe(30);

      const postSpy = vi.spyOn(inventoryService, 'createStockReceipt');
      component.postTransaction();

      expect(postSpy).toHaveBeenCalledWith(
        expect.objectContaining({
          lines: expect.arrayContaining([
            expect.objectContaining({ warehouseLocationId: 'loc-1', quantity: 10 }),
            expect.objectContaining({ warehouseLocationId: 'loc-2', quantity: 20 })
          ])
        })
      );
    });
  });

  describe('F2 authorization and error states', () => {
    beforeEach(() => {
      component.transactionType = 'receipt';
    });

    it('should stop loading and show Access Denied when warehouse lookup returns 403', () => {
      vi.spyOn(masterDataService, 'getWarehouses').mockReturnValue(
        throwError(() => new HttpErrorResponse({ status: 403, statusText: 'Forbidden' }))
      );

      fixture.detectChanges();

      expect(component.loading()).toBe(false);
      expect(component.isAccessDenied()).toBe(true);
      expect(component.loadError()).toBeNull();

      const compiled = fixture.nativeElement as HTMLElement;
      expect(compiled.textContent).toContain('Access Restricted');
    });

    it('should show Access Denied when product lookup returns 403', () => {
      vi.spyOn(masterDataService, 'getProducts').mockReturnValue(
        throwError(() => new HttpErrorResponse({ status: 403, statusText: 'Forbidden' }))
      );

      fixture.detectChanges();

      expect(component.loading()).toBe(false);
      expect(component.isAccessDenied()).toBe(true);
    });

    it('should show a safe retryable error instead of raw transport text for a 500', () => {
      vi.spyOn(masterDataService, 'getWarehouses').mockReturnValue(
        throwError(
          () =>
            new HttpErrorResponse({
              status: 500,
              statusText: 'Internal Server Error',
              url: 'http://localhost:4200/api/warehouses'
            })
        )
      );

      fixture.detectChanges();

      expect(component.loading()).toBe(false);
      expect(component.isAccessDenied()).toBe(false);
      expect(component.loadError()).toBeTruthy();
      expect(component.loadError()).not.toMatch(/http failure response/i);
      expect(component.loadError()).not.toMatch(/localhost/);

      const compiled = fixture.nativeElement as HTMLElement;
      expect(compiled.textContent).not.toContain('Http failure response');
      expect(compiled.textContent).toContain('Retry');
    });

    it('should re-invoke the lookup and clear the error when loading is retried', () => {
      const warehouseSpy = vi
        .spyOn(masterDataService, 'getWarehouses')
        .mockReturnValue(
          throwError(() => new HttpErrorResponse({ status: 500, statusText: 'Internal Server Error' }))
        );

      fixture.detectChanges();
      expect(component.loadError()).toBeTruthy();

      warehouseSpy.mockReturnValue(of(mockWarehouses));
      component.loadMasterData();

      expect(warehouseSpy).toHaveBeenCalledTimes(2);
      expect(component.loadError()).toBeNull();
      expect(component.warehouses().length).toBe(1);
      expect(component.isAccessDenied()).toBe(false);
    });
  });

  describe('Stock Transaction Form Permissions (Flow A)', () => {
    it('should allow post in receipt mode when user has STOCK_RECEIPT.POST even without PRODUCT.EDIT', () => {
      component.transactionType = 'receipt';
      vi.spyOn(authService, 'isTenantAdmin').mockReturnValue(false);
      vi.spyOn(authService, 'isPlatformAdmin').mockReturnValue(false);
      vi.spyOn(authService, 'isPlatformStaff').mockReturnValue(false);
      vi.spyOn(authService, 'hasPermission').mockImplementation((perm: string) => perm === 'STOCK_RECEIPT.POST');

      expect(component.canPost()).toBe(true);
    });

    it('should allow post in issue mode when user has STOCK_ISSUE.POST even without PRODUCT.EDIT', () => {
      component.transactionType = 'issue';
      vi.spyOn(authService, 'isTenantAdmin').mockReturnValue(false);
      vi.spyOn(authService, 'isPlatformAdmin').mockReturnValue(false);
      vi.spyOn(authService, 'isPlatformStaff').mockReturnValue(false);
      vi.spyOn(authService, 'hasPermission').mockImplementation((perm: string) => perm === 'STOCK_ISSUE.POST');

      expect(component.canPost()).toBe(true);
    });

    it('should disallow post when user has PRODUCT.EDIT but lacks transaction-specific permission', () => {
      vi.spyOn(authService, 'isTenantAdmin').mockReturnValue(false);
      vi.spyOn(authService, 'isPlatformAdmin').mockReturnValue(false);
      vi.spyOn(authService, 'isPlatformStaff').mockReturnValue(false);
      vi.spyOn(authService, 'hasPermission').mockImplementation((perm: string) => perm === 'PRODUCT.EDIT');

      component.transactionType = 'receipt';
      expect(component.canPost()).toBe(false);

      component.transactionType = 'issue';
      expect(component.canPost()).toBe(false);
    });

    it('should deny access on create mode when user lacks post permission (Auditor/Warehouse Manager)', () => {
      vi.spyOn(authService, 'isTenantAdmin').mockReturnValue(false);
      vi.spyOn(authService, 'isPlatformAdmin').mockReturnValue(false);
      vi.spyOn(authService, 'isPlatformStaff').mockReturnValue(false);
      vi.spyOn(authService, 'hasPermission').mockImplementation((perm: string) => perm === 'INVENTORY.VIEW');

      component.transactionType = 'receipt';
      component.mode = 'create';
      component.ngOnInit();

      expect(component.isAccessDenied()).toBe(true);
      expect(component.accessDeniedMessage()).toContain('STOCK_RECEIPT.POST');
    });

    it('should allow view mode when user has INVENTORY.VIEW even without post permissions', () => {
      vi.spyOn(authService, 'isTenantAdmin').mockReturnValue(false);
      vi.spyOn(authService, 'isPlatformAdmin').mockReturnValue(false);
      vi.spyOn(authService, 'isPlatformStaff').mockReturnValue(false);
      vi.spyOn(authService, 'hasPermission').mockImplementation((perm: string) => perm === 'INVENTORY.VIEW');

      component.transactionType = 'receipt';
      component.mode = 'view';
      component.recordId = 'receipt-123';
      vi.spyOn(inventoryService, 'getStockReceipt').mockReturnValue(of(mockReceiptResult[0]));

      component.ngOnInit();

      expect(component.isAccessDenied()).toBe(false);
      expect(component.canView()).toBe(true);
      expect(component.canPost()).toBe(false);
    });
  });

  describe('Batch & Serial Tracking in Stock Receipt', () => {
    beforeEach(() => {
      component.transactionType = 'receipt';
      component.mode = 'create';
      fixture.detectChanges();
    });

    it('should initialize trackingMode and trackingAllocations when a tracked product is selected via lookup', () => {
      const row = component.defaultNewRow() as StockTransactionLineItem;
      const lookupCol = component.columns().find(c => c.field === 'productCode');
      expect(lookupCol).toBeDefined();

      const trackedItem = component.productLookupItems().find(p => p.code === 'STL-001');
      expect(trackedItem).toBeDefined();

      lookupCol?.onLookupSelect!(trackedItem!, row);

      expect(row.productId).toBe('prod-1');
      expect(row.productCode).toBe('STL-001');
      expect(row.trackingMode).toBe(1); // TrackingMode.Batch
      expect(row.trackingAllocations).toEqual([]);
    });

    it('should initialize trackingMode as 0 (None) for untracked products', () => {
      component.products.set([mockProducts[0], mockUntrackedProduct]);
      const row = component.defaultNewRow() as StockTransactionLineItem;
      const lookupCol = component.columns().find(c => c.field === 'productCode');

      const untrackedItem = component.productLookupItems().find(p => p.code === 'BLT-001');
      expect(untrackedItem).toBeDefined();

      lookupCol?.onLookupSelect!(untrackedItem!, row);

      expect(row.productId).toBe('prod-2');
      expect(row.trackingMode).toBe(0); // TrackingMode.None
      expect(row.trackingAllocations).toEqual([]);
    });

    it('should configure tracking details column instead of raw batch and serial columns in receipt mode', () => {
      const cols = component.columns();
      const fields = cols.map(c => c.field);

      expect(fields).toContain('trackingMode');
      expect(fields).not.toContain('batchNumber');
      expect(fields).not.toContain('serialNumber');
      expect(fields).not.toContain('manufacturingDate');
      expect(fields).not.toContain('expiryDate');

      const trackingCol = cols.find(c => c.field === 'trackingMode');
      expect(trackingCol?.header).toBe('Tracking Details');
      expect(trackingCol?.editable).toBe(false);
      expect(trackingCol?.cellTemplate).toBeDefined();
    });

    it('should configure tracking details column instead of raw batch and serial columns in issue mode', () => {
      component.transactionType = 'issue';
      fixture.detectChanges();

      const cols = component.columns();
      const fields = cols.map(c => c.field);

      expect(fields).not.toContain('batchNumber');
      expect(fields).not.toContain('serialNumber');
      expect(fields).toContain('trackingMode');

      const trackingCol = cols.find(c => c.field === 'trackingMode');
      expect(trackingCol?.header).toBe('Tracking Details');
      expect(trackingCol?.editable).toBe(false);
      expect(trackingCol?.cellTemplate).toBeDefined();
    });

    it('should invoke BatchSerialTrackingDialogComponent via MatDialog with proper dialog data', () => {
      const row: StockTransactionLineItem = {
        id: 'line-track-1',
        productId: 'prod-1',
        productCode: 'STL-001',
        productName: 'Galvanized Steel Sheet',
        unitOfMeasure: 'SHT',
        warehouseId: 'wh-1',
        warehouseLocationId: 'loc-1',
        availableStock: 0,
        loadingStock: false,
        quantity: 10,
        unitCost: 120,
        trackingMode: 1, // Batch
        trackingAllocations: []
      };

      const mockAllocations: BatchSerialAllocation[] = [
        {
          id: 'alloc-1',
          batchNumber: 'LOT-2026-A',
          quantity: 10,
          manufacturingDate: '2026-01-15T00:00:00Z',
          expiryDate: '2027-01-15T00:00:00Z'
        }
      ];

      const openSpy = vi.spyOn(matDialog, 'open').mockReturnValue({
        afterClosed: () => of(mockAllocations)
      } as any);

      component.openTrackingDialog(row);

      expect(openSpy).toHaveBeenCalledWith(
        BatchSerialTrackingDialogComponent,
        expect.objectContaining({
          width: '920px',
          disableClose: true,
          data: expect.objectContaining({
            productCode: 'STL-001',
            productName: 'Galvanized Steel Sheet',
            unitOfMeasure: 'SHT',
            trackingMode: 1,
            targetQuantity: 10,
            readOnly: false,
            allocations: []
          })
        })
      );

      expect(row.trackingAllocations).toEqual(mockAllocations);
    });

    it('should prevent opening tracking dialog if quantity is zero or invalid', () => {
      const row: StockTransactionLineItem = {
        id: 'line-zero-qty',
        productId: 'prod-1',
        productCode: 'STL-001',
        productName: 'Galvanized Steel Sheet',
        unitOfMeasure: 'SHT',
        warehouseId: 'wh-1',
        warehouseLocationId: 'loc-1',
        availableStock: 0,
        loadingStock: false,
        quantity: 0,
        unitCost: 120,
        trackingMode: 1,
        trackingAllocations: []
      };

      const openSpy = vi.spyOn(matDialog, 'open');
      component.openTrackingDialog(row);

      expect(openSpy).not.toHaveBeenCalled();
      expect(component.error()).toContain('Specify a quantity greater than 0');
    });

    it('should validate tracking completeness: incomplete allocations return isValid false', () => {
      const line: StockTransactionLineItem = {
        id: 'line-incomplete',
        productId: 'prod-1',
        productCode: 'STL-001',
        productName: 'Galvanized Steel Sheet',
        unitOfMeasure: 'SHT',
        warehouseId: 'wh-1',
        warehouseLocationId: 'loc-1',
        availableStock: 0,
        loadingStock: false,
        quantity: 10,
        unitCost: 120,
        trackingMode: 1, // Batch tracked
        trackingAllocations: [] // No allocations yet
      };

      component.lines.set([line]);
      expect(component.isLineTrackingComplete(line)).toBe(false);
      expect(component.isValid()).toBe(false);

      // Partial allocation (6 of 10)
      line.trackingAllocations = [
        {
          id: 'a1',
          batchNumber: 'BATCH-01',
          quantity: 6
        }
      ];
      expect(component.isLineTrackingComplete(line)).toBe(false);
      expect(component.isValid()).toBe(false);

      // Missing batch number
      line.trackingAllocations = [
        {
          id: 'a1',
          batchNumber: '',
          quantity: 10
        }
      ];
      expect(component.isLineTrackingComplete(line)).toBe(false);
      expect(component.isValid()).toBe(false);

      // Fully allocated (6 + 4 = 10 with valid batch numbers)
      const completeAllocs: BatchSerialAllocation[] = [
        {
          id: 'a1',
          batchNumber: 'BATCH-01',
          quantity: 6
        },
        {
          id: 'a2',
          batchNumber: 'BATCH-02',
          quantity: 4
        }
      ];
      line.trackingAllocations = completeAllocs;
      component.lines.set([{ ...line, trackingAllocations: completeAllocs }]);
      expect(component.isLineTrackingComplete(line)).toBe(true);
      expect(component.isValid()).toBe(true);
    });

    it('should allow posting untracked products without tracking allocations', () => {
      const untrackedLine: StockTransactionLineItem = {
        id: 'line-untracked',
        productId: 'prod-2',
        productCode: 'BLT-001',
        productName: 'M8 Hex Bolt',
        unitOfMeasure: 'PCS',
        warehouseId: 'wh-1',
        warehouseLocationId: 'loc-1',
        availableStock: 0,
        loadingStock: false,
        quantity: 50,
        unitCost: 2,
        trackingMode: 0, // Untracked
        trackingAllocations: []
      };

      component.lines.set([untrackedLine]);
      expect(component.isLineTrackingComplete(untrackedLine)).toBe(true);
      expect(component.isValid()).toBe(true);
    });

    it('should expand multiple batch allocations into individual CreateStockReceiptLineRequest entries upon posting', () => {
      const trackedLine: StockTransactionLineItem = {
        id: 'line-expand',
        productId: 'prod-1',
        productCode: 'STL-001',
        productName: 'Galvanized Steel Sheet',
        unitOfMeasure: 'SHT',
        warehouseId: 'wh-1',
        warehouseLocationId: 'loc-1',
        availableStock: 0,
        loadingStock: false,
        quantity: 10,
        unitCost: 120,
        remarks: 'Main line remark',
        trackingMode: 1,
        trackingAllocations: [
          {
            id: 'alloc-1',
            batchNumber: 'BATCH-ALPHA',
            quantity: 6,
            manufacturingDate: '2026-01-01T00:00:00Z',
            expiryDate: '2027-01-01T00:00:00Z',
            remarks: 'Sub-batch Alpha'
          },
          {
            id: 'alloc-2',
            batchNumber: 'BATCH-BETA',
            quantity: 4,
            manufacturingDate: '2026-02-01T00:00:00Z',
            expiryDate: '2027-02-01T00:00:00Z',
            remarks: 'Sub-batch Beta'
          }
        ]
      };

      const untrackedLine: StockTransactionLineItem = {
        id: 'line-single',
        productId: 'prod-2',
        productCode: 'BLT-001',
        productName: 'M8 Hex Bolt',
        unitOfMeasure: 'PCS',
        warehouseId: 'wh-1',
        warehouseLocationId: 'loc-1',
        availableStock: 0,
        loadingStock: false,
        quantity: 15,
        unitCost: 2.5,
        remarks: 'Standard hardware item',
        trackingMode: 0
      };

      component.lines.set([trackedLine, untrackedLine]);
      expect(component.isValid()).toBe(true);

      const postSpy = vi.spyOn(inventoryService, 'createStockReceipt').mockReturnValue(of(mockReceiptResult));
      component.postTransaction();

      expect(postSpy).toHaveBeenCalled();
      const payload: CreateStockReceiptRequest = postSpy.mock.calls[0][0];

      // Expanded from 2 form rows to 3 payload request lines (2 for batches + 1 for untracked)
      expect(payload.lines.length).toBe(3);

      // Allocation 1
      expect(payload.lines[0].productId).toBe('prod-1');
      expect(payload.lines[0].quantity).toBe(6);
      expect(payload.lines[0].batchNumber).toBe('BATCH-ALPHA');
      expect(payload.lines[0].unitCost).toBe(120);
      expect(payload.lines[0].remarks).toBe('Sub-batch Alpha');

      // Allocation 2
      expect(payload.lines[1].productId).toBe('prod-1');
      expect(payload.lines[1].quantity).toBe(4);
      expect(payload.lines[1].batchNumber).toBe('BATCH-BETA');
      expect(payload.lines[1].unitCost).toBe(120);
      expect(payload.lines[1].remarks).toBe('Sub-batch Beta');

      // Untracked line
      expect(payload.lines[2].productId).toBe('prod-2');
      expect(payload.lines[2].quantity).toBe(15);
      expect(payload.lines[2].batchNumber).toBeNull();
      expect(payload.lines[2].unitCost).toBe(2.5);
      expect(payload.lines[2].remarks).toBe('Standard hardware item');
    });

    it('should generate correct line tracking summary labels', () => {
      const line: StockTransactionLineItem = {
        id: 'line-summary',
        productId: 'prod-1',
        productCode: 'STL-001',
        productName: 'Steel',
        unitOfMeasure: 'SHT',
        warehouseId: 'wh-1',
        warehouseLocationId: 'loc-1',
        availableStock: 0,
        loadingStock: false,
        quantity: 10,
        trackingMode: 1, // Batch
        trackingAllocations: []
      };

      expect(component.getLineTrackingSummary(line)).toBe('Configure (0/10)');

      line.trackingAllocations = [{ id: 'a1', batchNumber: 'B1', quantity: 5 }];
      expect(component.getLineTrackingSummary(line)).toBe('Configure (5/10)');

      line.trackingAllocations = [
        { id: 'a1', batchNumber: 'B1', quantity: 5 },
        { id: 'a2', batchNumber: 'B2', quantity: 5 }
      ];
      expect(component.getLineTrackingSummary(line)).toBe('Batches (2) ✓');

      // Serial mode
      line.trackingMode = 2; // Serial
      line.quantity = 2;
      line.trackingAllocations = [
        { id: 's1', serialNumber: 'SN-01', quantity: 1 },
        { id: 's2', serialNumber: 'SN-02', quantity: 1 }
      ];
      expect(component.getLineTrackingSummary(line)).toBe('Serials (2) ✓');

      // Untracked mode
      line.trackingMode = 0;
      expect(component.getLineTrackingSummary(line)).toBe('Not Tracked');
    });

    it('should open tracking dialog with mode: issue, productId, and warehouse details when in issue mode', () => {
      component.transactionType = 'issue';
      fixture.detectChanges();

      const line: StockTransactionLineItem = {
        id: 'line-issue-track',
        productId: 'prod-1',
        productCode: 'STL-001',
        productName: 'Galvanized Steel Sheet',
        unitOfMeasure: 'SHT',
        warehouseId: 'wh-1',
        warehouseLocationId: 'loc-1',
        availableStock: 20,
        loadingStock: false,
        quantity: 5,
        trackingMode: 1, // Batch
        trackingAllocations: []
      };

      const dialogSpy = vi.spyOn(matDialog, 'open').mockReturnValue({
        afterClosed: () => of(null)
      } as any);

      component.openTrackingDialog(line);

      expect(dialogSpy).toHaveBeenCalled();
      const dialogCall = dialogSpy.mock.calls[0];
      const dialogData = dialogCall[1]?.data as any;

      expect(dialogData.dialogMode).toBe('issue');
      expect(dialogData.productId).toBe('prod-1');
      expect(dialogData.warehouseId).toBe('wh-1');
      expect(dialogData.warehouseLocationId).toBe('loc-1');
      expect(dialogData.targetQuantity).toBe(5);
    });

    it('should validate tracking completeness in issue mode and reject posting if incomplete', () => {
      component.transactionType = 'issue';
      fixture.detectChanges();

      const line: StockTransactionLineItem = {
        id: 'line-issue-incomplete',
        productId: 'prod-1',
        productCode: 'STL-001',
        productName: 'Galvanized Steel Sheet',
        unitOfMeasure: 'SHT',
        warehouseId: 'wh-1',
        warehouseLocationId: 'loc-1',
        availableStock: 20,
        loadingStock: false,
        quantity: 5,
        trackingMode: 1, // Batch
        trackingAllocations: []
      };

      component.lines.set([line]);
      // Tracking is incomplete (0/5 allocated) -> isValid must be false
      expect(component.isValid()).toBe(false);

      // Complete allocations -> isValid becomes true
      component.lines.set([
        {
          ...line,
          trackingAllocations: [
            { id: 'a1', batchNumber: 'LOT-A', quantity: 3 },
            { id: 'a2', batchNumber: 'LOT-B', quantity: 2 }
          ]
        }
      ]);
      expect(component.isValid()).toBe(true);
    });

    it('should expand multi-batch allocations into separate issue request lines upon posting', () => {
      component.transactionType = 'issue';
      fixture.detectChanges();

      const trackedLine: StockTransactionLineItem = {
        id: 'line-issue-1',
        productId: 'prod-1',
        productCode: 'STL-001',
        productName: 'Galvanized Steel Sheet',
        unitOfMeasure: 'SHT',
        warehouseId: 'wh-1',
        warehouseLocationId: 'loc-1',
        availableStock: 20,
        loadingStock: false,
        quantity: 10,
        trackingMode: 1, // Batch
        trackingAllocations: [
          { id: 'a1', batchNumber: 'LOT-ALPHA', quantity: 6, remarks: 'Alpha issue' },
          { id: 'a2', batchNumber: 'LOT-BETA', quantity: 4, remarks: 'Beta issue' }
        ]
      };

      const untrackedLine: StockTransactionLineItem = {
        id: 'line-issue-2',
        productId: 'prod-2',
        productCode: 'BLT-002',
        productName: 'Stainless Bolt M8',
        unitOfMeasure: 'PCS',
        warehouseId: 'wh-1',
        warehouseLocationId: 'loc-1',
        availableStock: 100,
        loadingStock: false,
        quantity: 15,
        remarks: 'Standard bolts',
        trackingMode: 0
      };

      component.lines.set([trackedLine, untrackedLine]);
      expect(component.isValid()).toBe(true);

      const mockIssueResult: StockIssueDto[] = [
        {
          stockIssueId: 'iss-result-1',
          issueNumber: 'ISS-2026-0001',
          issueDate: '2026-11-15T00:00:00Z',
          purpose: StockIssuePurpose.InternalUse,
          purposeName: 'Internal Use',
          movementType: 'Issue',
          status: StockIssueStatus.Posted,
          statusName: 'Posted',
          warehouseId: 'wh-1',
          warehouseCode: 'WH01',
          warehouseName: 'Main Facility',
          warehouseLocationId: 'loc-1',
          locationCode: 'R01-L01-B01',
          locationName: 'Bin 01',
          productId: 'prod-1',
          productCode: 'STL-001',
          productName: 'Galvanized Steel Sheet',
          unitOfMeasure: 'SHT',
          quantity: 10,
          unitCost: 120,
          totalCost: 1200,
          createdAt: '2026-11-15T00:00:00Z'
        }
      ];

      const postSpy = vi.spyOn(inventoryService, 'createStockIssue').mockReturnValue(of(mockIssueResult));
      component.postTransaction();

      expect(postSpy).toHaveBeenCalled();
      const payload: CreateStockIssueRequest = postSpy.mock.calls[0][0];

      // Expanded from 2 form rows to 3 payload request lines
      expect(payload.lines.length).toBe(3);

      // Allocation 1
      expect(payload.lines[0].productId).toBe('prod-1');
      expect(payload.lines[0].quantity).toBe(6);
      expect(payload.lines[0].batchNumber).toBe('LOT-ALPHA');
      expect(payload.lines[0].remarks).toBe('Alpha issue');

      // Allocation 2
      expect(payload.lines[1].productId).toBe('prod-1');
      expect(payload.lines[1].quantity).toBe(4);
      expect(payload.lines[1].batchNumber).toBe('LOT-BETA');
      expect(payload.lines[1].remarks).toBe('Beta issue');

      // Untracked line
      expect(payload.lines[2].productId).toBe('prod-2');
      expect(payload.lines[2].quantity).toBe(15);
      expect(payload.lines[2].batchNumber).toBeNull();
      expect(payload.lines[2].remarks).toBe('Standard bolts');
    });
  });
});
