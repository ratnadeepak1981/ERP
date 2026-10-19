import { describe, it, expect, beforeEach } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { TenantLayoutComponent } from './tenant-layout.component';

describe('TenantLayoutComponent', () => {
  let component: TenantLayoutComponent;
  let fixture: ComponentFixture<TenantLayoutComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [TenantLayoutComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(TenantLayoutComponent);
    component = fixture.componentInstance;
  });

  it('should create and render tenant name in header', () => {
    fixture.detectChanges();
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('.scope-value')?.textContent).toContain('Alpha Manufacturing Ltd');
  });

  it('should toggle sidebar collapsed state', () => {
    expect(component.sidebarCollapsed()).toBe(false);
    component.toggleSidebar();
    expect(component.sidebarCollapsed()).toBe(true);
    component.toggleSidebar();
    expect(component.sidebarCollapsed()).toBe(false);
  });

  it('should compute operational navigation when on /app workspace', () => {
    fixture.detectChanges();
    const groups = component.navGroups();
    expect(groups.some(g => g.groupName === 'Supply Chain & Operations')).toBe(true);
    expect(component.brandTag()).toBe('Workspace');
  });

  it('should conditionally hide Procurement navigation items when user lacks permission', () => {
    vi.spyOn(component.authService, 'isTenantAdmin').mockReturnValue(false);
    vi.spyOn(component.authService, 'isPlatformAdmin').mockReturnValue(false);
    vi.spyOn(component.authService, 'isPlatformStaff').mockReturnValue(false);
    vi.spyOn(component.authService, 'permissions').mockReturnValue(['INVENTORY.VIEW']);

    fixture.detectChanges();
    const supplyGroup = component.navGroups().find(g => g.groupName === 'Supply Chain & Operations');
    expect(supplyGroup).toBeTruthy();
    expect(supplyGroup?.items.some(i => i.label.includes('Procurement'))).toBe(false);
    expect(supplyGroup?.items.some(i => i.label === 'Purchase Orders')).toBe(false);
  });

  it('should include Procurement Overview and Purchase Orders when permitted', () => {
    fixture.detectChanges();
    const supplyGroup = component.navGroups().find(g => g.groupName === 'Supply Chain & Operations');
    expect(supplyGroup).toBeTruthy();
    expect(supplyGroup?.items.some(i => i.label === 'Procurement Overview')).toBe(true);
    expect(supplyGroup?.items.some(i => i.label === 'Purchase Orders')).toBe(true);
  });

  it('should include Inventory with standardized sub-screens under expandable menu', () => {
    fixture.detectChanges();
    const supplyGroup = component.navGroups().find(g => g.groupName === 'Supply Chain & Operations');
    const inventoryItem = supplyGroup?.items.find(i => i.label === 'Inventory');
    expect(inventoryItem).toBeTruthy();
    expect(inventoryItem?.children).toBeTruthy();
    expect(inventoryItem?.children?.length).toBe(6);

    const childLabels = inventoryItem?.children?.map(c => c.label);
    expect(childLabels).toContain('Overview');
    expect(childLabels).toContain('Stock Receipt');
    expect(childLabels).toContain('Storage & Putaway');
    expect(childLabels).toContain('Stock Transfers');
    expect(childLabels).toContain('Stock Count & Adjustment');
    expect(childLabels).toContain('Stock Issue');

    // Test toggle expansion
    expect(component.isItemExpanded(inventoryItem!)).toBe(true);
    component.toggleItemExpanded(inventoryItem!);
    expect(component.isItemExpanded(inventoryItem!)).toBe(false);
  });

  it('should include Master Data with standardized sub-screens under expandable menu', () => {
    fixture.detectChanges();
    const supplyGroup = component.navGroups().find(g => g.groupName === 'Supply Chain & Operations');
    const masterDataItem = supplyGroup?.items.find(i => i.label === 'Master Data');
    expect(masterDataItem).toBeTruthy();
    expect(masterDataItem?.children).toBeTruthy();
    expect(masterDataItem?.children?.length).toBe(4);

    const childLabels = masterDataItem?.children?.map(c => c.label);
    expect(childLabels).toContain('Overview');
    expect(childLabels).toContain('Products');
    expect(childLabels).toContain('Suppliers');
    expect(childLabels).toContain('Warehouses & Locations');
  });
});
