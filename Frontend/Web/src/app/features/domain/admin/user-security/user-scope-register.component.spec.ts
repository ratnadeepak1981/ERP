import { describe, it, expect, beforeEach, vi } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { of } from 'rxjs';
import { UserScopeRegisterComponent } from './user-scope-register.component';
import { UserSecurityService } from '../../../../core/services/user-security.service';
import { UserScopeAssignment, UserAccount } from '../../../../core/models/security.models';

describe('UserScopeRegisterComponent', () => {
  let component: UserScopeRegisterComponent;
  let fixture: ComponentFixture<UserScopeRegisterComponent>;
  let securityService: UserSecurityService;
  let router: Router;

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
    }
  ];

  const mockScopes: UserScopeAssignment[] = [
    {
      id: 'scp-1',
      userId: 'u-1',
      userName: 'Alice Smith',
      userEmail: 'alice@erp.lk',
      companyId: 'c-1',
      companyName: 'Apex Corp',
      branchId: 'b-1',
      branchName: 'Plant 1',
      isAllBranches: false,
      isPrimary: true,
      status: 'Authorized',
      grantedAt: '2026-01-01',
      grantedBy: 'Tenant Admin'
    },
    {
      id: 'scp-2',
      userId: 'u-1',
      userName: 'Alice Smith',
      userEmail: 'alice@erp.lk',
      companyId: 'c-2',
      companyName: 'Apex Logistics',
      branchId: '',
      branchName: 'All Facilities',
      isAllBranches: true,
      isPrimary: false,
      status: 'Authorized',
      grantedAt: '2026-01-02',
      grantedBy: 'Tenant Admin'
    }
  ];

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [UserScopeRegisterComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        UserSecurityService
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(UserScopeRegisterComponent);
    component = fixture.componentInstance;
    securityService = TestBed.inject(UserSecurityService);
    router = TestBed.inject(Router);

    vi.spyOn(securityService, 'getUserScopes').mockReturnValue(of([...mockScopes]));
    vi.spyOn(securityService, 'getUsers').mockReturnValue(of([...mockUsers]));
  });

  it('should initialize and load user scopes with metrics', () => {
    fixture.detectChanges();
    expect(component.scopes().length).toBe(2);
    expect(component.totalScopesCount()).toBe(2);
    expect(component.companyLevelCount()).toBe(1);
    expect(component.branchLevelCount()).toBe(1);
    expect(component.revokedCount()).toBe(0);
  });

  it('should filter scopes by search query', () => {
    fixture.detectChanges();
    component.searchQuery.set('Logistics');
    expect(component.filteredScopes().length).toBe(1);
    expect(component.filteredScopes()[0].companyName).toBe('Apex Logistics');
  });

  it('should revoke a user scope via revokeUserScope', () => {
    const revokeSpy = vi.spyOn(securityService, 'revokeUserScope').mockReturnValue(of(true));
    fixture.detectChanges();

    component.revokeScope(mockScopes[0]);
    expect(revokeSpy).toHaveBeenCalledWith('scp-1');
  });

  it('should navigate to assign scope page', () => {
    const navSpy = vi.spyOn(router, 'navigate');
    component.navigateToNewScope();
    expect(navSpy).toHaveBeenCalledWith(['/tenant-admin/user-scope/new']);
  });
});
