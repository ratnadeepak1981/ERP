import { describe, it, expect, beforeEach, vi } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter, Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import { StockReceiptRegisterComponent } from './stock-receipt-register.component';
import { InventoryService } from '../../../../../core/services/inventory.service';
import {
  StockReceiptDto,
  StockReceiptReason,
  StockReceiptStatus
} from '../../../../../core/models/inventory.models';
import { AuthService } from '../../../../../core/auth/auth.service';

describe('StockReceiptRegisterComponent', () => {
  let component: StockReceiptRegisterComponent;
  let fixture: ComponentFixture<StockReceiptRegisterComponent>;
  let inventoryService: InventoryService;
  let router: Router;
  let authService: AuthService;

  const mockReceipts: StockReceiptDto[] = [
    {
      stockReceiptId: 'receipt-1',
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
      locationCode: 'A1-01',
      locationName: 'Bin A1',
      productId: 'prod-1',
      productCode: 'STL-001',
      productName: 'Galvanized Steel Sheet',
      unitOfMeasure: 'SHT',
      quantity: 50,
      unitCost: 120,
      totalCost: 6000,
      sourceDocumentReference: 'OB-2026-01',
      remarks: 'Initial stock load',
      createdAt: '2026-10-19T10:00:00Z'
    },
    {
      stockReceiptId: 'receipt-2',
      receiptNumber: 'REC-2026-0002',
      receiptDate: '2026-10-19T11:00:00Z',
      reason: StockReceiptReason.CustomerReturn,
      reasonName: 'Customer Sales Return',
      movementType: 'StockAdjustmentIn',
      status: StockReceiptStatus.Posted,
      statusName: 'Posted',
      warehouseId: 'wh-1',
      warehouseCode: 'MAIN',
      warehouseName: 'Central Logistics Hub',
      warehouseLocationId: 'loc-2',
      locationCode: 'A2-02',
      locationName: 'Bin A2',
      productId: 'prod-2',
      productCode: 'ALU-002',
      productName: 'Aluminum Profile',
      unitOfMeasure: 'BAR',
      quantity: 10,
      unitCost: 80,
      totalCost: 800,
      sourceDocumentReference: 'RMA-108',
      remarks: 'Customer exchange return',
      createdAt: '2026-10-19T11:00:00Z'
    }
  ];

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [StockReceiptRegisterComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(StockReceiptRegisterComponent);
    component = fixture.componentInstance;
    inventoryService = TestBed.inject(InventoryService);
    router = TestBed.inject(Router);
    authService = TestBed.inject(AuthService);

    vi.spyOn(authService, 'isTenantAdmin').mockReturnValue(true);
    vi.spyOn(router, 'navigate').mockResolvedValue(true);
    vi.spyOn(inventoryService, 'getStockReceipts').mockReturnValue(of(mockReceipts));
  });

  it('should create component and load stock receipts', () => {
    fixture.detectChanges();
    expect(component).toBeTruthy();
    expect(component.receipts().length).toBe(2);
    expect(component.loading()).toBe(false);
  });

  it('should filter stock receipts by reason', () => {
    fixture.detectChanges();
    component.activeReasonFilter.set('opening');
    expect(component.filteredReceipts().length).toBe(1);
    expect(component.filteredReceipts()[0].receiptNumber).toBe('REC-2026-0001');

    component.activeReasonFilter.set('return');
    expect(component.filteredReceipts().length).toBe(1);
    expect(component.filteredReceipts()[0].receiptNumber).toBe('REC-2026-0002');

    component.activeReasonFilter.set('production');
    expect(component.filteredReceipts().length).toBe(0);
  });

  it('should filter stock receipts by search term', () => {
    fixture.detectChanges();
    component.searchTerm.set('ALU-002');
    expect(component.filteredReceipts().length).toBe(1);
    expect(component.filteredReceipts()[0].productName).toBe('Aluminum Profile');
  });

  it('should navigate to create stock receipt page', () => {
    const navigateSpy = vi.spyOn(router, 'navigate');
    component.navigateToCreate();
    expect(navigateSpy).toHaveBeenCalledWith(['/app/inventory/stock-receipts/new']);
  });

  it('should navigate to stock receipt detail page', () => {
    const navigateSpy = vi.spyOn(router, 'navigate');
    component.navigateToReceipt(mockReceipts[0]);
    expect(navigateSpy).toHaveBeenCalledWith(['/app/inventory/stock-receipts', 'receipt-1']);
  });

  it('should handle 403 access denied gracefully', () => {
    vi.spyOn(inventoryService, 'getStockReceipts').mockReturnValue(
      throwError(() => ({ status: 403, message: 'Forbidden' }))
    );
    component.loadStockReceipts();
    expect(component.isAccessDenied()).toBe(true);
    expect(component.loading()).toBe(false);
  });
});
