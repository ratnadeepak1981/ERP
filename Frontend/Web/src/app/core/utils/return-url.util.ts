/**
 * Validates a candidate return URL to ensure it is a safe, internal application route.
 *
 * Rules enforced:
 * - Must be a non-empty string.
 * - Must begin with exactly one '/' and NOT followed by '/' or '\'.
 * - Must NOT contain any backslashes (prevents Windows / browser scheme bypasses).
 * - Must NOT contain protocol schemes (e.g. 'http:', 'https:', 'javascript:', 'data:').
 * - Must NOT contain control characters (ASCII 0x00 - 0x1F, 0x7F) or embedded whitespace.
 * - Excludes authentication loop endpoints: '/login' and '/access-denied'
 *   (including trailing slashes, query strings, and fragments), but preserves legitimate
 *   unrelated routes such as '/login-history'.
 * - Preserves internal query parameters and fragments on valid routes (e.g. '/items?page=2#top').
 *
 * @param url - Candidate URL to validate
 * @returns true if the URL is safe for internal client navigation, false otherwise
 */
export function isSafeReturnUrl(url: string | null | undefined): boolean {
  if (!url || typeof url !== 'string') {
    return false;
  }

  const trimmed = url.trim();
  if (!trimmed) {
    return false;
  }

  // Reject control characters (0x00 - 0x1F, 0x7F) and any whitespace inside the URL
  // eslint-disable-next-line no-control-regex
  if (/[\x00-\x1F\x7F\s]/.test(trimmed)) {
    return false;
  }

  // Must begin with exactly one '/' and not followed by another '/' or '\'
  if (!trimmed.startsWith('/') || trimmed.startsWith('//') || trimmed.startsWith('/\\')) {
    return false;
  }

  // Reject any backslashes anywhere in the candidate URL
  if (trimmed.includes('\\')) {
    return false;
  }

  // Reject URL schemes (e.g. 'javascript:', 'data:', 'http:', 'https:', 'vbscript:', 'file:')
  if (/[a-zA-Z][a-zA-Z0-9+.-]*:/.test(trimmed)) {
    return false;
  }

  // Extract the path portion prior to query string '?' or fragment '#'
  const pathPart = trimmed.split(/[?#]/)[0];

  // Normalize by removing trailing slashes (e.g. '/login/' -> '/login')
  const normalizedPath = pathPart.replace(/\/+$/, '') || '/';

  // Exclude /login and /access-denied to prevent redirection loops
  // Exact match ensures routes like /login-history or /access-denied-audit are NOT excluded
  if (normalizedPath === '/login' || normalizedPath === '/access-denied') {
    return false;
  }

  return true;
}

/**
 * Returns the sanitized return URL string if safe; otherwise returns null.
 *
 * @param url - Candidate return URL
 * @returns Cleaned URL string if safe, null otherwise
 */
export function sanitizeReturnUrl(url: string | null | undefined): string | null {
  if (!isSafeReturnUrl(url)) {
    return null;
  }
  return url!.trim();
}
