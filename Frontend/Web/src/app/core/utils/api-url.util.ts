import { environment } from '../../../environments/environment';

/**
 * Builds an API URL by joining the configured API base URL with an endpoint path.
 * Correctly handles trailing and leading slashes to prevent malformed URL paths.
 *
 * @param path - The relative endpoint path (e.g. '/SubscriptionPlan' or 'Tenant/tenant-registration-test')
 * @param baseUrl - Optional base URL override (defaults to environment.apiBaseUrl)
 * @returns Cleanly joined URL string
 */
export function buildApiUrl(path: string, baseUrl: string = environment.apiBaseUrl): string {
  if (!path) {
    return baseUrl;
  }

  const normalizedBase = baseUrl.replace(/\/+$/, '');
  const normalizedPath = path.replace(/^\/+/, '');

  if (!normalizedBase) {
    return `/${normalizedPath}`;
  }

  return `${normalizedBase}/${normalizedPath}`;
}
