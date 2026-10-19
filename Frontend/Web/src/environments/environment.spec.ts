import { describe, it, expect } from 'vitest';
import { environment as devEnvironment } from './environment';
import { environment as prodEnvironment } from './environment.prod';
import { buildApiUrl } from '../app/core/utils/api-url.util';

describe('Angular Environment Configuration', () => {
  describe('Environment file properties', () => {
    it('should have development environment configured with production=false and apiBaseUrl=/api', () => {
      expect(devEnvironment.production).toBe(false);
      expect(devEnvironment.apiBaseUrl).toBe('/api');
    });

    it('should have production environment configured with production=true and apiBaseUrl=/api', () => {
      expect(prodEnvironment.production).toBe(true);
      expect(prodEnvironment.apiBaseUrl).toBe('/api');
    });

    it('should align apiBaseUrl with dev proxy.conf.json route context', () => {
      // proxy.conf.json proxies "/api" to http://localhost:5182
      expect(devEnvironment.apiBaseUrl.startsWith('/api')).toBe(true);
    });
  });

  describe('API URL Construction (buildApiUrl)', () => {
    it('should use default environment.apiBaseUrl when no base is passed', () => {
      const url = buildApiUrl('/SubscriptionPlan');
      expect(url).toBe('/api/SubscriptionPlan');
    });

    it('should normalize leading and trailing slashes correctly', () => {
      expect(buildApiUrl('SubscriptionPlan')).toBe('/api/SubscriptionPlan');
      expect(buildApiUrl('/SubscriptionPlan')).toBe('/api/SubscriptionPlan');
      expect(buildApiUrl('///SubscriptionPlan')).toBe('/api/SubscriptionPlan');
      expect(buildApiUrl('/SubscriptionPlan', '/api/')).toBe('/api/SubscriptionPlan');
      expect(buildApiUrl('/SubscriptionPlan', '/api///')).toBe('/api/SubscriptionPlan');
    });

    it('should support custom base URLs for external or staging gateways', () => {
      const customGateway = 'https://api.apexerp.cloud/v1';
      expect(buildApiUrl('/SubscriptionPlan', customGateway)).toBe('https://api.apexerp.cloud/v1/SubscriptionPlan');
      expect(buildApiUrl('Tenant/tenant-registration-test', customGateway + '/')).toBe('https://api.apexerp.cloud/v1/Tenant/tenant-registration-test');
    });

    it('should preserve query parameters during URL construction', () => {
      const urlWithQuery = buildApiUrl('/Security/login-test?username=admin&password=123');
      expect(urlWithQuery).toBe('/api/Security/login-test?username=admin&password=123');
    });

    it('should handle edge cases like empty paths and blank bases', () => {
      expect(buildApiUrl('')).toBe('/api');
      expect(buildApiUrl('/test', '')).toBe('/test');
    });
  });

  describe('Existing Endpoint Path Integrity', () => {
    it('should construct correct existing endpoints for SubscriptionPlanService', () => {
      const planId = '11111111-2222-3333-4444-555555555555';
      expect(buildApiUrl('/SubscriptionPlan')).toBe('/api/SubscriptionPlan');
      expect(buildApiUrl(`/SubscriptionPlan/${planId}`)).toBe(`/api/SubscriptionPlan/${planId}`);
    });

    it('should construct correct existing endpoint for TenantRegistrationService', () => {
      expect(buildApiUrl('/Tenant/tenant-registration-test')).toBe('/api/Tenant/tenant-registration-test');
    });

    it('should construct correct existing endpoints for AuthService', () => {
      expect(buildApiUrl('/Security/login')).toBe('/api/Security/login');
      expect(buildApiUrl('/Security/status')).toBe('/api/Security/status');
      expect(buildApiUrl('/Security/protected-test')).toBe('/api/Security/protected-test');
      expect(buildApiUrl('/Security/platform-admin-test')).toBe('/api/Security/platform-admin-test');
      expect(buildApiUrl('/Security/diagnostic')).toBe('/api/Security/diagnostic');
    });

    it('should construct identical paths when evaluated against production environment configuration', () => {
      expect(buildApiUrl('/SubscriptionPlan', prodEnvironment.apiBaseUrl)).toBe('/api/SubscriptionPlan');
      expect(buildApiUrl('/Tenant/tenant-registration-test', prodEnvironment.apiBaseUrl)).toBe('/api/Tenant/tenant-registration-test');
      expect(buildApiUrl('/Security/login', prodEnvironment.apiBaseUrl)).toBe('/api/Security/login');
    });
  });
});
