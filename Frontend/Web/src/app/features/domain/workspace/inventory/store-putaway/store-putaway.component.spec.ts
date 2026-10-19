import { describe, it, expect, beforeEach, vi } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { StorePutawayComponent } from './store-putaway.component';
import { MasterDataService } from '../../../../../core/services/master-data.service';
import { AuthService } from '../../../../../core/auth/auth.service';
import { WarehouseItem, WarehouseLocationItem } from '../../../../../core/models/scope.models';

describe('StorePutawayComponent', () => {
  let component: StorePutawayComponent;
  let fixture: ComponentFixture<StorePutawayComponent>;
  let masterDataService: MasterDataService;
  let authService: AuthService;

  const mockWarehouses: WarehouseItem[] = [
    { warehouseId: 'wh-1', tenantId: 't-1', warehouseCode: 'MAIN', warehouseName: 'Main Warehouse', isActive: true }
  ];

  const mockLocations: WarehouseLocationItem[] = [
    {
      locationId: 'loc-1',
      warehouseId: 'wh-1',
      locationCode: 'A1-01',
      locationName: 'Receiving Dock 1',
      isReceivingLocation: true,
      isPickingLocation: false,
      isActive: true
    },
    {
      locationId: 'loc-2',
      warehouseId: 'wh-1',
      locationCode: 'B2-04',
      locationName: 'Primary Shelf 4',
      isReceivingLocation: false,
      isPickingLocation: true,
      isActive: true
    }
  ];

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [StorePutawayComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(StorePutawayComponent);
    component = fixture.componentInstance;
    masterDataService = TestBed.inject(MasterDataService);
    authService = TestBed.inject(AuthService);

    vi.spyOn(authService, 'isTenantAdmin').mockReturnValue(true);
    vi.spyOn(masterDataService, 'getWarehouses').mockReturnValue(of(mockWarehouses));
    vi.spyOn(masterDataService, 'getWarehouseLocations').mockReturnValue(of(mockLocations));
  });

  it('should create and load storage putaway locations', () => {
    fixture.detectChanges();

    expect(component).toBeTruthy();
    expect(component.totalWarehouses()).toBe(1);
    expect(component.totalLocations()).toBe(2);
    expect(component.receivingLocationsCount()).toBe(1);
    expect(component.pickingLocationsCount()).toBe(1);
  });

  it('should filter locations by search term', () => {
    fixture.detectChanges();

    component.searchTerm.set('Dock');
    expect(component.filteredLocations().length).toBe(1);
    expect(component.filteredLocations()[0].locationCode).toBe('A1-01');

    component.searchTerm.set('Shelf');
    expect(component.filteredLocations().length).toBe(1);
    expect(component.filteredLocations()[0].locationCode).toBe('B2-04');
  });
});
