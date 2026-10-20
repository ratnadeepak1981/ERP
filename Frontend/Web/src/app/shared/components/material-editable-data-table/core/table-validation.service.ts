import { Injectable, signal } from '@angular/core';
import { MaterialTableColumn, TableValidationErrors } from '../models/material-data-table.models';

@Injectable()
export class TableValidationService {
  readonly errors = signal<TableValidationErrors>({});

  validateCell<T>(rowIndex: number, col: MaterialTableColumn<T>, value: any, row: T): string | null {
    let errorMsg: string | null = null;

    // 1. Check custom validators first
    if (col.validators && col.validators.length > 0) {
      for (const validator of col.validators) {
        errorMsg = validator(value, row);
        if (errorMsg) break;
      }
    } else {
      // 2. Default validation: required checks
      const isRequired = col.required === true;
      const isEmpty = value === null || value === undefined || (typeof value === 'string' && value.trim() === '');

      if (isRequired && isEmpty) {
        errorMsg = `${col.header} is required`;
      } else if (!isEmpty) {
        // Numeric checks
        if (col.type === 'number' || col.type === 'currency') {
          const num = Number(value);
          if (isNaN(num)) {
            errorMsg = 'Must be a valid number';
          } else if (col.min !== undefined && num < col.min) {
            errorMsg = `Must be at least ${col.min}`;
          } else if (col.max !== undefined && num > col.max) {
            errorMsg = `Cannot exceed ${col.max}`;
          }
        }
      }
    }

    // Update errors signal
    const currentErrors = { ...this.errors() };
    if (errorMsg) {
      if (!currentErrors[rowIndex]) {
        currentErrors[rowIndex] = {};
      }
      currentErrors[rowIndex][col.field] = errorMsg;
    } else {
      if (currentErrors[rowIndex]) {
        delete currentErrors[rowIndex][col.field];
        if (Object.keys(currentErrors[rowIndex]).length === 0) {
          delete currentErrors[rowIndex];
        }
      }
    }
    this.errors.set(currentErrors);

    return errorMsg;
  }

  getCellError(rowIndex: number, colField: string): string | null {
    return this.errors()[rowIndex]?.[colField] || null;
  }

  isCellValid<T>(rowIndex: number, col: MaterialTableColumn<T>, value: any, row: T): boolean {
    const err = this.validateCell(rowIndex, col, value, row);
    return !err;
  }

  validateAll<T>(rows: T[], cols: MaterialTableColumn<T>[]): boolean {
    let isValid = true;
    for (let r = 0; r < rows.length; r++) {
      const row = rows[r];
      for (const col of cols) {
        if (col.type === 'actions' || col.type === 'calculated') continue;
        const val = (row as any)[col.field];
        const err = this.validateCell(r, col, val, row);
        if (err) {
          isValid = false;
        }
      }
    }
    return isValid;
  }

  clearErrors(): void {
    this.errors.set({});
  }

  clearRowErrors(rowIndex: number): void {
    const current = { ...this.errors() };
    delete current[rowIndex];
    this.errors.set(current);
  }
}
