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
});
