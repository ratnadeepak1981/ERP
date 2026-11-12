import { describe, it, expect, beforeEach, vi } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { CompanySetupComponent } from './company-setup.component';
import { CompanyService } from '../../../../core/services/company.service';
import { MasterDataService } from '../../../../core/services/master-data.service';
import { TenantSetupService } from '../../../../core/services/tenant-setup.service';
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
        MasterDataService,
        TenantSetupService
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
    expect(component.steps().length).toBe(3);
    expect(component.steps()[0].status).toBe('completed');
    expect(component.activeCurrencies()).toHaveLength(1);
  });

  it('guarantees LKR base currency when tenant has zero existing domain currencies', () => {
    vi.spyOn(masterDataService, 'getCurrencies').mockReturnValue(of([]));
    vi.spyOn(companyService, 'getCompanies').mockReturnValue(of([]));

    fixture.detectChanges();

    expect(component.activeCurrencies().length).toBeGreaterThanOrEqual(1);
    const lkr = component.activeCurrencies().find(c => c.currencyCode === 'LKR');
    expect(lkr).toBeTruthy();
    expect(component.initCompanyCurrencyCode).toBe('LKR');
  });

  it('supports sequential navigation through steps 1, 2, and 3 without modal popups', () => {
    expect(component.activeOnboardingStep()).toBe(1);
    component.goToStep(2);
    expect(component.activeOnboardingStep()).toBe(2);
    component.goToStep(3);
    expect(component.activeOnboardingStep()).toBe(3);
    expect(component.selectedIndustryProfile).toBe('retail');
  });
});
