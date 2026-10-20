import { Injectable } from '@angular/core';
import { CellCoord, MaterialTableColumn } from '../models/material-data-table.models';
import { TableEditingService } from './table-editing.service';
import { TableValidationService } from './table-validation.service';
import { TableStateService } from './table-state.service';

@Injectable()
export class TableNavigationService {
  constructor(
    private editingService: TableEditingService,
    private validationService: TableValidationService,
    private stateService: TableStateService
  ) {}

  isColumnEditable<T>(col: MaterialTableColumn<T>, row: T): boolean {
    if (col.type === 'actions' || col.type === 'calculated') return false;
    if (typeof col.editable === 'function') {
      return col.editable(row);
    }
    return col.editable !== false;
  }

  getEditableColumnsForRow<T>(cols: MaterialTableColumn<T>[], row: T): MaterialTableColumn<T>[] {
    return cols.filter(c => this.isColumnEditable(c, row));
  }

  handleTabNavigation<T>(
    event: KeyboardEvent,
    currentCoord: CellCoord,
    rows: T[],
    cols: MaterialTableColumn<T>[]
  ): boolean {
    // 1. Always prevent default browser Tab behavior to avoid double-advancement
    event.preventDefault();
    event.stopPropagation();

    const { rowIndex, colField } = currentCoord;
    const currentRow = rows[rowIndex];
    const currentCol = cols.find(c => c.field === colField);

    // 2. Validation Guard: Check if current cell is valid before allowing navigation
    if (currentCol) {
      const currentVal = (currentRow as any)[colField];
      const isCurrentValid = this.validationService.isCellValid(rowIndex, currentCol, currentVal, currentRow);
      if (!isCurrentValid) {
        // Keep focus in the invalid editor
        return false;
      }
    }

    const editableCols = this.getEditableColumnsForRow(cols, currentRow);
    const colIdx = editableCols.findIndex(c => c.field === colField);

    if (event.shiftKey) {
      // Shift+Tab: Navigate backward
      if (colIdx > 0) {
        // Previous column in same row
        const prevCol = editableCols[colIdx - 1];
        this.editingService.startEditing(rowIndex, prevCol.field, (currentRow as any)[prevCol.field]);
        return true;
      } else if (rowIndex > 0) {
        // Move to last editable column of previous row
        const prevRow = rows[rowIndex - 1];
        const prevRowEditableCols = this.getEditableColumnsForRow(cols, prevRow);
        if (prevRowEditableCols.length > 0) {
          const targetCol = prevRowEditableCols[prevRowEditableCols.length - 1];
          this.editingService.startEditing(rowIndex - 1, targetCol.field, (prevRow as any)[targetCol.field]);
          return true;
        }
      } else {
        // At very beginning of table
        this.editingService.commitEditing();
        return true;
      }
    } else {
      // Tab: Navigate forward
      if (colIdx < editableCols.length - 1) {
        // Next column in same row
        const nextCol = editableCols[colIdx + 1];
        this.editingService.startEditing(rowIndex, nextCol.field, (currentRow as any)[nextCol.field]);
        return true;
      } else if (rowIndex < rows.length - 1) {
        // Wrap to first editable column of next row
        const nextRow = rows[rowIndex + 1];
        const nextRowEditableCols = this.getEditableColumnsForRow(cols, nextRow);
        if (nextRowEditableCols.length > 0) {
          const targetCol = nextRowEditableCols[0];
          this.editingService.startEditing(rowIndex + 1, targetCol.field, (nextRow as any)[targetCol.field]);
          return true;
        }
      } else {
        // At very end of table
        this.editingService.commitEditing();
        return true;
      }
    }

    return false;
  }
}
