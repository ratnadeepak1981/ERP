import { describe, it, expect, beforeEach } from 'vitest';
import { TableEditingService } from './table-editing.service';

describe('TableEditingService', () => {
  let service: TableEditingService;

  beforeEach(() => {
    service = new TableEditingService();
  });

  it('should initialize with no active cell', () => {
    expect(service.activeCell()).toBeNull();
    expect(service.isEditing(0, 'productCode')).toBe(false);
  });

  it('should start editing a cell and remember original value', () => {
    service.startEditing(1, 'quantity', 50);
    expect(service.activeCell()).toEqual({ rowIndex: 1, colField: 'quantity' });
    expect(service.isEditing(1, 'quantity')).toBe(true);
    expect(service.isEditing(0, 'quantity')).toBe(false);
  });

  it('should commit editing and clear active cell', () => {
    service.startEditing(0, 'unitCost', 25);
    service.commitEditing();
    expect(service.activeCell()).toBeNull();
    expect(service.isEditing(0, 'unitCost')).toBe(false);
  });

  it('should cancel editing and return original value for reversion', () => {
    service.startEditing(2, 'productCode', 'OLD-SKU');
    const result = service.cancelEditing();
    expect(result).toEqual({
      rowIndex: 2,
      colField: 'productCode',
      originalValue: 'OLD-SKU'
    });
    expect(service.activeCell()).toBeNull();
  });
});
