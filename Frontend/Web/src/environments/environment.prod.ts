import { validateEnvironmentConfig } from './environment.validator';

export const environment = {
  production: true,
  apiBaseUrl: '/api',
  apiUrl: 'https://YOUR-PRODUCTION-DOMAIN/api',
  tenantBaseDomain: 'apexerp.com'
} as const;

validateEnvironmentConfig(environment);
