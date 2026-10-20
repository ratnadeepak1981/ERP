import { Component, Input, Output, EventEmitter, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';

@Component({
  selector: 'app-mat-table-row-actions',
  standalone: true,
  imports: [CommonModule, MatButtonModule, MatIconModule, MatTooltipModule],
  template: `
    <div class="row-actions-container">
      @if (allowDelete) {
        <button
          type="button"
          class="row-action-btn delete-btn"
          tabindex="-1"
          matTooltip="Delete line"
          (click)="onDelete($event)"
          aria-label="Delete line item"
        >
          🗑
        </button>
      }
    </div>
  `,
  styles: [`
    .row-actions-container {
      display: flex;
      align-items: center;
      justify-content: center;
      gap: 4px;
      height: 100%;
    }
    .row-action-btn {
      background: transparent;
      border: none;
      cursor: pointer;
      color: #605e5c;
      padding: 4px 6px;
      border-radius: 3px;
      font-size: 13px;
      line-height: 1;
      display: flex;
      align-items: center;
      justify-content: center;
      &:hover {
        background: #fde7e9;
        color: #a4262c;
      }
    }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class TableRowActionsComponent {
  @Input() allowDelete: boolean = true;
  @Output() delete = new EventEmitter<void>();

  onDelete(event: Event): void {
    event.stopPropagation();
    this.delete.emit();
  }
}
