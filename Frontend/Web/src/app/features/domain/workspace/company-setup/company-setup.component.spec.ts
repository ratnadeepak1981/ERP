import { describe, it, expect, beforeEach, vi } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { CompanySetupComponent } from './company-setup.component';
import { CompanyService } from '../../../../core/services/company.service';
import { MasterDataService } from '../../../../core/services/master-data.service';
import { of } from 'rxjs';

describe('CompanySetupComponent', () => {
  let component: CompanySetupComponent;
  let fixture: ComponentFixture<CompanySetupComponent>;
  let companyService: CompanyService;
  let masterDataService: MasterDataService;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CompanySetupComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        CompanyService,
        MasterDataService
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(CompanySetupComponent);
    component = fixture.componentInstance;
    companyService = TestBed.inject(CompanyService);
    masterDataService = TestBed.inject(MasterDataService);
  });

  it('should create and load company onboarding state', () => {
    vi.spyOn(masterDataService, 'getCurrencies').mockReturnValue(of([
      { currencyId: 'curr-1', tenantId: 't1', currencyCode: 'LKR', currencyName: 'Sri Lankan Rupee', symbol: 'Rs', decimalPlaces: 2, isActive: true }
    ]));
    vi.spyOn(companyService, 'getCompanies').mockReturnValue(of([
      { id: 'c1', tenantId: 't1', name: 'Alpha Manufacturing Ltd', code: 'ALPHA', isActive: true }
    ]));
    vi.spyOn(companyService, 'getBranches').mockReturnValue(of([
      { id: 'b1', companyId: 'c1', name: 'Colombo Main Branch', code: 'CMB', isActive: true }
    ]));
    vi.spyOn(masterDataService, 'getWarehouses').mockReturnValue(of([]));
    vi.spyOn(masterDataService, 'getProducts').mockReturnValue(of([]));

    fixture.detectChanges();

    expect(component).toBeTruthy();
    expect(component.companies().length).toBe(1);
    expect(component.branches().length).toBe(1);
    expect(component.steps().length).toBe(4);
    expect(component.steps()[0].status).toBe('completed');
    expect(component.activeCurrencies()).toHaveLength(1);
  });

  it('requires an active base currency before creating a company', () => {
    component.newCompanyName = 'New Company';
    component.newCompanyCode = 'NEW';
    component.newCompanyCurrencyId = '';

    component.saveCompany();

    expect(component.formError()).toBe('Select an active company base currency.');
  });
});
