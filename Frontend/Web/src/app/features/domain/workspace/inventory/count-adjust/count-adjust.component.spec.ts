import { describe, it, expect, beforeEach, vi } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { CountAdjustComponent } from './count-adjust.component';
import { MasterDataService } from '../../../../../core/services/master-data.service';
import { AuthService } from '../../../../../core/auth/auth.service';
import { ProductItem } from '../../../../../core/models/scope.models';

describe('CountAdjustComponent', () => {
  let component: CountAdjustComponent;
  let fixture: ComponentFixture<CountAdjustComponent>;
  let masterDataService: MasterDataService;
  let authService: AuthService;

  const mockProducts: ProductItem[] = [
    {
      productId: 'p-1',
      tenantId: 't-1',
      productCode: 'STL-001',
      productName: 'Galvanized Steel Sheet',
      categoryId: 'c-1',
      categoryName: 'Raw Materials',
      unitOfMeasure: 'SHT',
      isActive: true,
      isStockTracked: true,
      isManufacturable: false,
      isPurchasable: true,
      isSellable: true,
      canConsumeInProduction: true,
      canConsumeInMaintenance: true,
      productType: 1,
      trackingMode: 1,
      valuationMethod: 1,
      standardCost: 120,
      sellingPrice: 160,
      reorderLevel: 25,
      reorderQuantity: 50
    },
    {
      productId: 'p-2',
      tenantId: 't-1',
      productCode: 'ALU-002',
      productName: 'Aluminum Profile',
      categoryId: 'c-1',
      categoryName: 'Raw Materials',
      unitOfMeasure: 'BAR',
      isActive: true,
      isStockTracked: true,
      isManufacturable: false,
      isPurchasable: true,
      isSellable: true,
      canConsumeInProduction: true,
      canConsumeInMaintenance: true,
      productType: 1,
      trackingMode: 1,
      valuationMethod: 1,
      standardCost: 80,
      sellingPrice: 110,
      reorderLevel: 0,
      reorderQuantity: 20
    }
  ];

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CountAdjustComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(CountAdjustComponent);
    component = fixture.componentInstance;
    masterDataService = TestBed.inject(MasterDataService);
    authService = TestBed.inject(AuthService);

    vi.spyOn(authService, 'isTenantAdmin').mockReturnValue(true);
    vi.spyOn(masterDataService, 'getProducts').mockReturnValue(of(mockProducts));
  });

  it('should create and load count catalogue', () => {
    fixture.detectChanges();

    expect(component).toBeTruthy();
    expect(component.totalProducts()).toBe(2);
    expect(component.lowStockCount()).toBe(1);
    expect(component.healthyCount()).toBe(1);
  });

  it('should filter items by review status and search term', () => {
    fixture.detectChanges();

    component.statusFilter.set('low-stock');
    expect(component.filteredProducts().length).toBe(1);
    expect(component.filteredProducts()[0].productCode).toBe('STL-001');

    component.statusFilter.set('healthy');
    expect(component.filteredProducts().length).toBe(1);
    expect(component.filteredProducts()[0].productCode).toBe('ALU-002');

    component.statusFilter.set('all');
    component.searchTerm.set('Steel');
    expect(component.filteredProducts().length).toBe(1);
    expect(component.filteredProducts()[0].productCode).toBe('STL-001');
  });
});
