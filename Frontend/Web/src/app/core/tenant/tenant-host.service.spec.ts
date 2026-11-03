import { TestBed } from '@angular/core/testing';
import { TenantHostService } from './tenant-host.service';

describe('TenantHostService', () => {
  let service: TenantHostService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [TenantHostService]
    });
    service = TestBed.inject(TenantHostService);
  });

  afterEach(() => {
    service.resetHostnameForTesting();
  });

  describe('Legacy shared host detection', () => {
    it('should classify localhost as legacy shared host', () => {
      const result = service.parseHostname('localhost');
      expect(result.hostType).toBe('legacy-shared');
      expect(result.isLegacyShared).toBe(true);
      expect(result.isTenantSpecific).toBe(false);
      expect(result.subdomain).toBeNull();
      expect(result.isValid).toBe(true);
    });

    it('should classify 127.0.0.1 as legacy shared host', () => {
      const result = service.parseHostname('127.0.0.1');
      expect(result.hostType).toBe('legacy-shared');
      expect(result.isLegacyShared).toBe(true);
      expect(result.isTenantSpecific).toBe(false);
      expect(result.subdomain).toBeNull();
      expect(result.isValid).toBe(true);
    });

    it('should classify ::1 as legacy shared host', () => {
      const result = service.parseHostname('::1');
      expect(result.hostType).toBe('legacy-shared');
      expect(result.isLegacyShared).toBe(true);
      expect(result.isTenantSpecific).toBe(false);
      expect(result.subdomain).toBeNull();
      expect(result.isValid).toBe(true);
    });
  });

  describe('Valid tenant subdomain detection', () => {
    it('should detect retail.localhost as tenant subdomain', () => {
      const result = service.parseHostname('retail.localhost');
      expect(result.hostType).toBe('tenant-subdomain');
      expect(result.isTenantSpecific).toBe(true);
      expect(result.isLegacyShared).toBe(false);
      expect(result.subdomain).toBe('retail');
      expect(result.isValid).toBe(true);
    });

    it('should detect wholesale.localhost as tenant subdomain', () => {
      const result = service.parseHostname('wholesale.localhost');
      expect(result.hostType).toBe('tenant-subdomain');
      expect(result.isTenantSpecific).toBe(true);
      expect(result.subdomain).toBe('wholesale');
      expect(result.isValid).toBe(true);
    });

    it('should detect hardware.localhost as tenant subdomain', () => {
      const result = service.parseHostname('hardware.localhost');
      expect(result.hostType).toBe('tenant-subdomain');
      expect(result.isTenantSpecific).toBe(true);
      expect(result.subdomain).toBe('hardware');
      expect(result.isValid).toBe(true);
    });

    it('should support alphanumeric subdomains with internal hyphens', () => {
      const result = service.parseHostname('company-1.localhost');
      expect(result.hostType).toBe('tenant-subdomain');
      expect(result.isTenantSpecific).toBe(true);
      expect(result.subdomain).toBe('company-1');
      expect(result.isValid).toBe(true);
    });
  });

  describe('Reserved subdomains rejection', () => {
    const reservedList = ['admin', 'api', 'platform', 'app', 'www', 'mail', 'hangfire', 'swagger', 'auth', 'support', 'billing'];

    for (const reserved of reservedList) {
      it(`should reject reserved subdomain '${reserved}.localhost'`, () => {
        const result = service.parseHostname(`${reserved}.localhost`);
        expect(result.hostType).toBe('unsupported');
        expect(result.isValid).toBe(false);
        expect(result.isTenantSpecific).toBe(false);
        expect(result.statusMessage).toContain('reserved');
      });
    }
  });

  describe('Invalid and malformed subdomain handling', () => {
    it('should reject subdomain shorter than 3 characters', () => {
      const result = service.parseHostname('ab.localhost');
      expect(result.hostType).toBe('unsupported');
      expect(result.isValid).toBe(false);
      expect(result.statusMessage).toContain('invalid format');
    });

    it('should reject subdomain starting with hyphen', () => {
      const result = service.parseHostname('-invalid.localhost');
      expect(result.hostType).toBe('unsupported');
      expect(result.isValid).toBe(false);
    });

    it('should reject subdomain ending with hyphen', () => {
      const result = service.parseHostname('invalid-.localhost');
      expect(result.hostType).toBe('unsupported');
      expect(result.isValid).toBe(false);
    });

    it('should reject subdomain with underscore', () => {
      const result = service.parseHostname('retail_store.localhost');
      expect(result.hostType).toBe('unsupported');
      expect(result.isValid).toBe(false);
    });

    it('should reject subdomain with special characters', () => {
      const result = service.parseHostname('retail@store.localhost');
      expect(result.hostType).toBe('unsupported');
      expect(result.isValid).toBe(false);
    });

    it('should handle empty or whitespace hostname', () => {
      const emptyResult = service.parseHostname('');
      expect(emptyResult.hostType).toBe('unsupported');
      expect(emptyResult.isValid).toBe(false);

      const nullResult = service.parseHostname(null);
      expect(nullResult.hostType).toBe('unsupported');
      expect(nullResult.isValid).toBe(false);
    });

    it('should reject hostname with spaces or URI components', () => {
      const spaceResult = service.parseHostname('retail store.localhost');
      expect(spaceResult.hostType).toBe('unsupported');
      expect(spaceResult.isValid).toBe(false);

      const portResult = service.parseHostname('retail.localhost:4200');
      expect(portResult.hostType).toBe('unsupported');
      expect(portResult.isValid).toBe(false);
    });
  });

  describe('Hostname normalization edge cases', () => {
    it('should normalize uppercase hostname', () => {
      const result = service.parseHostname('RETAIL.LOCALHOST');
      expect(result.normalizedHostname).toBe('retail.localhost');
      expect(result.subdomain).toBe('retail');
      expect(result.isValid).toBe(true);
    });

    it('should trim trailing dot (DNS FQDN notation)', () => {
      const result = service.parseHostname('retail.localhost.');
      expect(result.normalizedHostname).toBe('retail.localhost');
      expect(result.subdomain).toBe('retail');
      expect(result.isValid).toBe(true);
    });

    it('should trim leading and trailing whitespace', () => {
      const result = service.parseHostname('  retail.localhost.  ');
      expect(result.normalizedHostname).toBe('retail.localhost');
      expect(result.subdomain).toBe('retail');
      expect(result.isValid).toBe(true);
    });
  });

  describe('Custom domain handling', () => {
    it('should classify portal.acme-corp.com as custom domain', () => {
      const result = service.parseHostname('portal.acme-corp.com');
      expect(result.hostType).toBe('custom-domain');
      expect(result.isTenantSpecific).toBe(true);
      expect(result.subdomain).toBeNull();
      expect(result.isValid).toBe(true);
    });
  });

  describe('Service signals and test hooks', () => {
    it('should update reactive signals when setHostnameForTesting is called', () => {
      service.setHostnameForTesting('wholesale.localhost');

      expect(service.isTenantSpecific()).toBe(true);
      expect(service.isLegacyShared()).toBe(false);
      expect(service.currentSubdomain()).toBe('wholesale');
      expect(service.currentHostname()).toBe('wholesale.localhost');
      expect(service.isValidHost()).toBe(true);
    });

    it('should reset back to initial state when resetHostnameForTesting is called', () => {
      service.setHostnameForTesting('wholesale.localhost');
      expect(service.isTenantSpecific()).toBe(true);

      service.resetHostnameForTesting();
      expect(service.currentHostname()).toBe('localhost');
      expect(service.isLegacyShared()).toBe(true);
      expect(service.isTenantSpecific()).toBe(false);
    });

    it('should not contain any hardcoded tenant ID mappings', () => {
      // Security requirement: Frontend does NOT map hostnames to tenant IDs
      const state = service.parseHostname('retail.localhost');
      expect((state as any).tenantId).toBeUndefined();
    });
  });
});
