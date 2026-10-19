import { describe, it, expect, beforeEach, vi } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { By } from '@angular/platform-browser';
import { AppModuleOverviewComponent } from './module-overview.component';
import { ModuleOverviewConfig, DashboardCardConfig } from '../../models/module-overview-config.model';
import { AuthService } from '../../../core/auth/auth.service';

describe('AppModuleOverviewComponent', () => {
  let component: AppModuleOverviewComponent;
  let fixture: ComponentFixture<AppModuleOverviewComponent>;
  let router: Router;
  let authService: AuthService;

  const mockConfig: ModuleOverviewConfig = {
    moduleName: 'Test Module Overview',
    moduleDescription: 'Module description and permitted quick actions',
    breadcrumbs: [{ label: 'Workspace', routerLink: '/app/dashboard' }, { label: 'Test Module' }],
    dashboardCards: [
      {
        id: 'card-1',
        title: 'Draft POs',
        icon: 'pi pi-file-edit',
        value: 12,
        colorVariant: 'blue',
        route: '/app/procurement/purchase-orders',
        queryParams: { status: 'draft' }
      },
      {
        id: 'card-2',
        title: 'Pending Approval',
        icon: 'pi pi-clock',
        value: 5,
        colorVariant: 'amber',
        route: '/app/procurement/purchase-orders',
        queryParams: { status: 'submitted' },
        requiredPermissions: ['PURCHASE_ORDER.APPROVE']
      },
      {
        id: 'card-3',
        title: 'Approved POs',
        icon: 'pi pi-check-circle',
        value: 28,
        colorVariant: 'green',
        route: '/app/procurement/purchase-orders',
        queryParams: { status: 'approved' }
      },
      {
        id: 'card-4',
        title: 'Order Value',
        icon: 'pi pi-wallet',
        value: 'LKR 2.4M',
        colorVariant: 'slate',
        route: '/app/procurement/purchase-orders'
      }
    ],
    processFlow: {
      enabled: true,
      position: 'belowCards',
      stages: [
        { id: 'pr', code: 'PR', label: 'Requisition', route: '/app/procurement/purchase-orders' },
        { id: 'po', code: 'PO', label: 'Purchase order', route: '/app/procurement/purchase-orders' }
      ]
    },
    recentTransactions: {
      title: 'Recent transactions',
      limit: 10,
      showSearch: true,
      showFilters: true,
      showViewAll: true,
      viewAllRoute: '/app/procurement/purchase-orders',
      viewAllLabel: 'View all Purchase Orders',
      createActionLabel: 'Create PO',
      createActionRoute: '/app/procurement/purchase-orders',
      createActionQueryParams: { action: 'create' },
      createActionPermission: 'PURCHASE_ORDER.CREATE'
    }
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AppModuleOverviewComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(AppModuleOverviewComponent);
    component = fixture.componentInstance;
    router = TestBed.inject(Router);
    authService = TestBed.inject(AuthService);
    vi.spyOn(router, 'navigate').mockResolvedValue(true);
    component.config = mockConfig;
  });

  it('should create and render module title and description', () => {
    fixture.detectChanges();
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Test Module Overview');
    expect(compiled.textContent).toContain('Module description and permitted quick actions');
  });

  it('should dynamically derive eligible cards and render the configured count', () => {
    vi.spyOn(authService, 'isTenantAdmin').mockReturnValue(true);
    fixture.detectChanges();

    expect(component.eligibleCards().length).toBe(4);
    const cardElements = fixture.nativeElement.querySelectorAll('.kpi-card');
    expect(cardElements.length).toBe(4);
    expect(cardElements[0].textContent).toContain('Draft POs');
    expect(cardElements[0].textContent).toContain('12');
    expect(cardElements[1].textContent).toContain('Pending Approval');
  });

  it('should exclude cards when user lacks required permission', () => {
    vi.spyOn(authService, 'isTenantAdmin').mockReturnValue(false);
    vi.spyOn(authService, 'isPlatformAdmin').mockReturnValue(false);
    vi.spyOn(authService, 'isPlatformStaff').mockReturnValue(false);
    vi.spyOn(authService, 'permissions').mockReturnValue(['PURCHASE_ORDER.VIEW']);

    fixture.detectChanges();

    // card-2 requires 'PURCHASE_ORDER.APPROVE', so it should be excluded
    expect(component.eligibleCards().length).toBe(3);
    const cardElements = fixture.nativeElement.querySelectorAll('.kpi-card');
    expect(cardElements.length).toBe(3);
    expect(fixture.nativeElement.textContent).not.toContain('Pending Approval');
  });

  it('should exclude cards with enabled = false', () => {
    vi.spyOn(authService, 'isTenantAdmin').mockReturnValue(true);
    component.config = {
      ...mockConfig,
      dashboardCards: [
        mockConfig.dashboardCards[0],
        { ...mockConfig.dashboardCards[1], enabled: false }
      ]
    };
    fixture.detectChanges();

    expect(component.eligibleCards().length).toBe(1);
  });

  it('should show guidance state when zero cards are eligible', () => {
    vi.spyOn(authService, 'isTenantAdmin').mockReturnValue(false);
    vi.spyOn(authService, 'isPlatformAdmin').mockReturnValue(false);
    vi.spyOn(authService, 'isPlatformStaff').mockReturnValue(false);
    vi.spyOn(authService, 'permissions').mockReturnValue([]);

    component.config = {
      ...mockConfig,
      dashboardCards: [
        { ...mockConfig.dashboardCards[0], enabled: false }
      ]
    };
    fixture.detectChanges();

    expect(component.eligibleCards().length).toBe(0);
    const guidance = fixture.nativeElement.querySelector('[data-testid="no-cards-guidance"]');
    expect(guidance).toBeTruthy();
    expect(guidance.textContent).toContain('No dashboard metrics are currently enabled or eligible');
  });

  it('should apply color variants classes correctly without breaking layout', () => {
    vi.spyOn(authService, 'isTenantAdmin').mockReturnValue(true);
    fixture.detectChanges();

    const cards = fixture.debugElement.queryAll(By.css('.kpi-card'));
    expect(cards[0].nativeElement.classList.contains('card-blue')).toBe(true);
    expect(cards[1].nativeElement.classList.contains('card-amber')).toBe(true);
    expect(cards[2].nativeElement.classList.contains('card-green')).toBe(true);
    expect(cards[3].nativeElement.classList.contains('card-slate')).toBe(true);
  });

  it('should navigate on card click with route and query parameters', () => {
    vi.spyOn(authService, 'isTenantAdmin').mockReturnValue(true);
    fixture.detectChanges();

    const spy = vi.spyOn(component.cardClick, 'emit');
    const firstCard = fixture.debugElement.query(By.css('[data-testid="dashboard-card-card-1"]'));
    firstCard.nativeElement.click();

    expect(spy).toHaveBeenCalledWith(mockConfig.dashboardCards[0]);
    expect(router.navigate).toHaveBeenCalledWith(['/app/procurement/purchase-orders'], {
      queryParams: { status: 'draft' }
    });
  });

  it('should support configurable process flow placements', () => {
    // 1. belowCards (default)
    component.config = {
      ...mockConfig,
      processFlow: { ...mockConfig.processFlow!, position: 'belowCards' }
    };
    fixture.detectChanges();
    expect(component.processFlowPosition()).toBe('belowCards');

    // 2. aboveCards
    component.config = {
      ...mockConfig,
      processFlow: { ...mockConfig.processFlow!, position: 'aboveCards' }
    };
    fixture.detectChanges();
    expect(component.processFlowPosition()).toBe('aboveCards');

    // 3. belowRecentTransactions
    component.config = {
      ...mockConfig,
      processFlow: { ...mockConfig.processFlow!, position: 'belowRecentTransactions' }
    };
    fixture.detectChanges();
    expect(component.processFlowPosition()).toBe('belowRecentTransactions');
  });

  it('should respect configured recent transactions limit', () => {
    const rawData = Array.from({ length: 25 }, (_, i) => ({ id: `row-${i}` }));
    component.recentData = rawData;

    // Default limit = 10
    fixture.detectChanges();
    expect(component.displayedRecentData().length).toBe(10);

    // Custom limit = 5
    component.config = {
      ...mockConfig,
      recentTransactions: { ...mockConfig.recentTransactions, limit: 5 }
    };
    fixture.detectChanges();
    expect(component.displayedRecentData().length).toBe(5);
  });

  it('should render 403 AccessDeniedComponent when isAccessDenied is true', () => {
    component.isAccessDenied = true;
    fixture.detectChanges();

    const denied = fixture.nativeElement.querySelector('app-access-denied');
    expect(denied).toBeTruthy();
  });
});
