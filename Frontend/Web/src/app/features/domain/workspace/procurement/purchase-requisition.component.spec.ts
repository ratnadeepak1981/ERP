import { describe, it, expect, beforeEach, vi } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { of, throwError } from 'rxjs';
import { PurchaseRequisitionComponent } from './purchase-requisition.component';
import { PurchaseRequisitionService } from '../../../../core/services/purchase-requisition.service';
import { WorkspaceScopeService } from '../../../../core/services/workspace-scope.service';
import { AuthService } from '../../../../core/auth/auth.service';
import {
  PurchaseRequisitionDto,
  PurchaseRequisitionStatus
} from '../../../../core/models/purchase-requisition.models';
import { Company, Branch } from '../../../../core/models/scope.models';

describe('PurchaseRequisitionComponent', () => {
  let component: PurchaseRequisitionComponent;
  let fixture: ComponentFixture<PurchaseRequisitionComponent>;
  let prService: PurchaseRequisitionService;
  let scopeService: WorkspaceScopeService;
  let authService: AuthService;
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

  const mockRequisitions: PurchaseRequisitionDto[] = [
    {
      id: 'pr-1',
      tenantId: 't1',
      companyId: mockCompany.id,
      branchId: mockBranch.id,
      requisitionNumber: 'PR-2026-001',
      requisitionDate: '2026-10-18T00:00:00Z',
      requestorUserId: 'u1',
      status: PurchaseRequisitionStatus.Draft,
      totalAmount: 155.0,
      notes: 'Monthly replenishment',
      isActive: true,
      items: [
        {
          id: 'pri-1',
          purchaseRequisitionId: 'pr-1',
          productId: 'p1',
          quantity: 10,
          estimatedUnitPrice: 15.5,
          estimatedLineTotal: 155.0,
          isActive: true
        }
      ]
    },
    {
      id: 'pr-2',
      tenantId: 't1',
      companyId: mockCompany.id,
      branchId: mockBranch.id,
      requisitionNumber: 'PR-2026-002',
      requisitionDate: '2026-10-19T00:00:00Z',
      requestorUserId: 'u2',
      status: PurchaseRequisitionStatus.Submitted,
      totalAmount: 320.0,
      notes: 'Urgent tools',
      isActive: true,
      items: []
    }
  ];

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [PurchaseRequisitionComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        PurchaseRequisitionService,
        WorkspaceScopeService,
        AuthService
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(PurchaseRequisitionComponent);
    component = fixture.componentInstance;
    prService = TestBed.inject(PurchaseRequisitionService);
    scopeService = TestBed.inject(WorkspaceScopeService);
    authService = TestBed.inject(AuthService);
    router = TestBed.inject(Router);

    vi.spyOn(scopeService, 'selectedCompany').mockReturnValue(mockCompany);
    vi.spyOn(scopeService, 'selectedBranch').mockReturnValue(mockBranch);
    vi.spyOn(scopeService, 'hasCompany').mockReturnValue(true);
    vi.spyOn(scopeService, 'branches').mockReturnValue([mockBranch]);
    vi.spyOn(authService, 'hasPermission').mockReturnValue(true);

    vi.spyOn(prService, 'getRequisitions').mockReturnValue(of(mockRequisitions));
  });

  it('should initialize and load requisitions', () => {
    fixture.detectChanges();
    expect(component.requisitions().length).toBe(2);
    expect(component.loading()).toBe(false);
  });

  it('should filter requisitions by status', () => {
    fixture.detectChanges();
    component.activeStatusFilter.set('draft');
    expect(component.filteredRequisitions().length).toBe(1);
    expect(component.filteredRequisitions()[0].requisitionNumber).toBe('PR-2026-001');

    component.activeStatusFilter.set('submitted');
    expect(component.filteredRequisitions().length).toBe(1);
    expect(component.filteredRequisitions()[0].requisitionNumber).toBe('PR-2026-002');
  });

  it('should search requisitions by number and notes', () => {
    fixture.detectChanges();
    component.searchTerm.set('replenishment');
    expect(component.filteredRequisitions().length).toBe(1);
    expect(component.filteredRequisitions()[0].id).toBe('pr-1');
  });

  it('should navigate to create new requisition', () => {
    const navSpy = vi.spyOn(router, 'navigate');
    component.navigateToCreate();
    expect(navSpy).toHaveBeenCalledWith(['/app/procurement/purchase-requisitions/new']);
  });

  it('should navigate to view requisition', () => {
    const navSpy = vi.spyOn(router, 'navigate');
    component.navigateToView(mockRequisitions[0]);
    expect(navSpy).toHaveBeenCalledWith(['/app/procurement/purchase-requisitions', 'pr-1']);
  });

  it('should navigate to edit draft requisition', () => {
    const navSpy = vi.spyOn(router, 'navigate');
    component.navigateToEdit(mockRequisitions[0]);
    expect(navSpy).toHaveBeenCalledWith(['/app/procurement/purchase-requisitions', 'pr-1', 'edit']);
  });

  it('should navigate to approve submitted requisition', () => {
    const navSpy = vi.spyOn(router, 'navigate');
    component.navigateToApproval(mockRequisitions[1]);
    expect(navSpy).toHaveBeenCalledWith(['/app/procurement/purchase-requisitions', 'pr-2', 'approve']);
  });

  it('should submit draft requisition from confirm modal', () => {
    fixture.detectChanges();
    const submitSpy = vi.spyOn(prService, 'submitRequisition').mockReturnValue(of(mockRequisitions[0]));
    component.openConfirmModal('submit', mockRequisitions[0]);
    expect(component.confirmModal()).toBeTruthy();

    component.executeConfirmModal();
    expect(submitSpy).toHaveBeenCalledWith(mockCompany.id, mockBranch.id, 'pr-1');
    expect(component.confirmModal()).toBeNull();
  });

  it('should delete draft requisition from confirm modal', () => {
    fixture.detectChanges();
    const deleteSpy = vi.spyOn(prService, 'deleteRequisition').mockReturnValue(of(undefined));
    component.openConfirmModal('delete', mockRequisitions[0]);
    expect(component.confirmModal()).toBeTruthy();

    component.executeConfirmModal();
    expect(deleteSpy).toHaveBeenCalledWith(mockCompany.id, mockBranch.id, 'pr-1');
    expect(component.confirmModal()).toBeNull();
  });
});
