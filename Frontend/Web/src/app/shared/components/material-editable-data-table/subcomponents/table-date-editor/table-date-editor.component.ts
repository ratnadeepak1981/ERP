import {
  Component,
  Input,
  Output,
  EventEmitter,
  ElementRef,
  ViewChild,
  AfterViewInit,
  ChangeDetectionStrategy
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'app-mat-table-date-editor',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <input
      #dateInput
      type="date"
      class="mat-cell-editor-input mat-cell-date-input"
      [ngModel]="formattedValue"
      (ngModelChange)="onDateChange($event)"
      (keydown)="onKeyDown($event)"
      (blur)="onBlur()"
      [min]="minDate || '1900-01-01'"
      [max]="maxDate || '2199-12-31'"
      autocomplete="off"
    />
  `,
  styles: [`
    :host {
      display: block;
      width: 100%;
      height: 100%;
    }
    .mat-cell-editor-input {
      width: 100%;
      height: var(--fluent-row-height, 36px);
      padding: 0 8px;
      font-family: inherit;
      font-size: 13px;
      color: inherit;
      border: 1px solid var(--fluent-primary, #0f6cbd);
      outline: none;
      box-sizing: border-box;
      background: #ffffff;
      border-radius: var(--erp-radius-sm, 4px);
    }
    .mat-cell-date-input {
      cursor: pointer;
    }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class TableDateEditorComponent implements AfterViewInit {
  @Input() value: any = '';
  @Input() minDate?: string;
  @Input() maxDate?: string;

  @Output() valueChange = new EventEmitter<string>();
  @Output() commit = new EventEmitter<void>();
  @Output() cancel = new EventEmitter<void>();
  @Output() tab = new EventEmitter<KeyboardEvent>();

  @ViewChild('dateInput') dateInputRef!: ElementRef<HTMLInputElement>;
  private isCancelled = false;

  get formattedValue(): string {
    if (!this.value) return '';
    if (typeof this.value === 'string') {
      // If ISO string like 2026-11-15T... extract YYYY-MM-DD
      if (this.value.length >= 10 && this.value[4] === '-' && this.value[7] === '-') {
        return this.value.substring(0, 10);
      }
      const d = new Date(this.value);
      if (!isNaN(d.getTime())) {
        return d.toISOString().substring(0, 10);
      }
    } else if (this.value instanceof Date && !isNaN(this.value.getTime())) {
      return this.value.toISOString().substring(0, 10);
    }
    return '';
  }

  ngAfterViewInit(): void {
    queueMicrotask(() => {
      const input = this.dateInputRef?.nativeElement;
      if (input) {
        input.focus();
      }
    });
  }

  onDateChange(newVal: string): void {
    this.valueChange.emit(newVal || '');
  }

  onKeyDown(event: KeyboardEvent): void {
    if (event.key === 'Enter') {
      event.preventDefault();
      event.stopPropagation();
      this.commit.emit();
    } else if (event.key === 'Escape') {
      event.preventDefault();
      event.stopPropagation();
      this.isCancelled = true;
      this.cancel.emit();
    } else if (event.key === 'Tab') {
      event.preventDefault();
      event.stopPropagation();
      this.tab.emit(event);
    }
  }

  onBlur(): void {
    if (!this.isCancelled) {
      this.commit.emit();
    }
  }
}
