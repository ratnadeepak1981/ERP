import { describe, it, expect, beforeEach, vi } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { of, throwError } from 'rxjs';
import { UserScopePageComponent } from './user-scope-page.component';
import { UserSecurityService } from '../../../../core/services/user-security.service';
import { CompanyService } from '../../../../core/services/company.service';
import { UserAccount, UserScopeAssignment } from '../../../../core/models/security.models';

describe('UserScopePageComponent', () => {
  let component: UserScopePageComponent;
  let fixture: ComponentFixture<UserScopePageComponent>;
  let securityService: UserSecurityService;
  let companyService: CompanyService;

  const mockUsers: UserAccount[] = [
    {
      id: 'u-1',
      tenantId: 't-1',
      firstName: 'Alice',
      lastName: 'Smith',
      fullName: 'Alice Smith',
      email: 'alice@erp.lk',
      username: 'asmith',
      accountStatus: 'Active',
      roleId: 'r-1',
      roleName: 'Store Manager',
      primaryCompanyId: 'c-1',
      primaryCompanyName: 'Apex Corp',
      primaryBranchId: 'b-1',
      primaryBranchName: 'Plant 1',
      createdAt: '2026-01-01'
    },
    {
      id: 'u-2',
      tenantId: 't-1',
      firstName: 'Bob',
      lastName: 'Jones',
      fullName: 'Bob Jones',
      email: 'bob@erp.lk',
      username: 'bjones',
      accountStatus: 'Active',
      roleId: 'r-2',
      roleName: 'Procurement Specialist',
      primaryCompanyId: 'c-1',
      primaryCompanyName: 'Apex Corp',
      primaryBranchId: '',
      primaryBranchName: '',
      createdAt: '2026-01-01'
    }
  ];

  const mockScopesUser1: UserScopeAssignment[] = [
    {
      id: 'scp-1',
      userId: 'u-1',
      userName: 'Alice Smith',
      userEmail: 'alice@erp.lk',
      companyId: 'c-1',
      companyName: 'Apex Corp',
      companyCode: 'APX',
      branchId: 'b-1',
      branchName: 'Plant 1',
      branchCode: 'P1',
      isAllBranches: false,
      scopeLevel: 'Branch-specific',
      isPrimary: false,
      status: 'Authorized',
      grantedAt: '2026-01-01T00:00:00Z',
      grantedBy: 'Tenant Admin'
    }
  ];

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [UserScopePageComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        UserSecurityService,
        CompanyService,
        {
          provide: ActivatedRoute,
          useValue: {
            queryParams: of({ userId: 'u-1' })
          }
        }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(UserScopePageComponent);
    component = fixture.componentInstance;
    securityService = TestBed.inject(UserSecurityService);
    companyService = TestBed.inject(CompanyService);

    vi.spyOn(securityService, 'getUsers').mockReturnValue(of(mockUsers));
    vi.spyOn(securityService, 'getUserScopes').mockImplementation((userId) => {
      if (userId === 'u-1') return of(mockScopesUser1);
      return of([]);
    });
    vi.spyOn(companyService, 'getCompanies').mockReturnValue(of([
      { id: 'c-1', tenantId: 't-1', name: 'Apex Corp', code: 'APX', isActive: true }
    ]));
    vi.spyOn(companyService, 'getBranches').mockReturnValue(of([
      { id: 'b-1', companyId: 'c-1', name: 'Plant 1', code: 'P1', isActive: true },
      { id: 'b-2', companyId: 'c-1', name: 'Plant 2', code: 'P2', isActive: true }
    ]));
  });

  it('should initialize and load users, companies, and existing scopes for pre-selected user', () => {
    fixture.detectChanges();
    expect(component.users().length).toBe(2);
    expect(component.companies().length).toBe(1);
    expect(component.branches().length).toBe(2);
    expect(component.selectedUserId).toBe('u-1');
    expect(component.userScopes().length).toBe(1);
    expect(component.userScopes()[0].branchName).toBe('Plant 1');
  });

  it('should clear previous scopes and load new ones when switching users', () => {
    fixture.detectChanges();
    expect(component.userScopes().length).toBe(1);

    component.onUserChange('u-2');
    expect(component.selectedUserId).toBe('u-2');
    // For u-2 mock returns empty array
    expect(component.userScopes().length).toBe(0);
  });

  it('should detect duplicate active scope and prevent assignment', () => {
    fixture.detectChanges();
    component.selectedUserId = 'u-1';
    component.selectedCompanyId = 'c-1';
    component.selectedBranchId = 'b-1';
    component.isAllBranches = false;

    expect(component.currentSelectionStatus()).toBe('ACTIVE');

    component.saveScope();
    expect(component.errorMessage()).toContain('already has this exact operating scope');
  });

  it('should assign a valid new scope and refresh scopes without navigating away', () => {
    const newAssignment: UserScopeAssignment = {
      id: 'scp-2',
      userId: 'u-1',
      userName: 'Alice Smith',
      userEmail: 'alice@erp.lk',
      companyId: 'c-1',
      companyName: 'Apex Corp',
      branchId: 'b-2',
      branchName: 'Plant 2',
      isAllBranches: false,
      isPrimary: false,
      status: 'Authorized',
      grantedAt: '2026-02-01T00:00:00Z',
      grantedBy: 'Tenant Admin'
    };

    const assignSpy = vi.spyOn(securityService, 'assignUserScope').mockReturnValue(of(newAssignment));
    const loadScopesSpy = vi.spyOn(component, 'loadUserScopes');

    fixture.detectChanges();
    component.selectedUserId = 'u-1';
    component.selectedCompanyId = 'c-1';
    component.selectedBranchId = 'b-2';
    component.isAllBranches = false;
    component.scopeNotes = 'Authorized for Plant 2';

    expect(component.currentSelectionStatus()).toBe('NEW');

    component.saveScope();

    expect(assignSpy).toHaveBeenCalled();
    expect(component.successMessage()).toContain('successfully granted');
    expect(loadScopesSpy).toHaveBeenCalledWith('u-1');
  });

  it('should extract backend error message safely when assignment fails', () => {
    vi.spyOn(securityService, 'assignUserScope').mockReturnValue(
      throwError(() => ({
        status: 409,
        error: { message: 'This operating scope is already assigned to the user.' }
      }))
    );

    fixture.detectChanges();
    component.selectedUserId = 'u-1';
    component.selectedCompanyId = 'c-1';
    component.selectedBranchId = 'b-2';
    component.isAllBranches = false;

    component.saveScope();

    expect(component.errorMessage()).toBe('This operating scope is already assigned to the user.');
  });
});
