import { Component, Input, Output, EventEmitter, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';

@Component({
  selector: 'app-mat-table-toolbar',
  standalone: true,
  imports: [CommonModule, MatButtonModule, MatIconModule],
  template: `
    <div class="fluent-table-toolbar">
      <div class="toolbar-left">
        @if (allowAddRow) {
          <button
            type="button"
            class="fluent-toolbar-btn primary-btn"
            (click)="addRow.emit()"
          >
            <span class="btn-icon">＋</span>
            <span>Add Line</span>
          </button>
        }

        <button
          type="button"
          class="fluent-toolbar-btn success-btn"
          [disabled]="dirtyCount === 0"
          (click)="save.emit()"
          title="Save all changes to in-memory demo data"
        >
          <span class="btn-icon">💾</span>
          <span>Save (In-Memory)</span>
          @if (dirtyCount > 0) {
            <span class="dirty-badge">{{ dirtyCount }}</span>
          }
        </button>

        <button
          type="button"
          class="fluent-toolbar-btn secondary-btn"
          [disabled]="dirtyCount === 0"
          (click)="cancel.emit()"
          title="Discard unsaved changes and reset to baseline"
        >
          <span class="btn-icon">↺</span>
          <span>Reset Changes</span>
        </button>
      </div>

      <div class="toolbar-right">
        <span class="total-lines-label">{{ totalLines }} {{ totalLines === 1 ? 'line' : 'lines' }}</span>
      </div>
    </div>
  `,
  styles: [`
    .fluent-table-toolbar {
      display: flex;
      align-items: center;
      justify-content: space-between;
      padding: 8px 12px;
      background: var(--fluent-neutral-lightest, #faf9f8);
      border-bottom: 1px solid var(--fluent-border-color, #edebe9);
    }
    .toolbar-left {
      display: flex;
      align-items: center;
      gap: 8px;
    }
    .toolbar-right {
      display: flex;
      align-items: center;
      font-size: 12px;
      color: #605e5c;
    }
    .fluent-toolbar-btn {
      display: flex;
      align-items: center;
      gap: 6px;
      padding: 5px 12px;
      font-family: inherit;
      font-size: 12px;
      font-weight: 600;
      border: 1px solid #d2d0ce;
      border-radius: 3px;
      background: #ffffff;
      color: #201f1e;
      cursor: pointer;
      transition: all 0.15s ease;
      &:hover:not(:disabled) {
        background: #f3f2f1;
        border-color: #8a8886;
      }
      &:disabled {
        opacity: 0.45;
        cursor: not-allowed;
      }
      &.primary-btn {
        background: #0078d4;
        color: #ffffff;
        border-color: #0078d4;
        &:hover:not(:disabled) {
          background: #106ebe;
        }
      }
      &.success-btn {
        background: #107c41;
        color: #ffffff;
        border-color: #107c41;
        &:hover:not(:disabled) {
          background: #0b6a37;
        }
      }
    }
    .btn-icon {
      font-size: 13px;
    }
    .dirty-badge {
      background: rgba(255, 255, 255, 0.3);
      padding: 0 5px;
      border-radius: 10px;
      font-size: 10px;
    }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class TableToolbarComponent {
  @Input() allowAddRow: boolean = true;
  @Input() dirtyCount: number = 0;
  @Input() totalLines: number = 0;

  @Output() addRow = new EventEmitter<void>();
  @Output() save = new EventEmitter<void>();
  @Output() cancel = new EventEmitter<void>();
}
