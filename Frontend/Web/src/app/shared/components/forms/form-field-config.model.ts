export type FormFieldType =
  | 'text'
  | 'number'
  | 'date'
  | 'select'
  | 'autocomplete'
  | 'textarea'
  | 'checkbox'
  | 'static';

export interface FormFieldOption {
  label: string;
  value: any;
  disabled?: boolean;
  secondaryLabel?: string;
  meta?: any;
}

export interface FormFieldConfig {
  name: string;
  label: string;
  type: FormFieldType;
  placeholder?: string;
  required?: boolean;
  disabled?: boolean;
  readonly?: boolean;
  hint?: string;
  options?: FormFieldOption[];
  colSpan?: number;
  prefix?: string;
  suffix?: string;
  min?: number;
  max?: number;
  step?: number;
  rows?: number;
  tooltip?: string;
  visible?: boolean;
  autocompleteFilter?: (query: string) => FormFieldOption[];
}
