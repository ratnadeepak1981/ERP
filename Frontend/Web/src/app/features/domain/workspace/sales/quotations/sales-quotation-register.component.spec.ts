import { describe, it, expect, beforeEach, vi } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, provideRouter } from '@angular/router';
import { BehaviorSubject, Subject, of } from 'rxjs';
import { signal } from '@angular/core';

import { SalesQuotationRegisterComponent } from './sales-quotation-register.component';
import { SalesQuotationService } from '../../../../../core/services/sales-quotation.service';
import { WorkspaceScopeService } from '../../../../../core/services/workspace-scope.service';
import { AuthService } from '../../../../../core/auth/auth.service';
import {
  SalesQuotationDto,
  SalesQuotationStatus,
  SalesQuotationType
} from '../../../../../core/models/sales-quotation.models';

describe('SalesQuotationRegisterComponent', () => {
  let fixture: ComponentFixture<SalesQuotationRegisterComponent>;
  let component: SalesQuotationRegisterComponent;

  const company = { id: 'comp-1', name: 'Novadmin Tenant (Pvt) Ltd', code: 'NOVA', isActive: true };
  const branch = { id: 'br-1', name: 'Novadmin Tenant Main Branch', code: 'MAIN', companyId: 'comp-1', isActive: true };

  let scopeChangeSubject: Subject<any>;
  let queryParamsSubject: BehaviorSubject<Record<string, any>>;
  let permissions: ReturnType<typeof signal<Set<string>>>;
  let mockQuotationService: {
    getQuotations: ReturnType<typeof vi.fn>;
    approveQuotation: ReturnType<typeof vi.fn>;
    rejectQuotation: ReturnType<typeof vi.fn>;
    cancelQuotation: ReturnType<typeof vi.fn>;
    sendQuotationEmail: ReturnType<typeof vi.fn>;
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
    scopeChangeSubject = new Subject<any>();
    queryParamsSubject = new BehaviorSubject<Record<string, any>>({ tab: 'quotations' });
    permissions = signal(new Set(['SALES_QUOTATION.VIEW', 'SALES_QUOTATION.CONFIRM']));

    mockQuotationService = {
      getQuotations: vi.fn().mockReturnValue(of([
        makeQuotation({
          id: 'q-pending',
          quotationNumber: 'SQ-2026-0005',
          status: SalesQuotationStatus.Submitted,
          approvalStatus: 'Pending Approval',
          customerStatus: 'Not Sent',
          emailStatus: 'NotSent'
        }),
        makeQuotation({
          id: 'q-approved',
          quotationNumber: 'SQ-2026-0006',
          status: SalesQuotationStatus.Confirmed,
          approvalStatus: 'Approved',
          customerStatus: 'Not Sent',
          emailStatus: 'NotSent',
          approvedAtUtc: '2026-11-25T08:30:00Z'
        }),
        makeQuotation({
          id: 'q-rejected',
          quotationNumber: 'SQ-2026-0007',
          status: SalesQuotationStatus.RejectedInternal,
          approvalStatus: 'Approval Rejected',
          customerStatus: 'Not Sent',
          emailStatus: 'NotSent',
          rejectedAtUtc: '2026-11-25T08:31:00Z'
        }),
        makeQuotation({
          id: 'q-sent',
          quotationNumber: 'SQ-2026-0008',
          status: SalesQuotationStatus.Sent,
          approvalStatus: 'Approved',
          customerStatus: 'Sent to Customer',
          emailStatus: 'Sent',
          approvedAtUtc: '2026-11-25T08:30:00Z',
          sentAtUtc: '2026-11-25T08:40:00Z'
        }),
        makeQuotation({
          id: 'q-accepted',
          quotationNumber: 'SQ-2026-0009',
          status: SalesQuotationStatus.Accepted,
          approvalStatus: 'Approved',
          customerStatus: 'Customer Accepted',
          emailStatus: 'Sent'
        }),
        makeQuotation({
          id: 'q-declined',
          quotationNumber: 'SQ-2026-0010',
          status: SalesQuotationStatus.DeclinedByCustomer,
          approvalStatus: 'Approved',
          customerStatus: 'Customer Rejected',
          emailStatus: 'Sent'
        }),
        makeQuotation({
          id: 'q-converted',
          quotationNumber: 'SQ-2026-0011',
          status: SalesQuotationStatus.Converted,
          approvalStatus: 'Approved',
          customerStatus: 'Sales Order Created',
          emailStatus: 'Sent'
        })
      ])),
      approveQuotation: vi.fn(),
      rejectQuotation: vi.fn(),
      cancelQuotation: vi.fn(),
      sendQuotationEmail: vi.fn()
    };

    await TestBed.configureTestingModule({
      imports: [SalesQuotationRegisterComponent],
      providers: [
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: { queryParams: queryParamsSubject.asObservable() }
        },
        {
          provide: SalesQuotationService,
          useValue: mockQuotationService
        },
        {
          provide: WorkspaceScopeService,
          useValue: {
            selectedCompany: signal(company),
            selectedBranch: signal(branch),
            canOperateTransactions: signal(true),
            scopeChange$: scopeChangeSubject.asObservable()
          }
        },
        {
          provide: AuthService,
          useValue: {
            hasPermission: (perm: string) => permissions().has(perm)
          }
        }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(SalesQuotationRegisterComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('displays separate Approval Status and Customer Progress badges in Tab B', () => {
    expect(component.activeTab()).toBe('quotations');
    const text = fixture.nativeElement.textContent as string;

    expect(text).toContain('Pending Approval');
    expect(text).toContain('Approved');
    expect(text).toContain('Approval Rejected');
    expect(text).toContain('Not Sent');
    expect(text).toContain('Sent to Customer');
    expect(text).toContain('Customer Accepted');
    expect(text).toContain('Customer Rejected');
    expect(text).toContain('Sales Order Created');
    expect(text).not.toContain('Approved (Internal)');
    expect(text).not.toContain('Rejected (Internal)');
  });

  it('updates quotation list immediately to Approved after manager approves and refreshes from backend', () => {
    const pendingRow = component.quotations().find(q => q.id === 'q-pending')!;
    const approvedFromApi = makeQuotation({
      ...pendingRow,
      status: SalesQuotationStatus.Confirmed,
      approvalStatus: 'Approved',
      customerStatus: 'Not Sent',
      emailStatus: 'NotSent',
      approvedAtUtc: '2026-11-25T08:50:00Z'
    });

    mockQuotationService.approveQuotation.mockReturnValue(of(approvedFromApi));
    mockQuotationService.getQuotations.mockReturnValue(of([approvedFromApi]));

    component.remarksTargetQuotation.set(pendingRow);
    component.executeRemarksActionWith('approve', 'Commercial margin verified');
    fixture.detectChanges();

    expect(mockQuotationService.approveQuotation).toHaveBeenCalledWith(
      company.id,
      branch.id,
      'q-pending',
      'Commercial margin verified'
    );
    expect(mockQuotationService.getQuotations).toHaveBeenCalledTimes(2);
    expect(component.quotations()[0].approvalStatus).toBe('Approved');
    expect(component.quotations()[0].customerStatus).toBe('Not Sent');

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Approved');
    expect(text).toContain('Not Sent');
  });

  it('retains Approved status after simulated page refresh from persisted backend data', () => {
    const persistedApproved = makeQuotation({
      id: 'q-persisted',
      quotationNumber: 'SQ-2026-0099',
      status: SalesQuotationStatus.Confirmed,
      approvalStatus: 'Approved',
      customerStatus: 'Not Sent',
      emailStatus: 'NotSent',
      approvedAtUtc: '2026-11-25T09:00:00Z'
    });
    mockQuotationService.getQuotations.mockReturnValue(of([persistedApproved]));

    // Simulate re-initialization after page refresh
    component.ngOnInit();
    fixture.detectChanges();

    expect(component.quotations()[0].approvalStatus).toBe('Approved');
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('SQ-2026-0099');
    expect(text).toContain('Approved');
  });

  it('hides Approve and Reject actions when user lacks SALES_QUOTATION.CONFIRM permission', () => {
    permissions.set(new Set(['SALES_QUOTATION.VIEW']));
    fixture.detectChanges();
    expect(component.canApprove()).toBe(false);

    const pendingRow = component.quotations().find(q => q.id === 'q-pending')!;
    const approveAction = component.quotationActions.find(a => a.id === 'approve')!;
    const rejectAction = component.quotationActions.find(a => a.id === 'reject')!;

    expect(approveAction.visible!(pendingRow)).toBe(false);
    expect(rejectAction.visible!(pendingRow)).toBe(false);
  });
});
