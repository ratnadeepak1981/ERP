import { describe, it, expect, beforeEach, vi } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { StockLocationReportViewComponent } from './stock-location-report-view.component';
import { StockByStorageLocationComponent } from './stock-by-storage-location/stock-by-storage-location.component';
import { StockByStagingLocationComponent } from './stock-by-staging-location/stock-by-staging-location.component';
import { InventoryService } from '../../../../../core/services/inventory.service';
import { MasterDataService } from '../../../../../core/services/master-data.service';
import { WorkspaceScopeService } from '../../../../../core/services/workspace-scope.service';
import { AuthService } from '../../../../../core/auth/auth.service';
import { StockByLocationReportDto, StockByLocationRowDto } from '../../../../../core/models/inventory.models';

describe('StockLocationReportViewComponent', () => {
  let component: StockLocationReportViewComponent;
  let fixture: ComponentFixture<StockLocationReportViewComponent>;
  let inventoryService: InventoryService;
  let masterDataService: MasterDataService;
  let authService: AuthService;

  const mockRows: StockByLocationRowDto[] = [
    {
      inventoryBalanceId: 'ib-1',
      productId: 'p-1',
      productCode: 'PRD-001',
      productName: 'Widget Alpha',
      sku: 'SKU-001',
      categoryName: 'Raw Materials',
      unitOfMeasure: 'PCS',
      warehouseId: 'wh-1',
      warehouseCode: 'WH-MAIN',
      warehouseName: 'Main Warehouse',
      warehouseLocationId: 'loc-1',
      locationCode: 'R01-B01',
      locationName: 'Rack 1 Bin 1',
      hierarchyPath: 'WH-MAIN > Zone A > Rack 1 Bin 1',
      locationTypeCode: 'BIN',
      locationTypeName: 'Bin',
      locationClassification: 'Storage',
      batchNumber: '',
      serialNumber: '',
      quantityOnHand: 50,
      quantityAllocated: 5,
      quantityQuarantine: 0,
      quantityAvailable: 45,
      unitCost: 10,
      totalCostValue: 500,
      valuationMethod: 'StandardCost',
      currency: 'USD'
    }
  ];

  const mockReport: StockByLocationReportDto = {
    items: mockRows,
    totalCount: 1,
    totalQuantityOnHand: 50,
    totalQuantityAvailable: 45,
    totalStockValue: 500,
    currency: 'USD',
    page: 1,
    pageSize: 25
  };

  beforeEach(async () => {
    const invSpy = {
      getStockByLocationReport: vi.fn().mockReturnValue(of(mockReport))
    };

    const mdSpy = {
      getWarehouses: vi.fn().mockReturnValue(of([
        { warehouseId: 'wh-1', warehouseCode: 'WH-MAIN', warehouseName: 'Main Warehouse' }
      ])),
      getWarehouseLocations: vi.fn().mockReturnValue(of([
        { warehouseLocationId: 'loc-1', locationCode: 'R01-B01', locationName: 'Rack 1 Bin 1', warehouseId: 'wh-1' }
      ]))
    };

    const scopeSpy = {
      selectedCompany: signal(null),
      selectedBranch: signal(null),
      scopeChange$: of(null)
    };

    const authSpy = {
      hasPermission: vi.fn().mockReturnValue(true)
    };

    await TestBed.configureTestingModule({
      imports: [StockLocationReportViewComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: InventoryService, useValue: invSpy },
        { provide: MasterDataService, useValue: mdSpy },
        { provide: WorkspaceScopeService, useValue: scopeSpy },
        { provide: AuthService, useValue: authSpy }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(StockLocationReportViewComponent);
    component = fixture.componentInstance;
    component.mode = 'storage';
    inventoryService = TestBed.inject(InventoryService);
    masterDataService = TestBed.inject(MasterDataService);
    authService = TestBed.inject(AuthService);
  });

  it('should create the component and initialize storage mode', () => {
    expect(component).toBeTruthy();
    expect(component.pageTitle()).toBe('Stock by Storage Location');
    expect(component.isStorage()).toBe(true);
  });

  it('should load report on init with correct classification and warehouse filter', () => {
    fixture.detectChanges();
    expect(inventoryService.getStockByLocationReport).toHaveBeenCalledWith(
      expect.objectContaining({
        locationClassification: 'storage'
      })
    );
    expect(component.reportData().items.length).toBe(1);
    expect(component.reportData().totalStockValue).toBe(500);
    expect(component.reportData().totalQuantityOnHand).toBe(50);
    expect(component.reportData().totalQuantityAvailable).toBe(45);
  });

  it('should request Staging classification when mode is staging', () => {
    component.mode = 'staging';
    expect(component.pageTitle()).toBe('Stock by Staging Location');
    expect(component.isStorage()).toBe(false);

    component.loadReport();
    expect(inventoryService.getStockByLocationReport).toHaveBeenCalledWith(
      expect.objectContaining({
        locationClassification: 'staging'
      })
    );
  });

  it('should update location filter and reload report when warehouse changes', () => {
    fixture.detectChanges();
    component.onWarehouseChange('wh-1');
    expect(component.selectedWarehouseId()).toBe('wh-1');
    expect(masterDataService.getWarehouseLocations).toHaveBeenCalledWith('wh-1', null, true);
  });

  it('should trigger report reload when search term changes', () => {
    fixture.detectChanges();
    component.onSearch('Widget');
    expect(component.searchTerm()).toBe('Widget');
    expect(inventoryService.getStockByLocationReport).toHaveBeenCalledWith(
      expect.objectContaining({
        search: 'Widget'
      })
    );
  });

  it('should handle error gracefully and set loading to false', () => {
    vi.spyOn(inventoryService, 'getStockByLocationReport').mockReturnValue(
      throwError(() => new Error('Server error'))
    );
    component.loadReport();
    expect(component.loading()).toBe(false);
    expect(component.reportData().items.length).toBe(0);
  });
});

describe('Report Wrappers', () => {
  beforeEach(async () => {
    const invSpy = {
      getStockByLocationReport: vi.fn().mockReturnValue(of({ items: [], totalCount: 0, totalQuantityOnHand: 0, totalQuantityAvailable: 0, totalStockValue: 0, currency: 'USD', page: 1, pageSize: 25 }))
    };
    const mdSpy = {
      getWarehouses: vi.fn().mockReturnValue(of([])),
      getWarehouseLocations: vi.fn().mockReturnValue(of([]))
    };
    const scopeSpy = {
      selectedCompany: signal(null),
      selectedBranch: signal(null),
      scopeChange$: of(null)
    };
    const authSpy = {
      hasPermission: vi.fn().mockReturnValue(true)
    };

    await TestBed.configureTestingModule({
      imports: [StockByStorageLocationComponent, StockByStagingLocationComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: InventoryService, useValue: invSpy },
        { provide: MasterDataService, useValue: mdSpy },
        { provide: WorkspaceScopeService, useValue: scopeSpy },
        { provide: AuthService, useValue: authSpy }
      ]
    }).compileComponents();
  });

  it('should create StockByStorageLocationComponent with storage mode', () => {
    const storageFixture = TestBed.createComponent(StockByStorageLocationComponent);
    expect(storageFixture.componentInstance).toBeTruthy();
  });

  it('should create StockByStagingLocationComponent with staging mode', () => {
    const stagingFixture = TestBed.createComponent(StockByStagingLocationComponent);
    expect(stagingFixture.componentInstance).toBeTruthy();
  });
});
