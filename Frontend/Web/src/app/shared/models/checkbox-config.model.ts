export type CheckboxShape = 'rounded-square' | 'square';
export type CheckboxSize = 'compact' | 'regular' | 'large' | 'small' | 'standard';
export type CheckboxColorToken = 'primary' | 'success' | 'warning' | 'danger' | 'neutral';

export interface ApexCheckboxConfig {
  shape?: CheckboxShape;
  size?: CheckboxSize;
  colorToken?: CheckboxColorToken;
  disabled?: boolean;
  readonly?: boolean;
  indeterminate?: boolean;
  label?: string;
  ariaLabel?: string;
  error?: boolean | string;
}
