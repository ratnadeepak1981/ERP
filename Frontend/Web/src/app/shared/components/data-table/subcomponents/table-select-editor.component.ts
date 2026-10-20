import {
  Component,
  Input,
  Output,
  EventEmitter,
  signal,
  computed,
  ViewChild,
  ElementRef,
  AfterViewInit,
  OnInit,
  ChangeDetectionStrategy
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { SelectOption } from '../../../models/data-table-config.model';

@Component({
  selector: 'app-table-select-editor',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div #container class="table-select-editor-container" (click)="$event.stopPropagation()">
      <div class="select-search-box">
        <input
          #searchInput
          type="text"
          class="select-search-input"
          [placeholder]="placeholder"
          [value]="searchTerm()"
          (input)="onSearchInput($any($event.target).value)"
          (keydown)="onInputKeyDown($event)"
          aria-label="Search and select option"
          autocomplete="off"
        />
        @if (searchTerm()) {
          <button
            type="button"
            class="clear-btn"
            aria-label="Clear search text"
            (click)="clearSearch($event)"
          >
            <i class="pi pi-times"></i>
          </button>
        }
        <i
          class="pi select-chevron"
          [class.pi-chevron-down]="!isOpen()"
          [class.pi-chevron-up]="isOpen()"
          (click)="toggleOpen($event)"
          aria-hidden="true"
        ></i>
      </div>

      @if (isOpen()) {
        <div
          #optionsPanel
          class="select-options-panel"
          role="listbox"
          [style.minWidth]="panelMinWidth()"
          [style.maxWidth]="panelMaxWidth()"
        >
          @if (filteredOptions().length === 0) {
            <div class="empty-option-item">
              <i class="pi pi-info-circle"></i>
              <span>No matching options</span>
            </div>
          } @else {
            @for (opt of filteredOptions(); track opt.value; let i = $index) {
              <div
                class="select-option-row"
                role="option"
                [attr.aria-selected]="opt.value === value"
                [class.is-highlighted]="i === activeIndex()"
                [class.is-selected]="opt.value === value"
                (mousedown)="selectOption(opt, $event)"
                (mouseenter)="activeIndex.set(i)"
              >
                <div class="option-details">
                  <span class="option-text">{{ opt.label }}</span>
                  @if (opt.subtitle) {
                    <span class="option-subtitle">{{ opt.subtitle }}</span>
                  }
                </div>
                @if (opt.badge) {
                  <span class="option-badge">{{ opt.badge }}</span>
                }
                @if (opt.value === value) {
                  <i class="pi pi-check selected-check"></i>
                }
              </div>
            }
          }
        </div>
      }
    </div>
  `,
  styleUrls: ['./table-select-editor.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class TableSelectEditorComponent implements OnInit, AfterViewInit {
  @Input() options: SelectOption[] = [];
  @Input() value: any = null;
  @Input() placeholder: string = 'Select...';
  @Input() allowFreeText: boolean = false;
  @Input() popupMaxWidth?: string;

  @Output() valueChange = new EventEmitter<any>();
  @Output() selectOptionEvent = new EventEmitter<SelectOption>();
  @Output() commit = new EventEmitter<any>();
  @Output() cancel = new EventEmitter<void>();
  @Output() tabOut = new EventEmitter<{ shiftKey: boolean; selectedOption?: SelectOption }>();

  @ViewChild('container') containerRef?: ElementRef<HTMLElement>;
  @ViewChild('searchInput') searchInputRef?: ElementRef<HTMLInputElement>;
  @ViewChild('optionsPanel') optionsPanelRef?: ElementRef<HTMLElement>;

  readonly isOpen = signal<boolean>(true);
  readonly searchTerm = signal<string>('');
  readonly activeIndex = signal<number>(-1);
  readonly panelMinWidth = signal<string>('240px');
  readonly panelMaxWidth = signal<string>('540px');

  readonly filteredOptions = computed<SelectOption[]>(() => {
    const opts = this.options || [];
    const query = this.searchTerm().trim().toLowerCase();
    if (!query) return opts;
    return opts.filter(o =>
      (o.label || '').toLowerCase().includes(query) ||
      (o.subtitle || '').toLowerCase().includes(query) ||
      (o.badge || '').toLowerCase().includes(query) ||
      String(o.value || '').toLowerCase().includes(query) ||
      (o.rawItem?.code && String(o.rawItem.code).toLowerCase().includes(query)) ||
      (o.rawItem?.name && String(o.rawItem.name).toLowerCase().includes(query)) ||
      (o.rawItem?.description && String(o.rawItem.description).toLowerCase().includes(query))
    );
  });

  ngOnInit(): void {
    if (this.value != null && this.options?.length > 0) {
      const matchIdx = this.options.findIndex(o => o.value === this.value);
      if (matchIdx >= 0) {
        this.activeIndex.set(matchIdx);
      } else {
        this.activeIndex.set(-1);
      }
    } else {
      this.activeIndex.set(-1);
    }
  }

  ngAfterViewInit(): void {
    setTimeout(() => {
      this.searchInputRef?.nativeElement.focus();
      this.adjustPosition();
      this.scrollToHighlighted();
    }, 15);
  }

  toggleOpen(event?: Event): void {
    if (event) {
      event.stopPropagation();
      event.preventDefault();
    }
    const next = !this.isOpen();
    this.isOpen.set(next);
    if (next) {
      setTimeout(() => this.adjustPosition(), 0);
    }
  }

  adjustPosition(): void {
    if (!this.containerRef?.nativeElement) return;
    const container = this.containerRef.nativeElement;
    const rect = container.getBoundingClientRect();
    const viewportWidth = window.innerWidth || document.documentElement.clientWidth;
    const viewportHeight = window.innerHeight || document.documentElement.clientHeight;

    // Minimum width is at least container/cell width
    const cellWidth = Math.max(Math.round(rect.width), 220);
    this.panelMinWidth.set(`${cellWidth}px`);

    const maxW = this.popupMaxWidth || `min(540px, calc(100vw - 32px))`;
    this.panelMaxWidth.set(maxW);

    setTimeout(() => {
      const panel = this.optionsPanelRef?.nativeElement;
      if (!panel) return;

      // Horizontal boundary adjustment (avoid right overflow)
      const panelWidth = panel.offsetWidth || cellWidth;
      if (rect.left + panelWidth > viewportWidth - 16) {
        panel.style.left = 'auto';
        panel.style.right = '0';
      } else {
        panel.style.left = '0';
        panel.style.right = 'auto';
      }

      // Vertical boundary adjustment (flip upwards if bottom clipped)
      const panelHeight = panel.offsetHeight || 220;
      const spaceBelow = viewportHeight - rect.bottom;
      const spaceAbove = rect.top;
      if (spaceBelow < 220 && spaceAbove > spaceBelow) {
        panel.style.top = 'auto';
        panel.style.bottom = 'calc(100% + 4px)';
      } else {
        panel.style.top = 'calc(100% + 4px)';
        panel.style.bottom = 'auto';
      }
    }, 0);
  }

  scrollToHighlighted(): void {
    setTimeout(() => {
      const panel = this.optionsPanelRef?.nativeElement;
      if (!panel) return;
      const highlighted = panel.querySelector('.select-option-row.is-highlighted') as HTMLElement;
      if (highlighted) {
        const panelTop = panel.scrollTop;
        const panelBottom = panelTop + panel.clientHeight;
        const elemTop = highlighted.offsetTop;
        const elemBottom = elemTop + highlighted.offsetHeight;
        if (elemTop < panelTop) {
          panel.scrollTop = elemTop;
        } else if (elemBottom > panelBottom) {
          panel.scrollTop = elemBottom - panel.clientHeight;
        }
      }
    }, 0);
  }

  onSearchInput(text: string): void {
    this.searchTerm.set(text);
    // Distinct state: typing filters options without arbitrarily committing or highlighting
    this.activeIndex.set(-1);
    this.isOpen.set(true);
    this.adjustPosition();
  }

  clearSearch(event?: Event): void {
    if (event) {
      event.stopPropagation();
      event.preventDefault();
    }
    this.searchTerm.set('');
    this.activeIndex.set(-1);
    this.isOpen.set(true);
    this.searchInputRef?.nativeElement.focus();
    this.adjustPosition();
  }

  selectOption(opt: SelectOption, event?: Event): void {
    if (event) {
      event.stopPropagation();
      event.preventDefault();
    }
    this.value = opt.value;
    this.valueChange.emit(opt.value);
    this.selectOptionEvent.emit(opt);
    this.isOpen.set(false);
    this.commit.emit(opt.value);
  }

  onInputKeyDown(event: KeyboardEvent): void {
    if (event.key === 'ArrowDown') {
      event.preventDefault();
      event.stopPropagation();
      if (!this.isOpen()) {
        this.isOpen.set(true);
        this.adjustPosition();
      }
      const count = this.filteredOptions().length;
      if (count > 0) {
        const next = this.activeIndex() < 0 ? 0 : (this.activeIndex() + 1) % count;
        this.activeIndex.set(next);
        this.scrollToHighlighted();
      }
    } else if (event.key === 'ArrowUp') {
      event.preventDefault();
      event.stopPropagation();
      if (!this.isOpen()) {
        this.isOpen.set(true);
        this.adjustPosition();
      }
      const count = this.filteredOptions().length;
      if (count > 0) {
        const next = this.activeIndex() < 0 ? count - 1 : (this.activeIndex() - 1 + count) % count;
        this.activeIndex.set(next);
        this.scrollToHighlighted();
      }
    } else if (event.key === 'Enter') {
      event.preventDefault();
      event.stopPropagation();
      const items = this.filteredOptions();
      const idx = this.activeIndex();
      if (this.isOpen() && idx >= 0 && idx < items.length) {
        this.selectOption(items[idx]);
      } else if (this.allowFreeText && this.searchTerm().trim()) {
        const freeVal = this.searchTerm().trim();
        this.value = freeVal;
        this.valueChange.emit(freeVal);
        this.isOpen.set(false);
        this.commit.emit(freeVal);
      } else {
        // No highlighted option: do not arbitrarily select first option
        this.isOpen.set(false);
        this.commit.emit(this.value);
      }
    } else if (event.key === 'Escape') {
      event.preventDefault();
      event.stopPropagation();
      if (this.isOpen()) {
        // First Escape: close popup without changing selection
        this.isOpen.set(false);
      } else {
        // Second Escape (or when closed): cancel editing
        this.cancel.emit();
      }
    } else if (event.key === 'Tab') {
      event.preventDefault();
      event.stopPropagation();
      const items = this.filteredOptions();
      let selectedOpt: SelectOption | undefined;
      const idx = this.activeIndex();
      if (this.isOpen() && idx >= 0 && idx < items.length) {
        selectedOpt = items[idx];
        this.value = selectedOpt.value;
        this.valueChange.emit(selectedOpt.value);
      } else if (this.allowFreeText && this.searchTerm().trim()) {
        const freeVal = this.searchTerm().trim();
        this.value = freeVal;
        this.valueChange.emit(freeVal);
      }
      this.isOpen.set(false);
      this.tabOut.emit({ shiftKey: event.shiftKey, selectedOption: selectedOpt });
    } else {
      event.stopPropagation();
    }
  }
}
