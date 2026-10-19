import { describe, it, expect, beforeEach, vi } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter, Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import { IssueStockRegisterComponent } from './issue-stock-register.component';
import { InventoryService } from '../../../../../core/services/inventory.service';
import {
  StockIssueDto,
  StockIssuePurpose,
  StockIssueStatus
} from '../../../../../core/models/inventory.models';
import { AuthService } from '../../../../../core/auth/auth.service';

describe('IssueStockRegisterComponent', () => {
  let component: IssueStockRegisterComponent;
  let fixture: ComponentFixture<IssueStockRegisterComponent>;
  let inventoryService: InventoryService;
  let router: Router;
  let authService: AuthService;

  const mockIssues: StockIssueDto[] = [
    {
      stockIssueId: 'issue-1',
      issueNumber: 'ISS-2026-0001',
      issueDate: '2026-10-19T10:00:00Z',
      purpose: StockIssuePurpose.InternalUse,
      purposeName: 'Internal Use / Maintenance',
      movementType: 'StockAdjustmentOut',
      status: StockIssueStatus.Posted,
      statusName: 'Posted',
      warehouseId: 'wh-1',
      warehouseCode: 'MAIN',
      warehouseName: 'Central Logistics Hub',
      warehouseLocationId: 'loc-1',
      locationCode: 'A1-01',
      locationName: 'Bin A1',
      productId: 'prod-1',
      productCode: 'STL-001',
      productName: 'Galvanized Steel Sheet',
      unitOfMeasure: 'SHT',
      quantity: 15,
      unitCost: 120,
      totalCost: 1800,
      sourceDocumentReference: 'MAINT-04',
      remarks: 'Maintenance rack repair',
      createdAt: '2026-10-19T10:00:00Z'
    },
    {
      stockIssueId: 'issue-2',
      issueNumber: 'ISS-2026-0002',
      issueDate: '2026-10-19T11:00:00Z',
      purpose: StockIssuePurpose.SalesDispatch,
      purposeName: 'Sales / Customer Dispatch',
      movementType: 'ShipmentDispatch',
      status: StockIssueStatus.Posted,
      statusName: 'Posted',
      warehouseId: 'wh-1',
      warehouseCode: 'MAIN',
      warehouseName: 'Central Logistics Hub',
      warehouseLocationId: 'loc-2',
      locationCode: 'A2-02',
      locationName: 'Bin A2',
      productId: 'prod-2',
      productCode: 'ALU-002',
      productName: 'Aluminum Profile',
      unitOfMeasure: 'BAR',
      quantity: 40,
      unitCost: 80,
      totalCost: 3200,
      sourceDocumentReference: 'SO-108',
      remarks: 'Customer delivery',
      createdAt: '2026-10-19T11:00:00Z'
    }
  ];

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [IssueStockRegisterComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(IssueStockRegisterComponent);
    component = fixture.componentInstance;
    inventoryService = TestBed.inject(InventoryService);
    router = TestBed.inject(Router);
    authService = TestBed.inject(AuthService);

    vi.spyOn(authService, 'isTenantAdmin').mockReturnValue(true);
    vi.spyOn(router, 'navigate').mockResolvedValue(true);
    vi.spyOn(inventoryService, 'getStockIssues').mockReturnValue(of(mockIssues));
  });

  it('should create component and load stock issues', () => {
    fixture.detectChanges();

    expect(component).toBeTruthy();
    expect(component.issues().length).toBe(2);
    expect(component.filteredIssues().length).toBe(2);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('.section-title, app-page-header')).toBeTruthy();
  });

  it('should filter stock issues by purpose pill', () => {
    fixture.detectChanges();

    component.activePurposeFilter.set('sales');
    expect(component.filteredIssues().length).toBe(1);
    expect(component.filteredIssues()[0].productCode).toBe('ALU-002');

    component.activePurposeFilter.set('internal');
    expect(component.filteredIssues().length).toBe(1);
    expect(component.filteredIssues()[0].productCode).toBe('STL-001');

    component.activePurposeFilter.set('all');
    expect(component.filteredIssues().length).toBe(2);
  });

  it('should search stock issues by keyword', () => {
    fixture.detectChanges();

    component.searchTerm.set('Steel');
    expect(component.filteredIssues().length).toBe(1);
    expect(component.filteredIssues()[0].productCode).toBe('STL-001');

    component.searchTerm.set('SO-108');
    expect(component.filteredIssues().length).toBe(1);
    expect(component.filteredIssues()[0].issueNumber).toBe('ISS-2026-0002');
  });

  it('should navigate to create page on action click', () => {
    component.navigateToCreate();
    expect(router.navigate).toHaveBeenCalledWith(['/app/inventory/issue-stock/new']);
  });

  it('should navigate to view details on row click', () => {
    component.navigateToIssue(mockIssues[0]);
    expect(router.navigate).toHaveBeenCalledWith(['/app/inventory/issue-stock', 'issue-1']);
  });

  it('should handle API 403 access denied gracefully', () => {
    vi.spyOn(inventoryService, 'getStockIssues').mockReturnValue(
      throwError(() => ({ status: 403, message: 'Forbidden' }))
    );

    component.loadStockIssues();

    expect(component.isAccessDenied()).toBe(true);
  });
});
