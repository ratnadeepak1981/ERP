import { describe, it, expect, beforeEach, vi } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter, Router } from '@angular/router';
import { of } from 'rxjs';
import { StorePutawayRegisterComponent } from './store-putaway-register.component';
import { PutawayService } from '../../../../../core/services/putaway.service';
import { MasterDataService } from '../../../../../core/services/master-data.service';
import { AuthService } from '../../../../../core/auth/auth.service';
import { PutawayRecordDto, PutawayStatus } from '../../../../../core/models/putaway.models';

describe('StorePutawayRegisterComponent', () => {
  let component: StorePutawayRegisterComponent;
  let fixture: ComponentFixture<StorePutawayRegisterComponent>;
  let putawayService: PutawayService;
  let authService: AuthService;
  let router: Router;

  const mockPutaways: PutawayRecordDto[] = [
    {
      putawayId: 'pa-1',
      putawayNumber: 'PA-2026-0001',
      putawayDate: '2026-10-21',
      sourceDocumentType: 'StockReceipt',
      sourceDocumentId: 'sr-101',
      sourceDocumentNumber: 'SR-2026-00125',
      warehouseId: 'wh-1',
      warehouseCode: 'WH-001',
      warehouseName: 'Main Warehouse',
      status: PutawayStatus.Completed,
      statusName: 'Completed',
      totalLines: 2,
      totalQuantity: 110,
      notes: 'Initial putaway',
      lines: [],
      createdAt: '2026-10-21T10:00:00Z'
    },
    {
      putawayId: 'pa-2',
      putawayNumber: 'PA-2026-0002',
      putawayDate: '2026-10-21',
      sourceDocumentType: 'GoodsReceipt',
      sourceDocumentId: 'grn-201',
      sourceDocumentNumber: 'GRN-2026-00045',
      warehouseId: 'wh-1',
      warehouseCode: 'WH-001',
      warehouseName: 'Main Warehouse',
      status: PutawayStatus.Draft,
      statusName: 'Draft',
      totalLines: 1,
      totalQuantity: 50,
      notes: 'Pending rack clearance',
      lines: [],
      createdAt: '2026-10-21T11:00:00Z'
    }
  ];

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [StorePutawayRegisterComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(StorePutawayRegisterComponent);
    component = fixture.componentInstance;
    putawayService = TestBed.inject(PutawayService);
    authService = TestBed.inject(AuthService);
    router = TestBed.inject(Router);

    vi.spyOn(authService, 'isTenantAdmin').mockReturnValue(true);
    vi.spyOn(putawayService, 'getPutaways').mockReturnValue(of(mockPutaways));
  });

  it('should create and load stores putaway records', () => {
    fixture.detectChanges();

    expect(component).toBeTruthy();
    expect(component.putaways().length).toBe(2);
    expect(component.filteredPutaways().length).toBe(2);
  });

  it('should filter putaway records by status', () => {
    fixture.detectChanges();

    component.selectedStatusFilter.set('completed');
    expect(component.filteredPutaways().length).toBe(1);
    expect(component.filteredPutaways()[0].putawayNumber).toBe('PA-2026-0001');

    component.selectedStatusFilter.set('draft');
    expect(component.filteredPutaways().length).toBe(1);
    expect(component.filteredPutaways()[0].putawayNumber).toBe('PA-2026-0002');
  });

  it('should filter putaway records by search term', () => {
    fixture.detectChanges();

    component.searchTerm.set('GRN-2026');
    expect(component.filteredPutaways().length).toBe(1);
    expect(component.filteredPutaways()[0].putawayNumber).toBe('PA-2026-0002');
  });

  it('should navigate to create new putaway', () => {
    const navSpy = vi.spyOn(router, 'navigate');
    component.navigateToCreate();
    expect(navSpy).toHaveBeenCalledWith(['/app/inventory/store-putaway/new']);
  });
});
