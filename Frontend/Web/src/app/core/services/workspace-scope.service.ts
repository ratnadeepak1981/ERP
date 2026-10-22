import { Injectable, signal, computed, inject } from '@angular/core';
import { Subject } from 'rxjs';
import { CompanyService } from './company.service';
import { AuthService } from '../auth/auth.service';
import { Company, Branch, WorkspaceScope } from '../models/scope.models';

const STORAGE_KEY_COMPANY = 'apex_erp_active_company_id';
const STORAGE_KEY_BRANCH = 'apex_erp_active_branch_id';

@Injectable({
  providedIn: 'root'
})
export class WorkspaceScopeService {
  private readonly companyService = inject(CompanyService);
  private readonly authService = inject(AuthService);

  readonly companies = signal<Company[]>([]);
  readonly branches = signal<Branch[]>([]);
  readonly selectedCompany = signal<Company | null>(null);
  readonly selectedBranch = signal<Branch | null>(null);
  readonly isAllBranches = signal<boolean>(true);
  readonly loading = signal<boolean>(false);
  readonly error = signal<string | null>(null);

  // Observable notification for subscribers when scope changes
  private readonly scopeChangeSubject = new Subject<WorkspaceScope>();
  readonly scopeChange$ = this.scopeChangeSubject.asObservable();

  readonly activeTenantName = computed(() => {
    const user = this.authService.currentUser();
    if (!user) return 'Alpha Manufacturing Ltd';
    if (user.tenantId === '11111111-1111-1111-1111-111111111111') return 'Tenant Alpha';
    if (user.tenantId === '22222222-2222-2222-2222-222222222222') return 'Tenant Beta';
    return user.username ? `${user.username} Tenant` : 'Alpha Manufacturing Ltd';
  });

  readonly currentScopeDescription = computed<string>(() => {
    const company = this.selectedCompany();
    const branch = this.selectedBranch();
    if (!company) {
      return 'Tenant-wide Workspace';
    }
    if (this.isAllBranches() || !branch) {
      return `${company.name} / All permitted branches`;
    }
    return `${company.name} / ${branch.name}`;
  });

  readonly hasCompany = computed<boolean>(() => !!this.selectedCompany());
  readonly hasBranch = computed<boolean>(() => !this.isAllBranches() && !!this.selectedBranch());
  readonly canOperateTransactions = computed<boolean>(() => !!this.selectedCompany() && !this.isAllBranches() && !!this.selectedBranch());
  readonly canAccessCompanyOperations = computed<boolean>(() => !!this.selectedCompany());

  readonly scopePrompt = computed<{ message: string; severity: 'warning' | 'info' | 'success'; canTransact: boolean }>(() => {
    if (!this.selectedCompany()) {
      return {
        message: 'Select a company to continue. Company-specific operations are disabled until an operating company is selected.',
        severity: 'warning',
        canTransact: false
      };
    }
    if (this.isAllBranches() || !this.selectedBranch()) {
      return {
        message: 'Company-wide view active. Branch selection required before branch-specific transactions.',
        severity: 'info',
        canTransact: false
      };
    }
    return {
      message: `Operating scope verified: ${this.selectedCompany()?.name} / ${this.selectedBranch()?.name}`,
      severity: 'success',
      canTransact: true
    };
  });

  readonly activeScope = computed<WorkspaceScope>(() => {
    return {
      tenantId: this.authService.tenantId(),
      tenantName: this.activeTenantName(),
      company: this.selectedCompany(),
      branch: this.selectedBranch(),
      isAllBranches: this.isAllBranches()
    };
  });

