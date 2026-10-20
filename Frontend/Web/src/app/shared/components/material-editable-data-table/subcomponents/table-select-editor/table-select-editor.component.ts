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
  ChangeDetectionStrategy
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MaterialSelectOption } from '../../models/material-data-table.models';

@Component({
  selector: 'app-mat-table-select-editor',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="mat-select-editor-wrapper">
      <div class="mat-select-display-box" (click)="toggleDropdown()">
        <span class="selected-text">{{ getSelectedLabel() }}</span>
        <span class="chevron-icon">{{ isOpen() ? '▲' : '▼' }}</span>
      </div>

      @if (isOpen()) {
        <div class="mat-select-options-panel" role="listbox">
          @for (opt of options; track opt.value; let i = $index) {
            <div
              class="mat-select-option"
              role="option"
              [class.is-highlighted]="i === activeIndex()"
              [class.is-selected]="opt.value === value"
              (mousedown)="selectOption(opt, $event)"
              (mouseenter)="activeIndex.set(i)"
            >
              <span>{{ opt.label }}</span>
              @if (opt.subtitle) {
                <span class="option-subtitle">{{ opt.subtitle }}</span>
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
    .mat-select-editor-wrapper {
      width: 100%;
      height: var(--fluent-row-height, 36px);
      position: relative;
    }
    .mat-select-display-box {
      width: 100%;
      height: 100%;
      display: flex;
      align-items: center;
      justify-content: space-between;
      padding: 0 8px;
      border: 1px solid var(--fluent-primary, #0078d4);
      background: #ffffff;
      box-sizing: border-box;
      cursor: pointer;
      user-select: none;
      font-size: 13px;
    }
    .chevron-icon {
      font-size: 9px;
      color: #605e5c;
    }
    .mat-select-options-panel {
      position: absolute;
      top: 100%;
      left: 0;
      min-width: 100%;
      max-height: 200px;
      overflow-y: auto;
      background: #ffffff;
      border: 1px solid #edebe9;
      box-shadow: 0 4px 16px rgba(0,0,0,0.14);
      z-index: 100;
      border-radius: 2px;
    }
    .mat-select-option {
      padding: 6px 10px;
      font-size: 13px;
      cursor: pointer;
      display: flex;
      flex-direction: column;
      &:hover, &.is-highlighted {
        background: #f3f2f1;
      }
      &.is-selected {
        background: #eff6fc;
        color: #0078d4;
        font-weight: 600;
      }
    }
    .option-subtitle {
      font-size: 11px;
      color: #605e5c;
    }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class TableSelectEditorComponent implements AfterViewInit {
  @Input() value: any;
  @Input() options: MaterialSelectOption[] = [];
  @Input() placeholder?: string;

  @Output() valueChange = new EventEmitter<any>();
  @Output() selectOptionEvent = new EventEmitter<MaterialSelectOption>();
  @Output() commit = new EventEmitter<void>();
  @Output() cancel = new EventEmitter<void>();
  @Output() tab = new EventEmitter<KeyboardEvent>();

  readonly isOpen = signal<boolean>(true);
  readonly activeIndex = signal<number>(-1);

  ngAfterViewInit(): void {
    const currentIdx = this.options.findIndex(o => o.value === this.value);
    this.activeIndex.set(currentIdx >= 0 ? currentIdx : -1);
  }

  getSelectedLabel(): string {
    const match = this.options.find(o => o.value === this.value);
    return match ? match.label : (this.placeholder || 'Select...');
  }

  toggleDropdown(): void {
    this.isOpen.set(!this.isOpen());
  }

  selectOption(opt: MaterialSelectOption, event?: Event): void {
    if (event) {
      event.preventDefault();
      event.stopPropagation();
    }
    this.value = opt.value;
    this.valueChange.emit(opt.value);
    this.selectOptionEvent.emit(opt);
    this.isOpen.set(false);
    this.commit.emit();
  }

  onKeyDown(event: KeyboardEvent): void {
    if (event.key === 'ArrowDown') {
      event.preventDefault();
      const count = this.options.length;
      if (count > 0) {
        this.activeIndex.set((this.activeIndex() + 1) % count);
      }
    } else if (event.key === 'ArrowUp') {
      event.preventDefault();
      const count = this.options.length;
      if (count > 0) {
        this.activeIndex.set(this.activeIndex() <= 0 ? count - 1 : this.activeIndex() - 1);
      }
    } else if (event.key === 'Enter') {
      event.preventDefault();
      const idx = this.activeIndex();
      if (idx >= 0 && idx < this.options.length) {
        this.selectOption(this.options[idx]);
      } else {
        this.commit.emit();
      }
    } else if (event.key === 'Tab') {
      const idx = this.activeIndex();
      if (this.isOpen() && idx >= 0 && idx < this.options.length) {
        this.selectOption(this.options[idx]);
      }
      this.tab.emit(event);
    } else if (event.key === 'Escape') {
      event.preventDefault();
      if (this.isOpen()) {
        this.isOpen.set(false);
      } else {
        this.cancel.emit();
      }
    }
  }
}
