export type HostType = 'legacy-shared' | 'tenant-subdomain' | 'custom-domain' | 'unsupported';

export interface TenantHostState {
  rawHostname: string;
  normalizedHostname: string;
  hostType: HostType;
  subdomain: string | null;
  isTenantSpecific: boolean;
  isLegacyShared: boolean;
  isValid: boolean;
  statusMessage?: string | null;
}

export const RESERVED_SUBDOMAINS: ReadonlySet<string> = new Set([
  'admin',
  'api',
  'platform',
  'app',
  'www',
  'mail',
  'hangfire',
  'swagger',
  'auth',
  'support',
  'billing',
  'login',
  'root',
  'dev',
  'test'
]);
