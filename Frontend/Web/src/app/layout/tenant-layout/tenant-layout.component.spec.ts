import { describe, it, expect, beforeEach, vi } from 'vitest';
import { TestBed, getTestBed } from '@angular/core/testing';
import {
  BrowserTestingModule,
  platformBrowserTesting
} from '@angular/platform-browser/testing';
import { provideRouter, Router, NavigationEnd } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { TenantLayoutComponent, NavItem } from './tenant-layout.component';

if (typeof (globalThis as any).document === 'undefined') {
  (globalThis as any).document = {
    createElement: () => ({ style: {} }),
    querySelector: () => null,
    querySelectorAll: () => [],
    documentElement: {
      classList: {
        remove: () => {},
        add: () => {},
        contains: () => false
      }
    },
    body: {
      querySelector: () => null,
      querySelectorAll: () => [],
      appendChild: () => {},
      removeChild: () => {}
    },
    location: { href: 'http://localhost' }
  };
  (globalThis as any).window = globalThis;
}

try {
  getTestBed().initTestEnvironment(
    BrowserTestingModule,
    platformBrowserTesting()
  );
} catch {
  // Already initialized in test environment
}

describe('TenantLayoutComponent', () => {
  let component: TenantLayoutComponent;

  beforeEach(async () => {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    });

    component = TestBed.runInInjectionContext(() => new TenantLayoutComponent());
  });

  it('should create and render tenant name in header', () => {
    expect(component.currentTenantName()).toBe('Alpha Manufacturing Ltd');
  });

  it('should toggle sidebar collapsed state', () => {
    expect(component.sidebarCollapsed()).toBe(false);
    component.toggleSidebar();
    expect(component.sidebarCollapsed()).toBe(true);
    component.toggleSidebar();
    expect(component.sidebarCollapsed()).toBe(false);
  });

  it('should compute operational navigation when on /app workspace', () => {
    const groups = component.navGroups();
    expect(groups.some(g => g.groupName === 'Supply Chain & Operations')).toBe(true);
    expect(component.brandTag()).toBe('Workspace');
  });

  // Test Requirement 1 & 2: Procurement is rendered as a collapsible parent with correct child routes
  it('should render Procurement as a collapsible parent with all standardized child routes', () => {
    const supplyGroup = component.navGroups().find(g => g.groupName === 'Supply Chain & Operations');
    const procurementItem = supplyGroup?.items.find(i => i.label === 'Procurement');

    expect(procurementItem).toBeTruthy();
    expect(procurementItem?.children).toBeTruthy();
    expect(procurementItem?.route).toBe('/app/procurement');

    const children = procurementItem?.children || [];
    expect(children.length).toBe(4);

    const routes = children.map(c => c.route);
    expect(routes).toEqual([
      '/app/procurement',
      '/app/procurement/purchase-requisitions',
      '/app/procurement/purchase-orders',
      '/app/procurement/goods-receipts'
    ]);

    const labels = children.map(c => c.label);
    expect(labels).toEqual([
      'Overview',
      'Purchase Requisitions',
      'Purchase Orders',
      'Product Receipts (GRN)'
    ]);

    // Test toggle expansion
    expect(component.isItemExpanded(procurementItem!)).toBe(true);
    component.toggleItemExpanded(procurementItem!);
    expect(component.isItemExpanded(procurementItem!)).toBe(false);
  });

  // Test Requirement 3: Product Receipts (GRN) appears under Procurement for appropriately authorized users
  it('should display Product Receipts (GRN) under Procurement when user has GRN.VIEW or GOODS_RECEIPT.VIEW', () => {
    vi.spyOn(component.authService, 'isTenantAdmin').mockReturnValue(false);
    vi.spyOn(component.authService, 'isPlatformAdmin').mockReturnValue(false);
    vi.spyOn(component.authService, 'isPlatformStaff').mockReturnValue(false);
    vi.spyOn(component.authService, 'permissions').mockReturnValue(['GRN.VIEW']);

    const supplyGroup = component.navGroups().find(g => g.groupName === 'Supply Chain & Operations');
    const procurementItem = supplyGroup?.items.find(i => i.label === 'Procurement');
    expect(procurementItem).toBeTruthy();

    const grnChild = procurementItem?.children?.find(c => c.route === '/app/procurement/goods-receipts');
    expect(grnChild).toBeTruthy();
    expect(grnChild?.label).toBe('Product Receipts (GRN)');
    expect(grnChild?.icon).toBe('pi pi-arrow-circle-down');
  });

  it('should also accept GOODS_RECEIPT.VIEW alias for Product Receipts (GRN)', () => {
    vi.spyOn(component.authService, 'isTenantAdmin').mockReturnValue(false);
    vi.spyOn(component.authService, 'isPlatformAdmin').mockReturnValue(false);
    vi.spyOn(component.authService, 'isPlatformStaff').mockReturnValue(false);
    vi.spyOn(component.authService, 'permissions').mockReturnValue(['GOODS_RECEIPT.VIEW']);

    const supplyGroup = component.navGroups().find(g => g.groupName === 'Supply Chain & Operations');
    const procurementItem = supplyGroup?.items.find(i => i.label === 'Procurement');
    expect(procurementItem).toBeTruthy();

    const grnChild = procurementItem?.children?.find(c => c.route === '/app/procurement/goods-receipts');
    expect(grnChild).toBeTruthy();
  });

  // Test Requirement 4: User with only one valid Procurement child permission can see that child and parent
  it('should display Procurement parent with only Purchase Requisitions when user only has PURCHASE_REQUISITION.VIEW', () => {
    vi.spyOn(component.authService, 'isTenantAdmin').mockReturnValue(false);
    vi.spyOn(component.authService, 'isPlatformAdmin').mockReturnValue(false);
    vi.spyOn(component.authService, 'isPlatformStaff').mockReturnValue(false);
    vi.spyOn(component.authService, 'permissions').mockReturnValue(['PURCHASE_REQUISITION.VIEW']);

    const supplyGroup = component.navGroups().find(g => g.groupName === 'Supply Chain & Operations');
    const procurementItem = supplyGroup?.items.find(i => i.label === 'Procurement');

    expect(procurementItem).toBeTruthy();
    expect(procurementItem?.children?.length).toBe(1);
    expect(procurementItem?.children?.[0].label).toBe('Purchase Requisitions');
    expect(procurementItem?.children?.[0].route).toBe('/app/procurement/purchase-requisitions');
  });

  it('should display Procurement parent with only Product Receipts when user only has GRN.VIEW', () => {
    vi.spyOn(component.authService, 'isTenantAdmin').mockReturnValue(false);
    vi.spyOn(component.authService, 'isPlatformAdmin').mockReturnValue(false);
    vi.spyOn(component.authService, 'isPlatformStaff').mockReturnValue(false);
    vi.spyOn(component.authService, 'permissions').mockReturnValue(['GRN.VIEW']);

    const supplyGroup = component.navGroups().find(g => g.groupName === 'Supply Chain & Operations');
    const procurementItem = supplyGroup?.items.find(i => i.label === 'Procurement');

    expect(procurementItem).toBeTruthy();
    expect(procurementItem?.children?.length).toBe(1);
    expect(procurementItem?.children?.[0].label).toBe('Product Receipts (GRN)');
    expect(procurementItem?.children?.[0].route).toBe('/app/procurement/goods-receipts');
  });

  // Test Requirement 5: Purchase Requisitions visibility remains independently guarded
  it('should omit Purchase Requisitions while retaining other authorized Procurement children', () => {
    vi.spyOn(component.authService, 'isTenantAdmin').mockReturnValue(false);
    vi.spyOn(component.authService, 'isPlatformAdmin').mockReturnValue(false);
    vi.spyOn(component.authService, 'isPlatformStaff').mockReturnValue(false);
    vi.spyOn(component.authService, 'permissions').mockReturnValue(['PURCHASE_ORDER.VIEW', 'GRN.VIEW']);

    const supplyGroup = component.navGroups().find(g => g.groupName === 'Supply Chain & Operations');
    const procurementItem = supplyGroup?.items.find(i => i.label === 'Procurement');

    expect(procurementItem).toBeTruthy();
    const labels = procurementItem?.children?.map(c => c.label) || [];
    expect(labels).not.toContain('Purchase Requisitions');
    expect(labels).toContain('Purchase Orders');
    expect(labels).toContain('Product Receipts (GRN)');
    expect(labels).toContain('Overview');
  });

  // Test Requirement 6: Sales is rendered as a collapsible parent with all existing children
  it('should render Sales as a collapsible parent with all existing children', () => {
    const supplyGroup = component.navGroups().find(g => g.groupName === 'Supply Chain & Operations');
    const salesItem = supplyGroup?.items.find(i => i.label === 'Sales');

    expect(salesItem).toBeTruthy();
    expect(salesItem?.children).toBeTruthy();
    expect(salesItem?.route).toBe('/app/sales');

    const children = salesItem?.children || [];
    expect(children.length).toBe(4);

    const routes = children.map(c => c.route);
    expect(routes).toEqual([
      '/app/sales',
      '/app/sales/orders',
      '/app/sales/dispatches',
      '/app/sales/invoices'
    ]);

    const labels = children.map(c => c.label);
    expect(labels).toEqual([
      'Overview',
      'Sales Orders',
      'Dispatch Notes',
      'Sales Invoices'
    ]);

    // Test toggle expansion
    expect(component.isItemExpanded(salesItem!)).toBe(true);
    component.toggleItemExpanded(salesItem!);
    expect(component.isItemExpanded(salesItem!)).toBe(false);
  });

  // Test Requirement 7: Inventory navigation remains unchanged
  it('should include Inventory with standardized sub-screens under expandable menu unchanged', () => {
    const supplyGroup = component.navGroups().find(g => g.groupName === 'Supply Chain & Operations');
    const inventoryItem = supplyGroup?.items.find(i => i.label === 'Inventory');

    expect(inventoryItem).toBeTruthy();
    expect(inventoryItem?.children).toBeTruthy();
    expect(inventoryItem?.children?.length).toBe(6);

    const childLabels = inventoryItem?.children?.map(c => c.label);
    expect(childLabels).toEqual([
      'Overview',
      'Stock Receipt',
      'Storage & Putaway',
      'Stock Transfers',
      'Stock Count & Adjustment',
      'Stock Issue'
    ]);

    const childRoutes = inventoryItem?.children?.map(c => c.route);
    expect(childRoutes).toEqual([
      '/app/inventory',
      '/app/inventory/stock-receipts',
      '/app/inventory/store-putaway',
      '/app/inventory/transfers',
      '/app/inventory/count-adjust',
      '/app/inventory/issue-stock'
    ]);

    // Test toggle expansion
    expect(component.isItemExpanded(inventoryItem!)).toBe(true);
    component.toggleItemExpanded(inventoryItem!);
    expect(component.isItemExpanded(inventoryItem!)).toBe(false);
  });

  // Test Requirement 8: Procurement and Sales parents expand for active nested routes
  it('should expand Procurement and Sales parents for active nested routes', () => {
    const supplyGroup = component.navGroups().find(g => g.groupName === 'Supply Chain & Operations');
    const procurementItem = supplyGroup?.items.find(i => i.label === 'Procurement');
    const salesItem = supplyGroup?.items.find(i => i.label === 'Sales');

    // Simulate router being on a nested Procurement route
    const router = TestBed.inject(Router);
    const grnUrl = '/app/procurement/goods-receipts/new?purchaseOrderId=123';
    Object.defineProperty(router, 'url', { value: grnUrl, writable: true });
    (router.events as any).next(new NavigationEnd(1, grnUrl, grnUrl));

    expect(component.isParentActive(procurementItem!)).toBe(true);
    expect(component.isItemExpanded(procurementItem!)).toBe(true);

    // Simulate router being on a nested Sales route
    const salesUrl = '/app/sales/orders/create';
    Object.defineProperty(router, 'url', { value: salesUrl, writable: true });
    (router.events as any).next(new NavigationEnd(2, salesUrl, salesUrl));

    expect(component.isParentActive(salesItem!)).toBe(true);
    expect(component.isItemExpanded(salesItem!)).toBe(true);
  });

  // Test Requirement 9: Unauthorized users do not see restricted navigation entries
  it('should completely hide Procurement navigation when user lacks all procurement permissions', () => {
    vi.spyOn(component.authService, 'isTenantAdmin').mockReturnValue(false);
    vi.spyOn(component.authService, 'isPlatformAdmin').mockReturnValue(false);
    vi.spyOn(component.authService, 'isPlatformStaff').mockReturnValue(false);
    vi.spyOn(component.authService, 'permissions').mockReturnValue(['INVENTORY.VIEW']);

    const supplyGroup = component.navGroups().find(g => g.groupName === 'Supply Chain & Operations');
    expect(supplyGroup).toBeTruthy();
    expect(supplyGroup?.items.some(i => i.label === 'Procurement')).toBe(false);
  });

  it('should completely hide Sales navigation when user lacks sales permissions', () => {
    vi.spyOn(component.authService, 'isTenantAdmin').mockReturnValue(false);
    vi.spyOn(component.authService, 'isPlatformAdmin').mockReturnValue(false);
    vi.spyOn(component.authService, 'isPlatformStaff').mockReturnValue(false);
    vi.spyOn(component.authService, 'permissions').mockReturnValue(['INVENTORY.VIEW']);

    const supplyGroup = component.navGroups().find(g => g.groupName === 'Supply Chain & Operations');
    expect(supplyGroup).toBeTruthy();
    expect(supplyGroup?.items.some(i => i.label === 'Sales')).toBe(false);
  });

  it('should hide Storage & Putaway when user lacks INVENTORY.VIEW', () => {
    vi.spyOn(component.authService, 'isTenantAdmin').mockReturnValue(false);
    vi.spyOn(component.authService, 'isPlatformAdmin').mockReturnValue(false);
    vi.spyOn(component.authService, 'isPlatformStaff').mockReturnValue(false);
    vi.spyOn(component.authService, 'permissions').mockReturnValue(['PRODUCT.VIEW']);

    const supplyGroup = component.navGroups().find(g => g.groupName === 'Supply Chain & Operations');
    const inventoryItem = supplyGroup?.items.find(i => i.label === 'Inventory');
    expect(inventoryItem?.children?.some(c => c.label === 'Storage & Putaway')).toBe(false);
  });

  it('should show Storage & Putaway when user has INVENTORY.VIEW', () => {
    vi.spyOn(component.authService, 'isTenantAdmin').mockReturnValue(false);
    vi.spyOn(component.authService, 'isPlatformAdmin').mockReturnValue(false);
    vi.spyOn(component.authService, 'isPlatformStaff').mockReturnValue(false);
    vi.spyOn(component.authService, 'permissions').mockReturnValue(['INVENTORY.VIEW']);

    const supplyGroup = component.navGroups().find(g => g.groupName === 'Supply Chain & Operations');
    const inventoryItem = supplyGroup?.items.find(i => i.label === 'Inventory');
    expect(inventoryItem?.children?.some(c => c.label === 'Storage & Putaway')).toBe(true);
  });

  // Test Requirement 10: No duplicate menu entries are produced
  it('should ensure no duplicate menu entries or duplicate routes exist in navigation', () => {
    const supplyGroup = component.navGroups().find(g => g.groupName === 'Supply Chain & Operations');
    const parentLabels = supplyGroup?.items.map(i => i.label) || [];
    const uniqueParentLabels = new Set(parentLabels);
    expect(parentLabels.length).toBe(uniqueParentLabels.size);

    // Check each parent's children for uniqueness
    for (const item of supplyGroup?.items || []) {
      if (item.children) {
        const childRoutes = item.children.map(c => c.route);
        const uniqueChildRoutes = new Set(childRoutes);
        expect(childRoutes.length).toBe(uniqueChildRoutes.size);

        const childLabels = item.children.map(c => c.label);
        const uniqueChildLabels = new Set(childLabels);
        expect(childLabels.length).toBe(uniqueChildLabels.size);
      }
    }
  });

  it('should include Master Data with standardized sub-screens under expandable menu', () => {
    const supplyGroup = component.navGroups().find(g => g.groupName === 'Supply Chain & Operations');
    const masterDataItem = supplyGroup?.items.find(i => i.label === 'Master Data');
    expect(masterDataItem).toBeTruthy();
    expect(masterDataItem?.children).toBeTruthy();
    expect(masterDataItem?.children?.length).toBe(7);

    const childLabels = masterDataItem?.children?.map(c => c.label);
    expect(childLabels).toContain('Overview');
    expect(childLabels).toContain('Products');
    expect(childLabels).toContain('Customers');
    expect(childLabels).toContain('Suppliers');
    expect(childLabels).toContain('Warehouses & Locations');
  });
});
