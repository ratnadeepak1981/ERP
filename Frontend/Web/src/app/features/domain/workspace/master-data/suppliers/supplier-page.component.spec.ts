import { describe, it, expect, beforeEach, vi } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router, ActivatedRoute } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { of, throwError } from 'rxjs';
import { SupplierPageComponent } from './supplier-page.component';
import { MasterDataService } from '../../../../../core/services/master-data.service';
import { WorkspaceScopeService } from '../../../../../core/services/workspace-scope.service';
import { SupplierItem } from '../../../../../core/models/scope.models';

describe('SupplierPageComponent', () => {
  let component: SupplierPageComponent;
  let fixture: ComponentFixture<SupplierPageComponent>;
  let masterDataService: MasterDataService;
  let router: Router;

  const mockSupplier: SupplierItem = {
    supplierId: 's-101',
    tenantId: 't1',
    supplierCode: 'SUP-001',
    supplierName: 'Prime Materials Ltd',
    contactName: 'John Doe',
    email: 'vendor@supplier.com',
    phone: '+1 555-0199',
    city: 'Colombo',
    country: 'Sri Lanka',
    paymentTerms: 'Net 30',
    leadTimeDays: 7,
    isActive: true
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SupplierPageComponent],
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

    fixture = TestBed.createComponent(SupplierPageComponent);
    component = fixture.componentInstance;
    masterDataService = TestBed.inject(MasterDataService);
    router = TestBed.inject(Router);
  });

  it('should initialize in create mode with empty code/name and default terms', () => {
    fixture.detectChanges();

    expect(component).toBeTruthy();
    expect(component.mode()).toBe('create');
    expect(component.pageTitle()).toBe('New Supplier Profile');
    expect(component.supplierForm.get('supplierCode')?.value).toBe('');
    expect(component.supplierForm.get('supplierName')?.value).toBe('');
    expect(component.supplierForm.get('paymentTerms')?.value).toBe('Net 30');
    expect(component.supplierForm.get('leadTimeDays')?.value).toBe(7);
  });

  it('should validate form and reject save when required fields are missing', () => {
    fixture.detectChanges();

    component.supplierForm.patchValue({
      supplierCode: '',
      supplierName: ''
    });

    component.saveSupplier();

    expect(component.supplierForm.invalid).toBe(true);
    expect(component.message()?.severity).toBe('danger');
    expect(component.message()?.text).toContain('Please correct the highlighted form errors');
  });

  it('should save new supplier and navigate to supplier master list', () => {
    const createSpy = vi.spyOn(masterDataService, 'createSupplier').mockReturnValue(of(mockSupplier));
    const navSpy = vi.spyOn(router, 'navigate');

    fixture.detectChanges();

    component.supplierForm.patchValue({
      supplierCode: 'SUP-001',
      supplierName: 'Prime Materials Ltd',
      contactName: 'John Doe',
      email: 'vendor@supplier.com',
      phone: '+1 555-0199',
      city: 'Colombo',
      paymentTerms: 'Net 30'
    });

    component.saveSupplier();

    expect(createSpy).toHaveBeenCalledWith(
      expect.objectContaining({
        supplierCode: 'SUP-001',
        supplierName: 'Prime Materials Ltd',
        city: 'Colombo',
        paymentTerms: 'Net 30'
      })
    );
    expect(component.message()?.severity).toBe('success');
  });

  it('should load supplier and disable code in edit mode', () => {
    const route = TestBed.inject(ActivatedRoute);
    vi.spyOn(route.snapshot.paramMap, 'get').mockImplementation((k: string) => (k === 'id' ? 's-101' : null));
    vi.spyOn(masterDataService, 'getSupplier').mockReturnValue(of(mockSupplier));

    fixture.detectChanges();

    expect(component.mode()).toBe('edit');
    expect(component.supplierId()).toBe('s-101');
    expect(component.pageTitle()).toContain('SUP-001');
    expect(component.supplierForm.get('supplierCode')?.value).toBe('SUP-001');
    expect(component.supplierForm.get('supplierCode')?.disabled).toBe(true);
    expect(component.supplierForm.get('supplierName')?.value).toBe('Prime Materials Ltd');
  });

  it('should update existing supplier in edit mode', () => {
    const route = TestBed.inject(ActivatedRoute);
    vi.spyOn(route.snapshot.paramMap, 'get').mockImplementation((k: string) => (k === 'id' ? 's-101' : null));
    vi.spyOn(masterDataService, 'getSupplier').mockReturnValue(of(mockSupplier));
    const updateSpy = vi.spyOn(masterDataService, 'updateSupplier').mockReturnValue(of(mockSupplier));

    fixture.detectChanges();

    component.supplierForm.patchValue({
      supplierName: 'Prime Materials Ltd — Updated'
    });

    component.saveSupplier();

    expect(updateSpy).toHaveBeenCalledWith(
      's-101',
      expect.objectContaining({
        supplierName: 'Prime Materials Ltd — Updated'
      })
    );
    expect(component.message()?.severity).toBe('success');
  });

  it('should display server error message when API call fails', () => {
    vi.spyOn(masterDataService, 'createSupplier').mockReturnValue(
      throwError(() => ({ error: { message: 'Supplier code SUP-001 already exists.' } }))
    );

    fixture.detectChanges();

    component.supplierForm.patchValue({
      supplierCode: 'SUP-001',
      supplierName: 'Duplicate Supplier'
    });

    component.saveSupplier();

    expect(component.saving()).toBe(false);
    expect(component.message()?.severity).toBe('danger');
    expect(component.message()?.text).toContain('already exists');
  });

  it('should load active currencies and populate currency dropdown options', () => {
    const mockCurrencies = [
      { currencyId: 'c-usd', tenantId: 't1', currencyCode: 'USD', currencyName: 'US Dollar', symbol: '$', decimalPlaces: 2, isActive: true },
      { currencyId: 'c-lkr', tenantId: 't1', currencyCode: 'LKR', currencyName: 'Sri Lankan Rupee', symbol: 'Rs.', decimalPlaces: 2, isActive: true },
      { currencyId: 'c-old', tenantId: 't1', currencyCode: 'OLD', currencyName: 'Inactive Coin', symbol: 'O', decimalPlaces: 2, isActive: false }
    ];
    vi.spyOn(masterDataService, 'getCurrencies').mockReturnValue(of(mockCurrencies));

    fixture.detectChanges();

    const options = component.currencyOptions();
    expect(options.length).toBe(3);
    expect(options[0].label).toContain('None');
    expect(options[1].label).toContain('USD');
    expect(options[2].label).toContain('LKR');
  });

  it('should save selected currencyId and support clearing currency to null', () => {
    const createSpy = vi.spyOn(masterDataService, 'createSupplier').mockReturnValue(of(mockSupplier));
    fixture.detectChanges();

    component.supplierForm.patchValue({
      supplierCode: 'SUP-CURR',
      supplierName: 'Currency Vendor',
      currencyId: 'c-usd'
    });

    component.saveSupplier();

    expect(createSpy).toHaveBeenCalledWith(
      expect.objectContaining({
        currencyId: 'c-usd'
      })
    );

    component.supplierForm.patchValue({
      currencyId: ''
    });

    component.saveSupplier();

    expect(createSpy).toHaveBeenLastCalledWith(
      expect.objectContaining({
        currencyId: null
      })
    );
  });
});
