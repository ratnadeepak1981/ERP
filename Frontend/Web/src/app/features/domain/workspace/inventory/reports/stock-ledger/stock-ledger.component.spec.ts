import { describe, it, expect, beforeEach, vi } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter, ActivatedRoute } from '@angular/router';
import { of, throwError } from 'rxjs';
import { StockLedgerComponent } from './stock-ledger.component';
import { InventoryService } from '../../../../../../core/services/inventory.service';
import { MasterDataService } from '../../../../../../core/services/master-data.service';
import { WorkspaceScopeService } from '../../../../../../core/services/workspace-scope.service';
import { AuthService } from '../../../../../../core/auth/auth.service';
import { StockLedgerReportDto, StockLedgerRowDto } from '../../../../../../core/models/inventory.models';

describe('StockLedgerComponent', () => {
  let component: StockLedgerComponent;
  let fixture: ComponentFixture<StockLedgerComponent>;
  let inventoryService: InventoryService;
  let masterDataService: MasterDataService;
  let scopeService: any;
  let authService: AuthService;

  const mockLedgerRow: StockLedgerRowDto = {
    inventoryTransactionId: 'tx-1',
    transactionDate: '2026-03-01T10:00:00Z',
    postingDate: '2026-03-01T10:00:00Z',
    transactionType: 'InternalTransferIn',
    movementTypeCode: 3,
    documentReference: 'PA-2026-0001',
    sourceDocumentType: 'Putaway',
    sourceDocumentId: 'pa-1',
    sourceDocumentLineId: 'pal-1',
    productCode: 'PRD-RAW-001',
    productName: 'Steel Ingot Grade A',
    unitOfMeasure: 'KG',
    warehouseCode: 'WH-MAIN',
    warehouseName: 'Main Central Facility',
    locationCode: 'R01-S01-B01',
    locationName: 'Rack 1 Shelf 1 Bin 1',
    sourceLocationCode: 'STG-RCV-01',
    destinationLocationCode: 'R01-S01-B01',
    batchNumber: 'LOT-202603',
    serialNumber: null,
    quantityIn: 100,
    quantityOut: 0,
    costRate: 45.5,
    inwardValue: 4550,
    outwardValue: 0,
    runningQuantity: 100,
    runningValue: 4550,
    valuationMethod: 'FIFO',
    currency: 'LKR'
  };

  const mockLedgerReport: StockLedgerReportDto = {
    productId: 'p-1',
    productCode: 'PRD-RAW-001',
    productName: 'Steel Ingot Grade A',
    unitOfMeasure: 'KG',
    warehouseId: 'wh-1',
    warehouseCode: 'WH-MAIN',
    warehouseName: 'Main Central Facility',
    warehouseLocationId: 'loc-1',
    locationCode: 'R01-S01-B01',
    locationName: 'Rack 1 Shelf 1 Bin 1',
    hierarchyPath: 'WH-MAIN > Zone A > Rack 1 Shelf 1 Bin 1',
    valuationMethod: 'FIFO',
    currency: 'LKR',
    openingQuantity: 0,
    openingValue: 0,
    totalQuantityIn: 100,
    totalInwardValue: 4550,
    totalQuantityOut: 0,
    totalOutwardValue: 0,
    closingQuantity: 100,
    closingValue: 4550,
    items: [mockLedgerRow],
    totalCount: 1,
    page: 1,
    pageSize: 1000
  };

  beforeEach(async () => {
    const invSpy = {
      getStockLedgerReport: vi.fn().mockReturnValue(of(mockLedgerReport))
    };

    const mdSpy = {
      getProducts: vi.fn().mockReturnValue(of([
        { productId: 'p-1', productCode: 'PRD-RAW-001', productName: 'Steel Ingot Grade A', unitOfMeasure: 'KG' }
      ])),
      getWarehouses: vi.fn().mockReturnValue(of([
        { warehouseId: 'wh-1', warehouseCode: 'WH-MAIN', warehouseName: 'Main Central Facility', companyId: 'comp-1', branchId: 'br-1' }
      ])),
      getWarehouseLocations: vi.fn().mockReturnValue(of([
        { warehouseLocationId: 'loc-1', locationCode: 'R01-S01-B01', locationName: 'Rack 1 Shelf 1 Bin 1', warehouseId: 'wh-1' }
      ]))
    };

    scopeService = {
      selectedCompany: signal({ id: 'comp-1', name: 'ApexFlow Industries', code: 'AFI' }),
      selectedBranch: signal({ id: 'br-1', name: 'Colombo Main Plant', code: 'COL-01' }),
      isAllBranches: signal(false),
      scopeChange$: of(null)
    };

    const authSpy = {
      hasPermission: vi.fn().mockReturnValue(true),
      currentUser: signal({ id: 'u-1', email: 'user@example.com', roles: ['User'] }),
      roles: signal(['User']),
      isTenantAdmin: signal(false),
      isPlatformAdmin: signal(false),
      isPlatformStaff: signal(false)
    };

    await TestBed.configureTestingModule({
      imports: [StockLedgerComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: {
            queryParams: of({})
          }
        },
        { provide: InventoryService, useValue: invSpy },
        { provide: MasterDataService, useValue: mdSpy },
        { provide: WorkspaceScopeService, useValue: scopeService },
        { provide: AuthService, useValue: authSpy }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(StockLedgerComponent);
    component = fixture.componentInstance;
    inventoryService = TestBed.inject(InventoryService);
    masterDataService = TestBed.inject(MasterDataService);
    authService = TestBed.inject(AuthService);
  });

  it('should create the component with initial state and master data', () => {
    expect(component).toBeTruthy();
    fixture.detectChanges();
    expect(component.currentCompanyName()).toContain('ApexFlow Industries');
    expect(component.currentBranchName()).toContain('Colombo Main Plant');
    expect(component.hasRequiredFilters()).toBe(false);
    expect(component.locations()[0].label).toContain('All Locations');
  });

  it('should display prompt state when product or warehouse is not selected', () => {
    fixture.detectChanges();
    expect(component.hasRequiredFilters()).toBe(false);
    expect(inventoryService.getStockLedgerReport).not.toHaveBeenCalled();
  });

  it('should load report with All Locations by default when Product and Warehouse are selected', () => {
    fixture.detectChanges();

    component.onProductChange('p-1');
    component.onWarehouseChange('wh-1');

    expect(component.hasRequiredFilters()).toBe(true);
    expect(component.selectedLocationId()).toBe('');
    expect(inventoryService.getStockLedgerReport).toHaveBeenCalledWith(
      expect.objectContaining({
        productId: 'p-1',
        warehouseId: 'wh-1',
        warehouseLocationId: '',
        companyId: 'comp-1',
        branchId: 'br-1'
      })
    );

    expect(component.reportData()).toEqual(mockLedgerReport);
    expect(component.closingQuantity()).toBe(100);
    expect(component.closingValue()).toBe(4550);
    expect(component.valuationMethod()).toBe('FIFO');
  });

  it('should load report for specific location when selected', () => {
    fixture.detectChanges();

    component.onProductChange('p-1');
    component.onWarehouseChange('wh-1');
    component.onLocationChange('loc-1');

    expect(component.hasRequiredFilters()).toBe(true);
    expect(component.selectedLocationId()).toBe('loc-1');
    expect(inventoryService.getStockLedgerReport).toHaveBeenCalledWith(
      expect.objectContaining({
        productId: 'p-1',
        warehouseId: 'wh-1',
        warehouseLocationId: 'loc-1',
        companyId: 'comp-1',
        branchId: 'br-1'
      })
    );
  });

  it('should omit branchId when isAllBranches is true', () => {
    scopeService.isAllBranches.set(true);
    fixture.detectChanges();

    component.onProductChange('p-1');
    component.onWarehouseChange('wh-1');

    expect(inventoryService.getStockLedgerReport).toHaveBeenCalledWith(
      expect.objectContaining({
        productId: 'p-1',
        warehouseId: 'wh-1',
        companyId: 'comp-1'
      })
    );
    const lastCall = vi.mocked(inventoryService.getStockLedgerReport).mock.calls[0][0];
    expect(lastCall.branchId).toBeUndefined();
  });

  it('should handle workspace change and filter warehouses', () => {
    fixture.detectChanges();
    component.selectedWarehouseId.set('wh-other');

    // Simulate workspace change
    scopeService.selectedCompany.set({ id: 'comp-99', name: 'Other Co', code: 'OTH' });
    component.handleWorkspaceChange();

    // Since wh-other doesn't belong to comp-99, selected warehouse is cleared
    expect(component.selectedWarehouseId()).toBe('');
    expect(component.hasRequiredFilters()).toBe(false);
  });

  it('should display error message when service call fails', () => {
    vi.spyOn(inventoryService, 'getStockLedgerReport').mockReturnValue(
      throwError(() => ({ status: 500, message: 'Server error occurred' }))
    );

    fixture.detectChanges();
    component.selectedProductId.set('p-1');
    component.selectedWarehouseId.set('wh-1');
    component.selectedLocationId.set('loc-1');

    component.loadLedger();

    expect(component.loading()).toBe(false);
    expect(component.error()).toBeTruthy();
  });

  it('should handle access denied when user lacks INVENTORY.VIEW permission', () => {
    vi.spyOn(authService, 'hasPermission').mockReturnValue(false);

    const deniedFixture = TestBed.createComponent(StockLedgerComponent);
    const deniedComp = deniedFixture.componentInstance;
    deniedFixture.detectChanges();

    expect(deniedComp.isAccessDenied()).toBe(true);
  });
});
