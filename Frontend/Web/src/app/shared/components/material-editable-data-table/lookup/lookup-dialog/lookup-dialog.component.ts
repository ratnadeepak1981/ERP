import { Component, Inject, signal, computed, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatDialogRef, MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatTableModule } from '@angular/material/table';
import { MatIconModule } from '@angular/material/icon';
import { MaterialLookupConfig } from '../../models/material-data-table.models';

export interface LookupDialogData<TItem = any> {
  config: MaterialLookupConfig<TItem>;
  initialQuery?: string;
}

@Component({
  selector: 'app-mat-lookup-dialog',
  standalone: true,
  imports: [CommonModule, FormsModule, MatDialogModule, MatButtonModule, MatTableModule, MatIconModule],
  template: `
    <div class="fluent-lookup-dialog">
      <div class="dialog-header">
        <h2 mat-dialog-title class="dialog-title">
          <mat-icon class="title-icon">search</mat-icon>
          {{ data.config.title || 'Select Record' }}
        </h2>
        <button mat-icon-button (click)="onClose()" aria-label="Close dialog" class="close-btn">
          <mat-icon>close</mat-icon>
        </button>
      </div>

      <mat-dialog-content class="dialog-content">
        <!-- Search Filter Bar -->
        <div class="search-bar">
          <mat-icon class="search-icon">search</mat-icon>
          <input
            type="text"
            class="search-input"
            [placeholder]="data.config.searchPlaceholder || 'Search by code, name, description...'"
            [ngModel]="searchQuery()"
            (ngModelChange)="searchQuery.set($event)"
            autofocus
          />
          @if (searchQuery()) {
            <button class="clear-search-btn" (click)="searchQuery.set('')">
              <mat-icon>clear</mat-icon>
            </button>
          }
        </div>

        <!-- Results Table -->
        <div class="results-table-container">
          <table mat-table [dataSource]="filteredItems()" class="mat-elevation-z0 lookup-mat-table">
            @for (col of data.config.columns; track col.field) {
              <ng-container [matColumnDef]="col.field">
                <th mat-header-cell *matHeaderCellDef [style.width]="col.width || 'auto'">
                  {{ col.header }}
                </th>
                <td mat-cell *matCellDef="let item" [style.width]="col.width || 'auto'">
                  {{ getColValue(item, col.field) }}
                </td>
              </ng-container>
            }

            <tr mat-header-row *matHeaderRowDef="displayedColumns"></tr>
            <tr
              mat-row
              *matRowDef="let row; columns: displayedColumns"
              class="lookup-data-row"
              [class.is-selected]="selectedItem() === row"
              (click)="selectedItem.set(row)"
              (dblclick)="onSelect(row)"
            ></tr>
          </table>

          @if (filteredItems().length === 0) {
            <div class="no-results-box">
              <mat-icon>info</mat-icon>
              <span>No matching records found for "{{ searchQuery() }}"</span>
            </div>
          }
        </div>
      </mat-dialog-content>

      <mat-dialog-actions align="end" class="dialog-actions">
        <button mat-button (click)="onClose()">Cancel</button>
        <button
          mat-flat-button
          color="primary"
          [disabled]="!selectedItem()"
          (click)="onSelect(selectedItem()!)"
        >
          Select
        </button>
      </mat-dialog-actions>
    </div>
  `,
  styles: [`
    .fluent-lookup-dialog {
      display: flex;
      flex-direction: column;
      min-width: 650px;
      max-width: 850px;
      font-family: var(--fluent-font-family, 'Segoe UI', sans-serif);
    }
    .dialog-header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      padding: 14px 20px 8px;
      border-bottom: 1px solid #edebe9;
      .dialog-title {
        display: flex;
        align-items: center;
        gap: 8px;
        margin: 0;
        font-size: 16px;
        font-weight: 600;
        color: #201f1e;
        .title-icon { color: #0078d4; }
      }
    }
    .dialog-content {
      padding: 16px 20px;
      max-height: 450px;
      overflow-y: auto;
    }
    .search-bar {
      display: flex;
      align-items: center;
      border: 1px solid #8a8886;
      border-radius: 2px;
      padding: 4px 8px;
      background: #ffffff;
      margin-bottom: 12px;
      &:focus-within {
        border-color: #0078d4;
        box-shadow: 0 0 0 1px #0078d4;
      }
      .search-icon {
        color: #605e5c;
        font-size: 18px;
        margin-right: 6px;
      }
      .search-input {
        flex: 1;
        border: none;
        outline: none;
        font-size: 13px;
        font-family: inherit;
      }
      .clear-search-btn {
        background: transparent;
        border: none;
        cursor: pointer;
        color: #605e5c;
        display: flex;
        align-items: center;
        mat-icon { font-size: 16px; }
      }
    }
    .results-table-container {
      border: 1px solid #edebe9;
      max-height: 320px;
      overflow-y: auto;
    }
    .lookup-mat-table {
      width: 100%;
      border-collapse: collapse;
      th {
        background: #faf9f8;
        font-weight: 600;
        color: #605e5c;
        font-size: 12px;
        padding: 8px 10px;
      }
      td {
        padding: 8px 10px;
        font-size: 13px;
      }
      .lookup-data-row {
        cursor: pointer;
        transition: background 0.1s;
        &:hover { background: #f3f2f1; }
        &.is-selected {
          background: #eff6fc;
          font-weight: 600;
        }
      }
    }
    .no-results-box {
      display: flex;
      align-items: center;
      justify-content: center;
      gap: 8px;
      padding: 32px;
      color: #605e5c;
      font-size: 13px;
    }
    .dialog-actions {
      padding: 12px 20px;
      border-top: 1px solid #edebe9;
    }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class LookupDialogComponent<TItem = any> {
  readonly searchQuery = signal<string>('');
  readonly selectedItem = signal<TItem | null>(null);

  readonly displayedColumns: string[];

  constructor(
    public dialogRef: MatDialogRef<LookupDialogComponent<TItem>>,
    @Inject(MAT_DIALOG_DATA) public data: LookupDialogData<TItem>
  ) {
    this.displayedColumns = data.config.columns.map(c => c.field);
    if (data.initialQuery) {
      this.searchQuery.set(data.initialQuery);
    }
  }

  getColValue(item: any, field: string): any {
    return item ? (item as any)[field] : '';
  }

  readonly allItems = computed<TItem[]>(() => {
    const raw = this.data.config.items;
    return typeof raw === 'function' ? raw() : (raw || []);
  });

  readonly filteredItems = computed<TItem[]>(() => {
    const query = this.searchQuery().trim().toLowerCase();
    const items = this.allItems();
    if (!query) return items;

    if (this.data.config.filterFn) {
      return items.filter(it => this.data.config.filterFn!(it, query));
    }

    return items.filter(it => {
      return Object.values(it as any).some(val =>
        val !== null && val !== undefined && String(val).toLowerCase().includes(query)
      );
    });
  });

  onSelect(item: TItem): void {
    this.dialogRef.close(item);
  }

  onClose(): void {
    this.dialogRef.close(null);
  }
}
