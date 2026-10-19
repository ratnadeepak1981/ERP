import {
  Component,
  Input,
  Output,
  EventEmitter,
  signal,
  computed,
  ChangeDetectionStrategy
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormGroup, ReactiveFormsModule } from '@angular/forms';
import { InputTextModule } from 'primeng/inputtext';
import { TooltipModule } from 'primeng/tooltip';
import { FormFieldConfig } from './form-field-config.model';

@Component({
  selector: 'app-reactive-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, InputTextModule, TooltipModule],
  templateUrl: './reactive-form.component.html',
  styleUrl: './reactive-form.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class AppReactiveFormComponent {
  private readonly _form = signal<FormGroup | null>(null);
  @Input({ required: true })
  set form(val: FormGroup) {
    this._form.set(val);
  }
  get form(): FormGroup {
    return this._form()!;
  }

  private readonly _fields = signal<FormFieldConfig[]>([]);
  @Input({ required: true })
  set fields(val: FormFieldConfig[]) {
    this._fields.set(val || []);
  }
  get fields(): FormFieldConfig[] {
    return this._fields();
  }

  readonly _readonly = signal<boolean>(false);
  @Input()
  set readonly(val: boolean) {
    this._readonly.set(!!val);
  }
  get readonly(): boolean {
    return this._readonly();
  }

  private readonly _serverErrors = signal<Record<string, string>>({});
  @Input()
  set serverErrors(val: Record<string, string>) {
    this._serverErrors.set(val || {});
  }
  get serverErrors(): Record<string, string> {
    return this._serverErrors();
  }

  @Input() columns: number = 2;
  @Input() loading: boolean = false;

  @Output() fieldChange = new EventEmitter<{ field: string; value: any }>();

  readonly visibleFields = computed<FormFieldConfig[]>(() => {
    return (this._fields() || []).filter(f => f.visible !== false);
  });

  isFieldDisabled(field: FormFieldConfig): boolean {
    if (this._readonly() || field.readonly) return true;
    if (field.disabled) return true;
    const ctrl = this._form()?.get(field.name);
    return ctrl?.disabled ?? false;
  }

  isFieldTouched(fieldName: string): boolean {
    const ctrl = this._form()?.get(fieldName);
    return !!(ctrl?.touched || ctrl?.dirty);
  }

  isFieldInvalid(fieldName: string): boolean {
    const sErrors = this._serverErrors();
    if (sErrors && sErrors[fieldName]) {
      return true;
    }
    const ctrl = this._form()?.get(fieldName);
    if (!ctrl) return false;
    return this.isFieldTouched(fieldName) && ctrl.invalid;
  }

  getFieldError(fieldName: string): string | null {
    const sErrors = this._serverErrors();
    if (sErrors && sErrors[fieldName]) {
      return sErrors[fieldName];
    }
    const ctrl = this._form()?.get(fieldName);
    if (!ctrl || !ctrl.errors) return null;

    if (ctrl.errors['required']) {
      return 'This field is required.';
    }
    if (ctrl.errors['min']) {
      return `Minimum allowed value is ${ctrl.errors['min'].min}.`;
    }
    if (ctrl.errors['max']) {
      return `Maximum allowed value is ${ctrl.errors['max'].max}.`;
    }
    if (ctrl.errors['email']) {
      return 'Please enter a valid email address.';
    }
    if (ctrl.errors['minlength']) {
      return `Must be at least ${ctrl.errors['minlength'].requiredLength} characters.`;
    }
    if (ctrl.errors['pattern']) {
      return 'Invalid format.';
    }
    if (ctrl.errors['custom']) {
      return String(ctrl.errors['custom']);
    }
    return 'Invalid field value.';
  }
}
