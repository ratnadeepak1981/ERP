import { describe, it, expect, beforeEach, vi } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter, Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import { InventoryOverviewComponent } from './inventory-overview.component';
import { InventoryService } from '../../../../core/services/inventory.service';
import { InventorySummaryDto, InventoryRecentTransactionDto } from '../../../../core/models/inventory.models';
import { AuthService } from '../../../../core/auth/auth.service';

describe('InventoryOverviewComponent', () => {
  let component: InventoryOverviewComponent;
  let fixture: ComponentFixture<InventoryOverviewComponent>;
  let inventoryService: InventoryService;
  let router: Router;
  let authService: AuthService;

  const mockSummary: InventorySummaryDto = {
    totalStockItems: 38,
    totalWarehouses: 4,
    totalLocations: 16,
    lowStockItems: 3,
    totalTransactions: 120,
    totalOnHandQuantity: 4500,
    totalInventoryValue: 1250000
  };

  const mockTransactions: InventoryRecentTransactionDto[] = [
    {
      inventoryTransactionId: 'tx-1',
      transactionNumber: 'TX-2026-001',
      movementType: 'GoodsReceipt',
      movementTypeCode: 1,
      sourceDocumentType: 'GoodsReceiptNote',
      sourceDocumentId: 'grn-1',
      productId: 'prod-1',
      productCode: 'STL-001',
      productName: 'Galvanized Steel Sheet',
      unitOfMeasure: 'SHT',
      warehouseId: 'wh-1',
      warehouseCode: 'MAIN',
      warehouseName: 'Central Logistics Hub',
      warehouseLocationId: 'loc-1',
      locationCode: 'A1-01',
      locationName: 'Bay 1 Rack A',
      quantity: 50,
      unitCost: 120,
      totalCost: 6000,
      batchNumber: 'LOT-99',
      serialNumber: null,
      transactionDate: '2026-10-18T10:00:00Z',
      createdAt: '2026-10-18T10:00:00Z'
    },
    {
      inventoryTransactionId: 'tx-2',
      transactionNumber: 'TX-2026-002',
      movementType: 'InternalTransferIn',
      movementTypeCode: 3,
      sourceDocumentType: 'StockTransferOrder',
      sourceDocumentId: 'to-1',
      productId: 'prod-2',
      productCode: 'ALU-002',
      productName: 'Aluminum Profile 2m',
      unitOfMeasure: 'BAR',
      warehouseId: 'wh-2',
      warehouseCode: 'PLANT',
      warehouseName: 'Fabrication Plant',
      warehouseLocationId: 'loc-2',
      locationCode: 'B2-04',
      locationName: 'Raw Bin 4',
      quantity: 25,
      unitCost: 80,
      totalCost: 2000,
      batchNumber: null,
      serialNumber: null,
      transactionDate: '2026-10-17T14:30:00Z',
      createdAt: '2026-10-17T14:30:00Z'
    }
  ];

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [InventoryOverviewComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(InventoryOverviewComponent);
    component = fixture.componentInstance;
    inventoryService = TestBed.inject(InventoryService);
    router = TestBed.inject(Router);
    authService = TestBed.inject(AuthService);

    vi.spyOn(authService, 'isTenantAdmin').mockReturnValue(true);
    vi.spyOn(router, 'navigate').mockResolvedValue(true);
    vi.spyOn(inventoryService, 'getSummary').mockReturnValue(of(mockSummary));
    vi.spyOn(inventoryService, 'getRecentTransactions').mockReturnValue(of(mockTransactions));
  });

  it('should create component and load overview data from real API service', () => {
    fixture.detectChanges();

    expect(component).toBeTruthy();
    expect(component.summary().totalStockItems).toBe(38);
    expect(component.summary().totalWarehouses).toBe(4);
    expect(component.summary().lowStockItems).toBe(3);
    expect(component.transactions().length).toBe(2);

    const compiled = fixture.nativeElement as HTMLElement;
    const cards = compiled.querySelectorAll('.card-metric-value');
    expect(cards.length).toBeGreaterThanOrEqual(4);
  });

  it('should render KPI metric cards with correct values and colors', () => {
    fixture.detectChanges();

    expect(component.totalStockItems()).toBe(38);
    expect(component.totalWarehouses()).toBe(4);
    expect(component.lowStockItems()).toBe(3);
    expect(component.totalTransactions()).toBe(120);

    const config = component.overviewConfig();
    expect(config.dashboardCards.length).toBe(4);
    expect(config.dashboardCards[0].title).toBe('Stock Items');
    expect(config.dashboardCards[0].value).toBe(38);
    expect(config.dashboardCards[2].title).toBe('Low Stock Alerts');
    expect(config.dashboardCards[2].colorVariant).toBe('amber');
  });

  it('should render process flow stages matching operational inventory screens and goods receipt', () => {
    fixture.detectChanges();

    const config = component.overviewConfig();
    expect(config.processFlow).toBeDefined();
    expect(config.processFlow?.stages.length).toBe(6);

    const grnStage = config.processFlow?.stages.find(s => s.id === 'inbound-grn');
    expect(grnStage?.label).toBe('Goods Receiving (GRN)');
    expect(grnStage?.route).toBe('/app/procurement/goods-receipts');
    expect(grnStage?.isAlternative).toBe(true);

    const receiptStage = config.processFlow?.stages.find(s => s.id === 'stock-receipt');
    expect(receiptStage?.label).toBe('Stock Receipt');
    expect(receiptStage?.route).toBe('/app/inventory/stock-receipts');
    expect(receiptStage?.isAlternative).toBe(true);

    const putawayStage = config.processFlow?.stages.find(s => s.id === 'store-putaway');
    expect(putawayStage?.label).toBe('Storage & Putaway');
    expect(putawayStage?.route).toBe('/app/inventory/store-putaway');
    expect(putawayStage?.isOptional).toBe(true);

    const transferStage = config.processFlow?.stages.find(s => s.id === 'transfer-stock');
    expect(transferStage?.label).toBe('Stock Transfers');
    expect(transferStage?.route).toBe('/app/inventory/transfers');

    const countStage = config.processFlow?.stages.find(s => s.id === 'count-adjust');
    expect(countStage?.label).toBe('Stock Count & Adjustment');
    expect(countStage?.route).toBe('/app/inventory/count-adjust');
    expect(countStage?.isConditional).toBe(true);

    const issueStage = config.processFlow?.stages.find(s => s.id === 'issue-stock');
    expect(issueStage?.label).toBe('Stock Issue');
    expect(issueStage?.route).toBe('/app/inventory/issue-stock');
  });

  it('should filter recent transactions by search term and movement type', () => {
    fixture.detectChanges();

    expect(component.recentTransactions().length).toBe(2);

    // Filter by movement type
    component.recentMovementFilter.set('internaltransferin');
    expect(component.recentTransactions().length).toBe(1);
    expect(component.recentTransactions()[0].productCode).toBe('ALU-002');

    // Reset movement filter and search by keyword
    component.recentMovementFilter.set('all');
    component.recentSearchTerm.set('Steel');
    expect(component.recentTransactions().length).toBe(1);
    expect(component.recentTransactions()[0].productCode).toBe('STL-001');
  });

  it('should respect configurable recentLimit', () => {
    expect(component.recentLimit).toBe(10);

    const manyTransactions: InventoryRecentTransactionDto[] = Array.from({ length: 15 }, (_, i) => ({
      inventoryTransactionId: `tx-${i + 1}`,
      transactionNumber: `TX-100${i}`,
      movementType: 'GoodsReceipt',
      movementTypeCode: 1,
      sourceDocumentType: 'GoodsReceiptNote',
      sourceDocumentId: `grn-${i + 1}`,
      productId: `prod-${i + 1}`,
      productCode: `ITEM-${i + 1}`,
      productName: `Product ${i + 1}`,
      unitOfMeasure: 'PCS',
      warehouseId: 'wh-1',
      warehouseCode: 'MAIN',
      warehouseName: 'Main Warehouse',
      warehouseLocationId: 'loc-1',
      locationCode: 'LOC-1',
      locationName: 'Location 1',
      quantity: 10 * (i + 1),
      unitCost: 10,
      totalCost: 100 * (i + 1),
      batchNumber: null,
      serialNumber: null,
      transactionDate: '2026-10-18T10:00:00Z',
      createdAt: '2026-10-18T10:00:00Z'
    }));

    component.transactions.set(manyTransactions);
    expect(component.recentTransactions().length).toBe(10);

    component.recentLimit = 5;
    expect(component.recentTransactions().length).toBe(5);
  });

  it('should handle API 403 access denied gracefully', () => {
    vi.spyOn(inventoryService, 'getSummary').mockReturnValue(
      throwError(() => ({ status: 403, message: 'Forbidden' }))
    );

    component.loadOverviewData();

    expect(component.isAccessDenied()).toBe(true);
  });

  it('should navigate to create stock receipt when createAction is triggered', () => {
    component.navigateToCreate();
    expect(router.navigate).toHaveBeenCalledWith(['/app/inventory/stock-receipts/new']);
  });

  it('should navigate to valid card route when card is clicked', () => {
    component.onCardClick({
      id: 'stockItems',
      title: 'Stock Items',
      icon: 'pi pi-box',
      value: 10,
      route: '/app/master-data/products'
    });
    expect(router.navigate).toHaveBeenCalledWith(['/app/master-data/products'], { queryParams: undefined });
  });
});
