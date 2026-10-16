import {
  Component,
  Input,
  Output,
  EventEmitter,
  forwardRef,
  signal,
  computed,
  ElementRef,
  HostListener,
  ChangeDetectionStrategy,
  ViewChild,
  OnInit,
  AfterViewInit
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';
import { LookupItem } from './lookup-item.model';

@Component({
  selector: 'app-lookup-dropdown',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './lookup-dropdown.component.html',
  styleUrl: './lookup-dropdown.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => AppLookupDropdownComponent),
      multi: true
    }
  ]
})
export class AppLookupDropdownComponent implements ControlValueAccessor, OnInit, AfterViewInit {
  @Input() items: LookupItem[] = [];
  @Input() placeholder: string = 'Select item...';
  @Input() searchPlaceholder: string = 'Search by code or name...';
  @Input() categoryFilter: string | null = null;
  @Input() onlyEligible: boolean = false;
  @Input() disabled: boolean = false;
  @Input() showClear: boolean = true;
  @Input() size: 'small' | 'normal' = 'small';
  @Input() ariaLabel: string = 'Lookup selector';
  @Input() autoOpen: boolean = false;

  @Output() selectItem = new EventEmitter<LookupItem>();
  @Output() valueChange = new EventEmitter<any>();
  @Output() clear = new EventEmitter<void>();

  @ViewChild('searchInput') searchInputRef?: ElementRef<HTMLInputElement>;
  @ViewChild('triggerButton') triggerButtonRef?: ElementRef<HTMLButtonElement>;

  readonly isOpen = signal<boolean>(false);
  readonly searchTerm = signal<string>('');
  readonly activeIndex = signal<number>(0);
  readonly selectedValue = signal<any>(null);

  // ControlValueAccessor callbacks
  private onChange: (value: any) => void = () => {};
  private onTouched: () => void = () => {};

  constructor(private hostEl: ElementRef) {}

  ngOnInit(): void {
    if (this.autoOpen) {
      this.open();
    }
  }

  ngAfterViewInit(): void {
    if (this.autoOpen && this.isOpen()) {
      setTimeout(() => this.searchInputRef?.nativeElement.focus(), 15);
    }
  }

  // Filtered lookup items
  readonly filteredItems = computed(() => {
    let result = this.items || [];

    // Category filter
    if (this.categoryFilter && this.categoryFilter !== 'ALL') {
      const cat = this.categoryFilter.toLowerCase();
      result = result.filter(item => (item.category || '').toLowerCase() === cat);
    }

    // Eligibility filter
    if (this.onlyEligible) {
      result = result.filter(item => item.isEligible !== false);
    }

    // Text search
    const query = this.searchTerm().trim().toLowerCase();
    if (query) {
      result = result.filter(item => {
        const code = (item.code || '').toLowerCase();
        const name = (item.name || '').toLowerCase();
        const desc = (item.description || '').toLowerCase();
        return code.includes(query) || name.includes(query) || desc.includes(query);
      });
    }

    return result;
  });

  // Currently selected item object
  readonly selectedItem = computed(() => {
    const val = this.selectedValue();
    if (val == null || val === '') return null;
    return this.items.find(item => item.id === val || item.code === val || item.name === val) || null;
  });

  // Display label for closed input trigger
  readonly displayText = computed(() => {
    const item = this.selectedItem();
    if (item) {
      return `${item.code} — ${item.name}`;
    }
    const rawVal = this.selectedValue();
    return rawVal ? String(rawVal) : '';
  });

  writeValue(value: any): void {
    this.selectedValue.set(value);
  }

  registerOnChange(fn: any): void {
    this.onChange = fn;
  }

  registerOnTouched(fn: any): void {
    this.onTouched = fn;
  }

  setDisabledState(isDisabled: boolean): void {
    this.disabled = isDisabled;
    if (isDisabled) {
      this.close();
    }
  }

  toggleOpen(): void {
    if (this.disabled) return;
    if (this.isOpen()) {
      this.close();
    } else {
      this.open();
    }
  }

  onTriggerFocus(): void {
    if (this.autoOpen && !this.isOpen()) {
      this.open();
    }
  }

  open(): void {
    if (this.disabled || this.isOpen()) return;
    this.isOpen.set(true);
    this.searchTerm.set('');
    this.activeIndex.set(0);

    setTimeout(() => {
      this.searchInputRef?.nativeElement.focus();
    }, 15);
  }

  close(): void {
    if (!this.isOpen()) return;
    this.isOpen.set(false);
    this.searchTerm.set('');
    this.activeIndex.set(0);
    this.onTouched();
  }

  select(item: LookupItem, event?: Event): void {
    if (event) {
      event.stopPropagation();
      event.preventDefault();
    }
    this.selectedValue.set(item.code || item.id);
    this.onChange(item.code || item.id);
    this.valueChange.emit(item.code || item.id);
    this.selectItem.emit(item);
    this.close();
    setTimeout(() => {
      this.triggerButtonRef?.nativeElement.focus();
    }, 0);
  }

  onClear(event: Event): void {
    event.stopPropagation();
    event.preventDefault();
    this.selectedValue.set(null);
    this.onChange(null);
    this.valueChange.emit(null);
    this.clear.emit();
  }

  onSearchChange(term: string): void {
    this.searchTerm.set(term);
    this.activeIndex.set(0);
  }

  onSearchInputKeyDown(event: KeyboardEvent): void {
    if (event.key === 'Tab') {
      const items = this.filteredItems();
      if (items.length > 0) {
        const idx = this.activeIndex() >= 0 && this.activeIndex() < items.length ? this.activeIndex() : 0;
        const itemToSelect = items[idx];
        this.selectedValue.set(itemToSelect.code || itemToSelect.id);
        this.onChange(itemToSelect.code || itemToSelect.id);
        this.valueChange.emit(itemToSelect.code || itemToSelect.id);
        this.selectItem.emit(itemToSelect);
      }
      this.close();
      // Tab bubbles naturally to move to the next cell
    } else if (event.key === 'ArrowDown') {
      event.preventDefault();
      const count = this.filteredItems().length;
      if (count > 0) {
        this.activeIndex.set((this.activeIndex() + 1) % count);
      }
    } else if (event.key === 'ArrowUp') {
      event.preventDefault();
      const count = this.filteredItems().length;
      if (count > 0) {
        this.activeIndex.set((this.activeIndex() - 1 + count) % count);
      }
    } else if (event.key === 'Enter') {
      event.preventDefault();
      const items = this.filteredItems();
      if (items.length > 0) {
        const idx = this.activeIndex() >= 0 && this.activeIndex() < items.length ? this.activeIndex() : 0;
        this.select(items[idx]);
      }
    } else if (event.key === 'Escape') {
      event.preventDefault();
      this.close();
      this.triggerButtonRef?.nativeElement.focus();
    }
  }

  onKeyDown(event: KeyboardEvent): void {
    if (!this.isOpen()) {
      if (event.key === 'ArrowDown' || event.key === 'Enter' || event.key === ' ') {
        event.preventDefault();
        this.open();
      }
      return;
    }
  }

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent): void {
    if (!this.isOpen()) return;
    const clickedInside = this.hostEl.nativeElement.contains(event.target);
    if (!clickedInside) {
      this.close();
    }
  }
}
