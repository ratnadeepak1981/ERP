import { describe, it, expect, beforeEach, vi } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { TransferStockComponent } from './transfer-stock.component';
import { InventoryService } from '../../../../../core/services/inventory.service';
import { AuthService } from '../../../../../core/auth/auth.service';
import { InventoryRecentTransactionDto } from '../../../../../core/models/inventory.models';

describe('TransferStockComponent', () => {
  let component: TransferStockComponent;
  let fixture: ComponentFixture<TransferStockComponent>;
  let inventoryService: InventoryService;
  let authService: AuthService;

  const mockTxs: InventoryRecentTransactionDto[] = [
    {
      inventoryTransactionId: 'tx-1',
      transactionNumber: 'TRX-2026-0001',
      transactionDate: '2026-10-19T10:00:00Z',
      productId: 'p-1',
      productCode: 'STL-001',
      productName: 'Galvanized Steel Sheet',
      unitOfMeasure: 'SHT',
      warehouseId: 'wh-1',
      warehouseCode: 'MAIN',
      warehouseName: 'Central Logistics Hub',
      warehouseLocationId: 'loc-1',
      locationCode: 'A1-01',
      locationName: 'Bin A1',
      movementType: 'StockAdjustmentIn',
      movementTypeCode: 3,
      quantity: 50,
      unitCost: 120,
      totalCost: 6000,
      sourceDocumentId: 'INIT-01',
      sourceDocumentType: 'InitialBalance',
      createdAt: '2026-10-19T10:00:00Z'
    },
    {
      inventoryTransactionId: 'tx-2',
      transactionNumber: 'TRX-2026-0002',
      transactionDate: '2026-10-19T11:00:00Z',
      productId: 'p-2',
      productCode: 'ALU-002',
      productName: 'Aluminum Profile',
      unitOfMeasure: 'BAR',
      warehouseId: 'wh-1',
      warehouseCode: 'MAIN',
      warehouseName: 'Central Logistics Hub',
      warehouseLocationId: 'loc-2',
      locationCode: 'A2-02',
      locationName: 'Bin A2',
      movementType: 'ShipmentDispatch',
      movementTypeCode: 4,
      quantity: -10,
      unitCost: 80,
      totalCost: 800,
      sourceDocumentId: 'SO-108',
      sourceDocumentType: 'SalesOrder',
      createdAt: '2026-10-19T11:00:00Z'
    }
  ];

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [TransferStockComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(TransferStockComponent);
    component = fixture.componentInstance;
    inventoryService = TestBed.inject(InventoryService);
    authService = TestBed.inject(AuthService);

    vi.spyOn(authService, 'isTenantAdmin').mockReturnValue(true);
    vi.spyOn(inventoryService, 'getRecentTransactions').mockReturnValue(of(mockTxs));
  });

  it('should create and load transfer movements', () => {
    fixture.detectChanges();

    expect(component).toBeTruthy();
    expect(component.totalTransfers()).toBe(2);
    expect(component.totalQuantityMoved()).toBe(60);
  });

  it('should filter movements by pill filter and search term', () => {
    fixture.detectChanges();

    component.movementFilter.set('dispatch');
    expect(component.filteredTransactions().length).toBe(1);
    expect(component.filteredTransactions()[0].productCode).toBe('ALU-002');

    component.movementFilter.set('all');
    component.searchTerm.set('Steel');
    expect(component.filteredTransactions().length).toBe(1);
    expect(component.filteredTransactions()[0].productCode).toBe('STL-001');
  });
});
