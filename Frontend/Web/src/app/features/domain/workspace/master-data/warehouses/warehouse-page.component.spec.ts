import { describe, it, expect, beforeEach, vi } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router, ActivatedRoute } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { WarehousePageComponent } from './warehouse-page.component';
import { MasterDataService } from '../../../../../core/services/master-data.service';
import { WorkspaceScopeService } from '../../../../../core/services/workspace-scope.service';
import { AuthService } from '../../../../../core/auth/auth.service';
import { of, throwError } from 'rxjs';
import { WarehouseItem } from '../../../../../core/models/scope.models';

describe('WarehousePageComponent', () => {
  let component: WarehousePageComponent;
  let fixture: ComponentFixture<WarehousePageComponent>;
  let masterDataService: MasterDataService;
  let router: Router;

  const mockWarehouse: WarehouseItem = {
    warehouseId: 'wh-1',
    tenantId: 't-1',
    warehouseCode: 'WH-CENTRAL',
    warehouseName: 'Central Logistics Hub',
    description: 'Central distribution depot',
    city: 'Birmingham',
    addressLine1: '50 Logistics Way',
    addressLine2: 'Bay 3',
    postalCode: 'B1 1AA',
    country: 'UK',
    isActive: true
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [WarehousePageComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        MasterDataService,
        WorkspaceScopeService,
        AuthService,
        {
          provide: ActivatedRoute,
          useValue: {
            url: of([{ path: 'new' }]),
            snapshot: {
              paramMap: {
                get: (key: string) => (key === 'id' ? null : null)
              },
              url: [{ path: 'new' }]
            }
          }
        }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(WarehousePageComponent);
    component = fixture.componentInstance;
    masterDataService = TestBed.inject(MasterDataService);
    router = TestBed.inject(Router);
  });

  it('should initialize in create mode with empty form and active status', () => {
    fixture.detectChanges();

    expect(component).toBeTruthy();
    expect(component.mode()).toBe('create');
    expect(component.warehouseForm.valid).toBe(false);
    expect(component.warehouseForm.controls.isActive.value).toBe(true);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Register Warehouse Facility');
  });

  it('should validate mandatory fields (warehouseCode, warehouseName, city)', () => {
    fixture.detectChanges();

    const form = component.warehouseForm;
    expect(form.controls.warehouseCode.valid).toBe(false);
    expect(form.controls.warehouseName.valid).toBe(false);
    expect(form.controls.city.valid).toBe(false);

    form.patchValue({
      warehouseCode: 'WH-01',
      warehouseName: 'Main Facility',
      city: 'London'
    });

    expect(form.valid).toBe(true);
  });

  it('should submit create warehouse and navigate back to warehouses directory', () => {
    fixture.detectChanges();
    const createSpy = vi.spyOn(masterDataService, 'createWarehouse').mockReturnValue(of(mockWarehouse));
    const navSpy = vi.spyOn(router, 'navigate');

    component.warehouseForm.patchValue({
      warehouseCode: 'WH-CENTRAL',
      warehouseName: 'Central Logistics Hub',
      city: 'Birmingham',
      addressLine1: '50 Logistics Way',
      postalCode: 'B1 1AA',
      country: 'UK'
    });

    component.saveWarehouse();

    expect(createSpy).toHaveBeenCalledWith(expect.objectContaining({
      warehouseCode: 'WH-CENTRAL',
      warehouseName: 'Central Logistics Hub',
      city: 'Birmingham'
    }));
    expect(navSpy).toHaveBeenCalledWith(['/app/master-data/warehouses'], {
      queryParams: { warehouseId: 'wh-1', tab: 'warehouses' }
    });
  });

  it('should handle API errors gracefully during create', () => {
    fixture.detectChanges();
    vi.spyOn(masterDataService, 'createWarehouse').mockReturnValue(
      throwError(() => ({ error: { message: 'Warehouse code already exists.' } }))
    );

    component.warehouseForm.patchValue({
      warehouseCode: 'WH-CENTRAL',
      warehouseName: 'Central Hub',
      city: 'Leeds'
    });

    component.saveWarehouse();

    expect(component.saving()).toBe(false);
    expect(component.message()?.text).toBe('Warehouse code already exists.');
    expect(component.message()?.severity).toBe('danger');
  });

  it('should load warehouse in edit mode and disable warehouseCode', () => {
    fixture.detectChanges();
    vi.spyOn(masterDataService, 'getWarehouse').mockReturnValue(of(mockWarehouse));

    component.mode.set('edit');
    component.warehouseId.set('wh-1');
    component.loadWarehouse('wh-1');

    expect(component.currentWarehouse()).toEqual(mockWarehouse);
    expect(component.warehouseForm.controls.warehouseCode.value).toBe('WH-CENTRAL');
    expect(component.warehouseForm.controls.warehouseCode.disabled).toBe(true);
    expect(component.warehouseForm.controls.warehouseName.value).toBe('Central Logistics Hub');
    expect(component.warehouseForm.controls.city.value).toBe('Birmingham');
  });

  it('should submit update in edit mode with modified values', () => {
    fixture.detectChanges();
    vi.spyOn(masterDataService, 'getWarehouse').mockReturnValue(of(mockWarehouse));
    const updateSpy = vi.spyOn(masterDataService, 'updateWarehouse').mockReturnValue(of({
      ...mockWarehouse,
      warehouseName: 'Central Logistics Hub Modified'
    }));
    const navSpy = vi.spyOn(router, 'navigate');

    component.mode.set('edit');
    component.warehouseId.set('wh-1');
    component.loadWarehouse('wh-1');

    component.warehouseForm.patchValue({
      warehouseName: 'Central Logistics Hub Modified'
    });

    component.saveWarehouse();

    expect(updateSpy).toHaveBeenCalledWith('wh-1', expect.objectContaining({
      warehouseName: 'Central Logistics Hub Modified'
    }));
    expect(navSpy).toHaveBeenCalledWith(['/app/master-data/warehouses'], {
      queryParams: { warehouseId: 'wh-1', tab: 'warehouses' }
    });
  });

  it('should navigate back on cancel', () => {
    const navSpy = vi.spyOn(router, 'navigate');
    component.warehouseId.set('wh-1');
    component.navigateBack();

    expect(navSpy).toHaveBeenCalledWith(['/app/master-data/warehouses'], {
      queryParams: { warehouseId: 'wh-1', tab: 'warehouses' }
    });
  });
});
