import { describe, it, expect, beforeEach, vi } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router, ActivatedRoute } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { of } from 'rxjs';
import { SalesInvoicePageComponent } from './sales-invoice-page.component';
import { SalesInvoiceService } from '../../../../../core/services/sales-invoice.service';
import { WorkspaceScopeService } from '../../../../../core/services/workspace-scope.service';
import { AuthService } from '../../../../../core/auth/auth.service';
import { SalesInvoiceDto, SalesInvoiceStatus } from '../../../../../core/models/sales-invoice.models';
import { Company, Branch } from '../../../../../core/models/scope.models';

describe('SalesInvoicePageComponent', () => {
  let component: SalesInvoicePageComponent;
  let fixture: ComponentFixture<SalesInvoicePageComponent>;
  let invoiceService: SalesInvoiceService;
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
    name: 'Colombo Branch',
    code: 'CMB',
    isActive: true
  };

  const mockInvoice: SalesInvoiceDto = {
    id: 'inv-1',
    tenantId: 't1',
    companyId: mockCompany.id,
    branchId: mockBranch.id,
    customerId: 'cust-1',
    customerName: 'Acme Mega Corp',
    currency: 'USD',
    salesOrderId: 'so-1',
    orderNumber: 'SO-2026-001',
    salesShipmentId: 'ship-1',
    shipmentNumber: 'DSP-2026-001',
    invoiceNumber: 'INV-2026-001',
    invoiceDate: '2026-10-22',
    status: SalesInvoiceStatus.Draft,
    subtotal: 100,
    discountAmount: 10,
    taxAmount: 13.5,
    totalAmount: 103.5,
    items: [
      {
        id: 'inv-item-1',
        salesInvoiceId: 'inv-1',
        salesOrderItemId: 'so-item-1',
        salesShipmentItemId: 'ship-item-1',
        productId: 'prod-1',
        productCode: 'ITEM-001',
        productName: 'Solar Panel 400W',
        quantity: 5,
        unitPrice: 20,
        discountRate: 10,
        discountAmount: 10,
        taxRate: 15,
        taxAmount: 13.5,
        subtotal: 100,
        lineTotal: 103.5
      }
    ]
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SalesInvoicePageComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        SalesInvoiceService,
        WorkspaceScopeService,
        AuthService,
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: { get: () => 'inv-1' }
            }
          }
        }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(SalesInvoicePageComponent);
    component = fixture.componentInstance;
    invoiceService = TestBed.inject(SalesInvoiceService);
    scopeService = TestBed.inject(WorkspaceScopeService);
    authService = TestBed.inject(AuthService);
    router = TestBed.inject(Router);

    vi.spyOn(scopeService, 'selectedCompany').mockReturnValue(mockCompany);
    vi.spyOn(scopeService, 'selectedBranch').mockReturnValue(mockBranch);
    vi.spyOn(invoiceService, 'getInvoice').mockReturnValue(of(mockInvoice));
    vi.spyOn(invoiceService, 'confirmInvoice').mockReturnValue(of({
      ...mockInvoice,
      status: SalesInvoiceStatus.Confirmed
    }));
    vi.spyOn(router, 'navigate').mockResolvedValue(true);
  });

  it('should load invoice in draft state with recalculable lines', () => {
    fixture.detectChanges();

    expect(component.currentInvoice()?.id).toBe('inv-1');
    expect(component.isDraft()).toBe(true);
    expect(component.workingLines().length).toBe(1);
    expect(component.grandTotalAmount()).toBe(103.5);
  });

  it('should allow user to explicitly confirm invoice', () => {
    fixture.detectChanges();

    component.executeConfirm();
    expect(invoiceService.confirmInvoice).toHaveBeenCalledWith(mockCompany.id, mockBranch.id, 'inv-1', undefined);
    expect(component.isConfirmed()).toBe(true);
  });
});
