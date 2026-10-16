import {
  Component,
  Input,
  Output,
  EventEmitter,
  ChangeDetectionStrategy,
  computed,
  signal
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'app-table-pagination',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="erp-pagination-container">
      <div class="pagination-info">
        <span>Showing <strong>{{ startRecord() }}</strong> to <strong>{{ endRecord() }}</strong> of <strong>{{ totalRecords }}</strong> entries</span>
      </div>

      <div class="pagination-controls">
        <!-- Rows per page selector -->
        <div class="rows-per-page">
          <label for="erp-page-size-select">Per page:</label>
          <select
            id="erp-page-size-select"
            class="page-size-select"
            [ngModel]="rows"
            (ngModelChange)="onRowsChange($event)"
            aria-label="Rows per page"
          >
            @for (opt of rowsPerPageOptions; track opt) {
              <option [value]="opt">{{ opt }}</option>
            }
          </select>
        </div>

        <!-- Navigation buttons -->
        <div class="nav-button-group">
          <button
            type="button"
            class="paginator-btn"
            [disabled]="currentPage() === 1"
            (click)="goToPage(1)"
            title="First page"
            aria-label="First page"
          >
            <i class="pi pi-angle-double-left"></i>
          </button>
          <button
            type="button"
            class="paginator-btn"
            [disabled]="currentPage() === 1"
            (click)="goToPage(currentPage() - 1)"
            title="Previous page"
            aria-label="Previous page"
          >
            <i class="pi pi-angle-left"></i>
          </button>

          <!-- Page numbers -->
          @for (p of visiblePages(); track p) {
            <button
              type="button"
              class="paginator-btn page-num"
              [class.active]="p === currentPage()"
              (click)="goToPage(p)"
              [attr.aria-current]="p === currentPage() ? 'page' : null"
            >
              {{ p }}
            </button>
          }

          <button
            type="button"
            class="paginator-btn"
            [disabled]="currentPage() === totalPages() || totalPages() === 0"
            (click)="goToPage(currentPage() + 1)"
            title="Next page"
            aria-label="Next page"
          >
            <i class="pi pi-angle-right"></i>
          </button>
          <button
            type="button"
            class="paginator-btn"
            [disabled]="currentPage() === totalPages() || totalPages() === 0"
            (click)="goToPage(totalPages())"
            title="Last page"
            aria-label="Last page"
          >
            <i class="pi pi-angle-double-right"></i>
          </button>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .erp-pagination-container {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: var(--erp-space-3, 12px);
      padding: 8px 14px;
      background: var(--erp-bg-surface, #ffffff);
      border-top: 1px solid var(--erp-border, #e2e8f0);
      font-size: 13px;
      color: var(--erp-text-secondary, #475569);
      flex-wrap: wrap;

      .pagination-info {
        font-size: 12.5px;
        strong {
          color: var(--erp-text-main, #0f172a);
        }
      }

      .pagination-controls {
        display: flex;
        align-items: center;
        gap: var(--erp-space-3, 12px);
        flex-wrap: wrap;

        .rows-per-page {
          display: flex;
          align-items: center;
          gap: 6px;
          font-size: 12.5px;

          .page-size-select {
            height: 28px;
            font-size: 12px;
            border-radius: var(--erp-radius-sm, 4px);
            border: 1px solid var(--erp-border, #e2e8f0);
            background: var(--erp-bg-surface, #ffffff);
            color: var(--erp-text-main, #0f172a);
            padding: 0 6px;

            &:focus {
              border-color: var(--erp-primary, #0f6cbd);
              outline: none;
            }
          }
        }

        .nav-button-group {
          display: inline-flex;
          align-items: center;
          gap: 3px;

          .paginator-btn {
            min-width: 28px;
            height: 28px;
            padding: 0 6px;
            background: transparent;
            border: 1px solid transparent;
            border-radius: var(--erp-radius-sm, 4px);
            color: var(--erp-text-secondary, #475569);
            font-size: 12px;
            cursor: pointer;
            display: inline-flex;
            align-items: center;
            justify-content: center;
            transition: all 0.15s ease-in-out;

            &:hover:not(:disabled) {
              background-color: var(--erp-bg-hover, #f1f5f9);
              color: var(--erp-primary, #0f6cbd);
              border-color: var(--erp-border, #e2e8f0);
            }

            &:focus-visible {
              outline: 2px solid var(--erp-primary, #0f6cbd);
            }

            &:disabled {
              opacity: 0.35;
              cursor: not-allowed;
            }

            &.page-num.active {
              background-color: var(--erp-primary, #0f6cbd);
              color: #ffffff;
              font-weight: 600;
              border-color: var(--erp-primary, #0f6cbd);
            }

            i {
              font-size: 11px;
            }
          }
        }
      }
    }
  `]
})
export class TablePaginationComponent {
  @Input() first: number = 0;
  @Input() rows: number = 10;
  @Input() totalRecords: number = 0;
  @Input() rowsPerPageOptions: number[] = [5, 10, 25, 50];

  @Output() pageChange = new EventEmitter<{ first: number; rows: number; page: number; pageCount: number }>();

  readonly currentPage = computed(() => {
    if (this.rows <= 0) return 1;
    return Math.floor(this.first / this.rows) + 1;
  });

  readonly totalPages = computed(() => {
    if (this.rows <= 0 || this.totalRecords <= 0) return 0;
    return Math.ceil(this.totalRecords / this.rows);
  });

  readonly startRecord = computed(() => {
    if (this.totalRecords === 0) return 0;
    return this.first + 1;
  });

  readonly endRecord = computed(() => {
    return Math.min(this.first + this.rows, this.totalRecords);
  });

  readonly visiblePages = computed(() => {
    const total = this.totalPages();
    const current = this.currentPage();
    if (total <= 1) return [1];

    const pages: number[] = [];
    const maxVisible = 5;
    let start = Math.max(1, current - Math.floor(maxVisible / 2));
    let end = Math.min(total, start + maxVisible - 1);

    if (end - start + 1 < maxVisible) {
      start = Math.max(1, end - maxVisible + 1);
    }

    for (let i = start; i <= end; i++) {
      pages.push(i);
    }
    return pages;
  });

  goToPage(page: number): void {
    if (page < 1 || page > this.totalPages() || page === this.currentPage()) {
      return;
    }
    const newFirst = (page - 1) * this.rows;
    this.pageChange.emit({
      first: newFirst,
      rows: this.rows,
      page,
      pageCount: this.totalPages()
    });
  }

  onRowsChange(newRows: number): void {
    const rowsNum = Number(newRows);
    this.pageChange.emit({
      first: 0,
      rows: rowsNum,
      page: 1,
      pageCount: Math.ceil(this.totalRecords / rowsNum)
    });
  }
}
