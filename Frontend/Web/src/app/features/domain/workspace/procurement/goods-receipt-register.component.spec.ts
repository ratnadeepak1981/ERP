import { describe, it, expect, beforeEach, vi } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { of, throwError } from 'rxjs';
import { GoodsReceiptRegisterComponent } from './goods-receipt-register.component';
import { GoodsReceiptNoteService } from '../../../../core/services/goods-receipt-note.service';
import { WorkspaceScopeService } from '../../../../core/services/workspace-scope.service';
import { AuthService } from '../../../../core/auth/auth.service';
import {
  GoodsReceiptNoteDto,
  GoodsReceiptNoteStatus
} from '../../../../core/models/goods-receipt-note.models';
import { Company, Branch } from '../../../../core/models/scope.models';

describe('GoodsReceiptRegisterComponent', () => {
  let component: GoodsReceiptRegisterComponent;
  let fixture: ComponentFixture<GoodsReceiptRegisterComponent>;
  let grnService: GoodsReceiptNoteService;
  let scopeService: WorkspaceScopeService;
  let router: Router;

  const mockCompany: Company = {
    id: 'c1111111-1111-1111-1111-111111111111',
    tenantId: 't1',
    name: 'Alpha Industries',
    code: 'ALPHA',
    isActive: true
  };

  const mockBranch: Branch = {
    id: 'b1111111-1111-1111-1111-111111111111',
    companyId: mockCompany.id,
    name: 'Colombo Plant',
    code: 'CMB',
    isActive: true
  };

  const mockReceipts: GoodsReceiptNoteDto[] = [
    {
      goodsReceiptNoteId: 'grn-1',
      tenantId: 't1',
      companyId: mockCompany.id,
      branchId: mockBranch.id,
      purchaseOrderId: 'po-1',
      grnNumber: 'GRN-2026-001',
      receiptDate: '2026-10-18T10:00:00Z',
      status: GoodsReceiptNoteStatus.Draft,
      deliveryNoteNumber: 'DN-1001',
      totalAmount: 150.0,
      lines: []
    },
    {
      goodsReceiptNoteId: 'grn-2',
      tenantId: 't1',
      companyId: mockCompany.id,
      branchId: mockBranch.id,
      purchaseOrderId: 'po-2',
      grnNumber: 'GRN-2026-002',
      receiptDate: '2026-10-17T10:00:00Z',
      status: GoodsReceiptNoteStatus.Confirmed,
      deliveryNoteNumber: 'DN-1002',
      totalAmount: 300.0,
      lines: []
    }
  ];

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [GoodsReceiptRegisterComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        GoodsReceiptNoteService,
        WorkspaceScopeService,
        AuthService
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(GoodsReceiptRegisterComponent);
    component = fixture.componentInstance;
    grnService = TestBed.inject(GoodsReceiptNoteService);
    scopeService = TestBed.inject(WorkspaceScopeService);
    router = TestBed.inject(Router);

    scopeService.selectedCompany.set(mockCompany);
    scopeService.selectedBranch.set(mockBranch);
    scopeService.isAllBranches.set(false);
    vi.spyOn(grnService, 'getGrns').mockReturnValue(of(mockReceipts));
  });

  it('should load goods receipts when scope is selected', () => {
    fixture.detectChanges();

    expect(component.receipts().length).toBe(2);
    expect(component.filteredReceipts().length).toBe(2);
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('GRN-2026-001');
    expect(compiled.textContent).toContain('GRN-2026-002');
  });

  it('should filter receipts by search term and status pill', () => {
    fixture.detectChanges();

    // Search by GRN number
    component.searchTerm.set('002');
    expect(component.filteredReceipts().length).toBe(1);
    expect(component.filteredReceipts()[0].grnNumber).toBe('GRN-2026-002');

    // Status filter Draft
    component.searchTerm.set('');
    component.activeStatusFilter.set('draft');
    expect(component.filteredReceipts().length).toBe(1);
    expect(component.filteredReceipts()[0].grnNumber).toBe('GRN-2026-001');
  });

  it('should navigate to view route when view action is triggered', () => {
    const navSpy = vi.spyOn(router, 'navigate');
    fixture.detectChanges();

    component.navigateToView(mockReceipts[0]);
    expect(navSpy).toHaveBeenCalledWith(['/app/procurement/goods-receipts', 'grn-1']);
  });

  it('should render AccessDeniedComponent on 403 Forbidden', () => {
    const forbiddenError = {
      status: 403,
      statusText: 'Forbidden',
      message: 'Http failure response: 403 Forbidden'
    };
    vi.spyOn(grnService, 'getGrns').mockReturnValue(throwError(() => forbiddenError));

    component.loadReceipts();
    fixture.detectChanges();

    expect(component.isAccessDenied()).toBe(true);
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('.grn-access-denied-wrapper')).toBeTruthy();
  });

  describe('Putaway Navigation & Visibility (Scenarios 9-10)', () => {
    it('Scenario 9: Confirmed GRN displays Putaway action and navigates to Put Away with preselected parameters', () => {
      const authService = TestBed.inject(AuthService);
      vi.spyOn(authService, 'isTenantAdmin').mockReturnValue(true);

      const putawayAction = component.tableActions.find(a => a.id === 'putaway');
      expect(putawayAction).toBeTruthy();

      // Confirmed receipt (mockReceipts[1]) should have visible = true
      const isVisible = putawayAction?.visible ? putawayAction.visible(mockReceipts[1]) : false;
      expect(isVisible).toBe(true);

      const navSpy = vi.spyOn(router, 'navigate');
      component.navigateToPutaway(mockReceipts[1]);
      expect(navSpy).toHaveBeenCalledWith(['/app/inventory/store-putaway/new'], {
        queryParams: {
          sourceType: 'GoodsReceipt',
          sourceId: 'grn-2'
        }
      });
    });

    it('Scenario 10: Unconfirmed GRN does not display Putaway action', () => {
      const authService = TestBed.inject(AuthService);
      vi.spyOn(authService, 'isTenantAdmin').mockReturnValue(true);

      const putawayAction = component.tableActions.find(a => a.id === 'putaway');
      expect(putawayAction).toBeTruthy();

      // Draft receipt (mockReceipts[0]) should have visible = false
      const isVisible = putawayAction?.visible ? putawayAction.visible(mockReceipts[0]) : false;
      expect(isVisible).toBe(false);
    });

    it('should hide Putaway action when user lacks PUTAWAY.POST', () => {
      const authService = TestBed.inject(AuthService);
      vi.spyOn(authService, 'isTenantAdmin').mockReturnValue(false);
      vi.spyOn(authService, 'isPlatformAdmin').mockReturnValue(false);
      vi.spyOn(authService, 'hasPermission').mockReturnValue(false);

      const putawayAction = component.tableActions.find(a => a.id === 'putaway');
      const isVisible = putawayAction?.visible ? putawayAction.visible(mockReceipts[1]) : false;
      expect(isVisible).toBe(false);
    });
  });
});
