import {
  Component,
  Input,
  Output,
  EventEmitter,
  ChangeDetectionStrategy
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { ButtonModule } from 'primeng/button';

@Component({
  selector: 'app-table-selection',
  standalone: true,
  imports: [CommonModule, ButtonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (selectedCount > 0) {
      <div class="selection-bar" role="status" aria-live="polite">
        <div class="selection-info">
          <i class="pi pi-check-circle" aria-hidden="true"></i>
          <span><strong>{{ selectedCount }}</strong> item{{ selectedCount > 1 ? 's' : '' }} selected</span>
          @if (totalCount > selectedCount) {
            <button
              type="button"
              class="select-all-btn"
              (click)="selectAll.emit()"
            >
              Select all {{ totalCount }}
            </button>
          }
        </div>

        <div class="selection-actions">
          <ng-content select="[actions]"></ng-content>
          <button
            pButton
            label="Clear"
            icon="pi pi-times"
            size="small"
            [text]="true"
            severity="secondary"
            (click)="clearSelection.emit()"
          ></button>
        </div>
      </div>
    }
  `,
  styles: [`
    .selection-bar {
      display: flex;
      align-items: center;
      justify-content: space-between;
      background-color: var(--erp-primary-light, #ebf3fc);
      border: 1px solid var(--erp-primary, #0f6cbd);
      border-radius: var(--erp-radius-sm, 4px);
      padding: 6px 12px;
      margin-bottom: var(--erp-space-2, 8px);
      font-size: 13px;
      color: var(--erp-text-main, #0f172a);
      animation: fadeIn 0.15s ease-in-out;

      .selection-info {
        display: flex;
        align-items: center;
        gap: 8px;

        i {
          color: var(--erp-primary, #0f6cbd);
          font-size: 14px;
        }

        strong {
          color: var(--erp-primary, #0f6cbd);
        }

        .select-all-btn {
          background: transparent;
          border: none;
          color: var(--erp-primary, #0f6cbd);
          font-weight: 600;
          cursor: pointer;
          text-decoration: underline;
          padding: 0 4px;
          font-size: 12.5px;

          &:hover {
            color: var(--erp-primary-hover, #115ea3);
          }
        }
      }

      .selection-actions {
        display: flex;
        align-items: center;
        gap: 6px;
      }
    }

    @keyframes fadeIn {
      from { opacity: 0; transform: translateY(-4px); }
      to { opacity: 1; transform: translateY(0); }
    }
  `]
})
export class TableSelectionComponent {
  @Input() selectedCount: number = 0;
  @Input() totalCount: number = 0;
  @Input() selectionMode: 'single' | 'multiple' | null = 'multiple';

  @Output() selectAll = new EventEmitter<void>();
  @Output() clearSelection = new EventEmitter<void>();
}
