import { describe, it, expect, beforeEach, vi } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { of, throwError } from 'rxjs';
import { SalesInvoiceRegisterComponent } from './sales-invoice-register.component';
import { SalesInvoiceService } from '../../../../../core/services/sales-invoice.service';
import { MasterDataService } from '../../../../../core/services/master-data.service';
import { WorkspaceScopeService } from '../../../../../core/services/workspace-scope.service';
import { AuthService } from '../../../../../core/auth/auth.service';
import { SalesInvoiceDto, SalesInvoiceStatus } from '../../../../../core/models/sales-invoice.models';
import { CustomerItem, Company, Branch } from '../../../../../core/models/scope.models';

describe('SalesInvoiceRegisterComponent', () => {
  let component: SalesInvoiceRegisterComponent;
  let fixture: ComponentFixture<SalesInvoiceRegisterComponent>;
  let invoiceService: SalesInvoiceService;
  let masterDataService: MasterDataService;
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

  const mockCustomers: CustomerItem[] = [
    {
      customerId: 'cust-1',
      tenantId: 't1',
      customerCode: 'CUST-001',
      customerName: 'Acme Mega Corp',
      isActive: true
    }
  ];

  const mockInvoices: SalesInvoiceDto[] = [
    {
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
      items: []
    },
    {
      id: 'inv-2',
      tenantId: 't1',
      companyId: mockCompany.id,
      branchId: mockBranch.id,
      customerId: 'cust-1',
      customerName: 'Acme Mega Corp',
      currency: 'USD',
      salesOrderId: 'so-2',
      orderNumber: 'SO-2026-002',
      salesShipmentId: 'ship-2',
      shipmentNumber: 'DSP-2026-002',
      invoiceNumber: 'INV-2026-002',
      invoiceDate: '2026-10-22',
      status: SalesInvoiceStatus.Confirmed,
      subtotal: 200,
      discountAmount: 0,
      taxAmount: 30,
      totalAmount: 230,
      items: []
    }
  ];

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SalesInvoiceRegisterComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        SalesInvoiceService,
        MasterDataService,
        WorkspaceScopeService,
        AuthService
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(SalesInvoiceRegisterComponent);
    component = fixture.componentInstance;
    invoiceService = TestBed.inject(SalesInvoiceService);
    masterDataService = TestBed.inject(MasterDataService);
    scopeService = TestBed.inject(WorkspaceScopeService);
    authService = TestBed.inject(AuthService);
    router = TestBed.inject(Router);

    vi.spyOn(scopeService, 'selectedCompany').mockReturnValue(mockCompany);
    vi.spyOn(scopeService, 'selectedBranch').mockReturnValue(mockBranch);
    vi.spyOn(masterDataService, 'getCustomers').mockReturnValue(of(mockCustomers));
    vi.spyOn(invoiceService, 'getInvoices').mockReturnValue(of(mockInvoices));
    vi.spyOn(router, 'navigate').mockResolvedValue(true);
  });

  it('should initialize and load sales invoices with metrics', () => {
    fixture.detectChanges();

    expect(component.invoices().length).toBe(2);
    expect(component.draftCount()).toBe(1);
    expect(component.confirmedCount()).toBe(1);
    expect(component.totalInvoiceValue()).toBe(333.5);
  });

  it('should filter invoices by search term and status', () => {
    fixture.detectChanges();

    component.activeStatusFilter.set('draft');
    expect(component.filteredInvoices().length).toBe(1);
    expect(component.filteredInvoices()[0].invoiceNumber).toBe('INV-2026-001');

    component.activeStatusFilter.set('all');
    component.searchTerm.set('INV-2026-002');
    expect(component.filteredInvoices().length).toBe(1);
    expect(component.filteredInvoices()[0].invoiceNumber).toBe('INV-2026-002');
  });

  it('should navigate to invoice view and edit draft correctly', () => {
    fixture.detectChanges();

    component.navigateToView(mockInvoices[0]);
    expect(router.navigate).toHaveBeenCalledWith(['/app/sales/invoices', 'inv-1']);

    component.navigateToEdit(mockInvoices[0]);
    expect(router.navigate).toHaveBeenCalledWith(['/app/sales/invoices', 'inv-1', 'edit']);
  });

  it('should handle 403 access denied gracefully', () => {
    vi.spyOn(invoiceService, 'getInvoices').mockReturnValue(throwError(() => ({ status: 403 })));
    fixture.detectChanges();

    expect(component.isAccessDenied()).toBe(true);
    expect(component.invoices().length).toBe(0);
  });
});
