import { describe, it, expect, beforeEach } from 'vitest';
import { TableNavigationService } from './table-navigation.service';
import { TableEditingService } from './table-editing.service';
import { TableValidationService } from './table-validation.service';
import { TableStateService } from './table-state.service';
import { MaterialTableColumn } from '../models/material-data-table.models';

interface TestRow {
  productCode: string;
  locationCode: string;
  quantity: number;
  unitOfMeasure: string;
  unitCost: number;
  lineTotal: number;
}

describe('TableNavigationService', () => {
  let navService: TableNavigationService;
  let editingService: TableEditingService;
  let validationService: TableValidationService;
  let stateService: TableStateService<TestRow>;

  const createTabEvent = (shift = false): KeyboardEvent =>
    ({
      key: 'Tab',
      shiftKey: shift,
      preventDefault: () => {},
      stopPropagation: () => {}
    }) as unknown as KeyboardEvent;

  const columns: MaterialTableColumn<TestRow>[] = [
    { field: 'productCode', header: 'Product', required: true },
    { field: 'locationCode', header: 'Location' },
    { field: 'quantity', header: 'Quantity', type: 'number', required: true, min: 1 },
    { field: 'unitOfMeasure', header: 'UOM', editable: false }, // Readonly, should skip
    { field: 'unitCost', header: 'Unit Cost', type: 'currency' },
    { field: 'lineTotal', header: 'Total', type: 'calculated' } // Calculated, should skip
  ];

  const rows: TestRow[] = [
    { productCode: 'P1', locationCode: 'L1', quantity: 2, unitOfMeasure: 'EA', unitCost: 10, lineTotal: 20 },
    { productCode: 'P2', locationCode: 'L2', quantity: 5, unitOfMeasure: 'EA', unitCost: 20, lineTotal: 100 }
  ];

  beforeEach(() => {
    editingService = new TableEditingService();
    validationService = new TableValidationService();
    stateService = new TableStateService<TestRow>();
    stateService.init(rows, columns, 'productCode');

    navService = new TableNavigationService(editingService, validationService, stateService);
  });

  it('should identify editable columns and filter out readonly/calculated columns', () => {
    const editable = navService.getEditableColumnsForRow(columns, rows[0]);
    expect(editable.map(c => c.field)).toEqual(['productCode', 'locationCode', 'quantity', 'unitCost']);
  });

  it('should advance to next editable column on Tab', () => {
    const event = createTabEvent(false);
    const handled = navService.handleTabNavigation(event, { rowIndex: 0, colField: 'productCode' }, rows, columns);

    expect(handled).toBe(true);
    expect(editingService.activeCell()).toEqual({ rowIndex: 0, colField: 'locationCode' });
  });

  it('should skip non-editable UOM column when tabbing from quantity to unitCost', () => {
    const event = createTabEvent(false);
    const handled = navService.handleTabNavigation(event, { rowIndex: 0, colField: 'quantity' }, rows, columns);

    expect(handled).toBe(true);
    // Skips unitOfMeasure, targets unitCost
    expect(editingService.activeCell()).toEqual({ rowIndex: 0, colField: 'unitCost' });
  });

  it('should wrap to first editable column of next row when tabbing from last column', () => {
    const event = createTabEvent(false);
    const handled = navService.handleTabNavigation(event, { rowIndex: 0, colField: 'unitCost' }, rows, columns);

    expect(handled).toBe(true);
    // Wraps to row 1, productCode
    expect(editingService.activeCell()).toEqual({ rowIndex: 1, colField: 'productCode' });
  });

  it('should move backward to previous editable column on Shift+Tab', () => {
    const event = createTabEvent(true);
    const handled = navService.handleTabNavigation(event, { rowIndex: 0, colField: 'unitCost' }, rows, columns);

    expect(handled).toBe(true);
    // Skips unitOfMeasure backwards, targets quantity
    expect(editingService.activeCell()).toEqual({ rowIndex: 0, colField: 'quantity' });
  });

  it('should wrap to last editable column of previous row on Shift+Tab from first column', () => {
    const event = createTabEvent(true);
    const handled = navService.handleTabNavigation(event, { rowIndex: 1, colField: 'productCode' }, rows, columns);

    expect(handled).toBe(true);
    expect(editingService.activeCell()).toEqual({ rowIndex: 0, colField: 'unitCost' });
  });

  it('should block navigation and keep focus in current cell if validation fails', () => {
    const invalidRows: TestRow[] = [
      { productCode: '', locationCode: 'L1', quantity: 2, unitOfMeasure: 'EA', unitCost: 10, lineTotal: 20 }
    ];

    const event = createTabEvent(false);
    const handled = navService.handleTabNavigation(event, { rowIndex: 0, colField: 'productCode' }, invalidRows, columns);

    expect(handled).toBe(false);
    expect(validationService.getCellError(0, 'productCode')).toBe('Product is required');
  });
});
