/**
 * Validates Angular environment configuration against security and protocol rules.
 */
export function validateEnvironmentConfig(env: {
  production: boolean;
  apiUrl?: string;
  apiBaseUrl?: string;
  allowHttpDevFallback?: boolean;
  httpFallbackUrl?: string;
}): void {
  const urlToCheck = env.apiUrl || env.apiBaseUrl;

  if (env.production) {
    if (!urlToCheck) {
      throw new Error('Production environment must specify an apiUrl.');
    }

    if (urlToCheck.startsWith('http://')) {
      throw new Error('Insecure HTTP protocol is strictly prohibited in production.');
    }

    if (!urlToCheck.startsWith('https://') && !urlToCheck.startsWith('/')) {
      throw new Error('Production apiUrl must use HTTPS protocol.');
    }

    if (env.allowHttpDevFallback) {
      throw new Error('HTTP fallback is strictly prohibited in production.');
    }
  }
}
