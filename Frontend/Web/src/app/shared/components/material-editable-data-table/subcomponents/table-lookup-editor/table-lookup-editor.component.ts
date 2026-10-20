import {
  Component,
  Input,
  Output,
  EventEmitter,
  ElementRef,
  ViewChild,
  AfterViewInit,
  signal,
  computed,
  inject,
  ChangeDetectionStrategy
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MaterialLookupConfig } from '../../models/material-data-table.models';
import { LookupDialogComponent } from '../../lookup/lookup-dialog/lookup-dialog.component';

@Component({
  selector: 'app-mat-table-lookup-editor',
  standalone: true,
  imports: [CommonModule, FormsModule, MatDialogModule, MatIconModule],
  template: `
    <div class="fluent-lookup-editor-wrapper">
      <div class="lookup-input-box" [class.is-focused]="isInputFocused()">
        <input
          #lookupInput
          type="text"
          class="lookup-text-input"
          [placeholder]="placeholder || 'Search or select...'"
          [ngModel]="searchTerm()"
          (ngModelChange)="onSearchChange($event)"
          (keydown)="onKeyDown($event)"
          (focus)="isInputFocused.set(true)"
          (blur)="onBlur()"
          autocomplete="off"
        />

        @if (searchTerm()) {
          <button
            type="button"
            class="lookup-clear-btn"
            aria-label="Clear search"
            (mousedown)="clearSearch($event)"
          >
            ✕
          </button>
        }

        <!-- Dropdown toggle / Browse dialog button -->
        <button
          type="button"
          class="lookup-trigger-btn"
          aria-label="Open Lookup Dialog"
          (mousedown)="onTriggerClick($event)"
          title="Browse records"
        >
          <span class="chevron-icon">{{ isOpen() ? '▲' : '▼' }}</span>
        </button>

        @if (config.enableDialog !== false) {
          <button
            type="button"
            class="lookup-dialog-btn"
            aria-label="Open Full Search Dialog"
            (mousedown)="openFullDialog($event)"
            title="Open advanced lookup dialog"
          >
            ☷
          </button>
        }
      </div>

      <!-- D365-Style Dropdown Popup Results Table -->
      @if (isOpen()) {
        <div
          #resultsPanel
          class="fluent-lookup-panel"
          role="listbox"
          [style.minWidth]="panelMinWidth()"
        >
          @if (filteredItems().length === 0) {
            <div class="lookup-empty-message">
              <span>No matching records found</span>
            </div>
          } @else {
            <div class="lookup-results-list">
              @for (item of filteredItems(); track getItemValue(item); let i = $index) {
                <div
                  class="lookup-result-row"
                  role="option"
                  [class.is-highlighted]="i === activeIndex()"
                  [class.is-selected]="isItemSelected(item)"
                  (mousedown)="selectItem(item, $event)"
                  (mouseenter)="activeIndex.set(i)"
                >
                  <div class="row-primary">
                    <span class="row-code">{{ getItemDisplay(item) }}</span>
                    @if (getItemName(item); as name) {
                      <span class="row-name">{{ name }}</span>
                    }
                  </div>
                  @if (getItemProp(item, 'category') || getItemProp(item, 'unitOfMeasure') || getItemProp(item, 'description')) {
                    <div class="row-secondary">
                      @if (getItemProp(item, 'category'); as cat) {
                        <span class="badge-tag">{{ cat }}</span>
                      }
                      @if (getItemProp(item, 'unitOfMeasure'); as uom) {
                        <span class="uom-tag">UOM: {{ uom }}</span>
                      }
                      @if (getItemProp(item, 'standardCost') !== undefined; as cost) {
                        <span class="cost-tag">Cost: \${{ getItemProp(item, 'standardCost') }}</span>
                      }
                    </div>
                  }
                </div>
              }
            </div>
          }
        </div>
      }
    </div>
  `,
  styles: [`
    :host {
      display: block;
      width: 100%;
      height: 100%;
      position: relative;
    }
    .fluent-lookup-editor-wrapper {
      width: 100%;
      height: var(--fluent-row-height, 36px);
      position: relative;
    }
    .lookup-input-box {
      width: 100%;
      height: 100%;
      display: flex;
      align-items: center;
      background: #ffffff;
      border: 1px solid var(--fluent-primary, #0078d4);
      box-sizing: border-box;
      padding: 0 4px 0 8px;
      &.is-focused {
        box-shadow: 0 0 0 1px var(--fluent-primary, #0078d4);
      }
    }
    .lookup-text-input {
      flex: 1;
      height: 100%;
      border: none;
      outline: none;
      font-size: 13px;
      font-family: inherit;
      color: inherit;
      background: transparent;
    }
    .lookup-clear-btn, .lookup-trigger-btn, .lookup-dialog-btn {
      background: transparent;
      border: none;
      cursor: pointer;
      color: #605e5c;
      padding: 2px 6px;
      display: flex;
      align-items: center;
      justify-content: center;
      font-size: 12px;
      border-radius: 2px;
      &:hover {
        background: #f3f2f1;
        color: #201f1e;
      }
    }
    .chevron-icon {
      font-size: 8px;
    }
    .lookup-dialog-btn {
      font-size: 14px;
      color: #0078d4;
    }
    .fluent-lookup-panel {
      position: absolute;
      top: calc(100% + 2px);
      left: 0;
      max-height: 260px;
      overflow-y: auto;
      background: #ffffff;
      border: 1px solid #edebe9;
      box-shadow: 0 4px 16px rgba(0, 0, 0, 0.14), 0 0 2px rgba(0, 0, 0, 0.12);
      z-index: 1000;
      border-radius: 4px;
      padding: 4px 0;
      box-sizing: border-box;
      width: max-content;
      max-width: 520px;
    }
    .lookup-empty-message {
      padding: 14px;
      text-align: center;
      color: #605e5c;
      font-size: 12px;
    }
    .lookup-results-list {
      display: flex;
      flex-direction: column;
    }
    .lookup-result-row {
      padding: 8px 12px;
      cursor: pointer;
      border-bottom: 1px solid #f3f2f1;
      display: flex;
      flex-direction: column;
      gap: 3px;
      &:last-child {
        border-bottom: none;
      }
      &:hover, &.is-highlighted {
        background: #f3f2f1;
      }
      &.is-selected {
        background: #eff6fc;
        .row-code {
          color: #0078d4;
          font-weight: 600;
        }
      }
    }
    .row-primary {
      display: flex;
      align-items: center;
      gap: 8px;
      font-size: 13px;
    }
    .row-code {
      font-weight: 600;
      color: #201f1e;
    }
    .row-name {
      color: #323130;
    }
    .row-secondary {
      display: flex;
      align-items: center;
      gap: 8px;
      font-size: 11px;
      color: #605e5c;
    }
    .badge-tag {
      background: #edebe9;
      padding: 1px 6px;
      border-radius: 2px;
    }
    .uom-tag, .cost-tag {
      color: #605e5c;
    }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class TableLookupEditorComponent<TItem = any> implements AfterViewInit {
  private readonly dialog = inject(MatDialog);

  @Input() value: any;
  @Input() config!: MaterialLookupConfig<TItem>;
  @Input() placeholder?: string;

  @Output() valueChange = new EventEmitter<any>();
  @Output() itemSelect = new EventEmitter<TItem>();
  @Output() commit = new EventEmitter<void>();
  @Output() cancel = new EventEmitter<void>();
  @Output() tab = new EventEmitter<KeyboardEvent>();

  @ViewChild('lookupInput') lookupInputRef!: ElementRef<HTMLInputElement>;
  @ViewChild('resultsPanel') resultsPanelRef?: ElementRef<HTMLDivElement>;

  readonly searchTerm = signal<string>('');
  readonly isOpen = signal<boolean>(true);
  readonly activeIndex = signal<number>(-1);
  readonly isInputFocused = signal<boolean>(false);
  readonly panelMinWidth = signal<string>('240px');

  ngAfterViewInit(): void {
    queueMicrotask(() => {
      const input = this.lookupInputRef?.nativeElement;
      if (input) {
        input.focus();
        input.select();
        this.panelMinWidth.set(`${input.offsetWidth || 240}px`);
      }
    });
  }

  getItemValue(item: any): any {
    return item ? (item as any)[this.config.valueField] : null;
  }

  getItemDisplay(item: any): string {
    return item ? String((item as any)[this.config.displayField] || '') : '';
  }

  getItemName(item: any): string {
    if (!item) return '';
    const p = item as any;
    return p.productName || p.name || '';
  }

  getItemProp(item: any, prop: string): any {
    return item ? (item as any)[prop] : undefined;
  }

  isItemSelected(item: any): boolean {
    return this.getItemValue(item) === this.value;
  }

  readonly allItems = computed<TItem[]>(() => {
    const raw = this.config.items;
    return typeof raw === 'function' ? raw() : (raw || []);
  });

  readonly filteredItems = computed<TItem[]>(() => {
    const query = this.searchTerm().trim().toLowerCase();
    const items = this.allItems();
    if (!query) return items;

    if (this.config.filterFn) {
      return items.filter(it => this.config.filterFn!(it, query));
    }

    return items.filter(it => {
      return Object.values(it as any).some(val =>
        val !== null && val !== undefined && String(val).toLowerCase().includes(query)
      );
    });
  });

  onSearchChange(val: string): void {
    this.searchTerm.set(val);
    this.activeIndex.set(-1);
    this.isOpen.set(true);
  }

  clearSearch(event: Event): void {
    event.preventDefault();
    event.stopPropagation();
    this.searchTerm.set('');
    this.activeIndex.set(-1);
    this.isOpen.set(true);
    this.lookupInputRef?.nativeElement.focus();
  }

  onTriggerClick(event: Event): void {
    event.preventDefault();
    event.stopPropagation();
    this.isOpen.set(!this.isOpen());
  }

  selectItem(item: TItem, event?: Event): void {
    if (event) {
      event.preventDefault();
      event.stopPropagation();
    }
    const val = (item as any)[this.config.valueField];
    this.value = val;
    this.searchTerm.set(val);
    this.valueChange.emit(val);
    this.itemSelect.emit(item);
    if (this.config.onSelect) {
      this.config.onSelect(item, null);
    }
    this.isOpen.set(false);
    this.commit.emit();
  }

  openFullDialog(event: Event): void {
    event.preventDefault();
    event.stopPropagation();
    this.isOpen.set(false);

    const dialogRef = this.dialog.open(LookupDialogComponent, {
      data: {
        config: this.config,
        initialQuery: this.searchTerm()
      },
      width: '720px',
      autoFocus: true,
      restoreFocus: true
    });

    dialogRef.afterClosed().subscribe((selected: TItem | null) => {
      if (selected) {
        this.selectItem(selected);
      } else {
        this.lookupInputRef?.nativeElement.focus();
      }
    });
  }

  onKeyDown(event: KeyboardEvent): void {
    if (event.key === 'ArrowDown') {
      event.preventDefault();
      if (!this.isOpen()) {
        this.isOpen.set(true);
      }
      const count = this.filteredItems().length;
      if (count > 0) {
        this.activeIndex.set((this.activeIndex() + 1) % count);
        this.scrollToHighlighted();
      }
    } else if (event.key === 'ArrowUp') {
      event.preventDefault();
      if (!this.isOpen()) {
        this.isOpen.set(true);
      }
      const count = this.filteredItems().length;
      if (count > 0) {
        this.activeIndex.set(this.activeIndex() <= 0 ? count - 1 : this.activeIndex() - 1);
        this.scrollToHighlighted();
      }
    } else if (event.key === 'Enter') {
      event.preventDefault();
      const idx = this.activeIndex();
      const items = this.filteredItems();
      if (this.isOpen() && idx >= 0 && idx < items.length) {
        this.selectItem(items[idx]);
      } else {
        this.isOpen.set(false);
        this.commit.emit();
      }
    } else if (event.key === 'Tab') {
      const idx = this.activeIndex();
      const items = this.filteredItems();
      if (this.isOpen() && idx >= 0 && idx < items.length) {
        const item = items[idx];
        const val = (item as any)[this.config.valueField];
        this.value = val;
        this.searchTerm.set(val);
        this.valueChange.emit(val);
        this.itemSelect.emit(item);
        if (this.config.onSelect) {
          this.config.onSelect(item, null);
        }
      }
      this.isOpen.set(false);
      this.tab.emit(event);
    } else if (event.key === 'Escape') {
      event.preventDefault();
      if (this.isOpen()) {
        this.isOpen.set(false);
      } else {
        this.cancel.emit();
      }
    } else if (event.key === 'F4') {
      event.preventDefault();
      this.openFullDialog(event);
    }
  }

  private scrollToHighlighted(): void {
    setTimeout(() => {
      const panel = this.resultsPanelRef?.nativeElement;
      if (!panel) return;
      const highlighted = panel.querySelector('.lookup-result-row.is-highlighted') as HTMLElement;
      if (highlighted) {
        highlighted.scrollIntoView({ block: 'nearest' });
      }
    }, 0);
  }

  onBlur(): void {
    this.isInputFocused.set(false);
    setTimeout(() => {
      if (!this.isInputFocused()) {
        this.isOpen.set(false);
        this.commit.emit();
      }
    }, 200);
  }
}
