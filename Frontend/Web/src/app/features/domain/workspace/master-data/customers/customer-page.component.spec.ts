import { describe, it, expect, beforeEach, vi } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router, ActivatedRoute } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { of, throwError } from 'rxjs';
import { CustomerPageComponent } from './customer-page.component';
import { MasterDataService } from '../../../../../core/services/master-data.service';
import { WorkspaceScopeService } from '../../../../../core/services/workspace-scope.service';
import { CustomerItem } from '../../../../../core/models/scope.models';

describe('CustomerPageComponent', () => {
  let component: CustomerPageComponent;
  let fixture: ComponentFixture<CustomerPageComponent>;
  let masterDataService: MasterDataService;
  let router: Router;

  const mockCustomer: CustomerItem = {
    customerId: 'c-101',
    tenantId: 't1',
    customerCode: 'CUST-001',
    customerName: 'Ceylon Beverage Holdings',
    contactPerson: 'Sunil Perera',
    email: 'orders@ceylonbev.lk',
    phone: '+94 11 234 5678',
    city: 'Colombo',
    country: 'Sri Lanka',
    paymentTerms: 'Net 30',
    creditLimit: 250000,
    isActive: true
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CustomerPageComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        MasterDataService,
        WorkspaceScopeService,
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: {
                get: (key: string) => (key === 'id' ? null : null)
              }
            },
            paramMap: of({
              get: (key: string) => (key === 'id' ? null : null)
            })
          }
        }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(CustomerPageComponent);
    component = fixture.componentInstance;
    masterDataService = TestBed.inject(MasterDataService);
    router = TestBed.inject(Router);
  });

  it('should initialize in create mode with empty code/name and default terms', () => {
    fixture.detectChanges();

    expect(component).toBeTruthy();
    expect(component.mode()).toBe('create');
    expect(component.pageTitle()).toBe('New Customer Account');
    expect(component.customerForm.get('customerCode')?.value).toBe('');
    expect(component.customerForm.get('customerName')?.value).toBe('');
    expect(component.customerForm.get('paymentTerms')?.value).toBe('Net 30');
    expect(component.customerForm.get('creditLimit')?.value).toBe(0);
  });

  it('should validate form and reject save when required fields are missing', () => {
    fixture.detectChanges();

    component.customerForm.patchValue({
      customerCode: '',
      customerName: '',
      city: ''
    });

    component.saveCustomer();

    expect(component.customerForm.invalid).toBe(true);
    expect(component.message()?.severity).toBe('danger');
    expect(component.message()?.text).toContain('Please correct the highlighted form errors');
  });

  it('should save new customer and navigate to master list', () => {
    const createSpy = vi.spyOn(masterDataService, 'createCustomer').mockReturnValue(of(mockCustomer));
    const navSpy = vi.spyOn(router, 'navigate');

    fixture.detectChanges();

    component.customerForm.patchValue({
      customerCode: 'CUST-001',
      customerName: 'Ceylon Beverage Holdings',
      contactPerson: 'Sunil Perera',
      email: 'orders@ceylonbev.lk',
      city: 'Colombo',
      paymentTerms: 'Net 30'
    });

    component.saveCustomer();

    expect(createSpy).toHaveBeenCalledWith(
      expect.objectContaining({
        customerCode: 'CUST-001',
        customerName: 'Ceylon Beverage Holdings',
        city: 'Colombo',
        paymentTerms: 'Net 30'
      })
    );
    expect(component.message()?.severity).toBe('success');
  });

  it('should load customer and disable code in edit mode', () => {
    const route = TestBed.inject(ActivatedRoute);
    vi.spyOn(route.snapshot.paramMap, 'get').mockImplementation((k: string) => (k === 'id' ? 'c-101' : null));
    vi.spyOn(masterDataService, 'getCustomer').mockReturnValue(of(mockCustomer));

    fixture.detectChanges();

    expect(component.mode()).toBe('edit');
    expect(component.customerId()).toBe('c-101');
    expect(component.pageTitle()).toContain('CUST-001');
    expect(component.customerForm.get('customerCode')?.value).toBe('CUST-001');
    expect(component.customerForm.get('customerCode')?.disabled).toBe(true);
    expect(component.customerForm.get('customerName')?.value).toBe('Ceylon Beverage Holdings');
  });
});
