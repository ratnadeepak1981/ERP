import { describe, it, expect, beforeEach, vi } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { CurrencyMasterComponent } from './currency-master.component';
import { MasterDataService } from '../../../../../core/services/master-data.service';
import { AuthService } from '../../../../../core/auth/auth.service';
import { of } from 'rxjs';
import { CurrencyItem } from '../../../../../core/models/scope.models';
import { ActivatedRoute, provideRouter } from '@angular/router';

describe('CurrencyMasterComponent', () => {
  let component: CurrencyMasterComponent;
  let fixture: ComponentFixture<CurrencyMasterComponent>;
  let masterDataService: MasterDataService;
  let authService: AuthService;

  const mockCurrencies: CurrencyItem[] = [
    {
      currencyId: 'c1',
      tenantId: 't1',
      currencyCode: 'USD',
      currencyName: 'US Dollar',
      symbol: '$',
      decimalPlaces: 2,
      isActive: true
    },
    {
      currencyId: 'c2',
      tenantId: 't1',
      currencyCode: 'EUR',
      currencyName: 'Euro',
      symbol: '€',
      decimalPlaces: 2,
      isActive: true
    },
    {
      currencyId: 'c3',
      tenantId: 't1',
      currencyCode: 'OLD',
      currencyName: 'Old Currency',
      symbol: 'O',
      decimalPlaces: 0,
      isActive: false
    }
  ];

  beforeEach(async () => {
    const masterDataSpy = {
      getCurrencies: vi.fn().mockReturnValue(of(mockCurrencies)),
      getCurrency: vi.fn(),
      createCurrency: vi.fn(),
      updateCurrency: vi.fn(),
      deactivateCurrency: vi.fn(),
      activateCurrency: vi.fn()
    };

    const authSpy = {
      isTenantAdmin: vi.fn().mockReturnValue(true),
      isPlatformAdmin: vi.fn().mockReturnValue(false),
      hasPermission: vi.fn().mockReturnValue(true)
    };

    await TestBed.configureTestingModule({
      imports: [CurrencyMasterComponent],
      providers: [
        provideRouter([]),
        { provide: MasterDataService, useValue: masterDataSpy },
        { provide: AuthService, useValue: authSpy },
        { provide: ActivatedRoute, useValue: { snapshot: { data: {}, paramMap: { get: () => null } } } }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(CurrencyMasterComponent);
    component = fixture.componentInstance;
    masterDataService = TestBed.inject(MasterDataService);
    authService = TestBed.inject(AuthService);
    fixture.detectChanges();
  });

  it('should create the component and load currencies', () => {
    expect(component).toBeTruthy();
    expect(masterDataService.getCurrencies).toHaveBeenCalled();
    expect(component.currencies().length).toBe(3);
    expect(component.loading()).toBe(false);
  });

  it('should filter currencies by search query', () => {
    component.searchTerm.set('Euro');
    expect(component.filteredCurrencies().length).toBe(1);
    expect(component.filteredCurrencies()[0].currencyCode).toBe('EUR');

    component.searchTerm.set('$');
    expect(component.filteredCurrencies().length).toBe(1);
    expect(component.filteredCurrencies()[0].currencyCode).toBe('USD');
  });

  it('should filter currencies by status', () => {
    component.statusFilter.set('ACTIVE');
    expect(component.filteredCurrencies().length).toBe(2);
    expect(component.filteredCurrencies().every(c => c.isActive)).toBe(true);

    component.statusFilter.set('INACTIVE');
    expect(component.filteredCurrencies().length).toBe(1);
    expect(component.filteredCurrencies()[0].currencyCode).toBe('OLD');
  });

  it('should open create form with default values', () => {
    component.openCreateForm();
    expect(component.showForm()).toBe(true);
    expect(component.isEditing()).toBe(false);
    expect(component.currencyCode).toBe('');
    expect(component.decimalPlaces).toBe(2);
  });

  it('should create a new currency', () => {
    const newCurrency: CurrencyItem = {
      currencyId: 'c4',
      tenantId: 't1',
      currencyCode: 'GBP',
      currencyName: 'British Pound',
      symbol: '£',
      decimalPlaces: 2,
      isActive: true
    };
    (masterDataService.createCurrency as any).mockReturnValue(of(newCurrency));

    component.openCreateForm();
    component.currencyCode = 'GBP';
    component.currencyName = 'British Pound';
    component.symbol = '£';
    component.decimalPlaces = 2;

    component.saveCurrency();

    expect(masterDataService.createCurrency).toHaveBeenCalledWith({
      currencyCode: 'GBP',
      currencyName: 'British Pound',
      symbol: '£',
      decimalPlaces: 2
    });
    expect(component.currencies().some(c => c.currencyCode === 'GBP')).toBe(true);
    expect(component.showForm()).toBe(false);
    expect(component.message()?.severity).toBe('success');
  });

  it('should open edit form with populated currency values', () => {
    const usd = mockCurrencies[0];
    component.openEditForm(usd);

    expect(component.showForm()).toBe(true);
    expect(component.isEditing()).toBe(true);
    expect(component.currencyCode).toBe('USD');
    expect(component.currencyName).toBe('US Dollar');
    expect(component.symbol).toBe('$');
    expect(component.decimalPlaces).toBe(2);
  });

  it('should update an existing currency', () => {
    const updatedCurrency: CurrencyItem = {
      currencyId: 'c1',
      tenantId: 't1',
      currencyCode: 'USD',
      currencyName: 'United States Dollar',
      symbol: '$',
      decimalPlaces: 2,
      isActive: true
    };
    (masterDataService.updateCurrency as any).mockReturnValue(of(updatedCurrency));

    component.openEditForm(mockCurrencies[0]);
    component.currencyName = 'United States Dollar';

    component.saveCurrency();

    expect(masterDataService.updateCurrency).toHaveBeenCalledWith('c1', {
      currencyName: 'United States Dollar',
      symbol: '$',
      decimalPlaces: 2
    });
    const found = component.currencies().find(c => c.currencyId === 'c1');
    expect(found?.currencyName).toBe('United States Dollar');
    expect(component.showForm()).toBe(false);
  });

  it('should validate decimal places between 0 and 4', () => {
    component.openCreateForm();
    component.currencyCode = 'CAD';
    component.currencyName = 'Canadian Dollar';
    component.symbol = 'C$';
    component.decimalPlaces = 5; // invalid

    component.saveCurrency();

    expect(masterDataService.createCurrency).not.toHaveBeenCalled();
    expect(component.message()?.severity).toBe('warn');
  });

  it('should prompt and deactivate an active currency', () => {
    (masterDataService.deactivateCurrency as any).mockReturnValue(of(undefined));

    component.promptDeactivate(mockCurrencies[0]);
    expect(component.pendingAction()?.type).toBe('deactivate');
    expect(component.pendingAction()?.code).toBe('USD');

    component.confirmPendingAction();

    expect(masterDataService.deactivateCurrency).toHaveBeenCalledWith('c1');
    const target = component.currencies().find(c => c.currencyId === 'c1');
    expect(target?.isActive).toBe(false);
    expect(component.pendingAction()).toBeNull();
  });

  it('should prompt and reactivate an inactive currency', () => {
    (masterDataService.activateCurrency as any).mockReturnValue(of(undefined));

    component.promptReactivate(mockCurrencies[2]);
    expect(component.pendingAction()?.type).toBe('reactivate');
    expect(component.pendingAction()?.code).toBe('OLD');

    component.confirmPendingAction();

    expect(masterDataService.activateCurrency).toHaveBeenCalledWith('c3');
    const target = component.currencies().find(c => c.currencyId === 'c3');
    expect(target?.isActive).toBe(true);
    expect(component.pendingAction()).toBeNull();
  });
});
