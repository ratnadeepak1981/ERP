import { Injectable, signal, computed } from '@angular/core';
import { MaterialTableColumn } from '../models/material-data-table.models';

@Injectable()
export class TableStateService<T = any> {
  // Primary State Signals
  readonly rows = signal<T[]>([]);
  readonly columns = signal<MaterialTableColumn<T>[]>([]);
  readonly dirtyRowIndices = signal<Set<number>>(new Set());
  readonly selectedRowIndices = signal<Set<number>>(new Set());

  // Baseline data for cancel / revert
  private baselineRows: T[] = [];

  // Configuration
  readonly rowKeyField = signal<string>('id');
  readonly onRowCalculate = signal<((row: T) => void) | undefined>(undefined);

  readonly rowCount = computed(() => this.rows().length);
  readonly dirtyCount = computed(() => this.dirtyRowIndices().size);

  init(initialRows: T[], cols: MaterialTableColumn<T>[], rowKey = 'id', rowCalcFn?: (row: T) => void): void {
    this.rowKeyField.set(rowKey);
    this.columns.set(cols);
    this.onRowCalculate.set(rowCalcFn);

    const cloned = JSON.parse(JSON.stringify(initialRows || []));
    this.baselineRows = JSON.parse(JSON.stringify(initialRows || []));
    this.rows.set(cloned);
    this.dirtyRowIndices.set(new Set());
    this.selectedRowIndices.set(new Set());

    // Calculate initial totals if configured
    if (rowCalcFn) {
      cloned.forEach((r: T) => rowCalcFn(r));
      this.rows.set([...cloned]);
    }
  }

  setRows(newRows: T[]): void {
    const cloned = JSON.parse(JSON.stringify(newRows || []));
    this.baselineRows = JSON.parse(JSON.stringify(newRows || []));
    this.rows.set(cloned);
    this.dirtyRowIndices.set(new Set());
  }

  updateCellValue(rowIndex: number, colField: string, value: any): void {
    const currentRows = [...this.rows()];
    if (rowIndex < 0 || rowIndex >= currentRows.length) return;

    const row = { ...currentRows[rowIndex], [colField]: value };

    // Run row calculation if configured (e.g. qty * cost = total)
    const calc = this.onRowCalculate();
    if (calc) {
      calc(row);
    }

    currentRows[rowIndex] = row;
    this.rows.set(currentRows);

    // Mark dirty if different from baseline
    const baseline = this.baselineRows[rowIndex];
    const isDirty = !baseline || JSON.stringify(baseline) !== JSON.stringify(row);
    const dirties = new Set(this.dirtyRowIndices());
    if (isDirty) {
      dirties.add(rowIndex);
    } else {
      dirties.delete(rowIndex);
    }
    this.dirtyRowIndices.set(dirties);
  }

  updateRow(rowIndex: number, updatedFields: Partial<T>): void {
    const currentRows = [...this.rows()];
    if (rowIndex < 0 || rowIndex >= currentRows.length) return;

    const row = { ...currentRows[rowIndex], ...updatedFields };

    const calc = this.onRowCalculate();
    if (calc) {
      calc(row);
    }

    currentRows[rowIndex] = row;
    this.rows.set(currentRows);

    const baseline = this.baselineRows[rowIndex];
    const isDirty = !baseline || JSON.stringify(baseline) !== JSON.stringify(row);
    const dirties = new Set(this.dirtyRowIndices());
    if (isDirty) {
      dirties.add(rowIndex);
    } else {
      dirties.delete(rowIndex);
    }
    this.dirtyRowIndices.set(dirties);
  }

  addRow(newRow: T, position: 'end' | 'start' = 'end'): number {
    const current = [...this.rows()];
    const calc = this.onRowCalculate();
    if (calc) {
      calc(newRow);
    }

    let insertIdx: number;
    if (position === 'start') {
      current.unshift(newRow);
      insertIdx = 0;
    } else {
      current.push(newRow);
      insertIdx = current.length - 1;
    }

    this.rows.set(current);

    const dirties = new Set(this.dirtyRowIndices());
    dirties.add(insertIdx);
    this.dirtyRowIndices.set(dirties);

    return insertIdx;
  }

  deleteRow(rowIndex: number): T | null {
    const current = [...this.rows()];
    if (rowIndex < 0 || rowIndex >= current.length) return null;

    const [deleted] = current.splice(rowIndex, 1);
    this.rows.set(current);

    // Rebuild dirty indices after index shift
    const oldDirties = this.dirtyRowIndices();
    const newDirties = new Set<number>();
    oldDirties.forEach(idx => {
      if (idx < rowIndex) newDirties.add(idx);
      else if (idx > rowIndex) newDirties.add(idx - 1);
    });
    this.dirtyRowIndices.set(newDirties);

    return deleted;
  }

  revertAll(): void {
    const reverted = JSON.parse(JSON.stringify(this.baselineRows));
    const calc = this.onRowCalculate();
    if (calc) {
      reverted.forEach((r: T) => calc(r));
    }
    this.rows.set(reverted);
    this.dirtyRowIndices.set(new Set());
  }

  commitAllAsBaseline(): T[] {
    const committed = JSON.parse(JSON.stringify(this.rows()));
    this.baselineRows = JSON.parse(JSON.stringify(committed));
    this.dirtyRowIndices.set(new Set());
    return committed;
  }
}
