import { Component, Input, Output, EventEmitter, ChangeDetectionStrategy } from '@angular/core';
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
      <button
        type="button"
        class="view-btn"
        [class.active]="view === 'table'"
        [attr.aria-pressed]="view === 'table'"
        aria-label="Table view"
        pTooltip="Table view"
        tooltipPosition="top"
        (click)="selectView('table')"
      >
        <i class="pi pi-table" aria-hidden="true"></i>
      </button>

      <button
        type="button"
        class="view-btn"
        [class.active]="view === 'list'"
        [attr.aria-pressed]="view === 'list'"
        aria-label="List view"
        pTooltip="List view"
        tooltipPosition="top"
        (click)="selectView('list')"
      >
        <i class="pi pi-list" aria-hidden="true"></i>
      </button>

      <button
        type="button"
        class="view-btn"
        [class.active]="view === 'card'"
        [attr.aria-pressed]="view === 'card'"
        aria-label="Card view"
        pTooltip="Card view"
        tooltipPosition="top"
        (click)="selectView('card')"
      >
        <i class="pi pi-th-large" aria-hidden="true"></i>
      </button>
    </div>
  `,
  styles: [`
    .table-view-switcher-group {
      display: inline-flex;
      align-items: center;
      gap: 2px;
      padding: 2px;
      background-color: var(--erp-bg-surface, #ffffff);
      border: 1px solid var(--erp-border, #e2e8f0);
      border-radius: var(--erp-radius-md, 6px);
      box-sizing: border-box;
      height: 32px;

      .view-btn {
        display: inline-flex;
        align-items: center;
        justify-content: center;
        width: 26px;
        height: 26px;
        border: none;
        background: transparent;
        color: var(--erp-text-secondary, #64748b);
        border-radius: var(--erp-radius-sm, 4px);
        cursor: pointer;
        transition: all 0.15s ease-in-out;
        padding: 0;

        i {
          font-size: 13px;
          line-height: 1;
        }

        &:hover:not(.active) {
          background-color: var(--erp-bg-hover, #f1f5f9);
          color: var(--erp-text-main, #0f172a);
        }

        &.active {
          background-color: #eff6fc;
          color: var(--erp-primary, #0f6cbd);
          box-shadow: 0 1px 2px rgba(15, 108, 189, 0.1);

          i {
            color: var(--erp-primary, #0f6cbd);
            font-weight: bold;
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
  @Input() view: TableViewMode = 'table';
  @Output() viewChange = new EventEmitter<TableViewMode>();

  selectView(mode: TableViewMode): void {
    if (this.view !== mode) {
      this.view = mode;
      this.viewChange.emit(mode);
    }
  }
}
