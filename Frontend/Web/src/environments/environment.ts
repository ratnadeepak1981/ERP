import { validateEnvironmentConfig } from './environment.validator';

export const environment = {
  production: false,
  apiBaseUrl: '/api',
  apiUrl: 'https://localhost:7202/api',
  allowHttpDevFallback: false,
  httpFallbackUrl: 'http://localhost:5182/api'
} as const;

validateEnvironmentConfig(environment);
