import { describe, it, expect, beforeEach } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { WorkspaceScopeService } from './workspace-scope.service';
import { CompanyService } from './company.service';
import { AuthService } from '../auth/auth.service';
import { of } from 'rxjs';
import { Company, Branch } from '../models/scope.models';

describe('WorkspaceScopeService', () => {
  let service: WorkspaceScopeService;
  let companyService: CompanyService;
  let authService: AuthService;

  const mockCompanies: Company[] = [
    { id: 'c1', tenantId: 't1', name: 'Alpha Corp', code: 'ALP', isActive: true },
    { id: 'c2', tenantId: 't1', name: 'Beta Ltd', code: 'BET', isActive: true }
  ];

  const mockBranches: Branch[] = [
    { id: 'b1', companyId: 'c1', name: 'Colombo Main', code: 'CMB', isActive: true },
    { id: 'b2', companyId: 'c1', name: 'Kandy Plant', code: 'KDY', isActive: true }
  ];

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        WorkspaceScopeService,
        CompanyService,
        AuthService,
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    });

    service = TestBed.inject(WorkspaceScopeService);
    companyService = TestBed.inject(CompanyService);
    authService = TestBed.inject(AuthService);
  });

  it('should be created and have initial defaults', () => {
    expect(service).toBeTruthy();
    expect(service.companies()).toEqual([]);
    expect(service.selectedCompany()).toBeNull();
    expect(service.isAllBranches()).toBe(true);
  });

  it('should select company and load corresponding branches', () => {
    const getBranchesSpy = vi.spyOn(companyService, 'getBranches').mockReturnValue(of(mockBranches));

    service.selectCompany(mockCompanies[0]);

    expect(service.selectedCompany()?.id).toBe('c1');
    expect(getBranchesSpy).toHaveBeenCalledWith('c1');
    expect(service.branches()).toEqual(mockBranches);
    expect(service.selectedBranch()?.id).toBe('b1');
  });

  it('should allow selecting all permitted branches', () => {
    service.selectCompany(mockCompanies[0]);
    service.selectAllBranches();

    expect(service.selectedBranch()).toBeNull();
    expect(service.isAllBranches()).toBe(true);
    expect(service.currentScopeDescription()).toContain('All permitted branches');
  });

  it('should update current scope description when branch is chosen', () => {
    vi.spyOn(companyService, 'getBranches').mockReturnValue(of(mockBranches));
    service.selectCompany(mockCompanies[0]);
    service.selectBranch(mockBranches[1]);

    expect(service.selectedBranch()?.name).toBe('Kandy Plant');
    expect(service.isAllBranches()).toBe(false);
    expect(service.currentScopeDescription()).toBe('Alpha Corp / Kandy Plant');
  });
});
