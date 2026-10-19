import {
  Component,
  Input,
  Output,
  EventEmitter,
  forwardRef,
  ChangeDetectionStrategy,
  computed,
  signal,
  booleanAttribute
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { ControlValueAccessor, NG_VALUE_ACCESSOR, FormsModule } from '@angular/forms';
import {
  CheckboxShape,
  CheckboxSize,
  CheckboxColorToken,
  ApexCheckboxConfig
} from '../../models/checkbox-config.model';

@Component({
  selector: 'app-checkbox',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './checkbox.component.html',
  styleUrl: './checkbox.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => AppCheckboxComponent),
      multi: true
    }
  ]
})
export class AppCheckboxComponent implements ControlValueAccessor {
  // Config object input for bulk configuration
  @Input() set config(cfg: ApexCheckboxConfig | undefined) {
    if (!cfg) return;
    if (cfg.shape !== undefined) this.shape = cfg.shape;
    if (cfg.size !== undefined) this.size = cfg.size;
    if (cfg.colorToken !== undefined) this.colorToken = cfg.colorToken;
    if (cfg.disabled !== undefined) this.disabled = cfg.disabled;
    if (cfg.readonly !== undefined) this.readonly = cfg.readonly;
    if (cfg.indeterminate !== undefined) this.indeterminate = cfg.indeterminate;
    if (cfg.label !== undefined) this.label = cfg.label;
    if (cfg.ariaLabel !== undefined) this.ariaLabel = cfg.ariaLabel;
    if (cfg.error !== undefined) this.error = cfg.error;
  }

  // Individual inputs
  @Input() shape: CheckboxShape = 'rounded-square';
  @Input() size: CheckboxSize = 'standard';
  @Input() colorToken: CheckboxColorToken = 'primary';
  @Input({ transform: booleanAttribute }) disabled: boolean = false;
  @Input({ transform: booleanAttribute }) readonly: boolean = false;
  @Input() label?: string;
  @Input() ariaLabel?: string;
  @Input() error?: boolean | string;

  // Checked state
  private readonly _checked = signal<boolean>(false);
  @Input() set checked(val: boolean) {
    this._checked.set(!!val);
  }
  get checked(): boolean {
    return this._checked();
  }

  // Indeterminate state
  private readonly _indeterminate = signal<boolean>(false);
  @Input({ transform: booleanAttribute }) set indeterminate(val: boolean) {
    this._indeterminate.set(!!val);
  }
  get indeterminate(): boolean {
    return this._indeterminate();
  }

  // Outputs
  @Output() checkedChange = new EventEmitter<boolean>();
  @Output() indeterminateChange = new EventEmitter<boolean>();

  // ControlValueAccessor callbacks
  private onChange: (val: boolean) => void = () => {};
  private onTouched: () => void = () => {};

  readonly isChecked = computed(() => this._checked());
  readonly isIndeterminate = computed(() => this._indeterminate());

  writeValue(value: any): void {
    this._checked.set(!!value);
  }

  registerOnChange(fn: any): void {
    this.onChange = fn;
  }

  registerOnTouched(fn: any): void {
    this.onTouched = fn;
  }

  setDisabledState(isDisabled: boolean): void {
    this.disabled = isDisabled;
  }

  toggle(event?: Event): void {
    if (event) {
      event.stopPropagation();
    }
    if (this.disabled || this.readonly) {
      return;
    }

    if (this._indeterminate()) {
      // Transition from indeterminate to checked
      this._indeterminate.set(false);
      this._checked.set(true);
      this.indeterminateChange.emit(false);
      this.checkedChange.emit(true);
      this.onChange(true);
    } else {
      const next = !this._checked();
      this._checked.set(next);
      this.checkedChange.emit(next);
      this.onChange(next);
    }

    this.onTouched();
  }

  onKeyDown(event: KeyboardEvent): void {
    if (event.key === ' ' || event.key === 'Enter') {
      event.preventDefault();
      this.toggle(event);
    }
  }

  get computedAriaLabel(): string {
    if (this.ariaLabel) return this.ariaLabel;
    if (this.label) return this.label;
    return 'Checkbox';
  }
}

export { AppCheckboxComponent as CheckboxComponent };
