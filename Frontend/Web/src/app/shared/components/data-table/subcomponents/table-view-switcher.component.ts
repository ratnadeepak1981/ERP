import { Component, Input, Output, EventEmitter, ChangeDetectionStrategy, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TooltipModule } from 'primeng/tooltip';

export type TableViewMode = 'table' | 'list' | 'card';

@Component({
  selector: 'app-table-view-switcher',
  standalone: true,
  imports: [CommonModule, TooltipModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div
      class="table-view-switcher-group"
      role="group"
      aria-label="Table display view options"
    >
      @if (isAvailable('table')) {
        <button
          type="button"
          class="view-btn"
          [class.active]="view === 'table'"
          [attr.aria-pressed]="view === 'table'"
          aria-label="Table view"
          pTooltip="Table view"
          tooltipPosition="top"
          (click)="selectView('table')"
          data-testid="view-btn-table"
        >
          <svg
            class="view-icon lucide lucide-table-2"
            xmlns="http://www.w3.org/2000/svg"
            width="15"
            height="15"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            stroke-width="2"
            stroke-linecap="round"
            stroke-linejoin="round"
            aria-hidden="true"
          >
            <path d="M9 3H5a2 2 0 0 0-2 2v4m6-6h10a2 2 0 0 1 2 2v4M9 3v18m0 0h10a2 2 0 0 0 2-2V9M9 21H5a2 2 0 0 1-2-2V9m0 0h18"/>
          </svg>
        </button>
      }

      @if (isAvailable('list')) {
        <button
          type="button"
          class="view-btn"
          [class.active]="view === 'list'"
          [attr.aria-pressed]="view === 'list'"
          aria-label="List view"
          pTooltip="List view"
          tooltipPosition="top"
          (click)="selectView('list')"
          data-testid="view-btn-list"
        >
          <svg
            class="view-icon lucide lucide-list"
            xmlns="http://www.w3.org/2000/svg"
            width="15"
            height="15"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            stroke-width="2"
            stroke-linecap="round"
            stroke-linejoin="round"
            aria-hidden="true"
          >
            <line x1="8" x2="21" y1="6" y2="6"/>
            <line x1="8" x2="21" y1="12" y2="12"/>
            <line x1="8" x2="21" y1="18" y2="18"/>
            <line x1="3" x2="3.01" y1="6" y2="6"/>
            <line x1="3" x2="3.01" y1="12" y2="12"/>
            <line x1="3" x2="3.01" y1="18" y2="18"/>
          </svg>
        </button>
      }

      @if (isAvailable('card')) {
        <button
          type="button"
          class="view-btn"
          [class.active]="view === 'card'"
          [attr.aria-pressed]="view === 'card'"
          aria-label="Cards view"
          pTooltip="Cards view"
          tooltipPosition="top"
          (click)="selectView('card')"
          data-testid="view-btn-card"
        >
          <svg
            class="view-icon lucide lucide-layout-grid"
            xmlns="http://www.w3.org/2000/svg"
            width="15"
            height="15"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            stroke-width="2"
            stroke-linecap="round"
            stroke-linejoin="round"
            aria-hidden="true"
          >
            <rect width="7" height="7" x="3" y="3" rx="1"/>
            <rect width="7" height="7" x="14" y="3" rx="1"/>
            <rect width="7" height="7" x="14" y="14" rx="1"/>
            <rect width="7" height="7" x="3" y="14" rx="1"/>
          </svg>
        </button>
      }
    </div>
  `,
  styles: [`
    .table-view-switcher-group {
      display: inline-flex;
      align-items: center;
      gap: 3px;
      padding: 2px;
      background-color: var(--erp-bg-surface, #ffffff);
      border: 1px solid var(--erp-border, #d1d5db);
      border-radius: var(--erp-radius-md, 6px);
      box-sizing: border-box;
      height: 32px;

      .view-btn {
        display: inline-flex;
        align-items: center;
        justify-content: center;
        width: 26px;
        height: 26px;
        border: 1px solid transparent;
        background: transparent;
        color: var(--erp-text-secondary, #64748b);
        border-radius: var(--erp-radius-sm, 4px);
        cursor: pointer;
        transition: all 0.15s ease-in-out;
        padding: 0;

        .view-icon {
          display: block;
          color: currentColor;
          flex-shrink: 0;
        }

        &:hover:not(.active) {
          background-color: var(--erp-bg-hover, #f1f5f9);
          color: var(--erp-text-main, #0f172a);
          border-color: #e2e8f0;
        }

        &.active {
          background-color: #eff6fc;
          color: var(--erp-primary, #0f6cbd);
          border-color: #bfdbfe;
          box-shadow: 0 1px 2px rgba(15, 108, 189, 0.08);

          .view-icon {
            color: var(--erp-primary, #0f6cbd);
            stroke-width: 2.2;
          }
        }

        &:focus-visible {
          outline: 2px solid var(--erp-primary, #0f6cbd);
          outline-offset: 1px;
        }
      }
    }
  `]
})
export class TableViewSwitcherComponent {
  readonly currentView = signal<TableViewMode>('table');
  readonly permittedViews = signal<TableViewMode[]>(['table', 'list', 'card']);

  @Input() set view(val: TableViewMode) {
    this.currentView.set(val || 'table');
  }
  get view(): TableViewMode {
    return this.currentView();
  }

  @Input() set availableViews(val: TableViewMode[]) {
    this.permittedViews.set(val || ['table', 'list', 'card']);
  }
  get availableViews(): TableViewMode[] {
    return this.permittedViews();
  }

  @Output() viewChange = new EventEmitter<TableViewMode>();

  isAvailable(mode: TableViewMode): boolean {
    const list = this.permittedViews();
    return !list || list.includes(mode);
  }

  selectView(mode: TableViewMode): void {
    if (this.currentView() !== mode && this.isAvailable(mode)) {
      this.currentView.set(mode);
      this.viewChange.emit(mode);
    }
  }
}
