import { Injectable, signal } from '@angular/core';
import { CellCoord } from '../models/material-data-table.models';

@Injectable()
export class TableEditingService {
  readonly activeCell = signal<CellCoord | null>(null);

  // Original value tracker for Escape cancel
  private originalValue: any = undefined;

  isEditing(rowIndex: number, colField: string): boolean {
    const current = this.activeCell();
    return current !== null && current.rowIndex === rowIndex && current.colField === colField;
  }

  startEditing(rowIndex: number, colField: string, currentValue: any): void {
    this.originalValue = currentValue;
    this.activeCell.set({ rowIndex, colField });
  }

  commitEditing(): void {
    this.activeCell.set(null);
    this.originalValue = undefined;
  }

  cancelEditing(): { rowIndex: number; colField: string; originalValue: any } | null {
    const current = this.activeCell();
    if (!current) return null;

    const result = {
      rowIndex: current.rowIndex,
      colField: current.colField,
      originalValue: this.originalValue
    };

    this.activeCell.set(null);
    this.originalValue = undefined;
    return result;
  }

  stopEditing(): void {
    this.activeCell.set(null);
    this.originalValue = undefined;
  }
}
