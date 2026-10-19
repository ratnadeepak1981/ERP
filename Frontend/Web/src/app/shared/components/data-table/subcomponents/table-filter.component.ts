import {
  Component,
  Input,
  Output,
  EventEmitter,
  ChangeDetectionStrategy,
  signal,
  ElementRef,
  HostListener,
  inject
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { InputTextModule } from 'primeng/inputtext';
import { ButtonModule } from 'primeng/button';
import { DataTableColumn, FilterType, SelectOption } from '../../../models/data-table-config.model';
import { AppCheckboxComponent } from '../../checkbox/checkbox.component';

@Component({
  selector: 'app-table-filter',
  standalone: true,
  imports: [CommonModule, FormsModule, InputTextModule, ButtonModule, AppCheckboxComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="table-filter-wrapper">
      <button
        type="button"
        class="filter-trigger-btn"
        [class.active]="isFilterActive()"
        (click)="toggleMenu($event)"
        [title]="'Filter by ' + column.header"
        [attr.aria-label]="'Filter by ' + column.header"
      >
        <i class="pi pi-filter" aria-hidden="true"></i>
      </button>

      @if (isOpen()) {
        <div class="filter-popover erp-card erp-card-elevated" (click)="$event.stopPropagation()">
          <div class="popover-header">
            <span class="header-title">Filter {{ column.header }}</span>
            <button type="button" class="close-btn" (click)="closeMenu()">
              <i class="pi pi-times"></i>
            </button>
          </div>

          <div class="popover-body">
            <!-- Text Filter -->
            @if (filterType === 'text') {
              <div class="filter-control-group">
                <label class="filter-label">Match Rule</label>
                <select class="erp-select" [(ngModel)]="matchMode">
                  <option value="contains">Contains</option>
                  <option value="startsWith">Starts With</option>
                  <option value="equals">Exact Match</option>
                </select>
                <input
                  pInputText
                  type="text"
                  [(ngModel)]="filterValue"
                  placeholder="Enter filter term..."
                  class="erp-filter-input"
                  (keydown.enter)="apply()"
                />
              </div>
            }

            <!-- Numeric Filter -->
            @if (filterType === 'numeric') {
              <div class="filter-control-group">
                <label class="filter-label">Condition</label>
                <select class="erp-select" [(ngModel)]="matchMode">
                  <option value="equals">Equals</option>
                  <option value="gt">Greater Than (&gt;)</option>
                  <option value="gte">Greater or Equal (&ge;)</option>
                  <option value="lt">Less Than (&lt;)</option>
                  <option value="lte">Less or Equal (&le;)</option>
                </select>
                <input
                  pInputText
                  type="number"
                  [(ngModel)]="filterValue"
                  placeholder="Enter numeric value..."
                  class="erp-filter-input"
                  (keydown.enter)="apply()"
                />
              </div>
            }

            <!-- Date Filter -->
            @if (filterType === 'date') {
              <div class="filter-control-group">
                <label class="filter-label">Date</label>
                <input
                  type="date"
                  class="erp-date-input"
                  [(ngModel)]="filterValue"
                  (keydown.enter)="apply()"
                />
              </div>
            }

            <!-- Select / Enum / Multiselect Filter (Excel Style) -->
            @if (filterType === 'select' && options.length > 0) {
              <div class="filter-control-group">
                <label class="filter-label">Select Values</label>
                <div class="options-checklist">
                  @for (opt of options; track opt.value) {
                    <div class="checklist-item">
                      <app-checkbox
                        [checked]="isOptionSelected(opt.value)"
                        (checkedChange)="toggleOption(opt.value, $event)"
                        [label]="opt.label"
                        size="small"
                      ></app-checkbox>
                    </div>
                  }
                </div>
              </div>
            }
          </div>

          <div class="popover-footer">
            <p-button
              label="Clear"
              size="small"
              [outlined]="true"
              severity="secondary"
              (onClick)="clear()"
            ></p-button>
            <p-button
              label="Apply"
              size="small"
              (onClick)="apply()"
            ></p-button>
          </div>
        </div>
      }
    </div>
  `,
  styles: [`
    .table-filter-wrapper {
      position: relative;
      display: inline-flex;
      align-items: center;

      .filter-trigger-btn {
        background: transparent;
        border: none;
        padding: 3px 5px;
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
          background-color: var(--erp-primary-light, #ebf3fc);
          font-weight: bold;
        }

        i {
          font-size: 11px;
        }
      }

      .filter-popover {
        position: absolute;
        top: calc(100% + 4px);
        right: 0;
        z-index: 1000;
        width: 250px;
        background: var(--erp-bg-surface, #ffffff);
        border: 1px solid var(--erp-border, #e2e8f0);
        border-radius: var(--erp-radius-md, 6px);
        box-shadow: var(--erp-shadow-md);
        padding: 0;
        text-align: left;
        color: var(--erp-text-main, #0f172a);

        .popover-header {
          display: flex;
          align-items: center;
          justify-content: space-between;
          padding: 8px 12px;
          border-bottom: 1px solid var(--erp-border, #e2e8f0);

          .header-title {
            font-size: 12px;
            font-weight: 600;
            color: var(--erp-text-secondary, #475569);
          }

          .close-btn {
            background: transparent;
            border: none;
            color: var(--erp-text-muted, #64748b);
            cursor: pointer;
            padding: 2px;
            display: flex;
            align-items: center;

            &:hover {
              color: var(--erp-text-main, #0f172a);
            }
          }
        }

        .popover-body {
          padding: 12px;

          .filter-control-group {
            display: flex;
            flex-direction: column;
            gap: 6px;

            .filter-label {
              font-size: 11.5px;
              font-weight: 500;
              color: var(--erp-text-secondary, #475569);
            }

            .erp-select {
              height: 30px;
              font-size: 12.5px;
              border-radius: var(--erp-radius-sm, 4px);
              border: 1px solid var(--erp-border, #e2e8f0);
              background: var(--erp-bg-surface, #ffffff);
              color: var(--erp-text-main, #0f172a);
              padding: 0 8px;

              &:focus {
                border-color: var(--erp-primary, #0f6cbd);
                outline: none;
              }
            }

            .erp-filter-input,
            .erp-date-input {
              height: 30px;
              font-size: 12.5px;
              border-radius: var(--erp-radius-sm, 4px);
              border: 1px solid var(--erp-border, #e2e8f0);
              background: var(--erp-bg-surface, #ffffff);
              color: var(--erp-text-main, #0f172a);
              padding: 4px 8px;
              box-sizing: border-box;

              &:focus {
                border-color: var(--erp-primary, #0f6cbd);
                outline: none;
              }
            }

            .options-checklist {
              max-height: 140px;
              overflow-y: auto;
              border: 1px solid var(--erp-border-subtle, #edf2f7);
              border-radius: var(--erp-radius-sm, 4px);
              padding: 6px 8px;
              display: flex;
              flex-direction: column;
              gap: 6px;

              .checklist-item {
                display: flex;
                align-items: center;
              }
            }
          }
        }

        .popover-footer {
          display: flex;
          justify-content: flex-end;
          gap: 6px;
          padding: 8px 12px;
          background: var(--erp-bg-secondary, #f1f5f9);
          border-top: 1px solid var(--erp-border, #e2e8f0);
          border-bottom-left-radius: var(--erp-radius-md, 6px);
          border-bottom-right-radius: var(--erp-radius-md, 6px);
        }
      }
    }
  `]
})
export class TableFilterComponent {
  private readonly elementRef = inject(ElementRef);

  @Input({ required: true }) column!: DataTableColumn;
  @Input() value: any = null;

  @Output() filterChange = new EventEmitter<{ field: string; value: any; matchMode: string }>();
  @Output() filterClear = new EventEmitter<string>();

  readonly isOpen = signal<boolean>(false);
  filterValue: any = '';
  matchMode: string = 'contains';
  selectedOptions: any[] = [];

  get filterType(): FilterType {
    if (this.column.filterType) return this.column.filterType;
    if (this.column.type === 'number' || this.column.type === 'currency') return 'numeric';
    if (this.column.type === 'date') return 'date';
    if (this.column.filterOptions && this.column.filterOptions.length > 0) return 'select';
    return 'text';
  }

  get options(): SelectOption[] {
    return this.column.filterOptions || [];
  }

  isFilterActive(): boolean {
    if (this.filterType === 'select') {
      return this.selectedOptions.length > 0;
    }
    return this.filterValue !== null && this.filterValue !== '' && this.filterValue !== undefined;
  }

  toggleMenu(event: Event): void {
    event.stopPropagation();
    this.isOpen.update(open => !open);
    if (this.isOpen()) {
      if (this.filterType === 'select') {
        this.selectedOptions = Array.isArray(this.value) ? [...this.value] : [];
      } else {
        this.filterValue = this.value ?? '';
      }
    }
  }

  closeMenu(): void {
    this.isOpen.set(false);
  }

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent): void {
    if (this.isOpen() && !this.elementRef.nativeElement.contains(event.target)) {
      this.closeMenu();
    }
  }

  isOptionSelected(val: any): boolean {
    return this.selectedOptions.includes(val);
  }

  toggleOption(val: any, checked: boolean): void {
    if (checked) {
      if (!this.selectedOptions.includes(val)) {
        this.selectedOptions.push(val);
      }
    } else {
      this.selectedOptions = this.selectedOptions.filter(o => o !== val);
    }
  }

  apply(): void {
    const field = String(this.column.field);
    if (this.filterType === 'select') {
      this.filterChange.emit({
        field,
        value: this.selectedOptions.length > 0 ? [...this.selectedOptions] : null,
        matchMode: 'in'
      });
    } else {
      this.filterChange.emit({
        field,
        value: this.filterValue !== '' ? this.filterValue : null,
        matchMode: this.matchMode
      });
    }
    this.closeMenu();
  }

  clear(): void {
    const field = String(this.column.field);
    this.filterValue = '';
    this.selectedOptions = [];
    this.filterClear.emit(field);
    this.closeMenu();
  }
}
