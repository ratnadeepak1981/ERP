/**
 * Maps HTTP failures to safe, user-facing messages.
 *
 * Screens must never render raw transport text such as
 * "Http failure response for ... 403 Forbidden", backend stack traces or
 * internal URLs. HTTP 403 responses are handled separately by the screens as
 * an Access Denied state.
 */

const NETWORK_ERROR_MESSAGE = 'Unable to reach the server. Check your connection and try again.';
const SERVER_ERROR_MESSAGE = 'The server could not complete the request. Please try again later.';
const NOT_FOUND_MESSAGE = 'The requested record could not be found.';
const FORBIDDEN_MESSAGE = 'You do not have permission to perform this action.';

export function getHttpStatus(error: unknown): number | null {
  if (error && typeof error === 'object' && 'status' in error) {
    const status = (error as { status?: unknown }).status;
    if (typeof status === 'number') {
      return status;
    }
  }
  return null;
}

export function isForbiddenError(error: unknown): boolean {
  return getHttpStatus(error) === 403;
}

export function toUserFacingErrorMessage(error: unknown, fallback: string): string {
  const status = getHttpStatus(error);

  if (status === null) {
    return fallback;
  }

  if (status === 0) {
    return NETWORK_ERROR_MESSAGE;
  }

  if (status === 403) {
    return FORBIDDEN_MESSAGE;
  }

  if (status === 404) {
    return NOT_FOUND_MESSAGE;
  }

  if (status >= 500) {
    return SERVER_ERROR_MESSAGE;
  }

  // Business validation messages are safe to show for deliberate client errors.
  if (status === 400 || status === 409 || status === 422) {
    return sanitizeServerMessage(error) ?? fallback;
  }

  return fallback;
}

function sanitizeServerMessage(error: unknown): string | null {
  if (!error || typeof error !== 'object') {
    return null;
  }

  const body = (error as { error?: unknown }).error;
  if (!body || typeof body !== 'object') {
    return null;
  }

  const candidate =
    (body as { message?: unknown }).message ?? (body as { detail?: unknown }).detail;

  if (typeof candidate !== 'string') {
    return null;
  }

  const trimmed = candidate.trim();

  if (!trimmed || trimmed.length > 300) {
    return null;
  }

  if (/(http failure response|forbidden|unauthorized|exception|stack trace)/i.test(trimmed)) {
    return null;
  }

  if (/https?:\/\//i.test(trimmed)) {
    return null;
  }

  return trimmed;
}
