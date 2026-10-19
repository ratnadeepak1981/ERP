import { ComponentFixture, TestBed } from '@angular/core/testing';
import { AppEditableDataTableComponent } from './editable-data-table.component';
import { DataTableColumn } from '../../models/data-table-config.model';

interface TestItem {
  lineId: string;
  productCode: string;
  quantity: number;
  unitPrice: number;
  discountPercent: number;
  taxPercent: number;
  lineTotal: number;
}

describe('AppEditableDataTableComponent', () => {
  let component: AppEditableDataTableComponent<TestItem>;
  let fixture: ComponentFixture<AppEditableDataTableComponent<TestItem>>;

  const mockColumns: DataTableColumn<TestItem>[] = [
    { field: 'lineId', header: 'Line #', editable: false, width: '100px' },
    { field: 'productCode', header: 'Product Code', editable: true, editType: 'text' },
    {
      field: 'quantity',
      header: 'Qty',
      editable: true,
      editType: 'number',
      validators: [(val: any) => (Number(val) > 0 ? null : 'Quantity must be greater than 0')]
    },
    { field: 'unitPrice', header: 'Unit Price', editable: true, editType: 'currency' },
    { field: 'lineTotal', header: 'Total', editable: false, type: 'currency' }
  ];

  const mockData: TestItem[] = [
    { lineId: 'LINE-01', productCode: 'STEEL-01', quantity: 10, unitPrice: 50, discountPercent: 0, taxPercent: 0, lineTotal: 500 },
    { lineId: 'LINE-02', productCode: 'ALUM-02', quantity: 5, unitPrice: 120, discountPercent: 10, taxPercent: 0, lineTotal: 540 }
  ];

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AppEditableDataTableComponent]
    }).compileComponents();

    fixture = (TestBed.createComponent(AppEditableDataTableComponent) as unknown) as ComponentFixture<AppEditableDataTableComponent<TestItem>>;
    component = fixture.componentInstance;
    component.columns = mockColumns;
    component.data = mockData;
    fixture.detectChanges();
  });

  it('should create the editable data table', () => {
    expect(component).toBeTruthy();
    expect(component.workingData().length).toBe(2);
    expect(component.dirtyCount()).toBe(0);
  });

  it('should support click-to-edit and not be in edit mode initially', () => {
    const row = component.workingData()[0];
    const qtyCol = mockColumns.find(c => c.field === 'quantity')!;
    const lineIdCol = mockColumns.find(c => c.field === 'lineId')!;

    // Initially, no cell is editing
    expect(component.isCellEditing(row, qtyCol)).toBe(false);

    // Clicking non-editable cell does not start editing
    component.startEditing(row, lineIdCol);
    expect(component.isCellEditing(row, lineIdCol)).toBe(false);

    // Clicking editable cell activates editing
    component.startEditing(row, qtyCol);
    expect(component.isCellEditing(row, qtyCol)).toBe(true);

    // Calling stopEditing exits edit mode
    component.stopEditing();
    expect(component.isCellEditing(row, qtyCol)).toBe(false);
  });

  it('should revert cell value on Escape key during click-to-edit', () => {
    const row = component.workingData()[0];
    const qtyCol = mockColumns.find(c => c.field === 'quantity')!;

    component.startEditing(row, qtyCol);
    row.quantity = 999;

    const escapeEvent = new KeyboardEvent('keydown', { key: 'Escape' });
    component.onCellKeyDown(escapeEvent, row, qtyCol, 0);

    expect(component.isCellEditing(row, qtyCol)).toBe(false);
    expect(row.quantity).toBe(10); // reverted to original 10
  });

  it('should mark row as dirty when editable cell value changes', () => {
    const row = component.workingData()[0];
    const qtyCol = mockColumns.find(c => c.field === 'quantity')!;

    component.onCellValueChange(row, qtyCol, 20);

    expect(component.isRowDirty(row)).toBe(true);
    expect(component.dirtyCount()).toBe(1);
    expect(row.quantity).toBe(20);
    // Auto-recalculation
    expect(row.lineTotal).toBe(1000); // 20 * 50
  });

  it('should flag field validation error when invalid value is provided', () => {
    const row = component.workingData()[0];
    const qtyCol = mockColumns.find(c => c.field === 'quantity')!;

    component.onCellValueChange(row, qtyCol, -5);

    expect(component.getFieldError(row, 'quantity')).toBe('Quantity must be greater than 0');
    expect(component.hasErrors()).toBe(true);
  });

  it('should save changes and clear dirty state', () => {
    const saveSpy = vi.fn();
    component.save.subscribe(saveSpy);

    const row = component.workingData()[0];
    const qtyCol = mockColumns.find(c => c.field === 'quantity')!;
    component.onCellValueChange(row, qtyCol, 15);

    expect(component.dirtyCount()).toBe(1);

    component.saveChanges();

    expect(component.dirtyCount()).toBe(0);
    expect(saveSpy).toHaveBeenCalledWith(expect.objectContaining({ dirtyCount: 1 }));
    expect(component.baselineData()[0].quantity).toBe(15);
  });

  it('should discard changes on cancelAllChanges and restore baseline', () => {
    const cancelSpy = vi.fn();
    component.cancelEdits.subscribe(cancelSpy);

    const row = component.workingData()[0];
    const qtyCol = mockColumns.find(c => c.field === 'quantity')!;
    component.onCellValueChange(row, qtyCol, 99);

    expect(component.workingData()[0].quantity).toBe(99);

    component.cancelAllChanges();

    expect(component.workingData()[0].quantity).toBe(10);
    expect(component.dirtyCount()).toBe(0);
    expect(cancelSpy).toHaveBeenCalled();
  });

  it('should add a blank row to working data when addNewRow is called without defaultNewRow', () => {
    component.defaultNewRow = undefined;
    component.addNewRow();

    expect(component.workingData().length).toBe(3);
    expect(component.dirtyCount()).toBe(1);

    const blankRow = component.workingData()[0];
    expect(blankRow.productCode).toBe('');
    expect(blankRow.quantity).toBe(0);
    expect(blankRow.unitPrice).toBe(0);
  });

  it('should add a blank row when Tab is pressed on the last editable column of the last row', () => {
    const lastRowIndex = component.workingData().length - 1;
    const lastRow = component.workingData()[lastRowIndex];
    // unitPrice is the last editable column in mockColumns
    const lastEditableCol = mockColumns.find(c => c.field === 'unitPrice')!;

    const initialCount = component.workingData().length;
    const tabEvent = new KeyboardEvent('keydown', { key: 'Tab' });
    component.onCellKeyDown(tabEvent, lastRow, lastEditableCol, lastRowIndex);

    // A new blank row should have been appended!
    expect(component.workingData().length).toBe(initialCount + 1);
    const newlyAdded = component.workingData()[component.workingData().length - 1];
    expect(newlyAdded.productCode).toBe('');
    expect(component.isRowDirty(newlyAdded)).toBe(true);
  });

  it('should prompt and delete row when confirmed', () => {
    const rowToDelete = component.workingData()[1];
    component.promptDelete(rowToDelete);

    expect(component.deleteDialogVisible()).toBe(true);
    expect(component.pendingDeleteRow()?.lineId).toBe('LINE-02');

    component.confirmDelete();

    expect(component.workingData().length).toBe(1);
    expect(component.deleteDialogVisible()).toBe(false);
  });

  it('should compute subtotal accurately across working items', () => {
    expect(component.computedSubtotal()).toBe(1040);

    const row = component.workingData()[0];
    const priceCol = mockColumns.find(c => c.field === 'unitPrice')!;
    component.onCellValueChange(row, priceCol, 100);

    expect(component.computedSubtotal()).toBe(1540);
  });

  it('should delegate calculations to onRowCalculate callback when provided by parent', () => {
    const calcSpy = vi.fn((r: any) => {
      r.lineTotal = (Number(r.quantity) || 0) * (Number(r.unitPrice) || 0) * 2;
    });
    component.onRowCalculate = calcSpy;

    const row = component.workingData()[0];
    const qtyCol = mockColumns.find(c => c.field === 'quantity')!;
    component.onCellValueChange(row, qtyCol, 20);

    expect(calcSpy).toHaveBeenCalledWith(row);
    expect(row.lineTotal).toBe(20 * 50 * 2); // 2000
  });

  it('should insert a blank row in the middle of table right below target row', () => {
    // mockData has 2 items: LINE-01 at index 0, LINE-02 at index 1
    expect(component.workingData().length).toBe(2);

    // Insert row below index 0 (so new row is placed at index 1 between LINE-01 and LINE-02)
    component.insertRowAt(0);

    expect(component.workingData().length).toBe(3);
    expect(component.workingData()[0].lineId).toBe('LINE-01');
    expect(component.workingData()[1].productCode).toBe(''); // Middle inserted blank row
    expect(component.workingData()[2].lineId).toBe('LINE-02');
  });

  it('should navigate rows vertically using ArrowUp and ArrowDown', () => {
    const row0 = component.workingData()[0];
    const row1 = component.workingData()[1];
    const textCol = mockColumns.find(c => c.field === 'productCode')!;

    // Start editing row 0
    component.startEditing(row0, textCol);
    expect(component.isCellEditing(row0, textCol)).toBe(true);

    // ArrowDown moves edit mode to row 1
    const downEvent = new KeyboardEvent('keydown', { key: 'ArrowDown' });
    component.onCellKeyDown(downEvent, row0, textCol, 0);
    expect(component.isCellEditing(row1, textCol)).toBe(true);

    // ArrowUp moves edit mode back to row 0
    const upEvent = new KeyboardEvent('keydown', { key: 'ArrowUp' });
    component.onCellKeyDown(upEvent, row1, textCol, 1);
    expect(component.isCellEditing(row0, textCol)).toBe(true);
  });
});
