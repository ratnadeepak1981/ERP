import { describe, it, expect, beforeEach } from 'vitest';
import { TableValidationService } from './table-validation.service';
import { MaterialTableColumn } from '../models/material-data-table.models';

interface TestRow {
  productCode: string;
  quantity: number | null;
  unitCost: number | null;
}

describe('TableValidationService', () => {
  let service: TableValidationService;

  const productCol: MaterialTableColumn<TestRow> = {
    field: 'productCode',
    header: 'Product',
    required: true
  };

  const qtyCol: MaterialTableColumn<TestRow> = {
    field: 'quantity',
    header: 'Quantity',
    type: 'number',
    required: true,
    min: 1
  };

  const costCol: MaterialTableColumn<TestRow> = {
    field: 'unitCost',
    header: 'Unit Cost',
    type: 'currency',
    min: 0
  };

  beforeEach(() => {
    service = new TableValidationService();
  });

  it('should validate required fields and return error message', () => {
    const row: TestRow = { productCode: '', quantity: 1, unitCost: 10 };
    const err = service.validateCell(0, productCol, '', row);
    expect(err).toBe('Product is required');
    expect(service.getCellError(0, 'productCode')).toBe('Product is required');
  });

  it('should clear error message when cell value becomes valid', () => {
    const row: TestRow = { productCode: '', quantity: 1, unitCost: 10 };
    service.validateCell(0, productCol, '', row);
    expect(service.getCellError(0, 'productCode')).toBe('Product is required');

    service.validateCell(0, productCol, 'XYZ-P001', { ...row, productCode: 'XYZ-P001' });
    expect(service.getCellError(0, 'productCode')).toBeNull();
  });

  it('should validate min numeric bounds', () => {
    const row: TestRow = { productCode: 'P1', quantity: 0, unitCost: 10 };
    const err = service.validateCell(0, qtyCol, 0, row);
    expect(err).toBe('Must be at least 1');
  });

  it('should validate optional fields without errors when empty', () => {
    const row: TestRow = { productCode: 'P1', quantity: 2, unitCost: null };
    const err = service.validateCell(0, costCol, null, row);
    expect(err).toBeNull();
  });

  it('should validate all rows and return overall validity', () => {
    const rows: TestRow[] = [
      { productCode: 'P1', quantity: 2, unitCost: 10 },
      { productCode: '', quantity: 0, unitCost: 5 } // Invalid productCode and quantity
    ];
    const cols = [productCol, qtyCol, costCol];

    const isValid = service.validateAll(rows, cols);
    expect(isValid).toBe(false);
    expect(service.getCellError(1, 'productCode')).toBe('Product is required');
    expect(service.getCellError(1, 'quantity')).toBe('Must be at least 1');
  });
});
