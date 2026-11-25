import { describe, it, expect, beforeEach, vi } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { BehaviorSubject, Subject, of } from 'rxjs';
import { signal } from '@angular/core';

import { SalesQuotationPageComponent } from './sales-quotation-page.component';
import { SalesQuotationService } from '../../../../../core/services/sales-quotation.service';
import { MasterDataService } from '../../../../../core/services/master-data.service';
import { WorkspaceScopeService } from '../../../../../core/services/workspace-scope.service';
import { AuthService } from '../../../../../core/auth/auth.service';
import {
  SalesQuotationDto,
  SalesQuotationStatus,
  SalesQuotationType
} from '../../../../../core/models/sales-quotation.models';

describe('SalesQuotationPageComponent', () => {
  let fixture: ComponentFixture<SalesQuotationPageComponent>;
  let component: SalesQuotationPageComponent;

  const company = { id: 'comp-1', name: 'Novadmin Tenant (Pvt) Ltd', code: 'NOVA', isActive: true };
  const branch = { id: 'br-1', name: 'Novadmin Tenant Main Branch', code: 'MAIN', companyId: 'comp-1', isActive: true };

  let selectedCompanySig = signal<any>(company);
  let selectedBranchSig = signal<any>(branch);
  let canOperateSig = signal<boolean>(true);
  let scopeChangeSubject: Subject<any>;
  let paramMapSubject: BehaviorSubject<any>;
  let mockQuotationService: {
    getQuotation: ReturnType<typeof vi.fn>;
    getApprovalHistory: ReturnType<typeof vi.fn>;
    approveQuotation: ReturnType<typeof vi.fn>;
    rejectQuotation: ReturnType<typeof vi.fn>;
  };

  function makeQuotation(overrides: Partial<SalesQuotationDto> = {}): SalesQuotationDto {
    return {
      id: 'q-1',
      tenantId: 't-1',
      companyId: 'comp-1',
      branchId: 'br-1',
      customerId: 'cust-1',
      customerName: 'testcomp',
      customerCode: 'CUST-1',
      quotationNumber: 'SQ-2026-0005',
      revisionNumber: 0,
      documentType: SalesQuotationType.Standard,
      quotationDate: '2026-11-25T00:00:00Z',
      validUntilDateUtc: '2026-12-25T00:00:00Z',
      daysUntilExpiry: 30,
      isExpired: false,
      currency: 'USD',
      status: SalesQuotationStatus.Submitted,
      approvalStatus: 'Pending Approval',
      customerStatus: 'Not Sent',
      emailStatus: 'NotSent',
      subTotal: 22000,
      discountAmount: 0,
      taxAmount: 0,
      totalAmount: 22000,
      items: [],
      ...overrides
    };
  }

  beforeEach(async () => {
    selectedCompanySig = signal(company);
    selectedBranchSig = signal(branch);
    canOperateSig = signal(true);
    scopeChangeSubject = new Subject<any>();
    paramMapSubject = new BehaviorSubject(convertToParamMap({ id: 'q-1' }));

    const pendingQuote = makeQuotation();

    mockQuotationService = {
      getQuotation: vi.fn().mockReturnValue(of(pendingQuote)),
      getApprovalHistory: vi.fn().mockReturnValue(of([])),
      approveQuotation: vi.fn(),
      rejectQuotation: vi.fn()
    };

    await TestBed.configureTestingModule({
      imports: [SalesQuotationPageComponent],
      providers: [
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: { paramMap: convertToParamMap({ id: 'q-1' }) },
            paramMap: paramMapSubject.asObservable()
          }
        },
        {
          provide: SalesQuotationService,
          useValue: mockQuotationService
        },
        {
          provide: MasterDataService,
          useValue: {
            getCustomers: () => of([]),
            getProducts: () => of([]),
            getTaxCodes: () => of([])
          }
        },
        {
          provide: WorkspaceScopeService,
          useValue: {
            selectedCompany: selectedCompanySig,
            selectedBranch: selectedBranchSig,
            canOperateTransactions: canOperateSig,
            scopeChange$: scopeChangeSubject.asObservable()
          }
        },
        {
          provide: AuthService,
          useValue: {
            hasPermission: (perm: string) =>
              perm === 'SALES_QUOTATION.VIEW' ||
              perm === 'SALES_QUOTATION.CONFIRM' ||
              perm === 'SALES_QUOTATION.EDIT'
          }
        }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(SalesQuotationPageComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('transitions displayed status to Approved immediately upon manager approval', () => {
    const approvedQuote = makeQuotation({
      status: SalesQuotationStatus.Confirmed,
      approvalStatus: 'Approved',
      customerStatus: 'Not Sent',
      emailStatus: 'NotSent',
      approvedAtUtc: '2026-11-25T08:55:00Z'
    });

    mockQuotationService.approveQuotation.mockReturnValue(of(approvedQuote));
    mockQuotationService.getQuotation.mockReturnValue(of(approvedQuote));

    component.executeRemarksActionWith('approve', 'Approved for customer delivery');
    fixture.detectChanges();

    expect(mockQuotationService.approveQuotation).toHaveBeenCalledWith(
      company.id,
      branch.id,
      'q-1',
      'Approved for customer delivery'
    );
    expect(component.currentQuotation()?.approvalStatus).toBe('Approved');
    expect(component.currentQuotation()?.customerStatus).toBe('Not Sent');

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Approved');
    expect(text).toContain('Not Sent');
  });

  it('reloads persisted Approved status on page refresh even when scope initializes asynchronously', () => {
    const approvedQuote = makeQuotation({
      status: SalesQuotationStatus.Confirmed,
      approvalStatus: 'Approved',
      customerStatus: 'Not Sent',
      emailStatus: 'NotSent'
    });

    // Simulate page refresh where company/branch are initially null until WorkspaceScopeService resolves
    selectedCompanySig.set(null);
    selectedBranchSig.set(null);
    canOperateSig.set(false);
    mockQuotationService.getQuotation.mockClear();
    mockQuotationService.getQuotation.mockReturnValue(of(approvedQuote));

    component.ngOnInit();
    expect(mockQuotationService.getQuotation).not.toHaveBeenCalled();

    // WorkspaceScopeService finishes initialization asynchronously
    selectedCompanySig.set(company);
    selectedBranchSig.set(branch);
    canOperateSig.set(true);
    scopeChangeSubject.next({ company, branch });
    fixture.detectChanges();

    expect(mockQuotationService.getQuotation).toHaveBeenCalledWith(company.id, branch.id, 'q-1');
    expect(component.currentQuotation()?.approvalStatus).toBe('Approved');
  });

  it('does not render Send Quotation Email for internally rejected quotations', () => {
    const rejectedQuote = makeQuotation({
      status: SalesQuotationStatus.RejectedInternal,
      approvalStatus: 'Approval Rejected',
      customerStatus: 'Not Sent',
      rejectionRemarks: 'Margin below threshold'
    });

    mockQuotationService.getQuotation.mockReturnValue(of(rejectedQuote));
    component.loadExistingQuotation('q-1');
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Approval Rejected');
    expect(text).not.toContain('Send Quotation Email');
    expect(text).toContain('Revise Commercial Offer');
  });
});
