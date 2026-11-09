import { describe, it, expect } from 'vitest';
import { HttpErrorResponse } from '@angular/common/http';
import {
  getHttpStatus,
  isForbiddenError,
  toUserFacingErrorMessage
} from './http-error.util';

describe('http-error.util', () => {
  it('should read the HTTP status from an HttpErrorResponse', () => {
    const error = new HttpErrorResponse({ status: 403, statusText: 'Forbidden' });

    expect(getHttpStatus(error)).toBe(403);
    expect(isForbiddenError(error)).toBe(true);
  });

  it('should treat non-HTTP errors as unknown status', () => {
    expect(getHttpStatus(new Error('boom'))).toBeNull();
    expect(getHttpStatus(null)).toBeNull();
    expect(getHttpStatus(undefined)).toBeNull();
    expect(isForbiddenError(new Error('boom'))).toBe(false);
  });

  it('should never surface raw transport text for a 403 response', () => {
    const error = new HttpErrorResponse({
      status: 403,
      statusText: 'Forbidden',
      url: 'http://localhost:4200/api/warehouses'
    });

    const message = toUserFacingErrorMessage(error, 'Failed to load warehouses.');

    expect(message).not.toMatch(/http failure response/i);
    expect(message).not.toMatch(/forbidden/i);
    expect(message).not.toMatch(/localhost/);
    expect(message).toBe('You do not have permission to perform this action.');
  });

  it('should map network failures (status 0) to a safe message', () => {
    const error = new HttpErrorResponse({ status: 0 });

    const message = toUserFacingErrorMessage(error, 'Failed to load records.');

    expect(message).not.toBe('Failed to load records.');
    expect(message).toMatch(/unable to reach the server/i);
  });

  it('should map server failures (status >= 500) to a safe message', () => {
    const error = new HttpErrorResponse({
      status: 500,
      statusText: 'Internal Server Error'
    });

    const message = toUserFacingErrorMessage(error, 'Failed to load records.');

    expect(message).not.toMatch(/http failure response/i);
    expect(message).toMatch(/server could not complete/i);
  });

  it('should map 404 responses to a safe not-found message', () => {
    const error = new HttpErrorResponse({ status: 404, statusText: 'Not Found' });

    expect(toUserFacingErrorMessage(error, 'Failed to load record.')).toBe(
      'The requested record could not be found.'
    );
  });

  it('should keep safe business validation messages for 400, 409 and 422', () => {
    const error = new HttpErrorResponse({
      status: 422,
      error: { message: 'Quantity must be greater than zero.' }
    });

    expect(toUserFacingErrorMessage(error, 'Failed to save.')).toBe(
      'Quantity must be greater than zero.'
    );
  });

  it('should discard internal detail in business error bodies', () => {
    const error = new HttpErrorResponse({
      status: 400,
      error: { message: 'Http failure response for http://internal/api: 400 Bad Request' }
    });

    expect(toUserFacingErrorMessage(error, 'Failed to save.')).toBe('Failed to save.');
  });

  it('should fall back when the error has no status', () => {
    expect(toUserFacingErrorMessage({}, 'Fallback message.')).toBe('Fallback message.');
  });
});
