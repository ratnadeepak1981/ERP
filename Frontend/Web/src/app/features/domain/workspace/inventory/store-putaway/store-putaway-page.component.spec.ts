import { describe, it, expect, beforeEach, vi } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter, ActivatedRoute, Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import { StorePutawayPageComponent } from './store-putaway-page.component';
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
    expect(component.pageTitle()).toBe('New Stores Putaway');
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
});
