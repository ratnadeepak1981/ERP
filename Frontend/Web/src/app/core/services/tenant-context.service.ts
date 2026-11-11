import { Injectable, signal, computed, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { buildApiUrl } from '../utils/api-url.util';

export interface TenantContextDto {
  isTenant: boolean;
  tenantId: string | null;
  name: string;
  subdomain: string | null;
  industryType: string | null;
  companyStructure: string | null;
  eCommerceEnabled: boolean;
}

export interface ApiResponse<T> {
  succeeded: boolean;
  message: string;
  data: T;
  statusCode: number;
}

@Injectable({
  providedIn: 'root'
})
export class TenantContextService {
  private readonly http = inject(HttpClient);

  readonly subdomain = signal<string | null>(this.extractSubdomainFromWindow());
  readonly isTenantDomain = computed(() => !!this.subdomain());
  readonly currentTenant = signal<TenantContextDto | null>(null);
  readonly isLoading = signal<boolean>(false);
  readonly isNotFound = signal<boolean>(false);
  readonly errorMessage = signal<string | null>(null);

  constructor() {
    if (this.isTenantDomain()) {
      this.loadTenantContext();
    }
  }

  /**
   * Resolves the tenant subdomain from the browser's current window.location.hostname.
   * Supports:
   * 1. Localhost development: retail.localhost -> 'retail'
   * 2. LAN demo (wildcard IP DNS): retail.192.168.1.5.nip.io -> 'retail'
   * 3. Configured base domain: retail.apexerp.com -> 'retail'
   * 4. Platform root: localhost, 192.168.1.5, apexerp.com -> null (Platform mode)
   */
  extractSubdomainFromWindow(): string | null {
    if (typeof window === 'undefined' || !window.location || !window.location.hostname) {
      return null;
    }

    return this.parseSubdomain(window.location.hostname, (environment as any).tenantBaseDomain || 'localhost');
  }

  parseSubdomain(hostname: string, baseDomain: string): string | null {
    if (!hostname) {
      return null;
    }

    const host = hostname.trim().toLowerCase();

    // Bare localhost or IP
    if (host === 'localhost' || host === '127.0.0.1' || /^(\d{1,3}\.){3}\d{1,3}$/.test(host)) {
      return null;
    }

    // Configured base domain
    const cleanBase = baseDomain.trim().toLowerCase();
    if (host === cleanBase) {
      return null;
    }

    if (host.endsWith('.' + cleanBase)) {
      const prefix = host.slice(0, -(cleanBase.length + 1));
      return prefix.split('.')[0] || null;
    }

    // Wildcard LAN IP support (.nip.io / .sslip.io)
    if (host.endsWith('.nip.io') || host.endsWith('.sslip.io')) {
      const parts = host.split('.');
      if (parts.length >= 7) {
        // e.g. [retail, 192, 168, 1, 5, nip, io]
        return parts[0] || null;
      }
      return null;
    }

    // Default localhost subdomains (e.g. retail.localhost)
    if (host.endsWith('.localhost')) {
      const prefix = host.slice(0, -'.localhost'.length);
      return prefix.split('.')[0] || null;
    }

    return null;
  }

  loadTenantContext(): void {
    const slug = this.subdomain();
    if (!slug) {
      return;
    }

    this.isLoading.set(true);
    this.errorMessage.set(null);

    this.http.get<ApiResponse<TenantContextDto>>(buildApiUrl('/Tenant/context')).subscribe({
      next: (response) => {
        this.isLoading.set(false);
        if (response?.succeeded && response.data) {
          this.currentTenant.set(response.data);
          this.isNotFound.set(false);
        } else {
          this.errorMessage.set(response?.message || 'Failed to load tenant context.');
        }
      },
      error: (err) => {
        this.isLoading.set(false);
        if (err?.status === 404) {
          this.isNotFound.set(true);
          this.errorMessage.set(`Tenant '${slug}' was not found.`);
        } else if (err?.status === 403) {
          this.errorMessage.set(`Tenant '${slug}' is inactive or suspended.`);
        } else {
          this.errorMessage.set(err?.error?.message || err?.message || 'Network error resolving tenant.');
        }
      }
    });
  }
}
