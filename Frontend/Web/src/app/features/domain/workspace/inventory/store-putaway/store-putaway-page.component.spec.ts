import { describe, it, expect, beforeEach, vi } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient, HttpErrorResponse } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter, ActivatedRoute, Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import { StorePutawayPageComponent, EditablePutawayLine } from './store-putaway-page.component';
import { PutawayService } from '../../../../../core/services/putaway.service';
import { MasterDataService } from '../../../../../core/services/master-data.service';
import { AuthService } from '../../../../../core/auth/auth.service';
import { PutawayRecordDto, PutawayStatus, PutawayLineDto } from '../../../../../core/models/putaway.models';
import { WarehouseItem, WarehouseLocationItem } from '../../../../../core/models/scope.models';

describe('StorePutawayPageComponent', () => {
  let component: StorePutawayPageComponent;
  let fixture: ComponentFixture<StorePutawayPageComponent>;
  let putawayService: PutawayService;
  let masterDataService: MasterDataService;
  let authService: AuthService;
  let router: Router;

  const mockWarehouse: WarehouseItem = {
    warehouseId: 'wh-1',
    tenantId: 't-1',
    warehouseCode: 'MAIN',
    warehouseName: 'Main Warehouse',
    isActive: true
  };

  const mockLocations: WarehouseLocationItem[] = [
    {
      locationId: 'loc-stage',
      warehouseLocationId: 'loc-stage',
      warehouseId: 'wh-1',
      locationCode: 'LOC-STAGE',
      locationName: 'Inbound Staging Bay',
      isReceivingLocation: true,
      isActive: true
    },
    {
      locationId: 'loc-bin-1',
      warehouseLocationId: 'loc-bin-1',
      warehouseId: 'wh-1',
      locationCode: 'R01-L01-B01',
      locationName: 'Rack 1 Level 1 Bin 1',
      zoneName: 'Zone A',
      isReceivingLocation: false,
      isActive: true
    }
  ];

  const mockReceiptLines: PutawayLineDto[] = [
    {
      lineId: 'line-1',
      productId: 'p-1',
      productCode: 'PRD-001',
      productName: 'Industrial Bolt M10',
      unitOfMeasure: 'EA',
      sourceQuantity: 100,
      alreadyPutawayQuantity: 20,
      outstandingQuantity: 80,
      putawayQuantity: 80,
      sourceLocationId: 'loc-stage',
      destinationLocationId: 'loc-bin-1',
      destinationLocationCode: 'R01-L01-B01'
    }
  ];

  const mockExistingRecord: PutawayRecordDto = {
    putawayId: 'pa-101',
    putawayNumber: 'PA-2026-0001',
    putawayDate: '2026-10-21',
    sourceDocumentType: 'StockReceipt',
    sourceDocumentId: 'sr-1',
    sourceDocumentNumber: 'SR-2026-00125',
    warehouseId: 'wh-1',
    warehouseCode: 'MAIN',
    warehouseName: 'Main Warehouse',
    status: PutawayStatus.Completed,
    statusName: 'Completed',
    totalLines: 1,
    totalQuantity: 80,
    lines: mockReceiptLines,
    createdAt: '2026-10-21T10:00:00Z'
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [StorePutawayPageComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        {
          provide: ActivatedRoute,
          useValue: {
            paramMap: of({ get: () => 'new' }),
            snapshot: { paramMap: { get: () => 'new' } }
          }
        }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(StorePutawayPageComponent);
    component = fixture.componentInstance;
    putawayService = TestBed.inject(PutawayService);
    masterDataService = TestBed.inject(MasterDataService);
    authService = TestBed.inject(AuthService);
    router = TestBed.inject(Router);

    vi.spyOn(authService, 'isTenantAdmin').mockReturnValue(true);
    vi.spyOn(masterDataService, 'getWarehouses').mockReturnValue(of([mockWarehouse]));
    vi.spyOn(masterDataService, 'getWarehouseLocations').mockReturnValue(of(mockLocations));
    vi.spyOn(putawayService, 'getNextPutawayNumber').mockReturnValue('PA-2026-0001');
    vi.spyOn(putawayService, 'getAvailableSourceReceipts').mockReturnValue(of([
      {
        id: 'sr-1',
        number: 'SR-2026-00125',
        type: 'StockReceipt',
        date: '2026-10-21',
        warehouseId: 'wh-1',
        warehouseName: 'Main Warehouse',
        lineCount: 1,
        outstandingQuantity: 80
      }
    ]));
    vi.spyOn(putawayService, 'getLinesForSourceReceipt').mockReturnValue(of(mockReceiptLines));
    component.headerForm.controls.warehouseId.setValue('wh-1');
  });

  it('should initialize in create mode with generated putaway number', () => {
    fixture.detectChanges();

    expect(component).toBeTruthy();
    expect(component.mode()).toBe('create');
    expect(component.headerForm.controls.putawayNumber.value).toBe('PA-2026-0001');
    expect(component.pageTitle()).toBe('New Storage & Putaway');
  });

  it('should populate detail lines when a source receipt is selected', () => {
    fixture.detectChanges();

    component.onSourceReceiptSelect('sr-1');

    expect(component.workingLines().length).toBe(1);
    expect(component.workingLines()[0].productCode).toBe('PRD-001');
    expect(component.workingLines()[0].outstandingQuantity).toBe(80);
    expect(component.totalPutawayQuantity()).toBe(80);
  });

  it('should validate destination location before confirming putaway', () => {
    fixture.detectChanges();
    component.headerForm.controls.warehouseId.setValue('wh-1');

    component.workingLines.set([
      {
        ...mockReceiptLines[0],
        destinationLocationId: '' // missing location
      }
    ]);

    component.confirmPutaway();
    expect(component.feedbackMessage()?.type).toBe('error');
    expect(component.feedbackMessage()?.text).toContain('Destination location is required');
  });

  it('should prevent over-putaway exceeding outstanding quantity', () => {
    fixture.detectChanges();
    component.headerForm.controls.warehouseId.setValue('wh-1');

    component.workingLines.set([
      {
        ...mockReceiptLines[0],
        putawayQuantity: 999 // exceeds outstanding 80
      }
    ]);

    component.confirmPutaway();
    expect(component.feedbackMessage()?.type).toBe('error');
    expect(component.feedbackMessage()?.text).toContain('exceeds outstanding quantity');
  });

  it('should save putaway draft successfully', () => {
    fixture.detectChanges();
    component.headerForm.controls.warehouseId.setValue('wh-1');
    component.workingLines.set([...mockReceiptLines]);

    const createSpy = vi.spyOn(putawayService, 'createPutaway').mockReturnValue(
      of({ ...mockExistingRecord, status: PutawayStatus.Draft, statusName: 'Draft' })
    );

    component.saveDraft();
    expect(createSpy).toHaveBeenCalledWith(expect.anything(), true);
    expect(component.feedbackMessage()?.type).toBe('success');
  });

  it('should confirm putaway and mark status as completed', () => {
    fixture.detectChanges();
    component.headerForm.controls.warehouseId.setValue('wh-1');
    component.workingLines.set([...mockReceiptLines]);

    const createSpy = vi.spyOn(putawayService, 'createPutaway').mockReturnValue(
      of({ ...mockExistingRecord, status: PutawayStatus.Completed, statusName: 'Completed' })
    );

    component.confirmPutaway();
    expect(createSpy).toHaveBeenCalledWith(expect.anything(), false);
    expect(component.feedbackMessage()?.type).toBe('success');
  });

  describe('F2 authorization and error states', () => {
    it('should block the create form with Access Denied when warehouses return 403', () => {
      vi.spyOn(masterDataService, 'getWarehouses').mockReturnValue(
        throwError(() => new HttpErrorResponse({ status: 403, statusText: 'Forbidden' }))
      );

      fixture.detectChanges();

      expect(component.isAccessDenied()).toBe(true);

      const compiled = fixture.nativeElement as HTMLElement;
      expect(compiled.textContent).toContain('Access Restricted');
      expect(compiled.textContent).not.toContain('Http failure response');
      // The transaction form must not render against empty warehouse master data.
      expect(compiled.querySelector('.putaway-content-container')).toBeNull();
    });

    it('should show a safe retryable error instead of raw transport text for a 500', () => {
      vi.spyOn(masterDataService, 'getWarehouses').mockReturnValue(
        throwError(
          () =>
            new HttpErrorResponse({
              status: 500,
              statusText: 'Internal Server Error',
              url: 'http://localhost:4200/api/warehouses'
            })
        )
      );

      fixture.detectChanges();

      expect(component.isAccessDenied()).toBe(false);
      expect(component.error()).toBeTruthy();
      expect(component.error()).not.toMatch(/http failure response/i);

      const compiled = fixture.nativeElement as HTMLElement;
      expect(compiled.textContent).toContain('Retry');
      expect(compiled.textContent).not.toContain('Http failure response');
    });

    it('should re-invoke warehouse loading when retried after failure', () => {
      const warehouseSpy = vi
        .spyOn(masterDataService, 'getWarehouses')
        .mockReturnValue(
          throwError(() => new HttpErrorResponse({ status: 0, statusText: 'Unknown Error' }))
        );

      fixture.detectChanges();
      expect(component.error()).toBeTruthy();

      warehouseSpy.mockReturnValue(of([mockWarehouse]));
      component.initModeAndData();

      expect(warehouseSpy).toHaveBeenCalledTimes(2);
      expect(component.error()).toBeNull();
      expect(component.warehouses().length).toBe(1);
    });
  });

  describe('Destination Location Filtering & Hierarchy Formatting (Scenarios 13-14)', () => {
    it('Scenario 13: should filter out inactive, receiving dock, canPutAway=false and canStoreInventory=false locations', () => {
      const complexLocations: WarehouseLocationItem[] = [
        {
          locationId: 'loc-active-storage',
          warehouseId: 'wh-1',
          locationCode: 'LOC-STORAGE',
          locationName: 'Storage Bin 1',
          isActive: true,
          isReceivingLocation: false,
          canPutAway: true,
          canStoreInventory: true
        },
        {
          locationId: 'loc-dock',
          warehouseId: 'wh-1',
          locationCode: 'LOC-DOCK',
          locationName: 'Receiving Dock',
          isActive: true,
          isReceivingLocation: true,
          canPutAway: true,
          canStoreInventory: true
        },
        {
          locationId: 'loc-inactive',
          warehouseId: 'wh-1',
          locationCode: 'LOC-INACTIVE',
          locationName: 'Inactive Bin',
          isActive: false,
          isReceivingLocation: false,
          canPutAway: true,
          canStoreInventory: true
        },
        {
          locationId: 'loc-no-putaway',
          warehouseId: 'wh-1',
          locationCode: 'LOC-NO-PUTAWAY',
          locationName: 'No Putaway Bin',
          isActive: true,
          isReceivingLocation: false,
          canPutAway: false,
          canStoreInventory: true
        },
        {
          locationId: 'loc-no-store',
          warehouseId: 'wh-1',
          locationCode: 'LOC-NO-STORE',
          locationName: 'No Store Bin',
          isActive: true,
          isReceivingLocation: false,
          canPutAway: true,
          canStoreInventory: false
        },
        {
          locationId: 'loc-other-wh',
          warehouseId: 'wh-2',
          locationCode: 'LOC-OTHER-WH',
          locationName: 'Other Warehouse Bin',
          isActive: true,
          isReceivingLocation: false,
          canPutAway: true,
          canStoreInventory: true
        }
      ];

      component.warehouseLocations.set(complexLocations);
      component.headerForm.controls.warehouseId.setValue('wh-1');

      const options = component.destinationLocationOptions();
      expect(options.length).toBe(1);
      expect(options[0].value).toBe('loc-active-storage');
    });

    it('Scenario 14: should format location hierarchy using hierarchyPath if available, or fallback to zone/name', () => {
      const locWithPath: WarehouseLocationItem = {
        locationId: 'loc-hier',
        warehouseId: 'wh-1',
        locationCode: 'LOC-H1',
        locationName: 'Bin 1',
        isActive: true,
        hierarchyPath: 'WH-01 > Zone A > Aisle 1 > Rack 2 > Shelf 3 > Bin 1'
      };

      const formattedPath = component.formatLocationHierarchy(locWithPath);
      expect(formattedPath).toBe('WH-01 > Zone A > Aisle 1 > Rack 2 > Shelf 3 > Bin 1 (Bin 1)');

      const locWithoutPath: WarehouseLocationItem = {
        locationId: 'loc-no-hier',
        warehouseId: 'wh-1',
        locationCode: 'LOC-H2',
        locationName: 'Bin 2',
        isActive: true,
        zoneName: 'Inbound Zone'
      };

      const fallbackFormatted = component.formatLocationHierarchy(locWithoutPath);
      expect(fallbackFormatted).toBe('Inbound Zone / LOC-H2 (Bin 2)');
    });
  });

  describe('Authorization & Multi-bin / Partial Putaway', () => {
    it('should deny access when user lacks PUTAWAY.POST navigating to /new', () => {
      vi.spyOn(authService, 'isTenantAdmin').mockReturnValue(false);
      vi.spyOn(authService, 'hasPermission').mockImplementation((perm: string) => {
        return perm === 'INVENTORY.VIEW'; // lacks PUTAWAY.POST
      });

      component.initModeAndData();
      expect(component.isAccessDenied()).toBe(true);
    });

    it('should split line to multiple destination bins correctly', () => {
      fixture.detectChanges();
      component.workingLines.set([
        {
          ...mockReceiptLines[0],
          putawayQuantity: 80,
          outstandingQuantity: 80
        }
      ]);

      component.splitLine('line-1');

      expect(component.workingLines().length).toBe(2);
      expect(component.workingLines()[0].putawayQuantity).toBe(40);
      expect(component.workingLines()[1].putawayQuantity).toBe(40);
      expect(component.totalPutawayQuantity()).toBe(80);
    });
  });

  describe('Put Away Source & Destination UX Corrections', () => {
    it('1. should display valid different destination when source and destination are different', () => {
      const locations: WarehouseLocationItem[] = [
        {
          locationId: 'loc-source-dock',
          warehouseId: 'wh-1',
          locationCode: 'LOC-DOCK',
          locationName: 'Receiving Dock',
          isActive: true,
          isReceivingLocation: true,
          canPutAway: false,
          canStoreInventory: false
        },
        {
          locationId: 'loc-storage-bin-1',
          warehouseId: 'wh-1',
          locationCode: 'LOC-BIN-1',
          locationName: 'Storage Bin 1',
          isActive: true,
          isReceivingLocation: false,
          canPutAway: true,
          canStoreInventory: true
        }
      ];

      component.warehouseLocations.set(locations);
      component.headerForm.controls.warehouseId.setValue('wh-1');

      const line: EditablePutawayLine = {
        ...mockReceiptLines[0],
        sourceLocationId: 'loc-source-dock',
        destinationLocationId: ''
      };
      component.workingLines.set([line]);

      const options = component.getDestinationOptionsForLine(line);
      expect(options.length).toBe(1);
      expect(options[0].value).toBe('loc-storage-bin-1');
    });

    it('2. should leave destination blank when source is the only eligible storage location in warehouse', () => {
      const locations: WarehouseLocationItem[] = [
        {
          locationId: 'loc-stk-01',
          warehouseId: 'wh-1',
          locationCode: 'LOC-STK-01',
          locationName: 'Main Rack A-01',
          isActive: true,
          isReceivingLocation: false,
          canPutAway: true,
          canStoreInventory: true
        }
      ];

      component.warehouseLocations.set(locations);
      component.headerForm.controls.warehouseId.setValue('wh-1');

      const line: EditablePutawayLine = {
        ...mockReceiptLines[0],
        sourceLocationId: 'loc-stk-01',
        destinationLocationId: ''
      };

      const destOptions = component.getDestinationOptionsForLine(line);
      expect(destOptions.length).toBe(0);
    });

    it('3. should strictly exclude source location from destination options', () => {
      const locations: WarehouseLocationItem[] = [
        {
          locationId: 'loc-source-stk',
          warehouseId: 'wh-1',
          locationCode: 'LOC-SRC',
          locationName: 'Source Bin',
          isActive: true,
          isReceivingLocation: false,
          canPutAway: true,
          canStoreInventory: true
        },
        {
          locationId: 'loc-dest-stk',
          warehouseId: 'wh-1',
          locationCode: 'LOC-DST',
          locationName: 'Dest Bin',
          isActive: true,
          isReceivingLocation: false,
          canPutAway: true,
          canStoreInventory: true
        }
      ];

      component.warehouseLocations.set(locations);
      component.headerForm.controls.warehouseId.setValue('wh-1');

      const testLine: EditablePutawayLine = {
        ...mockReceiptLines[0],
        sourceLocationId: 'loc-source-stk'
      };

      const options = component.getDestinationOptionsForLine(testLine);
      expect(options.length).toBe(1);
      expect(options.some(o => o.value === 'loc-source-stk')).toBe(false);
      expect(options[0].value).toBe('loc-dest-stk');
    });

    it('4. should display original receipt location as read-only metadata', () => {
      const loc: WarehouseLocationItem = {
        locationId: 'loc-stage',
        warehouseId: 'wh-1',
        locationCode: 'LOC-STAGE',
        locationName: 'Inbound Staging Bay',
        zoneName: 'Receiving Dock Zone',
        isActive: true,
        hierarchyPath: 'WH-01 / Receiving Dock Zone / LOC-STAGE'
      };

      component.warehouses.set([mockWarehouse]);
      component.warehouseLocations.set([loc]);
      component.headerForm.controls.warehouseId.setValue('wh-1');
      component.workingLines.set([{
        ...mockReceiptLines[0],
        sourceLocationId: 'loc-stage',
        sourceLocationCode: 'LOC-STAGE',
        sourceLocationName: 'Inbound Staging Bay'
      }]);

      const srcDetails = component.sourceReceiptLocationDetails();
      expect(srcDetails).toBeTruthy();
      expect(srcDetails?.locationCode).toBe('LOC-STAGE');
      expect(srcDetails?.locationName).toBe('Inbound Staging Bay');
      expect(srcDetails?.zoneName).toBe('Receiving Dock Zone');
      expect(srcDetails?.hierarchyPath).toContain('LOC-STAGE');
    });

    it('5. should reject identical source and destination IDs on confirmation', () => {
      component.headerForm.controls.warehouseId.setValue('wh-1');
      component.workingLines.set([
        {
          ...mockReceiptLines[0],
          sourceLocationId: 'loc-same-bin',
          destinationLocationId: 'loc-same-bin'
        }
      ]);

      component.confirmPutaway();

      expect(component.feedbackMessage()?.type).toBe('error');
      expect(component.feedbackMessage()?.text).toContain('Source and destination locations cannot be identical');
    });

    it('6. should format destination label in movement summary correctly', () => {
      const loc: WarehouseLocationItem = {
        locationId: 'loc-bin-42',
        warehouseId: 'wh-1',
        locationCode: 'BIN-42',
        locationName: 'Shelf 4 Bin 2',
        isActive: true
      };
      component.warehouseLocations.set([loc]);

      const label = component.getDestinationLocationLabel('loc-bin-42');
      expect(label).toBe('BIN-42 (Shelf 4 Bin 2)');

      const unassigned = component.getDestinationLocationLabel('');
      expect(unassigned).toBe('(Select Destination Bin)');
    });
  });
});
