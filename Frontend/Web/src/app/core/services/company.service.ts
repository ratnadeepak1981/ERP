import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, map, of, catchError } from 'rxjs';
import { buildApiUrl } from '../utils/api-url.util';
import { ApiResponse } from '../models/auth.models';
import { Company, Branch } from '../models/scope.models';
import { AuthService } from '../auth/auth.service';

const LOCAL_COMPANIES_PREFIX = 'apex_erp_tenant_companies_';
const LOCAL_BRANCHES_PREFIX = 'apex_erp_company_branches_';

@Injectable({
  providedIn: 'root'
})
export class CompanyService {
  private readonly http = inject(HttpClient);
  private readonly authService = inject(AuthService, { optional: true });

  private getTenantStorageKey(): string {
    const user = this.authService?.currentUser?.();
    return user?.tenantId || user?.username || 'default';
  }

  getCompanies(): Observable<Company[]> {
    return this.http
      .get<ApiResponse<Company[] | { items: Company[] }>>(buildApiUrl('/companies'))
      .pipe(
        map(res => {
          let list: Company[] = [];
          if (res?.data) {
            if (Array.isArray(res.data)) {
              list = res.data;
            } else if ((res.data as any).items) {
              list = (res.data as any).items;
            }
          }
          // Return what the backend provides. Do NOT fall back to localStorage
          // on a successful response (even empty): stale localStorage entries
          // have fake IDs that cause every scope-gated API call to return 403.
          return list;
        }),
        catchError((err) => {
          // Only use localStorage on genuine network errors (status 0 / no response).
          // For 401/403/5xx the backend responded — return [] so the UI shows
          // "no company" rather than a stale company with a fake local ID.
          if (err?.status === 0 || err?.status == null) {
            return of(this.getLocalCompanies());
          }
          return of([]);
        })
      );
  }

  getCompany(companyId: string): Observable<Company | null> {
    return this.http
      .get<ApiResponse<Company>>(buildApiUrl(`/companies/${companyId}`))
      .pipe(
        map(res => res?.data ?? null),
        catchError(() => {
          const comps = this.getLocalCompanies();
          return of(comps.find(c => c.id === companyId) || null);
        })
      );
  }

  getBranches(companyId: string): Observable<Branch[]> {
    return this.http
      .get<ApiResponse<Branch[] | { items: Branch[] }>>(buildApiUrl(`/companies/${companyId}/branches`))
      .pipe(
        map(res => {
          let list: Branch[] = [];
          if (res?.data) {
            if (Array.isArray(res.data)) {
              list = res.data;
            } else if ((res.data as any).items) {
              list = (res.data as any).items;
            }
          }
          if (list.length > 0) {
            return list;
          }
          return this.getLocalBranches(companyId);
        }),
        catchError(() => {
          return of(this.getLocalBranches(companyId));
        })
      );
  }

  getBranch(companyId: string, branchId: string): Observable<Branch | null> {
    return this.http
      .get<ApiResponse<Branch>>(buildApiUrl(`/companies/${companyId}/branches/${branchId}`))
      .pipe(
        map(res => res?.data ?? null),
        catchError(() => {
          const brs = this.getLocalBranches(companyId);
          return of(brs.find(b => b.id === branchId) || null);
        })
      );
  }

  createCompany(dto: { name: string; code: string; currency?: string }): Observable<Company> {
    const key = this.getTenantStorageKey();
    const newCompany: Company = {
      id: 'comp-' + Math.random().toString(36).substring(2, 9),
      tenantId: this.authService?.tenantId() || 'tenant-local',
      name: dto.name.trim(),
      code: dto.code.trim().toUpperCase(),
      isActive: true,
      currency: dto.currency || 'USD'
    };

    const companies = this.getLocalCompanies();
    companies.push(newCompany);
    this.saveLocalCompanies(companies);

    return of(newCompany);
  }

  createBranch(companyId: string, dto: { name: string; code: string }): Observable<Branch> {
    const newBranch: Branch = {
      id: 'br-' + Math.random().toString(36).substring(2, 9),
      companyId,
      name: dto.name.trim(),
      code: dto.code.trim().toUpperCase(),
      isActive: true
    };

    const branches = this.getLocalBranches(companyId);
    branches.push(newBranch);
    this.saveLocalBranches(companyId, branches);

    return of(newBranch);
  }

  initializeDefaultCompanyAndBranch(
    tenantDisplayName?: string,
    customParams?: {
      companyName?: string;
      companyCode?: string;
      currency?: string;
      branchName?: string;
      branchCode?: string;
    }
  ): Observable<{ company: Company; branch: Branch }> {
    const namePrefix = customParams?.companyName || (tenantDisplayName ? `${tenantDisplayName} (Pvt) Ltd` : 'Operating Enterprise (Pvt) Ltd');
    const compCode = customParams?.companyCode || ((tenantDisplayName?.replace(/[^A-Za-z0-9]/g, '').substring(0, 4).toUpperCase() || 'MAIN') + '-MFG');
    const currency = customParams?.currency || 'USD';
    const brName = customParams?.branchName || (tenantDisplayName ? `${tenantDisplayName} Main Branch` : 'Main Headquarters');
    const brCode = customParams?.branchCode || ((tenantDisplayName?.replace(/[^A-Za-z0-9]/g, '').substring(0, 4).toUpperCase() || 'MAIN') + '-BR01');

    const company: Company = {
      id: 'comp-' + Math.random().toString(36).substring(2, 9),
      tenantId: this.authService?.tenantId() || 'tenant-local',
      name: namePrefix,
      code: compCode,
      isActive: true,
      currency: currency
    };

    const branch: Branch = {
      id: 'br-' + Math.random().toString(36).substring(2, 9),
      companyId: company.id,
      name: brName,
      code: brCode,
      isActive: true
    };

    const companies = this.getLocalCompanies();
    companies.push(company);
    this.saveLocalCompanies(companies);

    const branches = this.getLocalBranches(company.id);
    branches.push(branch);
    this.saveLocalBranches(company.id, branches);

    return of({ company, branch });
  }

  private getLocalCompanies(): Company[] {
    try {
      if (typeof window !== 'undefined' && typeof localStorage !== 'undefined') {
        const stored = localStorage.getItem(LOCAL_COMPANIES_PREFIX + this.getTenantStorageKey());
        if (stored) {
          return JSON.parse(stored) as Company[];
        }
      }
    } catch {
      // Ignore storage errors
    }
    return [];
  }

  private saveLocalCompanies(companies: Company[]): void {
    try {
      if (typeof window !== 'undefined' && typeof localStorage !== 'undefined') {
        localStorage.setItem(LOCAL_COMPANIES_PREFIX + this.getTenantStorageKey(), JSON.stringify(companies));
      }
    } catch {
      // Ignore storage errors
    }
  }

  private getLocalBranches(companyId: string): Branch[] {
    try {
      if (typeof window !== 'undefined' && typeof localStorage !== 'undefined') {
        const stored = localStorage.getItem(LOCAL_BRANCHES_PREFIX + companyId);
        if (stored) {
          return JSON.parse(stored) as Branch[];
        }
      }
    } catch {
      // Ignore storage errors
    }
    return [];
  }

  private saveLocalBranches(companyId: string, branches: Branch[]): void {
    try {
      if (typeof window !== 'undefined' && typeof localStorage !== 'undefined') {
        localStorage.setItem(LOCAL_BRANCHES_PREFIX + companyId, JSON.stringify(branches));
      }
    } catch {
      // Ignore storage errors
    }
  }
}