  initializeScope(): void {
    if (!this.authService.isAuthenticated()) {
      return;
    }
    this.loading.set(true);
    this.error.set(null);

    this.companyService.getCompanies().subscribe({
      next: (companies) => {
        this.loading.set(false);
        this.companies.set(companies);

        if (companies.length === 0) {
          this.selectedCompany.set(null);
          this.branches.set([]);
          this.selectedBranch.set(null);
          this.isAllBranches.set(true);
          this.emitScopeChange();
          return;
        }

        // Check if there was a saved company ID in storage
        const savedCompanyId = this.getFromStorage(STORAGE_KEY_COMPANY);
        let targetCompany = companies.find(c => c.id === savedCompanyId);
        if (!targetCompany) {
          targetCompany = companies[0];
        }

        this.selectCompany(targetCompany, true);
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(err?.error?.message || 'Unable to load company access permissions.');
      }
    });
  }

  selectCompany(company: Company | null, emit = true): void {
    this.selectedCompany.set(company);
    this.saveToStorage(STORAGE_KEY_COMPANY, company?.id ?? null);

    if (!company) {
      this.branches.set([]);
      this.selectedBranch.set(null);
      this.isAllBranches.set(true);
      const company = this.selectedCompany();
    if (company) {
      this.saveToStorage(`${STORAGE_KEY_BRANCH}_${company.id}`, null);
    }
    this.saveToStorage(STORAGE_KEY_BRANCH, null);
      if (emit) this.emitScopeChange();
      return;
    }

    this.loading.set(true);
    this.companyService.getBranches(company.id).subscribe({
      next: (branches) => {
        this.loading.set(false);
        this.branches.set(branches);

        const companyBranchKey = `${STORAGE_KEY_BRANCH}_${company.id}`;
        const savedBranchId = this.getFromStorage(companyBranchKey) || this.getFromStorage(STORAGE_KEY_BRANCH);
        const targetBranch = branches.find(b => b.id === savedBranchId);

        if (targetBranch) {
          this.selectedBranch.set(targetBranch);
          this.isAllBranches.set(false);
        } else if (branches.length > 0) {
          // Default to first branch if available
          this.selectedBranch.set(branches[0]);
          this.isAllBranches.set(false);
        } else {
          this.selectedBranch.set(null);
          this.isAllBranches.set(true);
        }

        if (emit) this.emitScopeChange();
      },
      error: (err) => {
        this.loading.set(false);
        this.branches.set([]);
        this.selectedBranch.set(null);
        this.isAllBranches.set(true);
        this.error.set(err?.error?.message || 'Unable to load branch access permissions.');
        if (emit) this.emitScopeChange();
      }
    });
  }

  selectBranch(branch: Branch | null): void {
    this.selectedBranch.set(branch);
    this.isAllBranches.set(!branch);
    const company = this.selectedCompany();
    if (company) {
      this.saveToStorage(`${STORAGE_KEY_BRANCH}_${company.id}`, branch?.id ?? null);
    }
    this.saveToStorage(STORAGE_KEY_BRANCH, branch?.id ?? null);
    this.emitScopeChange();
  }

  selectAllBranches(): void {
    this.selectedBranch.set(null);
    this.isAllBranches.set(true);
    const company = this.selectedCompany();
    if (company) {
      this.saveToStorage(`${STORAGE_KEY_BRANCH}_${company.id}`, null);
    }
    this.saveToStorage(STORAGE_KEY_BRANCH, null);
    this.emitScopeChange();
  }

  private emitScopeChange(): void {
    this.scopeChangeSubject.next(this.activeScope());
  }

  private saveToStorage(key: string, val: string | null): void {
    try {
      if (typeof window !== 'undefined' && typeof localStorage !== 'undefined') {
        if (val) {
          localStorage.setItem(key, val);
        } else {
          localStorage.removeItem(key);
        }
      }
    } catch {
      // Ignore storage errors
    }
  }

  private getFromStorage(key: string): string | null {
    try {
      if (typeof window !== 'undefined' && typeof localStorage !== 'undefined') {
        return localStorage.getItem(key);
      }
    } catch {
      // Ignore storage errors
    }
    return null;
  }
}
