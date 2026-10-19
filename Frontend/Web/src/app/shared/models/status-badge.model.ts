export type BadgeSeverity = 'success' | 'warning' | 'danger' | 'info' | 'neutral';

export interface StatusBadgeConfig {
  label: string;
  severity: BadgeSeverity;
  icon?: string;
}
