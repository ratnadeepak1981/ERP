import {
  Component,
  Input,
  Output,
  EventEmitter,
  ChangeDetectionStrategy,
  computed
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { TableSortMeta } from '../../../models/data-table-config.model';

@Component({
  selector: 'app-table-sort',
  standalone: true,
  imports: [CommonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <button
      type="button"
      class="table-sort-trigger"
      [class.active]="order() !== 0"
      (click)="onClick($event)"
      [attr.aria-label]="ariaLabel()"
      [attr.aria-sort]="ariaSort()"
    >
      <i [class]="iconClass()" aria-hidden="true"></i>
      @if (sortIndex() > 0) {
        <span class="sort-badge">{{ sortIndex() }}</span>
      }
    </button>
  `,
  styles: [`
    .table-sort-trigger {
      display: inline-flex;
      align-items: center;
      gap: 3px;
      background: transparent;
      border: none;
      padding: 2px 4px;
      cursor: pointer;
      color: var(--erp-text-muted, #64748b);
      border-radius: var(--erp-radius-sm, 4px);
      transition: color 0.15s, background-color 0.15s;

      &:hover {
        color: var(--erp-primary, #0f6cbd);
        background-color: var(--erp-bg-hover, #f1f5f9);
      }

      &:focus-visible {
        outline: 2px solid var(--erp-primary, #0f6cbd);
      }

      &.active {
        color: var(--erp-primary, #0f6cbd);
        font-weight: 600;
      }

      i {
        font-size: 11px;
      }

      .sort-badge {
        font-size: 10px;
        background: var(--erp-primary, #0f6cbd);
        color: #ffffff;
        border-radius: 9999px;
        width: 14px;
        height: 14px;
        display: inline-flex;
        align-items: center;
        justify-content: center;
        line-height: 1;
      }
    }
  `]
})
export class TableSortComponent {
  @Input({ required: true }) field!: string;
  @Input() sortMode: 'single' | 'multiple' = 'single';
  @Input() sortField?: string;
  @Input() sortOrder: 1 | -1 | 0 = 0;
  @Input() multiSortMeta?: TableSortMeta[];

  @Output() sortChange = new EventEmitter<{ field: string; order: 1 | -1 | 0 }>();

  readonly order = computed(() => {
    if (this.sortMode === 'multiple' && this.multiSortMeta) {
      const found = this.multiSortMeta.find(m => m.field === this.field);
      return found ? found.order : 0;
    }
    return this.sortField === this.field ? this.sortOrder : 0;
  });

  readonly sortIndex = computed(() => {
    if (this.sortMode === 'multiple' && this.multiSortMeta && this.multiSortMeta.length > 1) {
      const idx = this.multiSortMeta.findIndex(m => m.field === this.field);
      return idx >= 0 ? idx + 1 : 0;
    }
    return 0;
  });

  readonly iconClass = computed(() => {
    const o = this.order();
    if (o === 1) return 'pi pi-sort-amount-up-alt';
    if (o === -1) return 'pi pi-sort-amount-down';
    return 'pi pi-sort-alt';
  });

  readonly ariaLabel = computed(() => {
    const o = this.order();
    if (o === 1) return `Sort ${this.field} descending`;
    if (o === -1) return `Clear sort on ${this.field}`;
    return `Sort ${this.field} ascending`;
  });

  readonly ariaSort = computed(() => {
    const o = this.order();
    if (o === 1) return 'ascending';
    if (o === -1) return 'descending';
    return 'none';
  });

  onClick(event: Event): void {
    event.stopPropagation();
    const current = this.order();
    let next: 1 | -1 | 0 = 1;
    if (current === 1) {
      next = -1;
    } else if (current === -1) {
      next = 0;
    }
    this.sortChange.emit({ field: this.field, order: next });
  }
}
