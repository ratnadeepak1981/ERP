import '@angular/compiler';
import { describe, it, expect, beforeEach } from 'vitest';
import { AppMaterialEditableDataTableComponent } from './app-material-editable-data-table.component';
import { MaterialTableColumn } from '../models/material-data-table.models';
import { TableStateService } from './table-state.service';
import { TableEditingService } from './table-editing.service';
import { TableValidationService } from './table-validation.service';
import { TableNavigationService } from './table-navigation.service';

interface SampleItem {
  id: string;
  productCode: string;
  quantity: number;
  unitCost: number;
  lineTotal: number;
}

describe('AppMaterialEditableDataTableComponent', () => {
  let component: AppMaterialEditableDataTableComponent<SampleItem>;
  let stateService: TableStateService<SampleItem>;
  let editingService: TableEditingService;
  let validationService: TableValidationService;
  let navigationService: TableNavigationService;

  const sampleCols: MaterialTableColumn<SampleItem>[] = [
    { field: 'productCode', header: 'Product', required: true },
    { field: 'quantity', header: 'Quantity', type: 'number', required: true, min: 1 },
    { field: 'unitCost', header: 'Unit Cost', type: 'currency', required: true, min: 0 },
    { field: 'lineTotal', header: 'Line Total', type: 'calculated' },
    { field: 'actions', header: '', type: 'actions' }
  ];

  const sampleData: SampleItem[] = [
    { id: '1', productCode: 'XYZ-001', quantity: 2, unitCost: 100, lineTotal: 200 },
    { id: '2', productCode: 'CBL-002', quantity: 5, unitCost: 20, lineTotal: 100 }
  ];

  beforeEach(() => {
    editingService = new TableEditingService();
    validationService = new TableValidationService();
    stateService = new TableStateService<SampleItem>();
    navigationService = new TableNavigationService(editingService, validationService, stateService);

    component = new AppMaterialEditableDataTableComponent<SampleItem>(
      stateService,
      editingService,
      validationService,
      navigationService
    );

    component.data = [...sampleData];
    component.columns = sampleCols;
    component.rowKey = 'id';
    component.onRowCalculate = (r: SampleItem) => {
      r.lineTotal = (Number(r.quantity) || 0) * (Number(r.unitCost) || 0);
    };

    component.ngOnInit();
  });

  it('should initialize and render table with rows', () => {
    expect(component.stateService.rows().length).toBe(2);
    expect(component.stateService.dirtyCount()).toBe(0);
    expect(component.getTotalQuantity()).toBe(7);
    expect(component.getTotalValuation()).toBe(300);
  });

  it('should enter edit mode when clicking an editable cell', () => {
    component.onCellClick({ rowIndex: 0, column: sampleCols[0] });
    expect(component.editingService.isEditing(0, 'productCode')).toBe(true);
  });

  it('should update cell value, recalculate line total, and mark row dirty', () => {
    component.onCellValueChange(0, sampleCols[1], 10);
    expect(component.stateService.rows()[0].quantity).toBe(10);
    expect(component.stateService.rows()[0].lineTotal).toBe(1000); // 10 * 100
    expect(component.stateService.dirtyCount()).toBe(1);
    expect(component.getTotalQuantity()).toBe(15);
    expect(component.getTotalValuation()).toBe(1100);
  });

  it('should add a new row when onAddRow is triggered', () => {
    component.defaultNewRow = () => ({
      id: 'new-3',
      productCode: '',
      quantity: 1,
      unitCost: 0,
      lineTotal: 0
    });

    component.onAddRow();
    expect(component.stateService.rows().length).toBe(3);
    // Auto-focused first editable cell of new row
    expect(component.editingService.isEditing(2, 'productCode')).toBe(true);
  });

  it('should delete a row when onDeleteRow is triggered', () => {
    component.onDeleteRow(0);
    expect(component.stateService.rows().length).toBe(1);
    expect(component.stateService.rows()[0].id).toBe('2');
  });

  it('should perform in-memory save and emit save event when all rows are valid', () => {
    let savedEvent: any = null;
    component.save.subscribe(e => (savedEvent = e));

    component.onCellValueChange(0, sampleCols[1], 4);
    component.onSave();

    expect(savedEvent).toBeTruthy();
    expect(savedEvent.dirtyCount).toBe(1);
    expect(savedEvent.updatedData[0].quantity).toBe(4);
    expect(component.stateService.dirtyCount()).toBe(0);
    expect(component.feedbackBanner()?.type).toBe('success');
  });

  it('should block save and show error feedback if validation fails', () => {
    let savedEvent: any = null;
    component.save.subscribe(e => (savedEvent = e));

    // Clear required productCode
    component.onCellValueChange(0, sampleCols[0], '');
    component.onSave();

    expect(savedEvent).toBeNull();
    expect(component.feedbackBanner()?.type).toBe('error');
    expect(component.validationService.getCellError(0, 'productCode')).toBe('Product is required');
  });

  it('should revert all edits to baseline when onReset is triggered', () => {
    component.onCellValueChange(0, sampleCols[1], 99);
    expect(component.stateService.rows()[0].quantity).toBe(99);

    component.onReset();
    expect(component.stateService.rows()[0].quantity).toBe(2);
    expect(component.stateService.dirtyCount()).toBe(0);
    expect(component.feedbackBanner()?.type).toBe('info');
  });
});
