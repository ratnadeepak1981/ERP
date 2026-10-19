import { describe, it, expect, beforeEach, vi } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router, ActivatedRoute, convertToParamMap } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { LocationPageComponent } from './location-page.component';
import { MasterDataService } from '../../../../../core/services/master-data.service';
import { WorkspaceScopeService } from '../../../../../core/services/workspace-scope.service';
import { of, throwError } from 'rxjs';
import {
  WarehouseItem,
  WarehouseZoneItem,
  WarehouseLocationItem,
  LocationTypeItem
} from '../../../../../core/models/scope.models';

describe('LocationPageComponent', () => {
  let component: LocationPageComponent;
  let fixture: ComponentFixture<LocationPageComponent>;
  let masterDataService: MasterDataService;
  let router: Router;

  const mockWarehouse: WarehouseItem = {
    warehouseId: 'wh-1',
    tenantId: 't-1',
    warehouseCode: 'WH-MAIN',
    warehouseName: 'Main Logistics Center',
    city: 'London',
    isActive: true
  };

  const mockZones: WarehouseZoneItem[] = [
    {
      warehouseZoneId: 'zone-1',
      warehouseId: 'wh-1',
      zoneCode: 'BULK',
      zoneName: 'Bulk Storage Area',
      isActive: true
    }
  ];

  const mockTypes: LocationTypeItem[] = [
    {
      locationTypeId: 'type-bin',
      code: 'BIN',
      name: 'Bin',
      canContainChildren: false,
      canStoreInventory: true,
      canPick: true,
      canPutAway: true,
      isActive: true
    },
    {
      locationTypeId: 'type-rack',
      code: 'RACK',
      name: 'Rack',
      canContainChildren: true,
      canStoreInventory: false,
      canPick: false,
      canPutAway: false,
      isActive: true
    }
  ];

  const mockLocations: WarehouseLocationItem[] = [
    {
      locationId: 'loc-1',
      warehouseLocationId: 'loc-1',
      warehouseId: 'wh-1',
      locationCode: 'R01',
      locationName: 'Rack 01',
      hierarchyPath: 'R01',
      locationTypeId: 'type-rack',
      isActive: true,
      canStoreInventory: false,
      canPick: false,
      canPutAway: false
    },
    {
      locationId: 'loc-2',
      warehouseLocationId: 'loc-2',
      warehouseId: 'wh-1',
      locationCode: 'B01',
      locationName: 'Bin 01',
      hierarchyPath: 'R01 > B01',
      parentLocationId: 'loc-1',
      locationTypeId: 'type-bin',
      isActive: true,
      canStoreInventory: true,
      canPick: true,
      canPutAway: true
    }
  ];

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [LocationPageComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        MasterDataService,
        WorkspaceScopeService,
        {
          provide: ActivatedRoute,
          useValue: {
            paramMap: of(convertToParamMap({ warehouseId: 'wh-1', locationId: null })),
            snapshot: {
              paramMap: convertToParamMap({ warehouseId: 'wh-1', locationId: null }),
              queryParamMap: convertToParamMap({ zoneId: 'zone-1' }),
              url: [{ path: 'wh-1' }, { path: 'locations' }, { path: 'new' }]
            }
          }
        }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(LocationPageComponent);
    component = fixture.componentInstance;
    masterDataService = TestBed.inject(MasterDataService);
    router = TestBed.inject(Router);

    vi.spyOn(masterDataService, 'getWarehouse').mockReturnValue(of(mockWarehouse));
    vi.spyOn(masterDataService, 'getWarehouseZones').mockReturnValue(of(mockZones));
    vi.spyOn(masterDataService, 'getLocationTypes').mockReturnValue(of(mockTypes));
    vi.spyOn(masterDataService, 'getWarehouseLocations').mockReturnValue(of(mockLocations));
    vi.spyOn(masterDataService, 'getWarehouseLocation').mockReturnValue(of(mockLocations[1]));
  });

  it('should initialize and load metadata in create mode with preselected zone and default BIN type', () => {
    fixture.detectChanges();

    expect(component).toBeTruthy();
    expect(component.mode()).toBe('create');
    expect(component.warehouseId()).toBe('wh-1');
    expect(component.currentWarehouse()?.warehouseCode).toBe('WH-MAIN');
    expect(component.zones().length).toBe(1);
    expect(component.locationTypes().length).toBe(2);
    expect(component.allLocations().length).toBe(2);

    expect(component.locationForm.controls.warehouseZoneId.value).toBe('zone-1');
    expect(component.locationForm.controls.locationTypeId.value).toBe('type-bin');
    expect(component.locationForm.controls.canStoreInventory.value).toBe(true);
    expect(component.locationForm.controls.canPick.value).toBe(true);
    expect(component.locationForm.controls.canPutAway.value).toBe(true);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Register Storage Location');
    expect(compiled.textContent).toContain('WH-MAIN');
  });

  it('should update capability flags when location type changes in create mode', () => {
    fixture.detectChanges();

    // Change to Rack type which cannot store inventory or pick
    component.locationForm.controls.locationTypeId.setValue('type-rack');

    expect(component.locationForm.controls.canStoreInventory.value).toBe(false);
    expect(component.locationForm.controls.canPick.value).toBe(false);
    expect(component.locationForm.controls.canPutAway.value).toBe(false);

    // Change back to Bin type
    component.locationForm.controls.locationTypeId.setValue('type-bin');

    expect(component.locationForm.controls.canStoreInventory.value).toBe(true);
    expect(component.locationForm.controls.canPick.value).toBe(true);
    expect(component.locationForm.controls.canPutAway.value).toBe(true);
  });

  it('should compute hierarchy path preview dynamically', () => {
    fixture.detectChanges();

    component.locationForm.controls.parentLocationId.setValue('loc-1');
    component.locationForm.controls.locationCode.setValue('B99');

    expect(component.hierarchyPathPreview()).toBe('R01 > B99');
  });

  it('should filter parent candidates to exclude self in edit mode', () => {
    component.mode.set('edit');
    component.warehouseId.set('wh-1');
    component.locationId.set('loc-2');
    component.allLocations.set(mockLocations);

    const candidates = component.parentCandidates();
    expect(candidates.find(c => c.locationId === 'loc-2')).toBeUndefined();
    expect(candidates.find(c => c.locationId === 'loc-1')).toBeDefined();
  });

  it('should submit create location and navigate to warehouse locations tab', () => {
    fixture.detectChanges();

    const createdLocation: WarehouseLocationItem = {
      locationId: 'loc-new',
      warehouseLocationId: 'loc-new',
      warehouseId: 'wh-1',
      locationCode: 'R01-S01-B02',
      locationName: 'Bin 02',
      isActive: true
    };
    const createSpy = vi.spyOn(masterDataService, 'createWarehouseLocation').mockReturnValue(of(createdLocation));
    const navSpy = vi.spyOn(router, 'navigate');

    component.locationForm.patchValue({
      locationCode: 'R01-S01-B02',
      locationName: 'Bin 02',
      warehouseZoneId: 'zone-1',
      locationTypeId: 'type-bin',
      parentLocationId: 'loc-1'
    });

    component.saveLocation();

    expect(createSpy).toHaveBeenCalledWith('wh-1', {
      locationCode: 'R01-S01-B02',
      locationName: 'Bin 02',
      warehouseZoneId: 'zone-1',
      locationTypeId: 'type-bin',
      parentLocationId: 'loc-1'
    });
    expect(navSpy).toHaveBeenCalledWith(['/app/master-data/warehouses'], {
      queryParams: { warehouseId: 'wh-1', tab: 'locations' }
    });
  });

  it('should load location in edit mode and submit update', () => {
    fixture.detectChanges();
    component.mode.set('edit');
    component.warehouseId.set('wh-1');
    component.locationId.set('loc-2');
    component.loadData('wh-1', 'loc-2');

    expect(component.currentLocation()?.locationCode).toBe('B01');
    expect(component.locationForm.controls.locationCode.disabled).toBe(true);

    const updateSpy = vi.spyOn(masterDataService, 'updateWarehouseLocation').mockReturnValue(of({} as any));
    const navSpy = vi.spyOn(router, 'navigate');

    component.locationForm.patchValue({
      locationName: 'Bin 01 Renamed',
      locationTypeId: 'type-bin',
      parentLocationId: 'loc-1'
    });

    component.saveLocation();

    expect(updateSpy).toHaveBeenCalledWith('loc-2', {
      locationName: 'Bin 01 Renamed',
      locationTypeId: 'type-bin',
      parentLocationId: 'loc-1'
    });
    expect(navSpy).toHaveBeenCalledWith(['/app/master-data/warehouses'], {
      queryParams: { warehouseId: 'wh-1', tab: 'locations' }
    });
  });

  it('should handle API errors and show error banner', () => {
    fixture.detectChanges();
    vi.spyOn(masterDataService, 'createWarehouseLocation').mockReturnValue(
      throwError(() => ({
        error: {
          message: 'Location code already exists in this warehouse.',
          errors: { LocationCode: ['Must be unique within warehouse.'] }
        }
      }))
    );

    component.locationForm.patchValue({
      locationCode: 'DUPLICATE',
      locationName: 'Duplicate Bin'
    });

    component.saveLocation();

    expect(component.saving()).toBe(false);
    expect(component.message()?.text).toBe('Location code already exists in this warehouse.');
    expect(component.message()?.severity).toBe('danger');
    expect(component.serverErrors()['locationCode']).toBe('Must be unique within warehouse.');
  });

  it('should navigate back to warehouse locations tab on cancel', () => {
    const navSpy = vi.spyOn(router, 'navigate');
    component.warehouseId.set('wh-1');
    component.navigateBack();

    expect(navSpy).toHaveBeenCalledWith(['/app/master-data/warehouses'], {
      queryParams: { warehouseId: 'wh-1', tab: 'locations' }
    });
  });
});
