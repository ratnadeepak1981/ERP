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
  selector: 'app-mat-table-text-editor',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <input
      #textInput
      type="text"
      class="mat-cell-editor-input"
      [ngModel]="value"
      (ngModelChange)="onValueChange($event)"
      (keydown)="onKeyDown($event)"
      (blur)="onBlur()"
      [placeholder]="placeholder || ''"
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
    }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class TableTextEditorComponent implements AfterViewInit {
  @Input() value: string = '';
  @Input() placeholder?: string;

  @Output() valueChange = new EventEmitter<string>();
  @Output() commit = new EventEmitter<void>();
  @Output() cancel = new EventEmitter<void>();
  @Output() tab = new EventEmitter<KeyboardEvent>();

  @ViewChild('textInput') textInputRef!: ElementRef<HTMLInputElement>;
  private isCancelled = false;

  ngAfterViewInit(): void {
    queueMicrotask(() => {
      const input = this.textInputRef?.nativeElement;
      if (input) {
        input.focus();
        input.select();
      }
    });
  }

  onValueChange(val: string): void {
    this.value = val;
    this.valueChange.emit(val);
  }

  onKeyDown(event: KeyboardEvent): void {
    if (event.key === 'Tab') {
      this.tab.emit(event);
    } else if (event.key === 'Enter') {
      event.preventDefault();
      this.commit.emit();
    } else if (event.key === 'Escape') {
      event.preventDefault();
      event.stopPropagation();
      this.isCancelled = true;
      this.cancel.emit();
    }
  }

  onBlur(): void {
    if (this.isCancelled) return;
    this.commit.emit();
  }
}
