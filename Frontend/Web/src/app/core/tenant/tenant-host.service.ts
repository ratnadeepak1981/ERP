import { Injectable, signal, computed, inject } from '@angular/core';
import { DOCUMENT } from '@angular/common';
import { HostType, TenantHostState, RESERVED_SUBDOMAINS } from './tenant-host.models';

const SUBDOMAIN_REGEX = /^[a-z0-9]([a-z0-9-]{1,61}[a-z0-9])?$/;
const IP_REGEX = /^(?:[0-9]{1,3}\.){3}[0-9]{1,3}$/;

@Injectable({
  providedIn: 'root'
})
export class TenantHostService {
  private readonly document = inject(DOCUMENT, { optional: true });

  private readonly activeHostState = signal<TenantHostState>(
    this.parseHostname(this.resolveInitialHostname())
  );

  readonly hostInfo = computed(() => this.activeHostState());
  readonly hostType = computed(() => this.activeHostState().hostType);
  readonly isTenantSpecific = computed(() => this.activeHostState().isTenantSpecific);
  readonly isLegacyShared = computed(() => this.activeHostState().isLegacyShared);
  readonly currentSubdomain = computed(() => this.activeHostState().subdomain);
  readonly currentHostname = computed(() => this.activeHostState().normalizedHostname);
  readonly isValidHost = computed(() => this.activeHostState().isValid);
  readonly statusMessage = computed(() => this.activeHostState().statusMessage ?? null);

  private resolveInitialHostname(): string {
    if (this.document?.defaultView?.location?.hostname) {
      return this.document.defaultView.location.hostname;
    }
    if (typeof window !== 'undefined' && window.location?.hostname) {
      return window.location.hostname;
    }
    return 'localhost';
  }

  /**
   * Pure parsing function to detect and classify incoming hostnames.
   * Enforces RFC 1035/1123 format rules and reserved subdomain restrictions.
   * Note: The API remains authoritative for tenant resolution.
   */
  parseHostname(rawHost: string | null | undefined): TenantHostState {
    const raw = (rawHost ?? '').trim();
    if (!raw) {
      return {
        rawHostname: '',
        normalizedHostname: '',
        hostType: 'unsupported',
        subdomain: null,
        isTenantSpecific: false,
        isLegacyShared: false,
        isValid: false,
        statusMessage: 'Hostname is empty or undefined.'
      };
    }

    // Normalization: lowercase, trim whitespace and trailing dots
    const normalized = raw.toLowerCase().replace(/\.+$/, '');

    // 1. Legacy shared hosts: localhost, loopback IPs, raw IPv4/IPv6
    const unbracketed = normalized.replace(/^\[|\]$/g, '');
    if (
      unbracketed === 'localhost' ||
      unbracketed === '127.0.0.1' ||
      unbracketed === '::1' ||
      IP_REGEX.test(unbracketed)
    ) {
      return {
        rawHostname: raw,
        normalizedHostname: normalized,
        hostType: 'legacy-shared',
        subdomain: null,
        isTenantSpecific: false,
        isLegacyShared: true,
        isValid: true,
        statusMessage: null
      };
    }

    // Check for spaces, port separators, or invalid URI characters in standard hostnames
    if (/\s/.test(normalized) || /[:\/\\?#@]/.test(normalized)) {
      return {
        rawHostname: raw,
        normalizedHostname: normalized,
        hostType: 'unsupported',
        subdomain: null,
        isTenantSpecific: false,
        isLegacyShared: false,
        isValid: false,
        statusMessage: 'Hostname contains invalid characters or port separators.'
      };
    }

    // 2. Localhost subdomains: e.g. retail.localhost, wholesale.localhost
    if (normalized.endsWith('.localhost')) {
      const parts = normalized.split('.');
      if (parts.length === 2) {
        const candidateSubdomain = parts[0];

        if (RESERVED_SUBDOMAINS.has(candidateSubdomain)) {
          return {
            rawHostname: raw,
            normalizedHostname: normalized,
            hostType: 'unsupported',
            subdomain: candidateSubdomain,
            isTenantSpecific: false,
            isLegacyShared: false,
            isValid: false,
            statusMessage: `Subdomain '${candidateSubdomain}' is reserved for platform infrastructure.`
          };
        }

        if (candidateSubdomain.length < 3 || candidateSubdomain.length > 63 || !SUBDOMAIN_REGEX.test(candidateSubdomain)) {
          return {
            rawHostname: raw,
            normalizedHostname: normalized,
            hostType: 'unsupported',
            subdomain: candidateSubdomain,
            isTenantSpecific: false,
            isLegacyShared: false,
            isValid: false,
            statusMessage: `Subdomain '${candidateSubdomain}' has invalid format. Must be 3-63 alphanumeric characters.`
          };
        }

        return {
          rawHostname: raw,
          normalizedHostname: normalized,
          hostType: 'tenant-subdomain',
          subdomain: candidateSubdomain,
          isTenantSpecific: true,
          isLegacyShared: false,
          isValid: true,
          statusMessage: null
        };
      }
    }

    // 3. Domain subdomains: e.g. retail.apexerp.com
    if (normalized.endsWith('.apexerp.com')) {
      const parts = normalized.split('.');
      if (parts.length === 3) {
        const candidateSubdomain = parts[0];

        if (RESERVED_SUBDOMAINS.has(candidateSubdomain)) {
          return {
            rawHostname: raw,
            normalizedHostname: normalized,
            hostType: 'unsupported',
            subdomain: candidateSubdomain,
            isTenantSpecific: false,
            isLegacyShared: false,
            isValid: false,
            statusMessage: `Subdomain '${candidateSubdomain}' is reserved for platform infrastructure.`
          };
        }

        if (candidateSubdomain.length >= 3 && candidateSubdomain.length <= 63 && SUBDOMAIN_REGEX.test(candidateSubdomain)) {
          return {
            rawHostname: raw,
            normalizedHostname: normalized,
            hostType: 'tenant-subdomain',
            subdomain: candidateSubdomain,
            isTenantSpecific: true,
            isLegacyShared: false,
            isValid: true,
            statusMessage: null
          };
        }
      }
    }

    // 4. Custom domains: e.g. portal.acme-corp.com, retail.example.org
    if (normalized.includes('.')) {
      const segments = normalized.split('.');
      const hasValidSegments = segments.every(s => s.length > 0 && /^[a-z0-9-]+$/.test(s));
      if (hasValidSegments && segments.length >= 2) {
        return {
          rawHostname: raw,
          normalizedHostname: normalized,
          hostType: 'custom-domain',
          subdomain: null,
          isTenantSpecific: true,
          isLegacyShared: false,
          isValid: true,
          statusMessage: null
        };
      }
    }

    return {
      rawHostname: raw,
      normalizedHostname: normalized,
      hostType: 'unsupported',
      subdomain: null,
      isTenantSpecific: false,
      isLegacyShared: false,
      isValid: false,
      statusMessage: `Hostname '${raw}' is not supported.`
    };
  }

  /**
   * Testing hook to simulate running under different hostnames
   * without mutating browser window location.
   */
  setHostnameForTesting(hostname: string): void {
    this.activeHostState.set(this.parseHostname(hostname));
  }

  /**
   * Reset testing state back to default environment host.
   */
  resetHostnameForTesting(): void {
    this.activeHostState.set(this.parseHostname(this.resolveInitialHostname()));
  }
}
