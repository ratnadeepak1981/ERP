import { describe, it, expect } from 'vitest';
import { isSafeReturnUrl, sanitizeReturnUrl } from './return-url.util';

describe('Return URL Security Validation (return-url.util)', () => {
  describe('Accepted legitimate internal routes', () => {
    it('should accept standard internal application paths like /dashboard', () => {
      expect(isSafeReturnUrl('/dashboard')).toBe(true);
      expect(sanitizeReturnUrl('/dashboard')).toBe('/dashboard');
    });

    it('should accept routes with legitimate query parameters and fragments', () => {
      const url = '/inventory/items?page=2#results';
      expect(isSafeReturnUrl(url)).toBe(true);
      expect(sanitizeReturnUrl(url)).toBe(url);
    });

    it('should accept /login-history and not falsely reject it', () => {
      expect(isSafeReturnUrl('/login-history')).toBe(true);
      expect(sanitizeReturnUrl('/login-history')).toBe('/login-history');
    });

    it('should accept deeply nested valid paths', () => {
      expect(isSafeReturnUrl('/app/workspace/demonstration')).toBe(true);
      expect(isSafeReturnUrl('/plans?selected=PRO')).toBe(true);
    });

    it('should accept root path /', () => {
      expect(isSafeReturnUrl('/')).toBe(true);
      expect(sanitizeReturnUrl('/')).toBe('/');
    });
  });

  describe('Excluded auth-loop routes (/login and /access-denied)', () => {
    it('should reject bare /login', () => {
      expect(isSafeReturnUrl('/login')).toBe(false);
      expect(sanitizeReturnUrl('/login')).toBeNull();
    });

    it('should reject /login with trailing slash', () => {
      expect(isSafeReturnUrl('/login/')).toBe(false);
      expect(sanitizeReturnUrl('/login/')).toBeNull();
    });

    it('should reject /login with query parameters like /login?returnUrl=/dashboard', () => {
      expect(isSafeReturnUrl('/login?returnUrl=/dashboard')).toBe(false);
      expect(sanitizeReturnUrl('/login?returnUrl=/dashboard')).toBeNull();
    });

    it('should reject /login with fragments like /login#section', () => {
      expect(isSafeReturnUrl('/login#section')).toBe(false);
      expect(sanitizeReturnUrl('/login#section')).toBeNull();
    });

    it('should reject bare /access-denied', () => {
      expect(isSafeReturnUrl('/access-denied')).toBe(false);
      expect(sanitizeReturnUrl('/access-denied')).toBeNull();
    });

    it('should reject /access-denied with trailing slash', () => {
      expect(isSafeReturnUrl('/access-denied/')).toBe(false);
      expect(sanitizeReturnUrl('/access-denied/')).toBeNull();
    });

    it('should reject /access-denied with query parameters like /access-denied?reason=forbidden', () => {
      expect(isSafeReturnUrl('/access-denied?reason=forbidden')).toBe(false);
      expect(sanitizeReturnUrl('/access-denied?reason=forbidden')).toBeNull();
    });
  });

  describe('Rejected external, protocol-relative, and dangerous scheme URLs', () => {
    it('should reject absolute URLs like https://evil.example', () => {
      expect(isSafeReturnUrl('https://evil.example')).toBe(false);
      expect(isSafeReturnUrl('http://evil.example')).toBe(false);
      expect(sanitizeReturnUrl('https://evil.example')).toBeNull();
    });

    it('should reject protocol-relative URLs like //evil.example', () => {
      expect(isSafeReturnUrl('//evil.example')).toBe(false);
      expect(isSafeReturnUrl('///evil.example')).toBe(false);
      expect(sanitizeReturnUrl('//evil.example')).toBeNull();
    });

    it('should reject backslash variations like /\\\\evil.example and other backslash payloads', () => {
      expect(isSafeReturnUrl('/\\evil.example')).toBe(false);
      expect(isSafeReturnUrl('/\\\\evil.example')).toBe(false);
      expect(isSafeReturnUrl('\\evil.example')).toBe(false);
      expect(isSafeReturnUrl('\\\\evil.example')).toBe(false);
      expect(isSafeReturnUrl('/foo\\bar')).toBe(false);
      expect(sanitizeReturnUrl('/\\evil.example')).toBeNull();
    });

    it('should reject dangerous pseudo-schemes like javascript:alert(1) and data:', () => {
      expect(isSafeReturnUrl('javascript:alert(1)')).toBe(false);
      expect(isSafeReturnUrl('/javascript:alert(1)')).toBe(false);
      expect(isSafeReturnUrl('data:text/html;base64,PHNjcmlwdD4=')).toBe(false);
      expect(isSafeReturnUrl('vbscript:msgbox(1)')).toBe(false);
      expect(sanitizeReturnUrl('javascript:alert(1)')).toBeNull();
    });
  });

  describe('Rejected malformed, empty, and control-character inputs', () => {
    it('should reject null and undefined', () => {
      expect(isSafeReturnUrl(null)).toBe(false);
      expect(isSafeReturnUrl(undefined)).toBe(false);
      expect(sanitizeReturnUrl(null)).toBeNull();
      expect(sanitizeReturnUrl(undefined)).toBeNull();
    });

    it('should reject empty or whitespace-only strings', () => {
      expect(isSafeReturnUrl('')).toBe(false);
      expect(isSafeReturnUrl('   ')).toBe(false);
      expect(isSafeReturnUrl('\t\n')).toBe(false);
      expect(sanitizeReturnUrl('')).toBeNull();
    });

    it('should reject control-character-containing inputs', () => {
      expect(isSafeReturnUrl('/dashboard\r\n/evil')).toBe(false);
      expect(isSafeReturnUrl('/dashboard\0')).toBe(false);
      expect(isSafeReturnUrl('/dash\x1bboard')).toBe(false);
      expect(isSafeReturnUrl('/dashboard \tevil')).toBe(false);
      expect(sanitizeReturnUrl('/dashboard\0')).toBeNull();
    });
  });
});
