import {
  Component,
  Input,
  Output,
  EventEmitter,
  ChangeDetectionStrategy,
  signal,
  effect
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { InputTextModule } from 'primeng/inputtext';

@Component({
  selector: 'app-table-search',
  standalone: true,
  imports: [CommonModule, FormsModule, InputTextModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="table-search-box">
      <i class="pi pi-search search-icon" aria-hidden="true"></i>
      <input
        pInputText
        type="text"
        [ngModel]="currentValue()"
        (ngModelChange)="onModelChange($event)"
        (keydown.escape)="clear()"
        [placeholder]="placeholder"
        [attr.aria-label]="ariaLabel || placeholder"
        class="erp-table-search-input"
      />
      @if (currentValue()) {
        <button
          type="button"
          class="clear-search-btn"
          (click)="clear()"
          title="Clear search"
          aria-label="Clear search"
        >
          <i class="pi pi-times" aria-hidden="true"></i>
        </button>
      }
    </div>
  `,
  styles: [`
    .table-search-box {
      position: relative;
      display: inline-flex;
      align-items: center;
      width: 100%;
      max-width: 320px;

      .search-icon {
        position: absolute;
        left: 10px;
        color: var(--erp-text-muted, #64748b);
        font-size: 13px;
        pointer-events: none;
      }

      .erp-table-search-input {
        width: 100%;
        padding-left: 32px;
        padding-right: 28px;
        height: 32px;
        font-size: 13px;
        border-radius: var(--erp-radius-sm, 4px);
        border: 1px solid var(--erp-border, #e2e8f0);
        background: var(--erp-bg-surface, #ffffff);
        color: var(--erp-text-main, #0f172a);
        transition: border-color 0.15s ease-in-out, box-shadow 0.15s ease-in-out;

        &:focus {
          border-color: var(--erp-primary, #0f6cbd);
          box-shadow: 0 0 0 1px var(--erp-primary, #0f6cbd);
          outline: none;
        }
      }

      .clear-search-btn {
        position: absolute;
        right: 8px;
        background: transparent;
        border: none;
        color: var(--erp-text-muted, #64748b);
        cursor: pointer;
        padding: 2px 4px;
        display: flex;
        align-items: center;
        justify-content: center;
        border-radius: 2px;

        &:hover {
          color: var(--erp-text-main, #0f172a);
          background-color: var(--erp-bg-secondary, #f1f5f9);
        }

        &:focus-visible {
          outline: 2px solid var(--erp-primary, #0f6cbd);
        }
      }
    }
  `]
})
export class TableSearchComponent {
  @Input() placeholder: string = 'Search records...';
  @Input() ariaLabel?: string;
  @Input() debounceTimeMs: number = 250;

  @Input() set value(val: string) {
    this.currentValue.set(val || '');
  }

  @Output() searchChange = new EventEmitter<string>();
  @Output() searchClear = new EventEmitter<void>();

  readonly currentValue = signal<string>('');
  private debounceTimer: any = null;

  onModelChange(val: string): void {
    this.currentValue.set(val);
    if (this.debounceTimer) {
      clearTimeout(this.debounceTimer);
    }
    this.debounceTimer = setTimeout(() => {
      this.searchChange.emit(val);
    }, this.debounceTimeMs);
  }

  clear(): void {
    if (this.debounceTimer) {
      clearTimeout(this.debounceTimer);
    }
    this.currentValue.set('');
    this.searchChange.emit('');
    this.searchClear.emit();
  }
}
