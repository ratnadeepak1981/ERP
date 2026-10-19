import { describe, it, expect, beforeEach, vi } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router, ActivatedRoute, convertToParamMap } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { ZonePageComponent } from './zone-page.component';
import { MasterDataService } from '../../../../../core/services/master-data.service';
import { WorkspaceScopeService } from '../../../../../core/services/workspace-scope.service';
import { of, throwError } from 'rxjs';
import { WarehouseItem, WarehouseZoneItem } from '../../../../../core/models/scope.models';

describe('ZonePageComponent', () => {
  let component: ZonePageComponent;
  let fixture: ComponentFixture<ZonePageComponent>;
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

  const mockZone: WarehouseZoneItem = {
    warehouseZoneId: 'zone-1',
    warehouseId: 'wh-1',
    zoneCode: 'BULK',
    zoneName: 'Bulk Storage Area',
    description: 'Heavy racking area',
    isActive: true,
    storageLocationsCount: 4
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ZonePageComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        MasterDataService,
        WorkspaceScopeService,
        {
          provide: ActivatedRoute,
          useValue: {
            paramMap: of(convertToParamMap({ warehouseId: 'wh-1', zoneId: null })),
            snapshot: {
              paramMap: convertToParamMap({ warehouseId: 'wh-1', zoneId: null }),
              url: [{ path: 'wh-1' }, { path: 'zones' }, { path: 'new' }]
            }
          }
        }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(ZonePageComponent);
    component = fixture.componentInstance;
    masterDataService = TestBed.inject(MasterDataService);
    router = TestBed.inject(Router);

    vi.spyOn(masterDataService, 'getWarehouse').mockReturnValue(of(mockWarehouse));
    vi.spyOn(masterDataService, 'getWarehouseZone').mockReturnValue(of(mockZone));
  });

  it('should initialize and load parent warehouse context in create mode', () => {
    fixture.detectChanges();

    expect(component).toBeTruthy();
    expect(component.mode()).toBe('create');
    expect(component.warehouseId()).toBe('wh-1');
    expect(component.currentWarehouse()?.warehouseCode).toBe('WH-MAIN');
    expect(component.zoneForm.valid).toBe(false);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Register Warehouse Zone');
    expect(compiled.textContent).toContain('WH-MAIN');
  });

  it('should validate required fields (zoneCode, zoneName)', () => {
    fixture.detectChanges();

    expect(component.zoneForm.controls.zoneCode.valid).toBe(false);
    expect(component.zoneForm.controls.zoneName.valid).toBe(false);

    component.zoneForm.patchValue({
      zoneCode: 'COLD',
      zoneName: 'Cold Storage Room'
    });

    expect(component.zoneForm.valid).toBe(true);
  });

  it('should submit create zone and navigate back to warehouse zones tab', () => {
    fixture.detectChanges();

    const createdZone: WarehouseZoneItem = {
      warehouseZoneId: 'zone-2',
      warehouseId: 'wh-1',
      zoneCode: 'COLD',
      zoneName: 'Cold Storage Room',
      isActive: true
    };
    const createSpy = vi.spyOn(masterDataService, 'createWarehouseZone').mockReturnValue(of(createdZone));
    const navSpy = vi.spyOn(router, 'navigate');

    component.zoneForm.patchValue({
      zoneCode: 'COLD',
      zoneName: 'Cold Storage Room',
      description: 'Refrigerated items'
    });

    component.saveZone();

    expect(createSpy).toHaveBeenCalledWith('wh-1', {
      zoneCode: 'COLD',
      zoneName: 'Cold Storage Room',
      description: 'Refrigerated items'
    });
    expect(navSpy).toHaveBeenCalledWith(['/app/master-data/warehouses'], {
      queryParams: { warehouseId: 'wh-1', tab: 'zones' }
    });
  });

  it('should load zone in edit mode and disable zoneCode', () => {
    fixture.detectChanges();
    component.mode.set('edit');
    component.warehouseId.set('wh-1');
    component.zoneId.set('zone-1');
    component.loadZone('wh-1', 'zone-1');

    expect(component.currentZone()).toEqual(mockZone);
    expect(component.zoneForm.controls.zoneCode.value).toBe('BULK');
    expect(component.zoneForm.controls.zoneCode.disabled).toBe(true);
    expect(component.zoneForm.controls.zoneName.value).toBe('Bulk Storage Area');
  });

  it('should submit update in edit mode and navigate to zones tab', () => {
    fixture.detectChanges();
    component.mode.set('edit');
    component.warehouseId.set('wh-1');
    component.zoneId.set('zone-1');
    component.loadZone('wh-1', 'zone-1');

    const updatedZone: WarehouseZoneItem = {
      ...mockZone,
      zoneName: 'Bulk Storage High-Bay Area'
    };
    const updateSpy = vi.spyOn(masterDataService, 'updateWarehouseZone').mockReturnValue(of(updatedZone));
    const navSpy = vi.spyOn(router, 'navigate');

    component.zoneForm.patchValue({
      zoneName: 'Bulk Storage High-Bay Area'
    });

    component.saveZone();

    expect(updateSpy).toHaveBeenCalledWith('wh-1', 'zone-1', expect.objectContaining({
      zoneName: 'Bulk Storage High-Bay Area'
    }));
    expect(navSpy).toHaveBeenCalledWith(['/app/master-data/warehouses'], {
      queryParams: { warehouseId: 'wh-1', tab: 'zones' }
    });
  });

  it('should handle API errors and show error banner', () => {
    fixture.detectChanges();
    vi.spyOn(masterDataService, 'createWarehouseZone').mockReturnValue(
      throwError(() => ({ error: { message: 'Zone code already exists in warehouse.' } }))
    );

    component.zoneForm.patchValue({
      zoneCode: 'BULK',
      zoneName: 'Bulk Area'
    });

    component.saveZone();

    expect(component.saving()).toBe(false);
    expect(component.message()?.text).toBe('Zone code already exists in warehouse.');
    expect(component.message()?.severity).toBe('danger');
  });

  it('should navigate back to warehouse zones tab on cancel', () => {
    const navSpy = vi.spyOn(router, 'navigate');
    component.warehouseId.set('wh-1');
    component.navigateBack();

    expect(navSpy).toHaveBeenCalledWith(['/app/master-data/warehouses'], {
      queryParams: { warehouseId: 'wh-1', tab: 'zones' }
    });
  });
});
