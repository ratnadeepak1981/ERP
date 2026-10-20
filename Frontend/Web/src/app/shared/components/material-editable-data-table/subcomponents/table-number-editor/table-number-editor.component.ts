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
  selector: 'app-mat-table-number-editor',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <input
      #numInput
      type="text"
      inputmode="decimal"
      class="mat-cell-editor-input align-right"
      [ngModel]="rawStringValue"
      (ngModelChange)="onRawChange($event)"
      (keydown)="onKeyDown($event)"
      (blur)="onBlur()"
      [placeholder]="placeholder || '0'"
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
      border: 1px solid var(--fluent-primary, #0078d4);
      outline: none;
      box-sizing: border-box;
      background: #ffffff;
      text-align: right;
    }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class TableNumberEditorComponent implements AfterViewInit {
  @Input() set value(val: number | null | undefined) {
    if (val === null || val === undefined) {
      this.rawStringValue = '';
    } else {
      this.rawStringValue = String(val);
    }
  }
  @Input() step?: number | string;
  @Input() min?: number;
  @Input() max?: number;
  @Input() placeholder?: string;

  @Output() valueChange = new EventEmitter<number | null>();
  @Output() commit = new EventEmitter<void>();
  @Output() cancel = new EventEmitter<void>();
  @Output() tab = new EventEmitter<KeyboardEvent>();

  @ViewChild('numInput') numInputRef!: ElementRef<HTMLInputElement>;

  rawStringValue: string = '';
  private isCancelled = false;

  ngAfterViewInit(): void {
    queueMicrotask(() => {
      const input = this.numInputRef?.nativeElement;
      if (input) {
        input.focus();
        input.select();
      }
    });
  }

  onRawChange(str: string): void {
    this.rawStringValue = str;

    if (str.trim() === '') {
      this.valueChange.emit(null);
      return;
    }

    // Allow intermediate typing (e.g. "2." or "-") without emitting NaN
    if (str === '-' || str === '.' || str.endsWith('.')) {
      return;
    }

    const parsed = Number(str);
    if (!isNaN(parsed)) {
      this.valueChange.emit(parsed);
    }
  }

  onKeyDown(event: KeyboardEvent): void {
    if (event.key === 'Tab') {
      this.commitValue();
      this.tab.emit(event);
    } else if (event.key === 'Enter') {
      event.preventDefault();
      this.commitValue();
      this.commit.emit();
    } else if (event.key === 'Escape') {
      event.preventDefault();
      event.stopPropagation();
      this.isCancelled = true;
      this.cancel.emit();
    }
  }

  private commitValue(): void {
    const trimmed = this.rawStringValue.trim();
    if (trimmed === '') {
      this.valueChange.emit(null);
    } else {
      const parsed = Number(trimmed);
      this.valueChange.emit(isNaN(parsed) ? null : parsed);
    }
  }

  onBlur(): void {
    if (this.isCancelled) return;
    this.commitValue();
    this.commit.emit();
  }
}
