import { validateEnvironmentConfig } from './environment.validator';

export const environment = {
  production: true,
  apiBaseUrl: '/api',
  apiUrl: 'https://YOUR-PRODUCTION-DOMAIN/api'
} as const;

validateEnvironmentConfig(environment);
