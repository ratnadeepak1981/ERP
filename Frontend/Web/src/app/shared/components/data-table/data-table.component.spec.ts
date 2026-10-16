import { describe, it, expect, beforeEach, vi } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { AppDataTableComponent } from './data-table.component';
import { TableColumn } from '../../models/table-column.model';

interface TestCustomer {
  name: string;
}

interface TestRow {
  id: string;
  code: string;
  name: string;
  amount: number;
  customer?: TestCustomer;
}

describe('AppDataTableComponent', () => {
  let component: AppDataTableComponent<TestRow>;
  let fixture: ComponentFixture<AppDataTableComponent<TestRow>>;

  const mockColumns: TableColumn<TestRow>[] = [
    { field: 'code', header: 'Code', sortable: true },
    { field: 'name', header: 'Name', sortable: true },
    { field: 'amount', header: 'Amount', type: 'currency', align: 'right' },
    { field: 'customer.name', header: 'Customer', sortable: false }
  ];

  const mockData: TestRow[] = [
    { id: '1', code: 'PRD-001', name: 'Raw Material Steel', amount: 1250.5, customer: { name: 'Acme Steel Corp' } },
    { id: '2', code: 'PRD-002', name: 'Industrial Motor', amount: 8900.0, customer: { name: 'Beta Motors' } }
  ];

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AppDataTableComponent]
    }).compileComponents();

    fixture = TestBed.createComponent(AppDataTableComponent) as unknown as ComponentFixture<AppDataTableComponent<TestRow>>;
    component = fixture.componentInstance;
  });

  it('should initialize and compute global filter fields', () => {
    fixture.componentRef.setInput('columns', mockColumns);
    fixture.componentRef.setInput('data', mockData);
    fixture.detectChanges();

    expect(component.globalFilterFields()).toEqual(['code', 'name', 'amount', 'customer.name']);
  });

  it('should format default currency (USD / en-US) correctly', () => {
    const col: TableColumn<TestRow> = { field: 'amount', header: 'Amount', type: 'currency' };
    const formatted = component.formatValue(col, mockData[0]);
    expect(formatted).toContain('1,250.50');
  });

  it('should format custom currency and locale when configured', () => {
    fixture.componentRef.setInput('currencyCode', 'EUR');
    fixture.componentRef.setInput('currencyLocale', 'de-DE');
    fixture.detectChanges();

    const col: TableColumn<TestRow> = { field: 'amount', header: 'Amount', type: 'currency' };
    const formatted = component.formatValue(col, mockData[0]);
    // German format uses period as thousand separator and comma as decimal separator
    expect(formatted).toMatch(/1\.250,50/);
  });

  it('should resolve nested object paths', () => {
    const value = component.getFieldValue(mockData[0], 'customer.name');
    expect(value).toBe('Acme Steel Corp');
  });

  it('should handle undefined nested object paths safely', () => {
    const rowWithoutCustomer: TestRow = { id: '3', code: 'PRD-003', name: 'Bolts', amount: 50 };
    const value = component.getFieldValue(rowWithoutCustomer, 'customer.name');
    expect(value).toBeUndefined();
  });

  it('should format null/undefined as dash', () => {
    const col: TableColumn<TestRow> = { field: 'name', header: 'Name' };
    const formatted = component.formatValue(col, { id: '3', code: 'PRD-003', name: null as any, amount: 0 });
    expect(formatted).toBe('-');
  });

  it('should emit rowClick on row selection or click', () => {
    let clickedRow: TestRow | null = null;
    component.rowClick.subscribe((row: TestRow) => {
      clickedRow = row;
    });

    component.onRowClick(mockData[0]);
    expect(clickedRow).toEqual(mockData[0]);
  });

  it('should delegate exportCSV to underlying PrimeNG table', () => {
    fixture.componentRef.setInput('columns', mockColumns);
    fixture.componentRef.setInput('data', mockData);
    fixture.detectChanges();

    if (component.dtTable) {
      const spy = vi.spyOn(component.dtTable, 'exportCSV').mockImplementation(() => {});
      component.exportCSV({ selectionOnly: true });
      expect(spy).toHaveBeenCalledWith({ selectionOnly: true });
    } else {
      // If dtTable is not instantiated in test fixture DOM, exportCSV should not throw
      expect(() => component.exportCSV()).not.toThrow();
    }
  });

  it('should filter data when onColumnFilter is called', () => {
    fixture.componentRef.setInput('columns', mockColumns);
    fixture.componentRef.setInput('data', mockData);
    fixture.detectChanges();

    component.onColumnFilter({ field: 'code', value: 'PRD-001', matchMode: 'contains' });
    fixture.detectChanges();

    expect(component.processedData().length).toBe(1);
    expect(component.processedData()[0].code).toBe('PRD-001');

    component.onColumnFilterClear('code');
    fixture.detectChanges();

    expect(component.processedData().length).toBe(2);
  });

  it('should sort data when onColumnSort is called', () => {
    fixture.componentRef.setInput('columns', mockColumns);
    fixture.componentRef.setInput('data', mockData);
    fixture.detectChanges();

    // Sort descending by amount
    component.onColumnSort({ field: 'amount', order: -1 });
    fixture.detectChanges();

    expect(component.processedData()[0].amount).toBe(8900.0);
    expect(component.processedData()[1].amount).toBe(1250.5);
  });

  it('should support row selection and select-all', () => {
    fixture.componentRef.setInput('columns', mockColumns);
    fixture.componentRef.setInput('data', mockData);
    fixture.componentRef.setInput('selectionMode', 'multiple');
    fixture.detectChanges();

    expect(component.selectedCount()).toBe(0);

    component.toggleRowSelection(mockData[0], true);
    expect(component.selectedCount()).toBe(1);
    expect(component.isRowSelected(mockData[0])).toBe(true);
    expect(component.isRowSelected(mockData[1])).toBe(false);

    // Select all page
    component.toggleSelectAllPage(true);
    expect(component.selectedCount()).toBe(2);
    expect(component.isAllPageSelected()).toBe(true);

    // Clear
    component.clearSelection();
    expect(component.selectedCount()).toBe(0);
  });

  it('should support switching between Table, List, and Card views', () => {
    fixture.componentRef.setInput('columns', mockColumns);
    fixture.componentRef.setInput('data', mockData);
    fixture.detectChanges();

    expect(component.currentView()).toBe('table');

    const viewChangeSpy = vi.fn();
    component.viewModeChange.subscribe(viewChangeSpy);

    component.setViewMode('list');
    expect(component.currentView()).toBe('list');
    expect(viewChangeSpy).toHaveBeenCalledWith('list');

    component.setViewMode('card');
    expect(component.currentView()).toBe('card');
    expect(viewChangeSpy).toHaveBeenCalledWith('card');
  });

  it('should preserve search, filter, and selection state when switching views', () => {
    fixture.componentRef.setInput('columns', mockColumns);
    fixture.componentRef.setInput('data', mockData);
    fixture.componentRef.setInput('selectionMode', 'multiple');
    fixture.detectChanges();

    // 1. Perform search
    component.onGlobalSearch('Steel');
    fixture.detectChanges();
    expect(component.processedData().length).toBe(1);
    expect(component.processedData()[0].code).toBe('PRD-001');

    // 2. Select the record
    component.toggleRowSelection(mockData[0], true);
    expect(component.isRowSelected(mockData[0])).toBe(true);

    // 3. Switch to Card view
    component.setViewMode('card');
    fixture.detectChanges();

    // Data, search, and selection must be preserved
    expect(component.processedData().length).toBe(1);
    expect(component.isRowSelected(mockData[0])).toBe(true);

    // 4. Switch to List view
    component.setViewMode('list');
    fixture.detectChanges();

    expect(component.processedData().length).toBe(1);
    expect(component.isRowSelected(mockData[0])).toBe(true);
  });

  it('should format card title and fields accurately for Card View', () => {
    fixture.componentRef.setInput('columns', mockColumns);
    fixture.componentRef.setInput('data', mockData);
    fixture.componentRef.setInput('cardTitleField', 'name');
    fixture.componentRef.setInput('cardSubtitleField', 'code');
    fixture.detectChanges();

    const title = component.getCardTitle(mockData[0]);
    const subtitle = component.getCardSubtitle(mockData[0]);
    const fields = component.getCardFields(mockData[0]);

    expect(title).toBe('Raw Material Steel');
    expect(subtitle).toBe('PRD-001');
    expect(fields.length).toBeGreaterThan(0);
    expect(fields.some(f => f.header === 'Amount')).toBe(true);
  });
});


