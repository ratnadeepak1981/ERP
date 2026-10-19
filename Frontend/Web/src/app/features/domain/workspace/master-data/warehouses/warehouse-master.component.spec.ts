import { describe, it, expect, beforeEach, vi } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { WarehouseMasterComponent } from './warehouse-master.component';
import { MasterDataService } from '../../../../../core/services/master-data.service';
import { AuthService } from '../../../../../core/auth/auth.service';
import { WorkspaceScopeService } from '../../../../../core/services/workspace-scope.service';
import { of, throwError } from 'rxjs';
import {
  WarehouseItem,
  WarehouseZoneItem,
  WarehouseLocationItem,
  LocationTypeItem
} from '../../../../../core/models/scope.models';

describe('WarehouseMasterComponent', () => {
  let component: WarehouseMasterComponent;
  let fixture: ComponentFixture<WarehouseMasterComponent>;
  let masterDataService: MasterDataService;
  let authService: AuthService;

  const mockWarehouses: WarehouseItem[] = [
    {
      warehouseId: 'wh-1',
      tenantId: 'tenant-1',
      warehouseCode: 'WH-MAIN',
      warehouseName: 'Main Logistics Center',
      description: 'Primary central warehouse',
      city: 'London',
      addressLine1: '100 Industrial Park',
      postalCode: 'E1 6AN',
      country: 'UK',
      isActive: true,
      zonesCount: 2,
      locationsCount: 5
    },
    {
      warehouseId: 'wh-2',
      tenantId: 'tenant-1',
      warehouseCode: 'WH-NORTH',
      warehouseName: 'Northern Distribution Hub',
      description: 'Regional distribution hub',
      city: 'Manchester',
      addressLine1: '20 Northern Way',
      postalCode: 'M1 1AA',
      country: 'UK',
      isActive: true,
      zonesCount: 1,
      locationsCount: 2
    }
  ];

  const mockZones: WarehouseZoneItem[] = [
    {
      warehouseZoneId: 'zone-1',
      warehouseId: 'wh-1',
      zoneCode: 'BULK',
      zoneName: 'Bulk Storage Zone',
      description: 'Pallet bulk racking',
      isActive: true,
      storageLocationsCount: 3
    },
    {
      warehouseZoneId: 'zone-2',
      warehouseId: 'wh-1',
      zoneCode: 'PICK',
      zoneName: 'Fast-Pick Area',
      description: 'High velocity picking bins',
      isActive: true,
      storageLocationsCount: 2
    }
  ];

  const mockLocations: WarehouseLocationItem[] = [
    {
      locationId: 'loc-1',
      warehouseLocationId: 'loc-1',
      warehouseId: 'wh-1',
      warehouseZoneId: 'zone-1',
      zoneName: 'Bulk Storage Zone',
      locationCode: 'R01',
      locationName: 'Rack 01',
      locationTypeId: 'lt-rack',
      locationTypeCode: 'RACK',
      locationTypeName: 'Rack',
      parentLocationId: null,
      parentLocationCode: null,
      hierarchyPath: 'R01',
      hierarchyLevel: 1,
      isLeaf: false,
      canStoreInventory: true,
      canPick: false,
      canPutAway: true,
      isActive: true
    },
    {
      locationId: 'loc-2',
      warehouseLocationId: 'loc-2',
      warehouseId: 'wh-1',
      warehouseZoneId: 'zone-1',
      zoneName: 'Bulk Storage Zone',
      locationCode: 'R01-S01',
      locationName: 'Shelf 01',
      locationTypeId: 'lt-shelf',
      locationTypeCode: 'SHELF',
      locationTypeName: 'Shelf',
      parentLocationId: 'loc-1',
      parentLocationCode: 'R01',
      hierarchyPath: 'R01 > R01-S01',
      hierarchyLevel: 2,
      isLeaf: false,
      canStoreInventory: true,
      canPick: true,
      canPutAway: true,
      isActive: true
    },
    {
      locationId: 'loc-3',
      warehouseLocationId: 'loc-3',
      warehouseId: 'wh-1',
      warehouseZoneId: 'zone-1',
      zoneName: 'Bulk Storage Zone',
      locationCode: 'R01-S01-B01',
      locationName: 'Bin 01',
      locationTypeId: 'lt-bin',
      locationTypeCode: 'BIN',
      locationTypeName: 'Bin',
      parentLocationId: 'loc-2',
      parentLocationCode: 'R01-S01',
      hierarchyPath: 'R01 > R01-S01 > R01-S01-B01',
      hierarchyLevel: 3,
      isLeaf: true,
      canStoreInventory: true,
      canPick: true,
      canPutAway: true,
      isActive: true
    },
    {
      locationId: 'loc-4',
      warehouseLocationId: 'loc-4',
      warehouseId: 'wh-1',
      warehouseZoneId: 'zone-2',
      zoneName: 'Fast-Pick Area',
      locationCode: 'PK-01',
      locationName: 'Pick Bin 01',
      locationTypeId: 'lt-bin',
      locationTypeCode: 'BIN',
      locationTypeName: 'Bin',
      parentLocationId: null,
      parentLocationCode: null,
      hierarchyPath: 'PK-01',
      hierarchyLevel: 1,
      isLeaf: true,
      canStoreInventory: true,
      canPick: true,
      canPutAway: true,
      isActive: false
    }
  ];

  const mockLocationTypes: LocationTypeItem[] = [
    {
      locationTypeId: 'lt-area',
      code: 'AREA',
      name: 'Area / Floor',
      canStoreInventory: true,
      canPick: true,
      canPutAway: true,
      canContainChildren: true,
      isActive: true
    },
    {
      locationTypeId: 'lt-rack',
      code: 'RACK',
      name: 'Rack',
      canStoreInventory: false,
      canPick: false,
      canPutAway: true,
      canContainChildren: true,
      isActive: true
    },
    {
      locationTypeId: 'lt-shelf',
      code: 'SHELF',
      name: 'Shelf',
      canStoreInventory: false,
      canPick: true,
      canPutAway: true,
      canContainChildren: true,
      isActive: true
    },
    {
      locationTypeId: 'lt-bin',
      code: 'BIN',
      name: 'Bin',
      canStoreInventory: true,
      canPick: true,
      canPutAway: true,
      canContainChildren: false,
      isActive: true
    }
  ];

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [WarehouseMasterComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        MasterDataService,
        WorkspaceScopeService,
        AuthService
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(WarehouseMasterComponent);
    component = fixture.componentInstance;
    masterDataService = TestBed.inject(MasterDataService);
    authService = TestBed.inject(AuthService);

    // Default spies
    vi.spyOn(masterDataService, 'getWarehouses').mockReturnValue(of(mockWarehouses));
    vi.spyOn(masterDataService, 'getWarehouseZones').mockReturnValue(of(mockZones));
    vi.spyOn(masterDataService, 'getWarehouseLocations').mockReturnValue(of(mockLocations));
    vi.spyOn(masterDataService, 'getLocationTypes').mockReturnValue(of(mockLocationTypes));

    // Set Tenant Admin session
    authService.currentUser.set({
      userId: 'admin-1',
      username: 'admin',
      email: 'admin@apex.local',
      tenantId: 'tenant-1',
      roles: ['Tenant Admin'],
      permissions: ['*'],
      token: 'admin-token'
    });
  });

  it('should initialize and load warehouses, zones, and storage locations', () => {
    fixture.detectChanges();

    expect(component).toBeTruthy();
    expect(component.warehouses().length).toBe(2);
    expect(component.selectedWarehouseId()).toBe('wh-1');
    expect(component.zones().length).toBe(2);
    expect(component.locations().length).toBe(4);
    expect(component.locationTypes().length).toBe(4);
    expect(component.loading()).toBe(false);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Main Logistics Center');
    expect(compiled.textContent).toContain('WH-MAIN');
  });

  it('should switch selected warehouse and load its corresponding zones and locations', () => {
    fixture.detectChanges();

    const wh2Zones: WarehouseZoneItem[] = [
      {
        warehouseZoneId: 'zone-north-1',
        warehouseId: 'wh-2',
        zoneCode: 'RECV',
        zoneName: 'Receiving Dock',
        description: 'Inbound staging',
        isActive: true,
        storageLocationsCount: 1
      }
    ];
    const wh2Locations: WarehouseLocationItem[] = [
      {
        locationId: 'loc-north-1',
        warehouseLocationId: 'loc-north-1',
        warehouseId: 'wh-2',
        warehouseZoneId: 'zone-north-1',
        zoneName: 'Receiving Dock',
        locationCode: 'RCV-01',
        locationName: 'Dock Staging Bay 01',
        locationTypeId: 'lt-area',
        hierarchyPath: 'RCV-01',
        hierarchyLevel: 1,
        isLeaf: true,
        canStoreInventory: true,
        canPick: false,
        canPutAway: true,
        isActive: true
      }
    ];

    vi.spyOn(masterDataService, 'getWarehouseZones').mockReturnValue(of(wh2Zones));
    vi.spyOn(masterDataService, 'getWarehouseLocations').mockReturnValue(of(wh2Locations));

    component.selectWarehouse('wh-2');
    fixture.detectChanges();

    expect(component.selectedWarehouseId()).toBe('wh-2');
    expect(component.selectedWarehouse()?.warehouseCode).toBe('WH-NORTH');
    expect(component.zones().length).toBe(1);
    expect(component.locations().length).toBe(1);
    expect(component.filteredLocations()[0].locationCode).toBe('RCV-01');
  });

  it('should filter locations by zone', () => {
    fixture.detectChanges();

    // Default status is 'active', so loc-1, loc-2, loc-3 are active (zone-1)
    expect(component.filteredLocations().length).toBe(3);

    component.selectedZoneFilter.set('zone-2');
    component.locationStatusFilter.set('all');
    fixture.detectChanges();

    expect(component.filteredLocations().length).toBe(1);
    expect(component.filteredLocations()[0].locationCode).toBe('PK-01');

    component.selectedZoneFilter.set('all');
    expect(component.filteredLocations().length).toBe(4);
  });

  it('should filter locations by search term', () => {
    fixture.detectChanges();

    component.locationStatusFilter.set('all');
    component.locationSearch.set('Bin 01');
    fixture.detectChanges();

    // Matches 'Bin 01' and 'Pick Bin 01'
    expect(component.filteredLocations().length).toBe(2);

    component.locationSearch.set('PK-01');
    expect(component.filteredLocations().length).toBe(1);
    expect(component.filteredLocations()[0].locationName).toBe('Pick Bin 01');

    component.locationSearch.set('R01-S01-B01');
    expect(component.filteredLocations().length).toBe(1);
    expect(component.filteredLocations()[0].locationCode).toBe('R01-S01-B01');
  });

  it('should filter locations by capability', () => {
    fixture.detectChanges();

    // Active locations: loc-1 (no pick), loc-2 (pick), loc-3 (pick)
    component.locationCapabilityFilter.set('pick');
    expect(component.filteredLocations().length).toBe(2);
    expect(component.filteredLocations().some(l => l.locationCode === 'R01')).toBe(false);

    component.locationCapabilityFilter.set('all');
    expect(component.filteredLocations().length).toBe(3);
  });

  it('should switch tabs between locations, zones, warehouses, and policies', () => {
    fixture.detectChanges();

    expect(component.activeTab()).toBe('locations');
    component.activeTab.set('zones');
    fixture.detectChanges();
    expect(component.activeTab()).toBe('zones');

    component.activeTab.set('warehouses');
    fixture.detectChanges();
    expect(component.activeTab()).toBe('warehouses');

    component.activeTab.set('policies');
    fixture.detectChanges();
    expect(component.activeTab()).toBe('policies');
  });

  it('should navigate to dedicated create warehouse page when new warehouse is clicked', () => {
    fixture.detectChanges();
    const router = TestBed.inject(Router);
    const navSpy = vi.spyOn(router, 'navigate');

    component.navigateToCreateWarehouse();
    expect(navSpy).toHaveBeenCalledWith(['/app/master-data/warehouses/new']);
  });

  it('should navigate to dedicated edit warehouse page when edit warehouse is clicked', () => {
    fixture.detectChanges();
    const router = TestBed.inject(Router);
    const navSpy = vi.spyOn(router, 'navigate');

    component.navigateToEditWarehouse(mockWarehouses[0]);
    expect(navSpy).toHaveBeenCalledWith(['/app/master-data/warehouses', 'wh-1', 'edit']);
  });

  it('should navigate to dedicated create zone page under selected warehouse', () => {
    fixture.detectChanges();
    const router = TestBed.inject(Router);
    const navSpy = vi.spyOn(router, 'navigate');

    component.navigateToCreateZone();
    expect(navSpy).toHaveBeenCalledWith(['/app/master-data/warehouses', 'wh-1', 'zones', 'new']);
  });

  it('should navigate to dedicated edit zone page with warehouse and zone IDs', () => {
    fixture.detectChanges();
    const router = TestBed.inject(Router);
    const navSpy = vi.spyOn(router, 'navigate');

    component.navigateToEditZone(mockZones[0]);
    expect(navSpy).toHaveBeenCalledWith(['/app/master-data/warehouses', 'wh-1', 'zones', 'zone-1', 'edit']);
  });

  it('should navigate to dedicated create location page with active zone queryParam if filtered', () => {
    fixture.detectChanges();
    const router = TestBed.inject(Router);
    const navSpy = vi.spyOn(router, 'navigate');

    component.selectedZoneFilter.set('zone-1');
    component.navigateToCreateLocation();
    expect(navSpy).toHaveBeenCalledWith(
      ['/app/master-data/warehouses', 'wh-1', 'locations', 'new'],
      { queryParams: { zoneId: 'zone-1' } }
    );
  });

  it('should navigate to dedicated edit location page with warehouse and location IDs', () => {
    fixture.detectChanges();
    const router = TestBed.inject(Router);
    const navSpy = vi.spyOn(router, 'navigate');

    component.navigateToEditLocation(mockLocations[0]);
    expect(navSpy).toHaveBeenCalledWith(['/app/master-data/warehouses', 'wh-1', 'locations', 'loc-1', 'edit']);
  });

  it('should deactivate warehouse when confirmed', () => {
    fixture.detectChanges();

    vi.spyOn(window, 'confirm').mockReturnValue(true);
    const deactSpy = vi.spyOn(masterDataService, 'deactivateWarehouse').mockReturnValue(of(undefined));

    component.deactivateWarehouse(mockWarehouses[0]);
    expect(deactSpy).toHaveBeenCalledWith('wh-1');
  });

  it('should handle deactivation error gracefully when active entities exist', () => {
    fixture.detectChanges();

    vi.spyOn(window, 'confirm').mockReturnValue(true);
    vi.spyOn(masterDataService, 'deactivateWarehouseZone').mockReturnValue(
      throwError(() => ({ error: { message: 'Cannot deactivate zone: active storage locations are assigned to it.' } }))
    );

    component.deactivateZone(mockZones[0]);
    expect(component.actionError()).toContain('active storage locations');
  });

  it('should evaluate permissions correctly for tenant admin vs staff', () => {
    expect(component.canCreateWarehouse()).toBe(true);
    expect(component.canEditWarehouse()).toBe(true);
    expect(component.canManageZones()).toBe(true);
    expect(component.canCreateLocation()).toBe(true);
    expect(component.canEditLocation()).toBe(true);
    expect(component.canDeleteLocation()).toBe(true);

    authService.currentUser.set({
      userId: 'staff-1',
      username: 'warehouse_staff',
      email: 'staff@apex.local',
      tenantId: 'tenant-1',
      roles: ['Tenant User'],
      permissions: ['LOCATION.CREATE'],
      token: 'staff-token'
    });

    expect(component.canCreateWarehouse()).toBe(false);
    expect(component.canCreateLocation()).toBe(true);
    expect(component.canDeleteLocation()).toBe(false);
  });
});
