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
      locationTypeId: 'type-rack',
      code: 'RACK',
      name: 'Rack',
      canContainChildren: true,
      canStoreInventory: false,
      canPick: false,
      canPutAway: false,
      isActive: true
    },
    {
      locationTypeId: 'type-shelf',
      code: 'SHELF',
      name: 'Shelf',
      canContainChildren: true,
      canStoreInventory: false,
      canPick: false,
      canPutAway: false,
      isActive: true
    },
    {
      locationTypeId: 'type-bin',
      code: 'BIN',
      name: 'Bin',
      canContainChildren: false,
      canStoreInventory: true,
      canPick: true,
      canPutAway: true,
      isActive: true
    }
  ];

  const mockLocations: WarehouseLocationItem[] = [
    {
      locationId: 'loc-1',
      warehouseLocationId: 'loc-1',
      warehouseId: 'wh-1',
      warehouseZoneId: 'zone-1',
      locationCode: 'R01',
      locationName: 'Rack 01',
      hierarchyPath: 'R01',
      locationTypeId: 'type-rack',
      locationTypeCode: 'RACK',
      zoneName: 'Bulk Storage Area',
      isActive: true,
      canStoreInventory: false,
      canPick: false,
      canPutAway: false
    },
    {
      locationId: 'loc-2',
      warehouseLocationId: 'loc-2',
      warehouseId: 'wh-1',
      warehouseZoneId: 'zone-1',
      locationCode: 'S01',
      locationName: 'Shelf 01',
      hierarchyPath: 'R01 > S01',
      parentLocationId: 'loc-1',
      locationTypeId: 'type-shelf',
      locationTypeCode: 'SHELF',
      zoneName: 'Bulk Storage Area',
      isActive: true,
      canStoreInventory: false,
      canPick: false,
      canPutAway: false
    },
    {
      locationId: 'loc-3',
      warehouseLocationId: 'loc-3',
      warehouseId: 'wh-1',
      warehouseZoneId: 'zone-1',
      locationCode: 'B01',
      locationName: 'Bin 01',
      hierarchyPath: 'R01 > S01 > B01',
      parentLocationId: 'loc-2',
      locationTypeId: 'type-bin',
      locationTypeCode: 'BIN',
      zoneName: 'Bulk Storage Area',
      isActive: true,
      canStoreInventory: true,
      canPick: true,
      canPutAway: true
    },
    {
      locationId: 'loc-other-zone',
      warehouseLocationId: 'loc-other-zone',
      warehouseId: 'wh-1',
      warehouseZoneId: 'zone-2',
      locationCode: 'R02',
      locationName: 'Rack 02',
      locationTypeId: 'type-rack',
      locationTypeCode: 'RACK',
      zoneName: 'Cold Storage Area',
      isActive: true,
      canStoreInventory: false,
      canPick: false,
      canPutAway: false
    },
    {
      locationId: 'loc-inactive',
      warehouseLocationId: 'loc-inactive',
      warehouseId: 'wh-1',
      warehouseZoneId: 'zone-1',
      locationCode: 'R-INACTIVE',
      locationName: 'Inactive Rack',
      locationTypeId: 'type-rack',
      locationTypeCode: 'RACK',
      zoneName: 'Bulk Storage Area',
      isActive: false,
      canStoreInventory: false,
      canPick: false,
      canPutAway: false
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
    vi.spyOn(masterDataService, 'getWarehouseLocation').mockReturnValue(of(mockLocations[2]));
  });

  it('should initialize and load metadata in create mode with preselected zone and default RACK type', () => {
    fixture.detectChanges();

    expect(component).toBeTruthy();
    expect(component.mode()).toBe('create');
    expect(component.warehouseId()).toBe('wh-1');
    expect(component.currentWarehouse()?.warehouseCode).toBe('WH-MAIN');
    expect(component.zones().length).toBe(1);
    expect(component.locationTypes().length).toBe(3);
    expect(component.allLocations().length).toBe(5);

    expect(component.locationForm.controls.warehouseZoneId.value).toBe('zone-1');
    expect(component.locationForm.controls.locationTypeId.value).toBe('type-rack');

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Register Storage Location');
    expect(compiled.textContent).toContain('WH-MAIN');
  });

  it('should configure RACK as top-level with no parent candidates and isTopLevel true', () => {
    fixture.detectChanges();
    component.locationForm.controls.locationTypeId.setValue('type-rack');

    const rule = component.expectedParentInfo();
    expect(rule.isTopLevel).toBe(true);
    expect(rule.name).toBe('None (Top-Level)');
    expect(component.parentCandidates().length).toBe(0);
  });

  it('should filter parent candidates to only RACK locations in the same zone when child is SHELF', () => {
    fixture.detectChanges();
    component.locationForm.controls.warehouseZoneId.setValue('zone-1');
    component.locationForm.controls.locationTypeId.setValue('type-shelf');

    const rule = component.expectedParentInfo();
    expect(rule.isTopLevel).toBe(false);
    expect(rule.code).toBe('RACK');
    expect(rule.isRequired).toBe(true);

    const candidates = component.parentCandidates();
    // Only loc-1 (Rack 01 in zone-1) is active and in zone-1.
    // loc-other-zone is in zone-2, loc-inactive is inactive, loc-2 is SHELF, loc-3 is BIN
    expect(candidates.length).toBe(1);
    expect(candidates[0].locationCode).toBe('R01');
    expect(candidates[0].warehouseZoneId).toBe('zone-1');
  });

  it('should filter parent candidates to only SHELF locations in the same zone when child is BIN', () => {
    fixture.detectChanges();
    component.locationForm.controls.warehouseZoneId.setValue('zone-1');
    component.locationForm.controls.locationTypeId.setValue('type-bin');

    const rule = component.expectedParentInfo();
    expect(rule.isTopLevel).toBe(false);
    expect(rule.code).toBe('SHELF');
    expect(rule.isRequired).toBe(true);

    const candidates = component.parentCandidates();
    // Only loc-2 (Shelf 01 in zone-1) is active and in zone-1
    expect(candidates.length).toBe(1);
    expect(candidates[0].locationCode).toBe('S01');
  });

  it('should format parent option labels with code, name, type, and zone', () => {
    fixture.detectChanges();
    component.locationForm.controls.warehouseZoneId.setValue('zone-1');
    component.locationForm.controls.locationTypeId.setValue('type-shelf');

    const options = component.parentOptions();
    expect(options.length).toBe(1);
    expect(options[0].code).toBe('R01');
    expect(options[0].name).toBe('Rack 01');
    expect(options[0].typeName).toBe('RACK');
    expect(options[0].label).toContain('R01 — Rack 01 (RACK) · Bulk Storage Area');
  });

  it('should filter parent options using parentSearchTerm by code or name', () => {
    fixture.detectChanges();
    component.locationForm.controls.warehouseZoneId.setValue('zone-1');
    component.locationForm.controls.locationTypeId.setValue('type-shelf');

    // Matching code
    component.parentSearchTerm.set('R01');
    expect(component.parentOptions().length).toBe(1);

    // Non-matching term
    component.parentSearchTerm.set('NONEXISTENT');
    expect(component.parentOptions().length).toBe(0);
  });

  it('should exclude current location and all its descendants to prevent circular parentage', () => {
    fixture.detectChanges();
    // In edit mode for loc-1 (Rack 01):
    // loc-1 has child loc-2 (Shelf 01), and loc-2 has child loc-3 (Bin 01)
    component.mode.set('edit');
    component.warehouseId.set('wh-1');
    component.locationId.set('loc-1');
    component.locationForm.controls.warehouseZoneId.setValue('zone-1');
    // Test general container type to see all potential parents
    component.locationForm.controls.locationTypeId.setValue('type-shelf');

    const candidates = component.parentCandidates();
    const candidateIds = candidates.map(c => c.warehouseLocationId ?? c.locationId);

    // loc-1 (self), loc-2 (child), loc-3 (grandchild) must all be excluded!
    expect(candidateIds).not.toContain('loc-1');
    expect(candidateIds).not.toContain('loc-2');
    expect(candidateIds).not.toContain('loc-3');
  });

  it('should clear parent selection and show warning message when zone changes to an incompatible one', () => {
    fixture.detectChanges();
    component.locationForm.controls.warehouseZoneId.setValue('zone-1');
    component.locationForm.controls.locationTypeId.setValue('type-shelf');
    component.locationForm.controls.parentLocationId.setValue('loc-1');

    expect(component.locationForm.controls.parentLocationId.value).toBe('loc-1');

    // Change zone to zone-2 (where loc-1 does not belong)
    component.locationForm.controls.warehouseZoneId.setValue('zone-2');

    expect(component.locationForm.controls.parentLocationId.value).toBeNull();
    expect(component.parentWarning()).toContain('Previous parent selection was cleared');
  });

  it('should compute hierarchy path preview dynamically', () => {
    fixture.detectChanges();

    component.locationForm.controls.parentLocationId.setValue('loc-2');
    component.locationForm.controls.locationCode.setValue('B99');

    expect(component.hierarchyPathPreview()).toBe('R01 > S01 > B99');
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
      parentLocationId: 'loc-2'
    });

    component.saveLocation();

    expect(createSpy).toHaveBeenCalledWith('wh-1', {
      locationCode: 'R01-S01-B02',
      locationName: 'Bin 02',
      warehouseZoneId: 'zone-1',
      locationTypeId: 'type-bin',
      parentLocationId: 'loc-2'
    });
    expect(navSpy).toHaveBeenCalledWith(['/app/master-data/warehouses'], {
      queryParams: { warehouseId: 'wh-1', tab: 'locations' }
    });
  });

  it('should load location in edit mode and submit update', () => {
    fixture.detectChanges();
    component.mode.set('edit');
    component.warehouseId.set('wh-1');
    component.locationId.set('loc-3');
    component.loadData('wh-1', 'loc-3');

    expect(component.currentLocation()?.locationCode).toBe('B01');
    expect(component.locationForm.controls.locationCode.disabled).toBe(true);

    const updateSpy = vi.spyOn(masterDataService, 'updateWarehouseLocation').mockReturnValue(of({} as any));
    const navSpy = vi.spyOn(router, 'navigate');

    component.locationForm.patchValue({
      locationName: 'Bin 01 Renamed',
      locationTypeId: 'type-bin',
      parentLocationId: 'loc-2'
    });

    component.saveLocation();

    expect(updateSpy).toHaveBeenCalledWith('loc-3', {
      locationName: 'Bin 01 Renamed',
      locationTypeId: 'type-bin',
      parentLocationId: 'loc-2'
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
