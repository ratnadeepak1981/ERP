import { describe, it, expect, beforeEach } from 'vitest';
import { TableStateService } from './table-state.service';
import { MaterialTableColumn } from '../models/material-data-table.models';

interface TestRow {
  id: string;
  productCode: string;
  quantity: number;
  unitCost: number;
  lineTotal: number;
}

describe('TableStateService', () => {
  let service: TableStateService<TestRow>;

  const initialRows: TestRow[] = [
    { id: '1', productCode: 'P1', quantity: 2, unitCost: 10, lineTotal: 20 },
    { id: '2', productCode: 'P2', quantity: 5, unitCost: 20, lineTotal: 100 }
  ];

  const columns: MaterialTableColumn<TestRow>[] = [
    { field: 'productCode', header: 'Product' },
    { field: 'quantity', header: 'Quantity', type: 'number' },
    { field: 'unitCost', header: 'Unit Cost', type: 'currency' },
    { field: 'lineTotal', header: 'Total', type: 'calculated' }
  ];

  const calcFn = (r: TestRow) => {
    r.lineTotal = (r.quantity || 0) * (r.unitCost || 0);
  };

  beforeEach(() => {
    service = new TableStateService<TestRow>();
    service.init(initialRows, columns, 'id', calcFn);
  });

  it('should initialize rows and calculate totals', () => {
    expect(service.rows().length).toBe(2);
    expect(service.rowCount()).toBe(2);
    expect(service.dirtyCount()).toBe(0);
    expect(service.rows()[0].lineTotal).toBe(20);
  });

  it('should update cell value and mark row dirty', () => {
    service.updateCellValue(0, 'quantity', 4);
    expect(service.rows()[0].quantity).toBe(4);
    expect(service.rows()[0].lineTotal).toBe(40); // Recalculated
    expect(service.dirtyRowIndices().has(0)).toBe(true);
    expect(service.dirtyCount()).toBe(1);
  });

  it('should update multiple row fields', () => {
    service.updateRow(1, { quantity: 10, unitCost: 15 });
    expect(service.rows()[1].quantity).toBe(10);
    expect(service.rows()[1].unitCost).toBe(15);
    expect(service.rows()[1].lineTotal).toBe(150);
    expect(service.dirtyRowIndices().has(1)).toBe(true);
  });

  it('should add a row at the end and mark it dirty', () => {
    const newRow: TestRow = { id: '3', productCode: 'P3', quantity: 1, unitCost: 50, lineTotal: 0 };
    const idx = service.addRow(newRow, 'end');
    expect(idx).toBe(2);
    expect(service.rows().length).toBe(3);
    expect(service.rows()[2].lineTotal).toBe(50);
    expect(service.dirtyRowIndices().has(2)).toBe(true);
  });

  it('should delete a row and adjust dirty indices', () => {
    service.updateCellValue(1, 'quantity', 8);
    expect(service.dirtyRowIndices().has(1)).toBe(true);

    const deleted = service.deleteRow(0);
    expect(deleted?.id).toBe('1');
    expect(service.rows().length).toBe(1);
    expect(service.rows()[0].id).toBe('2');
    // Dirty index shifted from 1 to 0
    expect(service.dirtyRowIndices().has(0)).toBe(true);
  });

  it('should revert all unsaved changes', () => {
    service.updateCellValue(0, 'quantity', 99);
    expect(service.rows()[0].quantity).toBe(99);

    service.revertAll();
    expect(service.rows()[0].quantity).toBe(2);
    expect(service.rows()[0].lineTotal).toBe(20);
    expect(service.dirtyCount()).toBe(0);
  });

  it('should commit all as new baseline', () => {
    service.updateCellValue(0, 'quantity', 12);
    expect(service.dirtyCount()).toBe(1);

    const committed = service.commitAllAsBaseline();
    expect(committed[0].quantity).toBe(12);
    expect(service.dirtyCount()).toBe(0);

    // Revert now reverts to 12
    service.updateCellValue(0, 'quantity', 99);
    service.revertAll();
    expect(service.rows()[0].quantity).toBe(12);
  });
});
