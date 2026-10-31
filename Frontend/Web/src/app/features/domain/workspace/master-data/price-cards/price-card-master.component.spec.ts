import { describe, it, expect, beforeEach, vi } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { of } from 'rxjs';
import { PriceCardMasterComponent } from './price-card-master.component';
import { MasterDataService } from '../../../../../core/services/master-data.service';
import { AuthService } from '../../../../../core/auth/auth.service';
import {
  PriceCardItem,
  PriceCardType
} from '../../../../../core/models/price-card.models';
import { PagedResult } from '../../../../../core/models/scope.models';

describe('PriceCardMasterComponent', () => {
  let component: PriceCardMasterComponent;
  let fixture: ComponentFixture<PriceCardMasterComponent>;
  let masterDataService: MasterDataService;
  let authService: AuthService;
  let router: Router;

  const mockCards: PriceCardItem[] = [
    {
      priceCardId: 'pc-001',
      tenantId: 'tenant-1',
      priceCardCode: 'PC-PUR-001',
      priceCardName: 'Raw Materials 2026',
      priceCardType: PriceCardType.Purchase,
      currency: 'USD',
      effectiveFrom: '2026-01-01T00:00:00Z',
      effectiveTo: '2026-12-31T23:59:59Z',
      description: 'Standard supplier purchase agreements',
      isActive: true,
      lineCount: 3,
      lines: []
    },
    {
      priceCardId: 'pc-002',
      tenantId: 'tenant-1',
      priceCardCode: 'PC-SAL-001',
      priceCardName: 'Domestic Wholesale Sales',
      priceCardType: PriceCardType.Sales,
      currency: 'USD',
      effectiveFrom: '2026-03-01T00:00:00Z',
      effectiveTo: null,
      description: 'Domestic wholesale price tiers',
      isActive: false,
      lineCount: 1,
      lines: []
    }
  ];

  const mockPagedResult: PagedResult<PriceCardItem> = {
    items: mockCards,
    totalCount: 2,
    page: 1,
    pageSize: 10,
    hasMore: false
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [PriceCardMasterComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        MasterDataService,
        AuthService
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(PriceCardMasterComponent);
    component = fixture.componentInstance;
    masterDataService = TestBed.inject(MasterDataService);
    authService = TestBed.inject(AuthService);
    router = TestBed.inject(Router);
  });

  it('should create and load price card records on init', () => {
    const spy = vi
      .spyOn(masterDataService, 'getPriceCards')
      .mockReturnValue(of(mockPagedResult));

    fixture.detectChanges();

    expect(component).toBeTruthy();
    expect(spy).toHaveBeenCalled();
    expect(component.priceCards().length).toBe(2);
    expect(component.totalCount()).toBe(2);
  });

  it('should filter by search term', () => {
    const spy = vi
      .spyOn(masterDataService, 'getPriceCards')
      .mockReturnValue(of(mockPagedResult));

    fixture.detectChanges();

    component.onSearchChange('Raw');
    expect(component.searchTerm()).toBe('Raw');
    expect(spy).toHaveBeenLastCalledWith(
      expect.objectContaining({ search: 'Raw', page: 1 })
    );
  });

  it('should filter by price card type', () => {
    const spy = vi
      .spyOn(masterDataService, 'getPriceCards')
      .mockReturnValue(of(mockPagedResult));

    fixture.detectChanges();

    component.onTypeFilterChange('1');
    expect(component.typeFilter()).toBe('1');
    expect(spy).toHaveBeenLastCalledWith(
      expect.objectContaining({ type: PriceCardType.Purchase, page: 1 })
    );

    component.onTypeFilterChange('2');
    expect(component.typeFilter()).toBe('2');
    expect(spy).toHaveBeenLastCalledWith(
      expect.objectContaining({ type: PriceCardType.Sales, page: 1 })
    );
  });

  it('should filter by active/inactive status', () => {
    const spy = vi
      .spyOn(masterDataService, 'getPriceCards')
      .mockReturnValue(of(mockPagedResult));

    fixture.detectChanges();

    component.onStatusFilterChange('ACTIVE');
    expect(component.statusFilter()).toBe('ACTIVE');
    expect(spy).toHaveBeenLastCalledWith(
      expect.objectContaining({ isActive: true, page: 1 })
    );

    component.onStatusFilterChange('INACTIVE');
    expect(component.statusFilter()).toBe('INACTIVE');
    expect(spy).toHaveBeenLastCalledWith(
      expect.objectContaining({ isActive: false, page: 1 })
    );
  });

  it('should prompt and execute deactivation with action dialog', () => {
    vi.spyOn(masterDataService, 'getPriceCards').mockReturnValue(of(mockPagedResult));
    const deactSpy = vi
      .spyOn(masterDataService, 'deactivatePriceCard')
      .mockReturnValue(of(void 0));

    fixture.detectChanges();

    const activeCard = mockCards[0];
    component.promptDeactivate(activeCard);

    expect(component.isActionDialogVisible()).toBe(true);
    expect(component.pendingAction()?.type).toBe('deactivate');

    component.executePendingAction();

    expect(deactSpy).toHaveBeenCalledWith(activeCard.priceCardId);
    expect(component.isActionDialogVisible()).toBe(false);
    expect(component.pendingAction()).toBeNull();
  });

  it('should prompt and execute reactivation with action dialog', () => {
    vi.spyOn(masterDataService, 'getPriceCards').mockReturnValue(of(mockPagedResult));
    const reactSpy = vi
      .spyOn(masterDataService, 'reactivatePriceCard')
      .mockReturnValue(of(void 0));

    fixture.detectChanges();

    const inactiveCard = mockCards[1];
    component.promptReactivate(inactiveCard);

    expect(component.isActionDialogVisible()).toBe(true);
    expect(component.pendingAction()?.type).toBe('reactivate');

    component.executePendingAction();

    expect(reactSpy).toHaveBeenCalledWith(inactiveCard.priceCardId);
    expect(component.isActionDialogVisible()).toBe(false);
  });

  it('should handle sorting and paging via query change', () => {
    const spy = vi
      .spyOn(masterDataService, 'getPriceCards')
      .mockReturnValue(of(mockPagedResult));

    fixture.detectChanges();

    component.onQueryChange({
      first: 10,
      rows: 10,
      sortField: 'name',
      sortOrder: -1
    });

    expect(component.sortBy()).toBe('name');
    expect(component.sortDirection()).toBe('desc');
    expect(component.page()).toBe(2);
    expect(spy).toHaveBeenLastCalledWith(
      expect.objectContaining({ sortBy: 'name', sortDirection: 'desc', page: 2 })
    );
  });

  it('should respect permissions for creation and management', () => {
    vi.spyOn(masterDataService, 'getPriceCards').mockReturnValue(of(mockPagedResult));
    vi.spyOn(authService, 'isTenantAdmin').mockReturnValue(false);
    vi.spyOn(authService, 'isPlatformAdmin').mockReturnValue(false);

    vi.spyOn(authService, 'hasPermission').mockImplementation(perm => {
      return perm === 'PRICE_CARD.VIEW';
    });

    fixture.detectChanges();

    expect(component.canCreate()).toBe(false);
    expect(component.canEdit()).toBe(false);
    expect(component.canManage()).toBe(false);
  });

  it('should navigate to create page when navigateToCreate is called', () => {
    const navSpy = vi.spyOn(router, 'navigate').mockResolvedValue(true);
    component.navigateToCreate();
    expect(navSpy).toHaveBeenCalledWith(['/app/master-data/price-cards/new']);
  });
});
