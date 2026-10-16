export interface LookupItem<T = any> {
  id: string | number;
  code: string;
  name: string;
  category?: string;
  description?: string;
  status?: string;
  isEligible?: boolean;
  meta?: T;
}
