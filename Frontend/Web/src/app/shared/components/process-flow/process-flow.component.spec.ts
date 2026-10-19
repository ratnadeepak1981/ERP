import { describe, it, expect, beforeEach, vi } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { AppProcessFlowComponent } from './process-flow.component';
import { ProcessStageConfig } from '../../models/module-overview-config.model';
import { AuthService } from '../../../core/auth/auth.service';

describe('AppProcessFlowComponent', () => {
  let component: AppProcessFlowComponent;
  let fixture: ComponentFixture<AppProcessFlowComponent>;
  let router: Router;
  let authService: AuthService;

  const mockStages: ProcessStageConfig[] = [
    {
      id: 'pr',
      code: 'PR',
      label: 'Requisition',
      description: 'Purchase requisitions and demands',
      route: '/app/procurement/purchase-orders',
      queryParams: { tab: 'requisitions' },
      requiredPermissions: ['PURCHASE_REQUISITION.VIEW']
    },
    {
      id: 'po',
      code: 'PO',
      label: 'Purchase order',
      route: '/app/procurement/purchase-orders',
      requiredPermissions: ['PURCHASE_ORDER.VIEW']
    },
    {
      id: 'grn',
      code: 'GRN',
      label: 'Receipt',
      status: 'disabled'
    },
    {
      id: 'stock',
      code: 'Stock',
      label: 'Posting',
      route: '/app/inventory',
      requiredPermissions: ['INVENTORY.VIEW']
    }
  ];

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AppProcessFlowComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(AppProcessFlowComponent);
    component = fixture.componentInstance;
    router = TestBed.inject(Router);
    authService = TestBed.inject(AuthService);
    vi.spyOn(router, 'navigate').mockResolvedValue(true);
  });

  it('should create the component', () => {
    expect(component).toBeTruthy();
  });

  it('should render all configured stages when user has permissions', () => {
    vi.spyOn(authService, 'isTenantAdmin').mockReturnValue(true);
    component.stages = mockStages;
    fixture.detectChanges();

    const stageElements = fixture.nativeElement.querySelectorAll('.stage-item');
    expect(stageElements.length).toBe(4);
    expect(stageElements[0].textContent).toContain('PR');
    expect(stageElements[0].textContent).toContain('Requisition');
    expect(stageElements[1].textContent).toContain('PO');
    expect(stageElements[1].textContent).toContain('Purchase order');
  });

  it('should exclude stages when user lacks required permission', () => {
    vi.spyOn(authService, 'isTenantAdmin').mockReturnValue(false);
    vi.spyOn(authService, 'isPlatformAdmin').mockReturnValue(false);
    vi.spyOn(authService, 'isPlatformStaff').mockReturnValue(false);
    vi.spyOn(authService, 'permissions').mockReturnValue(['PURCHASE_ORDER.VIEW']);

    component.stages = mockStages;
    fixture.detectChanges();

    const stageElements = fixture.nativeElement.querySelectorAll('.stage-item');
    // 'pr' (needs PURCHASE_REQUISITION.VIEW) and 'stock' (needs INVENTORY.VIEW) should be excluded
    // 'po' (has PURCHASE_ORDER.VIEW) and 'grn' (no permissions required) should be included
    expect(stageElements.length).toBe(2);
    expect(fixture.nativeElement.textContent).toContain('PO');
    expect(fixture.nativeElement.textContent).toContain('GRN');
    expect(fixture.nativeElement.textContent).not.toContain('Requisition');
  });

  it('should exclude stages when enabled is explicitly false', () => {
    vi.spyOn(authService, 'isTenantAdmin').mockReturnValue(true);
    component.stages = [
      ...mockStages.slice(0, 2),
      { ...mockStages[2], enabled: false }
    ];
    fixture.detectChanges();

    const stageElements = fixture.nativeElement.querySelectorAll('.stage-item');
    expect(stageElements.length).toBe(2);
    expect(fixture.nativeElement.textContent).not.toContain('GRN');
  });

  it('should navigate and emit stageClick when an interactive stage is clicked', () => {
    vi.spyOn(authService, 'isTenantAdmin').mockReturnValue(true);
    component.stages = mockStages;
    fixture.detectChanges();

    const spy = vi.spyOn(component.stageClick, 'emit');
    const poStageElement = fixture.nativeElement.querySelector('[data-testid="process-stage-po"]');
    poStageElement.click();

    expect(spy).toHaveBeenCalledWith(mockStages[1]);
    expect(router.navigate).toHaveBeenCalledWith(['/app/procurement/purchase-orders'], {
      queryParams: undefined
    });
  });

  it('should not navigate when a disabled stage is clicked', () => {
    vi.spyOn(authService, 'isTenantAdmin').mockReturnValue(true);
    component.stages = mockStages;
    fixture.detectChanges();

    const grnStageElement = fixture.nativeElement.querySelector('[data-testid="process-stage-grn"]');
    grnStageElement.click();

    expect(router.navigate).not.toHaveBeenCalled();
  });

  // --- Enhanced Process Flow Tests ---

  it('should render compact icon-and-label layout with completed, current, and upcoming visual states', () => {
    vi.spyOn(authService, 'isTenantAdmin').mockReturnValue(true);
    component.stages = [
      { id: 'step-1', label: 'Requisition', status: 'completed' },
      { id: 'step-2', label: 'Purchase Order', status: 'current', badge: 12 },
      { id: 'step-3', label: 'Goods Receipt', status: 'upcoming' },
      { id: 'step-4', label: 'Stock Posting', status: 'upcoming' }
    ];
    fixture.detectChanges();

    const items = fixture.nativeElement.querySelectorAll('.stage-item');
    expect(items.length).toBe(4);

    // Completed state
    expect(items[0].classList.contains('status-completed')).toBe(true);
    expect(items[0].querySelector('.status-text-completed')?.textContent?.trim()).toBe('Completed');
    expect(items[0].querySelector('.stage-svg')).toBeTruthy(); // Checkmark SVG rendered

    // Current state
    expect(items[1].classList.contains('status-current')).toBe(true);
    expect(items[1].querySelector('.status-text-current')?.textContent?.trim()).toBe('Current');
    expect(items[1].querySelector('.stage-metric-badge')?.textContent?.trim()).toBe('12');

    // Upcoming states
    expect(items[2].classList.contains('status-upcoming')).toBe(true);
    expect(items[2].querySelector('.status-text-upcoming')?.textContent?.trim()).toBe('Upcoming');
    expect(items[3].classList.contains('status-upcoming')).toBe(true);
  });

  it('should correctly render optional stages without falsely marking bypassed stages as completed', () => {
    vi.spyOn(authService, 'isTenantAdmin').mockReturnValue(true);
    component.stages = [
      {
        id: 'pr',
        label: 'Requisition',
        stageType: 'optional',
        isOptional: true,
        status: 'upcoming' // Bypassed or not created yet
      },
      {
        id: 'po',
        label: 'Purchase Order',
        status: 'current' // Direct PO in progress
      }
    ];
    fixture.detectChanges();

    const prItem = fixture.nativeElement.querySelector('[data-testid="process-stage-pr"]');
    const optionalBadge = prItem.querySelector('.tag-optional');
    expect(optionalBadge).toBeTruthy();
    expect(optionalBadge.textContent?.trim()).toBe('Optional');

    // Crucial check: PR must NOT be marked completed when bypassed
    expect(prItem.classList.contains('status-completed')).toBe(false);
    expect(prItem.querySelector('.status-text-completed')).toBeFalsy();
    expect(prItem.querySelector('.stage-status-text')?.textContent?.trim()).toBe('Optional');
  });

  it('should render alternative entry paths with an OR connector divider', () => {
    vi.spyOn(authService, 'isTenantAdmin').mockReturnValue(true);
    component.stages = [
      {
        id: 'pr',
        label: 'Requisition',
        pathGroup: 'entry',
        isOptional: true
      },
      {
        id: 'direct-po',
        label: 'Direct PO',
        pathGroup: 'entry',
        isAlternative: true,
        route: '/app/procurement/purchase-orders',
        queryParams: { action: 'create' }
      },
      {
        id: 'po',
        label: 'Purchase Order',
        status: 'current'
      }
    ];
    fixture.detectChanges();

    // Check OR connector between alternative entry paths
    const orConnector = fixture.nativeElement.querySelector('.or-connector');
    expect(orConnector).toBeTruthy();
    expect(orConnector.querySelector('.or-badge')?.textContent?.trim()).toBe('OR');

    // Check Alternative tag
    const directPoItem = fixture.nativeElement.querySelector('[data-testid="process-stage-direct-po"]');
    expect(directPoItem.querySelector('.tag-alternative')?.textContent?.trim()).toBe('Alternative');
  });

  it('should render converging path indicator when alternative paths meet a common stage', () => {
    vi.spyOn(authService, 'isTenantAdmin').mockReturnValue(true);
    component.stages = [
      {
        id: 'direct-po',
        label: 'Direct PO',
        isAlternative: true,
        convergesTo: 'po'
      },
      {
        id: 'po',
        label: 'Purchase Order',
        status: 'current'
      }
    ];
    fixture.detectChanges();

    const convergeConnector = fixture.nativeElement.querySelector('.converge-connector');
    expect(convergeConnector).toBeTruthy();
    expect(convergeConnector.textContent).toContain('Both paths converge');
  });

  it('should render conditional stages with a "When applicable" badge and explanation tooltip', () => {
    vi.spyOn(authService, 'isTenantAdmin').mockReturnValue(true);
    component.stages = [
      {
        id: 'po',
        label: 'Purchase Order',
        status: 'completed'
      },
      {
        id: 'stock',
        label: 'Stock Posting',
        stageType: 'conditional',
        isConditional: true,
        conditionalExplanation: 'Applies when items affect warehouse stock inventory',
        status: 'upcoming'
      }
    ];
    fixture.detectChanges();

    const stockItem = fixture.nativeElement.querySelector('[data-testid="process-stage-stock"]');
    const conditionalBadge = stockItem.querySelector('.tag-conditional');
    expect(conditionalBadge).toBeTruthy();
    expect(conditionalBadge.textContent?.trim()).toBe('When applicable');
    expect(component.getStageTooltip(component.stages[1])).toContain('Applies when items affect warehouse stock inventory');
  });

  it('should not allow planned or unavailable stages to be operationally clickable', () => {
    vi.spyOn(authService, 'isTenantAdmin').mockReturnValue(true);
    component.stages = [
      {
        id: 'grn',
        label: 'Goods Receipt',
        status: 'planned',
        planned: true,
        route: '/app/procurement/grn' // Destination defined but stage is planned/unimplemented
      }
    ];
    fixture.detectChanges();

    const grnItem = fixture.nativeElement.querySelector('[data-testid="process-stage-grn"]');
    expect(grnItem.classList.contains('clickable')).toBe(false);
    expect(grnItem.getAttribute('tabindex')).toBe('-1');

    // Clicking should NOT navigate
    grnItem.click();
    expect(router.navigate).not.toHaveBeenCalled();

    const plannedBadge = grnItem.querySelector('.tag-planned');
    expect(plannedBadge).toBeTruthy();
    expect(plannedBadge.textContent?.trim()).toBe('Planned');
  });

  it('should support blocked stage with warning appearance and blocked reason', () => {
    vi.spyOn(authService, 'isTenantAdmin').mockReturnValue(true);
    component.stages = [
      {
        id: 'approval',
        label: 'Director Approval',
        status: 'blocked',
        blockedReason: 'Pending budget re-allocation approval'
      }
    ];
    fixture.detectChanges();

    const blockedItem = fixture.nativeElement.querySelector('[data-testid="process-stage-approval"]');
    expect(blockedItem.classList.contains('status-blocked')).toBe(true);
    expect(blockedItem.querySelector('.status-text-blocked')?.textContent?.trim()).toBe('Blocked');
    expect(component.getStageTooltip(component.stages[0])).toBe('Pending budget re-allocation approval');
  });

  it('should filter stages by activePathId when path-aware routing is configured', () => {
    vi.spyOn(authService, 'isTenantAdmin').mockReturnValue(true);
    component.stages = [
      { id: 'pr', label: 'Requisition', pathId: 'path-requisition' },
      { id: 'direct-po', label: 'Direct PO', pathId: 'path-direct' },
      { id: 'po', label: 'Purchase Order' } // Common to all
    ];
    component.activePathId = 'path-direct';
    fixture.detectChanges();

    const items = fixture.nativeElement.querySelectorAll('.stage-item');
    expect(items.length).toBe(2);
    expect(fixture.nativeElement.textContent).toContain('Direct PO');
    expect(fixture.nativeElement.textContent).toContain('Purchase Order');
    expect(fixture.nativeElement.textContent).not.toContain('Requisition');
  });
});
